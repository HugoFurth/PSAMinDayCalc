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
            this.lblHeader = new System.Windows.Forms.Label();
            this.pnlGrid = new System.Windows.Forms.Panel();
            this.SuspendLayout();
            //
            // txtPairingID
            //
            this.txtPairingID.Location = new System.Drawing.Point(20, 20);
            this.txtPairingID.Name = "txtPairingID";
            this.txtPairingID.Size = new System.Drawing.Size(150, 20);
            this.txtPairingID.TabIndex = 0;
            //
            // txtPairingDate
            //
            this.txtPairingDate.Location = new System.Drawing.Point(190, 20);
            this.txtPairingDate.Name = "txtPairingDate";
            this.txtPairingDate.Size = new System.Drawing.Size(120, 20);
            this.txtPairingDate.TabIndex = 1;
            //
            // btnLookUp
            //
            this.btnLookUp.Location = new System.Drawing.Point(330, 18);
            this.btnLookUp.Name = "btnLookUp";
            this.btnLookUp.Size = new System.Drawing.Size(100, 24);
            this.btnLookUp.TabIndex = 2;
            this.btnLookUp.Text = "Look Up";
            this.btnLookUp.Click += new System.EventHandler(this.btnLookUp_Click);
            //
            // lblHeader
            //
            this.lblHeader.Location = new System.Drawing.Point(20, 55);
            this.lblHeader.Name = "lblHeader";
            this.lblHeader.Size = new System.Drawing.Size(1150, 60);
            this.lblHeader.TabIndex = 3;
            //
            // pnlGrid
            //
            this.pnlGrid.Location = new System.Drawing.Point(20, 120);
            this.pnlGrid.Name = "pnlGrid";
            this.pnlGrid.Size = new System.Drawing.Size(1150, 560);
            this.pnlGrid.TabIndex = 4;
            //
            // PairingInspectForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1200, 750);
            this.Controls.Add(this.pnlGrid);
            this.Controls.Add(this.lblHeader);
            this.Controls.Add(this.btnLookUp);
            this.Controls.Add(this.txtPairingDate);
            this.Controls.Add(this.txtPairingID);
            this.Name = "PairingInspectForm";
            this.Text = "Pairing Inspect";
            this.ResumeLayout(false);
            this.PerformLayout();

            }

        #endregion

        private System.Windows.Forms.TextBox txtPairingID;
        private System.Windows.Forms.TextBox txtPairingDate;
        private System.Windows.Forms.Button btnLookUp;
        private System.Windows.Forms.Label lblHeader;
        private System.Windows.Forms.Panel pnlGrid;
        }
    }
