namespace PairingInspect
    {
    partial class PairingInspectForm
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
            this.txtPairingID = new System.Windows.Forms.TextBox();
            this.txtPairingDate = new System.Windows.Forms.TextBox();
            this.btnLookUp = new System.Windows.Forms.Button();
            this.pnlGrid = new System.Windows.Forms.Panel();
            this.cboRecentPairings = new System.Windows.Forms.ComboBox();
            this.lblPairingID = new System.Windows.Forms.Label();
            this.lblPairingDate = new System.Windows.Forms.Label();
            this.lblRecentPairings = new System.Windows.Forms.Label();
            this.lblStatusTitle = new System.Windows.Forms.Label();
            this.lblStatusValue = new System.Windows.Forms.Label();
            this.lblCrewTypeTitle = new System.Windows.Forms.Label();
            this.lblCrewTypeValue = new System.Windows.Forms.Label();
            this.lblCreditStatusTitle = new System.Windows.Forms.Label();
            this.lblCreditStatusValue = new System.Windows.Forms.Label();
            this.btnQueueToCrewPost = new System.Windows.Forms.Button();
            this.btnRecalculateMinDay = new System.Windows.Forms.Button();
            this.btnLaunchCTWPM = new System.Windows.Forms.Button();
            this.tabMain = new Telerik.WinControls.UI.RadPageView();
            this.tabPageInspect = new Telerik.WinControls.UI.RadPageViewPage("Inspect");
            this.tabPageMinDayExceptions = new Telerik.WinControls.UI.RadPageViewPage("Min Day Exceptions");
            this.SuspendLayout();
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
            // lblPairingDate
            //
            this.lblPairingDate.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F);
            this.lblPairingDate.Location = new System.Drawing.Point(115, 32);
            this.lblPairingDate.Name = "lblPairingDate";
            this.lblPairingDate.Size = new System.Drawing.Size(75, 17);
            this.lblPairingDate.TabIndex = 7;
            this.lblPairingDate.Text = "Date:";
            //
            // txtPairingID
            //
            this.txtPairingID.Location = new System.Drawing.Point(20, 50);
            this.txtPairingID.Name = "txtPairingID";
            this.txtPairingID.CharacterCasing = System.Windows.Forms.CharacterCasing.Upper;
            this.txtPairingID.Size = new System.Drawing.Size(75, 20);
            this.txtPairingID.TabIndex = 0;
            //
            // txtPairingDate
            //
            this.txtPairingDate.Location = new System.Drawing.Point(115, 50);
            this.txtPairingDate.Name = "txtPairingDate";
            this.txtPairingDate.Size = new System.Drawing.Size(75, 20);
            this.txtPairingDate.TabIndex = 1;
            //
            // btnLookUp
            //
            this.btnLookUp.Location = new System.Drawing.Point(210, 48);
            this.btnLookUp.Name = "btnLookUp";
            this.btnLookUp.Size = new System.Drawing.Size(100, 24);
            this.btnLookUp.TabIndex = 2;
            this.btnLookUp.Text = "Look Up";
            this.btnLookUp.Click += new System.EventHandler(this.btnLookUp_Click);
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
            this.cboRecentPairings.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboRecentPairings.Location = new System.Drawing.Point(330, 49);
            this.cboRecentPairings.Name = "cboRecentPairings";
            this.cboRecentPairings.Size = new System.Drawing.Size(300, 21);
            this.cboRecentPairings.TabIndex = 5;
            this.cboRecentPairings.SelectedIndexChanged += new System.EventHandler(this.cboRecentPairings_SelectedIndexChanged);
            //
            // lblStatusTitle
            //
            this.lblStatusTitle.AutoSize = true;
            this.lblStatusTitle.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Bold);
            this.lblStatusTitle.Location = new System.Drawing.Point(20, 86);
            this.lblStatusTitle.Name = "lblStatusTitle";
            this.lblStatusTitle.TabIndex = 9;
            this.lblStatusTitle.Text = "Pairing Status:";
            //
            // lblStatusValue
            //
            this.lblStatusValue.AutoSize = true;
            this.lblStatusValue.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F);
            this.lblStatusValue.Location = new System.Drawing.Point(140, 86);
            this.lblStatusValue.Name = "lblStatusValue";
            this.lblStatusValue.TabIndex = 10;
            //
            // lblCrewTypeTitle
            //
            this.lblCrewTypeTitle.AutoSize = true;
            this.lblCrewTypeTitle.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Bold);
            this.lblCrewTypeTitle.Location = new System.Drawing.Point(220, 86);
            this.lblCrewTypeTitle.Name = "lblCrewTypeTitle";
            this.lblCrewTypeTitle.TabIndex = 11;
            this.lblCrewTypeTitle.Text = "Crew Type:";
            //
            // lblCrewTypeValue
            //
            this.lblCrewTypeValue.AutoSize = true;
            this.lblCrewTypeValue.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F);
            this.lblCrewTypeValue.Location = new System.Drawing.Point(320, 86);
            this.lblCrewTypeValue.Name = "lblCrewTypeValue";
            this.lblCrewTypeValue.TabIndex = 12;
            //
            // lblCreditStatusTitle
            //
            this.lblCreditStatusTitle.AutoSize = true;
            this.lblCreditStatusTitle.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Bold);
            this.lblCreditStatusTitle.Location = new System.Drawing.Point(400, 86);
            this.lblCreditStatusTitle.Name = "lblCreditStatusTitle";
            this.lblCreditStatusTitle.TabIndex = 13;
            this.lblCreditStatusTitle.Text = "Credit Status:";
            //
            // lblCreditStatusValue
            //
            this.lblCreditStatusValue.AutoSize = true;
            this.lblCreditStatusValue.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F);
            this.lblCreditStatusValue.Location = new System.Drawing.Point(510, 86);
            this.lblCreditStatusValue.Name = "lblCreditStatusValue";
            this.lblCreditStatusValue.TabIndex = 14;
            //
            // btnQueueToCrewPost
            //
            this.btnQueueToCrewPost.Location = new System.Drawing.Point(20, 110);
            this.btnQueueToCrewPost.Name = "btnQueueToCrewPost";
            this.btnQueueToCrewPost.Size = new System.Drawing.Size(150, 24);
            this.btnQueueToCrewPost.TabIndex = 15;
            this.btnQueueToCrewPost.Text = "Queue to CrewPost";
            this.btnQueueToCrewPost.Click += new System.EventHandler(this.btnQueueToCrewPost_Click);
            //
            // btnRecalculateMinDay
            //
            this.btnRecalculateMinDay.Location = new System.Drawing.Point(180, 110);
            this.btnRecalculateMinDay.Name = "btnRecalculateMinDay";
            this.btnRecalculateMinDay.Size = new System.Drawing.Size(150, 24);
            this.btnRecalculateMinDay.TabIndex = 16;
            this.btnRecalculateMinDay.Text = "Recalculate Min Day";
            this.btnRecalculateMinDay.Click += new System.EventHandler(this.btnRecalculateMinDay_Click);
            //
            // btnLaunchCTWPM
            //
            this.btnLaunchCTWPM.Location = new System.Drawing.Point(340, 110);
            this.btnLaunchCTWPM.Name = "btnLaunchCTWPM";
            this.btnLaunchCTWPM.Size = new System.Drawing.Size(150, 24);
            this.btnLaunchCTWPM.TabIndex = 17;
            this.btnLaunchCTWPM.Text = "Launch CTWPM";
            this.btnLaunchCTWPM.Click += new System.EventHandler(this.btnLaunchCTWPM_Click);
            //
            // pnlGrid
            //
            this.pnlGrid.Location = new System.Drawing.Point(20, 151);
            this.pnlGrid.Name = "pnlGrid";
            this.pnlGrid.Size = new System.Drawing.Size(1150, 560);
            this.pnlGrid.TabIndex = 4;
            //
            // tabPageInspect
            //
            this.tabPageInspect.Controls.Add(this.pnlGrid);
            this.tabPageInspect.Controls.Add(this.btnQueueToCrewPost);
            this.tabPageInspect.Controls.Add(this.btnRecalculateMinDay);
            this.tabPageInspect.Controls.Add(this.btnLaunchCTWPM);
            this.tabPageInspect.Controls.Add(this.lblStatusTitle);
            this.tabPageInspect.Controls.Add(this.lblStatusValue);
            this.tabPageInspect.Controls.Add(this.lblCrewTypeTitle);
            this.tabPageInspect.Controls.Add(this.lblCrewTypeValue);
            this.tabPageInspect.Controls.Add(this.lblCreditStatusTitle);
            this.tabPageInspect.Controls.Add(this.lblCreditStatusValue);
            this.tabPageInspect.Controls.Add(this.lblRecentPairings);
            this.tabPageInspect.Controls.Add(this.cboRecentPairings);
            this.tabPageInspect.Controls.Add(this.btnLookUp);
            this.tabPageInspect.Controls.Add(this.lblPairingDate);
            this.tabPageInspect.Controls.Add(this.txtPairingDate);
            this.tabPageInspect.Controls.Add(this.lblPairingID);
            this.tabPageInspect.Controls.Add(this.txtPairingID);
            this.tabPageInspect.Name = "tabPageInspect";
            this.tabPageInspect.Text = "Inspect";
            //
            // tabPageMinDayExceptions
            //
            this.tabPageMinDayExceptions.Name = "tabPageMinDayExceptions";
            this.tabPageMinDayExceptions.Text = "Min Day Exceptions";
            //
            // tabMain
            //
            this.tabMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabMain.Location = new System.Drawing.Point(0, 0);
            this.tabMain.Name = "tabMain";
            this.tabMain.Pages.Add(this.tabPageInspect);
            this.tabMain.Pages.Add(this.tabPageMinDayExceptions);
            this.tabMain.SelectedPage = this.tabPageInspect;
            this.tabMain.Size = new System.Drawing.Size(1200, 781);
            this.tabMain.TabIndex = 18;
            this.tabMain.ThemeName = "Office2010Black";
            //
            // PairingInspectForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1200, 781);
            this.Controls.Add(this.tabMain);
            this.Name = "PairingInspectForm";
            this.Text = "Pairing Inspect";
            this.AcceptButton = this.btnLookUp;
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.PairingInspectForm_FormClosing);
            this.ResumeLayout(false);
            this.PerformLayout();

            }

        #endregion

        private System.Windows.Forms.TextBox txtPairingID;
        private System.Windows.Forms.TextBox txtPairingDate;
        private System.Windows.Forms.Button btnLookUp;
        private System.Windows.Forms.Panel pnlGrid;
        private System.Windows.Forms.ComboBox cboRecentPairings;
        private System.Windows.Forms.Label lblPairingID;
        private System.Windows.Forms.Label lblPairingDate;
        private System.Windows.Forms.Label lblRecentPairings;
        private System.Windows.Forms.Label lblStatusTitle;
        private System.Windows.Forms.Label lblStatusValue;
        private System.Windows.Forms.Label lblCrewTypeTitle;
        private System.Windows.Forms.Label lblCrewTypeValue;
        private System.Windows.Forms.Label lblCreditStatusTitle;
        private System.Windows.Forms.Label lblCreditStatusValue;
        private System.Windows.Forms.Button btnQueueToCrewPost;
        private System.Windows.Forms.Button btnRecalculateMinDay;
        private System.Windows.Forms.Button btnLaunchCTWPM;
        private Telerik.WinControls.UI.RadPageView tabMain;
        private Telerik.WinControls.UI.RadPageViewPage tabPageInspect;
        private Telerik.WinControls.UI.RadPageViewPage tabPageMinDayExceptions;
        }
    }
