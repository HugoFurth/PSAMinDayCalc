using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using SFIConfigUtils;
using SFICTDataAccess;
using SFICTDateTimeUtils;
using MinDayProcessNS;
using Telerik.WinControls;
using Telerik.WinControls.UI;

namespace PairingInspect
    {
    public partial class PairingInspectForm : CTAppNS.FormBase
        {
        const int MaxRecentPairings = 20;

        CTPairing prg;
        RadGridView grid;
        Timer securitySessionTimer;
        MinDayProcess minDayProcess;
        List<string> minDayCriticalMessages;

        public PairingInspectForm()
            {
            InitializeComponent();
            RadMessageBox.ThemeName = (new Telerik.WinControls.Themes.Office2010BlackTheme()).ThemeName;
            if (this.DesignMode)
                return;
            prg = new CTPairing();
            minDayCriticalMessages = new List<string>();
            SetupGrid();
            SetupLaunchCtwpmButton();
            SetupSecuritySessionTimer();
            txtPairingID.Text = Properties.Settings.Default.LastPairingID;
            txtPairingDate.Text = PairingDateToDisplay(Properties.Settings.Default.LastPairingDate);
            RefreshRecentComboBox();

            if (!string.IsNullOrWhiteSpace(txtPairingID.Text) && !string.IsNullOrWhiteSpace(txtPairingDate.Text))
                btnLookUp_Click(this, EventArgs.Empty);
            }

        // CTW.exe force-closes apps it launches directly (WM_CLOSE) when it shuts down,
        // but also calls CTWSECUR.DLL's ResetSecurity() on shutdown, which clears the
        // shared cross-process security state every process's loaded copy of the DLL
        // reads from. Since CTW.exe doesn't launch PairingInspect, it never gets the
        // WM_CLOSE -- polling RunCheck.OkToRun() is how we notice the second signal instead.
        private void SetupSecuritySessionTimer()
            {
            securitySessionTimer = new Timer();
            securitySessionTimer.Interval = 5000;
            securitySessionTimer.Tick += SecuritySessionTimer_Tick;
            securitySessionTimer.Start();
            }

        private void SecuritySessionTimer_Tick(object sender, EventArgs e)
            {
            bool sessionStillValid;
            try
                {
                sessionStillValid = CTSecurity.UserID() != 0;
                }
            catch (Exception)
                {
                sessionStillValid = false;
                }

            if (!sessionStillValid)
                {
                securitySessionTimer.Stop();
                this.Close();
                }
            }

        // Created on first use rather than in the constructor -- setup (bid periods,
        // config/marker loading) is non-trivial and not every session ends up
        // recalculating a min day. One instance lives for the rest of the form's
        // lifetime; the security session timer above closes the form the moment
        // CrewTrac logs out, so userID can't go stale underneath it.
        private MinDayProcess GetMinDayProcess()
            {
            if (minDayProcess == null)
                {
                minDayProcess = new MinDayProcess((int)CTSecurity.UserID());
                minDayProcess.StatusUpdate += MinDayProcess_StatusUpdate;
                }
            return minDayProcess;
            }

        // MinDayProcess can report multiple Critical-level problems in a single call
        // (e.g. one per crewmember it fails to evaluate), so collect them here rather
        // than surfacing only the last one -- the caller (btnRecalculateMinDay_Click)
        // clears this list before each run and shows whatever accumulated afterward.
        private void MinDayProcess_StatusUpdate(object sender, MinDayStatusEventArgs args)
            {
            if (args.Status == MinDayStatus.Critical)
                minDayCriticalMessages.Add(args.Message);
            }

        private void SetupGrid()
            {
            grid = new RadGridView();
            grid.Dock = DockStyle.Fill;
            grid.ShowGroupPanel = false;
            // Rows are hand-ordered (legs under their duty, divider rows, a trailing totals
            // row) to mirror the reference layout -- column-click sorting would scramble that
            // and is never appropriate here.
            grid.EnableSorting = false;
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
            grid.Columns.Add(new GridViewTextBoxColumn("Report", "Report") { HeaderText = "Report", TextAlignment = ContentAlignment.MiddleCenter });
            grid.Columns.Add(new GridViewTextBoxColumn("Release", "Release") { HeaderText = "Release", TextAlignment = ContentAlignment.MiddleCenter });
            grid.Columns.Add(new GridViewTextBoxColumn("DutyTime", "DutyTime") { HeaderText = "Duty Time", TextAlignment = ContentAlignment.MiddleCenter });
            grid.Columns.Add(new GridViewTextBoxColumn("Note", "Note") { HeaderText = "Note", TextAlignment = ContentAlignment.MiddleCenter });

            // ColumnGroupsViewDefinition only shows columns explicitly listed in it --
            // every column needs a home, including ones that aren't part of any
            // visible group heading (put those in a ShowHeader=false group so they
            // render normally, with no extra header row of their own).
            ColumnGroupsViewDefinition columnGroupsView = new ColumnGroupsViewDefinition();

            GridViewColumnGroup leadingColumns = new GridViewColumnGroup("");
            leadingColumns.ShowHeader = false;
            leadingColumns.Rows.Add(new GridViewColumnGroupRow());
            foreach (string name in new string[] { "DutyPeriod", "Date", "OA", "FlightNum", "Deadhead", "DeptCity", "ArrvCity", "DeptTime", "ArrvTime", "Credit", "CreditHHMM" })
                leadingColumns.Rows[0].ColumnNames.Add(name);
            columnGroupsView.ColumnGroups.Add(leadingColumns);

            GridViewColumnGroup dutySummaryGroup = new GridViewColumnGroup("Duty Summary");
            dutySummaryGroup.Rows.Add(new GridViewColumnGroupRow());
            dutySummaryGroup.Rows[0].ColumnNames.Add("Report");
            dutySummaryGroup.Rows[0].ColumnNames.Add("Release");
            dutySummaryGroup.Rows[0].ColumnNames.Add("DutyTime");
            dutySummaryGroup.Rows[0].ColumnNames.Add("Note");
            columnGroupsView.ColumnGroups.Add(dutySummaryGroup);

            grid.ViewDefinition = columnGroupsView;

            grid.TableElement.TableHeaderHeight = 40;
            grid.ViewCellFormatting += grid_ViewCellFormatting;
            grid.ViewRowFormatting += grid_ViewRowFormatting;

            // A layout saved before a column was added/renamed/reordered will silently
            // override the current setup (header text, order, visibility) when loaded --
            // and a plain "does the file mention this column name" check doesn't catch
            // renames or reorders, only missing columns. Fingerprint name+header (in
            // definition order) straight from what was just built above, so this never
            // needs manual upkeep when columns change. GridSchemaVersion covers
            // structural changes the per-column fingerprint can't see, like switching
            // to a grouped-column ViewDefinition.
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
                bool isDutySummaryColumn = e.Column != null && (e.Column.Name == "Report" || e.Column.Name == "Release" || e.Column.Name == "DutyTime");

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

                bool isArrvTimeColumn = e.Column != null && e.Column.Name == "ArrvTime";

                if ((isCreditColumn && (row.RowKind == "Duty" || row.RowKind == "Totals")) ||
                    (isDutySummaryColumn && row.RowKind == "Duty") ||
                    (isArrvTimeColumn && row.RowKind == "Totals"))
                    e.CellElement.Font = new Font(e.CellElement.Font, FontStyle.Bold);
                else
                    e.CellElement.ResetValue(LightVisualElement.FontProperty, ValueResetFlags.Local);

                bool isNoteColumn = e.Column != null && e.Column.Name == "Note";
                if (isNoteColumn && !string.IsNullOrEmpty(row.Note))
                    {
                    e.CellElement.DisableHTMLRendering = false;
                    e.CellElement.Text = "<html><color= 34, 139, 34>✓ <color= 0, 0, 0>" + row.Note;
                    }
                else
                    {
                    e.CellElement.DisableHTMLRendering = true;
                    }
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

        // Bump this whenever a structural change isn't captured by the per-column
        // fingerprint below -- e.g. switching ViewDefinition (plain columns vs.
        // grouped-column headers), which the saved layout also can't distinguish
        // between until it's re-saved under the new structure.
        private const string GridSchemaVersion = "columngroups-v1";

        private string GridColumnFingerprint()
            {
            return GridSchemaVersion + "|" +
                string.Join("|", grid.Columns.Select(c => c.Name + "=" + c.FieldName + "=" + c.HeaderText + "=" + c.TextAlignment));
            }

        private void PairingInspectForm_FormClosing(object sender, FormClosingEventArgs e)
            {
            securitySessionTimer.Stop();
            grid.SaveLayout(GridLayoutPath);
            File.WriteAllText(GridFingerprintPath, GridColumnFingerprint());
            Properties.Settings.Default.LastPairingID = txtPairingID.Text;
            Properties.Settings.Default.LastPairingDate = PairingDateToInternal(txtPairingDate.Text);
            Properties.Settings.Default.Save();
            }

        private void btnLookUp_Click(object sender, EventArgs e)
            {
            try
                {
                string internalDate = PairingDateToInternal(txtPairingDate.Text.Trim());
                int result = prg.Assemble(txtPairingID.Text.Trim(), internalDate);
                if (result == 0)
                    {
                    RadMessageBox.Show("Pairing not found: " + txtPairingID.Text + " " + txtPairingDate.Text);
                    return;
                    }
                txtPairingDate.Text = PairingDateToDisplay(internalDate);
                string markerName = MarkerNameResolver.Resolve(prg, prg.PrgHdr.UpdateidUpdempno);
                DisplayHeader(markerName);
                PopulateGrid(markerName);
                AddToRecentList(txtPairingID.Text.Trim(), internalDate);
                }
            catch (Exception ex)
                {
                RadMessageBox.Show("Error looking up pairing: " + ex.Message);
                }
            }

        private void btnQueueToCrewPost_Click(object sender, EventArgs e)
            {
            // TODO: implement
            }

        private void btnRecalculateMinDay_Click(object sender, EventArgs e)
            {
            if (prg.PrgHdr == null)
                return;

            minDayCriticalMessages.Clear();
            try
                {
                GetMinDayProcess().ProcessSinglePairing(prg.PrgHdr.PrgID, prg.PrgHdr.PrgDate);

                // re-run the lookup so the marker/credit values just written reflect
                // immediately, without the user re-typing the pairing.
                prg.Assemble(prg.PrgHdr.PrgID, prg.PrgHdr.PrgDate);
                string markerName = MarkerNameResolver.Resolve(prg, prg.PrgHdr.UpdateidUpdempno);
                DisplayHeader(markerName);
                PopulateGrid(markerName);

                if (minDayCriticalMessages.Count > 0)
                    RadMessageBox.Show(string.Join(Environment.NewLine, minDayCriticalMessages), "Min Day Recalculation Problem");
                }
            catch (Exception ex)
                {
                RadMessageBox.Show("Error recalculating min day: " + ex.Message);
                }
            }

        // RadDropDownButton is used only for its arrow chrome -- its own Items/RadMenuItem popup
        // fires Click on mere hover/highlight rather than requiring an actual click, so the menu
        // itself is a plain ContextMenuStrip (native WinForms, click-only) shown from the button's
        // own Click. DropDownOpening is cancelled so Telerik's native (now-empty) popup never
        // flashes up alongside it.
        private ContextMenuStrip ctwpmFunctionMenu;

        private void SetupLaunchCtwpmButton()
            {
            ctwpmFunctionMenu = new ContextMenuStrip();

            ToolStripMenuItem itemInquire = new ToolStripMenuItem("Inquire");
            itemInquire.Click += itemPrgInquire_Click;
            ctwpmFunctionMenu.Items.Add(itemInquire);

            ToolStripMenuItem itemModify = new ToolStripMenuItem("Modify");
            itemModify.Click += itemPrgModify_Click;
            ctwpmFunctionMenu.Items.Add(itemModify);

            btnLaunchCTWPM.DropDownButtonElement.DropDownOpening += btnLaunchCTWPM_DropDownOpening;
            }

        private void btnLaunchCTWPM_DropDownOpening(object sender, CancelEventArgs e)
            {
            e.Cancel = true;
            }

        private void btnLaunchCTWPM_Click(object sender, EventArgs e)
            {
            ctwpmFunctionMenu.Show(btnLaunchCTWPM, new Point(0, btnLaunchCTWPM.Height));
            }

        private void itemPrgInquire_Click(object sender, EventArgs e)
            {
            LaunchCtwpm(CtwpmSelectionAutomator.FunctionInquire);
            }

        private void itemPrgModify_Click(object sender, EventArgs e)
            {
            LaunchCtwpm(CtwpmSelectionAutomator.FunctionModify);
            }

        private void LaunchCtwpm(string function)
            {
            try
                {
                string ctExeDir = SFIConfig.AppSetting("CTEXEDIR");
                if (prg.PrgHdr != null)
                    CtwpmSelectionAutomator.Launch(ctExeDir, function, prg.PrgHdr.PrgID, prg.PrgHdr.PrgDate);
                else
                    CtwpmSelectionAutomator.Launch(ctExeDir);
                }
            catch (Exception ex)
                {
                RadMessageBox.Show("Failed to launch CTWPM: " + ex.Message);
                }
            }

        private void cboRecentPairings_SelectedIndexChanged(object sender, Telerik.WinControls.UI.Data.PositionChangedEventArgs e)
            {
            if (cboRecentPairings.SelectedItem == null)
                return;
            string entry = cboRecentPairings.SelectedItem.ToString();
            int spaceIdx = entry.IndexOf(' ');
            if (spaceIdx <= 0)
                return;
            txtPairingID.Text = entry.Substring(0, spaceIdx);
            txtPairingDate.Text = PairingDateToDisplay(entry.Substring(spaceIdx + 1));
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
            List<string> items = new List<string>();
            if (Properties.Settings.Default.RecentPairings != null)
                foreach (string s in Properties.Settings.Default.RecentPairings)
                    items.Add(s);
            cboRecentPairings.DataSource = items;
            cboRecentPairings.SelectedIndexChanged += cboRecentPairings_SelectedIndexChanged;
            }

        private void DisplayHeader(string markerName)
            {
            PairingHeader hdr = prg.PrgHdr;

            lblStatusValue.Text = hdr.Canceled ? "Canceled" : "Active";
            lblCrewTypeValue.Text = CrewTypeDisplay(hdr.CrewType);
            lblCreditStatusValue.Text = CreditStatusDisplay(markerName) + " (empno=" + hdr.UpdateidUpdempno + ")";
            btnRecalculateMinDay.Text = MarkerNameResolver.IsKnownMarker(hdr.UpdateidUpdempno)
                ? "Recalculate Min Day" : "Calculate Min Day";
            LayoutHeaderLabels();
            }

        private static string CreditStatusDisplay(string markerName)
            {
            if (markerName == "MinDay - Updated")
                return "Min Day applied";
            if (markerName == "MinDay - No Update Needed")
                return "Min Day not applicable";
            if (markerName == "MinDay - Exception")
                return markerName;
            return "Pending Min Day processing";
            }

        private void LayoutHeaderLabels()
            {
            const int fieldGap = 20;
            const int titleValueGap = 4;
            int y = lblStatusTitle.Top;
            int x = lblStatusTitle.Left;

            Label[] labels = { lblStatusTitle, lblStatusValue, lblCrewTypeTitle, lblCrewTypeValue, lblCreditStatusTitle, lblCreditStatusValue };
            for (int i = 0; i < labels.Length; i++)
                {
                labels[i].Location = new Point(x, y);
                x += labels[i].Width + (i % 2 == 0 ? titleValueGap : fieldGap);
                }
            }

        // PM.type: B = both, P = pilot, C = cabin crew (per ctfiles.h)
        private static string CrewTypeDisplay(string crewType)
            {
            switch (crewType)
                {
                case "P": return "Pilot";
                case "C": return "FA";
                case "B": return "Mixed";
                default: return crewType;
                }
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

            public string Report { get; set; }
            public string Release { get; set; }
            public string DutyTime { get; set; }
            public string Note { get; set; }
            }

        private static string FormatReleaseTime(DateTimeWithGMTVar report, DateTimeWithGMTVar release)
            {
            string time = release.AsDisplayTime;
            if (report.AsMSDate.HasValue && release.AsMSDate.HasValue &&
                release.AsMSDate.Value.Date == report.AsMSDate.Value.Date.AddDays(1))
                return time + "+1";
            return time;
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

        // Pairing date field: displayed/typed as MM/DD/YY (slashes optional on input),
        // stored everywhere else (settings, recent list, Assemble()) as CT's internal
        // YYYYMMDD. These two convert between the two -- always convert at the boundary
        // rather than letting either format leak into the wrong place.
        private static string PairingDateToInternal(string mmddyyInput)
            {
            string digits = new string(mmddyyInput.Where(char.IsDigit).ToArray());
            if (digits.Length == 6)
                return "20" + digits.Substring(4, 2) + digits.Substring(0, 2) + digits.Substring(2, 2);
            if (digits.Length == 8)
                return digits;
            return mmddyyInput;
            }

        private static string PairingDateToDisplay(string internalDate)
            {
            if (string.IsNullOrEmpty(internalDate) || internalDate.Length != 8)
                return internalDate;
            return internalDate.Substring(4, 2) + "/" + internalDate.Substring(6, 2) + "/" + internalDate.Substring(2, 2);
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

        private void PopulateGrid(string markerName)
            {
            List<InspectRow> rows = new List<InspectRow>();
            int totalCredit = 0;

            var dutiesByPeriod = prg.PairingDuties.OfType<PairingDuty>()
                .OrderBy(d => d.DutyPeriod).ToList();

            foreach (PairingDuty duty in dutiesByPeriod)
                {
                var dutyLegs = prg.PairingLegs.Where(l => l.DutyPeriod == duty.DutyPeriod).ToList();
                int legCreditSum = 0;

                foreach (PairingLegItem leg in dutyLegs)
                    {
                    if (leg is AirlinePairingLeg)
                        {
                        AirlinePairingLeg airLeg = (AirlinePairingLeg)leg;
                        int legCredit = LatestNonZeroCredit(airLeg.SkedCredit, airLeg.EstCredit, airLeg.ActCredit);
                        legCreditSum += legCredit;
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
                            Credit = legCredit
                            });
                        }
                    else if (leg is NonFlyingPairingLeg)
                        {
                        NonFlyingPairingLeg nfLeg = (NonFlyingPairingLeg)leg;
                        legCreditSum += nfLeg.Credit;
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
                        legCreditSum += oaLeg.Credit;
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

                MinDayAmount minDay = MinDayDiffCalculator.CalculateForDuty(duty, prg.PrgHdr.PrgDate, prg.PrgHdr.CrewType, markerName, legCreditSum);
                rows.Add(new InspectRow
                    {
                    DutyPeriod = duty.DutyPeriod.ToString(),
                    RowKind = "Duty",
                    Credit = duty.ActCredit,
                    CreditDeltaAmount = minDay.Credit,
                    Report = duty.Report.AsDisplayTime,
                    Release = FormatReleaseTime(duty.Report, duty.ActEnd),
                    DutyTime = FormatHHMM(duty.ActOnDuty),
                    Note = minDay.HasMinDay ? "Min Day" : ""
                    });

                totalCredit += duty.ActCredit;

                rows.Add(new InspectRow { RowKind = "DividerAfterDuty" });
                }

            int tripRig = prg.PrgHdr.ActTripRig;
            rows.Add(new InspectRow
                {
                RowKind = "Totals",
                ArrvTime = "Totals:",
                Credit = totalCredit + tripRig,
                CreditDeltaAmount = tripRig,
                Note = tripRig > 0 ? "Trip Rig" : ""
                });

            grid.DataSource = rows;
            }
        }
    }
