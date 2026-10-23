# PairingInspect2 Porting Log

Why this exists: `PairingInspect.exe` (Telerik `2022.1.222.40`) throws `StackOverflowException`
on this machine because the installed Telerik distribution is `2023.3.1114.40`. Rather than fight
the version mismatch, we're rebuilding the UI from scratch as `PairingInspect2`, against the
correctly-installed newer Telerik version, porting `PairingInspectForm` piece by piece with a
build+visual-check after each step.

Overall stage plan (see "Status" column):

| Stage | What | Status |
|---|---|---|
| UI layout | Theme, icon, title, tabs, labels, buttons, dropdown, date pickers, text fields | **Done** |
| 0 | Base class decision + references (SFIConfigUtils, SFICTDateTimeUtils, SFICTDataAccess, MinDayProcess) + security gate | **Done** |
| 1 | Constructor data-layer bootstrap (CTAirlineHeader, CTPairing, CTCrewOnPairing, CTEmployees, CTOTs, MinDayNameResolver) | **Done** |
| 2 | The 3 grids (SetupGrid / SetupCrewGrid / SetupExceptionsGrid, columns, layout persistence) | **Done** |
| 3 | Lookup & display (`PopulateGrid`, `DisplayHeader`, formatting helpers, `btnLookUp_Click`) | **Done** |
| 4 | Min Day evaluation (`GetMinDayProcess`, event handlers, `btnRecalculateMinDay_Click`, `btnEvaluatePairing_Click`) | **Done** |
| 5 | CTWPM launch integration (dropdown menu, `LaunchCtwpm`, `RefreshAfterCtwpmExit`) | **Done** |
| 6 | Recent pairings & settings persistence | **Done** |
| 7 | Exceptions tab (`btnFindExceptions_Click`) | **Done** |
| 8 | Security session polling & FormClosing (grid layout save) | **Done** |
| 9 | Side-by-side validation against `PI` with real pairings | Not started — only remaining stage |

---

## Detailed checklist — complete and pending steps

### UI layout — complete
- [x] Theme setup (form + `tabMain` = `Office2010Black`, every other control = `ControlDefault`)
- [x] Icon + title (`"Pairing Inspect"`)
- [x] Tabbed UI: `tabPageInspect` ("Inspect"), `tabPageMinDayExceptions` ("Min Day Exceptions")
- [x] Inspect tab: labels, `cboRecentPairings`, `btnLookUp`, `txtPairingDate`, `txtPairingID`,
      `btnEvaluatePairing`, `btnRecalculateMinDay`, `btnLaunchCTWPM`, status/crew-type/min-day-status
      label pairs
- [x] Min Day Exceptions tab: `txtExceptionsFromDate`, `txtExceptionsToDate`, `btnFindExceptions`,
      labels
- [x] `pnlGrid`, `pnlCrewGrid` placeholder panels (Inspect tab)
- [x] `pnlExceptionsGrid` placeholder panel (Min Day Exceptions tab)

