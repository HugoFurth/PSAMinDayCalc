using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data;
using System.Data.OleDb;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using SFIConfigUtils;
using SFICTDataAccess;
using SFICTDateTimeUtils;
using MinDayProcessNS;
using Telerik.WinControls;
using Telerik.WinControls.UI;

namespace PairingInspect2
{
    public partial class RadForm1 : Telerik.WinControls.UI.RadForm
    {
        const int MaxRecentPairings = 20;

        CTPairing prg;
        CTCrewOnPairing crewOnPairingLookup;
        List<CrewMemOnPairing> crewOnPairing;
        CTOTs openPositionsLookup;
        CTEmployees employees;
        List<string> minDayCriticalMessages;
        RadGridView grid;
        RadGridView crewGrid;
        RadGridView exceptionsGrid;
        MinDayProcess minDayProcess;
        ContextMenuStrip ctwpmFunctionMenu;
        ToolStripMenuItem itemModify;
        ContextMenuStrip exceptionsPairingMenu;
        ContextMenuStrip printFunctionMenu;
        PXExceptionRow exceptionsContextRow; // row the exceptions grid's PairingID right-click menu is currently acting on
        Dictionary<string, string> recentPairingLookup; // displayed "PairingID MM/DD" -> full stored "PairingID InternalDate" entry
        // Full stored "PairingID InternalDate" entry -> the color it should show as in the
        // Recent Pairings box (currently only ever Red, for a Canceled pairing). Session-
        // only -- populated as AddToRecentList sees each pairing's current status, not
        // persisted and not reset by RefreshRecentComboBox, so an entry keeps its color
        // across dropdown rebuilds but a persisted entry from a PRIOR session shows no
        // color at all until it's looked up again this session.
        Dictionary<string, Color> recentPairingColor = new Dictionary<string, Color>();
        Timer securitySessionTimer;

        // True while the Inspect tab is showing a pairing loaded from a JSON file
        // (DisplayProposedPairing) instead of a real, DB-backed lookup -- it has no
        // CrewTrac record yet, so Recalculate/Evaluate/Pairing Function (all of which
        // act on a real pairing number) stay disabled the whole time this is set. Reset
        // to false the moment a real lookup runs (btnLookUp_Click), regardless of outcome.
        bool viewingProposedPairing;

        // args -- e.g. { "A7669", "20261103" } -- lets a launcher (CrewRec) open PrgInsp
        // straight to a specific pairing instead of whatever was last viewed. Optional so
        // existing callers (the designer, any other `new RadForm1()`) keep compiling.
        public RadForm1(string[] args = null)
        {
            // .NET scopes user settings (RecentPairings, LastPairingID, print preview
            // size, grid layouts) to the assembly's own version number -- a version
            // bump otherwise silently starts from a blank settings folder, with the
            // real data just sitting orphaned under the previous version's folder.
            // Confirmed live: bumping 1.0.0.0 -> 1.0.0.1 wiped Recent Pairings and
            // LastPairingID this way. Upgrade() pulls forward from the most recent
            // prior version once; the guard flag keeps it from re-running (and
            // potentially clobbering changes made under the current version) on
            // every subsequent launch of the same version.
            if (!Properties.Settings.Default.SettingsUpgraded)
                {
                Properties.Settings.Default.Upgrade();
                Properties.Settings.Default.SettingsUpgraded = true;
                Properties.Settings.Default.Save();
                }

            InitializeComponent();
            RadMessageBox.ThemeName = (new Telerik.WinControls.Themes.Office2010BlackTheme()).ThemeName;
            if (this.DesignMode)
                return;

            // Only ever 2 fixed pages (Inspect/Exceptions) -- the strip's scroll-left/
            // scroll-right arrows (permanently disabled, nothing to scroll to) and its
            // close ("x") button (closing the only two tabs makes no sense here) are
            // pure clutter. StripViewButtons.None removes all of them at once.
            RadPageViewStripElement tabStrip = tabMain.ViewElement as RadPageViewStripElement;
            if (tabStrip != null)
                tabStrip.StripButtons = StripViewButtons.None;

            new CTAirlineHeader().Fill(); // always read ah first.
            prg = new CTPairing();
            MinDayNameResolver.LoadDisplayNames(prg.Connection);
            LoadCrewPosOrder();

            crewOnPairingLookup = new CTCrewOnPairing();
            employees = new CTEmployees();
            employees.FillAll(); // includes inactive/terminated crew, not just currently-active -- a pairing can reference either
            crewOnPairingLookup.GetEmpName = employees.GetLastFirstname;
            openPositionsLookup = new CTOTs();
            minDayCriticalMessages = new List<string>();

            SetupCrewGrid();
            SetupGrid();
            SetupExceptionsGrid();
            txtExceptionsFromDate.Leave += txtExceptionsFromDate_Leave;
            SetupLaunchCtwpmButton();
            SetupPrintPairingButton();
            SetupPairingDragDrop();
            SetupSecuritySessionTimer();
            cboRecentPairings.VisualListItemFormatting += CboRecentPairings_VisualListItemFormatting;

            txtPairingID.Text = Properties.Settings.Default.LastPairingID;
            txtPairingDate.NullableValue = InternalDateToNullable(Properties.Settings.Default.LastPairingDate);
            txtExceptionsFromDate.NullableValue = InternalDateToNullable(Properties.Settings.Default.LastExceptionsFromDate);
            txtExceptionsToDate.NullableValue = InternalDateToNullable(Properties.Settings.Default.LastExceptionsToDate);
            RefreshRecentComboBox();

            // A single .json file argument is how a double-click on a pairing-load file
            // (Windows file-type association) launches us -- same JSON a drag-drop of
            // that file onto the Inspect tab loads, see LoadProposedPairingFromJsonFile.
            // A pairing passed on the command line as two args takes precedence over
            // whatever was last viewed -- same TryParsePairingRef parsing the drag-drop
            // path uses, so "PrgInsp.exe A7669 20261103" behaves identically to dropping
            // "A7669 20261103" onto the Inspect tab. Bad/missing args just fall through
            // to the normal last-pairing restore below -- no error shown either way.
            string cmdLinePrgID;
            DateTime? cmdLinePrgDate;
            if (args != null && args.Length == 1 && IsJsonPairingFile(args[0]))
                LoadProposedPairingFromJsonFile(args[0]);
            else if (args != null && args.Length >= 2 && TryParsePairingRef(args[0] + " " + args[1], out cmdLinePrgID, out cmdLinePrgDate))
                LookUpParsedPairing(cmdLinePrgID, cmdLinePrgDate.Value);
            else if (!string.IsNullOrWhiteSpace(txtPairingID.Text) && txtPairingDate.NullableValue.HasValue)
                btnLookUp_Click(this, EventArgs.Empty);

            if (txtExceptionsFromDate.NullableValue.HasValue && txtExceptionsToDate.NullableValue.HasValue)
                btnFindExceptions_Click(this, EventArgs.Empty);
        }

        private void cboRecentPairings_SelectedIndexChanged(object sender, Telerik.WinControls.UI.Data.PositionChangedEventArgs e)
        {
            UpdateRecentPairingsBoxForeColor();

            if (cboRecentPairings.SelectedItem == null)
                return;
            string entry;
            if (!recentPairingLookup.TryGetValue(cboRecentPairings.SelectedItem.ToString(), out entry))
                return;
            int spaceIdx = entry.IndexOf(' ');
            if (spaceIdx <= 0)
                return;
            txtPairingID.Text = entry.Substring(0, spaceIdx);
            txtPairingDate.NullableValue = InternalDateToNullable(entry.Substring(spaceIdx + 1));
            btnLookUp_Click(sender, e);
        }

        private void AddToRecentList(string pairingID, string pairingDate, Color? statusColor = null)
        {
            string entry = pairingID + " " + pairingDate;

            if (statusColor.HasValue)
                recentPairingColor[entry] = statusColor.Value;
            else
                recentPairingColor.Remove(entry);

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
            // Saved immediately rather than only on normal close -- an abnormal
            // termination (crash, forced kill, power loss) would otherwise lose
            // whatever recent-pairing updates happened since the last clean close.
            Properties.Settings.Default.Save();

            RefreshRecentComboBox();
        }

        private void RefreshRecentComboBox()
        {
            cboRecentPairings.SelectedIndexChanged -= cboRecentPairings_SelectedIndexChanged;
            List<string> displayItems = new List<string>();
            recentPairingLookup = new Dictionary<string, string>();
            if (Properties.Settings.Default.RecentPairings != null)
                foreach (string entry in Properties.Settings.Default.RecentPairings)
                {
                    int spaceIdx = entry.IndexOf(' ');
                    if (spaceIdx <= 0)
                        continue;
                    string display = entry.Substring(0, spaceIdx) + " " + CTDateFormat.DisplayWithoutYear(entry.Substring(spaceIdx + 1));
                    displayItems.Add(display);
                    recentPairingLookup[display] = entry;
                }
            cboRecentPairings.DataSource = displayItems;
            cboRecentPairings.SelectedIndexChanged += cboRecentPairings_SelectedIndexChanged;
            // The handler above is deliberately detached during the DataSource assignment
            // (same reason as always -- avoid a spurious re-lookup from the implicit
            // index reset), so whatever selection that reset lands on never ran through
            // it. Update the closed-box color explicitly here too, not just inside the
            // handler, to cover that case.
            UpdateRecentPairingsBoxForeColor();
        }

        // Colors each Recent Pairings dropdown entry from recentPairingColor (keyed by the
        // same "PrgID InternalDate" string recentPairingLookup maps display text to) --
        // red for a Canceled pairing, default otherwise/unknown. Covers the OPEN popup's
        // list items; UpdateRecentPairingsBoxForeColor (below) covers the CLOSED box's
        // own displayed text, which doesn't go through this event.
        private void CboRecentPairings_VisualListItemFormatting(object sender, VisualItemFormattingEventArgs e)
        {
            RadListDataItem dataItem = e.VisualItem.Data;
            string entry;
            Color color;
            if (dataItem != null && dataItem.Text != null && recentPairingLookup != null &&
                recentPairingLookup.TryGetValue(dataItem.Text, out entry) &&
                recentPairingColor.TryGetValue(entry, out color))
                e.VisualItem.ForeColor = color;
            else
                e.VisualItem.ResetValue(LightVisualElement.ForeColorProperty, ValueResetFlags.Local);
        }

        // RadDropDownList's closed-box display text is a separate visual element from the
        // popup list items -- VisualListItemFormatting never fires for it, so it needs its
        // own explicit color set, called both whenever the selection actually changes and
        // whenever the list is rebound (DataSource reset implicitly changes what's shown
        // without raising SelectedIndexChanged, since that handler is detached for the
        // assignment -- see RefreshRecentComboBox).
        private void UpdateRecentPairingsBoxForeColor()
        {
            string entry;
            Color color;
            if (cboRecentPairings.SelectedItem != null &&
                recentPairingLookup.TryGetValue(cboRecentPairings.SelectedItem.ToString(), out entry) &&
                recentPairingColor.TryGetValue(entry, out color))
                cboRecentPairings.ForeColor = color;
            else
                cboRecentPairings.ForeColor = SystemColors.ControlText;
        }

        // RadDropDownButton is used only for its arrow chrome -- its own Items/RadMenuItem popup
        // fires Click on mere hover/highlight rather than requiring an actual click, so the menu
        // itself is a plain ContextMenuStrip (native WinForms, click-only) shown from the button's
        // own Click. DropDownOpening is cancelled so Telerik's native (now-empty) popup never
        // flashes up alongside it.
        private void SetupLaunchCtwpmButton()
        {
            ctwpmFunctionMenu = new ContextMenuStrip();

            ToolStripMenuItem itemInquire = new ToolStripMenuItem("Inquire");
            itemInquire.Click += itemPrgInquire_Click;
            ctwpmFunctionMenu.Items.Add(itemInquire);

            itemModify = new ToolStripMenuItem("Modify");
            itemModify.Click += itemPrgModify_Click;
            ctwpmFunctionMenu.Items.Add(itemModify);

            btnLaunchCTWPM.DropDownButtonElement.DropDownOpening += btnLaunchCTWPM_DropDownOpening;
        }

        private void btnLaunchCTWPM_DropDownOpening(object sender, CancelEventArgs e)
        {
            e.Cancel = true;
        }

        // Same pattern as btnLaunchCTWPM: body and arrow do the same thing (show the
        // menu), so the native dropdown is cancelled in favor of a manually-shown
        // ContextMenuStrip. The button itself is just a static "Output" label -- all
        // 3 modes are plain menu choices, there's no sticky/remembered default.
        private void SetupPrintPairingButton()
        {
            btnPrintPairing.Text = "Output";

            printFunctionMenu = new ContextMenuStrip();

            ToolStripMenuItem itemPrint = new ToolStripMenuItem("Print");
            itemPrint.Click += (s, e) => DoPrint();
            printFunctionMenu.Items.Add(itemPrint);

            ToolStripMenuItem itemPreview = new ToolStripMenuItem("Print Preview");
            itemPreview.Click += (s, e) => DoPrintPreview();
            printFunctionMenu.Items.Add(itemPreview);

            ToolStripMenuItem itemSaveAsPdf = new ToolStripMenuItem("Save as PDF");
            itemSaveAsPdf.Click += (s, e) => DoSaveAsPdf();
            printFunctionMenu.Items.Add(itemSaveAsPdf);

            btnPrintPairing.DropDownButtonElement.DropDownOpening += btnPrintPairing_DropDownOpening;
        }

