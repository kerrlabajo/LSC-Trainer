import os
import subprocess
import boto3
import yaml
import time
from dotenv import load_dotenv

CODE_PATH= "/opt/ml/code/"
DATA_PATH = "/opt/ml/input/data/"
RESULTS_PATH = "/opt/ml/output/"
WEIGHTS_PATH = "/opt/ml/output/exp/weights/best.pt"
ONNX_PATH = "/opt/ml/output/exp/weights/best.onnx"
os.makedirs(DATA_PATH, exist_ok=True)
os.makedirs(RESULTS_PATH, exist_ok=True)

# Define a method that times the execution of a function
def time_action(method):
  def logger(*args, **kw):
      print('\033[1m' + '\nStarting %r...' % (method.__name__) + '\033[0m')
      ts = time.time()
      result = method(*args, **kw)
      te = time.time()
      print('\033[4m' + '%r completed in ' % (method.__name__) + '\033[92m%2.2f' % (te-ts) + '\033[4m seconds \033[0m\n')
      return result
  return logger

def split_s3_path(s3_path):
  path_parts=s3_path.replace("s3://","").split("/")
  bucket=path_parts.pop(0)
  file_key="/".join(path_parts)
  return bucket, file_key

@time_action
def download_dataset(s3, bucket, file_path):
  s3.Bucket(bucket).download_file(file_key, file_path)
  
@time_action
def extract_dataset(file_path):
  result = subprocess.run(['unzip', file_path, '-d', file_path.split('.')[0]], stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
  print('\033[90m' + result.stdout + '\033[0m')
  if result.stderr: print('\033[91m' + result.stderr + '\033[0m')
  os.remove(file_path)
  
@time_action
def modify_yaml(dataset_path):
  # Load the data from the YAML file
  with open(dataset_path + '/data.yaml', 'r') as file:
      data = yaml.safe_load(file)

  # Update the train, valid, and test paths
  data['train'] = dataset_path + '/train/images'
  data['val'] = dataset_path + '/valid/images'
  data['test'] = dataset_path + '/test/images'

  # Write the data back to the YAML file
  with open(dataset_path + '/data.yaml', 'w') as file:
      yaml.safe_dump(data, file)
  
  # Print the contents of the YAML file
  with open(dataset_path + '/data.yaml') as file:
    lines = file.readlines()
    for line in lines:
        print('\033[90m' + line + '\033[0m')
        
@time_action
def train_model():
# TODO: Set the arguments for the training script from user input/parameters passed
subprocess.run(
  ["python", HOME + "/yolov5/train.py",  "--img", "640", "--batch", "1", "--epochs", "1",
  "--data", "dataset/data.yaml", "--weights", "yolov5s.pt",
  "--project", "dataset/results", "--cache"
  ], check=True
)

if __name__ == "__main__":
  # Set the environment variable to avoid multithreading conflicts
  os.environ['KMP_DUPLICATE_LIB_OK'] = 'True'

  # Load environment variables from .env file
  load_dotenv()

  # Create an s3 resource object
  s3 = boto3.resource('s3', 
      aws_access_key_id=os.getenv('ACCESS_KEY_ID'), 
      aws_secret_access_key=os.getenv('SECRET_ACCESS_KEY'))
    
# Exit the program
exit(0)
