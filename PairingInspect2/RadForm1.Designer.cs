namespace PairingInspect2
{
    partial class RadForm1
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(RadForm1));
            this.office2010BlackTheme1 = new Telerik.WinControls.Themes.Office2010BlackTheme();
            this.tabMain = new Telerik.WinControls.UI.RadPageView();
            this.tabPageInspect = new Telerik.WinControls.UI.RadPageViewPage();
            this.pnlGrid = new System.Windows.Forms.Panel();
            this.pnlCrewGrid = new System.Windows.Forms.Panel();
            this.pnlExceptionsGrid = new System.Windows.Forms.Panel();
            this.btnEvaluatePairing = new Telerik.WinControls.UI.RadButton();
            this.btnRecalculateMinDay = new Telerik.WinControls.UI.RadButton();
            this.btnLaunchCTWPM = new Telerik.WinControls.UI.RadDropDownButton();
            this.lblStatusTitle = new System.Windows.Forms.Label();
            this.lblStatusValue = new System.Windows.Forms.Label();
            this.lblCrewTypeTitle = new System.Windows.Forms.Label();
            this.lblCrewTypeValue = new System.Windows.Forms.Label();
            this.lblMinDayStatusTitle = new System.Windows.Forms.Label();
            this.lblMinDayStatusValue = new System.Windows.Forms.Label();
            this.lblRecentPairings = new System.Windows.Forms.Label();
            this.cboRecentPairings = new Telerik.WinControls.UI.RadDropDownList();
            this.btnLookUp = new Telerik.WinControls.UI.RadButton();
            this.lblPairingDate = new System.Windows.Forms.Label();
            this.txtPairingDate = new Telerik.WinControls.UI.RadDateTimePicker();
            this.lblPairingID = new System.Windows.Forms.Label();
            this.txtPairingID = new Telerik.WinControls.UI.RadTextBoxControl();
            this.tabPageMinDayExceptions = new Telerik.WinControls.UI.RadPageViewPage();
            this.btnFindExceptions = new Telerik.WinControls.UI.RadButton();
            this.txtExceptionsToDate = new Telerik.WinControls.UI.RadDateTimePicker();
            this.txtExceptionsFromDate = new Telerik.WinControls.UI.RadDateTimePicker();
            this.lblExceptionsToDate = new System.Windows.Forms.Label();
            this.lblExceptionsFromDate = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.tabMain)).BeginInit();
            this.tabMain.SuspendLayout();
            this.tabPageInspect.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.btnEvaluatePairing)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.btnRecalculateMinDay)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.btnLaunchCTWPM)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.cboRecentPairings)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.btnLookUp)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.txtPairingDate)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.txtPairingID)).BeginInit();
            this.tabPageMinDayExceptions.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.btnFindExceptions)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.txtExceptionsToDate)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.txtExceptionsFromDate)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this)).BeginInit();
            this.SuspendLayout();
            // 
            // tabMain
            // 
            this.tabMain.Controls.Add(this.tabPageInspect);
            this.tabMain.Controls.Add(this.tabPageMinDayExceptions);
            this.tabMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabMain.Location = new System.Drawing.Point(0, 0);
            this.tabMain.Name = "tabMain";
            this.tabMain.SelectedPage = this.tabPageInspect;
            this.tabMain.Size = new System.Drawing.Size(1200, 781);
            this.tabMain.TabIndex = 0;
            this.tabMain.ThemeName = "Office2010Black";
            // 
            // tabPageInspect
            // 
            this.tabPageInspect.Controls.Add(this.pnlCrewGrid);
            this.tabPageInspect.Controls.Add(this.pnlGrid);
            this.tabPageInspect.Controls.Add(this.btnEvaluatePairing);
            this.tabPageInspect.Controls.Add(this.btnRecalculateMinDay);
            this.tabPageInspect.Controls.Add(this.btnLaunchCTWPM);
            this.tabPageInspect.Controls.Add(this.lblStatusTitle);
            this.tabPageInspect.Controls.Add(this.lblStatusValue);
            this.tabPageInspect.Controls.Add(this.lblCrewTypeTitle);
            this.tabPageInspect.Controls.Add(this.lblCrewTypeValue);
            this.tabPageInspect.Controls.Add(this.lblMinDayStatusTitle);
            this.tabPageInspect.Controls.Add(this.lblMinDayStatusValue);
            this.tabPageInspect.Controls.Add(this.lblRecentPairings);
            this.tabPageInspect.Controls.Add(this.cboRecentPairings);
            this.tabPageInspect.Controls.Add(this.btnLookUp);
            this.tabPageInspect.Controls.Add(this.lblPairingDate);
            this.tabPageInspect.Controls.Add(this.txtPairingDate);
            this.tabPageInspect.Controls.Add(this.lblPairingID);
            this.tabPageInspect.Controls.Add(this.txtPairingID);
            this.tabPageInspect.ItemSize = new System.Drawing.SizeF(64F, 30F);
            this.tabPageInspect.Location = new System.Drawing.Point(12, 40);
            this.tabPageInspect.Name = "tabPageInspect";
            this.tabPageInspect.Size = new System.Drawing.Size(1176, 729);
            this.tabPageInspect.Text = "Inspect";
            // 
            // btnEvaluatePairing
            // 
            this.btnEvaluatePairing.Location = new System.Drawing.Point(180, 110);
            this.btnEvaluatePairing.Name = "btnEvaluatePairing";
            this.btnEvaluatePairing.Size = new System.Drawing.Size(150, 24);
            this.btnEvaluatePairing.TabIndex = 16;
            this.btnEvaluatePairing.Text = "Evaluate Pairing";
            this.btnEvaluatePairing.ThemeName = "ControlDefault";
            this.btnEvaluatePairing.Click += new System.EventHandler(this.btnEvaluatePairing_Click);
            // 
            // btnRecalculateMinDay
            // 
            this.btnRecalculateMinDay.Location = new System.Drawing.Point(20, 110);
            this.btnRecalculateMinDay.Name = "btnRecalculateMinDay";
            this.btnRecalculateMinDay.Size = new System.Drawing.Size(150, 24);
            this.btnRecalculateMinDay.TabIndex = 15;
            this.btnRecalculateMinDay.Text = "Recalculate Min Day";
            this.btnRecalculateMinDay.ThemeName = "ControlDefault";
            this.btnRecalculateMinDay.Click += new System.EventHandler(this.btnRecalculateMinDay_Click);
            // 
            // btnLaunchCTWPM
            // 
            this.btnLaunchCTWPM.Location = new System.Drawing.Point(340, 110);
            this.btnLaunchCTWPM.Name = "btnLaunchCTWPM";
            this.btnLaunchCTWPM.Size = new System.Drawing.Size(150, 24);
            this.btnLaunchCTWPM.TabIndex = 17;
            this.btnLaunchCTWPM.Text = "Pairing Function";
            this.btnLaunchCTWPM.ThemeName = "ControlDefault";
            this.btnLaunchCTWPM.Click += new System.EventHandler(this.btnLaunchCTWPM_Click);
            // 
            // lblStatusTitle
            // 
            this.lblStatusTitle.AutoSize = true;
            this.lblStatusTitle.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Bold);
            this.lblStatusTitle.Location = new System.Drawing.Point(20, 86);
            this.lblStatusTitle.Name = "lblStatusTitle";
            this.lblStatusTitle.Size = new System.Drawing.Size(115, 17);
            this.lblStatusTitle.TabIndex = 9;
            this.lblStatusTitle.Text = "Pairing Status:";
            // 
            // lblStatusValue
            // 
            this.lblStatusValue.AutoSize = true;
            this.lblStatusValue.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F);
            this.lblStatusValue.Location = new System.Drawing.Point(140, 86);
            this.lblStatusValue.Name = "lblStatusValue";
            this.lblStatusValue.Size = new System.Drawing.Size(0, 17);
            this.lblStatusValue.TabIndex = 10;
            // 
            // lblCrewTypeTitle
            // 
            this.lblCrewTypeTitle.AutoSize = true;
            this.lblCrewTypeTitle.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Bold);
            this.lblCrewTypeTitle.Location = new System.Drawing.Point(220, 86);
            this.lblCrewTypeTitle.Name = "lblCrewTypeTitle";
            this.lblCrewTypeTitle.Size = new System.Drawing.Size(89, 17);
            this.lblCrewTypeTitle.TabIndex = 11;
            this.lblCrewTypeTitle.Text = "Crew Type:";
            // 
            // lblCrewTypeValue
            // 
            this.lblCrewTypeValue.AutoSize = true;
            this.lblCrewTypeValue.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F);
            this.lblCrewTypeValue.Location = new System.Drawing.Point(320, 86);
            this.lblCrewTypeValue.Name = "lblCrewTypeValue";
            this.lblCrewTypeValue.Size = new System.Drawing.Size(0, 17);
            this.lblCrewTypeValue.TabIndex = 12;
            // 
            // lblMinDayStatusTitle
            // 
            this.lblMinDayStatusTitle.AutoSize = true;
            this.lblMinDayStatusTitle.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Bold);
            this.lblMinDayStatusTitle.Location = new System.Drawing.Point(400, 86);
            this.lblMinDayStatusTitle.Name = "lblMinDayStatusTitle";
            this.lblMinDayStatusTitle.Size = new System.Drawing.Size(122, 17);
            this.lblMinDayStatusTitle.TabIndex = 13;
            this.lblMinDayStatusTitle.Text = "Min Day Status:";
            // 
            // lblMinDayStatusValue
            // 
            this.lblMinDayStatusValue.AutoSize = true;
            this.lblMinDayStatusValue.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F);
            this.lblMinDayStatusValue.Location = new System.Drawing.Point(510, 86);
            this.lblMinDayStatusValue.Name = "lblMinDayStatusValue";
            this.lblMinDayStatusValue.Size = new System.Drawing.Size(0, 17);
            this.lblMinDayStatusValue.TabIndex = 14;
            // 
            // lblRecentPairings
            // 
            this.lblRecentPairings.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F);
            this.lblRecentPairings.Location = new System.Drawing.Point(330, 32);
            this.lblRecentPairings.Name = "lblRecentPairings";
            this.lblRecentPairings.Size = new System.Drawing.Size(300, 17);
            this.lblRecentPairings.TabIndex = 8;
            this.lblRecentPairings.Text = "Recent Pairings:";
            // 
            // cboRecentPairings
            // 
            this.cboRecentPairings.DropDownAnimationEnabled = true;
            this.cboRecentPairings.Location = new System.Drawing.Point(330, 49);
            this.cboRecentPairings.Name = "cboRecentPairings";
            this.cboRecentPairings.Size = new System.Drawing.Size(300, 20);
            this.cboRecentPairings.TabIndex = 5;
            this.cboRecentPairings.ThemeName = "ControlDefault";
            this.cboRecentPairings.SelectedIndexChanged += new Telerik.WinControls.UI.Data.PositionChangedEventHandler(this.cboRecentPairings_SelectedIndexChanged);
            // 
            // btnLookUp
            // 
            this.btnLookUp.Location = new System.Drawing.Point(210, 48);
            this.btnLookUp.Name = "btnLookUp";
            this.btnLookUp.Size = new System.Drawing.Size(100, 24);
            this.btnLookUp.TabIndex = 2;
            this.btnLookUp.Text = "Look Up";
            this.btnLookUp.ThemeName = "ControlDefault";
            this.btnLookUp.Click += new System.EventHandler(this.btnLookUp_Click);
            // 
            // lblPairingDate
            // 
            this.lblPairingDate.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F);
            this.lblPairingDate.Location = new System.Drawing.Point(115, 32);
            this.lblPairingDate.Name = "lblPairingDate";
            this.lblPairingDate.Size = new System.Drawing.Size(75, 17);
            this.lblPairingDate.TabIndex = 7;
            this.lblPairingDate.Text = "Date:";
            // 
            // txtPairingDate
            // 
            this.txtPairingDate.CustomFormat = "MM/dd/yy";
            this.txtPairingDate.Format = System.Windows.Forms.DateTimePickerFormat.Custom;
            this.txtPairingDate.NullText = "";
            this.txtPairingDate.Location = new System.Drawing.Point(115, 50);
            this.txtPairingDate.Name = "txtPairingDate";
            this.txtPairingDate.Size = new System.Drawing.Size(75, 20);
            this.txtPairingDate.TabIndex = 1;
            this.txtPairingDate.TabStop = false;
            this.txtPairingDate.Text = "10/22/26";
            this.txtPairingDate.ThemeName = "ControlDefault";
            this.txtPairingDate.Value = new System.DateTime(2026, 10, 22, 11, 27, 58, 779);
            // 
            // lblPairingID
            // 
            this.lblPairingID.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F);
            this.lblPairingID.Location = new System.Drawing.Point(20, 32);
            this.lblPairingID.Name = "lblPairingID";
            this.lblPairingID.Size = new System.Drawing.Size(75, 17);
            this.lblPairingID.TabIndex = 6;
            this.lblPairingID.Text = "Pairing:";
            // 
            // txtPairingID
            // 
            this.txtPairingID.CharacterCasing = System.Windows.Forms.CharacterCasing.Upper;
            this.txtPairingID.Location = new System.Drawing.Point(20, 50);
            this.txtPairingID.Name = "txtPairingID";
            this.txtPairingID.Size = new System.Drawing.Size(75, 20);
            this.txtPairingID.TabIndex = 0;
            this.txtPairingID.ThemeName = "ControlDefault";
            // 
            // tabPageMinDayExceptions
            // 
            this.tabPageMinDayExceptions.Controls.Add(this.pnlExceptionsGrid);
            this.tabPageMinDayExceptions.Controls.Add(this.btnFindExceptions);
            this.tabPageMinDayExceptions.Controls.Add(this.txtExceptionsToDate);
            this.tabPageMinDayExceptions.Controls.Add(this.txtExceptionsFromDate);
            this.tabPageMinDayExceptions.Controls.Add(this.lblExceptionsToDate);
            this.tabPageMinDayExceptions.Controls.Add(this.lblExceptionsFromDate);
            this.tabPageMinDayExceptions.ItemSize = new System.Drawing.SizeF(126F, 30F);
            this.tabPageMinDayExceptions.Location = new System.Drawing.Point(0, 0);
            this.tabPageMinDayExceptions.Name = "tabPageMinDayExceptions";
            this.tabPageMinDayExceptions.Size = new System.Drawing.Size(200, 100);
            this.tabPageMinDayExceptions.Text = "Min Day Exceptions";
            // 
            // btnFindExceptions
            // 
            this.btnFindExceptions.Location = new System.Drawing.Point(210, 48);
            this.btnFindExceptions.Name = "btnFindExceptions";
            this.btnFindExceptions.Size = new System.Drawing.Size(100, 24);
            this.btnFindExceptions.TabIndex = 21;
            this.btnFindExceptions.Text = "Find";
            this.btnFindExceptions.ThemeName = "ControlDefault";
            this.btnFindExceptions.Click += new System.EventHandler(this.btnFindExceptions_Click);
            // 
            // txtExceptionsToDate
            // 
            this.txtExceptionsToDate.CustomFormat = "MM/dd/yy";
            this.txtExceptionsToDate.Format = System.Windows.Forms.DateTimePickerFormat.Custom;
            this.txtExceptionsToDate.NullText = "";
            this.txtExceptionsToDate.Location = new System.Drawing.Point(115, 50);
            this.txtExceptionsToDate.Name = "txtExceptionsToDate";
            this.txtExceptionsToDate.Size = new System.Drawing.Size(75, 20);
            this.txtExceptionsToDate.TabIndex = 20;
            this.txtExceptionsToDate.TabStop = false;
            this.txtExceptionsToDate.Text = "10/22/26";
            this.txtExceptionsToDate.ThemeName = "ControlDefault";
            this.txtExceptionsToDate.Value = new System.DateTime(2026, 10, 22, 11, 27, 58, 865);
            // 
            // txtExceptionsFromDate
            // 
            this.txtExceptionsFromDate.CustomFormat = "MM/dd/yy";
            this.txtExceptionsFromDate.Format = System.Windows.Forms.DateTimePickerFormat.Custom;
            this.txtExceptionsFromDate.NullText = "";
            this.txtExceptionsFromDate.Location = new System.Drawing.Point(20, 50);
            this.txtExceptionsFromDate.Name = "txtExceptionsFromDate";
            this.txtExceptionsFromDate.Size = new System.Drawing.Size(75, 20);
            this.txtExceptionsFromDate.TabIndex = 19;
            this.txtExceptionsFromDate.TabStop = false;
            this.txtExceptionsFromDate.Text = "10/22/26";
            this.txtExceptionsFromDate.ThemeName = "ControlDefault";
            this.txtExceptionsFromDate.Value = new System.DateTime(2026, 10, 22, 11, 27, 58, 918);
            // 
            // lblExceptionsToDate
            // 
            this.lblExceptionsToDate.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F);
            this.lblExceptionsToDate.Location = new System.Drawing.Point(115, 32);
            this.lblExceptionsToDate.Name = "lblExceptionsToDate";
            this.lblExceptionsToDate.Size = new System.Drawing.Size(75, 17);
            this.lblExceptionsToDate.TabIndex = 24;
            this.lblExceptionsToDate.Text = "To Date:";
            // 
            // lblExceptionsFromDate
            // 
            this.lblExceptionsFromDate.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F);
            this.lblExceptionsFromDate.Location = new System.Drawing.Point(20, 32);
            this.lblExceptionsFromDate.Name = "lblExceptionsFromDate";
            this.lblExceptionsFromDate.Size = new System.Drawing.Size(75, 17);
            this.lblExceptionsFromDate.TabIndex = 23;
            this.lblExceptionsFromDate.Text = "From Date:";
            // 
            // 
            // pnlCrewGrid
            // 
            this.pnlCrewGrid.Location = new System.Drawing.Point(20, 151);
            this.pnlCrewGrid.Name = "pnlCrewGrid";
            this.pnlCrewGrid.Size = new System.Drawing.Size(450, 100);
            this.pnlCrewGrid.TabIndex = 25;
            // 
            // pnlGrid
            // 
            this.pnlGrid.Location = new System.Drawing.Point(20, 261);
            this.pnlGrid.Name = "pnlGrid";
            this.pnlGrid.Size = new System.Drawing.Size(1150, 450);
            this.pnlGrid.TabIndex = 4;
            // 
            // pnlExceptionsGrid
            // 
            this.pnlExceptionsGrid.Location = new System.Drawing.Point(20, 90);
            this.pnlExceptionsGrid.Name = "pnlExceptionsGrid";
            this.pnlExceptionsGrid.Size = new System.Drawing.Size(1150, 620);
            this.pnlExceptionsGrid.TabIndex = 22;
            // RadForm1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1200, 781);
            this.Controls.Add(this.tabMain);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "RadForm1";
            this.Text = "Pairing Inspect";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.RadForm1_FormClosing);
            this.ThemeName = "Office2010Black";
            ((System.ComponentModel.ISupportInitialize)(this.tabMain)).EndInit();
            this.tabMain.ResumeLayout(false);
            this.tabPageInspect.ResumeLayout(false);
            this.tabPageInspect.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.btnEvaluatePairing)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.btnRecalculateMinDay)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.btnLaunchCTWPM)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.cboRecentPairings)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.btnLookUp)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.txtPairingDate)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.txtPairingID)).EndInit();
            this.tabPageMinDayExceptions.ResumeLayout(false);
            this.tabPageMinDayExceptions.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.btnFindExceptions)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.txtExceptionsToDate)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.txtExceptionsFromDate)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private Telerik.WinControls.Themes.Office2010BlackTheme office2010BlackTheme1;
        private Telerik.WinControls.UI.RadPageView tabMain;
        private Telerik.WinControls.UI.RadPageViewPage tabPageInspect;
        private Telerik.WinControls.UI.RadPageViewPage tabPageMinDayExceptions;
        private System.Windows.Forms.Label lblPairingID;
        private System.Windows.Forms.Label lblPairingDate;
        private Telerik.WinControls.UI.RadTextBoxControl txtPairingID;
        private Telerik.WinControls.UI.RadDateTimePicker txtPairingDate;
        private System.Windows.Forms.Label lblRecentPairings;
        private Telerik.WinControls.UI.RadDropDownList cboRecentPairings;
        private Telerik.WinControls.UI.RadButton btnLookUp;
        private System.Windows.Forms.Label lblStatusTitle;
        private System.Windows.Forms.Label lblStatusValue;
        private System.Windows.Forms.Label lblCrewTypeTitle;
        private System.Windows.Forms.Label lblCrewTypeValue;
        private System.Windows.Forms.Label lblMinDayStatusTitle;
        private System.Windows.Forms.Label lblMinDayStatusValue;
        private Telerik.WinControls.UI.RadButton btnRecalculateMinDay;
        private Telerik.WinControls.UI.RadButton btnEvaluatePairing;
        private Telerik.WinControls.UI.RadDropDownButton btnLaunchCTWPM;
        private System.Windows.Forms.Label lblExceptionsFromDate;
        private System.Windows.Forms.Label lblExceptionsToDate;
        private Telerik.WinControls.UI.RadDateTimePicker txtExceptionsFromDate;
        private Telerik.WinControls.UI.RadDateTimePicker txtExceptionsToDate;
        private Telerik.WinControls.UI.RadButton btnFindExceptions;
        private System.Windows.Forms.Panel pnlGrid;
        private System.Windows.Forms.Panel pnlCrewGrid;
        private System.Windows.Forms.Panel pnlExceptionsGrid;
    }
}
