﻿using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Amazon;
using Amazon.S3;
using Amazon.SageMaker;
using Amazon.SageMaker.Model;
using Amazon.SageMakerRuntime;

namespace LSC_Trainer
{
    public partial class Form1 : Form
    {
        private readonly AmazonSageMakerClient amazonSageMakerClient;
        private readonly AmazonS3Client s3Client;
        private readonly AmazonSageMakerRuntimeClient amazonSageMakerRuntimeClient;
        public Form1()
        {
            InitializeComponent();
            DotNetEnv.Env.Load("C:/Users/raul jay/Desktop/BSCS/4th/Thesis/LSC-Trainer/.env"); //change the path with your .env path

            string accessKey = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID");
            string secretKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY");
            string region = Environment.GetEnvironmentVariable("AWS_REGION");

            amazonSageMakerClient = new AmazonSageMakerClient(accessKey, secretKey, RegionEndpoint.GetBySystemName(region));
            s3Client = new AmazonS3Client();
            amazonSageMakerRuntimeClient = new AmazonSageMakerRuntimeClient();

        }

        private void connectToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            ///TODO: To test if your AWS credentials is accessible and able to connect, execute this event with the AmazonSageMakerClient.
            try
            {
                var response = amazonSageMakerClient.ListModelsAsync(new ListModelsRequest()).Result;
                MessageBox.Show("Connection successful.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception error)
            {
                MessageBox.Show($"Connection failed: {error.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void btnUpload_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "ZIP Files (*.zip)|*.zip|RAR Files (*.rar)|*.rar";
                openFileDialog.Title = "Select a Zip or Rar File";

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string selectedFilePath = openFileDialog.FileName;

                    // Display the selected file path (optional)
                    lblZipFile.Text = selectedFilePath;

                    // Handle the zip file as needed
                    // For example, you can extract its contents or upload it to a server
                    // For simplicity, we'll just show a message box with the file path
                    MessageBox.Show($"Selected file: {selectedFilePath}");
                }
            }
        }

        private void btnTraining_Click(object sender, EventArgs e)
        {
            string img_num = "";
            string img_size = "1280";
            string weights = "yolov5n6.pt";
            string patience = "100";
            string hyperparameters = "hyp.scratch-low.yaml";
            string epochs = "";
            string batch_size = "";
            string project = "";
            string workers = "8";
            string optimiser = "SGD";

            if(txtImgNum.Text != null || txtImgNum.Text != "")
            {
                img_num = txtImgNum.Text;
            }

            if (txtImgSize.Text != null || txtImgSize.Text != "")
            {
                img_size = txtImgSize.Text;
            }

            if (txtWeights.Text != null || txtWeights.Text != "")
            {
                weights = txtWeights.Text;
            }

            if (txtPatience.Text != null || txtPatience.Text != "")
            {
                patience = txtPatience.Text;
            }

            if (txtHyperparameters.Text != null || txtHyperparameters.Text != "")
            {
                hyperparameters = txtHyperparameters.Text;
            }

            if (txtEpochs.Text != null || txtEpochs.Text != "")
            {
                epochs = txtEpochs.Text;
            }

            if (txtBatchSize.Text != null || txtBatchSize.Text != "")
            {
                batch_size = txtBatchSize.Text;
            }

            if (txtProject.Text != null || txtProject.Text != "")
            {
                project = txtProject.Text;
            }

            if (txtWorkers.Text != null || txtWorkers.Text != "")
            {
                workers = txtWorkers.Text;
            }

            if (txtOptimiser.Text != null || txtOptimiser.Text != "")
            {
                optimiser = txtOptimiser.Text;
            }

            string jobName = String.Format("Training-YOLOv5-{0}", DateTime.Now.ToString("yyyy-MM-dd-hh-mmss"));

            CreateTrainingJobRequest ctrRequest = new CreateTrainingJobRequest()
            {
                AlgorithmSpecification = new AlgorithmSpecification()
                {
                    TrainingImage = "433757028032.dkr.ecr.us-west-2.amazonaws.com/image-classification:1",
                    TrainingInputMode = "File"
                },
                RoleArn = "INSERT TOLE",
                OutputDataConfig = new OutputDataConfig()
                {
                    S3OutputPath = String.Format(@"Amazon S3s3://{0}/{1}/output", "INSERT BUCKET NAME HERE", jobName)
                },
                ResourceConfig = new ResourceConfig()
                {
                    InstanceCount = 1,
                    InstanceType = TrainingInstanceType.MlM4Xlarge,
                    VolumeSizeInGB = 50 //size of ml storage
                },
                TrainingJobName = jobName,
                HyperParameters = FileHandler.ReadYamlFile(hyperparameters),
                StoppingCondition = new StoppingCondition()
                {
                    MaxRuntimeInSeconds = 360000        
                },
                InputDataConfig = new List<Channel>(){
                    new Channel()
                    {
                        ChannelName = "train",
                        ContentType = "application/x-recordio",
                        CompressionType = Amazon.SageMaker.CompressionType.None,
                        DataSource = new DataSource()
                        {
                            S3DataSource = new Amazon.SageMaker.Model.S3DataSource()
                            {
                                S3DataType = Amazon.SageMaker.S3DataType.S3Prefix,
                                S3Uri = "INSERT URI",
                                S3DataDistributionType = Amazon.SageMaker.S3DataDistribution.FullyReplicated
                            }
                        }
                    }
                }             
            };
        }

        private void panel1_Paint(object sender, PaintEventArgs e)
        {
                
        }

        private void button3_Click(object sender, EventArgs e)
        {

        }

        private void button3_Click_1(object sender, EventArgs e)
        {

        }

        private void lblZipFile_Click(object sender, EventArgs e)
        {

        }

        ///TODO: Create a button to upload a dataset in .rar/.zip file.

        ///TODO: Create a text area to get the training parameters:
        ///TODO: number of images to train [NOT YET SURE/IMPLEMENTED IN YOLOV5 REPO]
        ///TODO: image size (default 1280), batch size, epochs, 
        ///TODO: weights (default yolov5n6.pt), project (default S3 destination), 
        ///TODO: patience (default 100), workers (default 8), optimizer (default SGD), 
        ///TODO: hyperparameters (default hyp.scratch-low.yamL)

        ///TODO: Create a text area to print the ff:
        ///TODO: 1. AmazonSageMakerClient connection duration.
        ///TODO: 2. Dataset file size, and uploading network connectivity and duration
        ///TODO: 3. Virtual machine specs used for training (the instance we selected to create the training job).
        ///TODO: 4. Duration of the training in AWS SageMaker Training Job creation and training.
        ///TODO: 5. Model url that was saved in AWS S3.

        ///TODO: Create a proceed training button to create a training job in AWS.
        ///TODO: Implement the create training job using SageMaker with the .env variable that contains the AWS ECR location 
        ///TODO: of the team's training algorithm, specific EC2 Instance (Virtual Machine) that is free version, and other
        ///TODO: specific configurations in the web interface that can be replicated here in AWSSDK.SageMaker.

    }
}
