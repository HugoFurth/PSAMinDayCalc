using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using SFICTDataAccess;
using Telerik.WinControls;
using Telerik.WinControls.UI;

namespace PairingInspect
    {
    public partial class PairingInspectForm : CTAppNS.FormBase
        {
        const int MaxRecentPairings = 20;

        CTPairing prg;
        RadGridView grid;

        public PairingInspectForm()
            {
            InitializeComponent();
            RadMessageBox.ThemeName = (new Telerik.WinControls.Themes.Office2010BlackTheme()).ThemeName;
            if (this.DesignMode)
                return;
            prg = new CTPairing();
            SetupGrid();
            txtPairingID.Text = Properties.Settings.Default.LastPairingID;
            txtPairingDate.Text = Properties.Settings.Default.LastPairingDate;
            RefreshRecentComboBox();

            if (!string.IsNullOrWhiteSpace(txtPairingID.Text) && !string.IsNullOrWhiteSpace(txtPairingDate.Text))
                btnLookUp_Click(this, EventArgs.Empty);
            }

        private void SetupGrid()
            {
            grid = new RadGridView();
            grid.Dock = DockStyle.Fill;
            grid.ShowGroupPanel = false;
            grid.MasterTemplate.AutoGenerateColumns = false;
            grid.MasterTemplate.AllowAddNewRow = false;
            grid.Columns.Add(new GridViewTextBoxColumn("DutyPeriod", "DutyPeriod") { HeaderText = "Duty", TextAlignment = ContentAlignment.MiddleCenter });
            grid.Columns.Add(new GridViewTextBoxColumn("Date", "Date") { HeaderText = "Date", TextAlignment = ContentAlignment.MiddleCenter });
            grid.Columns.Add(new GridViewTextBoxColumn("OA", "OA") { HeaderText = "OA", TextAlignment = ContentAlignment.MiddleCenter });
            grid.Columns.Add(new GridViewTextBoxColumn("FlightNum", "FlightNum") { HeaderText = "Flight", TextAlignment = ContentAlignment.MiddleCenter });
            grid.Columns.Add(new GridViewTextBoxColumn("Deadhead", "Deadhead") { HeaderText = "Dhd", TextAlignment = ContentAlignment.MiddleCenter });
            grid.Columns.Add(new GridViewTextBoxColumn("DeptCity", "DeptCity") { HeaderText = "Org", TextAlignment = ContentAlignment.MiddleCenter });
            grid.Columns.Add(new GridViewTextBoxColumn("ArrvCity", "ArrvCity") { HeaderText = "Dst", TextAlignment = ContentAlignment.MiddleCenter });
            grid.Columns.Add(new GridViewTextBoxColumn("DeptTime", "DeptTime") { HeaderText = "Dept", TextAlignment = ContentAlignment.MiddleCenter });
            grid.Columns.Add(new GridViewTextBoxColumn("ArrvTime", "ArrvTime") { HeaderText = "Arrv", TextAlignment = ContentAlignment.MiddleCenter });
            grid.Columns.Add(new GridViewTextBoxColumn("Credit", "CreditDisplay") { HeaderText = "Credit" + Environment.NewLine + "(Mins)", TextAlignment = ContentAlignment.MiddleRight });
            grid.Columns.Add(new GridViewTextBoxColumn("CreditHHMM", "CreditHHMMDisplay") { HeaderText = "Credit" + Environment.NewLine + "(HH:MM)", TextAlignment = ContentAlignment.MiddleRight });
            grid.Columns.Add(new GridViewTextBoxColumn("Summary", "Summary") { HeaderText = "Duty Summary" });

            grid.TableElement.TableHeaderHeight = 40;
            grid.ViewCellFormatting += grid_ViewCellFormatting;
            grid.ViewRowFormatting += grid_ViewRowFormatting;

            // A layout saved before a column was added/renamed/reordered will silently
            // override the current setup (header text, order, visibility) when loaded --
            // and a plain "does the file mention this column name" check doesn't catch
            // renames or reorders, only missing columns. Fingerprint name+header (in
            // definition order) straight from what was just built above, so this never
            // needs manual upkeep when columns change.
            string currentFingerprint = GridColumnFingerprint();
            if (File.Exists(GridLayoutPath) && File.Exists(GridFingerprintPath) &&
                File.ReadAllText(GridFingerprintPath) == currentFingerprint)
                grid.LoadLayout(GridLayoutPath);

            pnlGrid.Controls.Add(grid);
            }

        private void grid_ViewCellFormatting(object sender, CellFormattingEventArgs e)
            {
            if (e.CellElement is GridHeaderCellElement && e.Column != null &&
                (e.Column.Name == "Credit" || e.Column.Name == "CreditHHMM"))
                {
                e.CellElement.TextWrap = true;
                return;
                }

            if (e.Row != null && e.Row.DataBoundItem is InspectRow)
                {
                InspectRow row = (InspectRow)e.Row.DataBoundItem;

                // Cell visual elements are reused across virtualization (scrolling,
                // re-populating the grid on a new lookup, etc.) -- every property set
                // here must have a matching reset on the "doesn't apply" branch, or
                // formatting bleeds from whatever row/cell last used this element.
                bool isCreditColumn = e.Column != null && (e.Column.Name == "Credit" || e.Column.Name == "CreditHHMM");

                if (row.RowKind == "DividerBeforeDuty" || row.RowKind == "DividerAfterDuty")
                    {
                    Color creditColor = row.RowKind == "DividerBeforeDuty" ? Color.LightGray : Color.Black;
                    e.CellElement.DrawFill = true;
                    e.CellElement.NumberOfColors = 1;
                    e.CellElement.BackColor = isCreditColumn ? creditColor : Color.White;
                    }
                else
                    {
                    e.CellElement.ResetValue(LightVisualElement.DrawFillProperty, ValueResetFlags.Local);
                    e.CellElement.ResetValue(LightVisualElement.NumberOfColorsProperty, ValueResetFlags.Local);
                    e.CellElement.ResetValue(LightVisualElement.BackColorProperty, ValueResetFlags.Local);
                    }

                if (isCreditColumn && (row.RowKind == "Duty" || row.RowKind == "Totals"))
                    e.CellElement.Font = new Font(e.CellElement.Font, FontStyle.Bold);
                else
                    e.CellElement.ResetValue(LightVisualElement.FontProperty, ValueResetFlags.Local);
                }
            }

        private void grid_ViewRowFormatting(object sender, RowFormattingEventArgs e)
            {
            if (e.RowElement.RowInfo.DataBoundItem is InspectRow)
                {
                InspectRow row = (InspectRow)e.RowElement.RowInfo.DataBoundItem;
                if (row.RowKind == "DividerBeforeDuty" || row.RowKind == "DividerAfterDuty")
                    {
                    e.RowElement.RowInfo.MinHeight = 4;
                    e.RowElement.RowInfo.MaxHeight = 4;
                    e.RowElement.RowInfo.Height = 4;
                    }
                }
            }

        private static string GridLayoutPath
            {
            get { return Path.Combine(Application.StartupPath, "PairingInspectGridLayout.xml"); }
            }

        private static string GridFingerprintPath
            {
            get { return Path.Combine(Application.StartupPath, "PairingInspectGridLayout.fingerprint.txt"); }
            }

        private string GridColumnFingerprint()
            {
            return string.Join("|", grid.Columns.Select(c => c.Name + "=" + c.FieldName + "=" + c.HeaderText + "=" + c.TextAlignment));
            }

        private void PairingInspectForm_FormClosing(object sender, FormClosingEventArgs e)
            {
            grid.SaveLayout(GridLayoutPath);
            File.WriteAllText(GridFingerprintPath, GridColumnFingerprint());
            Properties.Settings.Default.LastPairingID = txtPairingID.Text;
            Properties.Settings.Default.LastPairingDate = txtPairingDate.Text;
            Properties.Settings.Default.Save();
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
                PopulateGrid();
                AddToRecentList(txtPairingID.Text.Trim(), txtPairingDate.Text.Trim());
                }
            catch (Exception ex)
                {
                RadMessageBox.Show("Error looking up pairing: " + ex.Message);
                }
            }

        private void cboRecentPairings_SelectedIndexChanged(object sender, EventArgs e)
            {
            if (cboRecentPairings.SelectedItem == null)
                return;
            string entry = cboRecentPairings.SelectedItem.ToString();
            int spaceIdx = entry.IndexOf(' ');
            if (spaceIdx <= 0)
                return;
            txtPairingID.Text = entry.Substring(0, spaceIdx);
            txtPairingDate.Text = entry.Substring(spaceIdx + 1);
            btnLookUp_Click(sender, e);
            }

        private void AddToRecentList(string pairingID, string pairingDate)
            {
            string entry = pairingID + " " + pairingDate;

            StringCollection recent = Properties.Settings.Default.RecentPairings;
            if (recent == null)
                recent = new StringCollection();

            List<string> updated = new List<string>();
            updated.Add(entry);
            foreach (string s in recent)
                if (s != entry)
                    updated.Add(s);
            while (updated.Count > MaxRecentPairings)
                updated.RemoveAt(updated.Count - 1);

            StringCollection newRecent = new StringCollection();
            newRecent.AddRange(updated.ToArray());
            Properties.Settings.Default.RecentPairings = newRecent;

            RefreshRecentComboBox();
            }

        private void RefreshRecentComboBox()
            {
            cboRecentPairings.SelectedIndexChanged -= cboRecentPairings_SelectedIndexChanged;
            cboRecentPairings.Items.Clear();
            if (Properties.Settings.Default.RecentPairings != null)
                foreach (string s in Properties.Settings.Default.RecentPairings)
                    cboRecentPairings.Items.Add(s);
            cboRecentPairings.SelectedIndexChanged += cboRecentPairings_SelectedIndexChanged;
            }

        private void DisplayHeader()
            {
            PairingHeader hdr = prg.PrgHdr;
            string markerName = MarkerNameResolver.Resolve(prg, hdr.UpdateidUpdempno);
            lblHeader.Text = string.Format(
                "Pairing: {0}   From: {1}   Thru: {2}   Duty Periods: {3}   Canceled: {4}   Crew Type: {5}   Last touched by: {6}",
                hdr.PrgID, hdr.PrgDate, hdr.ActEnd.AsDisplayDate, hdr.NumDutyPeriods, hdr.Canceled, hdr.CrewType, markerName);
            }

        public class InspectRow
            {
            public string DutyPeriod { get; set; }
            public string RowKind { get; set; }   // "Leg", "Duty", "Totals", or "Divider"
            public string Date { get; set; }
            public string FlightNum { get; set; }
            public string DeptCity { get; set; }
            public string ArrvCity { get; set; }
            public string DeptTime { get; set; }
            public string ArrvTime { get; set; }
            public string OA { get; set; }
            public string Deadhead { get; set; }
            public int Credit { get; set; }
            // Parenthesized amount shown before the total: the min-day top-up on
            // Duty rows, or the trip rig included in the pairing total on the Totals row.
            public int CreditDeltaAmount { get; set; }
            public string CreditHHMM { get { return FormatHHMM(Credit); } }

            public string CreditDisplay
                {
                get
                    {
                    if (CreditDeltaAmount > 0)
                        return "(+" + CreditDeltaAmount + ") " + Credit;
                    return Credit.ToString();
                    }
                }

            public string CreditHHMMDisplay
                {
                get
                    {
                    if (CreditDeltaAmount > 0)
                        return "(+" + FormatHHMMDelta(CreditDeltaAmount) + ") " + CreditHHMM;
                    return CreditHHMM;
                    }
                }

            public string Summary { get; set; }
            }

        private static string FormatHHMM(int totalMinutes)
            {
            int hours = totalMinutes / 60;
            int mins = totalMinutes % 60;
            return string.Format("{0:D2}:{1:D2}", hours, mins);
            }

        // Same as FormatHHMM but without zero-padding the hours -- used for the
        // parenthesized min-day delta (e.g. "+1:11"), not the main total.
        private static string FormatHHMMDelta(int totalMinutes)
            {
            int hours = totalMinutes / 60;
            int mins = totalMinutes % 60;
            return string.Format("{0}:{1:D2}", hours, mins);
            }

        private static string FormatMMDD(string internalDate)
            {
            if (string.IsNullOrEmpty(internalDate) || internalDate.Length != 8)
                return internalDate;
            return internalDate.Substring(4, 2) + "/" + internalDate.Substring(6, 2);
            }

        // NonFlyingPairingLeg/OtherAirlineDeadheadPairingLeg only expose SkedDeptDate
        // (already "MM/DD/YY"), not a raw internal date -- trim it to match FormatMMDD's output.
        private static string FormatMMDDFromDisplayDate(string mmddyy)
            {
            if (string.IsNullOrEmpty(mmddyy) || mmddyy.Length < 5)
                return mmddyy;
            return mmddyy.Substring(0, 5);
            }

        // Sked/Est/Act each stay zero until populated (ctfiles.h FL struct comment) --
        // Act is the most current value once real actuals arrive, then Est once a FAM
        // arrives, falling back to Sked (always populated at pairing creation).
        private static int LatestNonZeroCredit(int sked, int est, int act)
            {
            if (act != 0)
                return act;
            if (est != 0)
                return est;
            return sked;
            }

        private void PopulateGrid()
            {
            List<InspectRow> rows = new List<InspectRow>();
            int totalCredit = 0, totalPay = 0;

            var dutiesByPeriod = prg.PairingDuties.OfType<PairingDuty>()
                .OrderBy(d => d.DutyPeriod).ToList();

            foreach (PairingDuty duty in dutiesByPeriod)
                {
                var dutyLegs = prg.PairingLegs.Where(l => l.DutyPeriod == duty.DutyPeriod).ToList();

                foreach (PairingLegItem leg in dutyLegs)
                    {
                    if (leg is AirlinePairingLeg)
                        {
                        AirlinePairingLeg airLeg = (AirlinePairingLeg)leg;
                        rows.Add(new InspectRow
                            {
                            DutyPeriod = duty.DutyPeriod.ToString(),
                            RowKind = "Leg",
                            Date = FormatMMDD(airLeg.FlightDateInternal),
                            FlightNum = airLeg.FlightNum,
                            DeptCity = airLeg.DeptCity,
                            ArrvCity = airLeg.ArrvCity,
                            DeptTime = airLeg.SkedDeptTimeasHHMM,
                            ArrvTime = airLeg.SkedArrvTimeasHHMM,
                            Credit = LatestNonZeroCredit(airLeg.SkedCredit, airLeg.EstCredit, airLeg.ActCredit)
                            });
                        }
                    else if (leg is NonFlyingPairingLeg)
                        {
                        NonFlyingPairingLeg nfLeg = (NonFlyingPairingLeg)leg;
                        rows.Add(new InspectRow
                            {
                            DutyPeriod = duty.DutyPeriod.ToString(),
                            RowKind = "Leg",
                            Date = FormatMMDDFromDisplayDate(nfLeg.SkedDeptDate),
                            FlightNum = nfLeg.TransCode,
                            DeptCity = nfLeg.DeptCity,
                            ArrvCity = nfLeg.ArrvCity,
                            DeptTime = nfLeg.SkedDeptTimeasHHMM,
                            ArrvTime = nfLeg.SkedArrvTimeasHHMM,
                            OA = nfLeg.AirlineCode,
                            Deadhead = nfLeg.DeadheadCode,
                            Credit = nfLeg.Credit
                            });
                        }
                    else if (leg is OtherAirlineDeadheadPairingLeg)
                        {
                        OtherAirlineDeadheadPairingLeg oaLeg = (OtherAirlineDeadheadPairingLeg)leg;
                        rows.Add(new InspectRow
                            {
                            DutyPeriod = duty.DutyPeriod.ToString(),
                            RowKind = "Leg",
                            Date = FormatMMDDFromDisplayDate(oaLeg.SkedDeptDate),
                            FlightNum = oaLeg.FlightNum,
                            DeptCity = oaLeg.DeptCity,
                            ArrvCity = oaLeg.ArrvCity,
                            DeptTime = oaLeg.SkedDeptTimeasHHMM,
                            ArrvTime = oaLeg.SkedArrvTimeasHHMM,
                            OA = oaLeg.AirlineCode,
                            Deadhead = oaLeg.DeadheadCode,
                            Credit = oaLeg.Credit
                            });
                        }
                    }

                rows.Add(new InspectRow { RowKind = "DividerBeforeDuty" });

                MinDayAmount minDay = MinDayDiffCalculator.CalculateForDuty(duty);
                rows.Add(new InspectRow
                    {
                    DutyPeriod = duty.DutyPeriod.ToString(),
                    RowKind = "Duty",
                    Credit = duty.ActCredit,
                    CreditDeltaAmount = minDay.Credit,
                    Summary = string.Format("Report {0}  Release {1}",
                        duty.Report.AsDisplayDate + " " + duty.Report.AsDisplayTime,
                        duty.ActEnd.AsDisplayDate + " " + duty.ActEnd.AsDisplayTime)
                    });

                totalCredit += duty.ActCredit;
                totalPay += duty.ActPay;

                rows.Add(new InspectRow { RowKind = "DividerAfterDuty" });
                }

            int tripRig = prg.PrgHdr.ActTripRig;
            rows.Add(new InspectRow
                {
                RowKind = "Totals",
                Credit = totalCredit + tripRig,
                CreditDeltaAmount = tripRig,
                Summary = string.Format("TOTALS  Credit {0}  Pay {1}", totalCredit, totalPay)
                });

            grid.DataSource = rows;
            }
        }
    }
