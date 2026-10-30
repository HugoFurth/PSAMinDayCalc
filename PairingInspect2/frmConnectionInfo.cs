using System;
using System.Windows.Forms;

namespace PairingInspect2
    {
    public partial class frmConnectionInfo : Form
        {
        public string ConnectionString { get; set; }
        public frmConnectionInfo()
            {
            InitializeComponent();
            }

        private void frmConnectionInfo_Shown(object sender, EventArgs e)
            {
            tbConfigFile.Text = AppDomain.CurrentDomain.SetupInformation.ConfigurationFile;
            tbConnection.Text = ConnectionString == null ? "" : ConnectionString;
            }

        private void button1_Click(object sender, EventArgs e)
            {
            Close();
            }
        }
    }
