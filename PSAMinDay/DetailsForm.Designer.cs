namespace PSAMinDay
    {
    partial class DetailsForm
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
            this.rtbDetails = new Telerik.WinControls.UI.RadTextBox();
            this.rtbPairings = new Telerik.WinControls.UI.RadTextBox();
            this.office2010BlackTheme1 = new Telerik.WinControls.Themes.Office2010BlackTheme();
            this.radLabel1 = new Telerik.WinControls.UI.RadLabel();
            this.radLabel2 = new Telerik.WinControls.UI.RadLabel();
            this.pmByTimestampTableAdapter1 = new SFICTDataAccess.CTDataSetTableAdapters.PMByTimestampTableAdapter();
            ((System.ComponentModel.ISupportInitialize)(this.radStatusStrip1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.rtbDetails)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.rtbPairings)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.radLabel1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.radLabel2)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this)).BeginInit();
            this.SuspendLayout();
            // 
            // rleStatus
            // 
            this.rleStatus.BackColor = System.Drawing.SystemColors.Control;
            this.rleStatus.Bounds = new System.Drawing.Rectangle(0, 0, 2, 19);
            this.rleStatus.Font = new System.Drawing.Font("Segoe UI", 8.25F);
            this.rleStatus.ForeColor = System.Drawing.Color.White;
            this.radStatusStrip1.SetSpring(this.rleStatus, false);
            // 
            // radStatusStrip1
            // 
            this.radStatusStrip1.Location = new System.Drawing.Point(0, 455);
            this.radStatusStrip1.Size = new System.Drawing.Size(575, 21);
            // 
            // rtbDetails
            // 
            this.rtbDetails.AutoSize = false;
            this.rtbDetails.Location = new System.Drawing.Point(0, 42);
            this.rtbDetails.Multiline = true;
            this.rtbDetails.Name = "rtbDetails";
            this.rtbDetails.ReadOnly = true;
            this.rtbDetails.Size = new System.Drawing.Size(340, 407);
            this.rtbDetails.TabIndex = 1;
            this.rtbDetails.TabStop = false;
            this.rtbDetails.ThemeName = "Office2010Black";
            // 
            // rtbPairings
            // 
            this.rtbPairings.AutoSize = false;
            this.rtbPairings.Location = new System.Drawing.Point(346, 42);
            this.rtbPairings.Multiline = true;
            this.rtbPairings.Name = "rtbPairings";
            this.rtbPairings.ReadOnly = true;
            this.rtbPairings.Size = new System.Drawing.Size(229, 407);
            this.rtbPairings.TabIndex = 2;
            this.rtbPairings.TabStop = false;
            this.rtbPairings.ThemeName = "Office2010Black";
            // 
            // radLabel1
            // 
            this.radLabel1.Location = new System.Drawing.Point(109, 18);
            this.radLabel1.Name = "radLabel1";
            this.radLabel1.Size = new System.Drawing.Size(37, 18);
            this.radLabel1.TabIndex = 3;
            this.radLabel1.Text = "Status";
            // 
            // radLabel2
            // 
            this.radLabel2.Location = new System.Drawing.Point(440, 18);
            this.radLabel2.Name = "radLabel2";
            this.radLabel2.Size = new System.Drawing.Size(46, 18);
            this.radLabel2.TabIndex = 4;
            this.radLabel2.Text = "Pairings";
            // 
            // pmByTimestampTableAdapter1
            // 
            this.pmByTimestampTableAdapter1.ClearBeforeFill = true;
            // 
            // DetailsForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.ClientSize = new System.Drawing.Size(575, 476);
            this.Controls.Add(this.radLabel2);
            this.Controls.Add(this.radLabel1);
            this.Controls.Add(this.rtbPairings);
            this.Controls.Add(this.rtbDetails);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Fixed3D;
            this.Name = "DetailsForm";
            // 
            // 
            // 
            this.RootElement.ApplyShapeToControl = true;
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.Text = "Min Day Credit Process";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.DetailsForm_FormClosing);
            this.Controls.SetChildIndex(this.radStatusStrip1, 0);
            this.Controls.SetChildIndex(this.rtbDetails, 0);
            this.Controls.SetChildIndex(this.rtbPairings, 0);
            this.Controls.SetChildIndex(this.radLabel1, 0);
            this.Controls.SetChildIndex(this.radLabel2, 0);
            ((System.ComponentModel.ISupportInitialize)(this.radStatusStrip1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.rtbDetails)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.rtbPairings)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.radLabel1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.radLabel2)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

            }

        #endregion

        private Telerik.WinControls.UI.RadTextBox rtbDetails;
        private Telerik.WinControls.UI.RadTextBox rtbPairings;
        private Telerik.WinControls.Themes.Office2010BlackTheme office2010BlackTheme1;
        private Telerik.WinControls.UI.RadLabel radLabel1;
        private Telerik.WinControls.UI.RadLabel radLabel2;
        private SFICTDataAccess.CTDataSetTableAdapters.PMByTimestampTableAdapter pmByTimestampTableAdapter1;
        }
    }
