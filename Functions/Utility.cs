﻿using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;


namespace LSC_Trainer.Functions
{
    public class Utility
    {
        private Dictionary<string, string> sizeToWeightMap;

        public Utility()
        {
            sizeToWeightMap = new Dictionary<string, string>
            {
                { "640", "yolov5s.pt" },
                { "1280", "yolov5n6.pt" }
                // more image sizes
            };
        }

        public string GetWeightFile(string imageSize)
        {
            if (sizeToWeightMap.TryGetValue(imageSize, out string weightFile))
            {
                return weightFile;
            }

            // Default value in the case where the size is not found
            return "640";
        }

        public string GetValueFromTextBox(TextBox textBox)
        {
            return !string.IsNullOrWhiteSpace(textBox.Text) ? textBox.Text : "";
        }

        public bool HasCustomUploads(string customUploadsURI)
        {
            string customUploadsDirectory = Path.GetFileName(Path.GetDirectoryName(customUploadsURI));
            if (customUploadsDirectory == "custom-uploads")
            {
                return false;
            }
            return true;
        }
    }
}
