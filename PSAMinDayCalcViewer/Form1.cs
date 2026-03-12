using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
//using SFICTDataAccess;
//using SFICTDateTimeUtils;
using MinDayProcessNS;
using Telerik.WinControls;

namespace PSAMinDayCalcViewer
    {
    public partial class Form1 : Form
        {
        MinDayProcess mdp;

        int iEventCount;
        public Form1()
            {
            InitializeComponent();
            radTextBox1.Text = Properties.Settings.Default.PMAfterDate;
            radTextBox2.Text = Properties.Settings.Default.PMAfterTime.ToString();
            }

        private void ClearAllFields()
           {
           radTextBox3.Text = radTextBox4.Text = radTextBox6.Text = "";
           }

        private void SaveProperites()
            {
            Properties.Settings.Default.PMAfterDate = radTextBox1.Text;
            Properties.Settings.Default.PMAfterTime = Convert.ToInt32(radTextBox2.Text);
            Properties.Settings.Default.Save();
            } 

        private void radButton1_Click(object sender, EventArgs e)
            {
 
            }



         private void rbProcess_Click(object sender, EventArgs e)
             {
             radTextBox3.Clear();
             radTextBox4.AppendText(Environment.NewLine);
             radTextBox6.AppendText(Environment.NewLine);
             try {
                 if (mdp == null)
                     {
                     mdp = new MinDayProcess();
                     mdp.PairingProcess += WritePairingsToScreen;
                     mdp.StatusUpdate += WriteStatusToScreen;
                     }
                 iEventCount = 0;
                 radTextBox3.Text = (iEventCount).ToString();
                 Application.DoEvents();
        //         mdp.Process(radTextBox1.Text, Convert.ToInt32(radTextBox2.Text));
                 mdp.Process();
                 }
             catch (Exception ee)
                 {
                 String InnerMess = "";
                 if (ee.InnerException != null)
                     InnerMess = " / " + ee.InnerException.Message;
                 RadMessageBox.Show(ee.Message + " " + InnerMess);
                 }
             }

        private void WriteStatusToScreen(object Sender, MinDayStatusEventArgs Args)
            {
            radTextBox4.AppendText("> " + Args.Message);
            radTextBox4.AppendText(Environment.NewLine);
            }


        private void WritePairingsToScreen(object Sender, PairingProcessInfoEventArgs Args)
            {
            ++iEventCount;
            if (Args.Bypassed)
                radTextBox6.AppendText("x " + Args.PairingID + " " + Args.PairingDate);
            else
                radTextBox6.AppendText("> " + Args.PairingID + " " + Args.PairingDate);

            foreach (int i in Args.ModifiedDutiesList)
                radTextBox6.AppendText(" *" +i.ToString()); 
            radTextBox6.AppendText(Environment.NewLine);
                

            radTextBox3.Text = (iEventCount).ToString() ;
            Application.DoEvents();
            }

        private void rbClearStatus_Click(object sender, EventArgs e)
            {
            radTextBox4.Clear();
            }

        private void rbClearPairings_Click(object sender, EventArgs e)
            {
            radTextBox6.Clear();
            }
        }
    }
