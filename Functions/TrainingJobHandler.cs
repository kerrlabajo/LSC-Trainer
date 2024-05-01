﻿using Amazon.CloudWatchLogs;
using Amazon.CloudWatchLogs.Model;
using Amazon.S3;
using Amazon.SageMaker;
using Amazon.SageMaker.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LSC_Trainer.Functions
{
    internal class TrainingJobHandler
    {
        private string prevStatusMessage = "";
        private string prevLogMessage = "";
        private int nextLogIndex = 0;
        private bool hasCustomUploads = false;
        private string datasetKey = "";
        private string s3Bucket = "";
        private bool showDialogBox = false;
        private int delay = 0;

        private AmazonSageMakerClient amazonSageMakerClient;
        private AmazonCloudWatchLogsClient cloudWatchLogsClient;
        private AmazonS3Client s3Client;
        private StartLiveTailResponse startLiveTailResponse;

        private LSC_Trainer.Functions.IFileTransferUtility transferUtility;
        private IUIUpdater uIUpdater;

        private System.Timers.Timer timer;
        public TrainingJobHandler(AmazonSageMakerClient amazonSageMakerClient, AmazonCloudWatchLogsClient cloudWatchLogsClient, AmazonS3Client s3Client, LSC_Trainer.Functions.IFileTransferUtility fileTransferUtility, IUIUpdater uIUpdater)
        {
            this.amazonSageMakerClient = amazonSageMakerClient;
            this.cloudWatchLogsClient = cloudWatchLogsClient;
            this.s3Client = s3Client;
            this.transferUtility = fileTransferUtility;
            this.uIUpdater = uIUpdater;
        }

        public async Task<bool> StartTrackingTrainingJob(string trainingJobName, string datasetKey, string s3Bucket, bool hasCustomUploads)
        {
            try
            {
                this.datasetKey = datasetKey;
                this.s3Bucket = s3Bucket;
                this.hasCustomUploads = hasCustomUploads;
                var completionSource = new TaskCompletionSource<bool>();
                timer = InitializeTimer(trainingJobName, completionSource);
                timer.Start();

                if(await completionSource.Task)
                {
                    timer.Stop();
                    await HandleCustomUploads();
                };

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error in training model: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        private System.Timers.Timer InitializeTimer(string trainingJobName, TaskCompletionSource<bool> completionSource)
        {
            // Create a Timer instance with a specified interval (e.g., every 5 secs)
            var timerInterval = 1000;
            var timer = new System.Timers.Timer(timerInterval);
            timer.Elapsed += async (sender, e) => await CheckTrainingJobStatus(amazonSageMakerClient, trainingJobName, completionSource);

            return timer;
        }
        private async Task CheckTrainingJobStatus(AmazonSageMakerClient amazonSageMakerClient, object state, TaskCompletionSource<bool> completionSource)
        {
            try
            {
                var trainingJobName = (string)state;
                DescribeTrainingJobResponse trainingDetails = await GetTrainingJobDetails(amazonSageMakerClient, trainingJobName);
                var trainingStatus = trainingDetails.TrainingJobStatus;

                TimeSpan timeSpan = TimeSpan.FromSeconds(trainingDetails.TrainingTimeInSeconds);
                string formattedTime = timeSpan.ToString(@"hh\:mm\:ss");

                if (trainingStatus == TrainingJobStatus.InProgress)
                {
                    await HandleInProgressStatus(trainingDetails, trainingJobName);
                    if(startLiveTailResponse == null)
                    {
                        if(trainingDetails.SecondaryStatusTransitions.Any() && trainingDetails.SecondaryStatusTransitions.Last().Status == "Training")
                        {
                            string logStreamName = await GetLatestLogStream(cloudWatchLogsClient, "/aws/sagemaker/TrainingJobs", trainingJobName);
                            if(!string.IsNullOrEmpty(logStreamName))
                                await StartLiveTail(cloudWatchLogsClient, "arn:aws:logs:ap-southeast-1:905418164808:log-group:/aws/sagemaker/TrainingJobs:", logStreamName);
                        }
                        
                    }
                }
                else if(trainingStatus == TrainingJobStatus.Completed)
                {
                    if (!completionSource.Task.IsCompleted)
                    {
                        completionSource.SetResult(true);
                        
                    }
                }
                else if(trainingStatus == TrainingJobStatus.Failed)
                {
                    uIUpdater.DisplayLogMessage($"Training job failed: {trainingDetails.FailureReason}");
                    if (!completionSource.Task.IsCompleted)
                    {
                        completionSource.SetResult(true);
                    }
                }
                else
                {
                    uIUpdater.DisplayLogMessage($"Training job stopped or in an unknown state.");
                    if (!completionSource.Task.IsCompleted)
                    {
                        completionSource.SetResult(true);
                    }
                }

                if (completionSource.Task.IsCompleted){
                    uIUpdater.UpdateTrainingStatus(
                        trainingDetails.ResourceConfig.InstanceType.ToString(),
                        formattedTime,
                        trainingDetails.SecondaryStatusTransitions.Last().Status,
                        trainingDetails.SecondaryStatusTransitions.Last().StatusMessage
                    );
                }
                
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error in Tracking Training Job: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task StartLiveTail(AmazonCloudWatchLogsClient amazonCloudWatchLogsClient, string logGroupName, string logStreamName)
        {
            var response = await amazonCloudWatchLogsClient.StartLiveTailAsync(new StartLiveTailRequest
            {
                LogGroupIdentifiers = new List<string>() { logGroupName },
                LogStreamNames = new List<string>() { logStreamName },
            });
            if (response.HttpStatusCode != System.Net.HttpStatusCode.OK)
            {
                uIUpdater.DisplayLogMessage("Failed to start live tail session");
                return ;
            }
            startLiveTailResponse = response;
            TrackLiveTail(startLiveTailResponse);
        }

        private async void TrackLiveTail(StartLiveTailResponse response)
        {
            var eventStream = response.ResponseStream;
            var cancellationTokenSource = new CancellationTokenSource();
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30), cancellationTokenSource.Token);

            try { 
                await Task.Run(() =>
                {
                    foreach (var item in eventStream)
                    {
                        if (item is LiveTailSessionUpdate liveTailSessionUpdate)
                        {
                            foreach (var sessionResult in liveTailSessionUpdate.SessionResults)
                            {
                                uIUpdater.DisplayLogMessage(sessionResult.Message);
                            }
                        }
                        if (item is LiveTailSessionStart)
                        {
                            uIUpdater.DisplayLogMessage("Live Tail session started");
                        }
                        // On-stream exceptions are processed here
                        if (item is CloudWatchLogsEventStreamException)
                        {
                            uIUpdater.DisplayLogMessage($"ERROR: {item}");
                        }
                    }
                    // Close the stream to stop the session after a timeout
                    if (!Task.Delay(TimeSpan.FromSeconds(30)).Wait(TimeSpan.FromSeconds(30)))
                    {
                        eventStream.Dispose();
                        uIUpdater.DisplayLogMessage("End of line");
                    }
                });
            }catch (OperationCanceledException)
            {
                uIUpdater.DisplayLogMessage("Live Tail session timed out after 30 seconds.");
            }
            catch (CloudWatchLogsEventStreamException ex)
            {
                //uIUpdater.DisplayLogMessage($"ERROR: {ex}");
                timer.Stop();
                eventStream.Dispose();
                Dispose();
            }
            finally
            {
                eventStream.Dispose();
                cancellationTokenSource.Cancel();
            }
        }

        private async Task<DescribeTrainingJobResponse> GetTrainingJobDetails(AmazonSageMakerClient amazonSageMakerClient, string trainingJobName)
        {
            return await amazonSageMakerClient.DescribeTrainingJobAsync(new DescribeTrainingJobRequest
            {
                TrainingJobName = trainingJobName
            });
        }

        private async Task HandleInProgressStatus(DescribeTrainingJobResponse trainingDetails, string trainingJobName)
        {
            UpdateTrainingStatusBasedOnTime(trainingDetails);
            //if (trainingDetails.SecondaryStatusTransitions.Any())
            //{
            //    await CheckSecondaryStatus(trainingDetails, trainingJobName);
            //} //temp
        }

        private void UpdateTrainingStatusBasedOnTime(DescribeTrainingJobResponse trainingDetails)
        {
            TimeSpan timeSpan = TimeSpan.FromSeconds(trainingDetails.TrainingTimeInSeconds);
            string formattedTime = timeSpan.ToString(@"hh\:mm\:ss");

            if (trainingDetails.TrainingTimeInSeconds == 0)
            {
                uIUpdater.UpdateTrainingStatus(
                    trainingDetails.ResourceConfig.InstanceType.ToString(),
                    formattedTime,
                    trainingDetails.SecondaryStatusTransitions.Last().Status,
                    trainingDetails.SecondaryStatusTransitions.Last().StatusMessage
                );
            }
            else
            {
                //temp
                uIUpdater.UpdateTrainingStatus(
                    trainingDetails.ResourceConfig.InstanceType.ToString(),
                    formattedTime,
                    trainingDetails.SecondaryStatusTransitions.Last().Status,
                    trainingDetails.SecondaryStatusTransitions.Last().StatusMessage
                );
            }
        }

        private async Task HandleCustomUploads()
        {
            if (hasCustomUploads && !showDialogBox)
            {
                showDialogBox = true;

                if(MessageBox.Show("Do you want to delete the dataset from the S3 bucket?", "Delete Dataset", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    uIUpdater.DisplayLogMessage($"{Environment.NewLine}Deleting dataset {datasetKey} from BUCKET ${s3Bucket}");
                    await transferUtility.DeleteDataSet(s3Client, s3Bucket, datasetKey);
                    uIUpdater.DisplayLogMessage($"{Environment.NewLine}Dataset deletion complete.");
                }
                else
                {
                    uIUpdater.DisplayLogMessage($"{Environment.NewLine}Skipping dataset deletion.");
                }
            }
        }

        private async Task CheckSecondaryStatus(DescribeTrainingJobResponse trainingDetails, string trainingJobName)
        {
            if (trainingDetails.SecondaryStatusTransitions.Any() && trainingDetails.SecondaryStatusTransitions.Last().Status == "Training")
            {
                string logStreamName = await GetLatestLogStream(cloudWatchLogsClient, "/aws/sagemaker/TrainingJobs", trainingJobName);

                if (!string.IsNullOrEmpty(logStreamName))
                {
                    GetLogEventsResponse logs = await cloudWatchLogsClient.GetLogEventsAsync(new GetLogEventsRequest
                    {
                        LogGroupName = "/aws/sagemaker/TrainingJobs",
                        LogStreamName = logStreamName
                    });


                    if (logs.Events.Any() && prevLogMessage != logs.Events.Last().Message)
                    {
                        for (int i = nextLogIndex; i < logs.Events.Count; i++)
                        {
                            uIUpdater.DisplayLogMessage(logs.Events[i].Message);
                        }
                        prevLogMessage = logs.Events.Last().Message;
                        nextLogIndex = logs.Events.IndexOf(logs.Events.Last()) + 1;
                    }
                }
                else
                {
                    delay++;
                    if (delay == 10 || delay == 0)
                    {
                        uIUpdater.DisplayLogMessage($"{Environment.NewLine}Creating log stream for the training job.");
                        delay = 1;
                    }
                }
            }

            if (trainingDetails.SecondaryStatusTransitions.Any() && trainingDetails.SecondaryStatusTransitions.Last().StatusMessage != prevStatusMessage)
            {
                uIUpdater.UpdateTrainingStatus(
                    trainingDetails.SecondaryStatusTransitions.Last().Status,
                    trainingDetails.SecondaryStatusTransitions.Last().StatusMessage
                );
                prevStatusMessage = trainingDetails.SecondaryStatusTransitions.Last().StatusMessage;
            }
        }

        public async Task<string> GetLatestLogStream(AmazonCloudWatchLogsClient amazonCloudWatchLogsClient, string logGroupName, string trainingJobName)
        {
            try {
                var request = new DescribeLogStreamsRequest
                {
                    LogGroupName = logGroupName,
                    LogStreamNamePrefix = trainingJobName
                };

                var response = await amazonCloudWatchLogsClient.DescribeLogStreamsAsync(request);

                var latestLogStream = response.LogStreams.FirstOrDefault();

                if (latestLogStream != null)
                {
                    return latestLogStream.LogStreamName;
                }
                else
                {
                    return null;
                }
            }
            catch (Amazon.CloudWatchLogs.Model.ResourceNotFoundException)
            {
                // Log or handle the exception accordingly
                Console.WriteLine($"Log group '{logGroupName}' does not exist.");
                uIUpdater.DisplayLogMessage($"{Environment.NewLine} The log group '{logGroupName}' is still being created or does not exist anymore.");
                return null;
            }catch(Exception ex)
            {
                // Log or handle the exception accordingly
                Console.WriteLine($"Error in getting log stream: {ex.Message}");
                uIUpdater.DisplayLogMessage($"Error in getting log stream: {ex.Message}");
                return null;
            }
        }

        public void Dispose()
        {
            // Dispose the resources
            DisposeTimer(timer);
            startLiveTailResponse.ResponseStream.Dispose();
            amazonSageMakerClient.Dispose();
            cloudWatchLogsClient.Dispose();
            s3Client.Dispose();
        } 

        public void DisposeTimer(System.Timers.Timer timer)
        {
            timer.Stop();
            timer.Dispose();
        }
    }
}