        private void btnPrintPairing_DropDownOpening(object sender, CancelEventArgs e)
        {
            e.Cancel = true;
        }

        // Same File menu as PSAPBSX's own main form (Connection Info / About / Exit).
        private void connectionInfoToolStripMenuItem_Click(object sender, EventArgs e)
            {
            frmConnectionInfo ci = new frmConnectionInfo();
            ci.ConnectionString = prg.Connection.ConnectionString;
            ci.ShowDialog();
            }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
            {
            AboutBox1 ab = new AboutBox1();
            ab.ShowDialog();
            }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
            {
            Close();
            }

        private void btnLaunchCTWPM_Click(object sender, EventArgs e)
        {
            ctwpmFunctionMenu.Show(btnLaunchCTWPM, new Point(0, btnLaunchCTWPM.Height));
        }

        private void itemPrgInquire_Click(object sender, EventArgs e)
        {
            // Inquire can still leave the pairing changed underneath us (e.g. an Evaluate
            // run from CTWPM's pairing screen), so refresh once the user closes it --
            // Modify intentionally doesn't get this, since its own flow already expects
            // the user to come back and re-look-up manually.
            //
            // A cancelled pairing won't be found under the selection dialog's default
            // Active filter -- pre-select Inactive for it so Inquire actually finds the
            // record instead of erroring.
            bool selectInactive = prg.PrgHdr != null && prg.PrgHdr.Canceled;
            LaunchCtwpm(CtwpmSelectionAutomator.FunctionInquire, refreshOnExit: true, selectInactive: selectInactive);
        }

        private void itemPrgModify_Click(object sender, EventArgs e)
        {
            LaunchCtwpm(CtwpmSelectionAutomator.FunctionModify);
        }

        // Shared "operation in progress" gate for the three pairing-level action buttons.
        // Recalculate, Evaluate, and Pairing Function (Inquire/Modify) all act on the same
        // pairing -- and Pairing Function's Inquire/Modify can launch the exact same CTWPM
        // process Evaluate does -- so starting any one of them disables all three, not just
        // the one clicked, rather than letting the others run concurrently against it.
        private void SetPairingActionButtonsBusy(bool busy)
            {
            if (busy)
                {
                btnRecalculateMinDay.Enabled = false;
                btnEvaluatePairing.Enabled = false;
                btnLaunchCTWPM.Enabled = false;
                }
            else
                {
                // Same conditions btnLookUp_Click's own success path uses -- Pairing
                // Function has no such gating anywhere else otherwise, so it's
                // unconditional except for the viewingProposedPairing override, which
                // applies to all three alike (see that field's own comment).
                btnRecalculateMinDay.Enabled = !viewingProposedPairing && prg.PrgHdr != null && !prg.PrgHdr.Canceled && !minDayVersionMismatch && !RecalculateBlockedByMarkerStatus();
                btnEvaluatePairing.Enabled = !viewingProposedPairing && prg.PrgHdr != null && !prg.PrgHdr.Canceled;
                btnLaunchCTWPM.Enabled = !viewingProposedPairing;
                }
            }

        // MinDayProcess.ProcessPairing itself no-ops the instant it sees one of these two
        // markers ("Min day already applied -- skipping" / "Min day condition not present -
        // skipping") -- clicking Recalculate in either state can't do anything, so it stays
        // disabled rather than inviting a click that's a guaranteed no-op. Exception/Error/
        // Unknown all still leave Recalculate enabled -- those are exactly the states where
        // running it again is meaningful (retry, or a pairing never yet examined).
        private bool RecalculateBlockedByMarkerStatus()
            {
            if (prg.PrgHdr == null)
                return false;
            MinDayMarkerStatus status = MinDayNameResolver.Resolve(prg.PrgHdr.UpdateidUpdempno);
            return status == MinDayMarkerStatus.Applied || status == MinDayMarkerStatus.NoUpdateNeeded;
            }

        private void btnEvaluatePairing_Click(object sender, EventArgs e)
        {
            if (prg.PrgHdr == null)
                return;
            // Re-enabled in RefreshAfterCtwpmExit (success path) or EvaluatePairing's own
            // catch (launch failure) -- never left permanently disabled either way.
            SetPairingActionButtonsBusy(true);
            EvaluatePairing(prg.PrgHdr.PrgID, prg.PrgHdr.PrgDate);
        }

        private enum PrintMode { Print, Preview, SaveAsPdf }

        // Body click shows the same menu the arrow does (see SetupPrintPairingButton).
        private void btnPrintPairing_Click(object sender, EventArgs e)
            {
            printFunctionMenu.Show(btnPrintPairing, new Point(0, btnPrintPairing.Height));
            }

        private void DoPrint()
            {
            PrintPairing(PrintMode.Print, null);
            }

        private void DoPrintPreview()
            {
            PrintPairing(PrintMode.Preview, null);
            }

        // RadPrintDocument.DocumentName is display-only in the print dialog -- it's
        // not what "Microsoft Print to PDF"'s own save prompt uses for its suggested
        // filename (confirmed: Telerik's own forum guidance is PrinterSettings.
        // PrintFileName, which only takes effect with PrintToFile -- i.e. no dialog
        // at all, it just writes straight to that path). Can't have both an
        // interactive dialog AND a pre-filled PDF name, so this asks for the save
        // location itself first (via a normal SaveFileDialog, pre-filled the way the
        // Print dialog couldn't be), then prints straight to that path with no
        // further prompt.
        private void DoSaveAsPdf()
            {
            if (prg.PrgHdr == null)
                return;

            using (SaveFileDialog dlg = new SaveFileDialog())
                {
                dlg.Filter = "PDF Files (*.pdf)|*.pdf";
                dlg.FileName = txtPairingID.Text + "_" + prg.PrgHdr.PrgDate + ".pdf";
                if (dlg.ShowDialog() == DialogResult.OK)
                    PrintPairing(PrintMode.SaveAsPdf, dlg.FileName);
                }
            }

        // Prints the Inspect tab's grid via RadGridView's own built-in Print/PrintPreview --
        // no export library needed, Telerik.WinControls.GridView.dll already has it.
        // The header info (PrgID/date/crew type/status) lives in labels outside the
        // grid, not in its rows -- rather than fake a grid row for it (which can't
        // span columns), it goes into RadPrintDocument's own MiddleHeader, which
        // prints across the full page width regardless of grid columns.
        private void PrintPairing(PrintMode mode, string pdfPath)
            {
            if (prg.PrgHdr == null)
                return;

            List<InspectRow> realRows = grid.DataSource as List<InspectRow>;
            if (realRows == null)
                return;

            // FitPageWidth scales every column down by the same uniform ratio to fit
            // the page -- it doesn't treat MinWidth as a floor (confirmed: raising
            // DutyTime's MinWidth from 80 to 110 made no difference), so a column
            // that's already wide enough on screen can still end up too narrow once
            // printed. Taking width fully out of Telerik's hands for print: set every
            // column's own absolute print width explicitly (Duty Time generously
            // oversized relative to the short columns), so FitPageWidth's uniform
            // scale-down still leaves it legible. Saved/restored so the on-screen
            // grid's own sizing is untouched.
            var realColumnWidths = new Dictionary<string, int>();
            var printColumnWidths = new Dictionary<string, int>
                {
                { "DutyPeriod", 40 }, { "Date", 55 }, { "OA", 35 }, { "FlightNum", 55 },
                { "Deadhead", 40 }, { "DeptCity", 40 }, { "ArrvCity", 40 }, { "DeptTime", 50 },
                { "ArrvTime", 50 }, { "Credit", 55 }, { "CreditHHMM", 70 }, { "Report", 50 },
                { "Release", 50 }, { "DutyTime", 85 }, { "Note", 90 }
                };
            foreach (KeyValuePair<string, int> kv in printColumnWidths)
                realColumnWidths[kv.Key] = grid.Columns[kv.Key].Width;

            try
                {
                foreach (KeyValuePair<string, int> kv in printColumnWidths)
                    grid.Columns[kv.Key].Width = kv.Value;


                // Collapse consecutive divider rows (DividerAfterDuty immediately
                // followed by the next duty's DividerBeforeDuty; the deliberate
                // double DividerAfterTotals below Totals) down to one, for print
                // only -- on-screen the grid's own ViewRowFormatting already shrinks
                // each one to 4px, but doubling or tripling them up still means more
                // visible line weight than intended once printed.
                var printRows = new List<InspectRow>();
                bool prevWasDivider = false;
                foreach (InspectRow row in realRows)
                    {
                    bool isDivider = row.RowKind == "DividerBeforeDuty" || row.RowKind == "DividerAfterDuty" || row.RowKind == "DividerAfterTotals";
                    if (isDivider && prevWasDivider)
                        continue;
                    printRows.Add(row);
                    prevWasDivider = isDivider;
                    }
                grid.DataSource = printRows;

                // ViewRowFormatting/PrintCellFormatting both fire too late in
                // Telerik's print pipeline to shrink these rows for the printed
                // output -- setting Height directly on the bound GridViewRowInfo
                // objects right after rebinding, before Print/PrintPreview is even
                // called, is what actually sticks.
                foreach (Telerik.WinControls.UI.GridViewRowInfo rowInfo in grid.Rows)
                    {
                    InspectRow row = rowInfo.DataBoundItem as InspectRow;
                    if (row != null && (row.RowKind == "DividerBeforeDuty" || row.RowKind == "DividerAfterDuty" || row.RowKind == "DividerAfterTotals"))
                        {
                        rowInfo.MinHeight = 4;
                        rowInfo.MaxHeight = 4;
                        rowInfo.Height = 4;
                        }
                    }

                // Columns shrunk proportionally to fit the page width instead of
                // spilling off the right edge.
                grid.PrintStyle.FitWidthMode = Telerik.WinControls.UI.PrintFitWidthMode.FitPageWidth;

                Telerik.WinControls.UI.RadPrintDocument printDoc = new Telerik.WinControls.UI.RadPrintDocument();
                printDoc.Landscape = true;
                printDoc.HeaderHeight = 40;
                // What "Microsoft Print to PDF" (and most print drivers) pre-fills as
                // the suggested filename/document name.
                printDoc.DocumentName = txtPairingID.Text + "_" + prg.PrgHdr.PrgDate;
                // Kept non-empty (not "") purely so Telerik still reserves
                // HeaderHeight's space above the grid -- the actual visible text is
                // drawn by DrawPairingPrintHeader below instead of this plain string,
                // since MiddleHeader can't mix bold/regular within itself.
                printDoc.MiddleHeader = " ";

                var headerSegments = new List<Tuple<string, bool>>
                    {
                    Tuple.Create("", true), Tuple.Create(txtPairingID.Text, true),
                    Tuple.Create("   ", false), Tuple.Create(txtPairingDate.Text, true)
                    };
                // CA/FO only, from the same crew list the Crew grid on this tab
                // already fetches (crewOnPairing) -- a pairing missing one of these
                // (e.g. not fully crewed yet) just omits that segment rather than
                // printing a blank label.
                foreach (CrewMemOnPairing c in crewOnPairing)
                    {
                    if (c.PairingPos != "CA" && c.PairingPos != "FO")
                        continue;
                    string assignCodeSuffix = (c.AssignCode == "RAS" || c.AssignCode == "TTA") ? " (" + c.AssignCode + ")" : "";
                    headerSegments.Add(Tuple.Create("   " + c.PairingPos + ": ", false));
                    headerSegments.Add(Tuple.Create(c.EmpNum + " " + c.LastFirstName + assignCodeSuffix, true));
                    }
                // Min Day status doesn't apply to a cancelled pairing -- drop the
                // field entirely rather than show a marker that's leftover from
                // before cancellation, and flag the cancellation itself instead.
                if (prg.PrgHdr.Canceled)
                    {
                    headerSegments.Add(Tuple.Create("   (Canceled)", true));
                    }
                else
                    {
                    headerSegments.Add(Tuple.Create("   Min Day: ", false));
                    headerSegments.Add(Tuple.Create(MinDayNameResolver.DisplayName(MinDayNameResolver.Resolve(prg.PrgHdr.UpdateidUpdempno)), true));
                    }
                if (mode == PrintMode.SaveAsPdf)
                    {
                    printDoc.PrinterSettings = new System.Drawing.Printing.PrinterSettings
                        {
                        PrinterName = "Microsoft Print to PDF",
                        PrintToFile = true,
                        PrintFileName = pdfPath
                        };
                    }

                System.Drawing.Printing.PrintPageEventHandler headerHandler = (s2, pe) => DrawPairingPrintHeader(pe, printDoc, headerSegments);
                printDoc.PrintPage += headerHandler;
                try
                    {
                    switch (mode)
                        {
                        case PrintMode.Preview:
                            ShowPrintPreview(printDoc);
                            break;
                        case PrintMode.SaveAsPdf:
                            // Already fully configured above (printer, PrintToFile,
                            // PrintFileName) -- showPrinterSettings=false so this
                            // doesn't pop a dialog on top of that.
                            grid.Print(false, printDoc);
                            break;
                        default:
                            grid.Print(true, printDoc);
                            break;
                        }
                    }
                finally
                    {
                    printDoc.PrintPage -= headerHandler;
                    }
                }
            finally
                {
                foreach (KeyValuePair<string, int> kv in realColumnWidths)
                    grid.Columns[kv.Key].Width = kv.Value;
                grid.DataSource = realRows;
                }
            }

