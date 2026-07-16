using System;
using SFICTDataAccess;
using Telerik.WinControls;

namespace PairingInspect
    {
    public partial class PairingInspectForm : CTAppNS.FormBase
        {
        CTPairing prg;

        public PairingInspectForm()
            {
            InitializeComponent();
            RadMessageBox.ThemeName = (new Telerik.WinControls.Themes.Office2010BlackTheme()).ThemeName;
            if (this.DesignMode)
                return;
            prg = new CTPairing();
            }

        private void btnLookUp_Click(object sender, EventArgs e)
            {
            try
                {
                int result = prg.Assemble(txtPairingID.Text.Trim(), txtPairingDate.Text.Trim());
                if (result == 0)
                    {
                    RadMessageBox.Show("Pairing not found: " + txtPairingID.Text + " " + txtPairingDate.Text);
                    return;
                    }
                DisplayHeader();
                }
            catch (Exception ex)
                {
                RadMessageBox.Show("Error looking up pairing: " + ex.Message);
                }
            }

        private void DisplayHeader()
            {
            PairingHeader hdr = prg.PrgHdr;
            string markerName = MarkerNameResolver.Resolve(prg, hdr.UpdateidUpdempno);
            lblHeader.Text = string.Format(
                "Pairing: {0}   From: {1}   Thru: {2}   Duty Periods: {3}   Canceled: {4}   Crew Type: {5}   Last touched by: {6}",
                hdr.PrgID, hdr.PrgDate, hdr.ActEnd.AsDisplayDate, hdr.NumDutyPeriods, hdr.Canceled, hdr.CrewType, markerName);
            }
        }
    }
