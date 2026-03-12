using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using MinDayProcessNS;
using System.Collections.Concurrent;
using System.Threading;
using System.Timers;
using System.Linq;

namespace PSAMinDay
    {
    public partial class DetailsForm : CTAppNS.FormBase
        {
        int LineCountMax;
        int iStatusLineCount;

        public DetailsForm()
            {
            InitializeComponent();
            ShowAbout = true; 
            iStatusLineCount = 0;
            LineCountMax = Properties.Settings.Default.MaxStatusLines;
            }

        public void WriteStatus(String newStat)
            {
            rtbDetails.AppendText("> " + newStat);
            rtbDetails.AppendText(Environment.NewLine);
            }

        public void WritePairing(String newPrg)
            {
            rtbPairings.AppendText(newPrg);
            rtbPairings.AppendText(Environment.NewLine);
            }


        public void WriteContextStatusToScreen(object Sender, MinDayContextEventArgs Args)
            {
            rtbDetails.AppendText("-- " + Args.Message);
            rtbDetails.AppendText(Environment.NewLine);
            }

        public void WriteStatusToScreen(object Sender, MinDayStatusEventArgs Args)
            {
            ++iStatusLineCount;
            rtbDetails.AppendText("> " + Args.Message);
            rtbDetails.AppendText(Environment.NewLine);
            if (iStatusLineCount > LineCountMax)
                {
                iStatusLineCount = LineCountMax / 2;
                var lines = this.rtbDetails.Lines;
                rtbDetails.Clear();
                rtbPairings.Clear();
                var newLines = lines.Skip(iStatusLineCount);
                this.rtbDetails.Lines = newLines.ToArray();
                }
            }


        public void WritePairingsToScreen(object Sender, PairingProcessInfoEventArgs Args)
            {
            if (Args.Bypassed)
                rtbPairings.AppendText("x " + Args.PairingID + " " + Args.PairingDate);
            else
                rtbPairings.AppendText("> " + Args.PairingID + " " + Args.PairingDate);

            foreach (int i in Args.ModifiedDutiesList)
                rtbPairings.AppendText(" *" + i.ToString());
            rtbPairings.AppendText(Environment.NewLine);


            Application.DoEvents();
            }

        private void DetailsForm_FormClosing(object sender, FormClosingEventArgs e)
            {
            if (e.CloseReason == CloseReason.UserClosing)
                {
                e.Cancel = true;
                Hide();
                }
            }
        }
    }
