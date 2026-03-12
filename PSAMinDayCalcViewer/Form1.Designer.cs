namespace PSAMinDayCalcViewer
    {
    partial class Form1
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
            this.radTextBox1 = new Telerik.WinControls.UI.RadTextBox();
            this.radTextBox2 = new Telerik.WinControls.UI.RadTextBox();
            this.radTextBox3 = new Telerik.WinControls.UI.RadTextBox();
            this.radLabel2 = new Telerik.WinControls.UI.RadLabel();
            this.radLabel4 = new Telerik.WinControls.UI.RadLabel();
            this.radLabel5 = new Telerik.WinControls.UI.RadLabel();
            this.radLabel6 = new Telerik.WinControls.UI.RadLabel();
            this.rbProcess = new Telerik.WinControls.UI.RadButton();
            this.rbClearStatus = new Telerik.WinControls.UI.RadButton();
            this.rbClearPairings = new Telerik.WinControls.UI.RadButton();
            this.radTextBox4 = new Telerik.WinControls.UI.RadTextBox();
            this.radTextBox6 = new Telerik.WinControls.UI.RadTextBox();
            ((System.ComponentModel.ISupportInitialize)(this.radTextBox1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.radTextBox2)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.radTextBox3)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.radLabel2)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.radLabel4)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.radLabel5)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.radLabel6)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.rbProcess)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.rbClearStatus)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.rbClearPairings)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.radTextBox4)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.radTextBox6)).BeginInit();
            this.SuspendLayout();
            // 
            // radTextBox1
            // 
            this.radTextBox1.Location = new System.Drawing.Point(50, 112);
            this.radTextBox1.Name = "radTextBox1";
            this.radTextBox1.Size = new System.Drawing.Size(100, 20);
            this.radTextBox1.TabIndex = 0;
            this.radTextBox1.Visible = false;
            // 
            // radTextBox2
            // 
            this.radTextBox2.Location = new System.Drawing.Point(156, 112);
            this.radTextBox2.Name = "radTextBox2";
            this.radTextBox2.Size = new System.Drawing.Size(100, 20);
            this.radTextBox2.TabIndex = 1;
            this.radTextBox2.Visible = false;
            // 
            // radTextBox3
            // 
            this.radTextBox3.Location = new System.Drawing.Point(522, 54);
            this.radTextBox3.Name = "radTextBox3";
            this.radTextBox3.Size = new System.Drawing.Size(100, 20);
            this.radTextBox3.TabIndex = 2;
            // 
            // radLabel2
            // 
            this.radLabel2.Location = new System.Drawing.Point(541, 37);
            this.radLabel2.Name = "radLabel2";
            this.radLabel2.Size = new System.Drawing.Size(56, 18);
            this.radLabel2.TabIndex = 2;
            this.radLabel2.Text = "PMs Read";
            // 
            // radLabel4
            // 
            this.radLabel4.Location = new System.Drawing.Point(42, 37);
            this.radLabel4.Name = "radLabel4";
            this.radLabel4.Size = new System.Drawing.Size(194, 18);
            this.radLabel4.TabIndex = 3;
            this.radLabel4.Text = "Pairings with Timestamp Greater than";
            this.radLabel4.Visible = false;
            // 
            // radLabel5
            // 
            this.radLabel5.Location = new System.Drawing.Point(59, 92);
            this.radLabel5.Name = "radLabel5";
            this.radLabel5.Size = new System.Drawing.Size(69, 18);
            this.radLabel5.TabIndex = 4;
            this.radLabel5.Text = "Update Date";
            this.radLabel5.Visible = false;
            // 
            // radLabel6
            // 
            this.radLabel6.Location = new System.Drawing.Point(166, 93);
            this.radLabel6.Name = "radLabel6";
            this.radLabel6.Size = new System.Drawing.Size(70, 18);
            this.radLabel6.TabIndex = 5;
            this.radLabel6.Text = "Update Time";
            this.radLabel6.Visible = false;
            // 
            // rbProcess
            // 
            this.rbProcess.Location = new System.Drawing.Point(519, 7);
            this.rbProcess.Name = "rbProcess";
            this.rbProcess.Size = new System.Drawing.Size(110, 24);
            this.rbProcess.TabIndex = 6;
            this.rbProcess.Text = "Process Pairings";
            this.rbProcess.Click += new System.EventHandler(this.rbProcess_Click);
            // 
            // rbClearStatus
            // 
            this.rbClearStatus.Location = new System.Drawing.Point(84, 498);
            this.rbClearStatus.Name = "rbClearStatus";
            this.rbClearStatus.Size = new System.Drawing.Size(110, 24);
            this.rbClearStatus.TabIndex = 7;
            this.rbClearStatus.Text = "Clear";
            this.rbClearStatus.Click += new System.EventHandler(this.rbClearStatus_Click);
            // 
            // rbClearPairings
            // 
            this.rbClearPairings.Location = new System.Drawing.Point(515, 498);
            this.rbClearPairings.Name = "rbClearPairings";
            this.rbClearPairings.Size = new System.Drawing.Size(110, 24);
            this.rbClearPairings.TabIndex = 8;
            this.rbClearPairings.Text = "Clear";
            this.rbClearPairings.Click += new System.EventHandler(this.rbClearPairings_Click);
            // 
            // radTextBox4
            // 
            this.radTextBox4.AutoSize = false;
            this.radTextBox4.Location = new System.Drawing.Point(12, 79);
            this.radTextBox4.Multiline = true;
            this.radTextBox4.Name = "radTextBox4";
            this.radTextBox4.Size = new System.Drawing.Size(338, 402);
            this.radTextBox4.TabIndex = 9;
            // 
            // radTextBox6
            // 
            this.radTextBox6.AutoSize = false;
            this.radTextBox6.Location = new System.Drawing.Point(413, 80);
            this.radTextBox6.Multiline = true;
            this.radTextBox6.Name = "radTextBox6";
            this.radTextBox6.Size = new System.Drawing.Size(289, 401);
            this.radTextBox6.TabIndex = 10;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(716, 536);
            this.Controls.Add(this.radTextBox6);
            this.Controls.Add(this.radTextBox4);
            this.Controls.Add(this.rbClearPairings);
            this.Controls.Add(this.rbClearStatus);
            this.Controls.Add(this.rbProcess);
            this.Controls.Add(this.radLabel6);
            this.Controls.Add(this.radLabel5);
            this.Controls.Add(this.radLabel4);
            this.Controls.Add(this.radLabel2);
            this.Controls.Add(this.radTextBox3);
            this.Controls.Add(this.radTextBox2);
            this.Controls.Add(this.radTextBox1);
            this.Name = "Form1";
            this.Text = "PSA Min Day Viewer";
            ((System.ComponentModel.ISupportInitialize)(this.radTextBox1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.radTextBox2)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.radTextBox3)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.radLabel2)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.radLabel4)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.radLabel5)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.radLabel6)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.rbProcess)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.rbClearStatus)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.rbClearPairings)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.radTextBox4)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.radTextBox6)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

            }

        #endregion

        private Telerik.WinControls.UI.RadTextBox radTextBox1;
        private Telerik.WinControls.UI.RadTextBox radTextBox2;
        private Telerik.WinControls.UI.RadTextBox radTextBox3;
        private Telerik.WinControls.UI.RadLabel radLabel2;
        private Telerik.WinControls.UI.RadLabel radLabel4;
        private Telerik.WinControls.UI.RadLabel radLabel5;
        private Telerik.WinControls.UI.RadLabel radLabel6;
        private Telerik.WinControls.UI.RadButton rbProcess;
        private Telerik.WinControls.UI.RadButton rbClearStatus;
        private Telerik.WinControls.UI.RadButton rbClearPairings;
        private Telerik.WinControls.UI.RadTextBox radTextBox4;
        private Telerik.WinControls.UI.RadTextBox radTextBox6;
        }
    }