        // grid.PrintPreview(printDoc) opens its own dialog at a fixed default size
        // with no way to configure it -- constructing RadPrintPreviewDialog directly
        // instead gives control over the initial size (and lets the last-used size be
        // remembered across sessions, the same way the pairing/exceptions grid
        // layouts already are via PairingInspect2.Properties.Settings). Setting
        // AssociatedObject on the document is what grid.PrintPreview() does
        // internally to wire the grid's content into the dialog -- without it the
        // dialog would just show a blank page.
        private void ShowPrintPreview(Telerik.WinControls.UI.RadPrintDocument printDoc)
            {
            printDoc.AssociatedObject = grid;

            using (Telerik.WinControls.UI.RadPrintPreviewDialog previewDialog = new Telerik.WinControls.UI.RadPrintPreviewDialog(printDoc))
                {
                int savedWidth = Properties.Settings.Default.PrintPreviewWidth;
                int savedHeight = Properties.Settings.Default.PrintPreviewHeight;
                Size desiredSize = (savedWidth > 0 && savedHeight > 0)
                    ? new Size(savedWidth, savedHeight)
                    : new Size(1100, 800); // generous default, well above the dialog's own small default
                Rectangle workingArea = Screen.FromControl(this).WorkingArea;
                previewDialog.Size = new Size(
                    Math.Min(desiredSize.Width, workingArea.Width),
                    Math.Min(desiredSize.Height, workingArea.Height));
                previewDialog.StartPosition = FormStartPosition.CenterParent;

                previewDialog.ShowDialog(this);

                Properties.Settings.Default.PrintPreviewWidth = previewDialog.Size.Width;
                Properties.Settings.Default.PrintPreviewHeight = previewDialog.Size.Height;
                Properties.Settings.Default.Save();
                }
            }

        // Draws the pairing summary into the header band RadPrintDocument.HeaderHeight
        // reserves, each label in regular weight and each value in bold -- the plain
        // LeftHeader/MiddleHeader/RightHeader strings share one HeaderFont and can't
        // mix weights within themselves, so this draws it manually instead. Shrinks
        // the font first if the full text would overflow the printable width, rather
        // than letting it run off the page or get clipped.
        private void DrawPairingPrintHeader(System.Drawing.Printing.PrintPageEventArgs pe, Telerik.WinControls.UI.RadPrintDocument printDoc, List<Tuple<string, bool>> segments)
            {
            const float startFontSize = 10f;
            const float minFontSize = 6f;
            float maxWidth = pe.MarginBounds.Width;

            float fontSize = startFontSize;
            float totalWidth;
            while (true)
                {
                using (Font testRegular = new Font("Microsoft Sans Serif", fontSize, FontStyle.Regular))
                using (Font testBold = new Font("Microsoft Sans Serif", fontSize, FontStyle.Bold))
                    {
                    totalWidth = 0;
                    foreach (Tuple<string, bool> seg in segments)
                        totalWidth += pe.Graphics.MeasureString(seg.Item1, seg.Item2 ? testBold : testRegular).Width;
                    }
                if (totalWidth <= maxWidth || fontSize <= minFontSize)
                    break;
                fontSize -= 0.5f;
                }

            using (Font regularFont = new Font("Microsoft Sans Serif", fontSize, FontStyle.Regular))
            using (Font boldFont = new Font("Microsoft Sans Serif", fontSize, FontStyle.Bold))
                {
                float x = pe.MarginBounds.Left + Math.Max(0, (maxWidth - totalWidth) / 2);
                float y = pe.MarginBounds.Top - printDoc.HeaderHeight + (printDoc.HeaderHeight - regularFont.Height) / 2f;
                foreach (Tuple<string, bool> seg in segments)
                    {
                    Font f = seg.Item2 ? boldFont : regularFont;
                    pe.Graphics.DrawString(seg.Item1, f, Brushes.Black, x, y);
                    x += pe.Graphics.MeasureString(seg.Item1, f).Width;
                    }
                }
            }

        // Silent, no-interaction Evaluate -- launches CTWPM in Inquire mode, auto-clicks past the
        // selection dialog, finds and clicks the pairing screen's "Eval&uate" button (moved
        // off-screen first so the user never sees it), then closes the window automatically.
        // Always refreshes on exit since the whole point is to change the pairing's evaluated
        // state -- RefreshAfterCtwpmExit itself guards on prg.PrgHdr already matching this
        // prgID/prgDate, so calling this for a pairing that isn't the one currently loaded in
        // the Inspect tab (e.g. from the exceptions grid's menu) safely no-ops the refresh
        // instead of clobbering whatever's actually displayed there.
        private void EvaluatePairing(string prgID, string prgDate)
        {
            try
            {
                string ctExeDir = SFIConfig.AppSetting("CTEXEDIR");
                Process ctwpmProcess = CtwpmSelectionAutomator.LaunchAndEvaluate(ctExeDir, prgID, prgDate);
                ctwpmProcess.EnableRaisingEvents = true;
                ctwpmProcess.Exited += (s, args) => RefreshAfterCtwpmExit(prgID, prgDate);
                // LaunchAndEvaluate already ran the whole click/close sequence before returning,
                // so the process may have already exited by the time we subscribe above -- Exited
                // does not fire retroactively for a transition that already happened, so check
                // directly rather than risk silently missing the refresh.
                if (ctwpmProcess.HasExited)
                    RefreshAfterCtwpmExit(prgID, prgDate);
            }
            catch (Exception ex)
            {
                SetPairingActionButtonsBusy(false);
                RadMessageBox.Show("Failed to launch CTWPM: " + ex.Message);
            }
        }

        private void LaunchCtwpm(string function, bool refreshOnExit = false, bool selectInactive = false)
        {
            if (prg.PrgHdr != null)
                LaunchCtwpm(function, prg.PrgHdr.PrgID, prg.PrgHdr.PrgDate, refreshOnExit, selectInactive);
            else
                LaunchCtwpm(function, null, null, refreshOnExit, selectInactive);
        }

        // Used directly by the Min Day Exceptions grid's Modify button, which acts on
        // whichever pairing is in that row -- not necessarily the one loaded in prg.
        private void LaunchCtwpm(string function, string prgID, string prgDate, bool refreshOnExit = false, bool selectInactive = false)
        {
            try
            {
                string ctExeDir = SFIConfig.AppSetting("CTEXEDIR");
                if (prgID != null)
                {
                    Process ctwpmProcess = CtwpmSelectionAutomator.Launch(ctExeDir, function, prgID, prgDate, selectInactive);
                    if (refreshOnExit)
                    {
                        ctwpmProcess.EnableRaisingEvents = true;
                        ctwpmProcess.Exited += (s, args) => RefreshAfterCtwpmExit(prgID, prgDate);
                    }
                }
                else
                    CtwpmSelectionAutomator.Launch(ctExeDir);
            }
            catch (Exception ex)
            {
                RadMessageBox.Show("Failed to launch CTWPM: " + ex.Message);
            }
        }

        // Fires on a threadpool thread when CTWPM exits -- marshal back before touching
        // any UI. Only refreshes if the pairing currently on screen is still the one that
        // was sent to CTWPM; if the user looked up something else in the meantime, leave
        // it alone rather than clobbering what they're now looking at.
        private void RefreshAfterCtwpmExit(string prgID, string prgDate)
        {
            if (IsDisposed || !IsHandleCreated)
                return;
            BeginInvoke((Action)(() =>
            {
                if (IsDisposed)
                    return;
                if (prg.PrgHdr == null || prg.PrgHdr.PrgID != prgID || prg.PrgHdr.PrgDate != prgDate)
                    return;

                try
                {
                    prg.Assemble(prgID, prgDate);
                    MinDayMarkerStatus minDayMarkerStatus = MinDayNameResolver.Resolve(prg.PrgHdr.UpdateidUpdempno);
                    DisplayHeader(minDayMarkerStatus);
                    PopulateGrid(minDayMarkerStatus);
                }
                catch (Exception ex)
                {
                    RadMessageBox.Show("Error refreshing pairing: " + ex.Message);
                }

                // Matches btnEvaluatePairing_Click's disable -- only re-enabled here (not
                // unconditionally) so a stale exit event for a pairing the user has since
                // navigated away from can't re-enable the buttons out from under a still-
                // in-progress Evaluate on whatever's now on screen; btnLookUp_Click already
                // resets these independently for whatever pairing gets looked up next. Runs
                // after the Assemble above (not before) so it reflects whatever marker CTWPM's
                // Evaluate may have just left behind, not the stale pre-exit one.
                SetPairingActionButtonsBusy(false);
            }));
        }

        // CTW.exe force-closes apps it launches directly (WM_CLOSE) when it shuts down,
        // but also calls CTWSECUR.DLL's ResetSecurity() on shutdown, which clears the
        // shared cross-process security state every process's loaded copy of the DLL
        // reads from. Since CTW.exe doesn't launch PairingInspect2, it never gets the
        // WM_CLOSE -- polling CTSecurity.UserID() is how we notice the second signal instead.
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

        // Saves resized/reordered column layout, plus last-used field values, back into user
        // settings on close.
        private void RadForm1_FormClosing(object sender, FormClosingEventArgs e)
        {
            securitySessionTimer.Stop();

            using (MemoryStream ms = new MemoryStream())
            {
                grid.SaveLayout(ms);
                Properties.Settings.Default.PairingGridLayout = Convert.ToBase64String(ms.ToArray());
            }
            Properties.Settings.Default.PairingGridLayoutFingerprint = GridColumnFingerprint();

            using (MemoryStream ms = new MemoryStream())
            {
                exceptionsGrid.SaveLayout(ms);
                Properties.Settings.Default.ExceptionsGridLayout = Convert.ToBase64String(ms.ToArray());
            }
            Properties.Settings.Default.ExceptionsGridLayoutFingerprint = ExceptionsGridColumnFingerprint();

            Properties.Settings.Default.LastPairingID = txtPairingID.Text;
            Properties.Settings.Default.LastPairingDate = NullableDateToInternal(txtPairingDate.NullableValue);
            Properties.Settings.Default.LastExceptionsFromDate = NullableDateToInternal(txtExceptionsFromDate.NullableValue);
            Properties.Settings.Default.LastExceptionsToDate = NullableDateToInternal(txtExceptionsToDate.NullableValue);
            Properties.Settings.Default.Save();
        }

        // Created on first use rather than in the constructor -- setup (bid periods,
        // config/marker loading) is non-trivial and not every session ends up
        // recalculating a min day.
        //
        // No longer version-checks MinDayProcess.dll here (used to compare
        // Assembly.GetAssembly(typeof(MinDayProcess)).GetName().Version against a hardcoded
        // minimum) -- that guarded against a stale standalone MinDayProcess.dll left behind
        // at a site that updated PairingInspect2.exe but not the DLL. The shipped deployment
        // now merges MinDayProcess.dll directly into PrgInsp.exe (ILRepack, 0PairingInspect2Merge.bat
        // -- required so CTPairing/PairingDuty are the same CLR type across the
        // ComputeLayoverCreditForDisplay call, not two distinct merged copies), so
        // typeof(MinDayProcess) now resolves to PrgInsp.exe's own assembly version (1.0.0.0,
        // never bumped) instead of MinDayProcess's -- the check always failed after that
        // change, and the scenario it guarded against (a stale separate file) can't happen
        // anymore since there's no longer a separate file to go stale.
        private MinDayProcess GetMinDayProcess()
        {
            if (minDayProcess == null)
            {
                CheckStandaloneMinDayProcessVersion();
                minDayProcess = new MinDayProcess((int)CTSecurity.UserID());
                minDayProcess.StatusUpdate += MinDayProcess_StatusUpdate;
                minDayProcess.PairingProcess += MinDayProcess_PairingProcess;
            }
            return minDayProcess;
        }

        // Written by 0PairingInspect2Merge.bat at merge time -- it records MinDayProcess.dll's
        // version exactly as it stood right before ILRepack folded its types into this exe
        // (that's the only moment the version is still readable; afterward the merged-in
        // assembly's own manifest no longer exists). Not a hand-maintained constant, so it
        // can't go stale from someone forgetting to bump it.
        private const string MergedVersionFileName = "MinDayProcess.MergedVersion.txt";

        // Sanity check for the free-standing MinDayProcess.dll that PSAMinDay.exe loads
        // externally (this exe has its own copy merged in via ILRepack -- see note above
        // GetMinDayProcess). A deploy that replaces only one of the two leaves PSAMinDay.exe
        // and PrgInsp.exe silently running different MinDay logic. AssemblyName.GetAssemblyName
        // just reads the file's manifest, so it won't load/clash with the merged-in copy.
        private void CheckStandaloneMinDayProcessVersion()
            {
            try
                {
                string exeDir = Path.GetDirectoryName(Application.ExecutablePath);
                string dllPath = Path.Combine(exeDir, "MinDayProcess.dll");
                string mergedVersionPath = Path.Combine(exeDir, MergedVersionFileName);

                // Missing sidecar file (e.g. an older deploy from before this check existed) --
                // nothing to compare against, so skip rather than false-alarm.
                if (!File.Exists(dllPath) || !File.Exists(mergedVersionPath))
                    return;

                Version actual = AssemblyName.GetAssemblyName(dllPath).Version;
                Version expected = new Version(File.ReadAllText(mergedVersionPath).Trim());
                if (actual != expected)
                    {
                    minDayVersionMismatch = true;
                    btnRecalculateMinDay.Enabled = false;
                    RadMessageBox.Show(
                        "Standalone MinDayProcess.dll (" + actual + ") does not match the version merged into this PrgInsp.exe at pack time (" + expected + ")." +
                        Environment.NewLine + "PSAMinDay.exe and PrgInsp.exe may be running different MinDay logic." +
                        Environment.NewLine + "Recalculate Min Day has been disabled until this is redeployed.",
                        "MinDayProcess version mismatch");
                    }
                }
            catch (Exception ex)
                {
                RadMessageBox.Show("Could not check standalone MinDayProcess.dll version: " + ex.Message, "MinDayProcess version check failed");
                }
            }

