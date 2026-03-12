using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;
using System.Windows.Forms;
using System.Collections.Concurrent;
using MinDayProcessNS;
using System.Reflection;
using Telerik.WinControls;
using Telerik.WinControls.UI;
using System.IO;
using SFIConfigUtils;

namespace PSAMinDay
    {
    public class MinDayContextEventArgs : EventArgs
        {

        public MinDayContextEventArgs(String Message)
            {
            this.Message = Message;
            }
        public readonly String Message;
        }

    public delegate void MinDayContextStatusDelegate(object Sender, MinDayContextEventArgs Args);

    public class PSAMinDayContext : ApplicationContext
        {
        System.ComponentModel.IContainer components;
        NotifyIcon notifyIcon;
        ContextMenuStrip cms;
        System.Windows.Forms.Timer aTimer;
                  
        ToolStripItem RunStat;
        MinDayProcess mdp;
        DetailsForm detailsForm;
        public event MinDayContextStatusDelegate ContextStatusUpdate;
        StreamWriter LogWriter;
        String LogFileName;
                   
        bool bCurrentlyProcessing;

        public PSAMinDayContext() 
		    {
			InitializeContext();
            RadMessageBox.ThemeName = (new Telerik.WinControls.Themes.Office2010BlackTheme()).ThemeName;
            try {
                mdp = new MinDayProcess();
                try {
                    LogFileName = SFIConfig.AppSetting("SFILOGDIR") + "\\MinDayErrors.log";
                    WriteLogInfoOnly("Application starting");
                    }
                catch (SystemException)
                    {
                    RadMessageBox.Show("Cannot open log file " + LogFileName, "Fatal Error - Program Execution Halted");
                    throw;
                    }

                aTimer = new System.Windows.Forms.Timer();
                aTimer.Tick += OnFormTimedEvent;
                aTimer.Interval = Properties.Settings.Default.TimerInterval*1000; // stored in seconds but timer needs milliseconds
                aTimer.Enabled = false;

                detailsForm = new DetailsForm();
                mdp.PairingProcess += detailsForm.WritePairingsToScreen;
                mdp.StatusUpdate += detailsForm.WriteStatusToScreen;
                this.ContextStatusUpdate += detailsForm.WriteContextStatusToScreen;
                mdp.StatusUpdate +=  this.LogCriticalErrors;
                mdp.ListExcludeableCodes();  // just put the excluded codes in the log
                detailsForm.Closed += detailsForm_Closed; // avoid reshowing a disposed form

                bCurrentlyProcessing = false;
                doProcess();
                }
            catch (Exception ee)
                {
                String InnerMess = "";
                if (ee.InnerException != null)
                    InnerMess = " / " + ee.InnerException.Message;
                RadMessageBox.Show(ee.Message + InnerMess, "Fatal Error - Program Execution Halted");
                Environment.Exit(-1);
                }
            }

        public void doProcess()
            {
            if (!bCurrentlyProcessing)
                {
                aTimer.Enabled = false;
                bCurrentlyProcessing = true;
                mdp.Process();
                bCurrentlyProcessing = false;

                RunStat.Text = "Pause";
                aTimer.Enabled = true;
                }
            }

        private void OnFormTimedEvent(object sender, EventArgs e)
            {
            doProcess();
            }

        protected void OnContextStatusUpdate(MinDayContextEventArgs Args)
            {
            if (ContextStatusUpdate != null)
                {
                ContextStatusUpdate(this, Args);
                }
            }

        private void UpdateContextStatus(String Message)
            {
            MinDayContextEventArgs Args = new MinDayContextEventArgs(Message);
            OnContextStatusUpdate(Args);
            }

        public void LogCriticalErrors(object Sender, MinDayStatusEventArgs Args)
            {
            if (Args.Status == MinDayStatus.Critical || Args.Status == MinDayStatus.CriticalStop)
                WriteLog(" <" + Args.Status.ToString() + "> " +  Args.Message);
            if (Args.Status == MinDayStatus.CriticalStop)
                {
                WriteLogInfoOnly("Application terminating due to critical stop");
                Exit();
                }
            }


        public void WriteLogInfoOnly(String Message)
            {
            WriteLog(" <Info> " + Message);
            }

        private void WriteLog(String Message)
            {
            LogWriter = new StreamWriter(LogFileName, true);
            String LogTS = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss > ");
            LogWriter.WriteLine(LogTS + Message);
            LogWriter.Close();
            }

        private void InitializeContext()
            {
            components = new System.ComponentModel.Container();

            notifyIcon = new NotifyIcon(components);
            cms = new ContextMenuStrip();
            notifyIcon.ContextMenuStrip = cms;
            notifyIcon.Icon = PSAMinDay.Properties.Resources.SFMult4;
            notifyIcon.ContextMenuStrip.Opening += ContextMenuStrip_Opening;
            notifyIcon.ContextMenuStrip.Items.Add("Details", null, detailsItem_Click);
            RunStat = notifyIcon.ContextMenuStrip.Items.Add("Pause", null, pauseRestart_Item_Click);
            notifyIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
            notifyIcon.ContextMenuStrip.Items.Add("Exit", null, exitItem_Click);

            notifyIcon.ContextMenuStrip = cms;
            notifyIcon.MouseClick += notifyIcon_Click;
            notifyIcon.Visible = true; 
            }

        private void ContextMenuStrip_Opening(object sender, System.ComponentModel.CancelEventArgs e)
            {

            }

        private void ProcessTimerItem_Click(object sender, EventArgs e)
            {
            aTimer.Interval = 5000;
            aTimer.Enabled = true;
            }

        private void ProcessItem_Click(object sender, EventArgs e)
            {
            doProcess();
            }

        private void notifyIcon_Click(object sender, MouseEventArgs e)
            {
            MethodInfo mi = typeof(NotifyIcon).GetMethod("ShowContextMenu", BindingFlags.Instance | BindingFlags.NonPublic);
            mi.Invoke(notifyIcon, null);
   //         cms.Show(Cursor.Position);
            }

        void detailsItem_Click(object sender, EventArgs e)
            {
            detailsForm.Activate(); 
            detailsForm.Show();
            Application.DoEvents();
            }

        private void detailsForm_Closed(object sender, EventArgs e) { detailsForm = null; }

        void pauseRestart_Item_Click(object sender, EventArgs e)
            {
            if (aTimer.Enabled)  // if currently enabled....
                {
                // disable 
                aTimer.Enabled = false;
                UpdateContextStatus("Pausing timer");
                RunStat.Text = "Start Processing";
                WriteLogInfoOnly("Pausing timer");
                }
            else
                {
                // enable 
                UpdateContextStatus("Starting process");
                RunStat.Text = "Pause Processing";
                WriteLogInfoOnly("Starting process");
                doProcess();
                }
            }

        void aboutItem_Click(object sender, EventArgs e)
            {
            // We must manually tidy up and remove the icon before we exit.
            // Otherwise it will be left behind until the user mouses over.
            notifyIcon.Visible = false;
            Application.Exit();
            }

        void exitItem_Click(object sender, EventArgs e)
            {
            // We must manually tidy up and remove the icon before we exit.
            // Otherwise it will be left behind until the user mouses over.
            WriteLogInfoOnly("Application terminating normally");
            Exit();
            }

        void Exit()
            {
            notifyIcon.Visible = false;
            Application.Exit();
            }
        }
    }
