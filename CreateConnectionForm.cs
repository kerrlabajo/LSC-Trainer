﻿using System;
using System.Windows.Forms;
using System.Collections.Generic;
using System.IO;

namespace LSC_Trainer
{
    public partial class CreateConnectionForm : Form
    {
        
        public CreateConnectionForm()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {

            bool allTextBoxesFilled = !string.IsNullOrWhiteSpace(accountID.Text) &&
                             !string.IsNullOrWhiteSpace(accessKeyID.Text) &&
                             !string.IsNullOrWhiteSpace(secretKeyID.Text) &&
                             !string.IsNullOrWhiteSpace(regionDropdown.GetItemText(regionDropdown.SelectedItem)) &&
                             !string.IsNullOrWhiteSpace(roleARN.Text);

            if (!allTextBoxesFilled)
            {
                MessageBox.Show("All fields are required!");
                return;
            }

            /*string roleArn = roleARN.Text;
            string roleType = await GetRoleDetailsAsync(roleArn);*/
            string accountId = accountID.Text;
            string accessKey = accessKeyID.Text;
            string secretKey = secretKeyID.Text;
            string region = regionDropdown.GetItemText(regionDropdown.SelectedItem);
            string roleArn = roleARN.Text;

            List<string> lines = new List<string>
            {
                $"ACCOUNT_ID={accountId}",
                $"ROLE_ARN={roleArn}",
                $"ACCESS_KEY_ID={accessKey}",
                $"SECRET_ACCESS_KEY={secretKey}",
                $"REGION={region}",
                $"ROLE_ARN={roleArn}",
            };

            string envFilePath = Path.Combine(Application.StartupPath, "./env");
            File.WriteAllLines(envFilePath, lines);

            MessageBox.Show("Successfully created a connection");
            this.Close();
        }

        private void comboBox1_SelectedValueChanged(object sender, EventArgs e)
        {
           
        }

    }
}