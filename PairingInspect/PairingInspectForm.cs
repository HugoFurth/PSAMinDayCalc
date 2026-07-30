using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data.OleDb;
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
        RadGridView exceptionsGrid;
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
            MinDayNameResolver.LoadDisplayNames(prg.Connection);
            minDayCriticalMessages = new List<string>();
            SetupGrid();
            SetupExceptionsGrid();
            SetupLaunchCtwpmButton();
            SetupSecuritySessionTimer();
            txtPairingID.Text = Properties.Settings.Default.LastPairingID;
            txtPairingDate.Text = PairingDateToDisplay(Properties.Settings.Default.LastPairingDate);
            txtExceptionsFromDate.Text = PairingDateToDisplay(Properties.Settings.Default.LastExceptionsFromDate);
            txtExceptionsToDate.Text = PairingDateToDisplay(Properties.Settings.Default.LastExceptionsToDate);
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

        // Temporarily surfacing every status level (not just Critical) so we can see
        // the full message trace for a recalc and decide what's worth keeping visible
        // long-term. MinDayProcess can report multiple messages in a single call (e.g.
        // one per crewmember it evaluates), so collect them here rather than surfacing
        // only the last one -- the caller (btnRecalculateMinDay_Click) clears this list
        // before each run and shows whatever accumulated afterward.
        private void MinDayProcess_StatusUpdate(object sender, MinDayStatusEventArgs args)
            {
            minDayCriticalMessages.Add("[" + args.Status + "] " + args.Message);
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

        private void SetupExceptionsGrid()
            {
            exceptionsGrid = new RadGridView();
            exceptionsGrid.Dock = DockStyle.Fill;
            exceptionsGrid.ShowGroupPanel = false;
            exceptionsGrid.MasterTemplate.AutoGenerateColumns = false;
            exceptionsGrid.MasterTemplate.AllowAddNewRow = false;
            exceptionsGrid.Columns.Add(new GridViewTextBoxColumn("PairingID", "PairingID") { HeaderText = "Pairing", TextAlignment = ContentAlignment.MiddleCenter });
            exceptionsGrid.Columns.Add(new GridViewTextBoxColumn("PairingDate", "PairingDate") { HeaderText = "Pairing Date", TextAlignment = ContentAlignment.MiddleCenter });
            exceptionsGrid.Columns.Add(new GridViewTextBoxColumn("ReportDate", "ReportDate") { HeaderText = "Report Date", TextAlignment = ContentAlignment.MiddleCenter });
            exceptionsGrid.Columns.Add(new GridViewTextBoxColumn("ReportTime", "ReportTime") { HeaderText = "Report Time", TextAlignment = ContentAlignment.MiddleCenter });
            exceptionsGrid.Columns.Add(new GridViewTextBoxColumn("EndDate", "EndDate") { HeaderText = "End Date", TextAlignment = ContentAlignment.MiddleCenter });
            exceptionsGrid.Columns.Add(new GridViewTextBoxColumn("RaisedDate", "RaisedDate") { HeaderText = "Raised Date", TextAlignment = ContentAlignment.MiddleCenter });
            exceptionsGrid.Columns.Add(new GridViewTextBoxColumn("RaisedTime", "RaisedTime") { HeaderText = "Raised Time", TextAlignment = ContentAlignment.MiddleCenter });
            exceptionsGrid.Columns.Add(new GridViewTextBoxColumn("UpdatedBy", "UpdatedBy") { HeaderText = "Updated By", TextAlignment = ContentAlignment.MiddleCenter });

            GridViewCommandColumn modifyColumn = new GridViewCommandColumn("Modify");
            modifyColumn.HeaderText = "";
            modifyColumn.UseDefaultText = true;
            modifyColumn.DefaultText = "Modify";
            exceptionsGrid.Columns.Add(modifyColumn);
            exceptionsGrid.CommandCellClick += ExceptionsGrid_CommandCellClick;

            string currentFingerprint = ExceptionsGridColumnFingerprint();
            if (File.Exists(ExceptionsGridLayoutPath) && File.Exists(ExceptionsGridFingerprintPath) &&
                File.ReadAllText(ExceptionsGridFingerprintPath) == currentFingerprint)
                exceptionsGrid.LoadLayout(ExceptionsGridLayoutPath);

            pnlExceptionsGrid.Controls.Add(exceptionsGrid);
            }

        private void ExceptionsGrid_CommandCellClick(object sender, GridViewCellEventArgs e)
            {
            if (e.Column.Name != "Modify")
                return;
            PXExceptionRow row = e.Row.DataBoundItem as PXExceptionRow;
            if (row == null)
                return;
            LaunchCtwpm(CtwpmSelectionAutomator.FunctionModify, row.PairingID, row.PairingDateRaw);
            }

        private static string ExceptionsGridLayoutPath
            {
            get { return Path.Combine(Application.StartupPath, "PairingInspectExceptionsGridLayout.xml"); }
            }

        private static string ExceptionsGridFingerprintPath
            {
            get { return Path.Combine(Application.StartupPath, "PairingInspectExceptionsGridLayout.fingerprint.txt"); }
            }

        private const string ExceptionsGridSchemaVersion = "v1";

        private string ExceptionsGridColumnFingerprint()
            {
            return ExceptionsGridSchemaVersion + "|" +
                string.Join("|", exceptionsGrid.Columns.Select(c => c.Name + "=" + c.FieldName + "=" + c.HeaderText + "=" + c.TextAlignment));
            }

        public class PXExceptionRow
            {
            public string PairingID { get; set; }
            public string PairingDate { get; set; }
            // Internal YYYYMMDD form of PairingDate -- CtwpmSelectionAutomator.Launch needs
            // this format, not the MM/DD/YY the grid displays.
            public string PairingDateRaw { get; set; }
            public string ReportDate { get; set; }
            public string ReportTime { get; set; }
            public string EndDate { get; set; }
            public string RaisedDate { get; set; }
            public string RaisedTime { get; set; }
            public uint UpdatedBy { get; set; }
            }

        // RaisedTime is stored as literal HHMM (e.g. 2214 -> 22:14, via
        // CTTimeFormat.AsRaisedTime), unlike Report_Time/duty times elsewhere in this
        // app which are minutes-since-midnight -- needs its own formatter, not FormatHHMM.
        private static string FormatRaisedTime(int hhmm)
            {
            return string.Format("{0:D2}:{1:D2}", hhmm / 100, hhmm % 100);
            }

        private void btnFindExceptions_Click(object sender, EventArgs e)
            {
            try
                {
                string fromDate = PairingDateToInternal(txtExceptionsFromDate.Text.Trim());
                string toDate = PairingDateToInternal(txtExceptionsToDate.Text.Trim());

                List<PXExceptionRow> rows = new List<PXExceptionRow>();
                using (OleDbCommand cmd = prg.Connection.CreateCommand())
                    {
                    cmd.CommandText = "SELECT Prgid_Prgno_Base+Prgid_Prgno_Eqpt+Prgid_Prgno_3+Prgid_Prgno_4_6 as PrgNo, " +
                        "Prgid_Prgdate, Report_Date, Report_Time, Enddate, RaisedDate, RaisedTime, Updateid_Updempno " +
                        "FROM PX WHERE ErrorNo_0 = ? AND Prgid_Prgdate >= ? AND Prgid_Prgdate <= ?";
                    cmd.Parameters.AddWithValue("errorNo", 6500);
                    cmd.Parameters.AddWithValue("fromDate", fromDate);
                    cmd.Parameters.AddWithValue("toDate", toDate);
                    using (OleDbDataReader reader = cmd.ExecuteReader())
                        {
                        while (reader.Read())
                            {
                            string rawPairingDate = reader["Prgid_Prgdate"].ToString();
                            rows.Add(new PXExceptionRow
                                {
                                PairingID = reader["PrgNo"].ToString().Trim(),
                                PairingDate = PairingDateToDisplay(rawPairingDate),
                                PairingDateRaw = rawPairingDate,
                                ReportDate = PairingDateToDisplay(reader["Report_Date"].ToString()),
                                ReportTime = FormatHHMM(Convert.ToInt32(reader["Report_Time"])),
                                EndDate = PairingDateToDisplay(reader["Enddate"].ToString()),
                                RaisedDate = PairingDateToDisplay(reader["RaisedDate"].ToString()),
                                RaisedTime = FormatRaisedTime(Convert.ToInt32(reader["RaisedTime"])),
                                UpdatedBy = Convert.ToUInt32(reader["Updateid_Updempno"])
                                });
                            }
                        }
                    }

                exceptionsGrid.DataSource = rows;
                }
            catch (Exception ex)
                {
                RadMessageBox.Show("Error finding exceptions: " + ex.Message);
                }
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
            exceptionsGrid.SaveLayout(ExceptionsGridLayoutPath);
            File.WriteAllText(ExceptionsGridFingerprintPath, ExceptionsGridColumnFingerprint());
            Properties.Settings.Default.LastPairingID = txtPairingID.Text;
            Properties.Settings.Default.LastPairingDate = PairingDateToInternal(txtPairingDate.Text);
            Properties.Settings.Default.LastExceptionsFromDate = PairingDateToInternal(txtExceptionsFromDate.Text);
            Properties.Settings.Default.LastExceptionsToDate = PairingDateToInternal(txtExceptionsToDate.Text);
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
                MinDayMarkerStatus minDayMarkerStatus = MinDayNameResolver.Resolve(prg.PrgHdr.UpdateidUpdempno);
                DisplayHeader(minDayMarkerStatus);
                PopulateGrid(minDayMarkerStatus);
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
                MinDayMarkerStatus minDayMarkerStatus = MinDayNameResolver.Resolve(prg.PrgHdr.UpdateidUpdempno);
                DisplayHeader(minDayMarkerStatus);
                PopulateGrid(minDayMarkerStatus);

                if (minDayCriticalMessages.Count > 0)
                    ShowMinDayTrace(string.Join(Environment.NewLine, minDayCriticalMessages));
                }
            catch (Exception ex)
                {
                RadMessageBox.Show("Error recalculating min day: " + ex.Message);
                }
            }

        // RadTextBox/RadForm rather than RadMessageBox -- the trace can run to many lines
        // and the user needs to select/copy it, which RadMessageBox doesn't support well.
        private void ShowMinDayTrace(string trace)
            {
            using (RadForm dlg = new RadForm())
                {
                dlg.ThemeName = RadMessageBox.ThemeName;
                dlg.Text = "Min Day Recalculation Trace";
                dlg.Width = 700;
                dlg.Height = 500;
                dlg.StartPosition = FormStartPosition.CenterParent;

                RadTextBox txt = new RadTextBox();
                txt.ThemeName = RadMessageBox.ThemeName;
                txt.Multiline = true;
                txt.ReadOnly = true;
                txt.Dock = DockStyle.Fill;
                txt.Font = new Font(FontFamily.GenericMonospace, 9);
                txt.Text = trace;

                RadPanel textPanel = new RadPanel();
                textPanel.ThemeName = RadMessageBox.ThemeName;
                textPanel.Dock = DockStyle.Fill;
                textPanel.Controls.Add(txt);

                RadButton btnOk = new RadButton();
                btnOk.ThemeName = RadMessageBox.ThemeName;
                btnOk.Text = "OK";
                btnOk.Size = new Size(80, 28);
                btnOk.Click += (s, e) => dlg.Close();

                RadPanel buttonPanel = new RadPanel();
                buttonPanel.ThemeName = RadMessageBox.ThemeName;
                buttonPanel.Dock = DockStyle.Bottom;
                buttonPanel.Height = 45;
                buttonPanel.Controls.Add(btnOk);

                // Centering by fixed math (e.g. off dlg.Width) is unreliable here --
                // dlg.Width includes window chrome, not buttonPanel's actual client
                // area, and that area isn't resolved until the dock layout runs. Doing
                // it on Shown (after layout, right before ShowDialog blocks) uses the
                // real, final size instead.
                dlg.Shown += (s, e) =>
                    {
                    btnOk.Left = (buttonPanel.ClientSize.Width - btnOk.Width) / 2;
                    btnOk.Top = (buttonPanel.ClientSize.Height - btnOk.Height) / 2;
                    };

                // Dock=Bottom control must be added before the Dock=Fill one, or the
                // fill control claims the whole client area before the bottom strip
                // gets a chance to reserve its space.
                dlg.Controls.Add(buttonPanel);
                dlg.Controls.Add(textPanel);
                dlg.ShowDialog(this);
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
            if (prg.PrgHdr != null)
                LaunchCtwpm(function, prg.PrgHdr.PrgID, prg.PrgHdr.PrgDate);
            else
                LaunchCtwpm(function, null, null);
            }

        // Used directly by the Min Day Exceptions grid's Modify button, which acts on
        // whichever pairing is in that row -- not necessarily the one loaded in prg.
        private void LaunchCtwpm(string function, string prgID, string prgDate)
            {
            try
                {
                string ctExeDir = SFIConfig.AppSetting("CTEXEDIR");
                if (prgID != null)
                    CtwpmSelectionAutomator.Launch(ctExeDir, function, prgID, prgDate);
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

        private void DisplayHeader(MinDayMarkerStatus minDayMarkerStatus)
            {
            PairingHeader hdr = prg.PrgHdr;

            lblStatusValue.Text = hdr.Canceled ? "Canceled" : "Active";
            lblCrewTypeValue.Text = CrewTypeDisplay(hdr.CrewType);
            lblMinDayStatusValue.Text = MinDayNameResolver.DisplayName(minDayMarkerStatus) + " (empno=" + hdr.UpdateidUpdempno + ")";
            btnRecalculateMinDay.Text = MinDayNameResolver.IsKnownMinDayMarker(hdr.UpdateidUpdempno)
                ? "Recalculate Min Day" : "Calculate Min Day";
            LayoutHeaderLabels();
            }

        private void LayoutHeaderLabels()
            {
            const int fieldGap = 20;
            const int titleValueGap = 4;
            int y = lblStatusTitle.Top;
            int x = lblStatusTitle.Left;

            Label[] labels = { lblStatusTitle, lblStatusValue, lblCrewTypeTitle, lblCrewTypeValue, lblMinDayStatusTitle, lblMinDayStatusValue };
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

        private void PopulateGrid(MinDayMarkerStatus minDayMarkerStatus)
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

                MinDayAmount minDay = MinDayDiffCalculator.CalculateForDuty(duty, prg.PrgHdr.PrgDate, prg.PrgHdr.CrewType, minDayMarkerStatus, legCreditSum);
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