        // Set by CheckStandaloneMinDayProcessVersion -- true means btnRecalculateMinDay was
        // disabled due to a detected version mismatch. ProcessCmdKey below is the only way
        // to re-enable it without redeploying, for diagnosing the mismatch itself.
        private bool minDayVersionMismatch = false;

        // Deliberately not wired to any visible menu/button/tooltip -- a version mismatch
        // means PSAMinDay.exe and PrgInsp.exe may be running different MinDay logic, so the
        // button stays disabled for ordinary use. This fires the same recalc directly (not
        // just re-enabling the button) so the disabled state is never left on for the rest
        // of the session -- each use is a deliberate, one-off diagnostic action.
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
            {
            if (minDayVersionMismatch && keyData == (Keys.Control | Keys.Alt | Keys.Shift | Keys.R))
                {
                btnRecalculateMinDay_Click(this, EventArgs.Empty);
                return true;
                }
            return base.ProcessCmdKey(ref msg, keyData);
            }

        private void MinDayProcess_StatusUpdate(object sender, MinDayStatusEventArgs args)
            {
            minDayCriticalMessages.Add("[" + args.Status + "] " + args.Message);
            }

        private void MinDayProcess_PairingProcess(object sender, PairingProcessInfoEventArgs args)
            {
            string modifiedDuties = (args.ModifiedDutiesList != null && args.ModifiedDutiesList.Count > 0)
                ? string.Join(",", args.ModifiedDutiesList)
                : "none";
            minDayCriticalMessages.Add("[Process] " + args.PairingID + " " + args.PairingDate +
                " Bypassed=" + args.Bypassed + " ModifiedDuties=" + modifiedDuties);
            }

        private void btnRecalculateMinDay_Click(object sender, EventArgs e)
            {
            if (prg.PrgHdr == null)
                return;

            minDayCriticalMessages.Clear();
            SetPairingActionButtonsBusy(true);
            btnRecalculateMinDay.Refresh(); // ProcessSinglePairing below is synchronous -- force the disabled state to paint before it blocks the UI thread
            btnEvaluatePairing.Refresh();
            btnLaunchCTWPM.Refresh();
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
            finally
                {
                // Set unconditionally here too (not just on the try block's success path)
                // so a pairing that threw mid-recalculate doesn't leave the buttons stuck
                // disabled.
                SetPairingActionButtonsBusy(false);
                }
            }

        // RadTextBox/RadForm rather than RadMessageBox -- the trace can run to many lines
        // and the user needs to select/copy it, which RadMessageBox doesn't support well.
        private void ShowMinDayTrace(string trace)
        {
            ShowSelectableMessage("Min Day Recalculation Trace", trace);
        }