### Stage 0 — Base class & references — complete
- [x] Decision: keep `RadForm1 : RadForm`, not `CTAppNS.FormBase`
- [x] Reference `SFIConfigUtils`, `SFICTDateTimeUtils` (loose, matching `PI`'s HintPaths)
- [x] `ProjectReference` to `SFICTDataAccess.csproj` and `MinDayProcess.csproj`
- [x] `SFIConfigUtils.RunCheck.OkToRun()` gate added to `Program.cs`
- [x] Fixed `Platform=x86` → `Platform=AnyCPU` + nested `PlatformTarget=x86` (standalone build fix)

### Stage 1 — Data-layer bootstrap — complete
- [x] Fields: `prg` (`CTPairing`), `crewOnPairingLookup` (`CTCrewOnPairing`), `openPositionsLookup`
      (`CTOTs`), `minDayCriticalMessages` (`List<string>`)
- [x] `RadMessageBox.ThemeName` set from `Office2010BlackTheme`
- [x] `CTAirlineHeader().Fill()`
- [x] `CTPairing` construction + `MinDayNameResolver.LoadDisplayNames`
- [x] `CTCrewOnPairing` + `CTEmployees.FillAll()` + `GetEmpName` wiring
- [x] `CTOTs` construction
- [x] Copied `MinDayNameResolver.cs` into the project, namespace → `PairingInspect2`
- [x] Fixed the `SFIConfig`→`MinDayConfig` bug in both `PI`'s real source and this copy
- [x] Fixed the `.sln` platform-mapping regression so F5 debugging works again

### Stage 2 — The 3 grids — complete
- [x] Add `pnlExceptionsGrid` placeholder panel (Min Day Exceptions tab)
- [x] Port `SetupGrid()` — main pairing grid: columns, `ColumnGroupsView`, formatting, layout
      persistence (load/save via embedded layout bytes)
- [x] Port `SetupCrewGrid()` — crew-on-pairing grid: columns, formatting
- [x] Port `SetupExceptionsGrid()` — exceptions grid: columns, layout persistence
- [x] Port `grid_ViewCellFormatting` / `grid_ViewRowFormatting`
- [x] Port `GridColumnFingerprint` / `ExceptionsGridColumnFingerprint` / schema-version consts
- [x] Dock all three `RadGridView`s into their panels
- [x] Build + visually confirm grids render empty but correctly columned/styled
- [x] Added the 4 grid-layout `Settings.settings` entries (`PairingGridLayout` /
      `PairingGridLayoutFingerprint` / `ExceptionsGridLayout` / `ExceptionsGridLayoutFingerprint`) —
      needed for `SetupGrid`/`SetupExceptionsGrid` to compile; hand-wrote `Settings.Designer.cs`
      since the VS single-file generator isn't available from the command line
- Deliberately **not** ported: the "one-time migration from the old shared-network-file layout
  scheme" branches in `SetupGrid`/`SetupExceptionsGrid` (reading `PairingInspectGridLayout.xml`
  etc. from `Application.StartupPath`) — dead code for a brand-new app, no such file will ever exist
- Deliberately **not** wired yet: `exceptionsGrid.CommandCellClick += ExceptionsGrid_CommandCellClick`
  — that handler needs `LaunchCtwpm` (Stage 5) and `PXExceptionRow` (Stage 3/7), so the `Modify`
  column exists structurally but doesn't do anything yet