        // Same selectable-text need as ShowMinDayTrace, generalized -- e.g. an exception
        // message or a drag-drop diagnostic the user needs to copy/paste elsewhere, not
        // just read, which RadMessageBox's plain label text doesn't support.
        private void ShowSelectableMessage(string title, string text)
        {
            using (RadForm dlg = new RadForm())
            {
                dlg.ThemeName = RadMessageBox.ThemeName;
                dlg.Text = title;
                dlg.Width = 700;
                dlg.Height = 500;
                dlg.StartPosition = FormStartPosition.CenterParent;

                RadTextBox txt = new RadTextBox();
                txt.ThemeName = RadMessageBox.ThemeName;
                txt.Multiline = true;
                txt.ReadOnly = true;
                txt.Dock = DockStyle.Fill;
                txt.Font = new Font(FontFamily.GenericMonospace, 9);
                txt.Text = text;

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

        private void btnLookUp_Click(object sender, EventArgs e)
        {
            // A real lookup always supersedes a JSON-loaded proposed pairing, success or
            // not -- regardless of outcome, whatever's left on screen afterward is no
            // longer the proposed one, so the action buttons' normal gating applies again,
            // and everything DisplayProposedPairing hides needs to come back.
            viewingProposedPairing = false;
            lblCrewTypeTitle.Visible = true;
            lblCrewTypeValue.Visible = true;
            lblMinDayStatusTitle.Visible = true;
            lblMinDayStatusValue.Visible = true;
            btnRecalculateMinDay.Visible = true;
            btnEvaluatePairing.Visible = true;
            btnLaunchCTWPM.Visible = true;

            try
            {
                string internalDate = NullableDateToInternal(txtPairingDate.NullableValue);
                int result = prg.Assemble(txtPairingID.Text.Trim(), internalDate);
                if (result == 0)
                {
                    // CTPairing.Assemble only ever sets PrgHdr inside GetPairingHeader,
                    // which never runs on a failed lookup -- without this, PrgHdr (and
                    // everything gated on it, like the Canceled-based button disabling)
                    // would silently keep showing whatever pairing was loaded before.
                    prg.PrgHdr = null;

                    btnRecalculateMinDay.Enabled = false;
                    btnEvaluatePairing.Enabled = false;
                    // Inquire/Modify stay usable -- LaunchCtwpm already falls back to
                    // CtwpmSelectionAutomator's no-args overload when PrgHdr is null,
                    // opening CTWPM blank for the user to fill in themselves.
                    itemModify.Enabled = true;
                    btnLaunchCTWPM.Enabled = true;

                    crewGrid.DataSource = null;
                    grid.DataSource = null;
                    lblStatusValue.Text = "";
                    lblStatusValue.ForeColor = SystemColors.ControlText;
                    lblCrewTypeValue.Text = "";
                    lblMinDayStatusValue.Text = "";

                    RadMessageBox.Show(this, "Pairing not found: " + txtPairingID.Text + " " + txtPairingDate.Text);
                    return;
                }

                crewOnPairingLookup.FillActiveCrewOnPairing(prg.PrgHdr.PrgID, prg.PrgHdr.PrgDate);
                // crewOnPairingLookup.List (and openPositionsLookup.List below) are the same
                // object references every call (cleared and refilled in place, not reallocated)
                // -- rebinding an identical reference as DataSource is a no-op to the grid, so it
                // never picks up the new pairing's data. Copying into a fresh list each time
                // forces the rebind.
                crewOnPairing = new List<CrewMemOnPairing>(crewOnPairingLookup.List);

                // FillActiveCrewOnPairing only ever sees mp.status='A' rows -- a cancelled
                // pairing has none (cancelling flips every one of its crew to 'I'), so this
                // is always empty for one. Fall back to every MP row the pairing has ever
                // had and keep only the ones whose Updateid exactly matches the pairing's own
                // PM.Updateid -- those are the crew who were actually on it at the moment it
                // was cancelled (both get stamped from the same hold_upddate/hold_updtime in
                // the native cancel call, confirmed in CT2410.cpp). Everything else in that
                // list is unrelated prior history (an earlier, ordinary drop/reassignment)
                // and is deliberately left out, not just hidden.
                bool isCanceledCrewFallback = false;
                if (crewOnPairing.Count == 0 && prg.PrgHdr.Canceled)
                    {
                    crewOnPairingLookup.FillAllCrewOnPairing(prg.PrgHdr.PrgID, prg.PrgHdr.PrgDate);
                    crewOnPairing = crewOnPairingLookup.List.Where(c =>
                        c.UpdateidUpdempno == prg.PrgHdr.UpdateidUpdempno &&
                        c.UpdateidUpddate == prg.PrgHdr.UpdateidUpddate &&
                        c.UpdateidUpdtime == prg.PrgHdr.UpdateidUpdtime).ToList();
                    isCanceledCrewFallback = true;
                    }

                openPositionsLookup.FillByPairing(prg.PrgHdr.PrgID, prg.PrgHdr.PrgDate);

                List<CrewGridRow> crewGridRows = new List<CrewGridRow>();
                foreach (CrewMemOnPairing c in crewOnPairing)
                    crewGridRows.Add(new CrewGridRow
                    {
                        PairingPos = c.PairingPos,
                        EmpNum = c.EmpNum,
                        LastFirstName = c.LastFirstName,
                        AssignCode = c.AssignCode,
                        PairingLineType = c.PairingLineType,
                        IsOpen = false,
                        IsCanceledCrew = isCanceledCrewFallback
                    });
                foreach (OTPM o in openPositionsLookup.List)
                    for (int i = 0; i < o.OpenCount; i++)
                        crewGridRows.Add(new CrewGridRow { PairingPos = o.Pos, EmpNum = 0, LastFirstName = "OPEN", IsOpen = true });

                crewGrid.DataSource = crewGridRows;

                MinDayMarkerStatus minDayMarkerStatus = MinDayNameResolver.Resolve(prg.PrgHdr.UpdateidUpdempno);
                DisplayHeader(minDayMarkerStatus);
                PopulateGrid(minDayMarkerStatus);
                AddToRecentList(txtPairingID.Text.Trim(), internalDate, prg.PrgHdr.Canceled ? Color.Red : (Color?)null);

                // A cancelled pairing has nothing left to recalculate or evaluate --
                // Recalculate also stays gated on minDayVersionMismatch from
                // CheckStandaloneMinDayProcessVersion, which Evaluate (a native CTWPM
                // action, unrelated to MinDayProcess.dll) doesn't need -- and on the
                // marker status itself (see RecalculateBlockedByMarkerStatus).
                btnRecalculateMinDay.Enabled = !prg.PrgHdr.Canceled && !minDayVersionMismatch && !RecalculateBlockedByMarkerStatus();
                btnEvaluatePairing.Enabled = !prg.PrgHdr.Canceled;
                itemModify.Enabled = !prg.PrgHdr.Canceled;
                // Not touched above like the other two -- restore it explicitly in case
                // a JSON-loaded proposed pairing (DisplayProposedPairing) left it disabled.
                btnLaunchCTWPM.Enabled = true;
            }
            catch (Exception ex)
            {
                RadMessageBox.Show("Error looking up pairing: " + ex.Message);
            }
        }

        private void DisplayHeader(MinDayMarkerStatus minDayMarkerStatus)
        {
            PairingHeader hdr = prg.PrgHdr;

            lblStatusValue.Text = hdr.Canceled ? "Canceled" : "Active";
            lblStatusValue.ForeColor = hdr.Canceled ? Color.DarkRed : SystemColors.ControlText;
            lblCrewTypeValue.Text = CrewTypeDisplay(hdr.CrewType);
            // Min Day status doesn't apply to a cancelled pairing -- whatever marker
            // happens to be stored is leftover from before cancellation, not current.
            lblMinDayStatusValue.Text = hdr.Canceled ? "-" : MinDayNameResolver.DisplayName(minDayMarkerStatus) + " (empno=" + hdr.UpdateidUpdempno + ")";
            // Red when the marker isn't one of ours -- MinDay has either never considered
            // this pairing, or a real touch since (a dispatcher edit, CrewPost syncing
            // actuals) cleared whatever MinDay marker it had. Totals' LO delta is already
            // gated on Applied specifically and won't show in this state; CO Credit and
            // Trip Rig are real stored values either way, just not guaranteed current.
            lblMinDayStatusValue.ForeColor = (minDayMarkerStatus == MinDayMarkerStatus.Unknown) ? Color.DarkRed : SystemColors.ControlText;
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

        private static string FormatReleaseTime(DateTimeWithGMTVar report, DateTimeWithGMTVar release)
        {
            string time = release.AsDisplayTime;
            if (report.AsMSDate.HasValue && release.AsMSDate.HasValue &&
                release.AsMSDate.Value.Date == report.AsMSDate.Value.Date.AddDays(1))
                return time + "+1";
            return time;
        }

        private static string FormatMMDD(string internalDate)
        {
            if (string.IsNullOrEmpty(internalDate) || internalDate.Length != 8)
                return internalDate;
            return internalDate.Substring(4, 2) + "/" + internalDate.Substring(6, 2);
        }

        // The date pickers hold a DateTime? (null when empty); everywhere else the date is
        // stored as CT's internal YYYYMMDD string (settings, recent list, Assemble()). These
        // two convert between the two -- always convert at the boundary rather than letting
        // either representation leak into the wrong place.
        private static string NullableDateToInternal(DateTime? date)
        {
            return date.HasValue ? CTDateFormat.CTInternal(date.Value) : "";
        }

        private static DateTime? InternalDateToNullable(string internalDate)
        {
            if (string.IsNullOrEmpty(internalDate) || internalDate.Length != 8)
                return null;
            return CTDateFormat.AsDateTime(internalDate);
        }

        private static string PairingDateToDisplay(string internalDate)
        {
            if (string.IsNullOrEmpty(internalDate) || internalDate.Length != 8)
                return internalDate;
            return internalDate.Substring(4, 2) + "/" + internalDate.Substring(6, 2) + "/" + internalDate.Substring(2, 2);
        }

        // Lets an external app -- or, for now, a quick manual test by dragging plain text
        // out of Notepad -- drop a pairing onto the Inspect tab and have it look up/display
        // automatically. Wired onto every control that visibly covers the tab (the tab page
        // itself plus both grids) since WinForms drag events fire on whichever control is
        // directly under the cursor at drop time, not bubbled up from a child to its parent.
        private const string PairingRefDragFormat = "PSAMinDayCalc.PairingRef";

        private void SetupPairingDragDrop()
        {
            RegisterPairingDropTarget(tabPageInspect);
            RegisterPairingDropTarget(pnlGrid);
            RegisterPairingDropTarget(pnlCrewGrid);
            RegisterPairingDropTarget(grid);
            RegisterPairingDropTarget(crewGrid);
        }

        private void RegisterPairingDropTarget(Control c)
        {
            c.AllowDrop = true;
            c.DragEnter += PairingDropTarget_DragEnter;
            c.DragDrop += PairingDropTarget_DragDrop;
        }

        private void PairingDropTarget_DragEnter(object sender, DragEventArgs e)
        {
            e.Effect = (e.Data.GetDataPresent(PairingRefDragFormat) || e.Data.GetDataPresent(DataFormats.Text) || GetDroppedJsonFile(e.Data) != null)
                ? DragDropEffects.Copy : DragDropEffects.None;
        }

        // Dragging a single .json pairing-load file onto the tab is the other way (besides
        // a command-line arg from a file-association double-click) to load a proposed
        // pairing -- see LoadProposedPairingFromJsonFile. A multi-file drop isn't this;
        // fall through to the text-payload path below, which will just fail to parse it.
        private static string GetDroppedJsonFile(IDataObject data)
        {
            if (!data.GetDataPresent(DataFormats.FileDrop))
                return null;
            string[] files = (string[])data.GetData(DataFormats.FileDrop);
            return (files != null && files.Length == 1 && IsJsonPairingFile(files[0])) ? files[0] : null;
        }

        private static bool IsJsonPairingFile(string path)
        {
            return !string.IsNullOrEmpty(path) && path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) && File.Exists(path);
        }

        private void PairingDropTarget_DragDrop(object sender, DragEventArgs e)
        {
            // OLE drag-drop calls in from COM (the source process's DoDragDrop), and an
            // exception thrown from inside a .NET event handler invoked that way can get
            // lost at the COM boundary before it ever reaches Application.ThreadException/
            // AppDomain.UnhandledException -- wrap the whole thing so nothing goes silent.
            try
            {
                string jsonFile = GetDroppedJsonFile(e.Data);
                if (jsonFile != null)
                {
                    LoadProposedPairingFromJsonFile(jsonFile);
                    return;
                }

                string payload = e.Data.GetDataPresent(PairingRefDragFormat)
                    ? (string)e.Data.GetData(PairingRefDragFormat)
                    : e.Data.GetDataPresent(DataFormats.Text) ? (string)e.Data.GetData(DataFormats.Text) : null;

                string prgID;
                DateTime? prgDate;
                if (!TryParsePairingRef(payload, out prgID, out prgDate))
                {
                    ShowSelectableMessage("Drag/Drop Diagnostic",
                        "Couldn't find a pairing ID and date in what was dropped." + Environment.NewLine + Environment.NewLine +
                        "Raw payload: \"" + (payload ?? "<null>") + "\"");
                    return;
                }

                LookUpParsedPairing(prgID, prgDate.Value);
            }
            catch (Exception ex)
            {
                ShowSelectableMessage("Drag/Drop Diagnostic", "Drop handler threw: " + ex);
            }
        }

        // Shared final step for every "something external handed us a pairing" path --
        // drag-drop, a startup command-line arg, and an already-running WM_COPYDATA
        // request all converge here once they've got a parsed PrgID/date; only how they
        // obtain that pair and react to a parse failure differs.
        private void LookUpParsedPairing(string prgID, DateTime prgDate)
        {
            txtPairingID.Text = prgID;
            txtPairingDate.NullableValue = prgDate;
            btnLookUp_Click(this, EventArgs.Empty);
        }

        // Lets CrewRec (or anything else) hand a pairing to an ALREADY-RUNNING PrgInsp
        // instance -- the command-line path above only covers a fresh launch. CrewRec
        // finds this window via Process.GetProcessesByName("PrgInsp")[0].MainWindowHandle
        // (hence: never rename the output exe away from "PrgInsp") and posts WM_COPYDATA
        // with dwData = PairingRefCopyDataId and lpData = a UTF-16 "PrgID YYYYMMDD"
        // string, the same shape TryParsePairingRef already accepts everywhere else.
        private const int WM_COPYDATA = 0x004A;
        private static readonly IntPtr PairingRefCopyDataId = (IntPtr)0x50524749; // "PRGI"

        [StructLayout(LayoutKind.Sequential)]
        private struct COPYDATASTRUCT
        {
            public IntPtr dwData;
            public int cbData;
            public IntPtr lpData;
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_COPYDATA)
            {
                COPYDATASTRUCT cds = (COPYDATASTRUCT)Marshal.PtrToStructure(m.LParam, typeof(COPYDATASTRUCT));
                if (cds.dwData == PairingRefCopyDataId)
                {
                    // Any other dwData is ignored entirely -- not ours, don't touch it,
                    // don't even acknowledge it (fall through to base.WndProc below).
                    HandleIncomingPairingRequest(cds);
                    m.Result = (IntPtr)1; // standard WM_COPYDATA ack: nonzero = handled
                    return;
                }
            }
            base.WndProc(ref m);
        }

        private void HandleIncomingPairingRequest(COPYDATASTRUCT cds)
        {
            try
            {
                // cbData is a byte count; a UTF-16 string is 2 bytes/char. Reading by
                // this exact length (rather than relying on PtrToStringUni's own
                // null-terminator scan) works whether or not the sender null-terminated
                // lpData.
                string payload = cds.lpData == IntPtr.Zero || cds.cbData <= 0
                    ? null
                    : Marshal.PtrToStringUni(cds.lpData, cds.cbData / 2).TrimEnd('\0');

                string prgID;
                DateTime? prgDate;
                if (!TryParsePairingRef(payload, out prgID, out prgDate))
                    return; // silently ignore a malformed request -- same spirit as bad/missing cmdline args

                if (WindowState == FormWindowState.Minimized)
                    WindowState = FormWindowState.Normal;
                // Plain Activate() can get lost to Windows' foreground-lock heuristics
                // even for a window activating itself from its own message loop -- the
                // TopMost toggle is the standard, reliable WinForms way to force it.
                TopMost = true;
                TopMost = false;
                Activate();

                tabMain.SelectedPage = tabPageInspect;
                LookUpParsedPairing(prgID, prgDate.Value);
            }
            catch
            {
                // An external IPC request must never be able to take the app down --
                // same reasoning as Program.cs's crash-log handlers, just silent here
                // (no UI to show it in, and a bad request from CrewRec shouldn't pop a
                // dialog over whatever the user's doing).
            }
        }

        // Accepts the same "<PrgID> <YYYYMMDD>" shape AddToRecentList/cboRecentPairings
        // already use internally -- a real drag source (e.g. CrewRec, once built) can send
        // exactly that under PairingRefDragFormat -- or a human-typed date (MM/dd/yy,
        // MM/dd/yyyy, etc.) after the pairing ID, for manual testing by dragging plain text
        // out of Notepad/Notepad++.
        private static bool TryParsePairingRef(string text, out string prgID, out DateTime? prgDate)
        {
            prgID = null;
            prgDate = null;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            string trimmed = text.Trim();
            int spaceIdx = trimmed.IndexOf(' ');
            if (spaceIdx <= 0)
                return false;

            prgID = trimmed.Substring(0, spaceIdx).Trim();
            string dateToken = trimmed.Substring(spaceIdx + 1).Trim();

            // Only try the internal YYYYMMDD path for an actual 8-digit numeric token --
            // InternalDateToNullable's own guard only checks Length != 8, and "10/01/26"
            // is ALSO 8 characters long (the slashes count), so it slipped past that guard
            // and got misread as if "10/0" were a year -- confirmed live via the exact
            // ArgumentOutOfRangeException this produced in CTDateFormat.AsDateTime.
            if (dateToken.Length == 8 && dateToken.All(char.IsDigit))
            {
                prgDate = InternalDateToNullable(dateToken);
                if (prgDate.HasValue)
                    return true;
            }

            // Deliberately not DateTime.TryParse -- its two-digit-year expansion goes
            // through the current culture's Calendar.TwoDigitYearMax, which rolled "26"
            // into the wrong century on live testing instead of 2026. This app's dates are
            // always 20xx (see CTDateFormat/PairingDateToDisplay's own MM/DD/YY convention),
            // so a 2-digit year is resolved to 2000+yy explicitly, never left to .NET/the
            // OS's own ambient year-window setting.
            string[] mdyParts = dateToken.Split('/');
            if (mdyParts.Length == 3)
            {
                int month, day, year;
                if (int.TryParse(mdyParts[0], out month) && int.TryParse(mdyParts[1], out day) && int.TryParse(mdyParts[2], out year))
                {
                    if (year < 100)
                        year += 2000;
                    try
                    {
                        prgDate = new DateTime(year, month, day);
                        return true;
                    }
                    catch (ArgumentOutOfRangeException) { }
                }
            }
            // No year given ("MM/DD") -- infer one relative to today (see InferYearForMonth).
            else if (mdyParts.Length == 2)
            {
                int month, day;
                if (int.TryParse(mdyParts[0], out month) && int.TryParse(mdyParts[1], out day))
                {
                    try
                    {
                        prgDate = new DateTime(InferYearForMonth(month), month, day);
                        return true;
                    }
                    catch (ArgumentOutOfRangeException) { }
                }
            }

            prgID = null;
            return false;
        }

        // No year given -- infer the most plausible one relative to today's date. Defaults
        // to the current year, except right at a year boundary: if today is Nov/Dec and the
        // given month is Jan/Feb, that's clearly meant as next year (dropping "01/15" in
        // December means next January, not this one); the mirror image (today is Jan/Feb,
        // given month is Nov/Dec) means last year's Nov/Dec instead.
        private static int InferYearForMonth(int month)
        {
            int currentYear = DateTime.Now.Year;
            int currentMonth = DateTime.Now.Month;

            if ((currentMonth == 11 || currentMonth == 12) && (month == 1 || month == 2))
                return currentYear + 1;
            if ((currentMonth == 1 || currentMonth == 2) && (month == 11 || month == 12))
                return currentYear - 1;
            return currentYear;
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
                            DeptTime = airLeg.LatestDept.AsDisplayTime,
                            ArrvTime = airLeg.LatestArrv.AsDisplayTime,
                            DeptTimeIsActual = airLeg.LatestDept.SEA == SEAType.Act,
                            ArrvTimeIsActual = airLeg.LatestArrv.SEA == SEAType.Act,
                            IsCanceledLeg = airLeg.FlightStatus == "I",
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
                            DeptTime = nfLeg.LatestDept.AsDisplayTime,
                            ArrvTime = nfLeg.LatestArrv.AsDisplayTime,
                            DeptTimeIsActual = nfLeg.LatestDept.SEA == SEAType.Act,
                            ArrvTimeIsActual = nfLeg.LatestArrv.SEA == SEAType.Act,
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
                            DeptTime = oaLeg.LatestDept.AsDisplayTime,
                            ArrvTime = oaLeg.LatestArrv.AsDisplayTime,
                            DeptTimeIsActual = oaLeg.LatestDept.SEA == SEAType.Act,
                            ArrvTimeIsActual = oaLeg.LatestArrv.SEA == SEAType.Act,
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

            // Trip Rig reads a real, currently-stored PM field -- true right now
            // regardless of marker (it's whatever the last write, MinDay or native
            // CrewPost Evaluate, actually left behind), so it's shown unconditionally.
            //
            // Layover Credit: ComputeLayoverCreditForDisplay now gates itself on
            // Updateid_Updempno == MINDAY_APPLIED_USER internally (it's never
            // written/stored anywhere until a real MinDay apply), so this call is
            // unconditional -- the method is trusted to return 0 on its own when
            // unapplied, rather than every caller having to remember to gate it.
            int layoverCredit = GetMinDayProcess().ComputeLayoverCreditForDisplay(prg, dutiesByPeriod);
            int creditDelta = tripRig + layoverCredit;

            var creditDeltaBreakdown = new List<KeyValuePair<string, int>>();
            var noteLabels = new List<string>();
            if (layoverCredit > 0)
                {
                creditDeltaBreakdown.Add(new KeyValuePair<string, int>("LO", layoverCredit));
                noteLabels.Add("LO");
                }
            if (tripRig > 0)
                {
                creditDeltaBreakdown.Add(new KeyValuePair<string, int>("Rig", tripRig));
                noteLabels.Add("Rig");
                }

            rows.Add(new InspectRow
            {
                RowKind = "Totals",
                ArrvTime = "Totals:",
                Credit = totalCredit + creditDelta,
                CreditDeltaAmount = creditDelta,
                CreditDeltaBreakdown = creditDeltaBreakdown,
                Note = string.Join("/", noteLabels)
            });

            // Double line (two stacked divider rows, same mechanism as the single
            // DividerBeforeDuty/DividerAfterDuty bars between duties) to set TAFB
            // visually apart as a distinct reference figure below Totals, not just
            // another duty-level row.
            rows.Add(new InspectRow { RowKind = "DividerAfterTotals" });
            rows.Add(new InspectRow { RowKind = "DividerAfterTotals" });

            // Lets the Rig delta above be eyeballed by hand: TAFB/4 (mentally) against
            // Totals. Act, not Sked -- same field UpdateDutyCreditsAndPay's own TAFBRig
            // formula actually uses (TAFBTotalCredit = round(ActTAFB/4)), so this is the
            // real number the Rig figure is derived from, not an approximation of it.
            rows.Add(new InspectRow
            {
                RowKind = "TAFB",
                ArrvTime = "TAFB:",
                Credit = prg.PrgHdr.ActTAFB
            });

            int coCredit = prg.PrgHdr.ActCreditCO;
            if (coCredit != 0)
                rows.Add(new InspectRow
                {
                    RowKind = "COCredit",
                    ArrvTime = "CO Credit:",
                    Credit = coCredit
                });

            grid.DataSource = rows;
        }

        // Shows a pairing straight from a CrewRec/solver pairing-load JSON file (see
        // D:\Data\VS\CrewRec\customer-docs\pairing-load-schema.json) -- a proposed trip
        // that has no CrewTrac record yet, so none of the real-pairing machinery
        // (CTPairing.Assemble, MinDayDiffCalculator, CO/Trip Rig fields) applies to it.
        // Reachable only by double-clicking a .json file (command-line arg, via Windows'
        // file-type association) or dragging one onto the Inspect tab -- never by typing
        // anything into the lookup fields, which are for real, DB-backed pairings only.
        private void LoadProposedPairingFromJsonFile(string path)
        {
            try
            {
                string json = File.ReadAllText(path);
                PairingLoadRoot root = new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<PairingLoadRoot>(json);
                if (root == null || root.pairings == null || root.pairings.Count == 0)
                {
                    ShowSelectableMessage("Pairing Load JSON", "No pairings found in \"" + path + "\".");
                    return;
                }

                DisplayProposedPairing(root.pairings[0]);

                // Only the Inspect tab's one pairing slot exists -- showing the first and
                // saying so beats silently dropping the rest or guessing which one the
                // user meant.
                if (root.pairings.Count > 1)
                    RadMessageBox.Show(this, "\"" + path + "\" contains " + root.pairings.Count + " pairings -- showing the first one only.");
            }
            catch (Exception ex)
            {
                ShowSelectableMessage("Pairing Load JSON", "Couldn't load \"" + path + "\":" + Environment.NewLine + Environment.NewLine + ex);
            }
        }

        private void DisplayProposedPairing(PairingLoadPairing p)
        {
            viewingProposedPairing = true;

            txtPairingID.Text = ProposedPairingIdDisplay(p.header.pairing_id);
            txtPairingDate.NullableValue = InternalDateToNullable(p.header.pairing_id.pairing_date);
            // Deliberately NOT added to Recent Pairings -- there's no real pairing number
            // to look up yet, and re-selecting it later would just fail.

            lblStatusValue.Text = "Proposed";
            lblStatusValue.ForeColor = Color.Blue;

            // Crew Type and Min Day Status don't apply to a pairing with no CrewTrac
            // record yet -- hidden rather than shown with a placeholder, same reasoning
            // as the 3 action buttons below.
            lblCrewTypeTitle.Visible = false;
            lblCrewTypeValue.Visible = false;
            lblMinDayStatusTitle.Visible = false;
            lblMinDayStatusValue.Visible = false;
            LayoutHeaderLabels();

            // Hidden, not just disabled -- all three act on a real pairing number this
            // one doesn't have yet, so showing them (even greyed) implies a capability
            // that doesn't exist here at all. Enabled is also kept false alongside
            // Visible so viewingProposedPairing's own gating (SetPairingActionButtonsBusy)
            // stays consistent with what's on screen.
            btnRecalculateMinDay.Visible = false;
            btnRecalculateMinDay.Enabled = false;
            btnEvaluatePairing.Visible = false;
            btnEvaluatePairing.Enabled = false;
            btnLaunchCTWPM.Visible = false;
            btnLaunchCTWPM.Enabled = false;

            // The schema only carries position codes/counts, never named crew -- every
            // seat shows as open, reusing the same OPEN-row convention a real pairing's
            // unfilled positions already use (see btnLookUp_Click's openPositionsLookup).
            List<CrewGridRow> crewGridRows = new List<CrewGridRow>();
            if (p.header.position != null)
                foreach (PairingLoadPosition pos in p.header.position)
                    for (int i = 0; i < pos.count; i++)
                        crewGridRows.Add(new CrewGridRow { PairingPos = pos.code, EmpNum = 0, LastFirstName = "OPEN", IsOpen = true });
            crewGrid.DataSource = crewGridRows;

            grid.DataSource = BuildProposedPairingRows(p);
        }

        // oneOf pairing_number OR pairing_base+pairing_eqpt per the schema -- whichever
        // was given is the best identifier available before a real number is assigned.
        private static string ProposedPairingIdDisplay(PairingLoadPairingId id)
        {
            return !string.IsNullOrEmpty(id.pairing_number) ? id.pairing_number : (id.pairing_base ?? "") + (id.pairing_eqpt ?? "");
        }

        private List<InspectRow> BuildProposedPairingRows(PairingLoadPairing p)
        {
            var rows = new List<InspectRow>();
            int totalCredit = 0;

            foreach (PairingLoadDuty duty in p.dutyPeriods)
            {
                if (duty.duty_num == 0)
                    continue; // short ground-time marker, not a real duty -- nothing to show

                int dutyCredit = 0;
                foreach (PairingLoadLeg leg in duty.legs)
                {
                    InspectRow row = new InspectRow
                    {
                        DutyPeriod = duty.duty_num.ToString(),
                        RowKind = "Leg",
                        Date = FormatMMDD(leg.dept_city_date),
                        DeptCity = leg.dept_city,
                        ArrvCity = leg.arrv_city,
                        DeptTime = FormatHHMMTime(leg.dept_city_time),
                        ArrvTime = FormatHHMMTime(leg.arrv_city_time)
                    };

                    switch (leg.type)
                    {
                        case 2: // operated flight leg
                            row.FlightNum = leg.flight_num;
                            row.Credit = ParseMinutes(leg.credit);
                            break;
                        case 4: // deadhead, own metal or OAD
                            row.FlightNum = leg.flight_num;
                            row.OA = leg.oad;
                            row.Deadhead = leg.deadhead_ind;
                            // A deadhead's credit is optional in the schema -- when
                            // omitted, the real ingest derives it from GMT-adjusted
                            // elapsed time, which this preview doesn't replicate.
                            row.Credit = ParseMinutes(leg.credit);
                            break;
                        case 5: // non-flying activity
                            row.FlightNum = leg.transaction_code;
                            break;
                    }

                    dutyCredit += row.Credit;
                    rows.Add(row);
                }

                rows.Add(new InspectRow { RowKind = "DividerBeforeDuty" });

                rows.Add(new InspectRow
                {
                    RowKind = "Duty",
                    Credit = dutyCredit,
                    Report = FormatHHMMTime(duty.start_time),
                    Release = FormatHHMMTime(duty.end_time) + (duty.end_date != duty.start_date ? "+1" : ""),
                    DutyTime = FormatHHMM(MinutesBetween(duty.start_date, duty.start_time, duty.end_date, duty.end_time))
                });

                totalCredit += dutyCredit;
                rows.Add(new InspectRow { RowKind = "DividerAfterDuty" });
            }

            rows.Add(new InspectRow { RowKind = "Totals", ArrvTime = "Totals:", Credit = totalCredit });
            rows.Add(new InspectRow { RowKind = "DividerAfterTotals" });
            rows.Add(new InspectRow { RowKind = "DividerAfterTotals" });

            PairingLoadHeader hdr = p.header;
            rows.Add(new InspectRow
            {
                RowKind = "TAFB",
                ArrvTime = "TAFB:",
                Credit = MinutesBetween(hdr.report_date, hdr.report_time, hdr.skdend_date, hdr.skdend_time)
            });

            return rows;
        }

        private static int ParseMinutes(string durationMinutes)
        {
            int minutes;
            return !string.IsNullOrEmpty(durationMinutes) && int.TryParse(durationMinutes, out minutes) ? minutes : 0;
        }

        // JSON times are "HHMM" (schema's timeHHMM) -- AsDisplayTime elsewhere in this
        // form renders the same "HH:mm" shape, so this mirrors it for the JSON path.
        private static string FormatHHMMTime(string hhmm)
        {
            return !string.IsNullOrEmpty(hhmm) && hhmm.Length == 4 ? hhmm.Substring(0, 2) + ":" + hhmm.Substring(2, 2) : hhmm;
        }

        private static int MinutesBetween(string fromDate, string fromTime, string toDate, string toTime)
        {
            DateTime from, to;
            if (!DateTime.TryParseExact(fromDate + fromTime, "yyyyMMddHHmm", null, System.Globalization.DateTimeStyles.None, out from) ||
                !DateTime.TryParseExact(toDate + toTime, "yyyyMMddHHmm", null, System.Globalization.DateTimeStyles.None, out to))
                return 0;
            return (int)(to - from).TotalMinutes;
        }

        private void SetupCrewGrid()
        {
            crewGrid = new RadGridView();
            crewGrid.ThemeName = "ControlDefault";
            crewGrid.Dock = DockStyle.Fill;
            crewGrid.ShowGroupPanel = false;
            crewGrid.EnableSorting = false;
            crewGrid.MasterTemplate.AutoGenerateColumns = false;
            crewGrid.MasterTemplate.AllowAddNewRow = false;
            crewGrid.MasterTemplate.AllowDeleteRow = false;
            crewGrid.MasterTemplate.ShowRowHeaderColumn = false; // hides the pin/select/delete indicator column -- not appropriate for this read-only grid
            crewGrid.AllowColumnReorder = false;
            crewGrid.ReadOnly = true; // editing a cell was eating the double-click meant to launch CTWMS
            crewGrid.Columns.Add(new GridViewTextBoxColumn("PairingPos", "PairingPos") { HeaderText = "Position", TextAlignment = ContentAlignment.MiddleCenter, Width = 74 });
            crewGrid.Columns.Add(new GridViewTextBoxColumn("EmpNum", "EmpNum") { HeaderText = "Emp #", TextAlignment = ContentAlignment.MiddleCenter, Width = 76 });
            crewGrid.Columns.Add(new GridViewTextBoxColumn("LastFirstName", "LastFirstName") { HeaderText = "Name", TextAlignment = ContentAlignment.MiddleCenter, Width = 130 });
            crewGrid.Columns.Add(new GridViewTextBoxColumn("AssignCode", "AssignCode") { HeaderText = "Assign Code", TextAlignment = ContentAlignment.MiddleCenter, Width = 91 });
            crewGrid.Columns.Add(new GridViewTextBoxColumn("PairingLineType", "PairingLineType") { HeaderText = "Line Type", TextAlignment = ContentAlignment.MiddleCenter, Width = 53 }); // 20px narrower than its resized width, leaving just enough room for the vertical scrollbar without triggering a horizontal one
            crewGrid.CellDoubleClick += CrewGrid_CellDoubleClick;
            crewGrid.ViewCellFormatting += CrewGrid_ViewCellFormatting;

            pnlCrewGrid.Controls.Add(crewGrid);
        }

        // Flags Emp #/Name in red for crew pulled from btnLookUp_Click's cancelled-pairing
        // fallback (IsCanceledCrew) -- same visual language as the Inspect grid's
        // canceled-leg highlighting, so it's clear at a glance this isn't a normal,
        // currently-active crew assignment.
        private void CrewGrid_ViewCellFormatting(object sender, CellFormattingEventArgs e)
        {
            if (e.Row == null || !(e.Row.DataBoundItem is CrewGridRow) || e.Column == null)
                return;

            CrewGridRow row = (CrewGridRow)e.Row.DataBoundItem;
            bool isEmpOrNameColumn = e.Column.Name == "EmpNum" || e.Column.Name == "LastFirstName";
            if (isEmpOrNameColumn && row.IsCanceledCrew)
                e.CellElement.ForeColor = Color.Red;
            else
                e.CellElement.ResetValue(LightVisualElement.ForeColorProperty, ValueResetFlags.Local);
        }

        // Same double-click-to-launch-CTWMS behavior as the exceptions grid's crew
        // columns (ExceptionsGrid_CellClick) -- here every row is one crew member
        // already (no Crew1/Crew2/Crew3 column ambiguity), and every row belongs to
        // the one pairing currently loaded in the Inspect tab, so any column fires it.
        private void CrewGrid_CellDoubleClick(object sender, GridViewCellEventArgs e)
            {
            CrewGridRow row = e.Row.DataBoundItem as CrewGridRow;
            if (row == null || row.IsOpen || row.EmpNum == 0 || prg.PrgHdr == null)
                return;

            LaunchCtwms(row.EmpNum, prg.PrgHdr.PrgDate);
            }

        // Display-only merge of assigned crew (CrewMemOnPairing, from MP) and open positions
        // (OTPM, from OT) into one shape crewGrid can bind to -- keeps both data-access types
        // clean rather than teaching CrewMemOnPairing about positions that have no crew member.
        public class CrewGridRow
        {
            public string PairingPos { get; set; }
            public uint EmpNum { get; set; }
            public string LastFirstName { get; set; }
            public string AssignCode { get; set; }
            public string PairingLineType { get; set; }
            public bool IsOpen { get; set; }
            // Set only for the cancelled-pairing fallback crew (btnLookUp_Click's
            // FillAllCrewOnPairing path) -- this is crew pulled from an inactive ('I')
            // MP record, not a normal active assignment, so it's flagged red rather than
            // shown identically to a real, currently-active crew member.
            public bool IsCanceledCrew { get; set; }
        }

        private void SetupGrid()
        {
            grid = new RadGridView();
            grid.ThemeName = "ControlDefault";
            grid.Dock = DockStyle.Fill;
            grid.ShowGroupPanel = false;
            // Rows are hand-ordered (legs under their duty, divider rows, a trailing totals
            // row) to mirror the reference layout -- column-click sorting would scramble that
            // and is never appropriate here.
            grid.EnableSorting = false;
            grid.MasterTemplate.AutoGenerateColumns = false;
            grid.MasterTemplate.AllowAddNewRow = false;
            grid.MasterTemplate.ShowRowHeaderColumn = false; // hides the pin/select/delete indicator column -- not appropriate for this read-only grid
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
            grid.Columns.Add(new GridViewTextBoxColumn("DutyTime", "DutyTime") { HeaderText = "Duty Time", TextAlignment = ContentAlignment.MiddleCenter, MinWidth = 110 });
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
            grid.PrintCellFormatting += grid_PrintCellFormatting;

            // A layout saved before a column was added/renamed/reordered will silently
            // override the current setup (header text, order, visibility) when loaded --
            // and a plain "does the file mention this column name" check doesn't catch
            // renames or reorders, only missing columns. Fingerprint name+header (in
            // definition order) straight from what was just built above, so this never
            // needs manual upkeep when columns change. GridSchemaVersion covers
            // structural changes the per-column fingerprint can't see, like switching
            // to a grouped-column ViewDefinition.
            string currentFingerprint = GridColumnFingerprint();
            if (!string.IsNullOrEmpty(Properties.Settings.Default.PairingGridLayout) &&
                Properties.Settings.Default.PairingGridLayoutFingerprint == currentFingerprint)
            {
                byte[] layoutBytes = Convert.FromBase64String(Properties.Settings.Default.PairingGridLayout);
                using (MemoryStream ms = new MemoryStream(layoutBytes))
                    grid.LoadLayout(ms);
            }

            pnlGrid.Controls.Add(grid);
        }

        private void SetupExceptionsGrid()
        {
            exceptionsGrid = new RadGridView();
            exceptionsGrid.ThemeName = "ControlDefault";
            exceptionsGrid.Dock = DockStyle.Fill;
            exceptionsGrid.ShowGroupPanel = false;
            exceptionsGrid.MasterTemplate.AutoGenerateColumns = false;
            exceptionsGrid.MasterTemplate.AllowAddNewRow = false;
            exceptionsGrid.ReadOnly = true; // editing a cell was eating the click meant to launch Modify
            exceptionsGrid.MasterTemplate.ShowRowHeaderColumn = false; // hides the pin/select/delete indicator column -- not appropriate for this read-only grid
            // Explicit starting Width on every column -- without one, RadGridView
            // auto-computes width from content/header and stretches whichever column
            // ends up last to absorb any leftover space so there's no gap at the grid's
            // right edge, and that auto-stretch recalculates on every layout pass,
            // overriding any manual drag-resize on that column.
            exceptionsGrid.Columns.Add(new GridViewTextBoxColumn("PairingID", "PairingID") { HeaderText = "Pairing", TextAlignment = ContentAlignment.MiddleCenter, Width = 90 });
            exceptionsGrid.Columns.Add(new GridViewTextBoxColumn("PairingDate", "PairingDate") { HeaderText = "Pairing Date", TextAlignment = ContentAlignment.MiddleCenter, Width = 100 });
            exceptionsGrid.Columns.Add(new GridViewTextBoxColumn("EndDate", "EndDate") { HeaderText = "End Date", TextAlignment = ContentAlignment.MiddleCenter, Width = 100 });
            exceptionsGrid.Columns.Add(new GridViewTextBoxColumn("Crew1", "Crew1") { HeaderText = "", TextAlignment = ContentAlignment.MiddleLeft, Width = 150 });
            exceptionsGrid.Columns.Add(new GridViewTextBoxColumn("Crew2", "Crew2") { HeaderText = "", TextAlignment = ContentAlignment.MiddleLeft, Width = 150 });
            exceptionsGrid.Columns.Add(new GridViewTextBoxColumn("Crew3", "Crew3") { HeaderText = "", TextAlignment = ContentAlignment.MiddleLeft, Width = 150 });
            exceptionsGrid.CellDoubleClick += ExceptionsGrid_CellClick;
            exceptionsGrid.ViewCellFormatting += ExceptionsGrid_ViewCellFormatting;

            SetupExceptionsPairingMenu();
            exceptionsGrid.MouseUp += ExceptionsGrid_MouseUp;

            // ColumnGroupsViewDefinition only shows columns explicitly listed in it --
            // every column needs a home, including ones that aren't part of any visible
            // group heading (same pattern as the main grid's "leadingColumns" group).
            // Crew1-3 go under one "Crew" header spanning both header rows; the 2nd row
            // is blank since the 3 slots aren't individually labeled.
            ColumnGroupsViewDefinition columnGroupsView = new ColumnGroupsViewDefinition();

            GridViewColumnGroup leadingColumns = new GridViewColumnGroup("");
            leadingColumns.ShowHeader = false;
            leadingColumns.Rows.Add(new GridViewColumnGroupRow());
            foreach (string name in new string[] { "PairingID", "PairingDate", "EndDate" })
                leadingColumns.Rows[0].ColumnNames.Add(name);
            columnGroupsView.ColumnGroups.Add(leadingColumns);

            GridViewColumnGroup crewGroup = new GridViewColumnGroup("Crew");
            crewGroup.Rows.Add(new GridViewColumnGroupRow());
            crewGroup.Rows[0].ColumnNames.Add("Crew1");
            crewGroup.Rows[0].ColumnNames.Add("Crew2");
            crewGroup.Rows[0].ColumnNames.Add("Crew3");
            columnGroupsView.ColumnGroups.Add(crewGroup);

            exceptionsGrid.ViewDefinition = columnGroupsView;

            string currentFingerprint = ExceptionsGridColumnFingerprint();
            if (!string.IsNullOrEmpty(Properties.Settings.Default.ExceptionsGridLayout) &&
                Properties.Settings.Default.ExceptionsGridLayoutFingerprint == currentFingerprint)
            {
                byte[] layoutBytes = Convert.FromBase64String(Properties.Settings.Default.ExceptionsGridLayout);
                using (MemoryStream ms = new MemoryStream(layoutBytes))
                    exceptionsGrid.LoadLayout(ms);
            }

            pnlExceptionsGrid.Controls.Add(exceptionsGrid);
        }

        public class PXExceptionRow
        {
            public string PairingID { get; set; }
            public string PairingDate { get; set; }
            // Internal YYYYMMDD form of PairingDate -- CtwpmSelectionAutomator.Launch needs
            // this format, not the MM/DD/YY the grid displays.
            public string PairingDateRaw { get; set; }
            public string EndDate { get; set; }
            public string Crew1 { get; set; }
            public string Crew2 { get; set; }
            public string Crew3 { get; set; }
            // Raw empno behind each Crew slot -- kept separate from the formatted
            // "empno pos name" display string so double-clicking a crew cell doesn't
            // need to parse it back out. 0 when that slot has no crew member.
            public uint CrewEmpNum1 { get; set; }
            public uint CrewEmpNum2 { get; set; }
            public uint CrewEmpNum3 { get; set; }
            // Drives the RAS/TTA dark-red highlight in ExceptionsGrid_ViewCellFormatting --
            // kept separate from the displayed "empno pos name" string for the same reason
            // as CrewEmpNumN above.
            public string CrewAssignCode1 { get; set; }
            public string CrewAssignCode2 { get; set; }
            public string CrewAssignCode3 { get; set; }
        }

        // Hardcoded fallback matches AH.Posmask_0..3's live order (CA, FO, FA, CF) --
        // only used if the AH read in LoadCrewPosOrder fails, so the grid's crew
        // ordering degrades gracefully rather than throwing.
        private static readonly string[] DefaultCrewPosOrder = { "CA", "FO", "FA", "CF" };
        private string[] crewPosOrder = DefaultCrewPosOrder;

        // AH is a single-row airline-config table; Posmask_0..3 give the crew position
        // display order (CA, FO, FA, CF). CTAirlineHeader's own Fill() doesn't select
        // these columns (only Code/Name -- see FillByNameAndCode's command text), so
        // read them directly here rather than changing that shared CTDataAccess class.
        // Called once from the constructor.
        private void LoadCrewPosOrder()
            {
            try
                {
                using (OleDbCommand cmd = prg.Connection.CreateCommand())
                    {
                    cmd.CommandText = "SELECT Posmask_0, Posmask_1, Posmask_2, Posmask_3 FROM AH";
                    using (OleDbDataReader reader = cmd.ExecuteReader())
                        {
                        if (reader.Read())
                            crewPosOrder = new[]
                                {
                                reader["Posmask_0"].ToString().Trim(),
                                reader["Posmask_1"].ToString().Trim(),
                                reader["Posmask_2"].ToString().Trim(),
                                reader["Posmask_3"].ToString().Trim()
                                };
                        }
                    }
                }
            catch (Exception)
                {
                crewPosOrder = DefaultCrewPosOrder;
                }
            }

        // Only 3 crew columns fit the exceptions grid, so take the first 3 in
        // crewPosOrder (CA, FO, FA, CF -- read from AH.Posmask_0..3, see
        // LoadCrewPosOrder) and fold anything past the 3rd into a "+N more" suffix on
        // the 3rd slot rather than silently dropping it -- the double-click-to-Modify
        // action still shows the real full crew list for a trip that needs closer
        // inspection. A position not found in crewPosOrder sorts last, after the known
        // positions, rather than erroring. crewOnPairingLookup.List is cleared and
        // refilled in place on every call (same object reference, not reallocated --
        // see the comment on its other caller above), so it must be read out into
        // row.CrewN immediately after this Fill call and before the next row's Fill
        // call overwrites it. Also must run after the PX reader above is fully
        // closed -- this shares prg.Connection's single OleDb connection
        // (GlobalCTDataSet.Instance.Connection), and that connection crashes the
        // native provider if a second reader is opened on it while one is still
        // active.
        private static string FormatCrewSlot(CrewMemOnPairing c)
        {
            return c.EmpNum + " " + c.PairingPos.Trim() + " " + c.LastFirstName;
        }

        private void PopulateCrewColumns(PXExceptionRow row)
        {
            crewOnPairingLookup.FillActiveCrewOnPairing(row.PairingID, row.PairingDateRaw);
            List<CrewMemOnPairing> crew = crewOnPairingLookup.List
                .OrderBy(c =>
                    {
                    int idx = Array.IndexOf(crewPosOrder, c.PairingPos.Trim());
                    return idx < 0 ? int.MaxValue : idx;
                    })
                .ToList();

            // Slot 3 never shows an actual crew member -- at 3 or more assigned,
            // it becomes a "+N more" summary instead (N = however many beyond the
            // first 2 shown), so it never carries a real empno/assign code for
            // click-to-launch purposes.
            row.Crew1 = crew.Count > 0 ? FormatCrewSlot(crew[0]) : "";
            row.Crew2 = crew.Count > 1 ? FormatCrewSlot(crew[1]) : "";
            row.Crew3 = crew.Count > 2 ? "+" + (crew.Count - 2) + " more" : "";

            row.CrewEmpNum1 = crew.Count > 0 ? crew[0].EmpNum : 0;
            row.CrewEmpNum2 = crew.Count > 1 ? crew[1].EmpNum : 0;
            row.CrewEmpNum3 = 0;

            row.CrewAssignCode1 = crew.Count > 0 ? crew[0].AssignCode : null;
            row.CrewAssignCode2 = crew.Count > 1 ? crew[1].AssignCode : null;
            row.CrewAssignCode3 = null;
        }

        // Keeps the range sane any time it might have gone bad -- a From past the
        // current To silently returned zero rows otherwise, with nothing pointing
        // at why. Called both when the user tabs/clicks away from From and right
        // before Find actually runs (e.g. clicking Find without ever leaving the
        // From field, or after it was changed programmatically).
        private void FixupExceptionsDateRange()
        {
            if (txtExceptionsFromDate.NullableValue.HasValue && txtExceptionsToDate.NullableValue.HasValue &&
                txtExceptionsFromDate.NullableValue.Value > txtExceptionsToDate.NullableValue.Value)
                txtExceptionsToDate.NullableValue = txtExceptionsFromDate.NullableValue;
        }

        private void txtExceptionsFromDate_Leave(object sender, EventArgs e)
        {
            FixupExceptionsDateRange();
        }

        private void btnFindExceptions_Click(object sender, EventArgs e)
        {
            FixupExceptionsDateRange();

            btnFindExceptions.Enabled = false;
            btnFindExceptions.Refresh(); // read below is synchronous, so force the disabled state to paint before it blocks the UI thread
            try
            {
                string fromDate = NullableDateToInternal(txtExceptionsFromDate.NullableValue);
                string toDate = NullableDateToInternal(txtExceptionsToDate.NullableValue);

                List<PXExceptionRow> rows = new List<PXExceptionRow>();
                using (OleDbCommand cmd = prg.Connection.CreateCommand())
                {
                    // PX has no Cancel field of its own -- a pairing's PX exception row
                    // survives cancellation (CTPairing cancel doesn't clean up PX), so this
                    // joins to PM to filter those out by the pairing's own Cancel flag.
                    cmd.CommandText = "SELECT px.Prgid_Prgno_Base+px.Prgid_Prgno_Eqpt+px.Prgid_Prgno_3+px.Prgid_Prgno_4_6 as PrgNo, " +
                        "px.Prgid_Prgdate, px.Enddate " +
                        "FROM PX " +
                        "INNER JOIN PM ON " +
                        "pm.Prgid_Prgno_Base = px.Prgid_Prgno_Base AND " +
                        "pm.Prgid_Prgno_Eqpt = px.Prgid_Prgno_Eqpt AND " +
                        "pm.Prgid_Prgno_3 = px.Prgid_Prgno_3 AND " +
                        "pm.Prgid_Prgno_4_6 = px.Prgid_Prgno_4_6 AND " +
                        "pm.Prgid_Prgdate = px.Prgid_Prgdate " +
                        // SeverityInd = 'H' means the native engine marked this PX row History
                        // (superseded by a later version of the same pairing, e.g. on a
                        // Build/Modify commit -- see Z:\Hugo\CT Source\Lib\Pairing\CT2290.cpp's
                        // db_updates_2710-style logic). Never deleted, just flagged -- exclude
                        // it here the same way Cancel <> 'C' excludes cancelled pairings.
                        "WHERE px.ErrorNo_0 = ? AND px.Prgid_Prgdate >= ? AND px.Prgid_Prgdate <= ? AND pm.Cancel <> 'C' AND px.SeverityInd <> 'H'";
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
                                EndDate = PairingDateToDisplay(reader["Enddate"].ToString())
                            });
                        }
                    }
                }