### Stage 3 — Lookup & display — complete
- [x] Port row classes: `InspectRow`, `CrewGridRow` (pulled forward in Stage 2 — needed by
      `grid_ViewCellFormatting`/`grid_ViewRowFormatting` and `SetupCrewGrid`'s columns)
- [ ] Port row class: `PXExceptionRow` — still deferred to Stage 7 (nothing needs it yet)
- [x] Port formatting helpers: `FormatHHMM`, `FormatHHMMDelta` (pulled forward — `InspectRow` needs
      them to compile)
- [x] Port remaining formatting helpers: `FormatMMDD`, `FormatMMDDFromDisplayDate`,
      `PairingDateToDisplay`, `NullableDateToInternal` / `InternalDateToNullable`,
      `FormatReleaseTime`, `LatestNonZeroCredit`, `CrewTypeDisplay`
- [x] Port `DisplayHeader(MinDayMarkerStatus)` / `LayoutHeaderLabels`
- [x] Port `PopulateGrid(MinDayMarkerStatus)`
- [x] Copied `MinDayDiffCalculator.cs` into the project (needed by `PopulateGrid`'s
      `MinDayDiffCalculator.CalculateForDuty` call — originally scoped to Stage 7, moved up since
      it's actually an Inspect-tab dependency, not an exceptions-tab one)
- [x] Port `btnLookUp_Click` (wired `btnLookUp.Click` in the Designer, matching `PI`'s convention)
- [x] Build + launch — grids are wired and ready for a live lookup test
- Deliberately **not** ported yet: the `AddToRecentList(...)` call at the end of `btnLookUp_Click`
  is commented out (Stage 6 — no `Settings`/recent-list plumbing exists yet)

### Stage 4 — Min Day evaluation — partially started
- [x] Port `GetMinDayProcess()` (+ `MinRequiredMinDayProcessVersion` check) — pulled forward since
      `PopulateGrid` calls `GetMinDayProcess().ComputeLayoverCreditForDisplay(...)`
- [x] Port `MinDayProcess_StatusUpdate` / `MinDayProcess_PairingProcess` event handlers (wired by
      `GetMinDayProcess()`)
- [x] Port `btnRecalculateMinDay_Click` (wired `btnRecalculateMinDay.Click` in the Designer)
- [x] Port `ShowMinDayTrace`
- [x] Port `btnEvaluatePairing_Click` (wired `btnEvaluatePairing.Click` in the Designer)
- [ ] Build + evaluate/recalculate a test pairing, compare status/trace to `PI`

### Stage 5 — CTWPM launch integration — complete
- [x] Port `ctwpmFunctionMenu` field + `SetupLaunchCtwpmButton()` (called from the constructor)
- [x] Port `btnLaunchCTWPM_DropDownOpening` / `btnLaunchCTWPM_Click` (wired `btnLaunchCTWPM.Click`
      in the Designer; `DropDownOpening` wired in code, matching `PI`)
- [x] Port `itemPrgInquire_Click` / `itemPrgModify_Click`
- [x] Port `LaunchCtwpm(string, bool)` / `LaunchCtwpm(string, string, string, bool)` overloads
- [x] Port `RefreshAfterCtwpmExit`
- [x] Copy `CtwpmSelectionAutomator.cs` into the project (pure Win32 interop, no Telerik/other
      dependency, safe to port verbatim)
- [ ] Build + launch each CTWPM menu function, confirm it opens and refreshes correctly — not
      live-tested yet, needs a real pairing + CTWPM environment

### Stage 6 — Recent pairings & settings persistence — complete
- [x] Port `recentPairingLookup` field + `MaxRecentPairings` const
- [x] Port `AddToRecentList` / `RefreshRecentComboBox` / `cboRecentPairings_SelectedIndexChanged`
      (wired `cboRecentPairings.SelectedIndexChanged` in the Designer, matching `PI` exactly --
      `RefreshRecentComboBox` also detaches/reattaches it around the `DataSource` rebind)
- [x] Added matching `Settings.settings` entries (`LastPairingID`, `LastPairingDate`,
      `RecentPairings`, `LastExceptionsFromDate`, `LastExceptionsToDate`) + hand-wrote their
      `Settings.Designer.cs` properties
- [x] Wired constructor's settings-restore lines + the pairing auto-lookup on startup
- [x] Uncommented `AddToRecentList(...)` in `btnLookUp_Click`; added the 4 `Last*` saves to
      `RadForm1_FormClosing`
- Deliberately **not** ported: the exceptions-tab auto-find on startup (`PI` calls
  `btnFindExceptions_Click(this, EventArgs.Empty)` if both exception dates are set) -- that
  method doesn't exist yet (Stage 7)
- [ ] Build + confirm a looked-up pairing lands in Recent, and last-used values restore on reopen
      -- not live-tested yet

### Stage 7 — Exceptions tab — complete
- [x] Port row class: `PXExceptionRow`
- [x] Port `btnFindExceptions_Click` (wired `btnFindExceptions.Click` in the Designer)
- [x] Port `ExceptionsGrid_CommandCellClick` (wired `exceptionsGrid.CommandCellClick` in
      `SetupExceptionsGrid`, now that `LaunchCtwpm` exists from Stage 5)
- [x] Port `FormatRaisedTime`
- [x] Added the constructor's auto-find-exceptions-on-startup call (matching `PI`), now that
      `btnFindExceptions_Click` exists
- [ ] Build + run a date-range exceptions search, compare results/grid against `PI` -- not
      live-tested yet

### Stage 8 — Security polling & FormClosing — complete
- [x] Port `RadForm1_FormClosing` (grid layout + `Last*` settings save on close) -- done ahead of
      schedule, between Stages 1 and 2, when asked directly about column-width persistence
- [x] Port `securitySessionTimer` field + `SetupSecuritySessionTimer()` / `SecuritySessionTimer_Tick`
      (called from the constructor; `RadForm1_FormClosing` now also stops it, matching `PI`)
- [ ] Confirm security-teardown detection behaves the same as `PI` live (harder to test directly --
      would need an actual CTW.exe logout while the app is running)

### Stage 9 — Side-by-side validation — pending
- [ ] Run `PI` and `PairingInspect2` against the same set of real pairings, including a
      carry-over pairing and a min-day-exception case
- [ ] Diff outputs; resolve any discrepancies
- [ ] Decide on retiring `PI` / switching delivery to `PairingInspect2` in the packaging batch script

---

## UI layout (done)

Ported `PairingInspectForm`'s Designer layout into `RadForm1` control-by-control, matching names,
types, positions, sizes, and text exactly. Key decisions along the way:

- **Theme**: discovered `PI`'s own controls never actually use the `Office2010Black` theme at all —
  only the form chrome and `tabMain` (RadPageView) carry it explicitly. Every other control was set
  to `ThemeName = "ControlDefault"` to match reality, and the app-wide
  `ThemeResolutionService.ApplicationThemeName = "Office2010Black"` line in `Program.cs` was removed
  (PI's own `Program.cs` never sets it either) because it was overriding the per-control settings at
  runtime even though the Designer looked right.
- Added the two grid placeholder panels (`pnlGrid`, `pnlCrewGrid`) at `PI`'s exact
  `Location`/`Size` — these are plain `System.Windows.Forms.Panel`s in `PI`'s own Designer too; the
  actual `RadGridView` instances are built in code-behind and docked in at runtime (Stage 2).
  `pnlExceptionsGrid` (on the Min Day Exceptions tab) is still outstanding.

## Stage 0 — Base class & references (done)

- **Decision: `RadForm1` stays `: RadForm` directly, NOT `CTAppNS.FormBase`.** `FormBase` (in
  `CTApp.csproj`) is still built against the *old* Telerik `2022.1.222.40` — inheriting it would
  reintroduce the same version-mismatch problem we're escaping. It also has leftover debug code
  (`OnShown` unconditionally does `MessageBox.Show("Base OnShown")`) that would pop a box on every
  launch. We've already hand-replicated the only two things `FormBase` gave `PI`: icon + theme.
- Added loose references `SFIConfigUtils` and `SFICTDateTimeUtils` (same HintPaths as `PI`'s own
  `.csproj`), plus `ProjectReference`s to `SFICTDataAccess.csproj` and `MinDayProcess.csproj` — all
  confirmed Telerik-free, safe to link directly.
- Added the `SFIConfigUtils.RunCheck.OkToRun()` security gate to `Program.cs`, matching `PI`.
- **Platform mismatch fix**: `PairingInspect2.csproj` originally had `Platform=x86` as its own
  top-level property (a VS-template default), but `MinDayProcess.csproj` (like the rest of this
  codebase) only defines `AnyCPU` configurations with `PlatformTarget=x86` nested inside. Building
  standalone with `Platform=x86` failed against that `ProjectReference`. Fixed by switching
  `PairingInspect2.csproj` to the same convention: `Platform=AnyCPU` + nested `PlatformTarget=x86`
  per config.

## Stage 1 — Data-layer bootstrap (done)

Ported the constructor's data setup: `CTAirlineHeader().Fill()`, `CTPairing`, `CTCrewOnPairing` +
employee name lookup, `CTOTs`, `MinDayNameResolver.LoadDisplayNames`, `RadMessageBox.ThemeName`
line. Copied `MinDayNameResolver.cs` into the project (self-contained, no Telerik dependency),
namespace adjusted to `PairingInspect2`.

**Two real bugs found and fixed along the way, not specific to this port:**

1. **False trail — not a real bug, but worth knowing**: running the exe *inline/synchronously*
   from within this automation shell (`.\PairingInspect2.exe` directly) produced a genuine
   WER-logged access-violation crash during `InitializeComponent()`. Launching the *identical*
   build normally via `Start-Process` (6/6 runs) never crashed. The inline-launch mode itself is
   flaky for this app — don't trust a crash seen that way without confirming via a normal launch.
2. **Real bug — latent, pre-existing in `PI` itself**: `MinDayNameResolver.GetMinDayUser` still
   called `SFIConfig.AppSettingRaw(...)` (reads `SFI.config`) instead of the newer
   `MinDayConfig.AppSettingRaw(...)` (reads the dedicated `MinDay.config`) that the MinDay.config
   migration earlier this session introduced. The migration removed the `MINDAY_*` keys from
   `SFI.config`, so this throws a bare `NullReferenceException` (the already-flagged
   `SFIConfig.AppSettingRaw` NRE wart) the moment `LoadDisplayNames` runs. `PI`'s *deployed* exe
   predates the migration so it hasn't broken yet — but rebuilding `PI` from current source today
   would hit this immediately. **Fixed in both places**: `PairingInspect\MinDayNameResolver.cs`
   (the real `PI` source) and the `PairingInspect2` copy, switched to `MinDayConfig.AppSettingRaw`.
   Logged in memory (`sficonfig-appsettingraw-nre-wart`).

## Stage 2 — The 3 grids (done)

Ported `SetupGrid()` (main pairing grid, grouped-column header view), `SetupCrewGrid()`
(crew-on-pairing grid), and `SetupExceptionsGrid()` (exceptions grid, including the structural
`Modify` command column) verbatim, plus `grid_ViewCellFormatting`/`grid_ViewRowFormatting` and both
fingerprint helpers. All three `RadGridView`s dock into their placeholder panels as in `PI`.

Two things pulled forward from later stages because the grid code couldn't compile without them:
`InspectRow`/`CrewGridRow` (row shape the grids bind to) and `FormatHHMM`/`FormatHHMMDelta`
(`InspectRow.CreditHHMM`/`CreditHHMMDisplay` call them). `PXExceptionRow` was **not** pulled
forward since nothing in Stage 2 needs it yet.

Two things deliberately left out, not oversights: the old shared-network-file layout-migration
branches (meaningless for a brand-new app with no legacy layout file), and wiring
`ExceptionsGrid_CommandCellClick` (needs `LaunchCtwpm`, which doesn't exist until Stage 5) — the
`Modify` column renders but doesn't do anything yet.

Added the 4 grid-layout `Settings.settings` entries needed for `SetupGrid`/`SetupExceptionsGrid`
to compile (`PairingGridLayout`, `PairingGridLayoutFingerprint`, `ExceptionsGridLayout`,
`ExceptionsGridLayoutFingerprint`) — hand-wrote `Settings.Designer.cs` to match since the VS
single-file generator isn't available from the command line here.

## Stage 3 — Lookup & display (done)

Ported `btnLookUp_Click`, `DisplayHeader`/`LayoutHeaderLabels`, `PopulateGrid`, and all the
formatting helpers it needs (`FormatMMDD`, `FormatMMDDFromDisplayDate`, `PairingDateToDisplay`,
`NullableDateToInternal`/`InternalDateToNullable`, `FormatReleaseTime`, `LatestNonZeroCredit`,
`CrewTypeDisplay`), verbatim from `PI`. Wired `btnLookUp.Click` in the Designer (`PI`'s own
convention) rather than in the constructor.

`PopulateGrid` turned out to need `GetMinDayProcess()` (for `ComputeLayoverCreditForDisplay`),
which is nominally Stage 4 scope — pulled that forward too, along with the two `MinDayProcess`
event handlers it wires up. Also copied `MinDayDiffCalculator.cs` into the project: it's actually
an Inspect-tab dependency (`PopulateGrid` calls `MinDayDiffCalculator.CalculateForDuty`), not an
exceptions-tab one as originally scoped in the Stage 7 checklist — moved it here instead.

Left `AddToRecentList(...)` commented out at the end of `btnLookUp_Click` since there's no
`Settings`/recent-list plumbing yet (Stage 6).

## Stage 4 (partial) — Min Day recalculation (done)

Ported `btnRecalculateMinDay_Click` and `ShowMinDayTrace` (the hand-rolled scrollable trace dialog,
not a `RadMessageBox`) verbatim, wired `btnRecalculateMinDay.Click` in the Designer. Also found and
fixed a stray `CalendarSize = new Size(290, 320)` override on `txtPairingDate` that never existed in
`PI`'s own Designer — removed it (and added the `NullText = ""` PI has on all 3 date pickers, which
was missing) while investigating a report that the new Telerik version's calendar popup looked much
bigger than `PI`'s. (A DPI-manifest theory was tested first and ruled out — reverted that change.)

## Stage 5 — CTWPM launch integration (done)

Copied `CtwpmSelectionAutomator.cs` in verbatim (pure Win32 interop via `user32.dll` P/Invoke —
no Telerik, no other dependency). Ported `SetupLaunchCtwpmButton`/the dropdown context-menu
pattern, both `LaunchCtwpm` overloads, `RefreshAfterCtwpmExit`, and finally `btnEvaluatePairing_Click`
itself (which had been blocked on this since Stage 4). Wired `btnLaunchCTWPM.Click` and
`btnEvaluatePairing.Click` in the Designer; `DropDownOpening` is wired in code inside
`SetupLaunchCtwpmButton`, matching `PI` exactly (it cancels Telerik's own dropdown popup so only
the plain `ContextMenuStrip` shows).

## Stage 6 — Recent pairings & settings persistence (done)

Added the 5 remaining `Settings.settings` entries (`LastPairingID`, `LastPairingDate`,
`RecentPairings`, `LastExceptionsFromDate`, `LastExceptionsToDate`) and hand-wrote their
`Settings.Designer.cs` properties, same as the Stage 2 grid-layout settings. Ported
`AddToRecentList`/`RefreshRecentComboBox`/`cboRecentPairings_SelectedIndexChanged` verbatim, wired
the combo's `SelectedIndexChanged` in the Designer (PI wires it there too, even though
`RefreshRecentComboBox` also detaches/reattaches it around each `DataSource` rebind -- that's
intentional, not redundant). Wired the constructor's settings-restore lines and the startup
auto-lookup, uncommented the `AddToRecentList` call in `btnLookUp_Click`, and added the 4 `Last*`
saves to `RadForm1_FormClosing`.

## Stage 7 — Exceptions tab (done)

Ported `PXExceptionRow`, `FormatRaisedTime`, `btnFindExceptions_Click`, and
`ExceptionsGrid_CommandCellClick` verbatim. The `Modify` column's `CommandCellClick` wiring (left
as a comment since Stage 2, pending `LaunchCtwpm`) is now live. Also added the constructor's
startup auto-find-exceptions call, matching `PI`, now that the method it needs exists.

With this, every one of `PI`'s button/grid event handlers has been ported.

## Stage 8 — Security polling (done)

Ported `SetupSecuritySessionTimer`/`SecuritySessionTimer_Tick` verbatim (polls
`CTSecurity.UserID()` every 5s, closes the form if the session goes invalid -- this is how
`PairingInspect2` notices a CTW.exe logout, since CTW.exe doesn't launch it directly and so never
sends it a `WM_CLOSE`). `RadForm1_FormClosing` (already ported earlier, ahead of schedule) now
also calls `securitySessionTimer.Stop()` first, matching `PI`.

**All 9 stages are now code-complete.** The only remaining work is Stage 9: an actual live,
side-by-side validation pass against `PI` with real pairings -- nothing left to port, only to test.

## Debugger fix (just done, outside the stage plan)

After Stage 0's platform change, **F5 debugging in Visual Studio broke** ("unable to start
debugging"). Root cause: `PSAMinDayCalc.sln`'s `GlobalSection(ProjectConfigurationPlatforms)` still
mapped `PairingInspect2`'s project GUID to `Debug|x86`/`Release|x86` regardless of solution
platform — but the `.csproj` no longer has those `PropertyGroup`s (only `Debug|AnyCPU` /
`Release|AnyCPU` now), so the solution-driven build produced nothing usable. Fixed by updating the
`.sln` mapping for that GUID to `Debug|Any CPU` / `Release|Any CPU`, matching how other projects
(e.g. `SFICTDataAccess`) are already mapped. Confirmed building via the solution
(`msbuild PSAMinDayCalc.sln /t:PairingInspect2`) now produces the exe at the expected path.
**If VS was already open with the old `.sln` cached, it may need the solution closed and reopened
to pick this up.**

## Open items / things to watch

Every button, grid, and settings path from `PairingInspectForm` has now been ported into
`RadForm1` — `btnLookUp`, `btnRecalculateMinDay`, `btnEvaluatePairing`, `btnLaunchCTWPM`'s dropdown
(Inquire/Modify), `btnFindExceptions`, `cboRecentPairings`, the exceptions grid's `Modify` command
column, the security session timer, and all settings persistence (grid layouts + last-used
fields + recent-pairings list). Nothing is stubbed or deferred anymore.

**What's left is purely Stage 9 — live validation, not more porting:**
- Run a real pairing lookup end-to-end and visually compare against `PI` (grid contents,
  header labels, formatting).
- Exercise `btnRecalculateMinDay`/`btnEvaluatePairing` against a test pairing and compare the
  trace/status output to `PI`.
- Launch each CTWPM dropdown function (Inquire/Modify) and the exceptions grid's Modify button,
  confirm `ctwpm.exe` automation still works identically.
- Run a date-range exceptions search and compare results to `PI`.
- Confirm recent-pairings and last-used-field persistence survives an app restart.
- Ideally test against a carry-over pairing and a min-day-exception case specifically, since
  those are the trickiest display paths.
- Decide on retiring `PI` / switching delivery to `PairingInspect2` in the packaging batch script
  once validation passes.