                foreach (PXExceptionRow row in rows)
                    PopulateCrewColumns(row);

                exceptionsGrid.DataSource = rows;
            }
            catch (Exception ex)
            {
                RadMessageBox.Show("Error finding exceptions: " + ex.Message);
            }
            finally
            {
                btnFindExceptions.Enabled = true;
            }
        }

        private void SetupExceptionsPairingMenu()
        {
            exceptionsPairingMenu = new ContextMenuStrip();

            ToolStripMenuItem itemInspect = new ToolStripMenuItem("Inspect");
            itemInspect.Click += itemExceptionsPairingInspect_Click;
            exceptionsPairingMenu.Items.Add(itemInspect);

            ToolStripMenuItem itemEvaluate = new ToolStripMenuItem("Evaluate");
            itemEvaluate.Click += itemExceptionsPairingEvaluate_Click;
            exceptionsPairingMenu.Items.Add(itemEvaluate);

            ToolStripMenuItem itemInquire = new ToolStripMenuItem("Inquire");
            itemInquire.Click += itemExceptionsPairingInquire_Click;
            exceptionsPairingMenu.Items.Add(itemInquire);

            ToolStripMenuItem itemModify = new ToolStripMenuItem("Modify");
            itemModify.Click += itemExceptionsPairingModify_Click;
            exceptionsPairingMenu.Items.Add(itemModify);
        }

        // Right-click on the PairingID column only -- GetElementAtPoint<GridDataCellElement>
        // is the documented way to hit-test a RadGridView cell at a screen point (RadElementTree,
        // not a dedicated RadGridView event -- CellClick/CellDoubleClick don't carry mouse-button
        // info, and there's no plain "right-click a cell" event on RadGridView itself).
        private void ExceptionsGrid_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
                return;

            GridDataCellElement cell = exceptionsGrid.ElementTree.GetElementAtPoint<GridDataCellElement>(e.Location);
            if (cell == null || cell.ColumnInfo == null || cell.ColumnInfo.Name != "PairingID")
                return;

            exceptionsContextRow = cell.RowInfo.DataBoundItem as PXExceptionRow;
            if (exceptionsContextRow == null)
                return;

            exceptionsPairingMenu.Show(exceptionsGrid, e.Location);
        }

        private void itemExceptionsPairingInspect_Click(object sender, EventArgs e)
        {
            if (exceptionsContextRow == null)
                return;
            OpenInPairingInspect(exceptionsContextRow.PairingID, exceptionsContextRow.PairingDateRaw);
        }

        // "Load it in the other screen and flip to that tab" -- fills the Inspect tab's own
        // pairing ID/date fields and runs its normal lookup, same as if the user had typed
        // them in and clicked Look Up themselves. Shared by the PairingID right-click menu's
        // "Pairing Inspect" item and a plain double-click on the PairingID cell.
        private void OpenInPairingInspect(string prgID, string prgDateRaw)
        {
            txtPairingID.Text = prgID;
            txtPairingDate.NullableValue = InternalDateToNullable(prgDateRaw);
            tabMain.SelectedPage = tabPageInspect;
            btnLookUp_Click(this, EventArgs.Empty);
        }

        private void itemExceptionsPairingEvaluate_Click(object sender, EventArgs e)
        {
            if (exceptionsContextRow == null)
                return;
            EvaluatePairing(exceptionsContextRow.PairingID, exceptionsContextRow.PairingDateRaw);
        }

        private void itemExceptionsPairingInquire_Click(object sender, EventArgs e)
        {
            if (exceptionsContextRow == null)
                return;
            LaunchCtwpm(CtwpmSelectionAutomator.FunctionInquire, exceptionsContextRow.PairingID, exceptionsContextRow.PairingDateRaw);
        }

        private void itemExceptionsPairingModify_Click(object sender, EventArgs e)
        {
            if (exceptionsContextRow == null)
                return;
            LaunchCtwpm(CtwpmSelectionAutomator.FunctionModify, exceptionsContextRow.PairingID, exceptionsContextRow.PairingDateRaw);
        }

        private void ExceptionsGrid_ViewCellFormatting(object sender, CellFormattingEventArgs e)
        {
            PXExceptionRow row = e.Row != null ? e.Row.DataBoundItem as PXExceptionRow : null;
            if (row == null || e.Column == null)
                return;

            string assignCode = null;
            if (e.Column.Name == "Crew1") assignCode = row.CrewAssignCode1;
            else if (e.Column.Name == "Crew2") assignCode = row.CrewAssignCode2;
            else if (e.Column.Name == "Crew3") assignCode = row.CrewAssignCode3;
            else
                return;

            bool isRasOrTta = assignCode == "RAS" || assignCode == "TTA";
            e.CellElement.ForeColor = isRasOrTta ? Color.DarkRed : SystemColors.ControlText;
        }

        private void ExceptionsGrid_CellClick(object sender, GridViewCellEventArgs e)
        {
            PXExceptionRow row = e.Row.DataBoundItem as PXExceptionRow;
            if (row == null)
                return;

            if (e.Column.Name == "PairingID")
                {
                OpenInPairingInspect(row.PairingID, row.PairingDateRaw);
                return;
                }

            uint empNum = 0;
            if (e.Column.Name == "Crew1") empNum = row.CrewEmpNum1;
            else if (e.Column.Name == "Crew2") empNum = row.CrewEmpNum2;
            else if (e.Column.Name == "Crew3") empNum = row.CrewEmpNum3;

            if (empNum != 0)
                LaunchCtwms(empNum, row.PairingDateRaw);
        }

        private void LaunchCtwms(uint empNum, string internalDate)
        {
            try
            {
                string ctExeDir = SFIConfig.AppSetting("CTEXEDIR");
                CtwmsSelectionAutomator.Launch(ctExeDir, empNum, internalDate);
            }
            catch (Exception ex)
            {
                RadMessageBox.Show("Failed to launch CTWMS: " + ex.Message);
            }
        }

        private const string ExceptionsGridSchemaVersion = "v2"; // v2: switched to ColumnGroupsViewDefinition (grouped Crew header)

        private string ExceptionsGridColumnFingerprint()
        {
            return ExceptionsGridSchemaVersion + "|" +
                string.Join("|", exceptionsGrid.Columns.Select(c => c.Name + "=" + c.FieldName + "=" + c.HeaderText + "=" + c.TextAlignment));
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

                if (row.RowKind == "DividerBeforeDuty" || row.RowKind == "DividerAfterDuty" || row.RowKind == "DividerAfterTotals")
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

                bool isDeptTimeColumn = e.Column != null && e.Column.Name == "DeptTime";
                bool isArrvTimeColumn = e.Column != null && e.Column.Name == "ArrvTime";

                if ((isCreditColumn && (row.RowKind == "Duty" || row.RowKind == "Totals" || row.RowKind == "COCredit" || row.RowKind == "TAFB")) ||
                    (isDutySummaryColumn && row.RowKind == "Duty") ||
                    (isArrvTimeColumn && (row.RowKind == "Totals" || row.RowKind == "COCredit" || row.RowKind == "TAFB")) ||
                    (isDeptTimeColumn && row.RowKind == "Leg" && row.DeptTimeIsActual) ||
                    (isArrvTimeColumn && row.RowKind == "Leg" && row.ArrvTimeIsActual))
                    e.CellElement.Font = new Font(e.CellElement.Font, FontStyle.Bold);
                else
                    e.CellElement.ResetValue(LightVisualElement.FontProperty, ValueResetFlags.Local);

                // Canceled (FH.Status == "I") flight legs -- flag the flight#/org/dst/
                // dept/arrv cells in red so a canceled flight within an otherwise-active
                // pairing stands out at a glance.
                bool isOrgDstDeptArrvColumn = e.Column != null &&
                    (e.Column.Name == "FlightNum" || e.Column.Name == "DeptCity" || e.Column.Name == "ArrvCity" || e.Column.Name == "DeptTime" || e.Column.Name == "ArrvTime");
                if (isOrgDstDeptArrvColumn && row.RowKind == "Leg" && row.IsCanceledLeg)
                    e.CellElement.ForeColor = Color.Red;
                else
                    e.CellElement.ResetValue(LightVisualElement.ForeColorProperty, ValueResetFlags.Local);

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
                if (row.RowKind == "DividerBeforeDuty" || row.RowKind == "DividerAfterDuty" || row.RowKind == "DividerAfterTotals")
                {
                    e.RowElement.RowInfo.MinHeight = 4;
                    e.RowElement.RowInfo.MaxHeight = 4;
                    e.RowElement.RowInfo.Height = 4;
                }
            }
        }

        // Telerik's print renderer is a separate pipeline from the on-screen grid --
        // it never raises ViewRowFormatting, so the 4px-tall divider rows above
        // rendered at full default row height when printed instead, showing up as
        // doubled/tripled solid lines (most visible after Totals, which deliberately
        // stacks two DividerAfterTotals rows for a heavier closing bar on screen).
        // PrintCellFormatting is the print-pipeline's own per-cell equivalent; it
        // still exposes a settable Row.Height, so the same collapse applies here.
        private void grid_PrintCellFormatting(object sender, Telerik.WinControls.UI.PrintCellFormattingEventArgs e)
        {
            if (e.Row.DataBoundItem is InspectRow)
            {
                InspectRow row = (InspectRow)e.Row.DataBoundItem;
                if (row.RowKind == "DividerBeforeDuty" || row.RowKind == "DividerAfterDuty" || row.RowKind == "DividerAfterTotals")
                {
                    e.Row.MinHeight = 4;
                    e.Row.MaxHeight = 4;
                    e.Row.Height = 4;
                }
            }
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

        private static string FormatHHMM(int totalMinutes)
        {
            return CTTimeFormat.MinsAsHHMM(totalMinutes).TrimStart();
        }

        // Same as FormatHHMM but without zero-padding the hours -- used for the
        // parenthesized min-day delta (e.g. "+1:11"), not the main total.
        private static string FormatHHMMDelta(int totalMinutes)
        {
            int hours = totalMinutes / 60;
            int mins = totalMinutes % 60;
            return string.Format("{0}:{1:D2}", hours, mins);
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
            public bool DeptTimeIsActual { get; set; }
            public bool ArrvTimeIsActual { get; set; }
            // Only ever set for AirlinePairingLeg rows (FlightStatus == "I", i.e. FH.Status
            // -- the flight's own real-world operational status, confirmed via
            // Z:\Hugo\CT Source\h\ctFILES.h's "FLIGHT LEG HEADER RECORD"). Non-flying and
            // other-airline-deadhead legs have no FH-backed status at all and never set
            // this -- it stays false (the type's own default) for them.
            public bool IsCanceledLeg { get; set; }
            public string OA { get; set; }
            public string Deadhead { get; set; }
            public int Credit { get; set; }
            // Parenthesized amount shown before the total: the min-day top-up on
            // Duty rows (single amount), or -- on the Totals row -- the combined sum
            // of layover bonus + trip rig, itemized via CreditDeltaBreakdown below.
            public int CreditDeltaAmount { get; set; }
            public string CreditHHMM { get { return FormatHHMM(Credit); } }

            // Totals-row only: each delta component labeled ("LO"/"Rig") with its own
            // minutes. Only used to label amounts individually when there are 2+
            // components to disambiguate -- with just one, the plain (+N) prefix is
            // enough and Note already names which component it is, so no redundant
            // label here. Null/empty on every other row kind, where CreditDeltaAmount's
            // single-prefix display always applies.
            public List<KeyValuePair<string, int>> CreditDeltaBreakdown { get; set; }

            // True for the thin spacer rows between/around duties -- these never
            // carry a real Credit value (it's just left at its int default, 0), so
            // CreditDisplay/CreditHHMMDisplay must not fall through to printing that
            // 0/00:00 -- confirmed live: that's exactly what was showing up as faint
            // extra lines under each duty's real credit when printed.
            private bool IsDividerRow
            {
                get { return RowKind == "DividerBeforeDuty" || RowKind == "DividerAfterDuty" || RowKind == "DividerAfterTotals"; }
            }

            public string CreditDisplay
            {
                get
                {
                    if (IsDividerRow)
                        return "";
                    if (CreditDeltaBreakdown != null && CreditDeltaBreakdown.Count > 1)
                        return string.Join("", CreditDeltaBreakdown.Select(kv => "(+" + kv.Value + " " + kv.Key + ")")) + " " + Credit;
                    if (CreditDeltaAmount > 0)
                        return "(+" + CreditDeltaAmount + ") " + Credit;
                    return Credit.ToString();
                }
            }

            public string CreditHHMMDisplay
            {
                get
                {
                    if (IsDividerRow)
                        return "";
                    if (CreditDeltaBreakdown != null && CreditDeltaBreakdown.Count > 1)
                        return string.Join("", CreditDeltaBreakdown.Select(kv => "(+" + FormatHHMMDelta(kv.Value) + " " + kv.Key + ")")) + " " + CreditHHMM;
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
    }
}
