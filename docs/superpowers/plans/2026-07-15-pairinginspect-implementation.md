# PairingInspect Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A new interactive WinForms tool, `PairingInspect`, that looks up a
pairing by ID+date and displays its full detail (header, duty periods, legs,
min-day flagging) using `CTPairing`'s existing read API.

**Architecture:** New WinForms project scaffolded like `PSAMinDay`
(references/theming) with `PSAMinDayCalcViewer`'s simple non-tray entry
point. Two small additive changes to `SFICTDataAccess` (expose
`Updateid_Updempno` on `PairingHeader`); everything else is new, read-only
code in the new project. `RadGridView` columns are built in code
(`SetupGrid()`), not in a hand-authored Designer file — the normal pattern
for this control, and far less fragile than hand-crafting generated-style
XML/Designer boilerplate.

**Tech Stack:** C# / .NET Framework 4.0, WinForms, Telerik UI for WinForms
(`RadGridView`, `RadTextBox`, `RadButton`), `System.Data.OleDb` via
`SFICTDataAccess`.

## Global Constraints

- No schema changes to the database — every field used already exists.
  (Spec, implicit throughout)
- `PairingInspect` is read-only — never calls `UpdateDutyCreditsAndPay`,
  `MarkPMExamined`, `MarkMSExamined`, or any other mutating method. (Spec,
  Out of scope)
- Min-day amount is diff-based: `duty.ActCredit - sum(that duty's legs'
  actual credit)` (and the `ActPay` equivalent) — no cross-check against the
  `PM`/99901 marker. (Spec, Min-day flagging)
- Grid matches the reference layout: legs for a duty, then an inline
  duty-summary row, repeated per duty, then a pairing-level Totals row. No
  time-format toggle, no Normal/Compact view, no
  Exceptions/Crew/Comments/Hotel/Evaluate buttons. (Spec, Reference; Grid
  layout)
- `PVA` stays untouched — reference only, not revived. (Spec, Purpose)

---

## Task 1: Scaffold the `PairingInspect` project

**Files:**
- Create: `PairingInspect/PairingInspect.csproj`
- Create: `PairingInspect/Program.cs`
- Create: `PairingInspect/PairingInspectForm.cs`
- Create: `PairingInspect/Properties/AssemblyInfo.cs`
- Create: `PairingInspect/app.config`
- Modify: `PSAMinDayCalc.sln`

**Interfaces:**
- Consumes: nothing (first task).
- Produces: a building, empty-window WinForms app — Task 4 fills in
  `PairingInspectForm`'s actual content.

- [ ] **Step 1: Create the project directory and `.csproj`**

Create `PairingInspect/PairingInspect.csproj`, modeled on `PSAMinDay.csproj`
(same reference set, minus `MinDayProcess` which isn't needed here):

```xml
<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="12.0" DefaultTargets="Build" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <Import Project="$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props" Condition="Exists('$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props')" />
  <PropertyGroup>
    <Configuration Condition=" '$(Configuration)' == '' ">Debug</Configuration>
    <Platform Condition=" '$(Platform)' == '' ">AnyCPU</Platform>
    <ProjectGuid>{A1B2C3D4-1111-4E5F-9A6B-7C8D9E0F1A2B}</ProjectGuid>
    <OutputType>WinExe</OutputType>
    <AppDesignerFolder>Properties</AppDesignerFolder>
    <RootNamespace>PairingInspect</RootNamespace>
    <AssemblyName>PairingInspect</AssemblyName>
    <TargetFrameworkVersion>v4.0</TargetFrameworkVersion>
    <FileAlignment>512</FileAlignment>
  </PropertyGroup>
  <PropertyGroup Condition=" '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' ">
    <PlatformTarget>x86</PlatformTarget>
    <DebugSymbols>true</DebugSymbols>
    <DebugType>full</DebugType>
    <Optimize>false</Optimize>
    <OutputPath>bin\Debug\</OutputPath>
    <DefineConstants>DEBUG;TRACE</DefineConstants>
    <ErrorReport>prompt</ErrorReport>
    <WarningLevel>4</WarningLevel>
  </PropertyGroup>
  <PropertyGroup Condition=" '$(Configuration)|$(Platform)' == 'Release|AnyCPU' ">
    <PlatformTarget>AnyCPU</PlatformTarget>
    <DebugType>pdbonly</DebugType>
    <Optimize>true</Optimize>
    <OutputPath>bin\Release\</OutputPath>
    <DefineConstants>TRACE</DefineConstants>
    <ErrorReport>prompt</ErrorReport>
    <WarningLevel>4</WarningLevel>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="SFIConfigUtils, Version=1.0.0.0, Culture=neutral, processorArchitecture=x86">
      <SpecificVersion>False</SpecificVersion>
      <HintPath>..\..\SFIConfigUtils\bin\Debug\SFIConfigUtils.dll</HintPath>
    </Reference>
    <Reference Include="SFICTDateTimeUtils, Version=1.0.0.0, Culture=neutral, processorArchitecture=MSIL">
      <SpecificVersion>False</SpecificVersion>
      <HintPath>..\..\CTDateTimeUtils\bin\Debug\SFICTDateTimeUtils.dll</HintPath>
    </Reference>
    <Reference Include="System" />
    <Reference Include="System.Core" />
    <Reference Include="System.Xml.Linq" />
    <Reference Include="System.Data.DataSetExtensions" />
    <Reference Include="Microsoft.CSharp" />
    <Reference Include="System.Data" />
    <Reference Include="System.Deployment" />
    <Reference Include="System.Drawing" />
    <Reference Include="System.Windows.Forms" />
    <Reference Include="System.Xml" />
    <Reference Include="Telerik.WinControls, Version=2022.1.222.40, Culture=neutral, PublicKeyToken=5bb2a467cbec794e, processorArchitecture=MSIL">
      <HintPath>..\lib\RCWF\2022.1.222.40\Telerik.WinControls.dll</HintPath>
      <Private>True</Private>
    </Reference>
    <Reference Include="Telerik.WinControls.GridView, Version=2022.1.222.40, Culture=neutral, PublicKeyToken=5bb2a467cbec794e, processorArchitecture=MSIL">
      <HintPath>..\lib\RCWF\2022.1.222.40\Telerik.WinControls.GridView.dll</HintPath>
      <Private>True</Private>
    </Reference>
    <Reference Include="Telerik.WinControls.Themes.Office2010Black, Version=2022.1.222.40, Culture=neutral, PublicKeyToken=5bb2a467cbec794e, processorArchitecture=MSIL">
      <HintPath>..\lib\RCWF\2022.1.222.40\Telerik.WinControls.Themes.Office2010Black.dll</HintPath>
      <Private>True</Private>
    </Reference>
    <Reference Include="Telerik.WinControls.UI, Version=2022.1.222.40, Culture=neutral, PublicKeyToken=5bb2a467cbec794e, processorArchitecture=MSIL">
      <HintPath>..\lib\RCWF\2022.1.222.40\Telerik.WinControls.UI.dll</HintPath>
      <Private>True</Private>
    </Reference>
    <Reference Include="TelerikCommon, Version=2022.1.222.40, Culture=neutral, PublicKeyToken=5bb2a467cbec794e, processorArchitecture=MSIL">
      <HintPath>..\lib\RCWF\2022.1.222.40\TelerikCommon.dll</HintPath>
      <Private>True</Private>
    </Reference>
  </ItemGroup>
  <ItemGroup>
    <Compile Include="Program.cs" />
    <Compile Include="PairingInspectForm.cs">
      <SubType>Form</SubType>
    </Compile>
    <Compile Include="Properties\AssemblyInfo.cs" />
    <None Include="app.config" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\CTApp\CTApp.csproj">
      <Project>{659BEDFF-92A3-4F2D-A5AC-DA489983EA9D}</Project>
      <Name>CTApp</Name>
    </ProjectReference>
    <ProjectReference Include="..\..\CTDataAccess\SFICTDataAccess.csproj">
      <Project>{32DC1F3D-04BA-4BFC-A48B-1345BA31C063}</Project>
      <Name>SFICTDataAccess</Name>
    </ProjectReference>
  </ItemGroup>
  <Import Project="$(MSBuildToolsPath)\Microsoft.CSharp.targets" />
</Project>
```

Note: no `PairingInspectForm.Designer.cs`/`.resx` — the form builds its own
controls in code (`InitializeComponent`-equivalent written by hand in
`PairingInspectForm.cs` itself, Task 4), so there's no separate Designer
partial class to keep in sync.

- [ ] **Step 2: Create `Program.cs`**

```csharp
using System;
using System.Windows.Forms;

namespace PairingInspect
    {
    static class Program
        {
        [STAThread]
        static void Main()
            {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new PairingInspectForm());
            }
        }
    }
```

- [ ] **Step 3: Create a minimal `PairingInspectForm.cs`**

Just enough to build and show an empty window — Task 4 fills in the real UI.

```csharp
using System;
using System.Windows.Forms;

namespace PairingInspect
    {
    public partial class PairingInspectForm : CTAppNS.FormBase
        {
        public PairingInspectForm()
            {
            this.Text = "Pairing Inspect";
            this.Width = 1100;
            this.Height = 700;
            }
        }
    }
```

- [ ] **Step 4: Create `Properties/AssemblyInfo.cs`**

```csharp
using System.Reflection;
using System.Runtime.InteropServices;

[assembly: AssemblyTitle("PairingInspect")]
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("PairingInspect")]
[assembly: AssemblyCopyright("Copyright © 2026")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]
[assembly: ComVisible(false)]
[assembly: Guid("b2c3d4e5-2222-4f5a-9b6c-7d8e9f0a1b2c")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
```

- [ ] **Step 5: Create `app.config`**

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
    <startup>
        <supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.0"/>
    </startup>
</configuration>
```

- [ ] **Step 6: Add the project to `PSAMinDayCalc.sln`**

Add a new `Project(...)` entry (same GUID type as the other C# projects,
`{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}`) and corresponding
`ProjectConfigurationPlatforms` entries (`Debug|Any CPU`, `Release|Any CPU`)
matching the pattern of the existing `PSAMinDay` entry in the `.sln` file.

- [ ] **Step 7: Build to confirm the empty project compiles**

Run:
```
"C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe" "PairingInspect\PairingInspect.csproj" -p:Configuration=Debug -nologo -v:minimal
```
(Use `MSYS2_ARG_CONV_EXCL="*"` prefix if running from Git Bash, to avoid
path-mangling the `-p:`/`-nologo` switches.)
Expected: zero `error CS` lines (ignore `MSB3021`/`MSB3027` file-lock errors
if Visual Studio is open with the solution — see prior session note).

- [ ] **Step 8: Commit**

```bash
cd "D:/data/vs/psamindaycalc"
git add PairingInspect/ PSAMinDayCalc.sln
git commit -m "Scaffold PairingInspect project"
```

---

## Task 2: Expose `Updateid_Updempno` on `PM` / `PairingHeader`

**Files:**
- Modify: `D:\data\vs\CTDataAccess\CTPMs.cs` (separate repo — `SFICTDataAccess`)
- Modify: `D:\data\vs\CTDataAccess\CTPairing.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces: `PairingHeader.UpdateidUpdempno` (`uint`) — Task 3's marker-decode
  helper reads this; Task 4's header panel displays it.

- [ ] **Step 1: Add the field to the `PM` class and its `AddToList` mapping**

In `CTPMs.cs`, find the `PM` class's property list (near `ActualInd`, the
most recently added field — same pattern to follow) and add:

```csharp
public uint UpdateidUpdempno { get; set; }
```

In `AddToList` (the `foreach (CTDataSet.PMRow pmrow in pm)` loop), right
after the existing `pmRec.ActualInd = pmrow.ActualInd;` line, add:

```csharp
pmRec.UpdateidUpdempno = (uint)pmrow.Updateid_Updempno;
```

- [ ] **Step 2: Thread it through to `PairingHeader`**

In `CTPairing.cs`'s `GetPairingHeader`, inside the `PairingHeader` object
initializer (right after the existing `ActualInd = PMs.List[0].ActualInd`
line), add:

```csharp
UpdateidUpdempno = PMs.List[0].UpdateidUpdempno
```

In the `PairingHeader` class definition itself, add the corresponding
property:

```csharp
public uint UpdateidUpdempno { get; set; }
```

- [ ] **Step 3: Build to confirm it compiles**

Run: `MSBuild.exe "D:\data\vs\CTDataAccess\SFICTDataAccess.csproj" -p:Configuration=Debug -nologo -v:minimal`
Expected: zero `error CS` lines.

- [ ] **Step 4: Manual verification**

Pick a known pairing already marked by the examination-markers work (e.g.
query `SELECT TOP 1 ... FROM PM WHERE Updateid_Updempno IN (99901,99902,99903)`
via the established ODBC pattern to find one), note its `PrgNo`/`PrgDate`,
then confirm — via a small throwaway harness or by cross-checking the same
live query — that `PairingHeader.UpdateidUpdempno` for that pairing matches
what the direct SQL query shows.

- [ ] **Step 5: Commit (in the `SFICTDataAccess` repo)**

```bash
cd "D:/data/vs/CTDataAccess"
git add CTPMs.cs CTPairing.cs
git commit -m "Expose Updateid_Updempno on PM and PairingHeader"
```

---

## Task 3: Marker-decode and min-day-diff helpers

**Files:**
- Create: `PairingInspect/MarkerNameResolver.cs`
- Create: `PairingInspect/MinDayDiffCalculator.cs`

**Interfaces:**
- Consumes: `PairingHeader.UpdateidUpdempno` (Task 2); `CTPairing.PrgHdr`,
  `CTPairing.PairingDuties` (`List<PairingDutyItem>`, filter to
  `PairingDuty` for real duties), `CTPairing.PairingLegs`
  (`List<PairingLegItem>`, each item's `DutyPeriod` from the base
  `PairingItem` class).
- Produces: `MarkerNameResolver.Resolve(uint empno)` → `string` (Task 4's
  header panel); `MinDayDiffCalculator.CalculateForDuty(PairingDuty duty,
  IEnumerable<PairingLegItem> dutyLegs)` → `MinDayAmount { int Credit; int
  Pay; }` (Task 4/5's grid rendering).

- [ ] **Step 1: Write `MarkerNameResolver`**

```csharp
using System;
using System.Data.OleDb;
using SFICTDataAccess;

namespace PairingInspect
    {
    public static class MarkerNameResolver
        {
        public const uint MINDAY_UPDATED = 99901;
        public const uint MINDAY_NO_UPDATE_NEEDED = 99902;
        public const uint MINDAY_EXCEPTION = 99903;

        public static string Resolve(CTDataAccesBase dataAccess, uint empno)
            {
            if (empno == MINDAY_UPDATED) return "MinDay - Updated";
            if (empno == MINDAY_NO_UPDATE_NEEDED) return "MinDay - No Update Needed";
            if (empno == MINDAY_EXCEPTION) return "MinDay - Exception";

            using (OleDbCommand cmd = dataAccess.Connection.CreateCommand())
                {
                cmd.CommandText = "SELECT T09username FROM TR09 WHERE T09Key_Number = 9 AND T09Key_Key = ?";
                byte[] keyBytes = new byte[10];
                BitConverter.GetBytes(empno).CopyTo(keyBytes, 0);
                for (int i = 4; i < 10; i++) keyBytes[i] = 0x20;
                cmd.Parameters.AddWithValue("key", keyBytes);
                object result = cmd.ExecuteScalar();
                return result == null ? ("Employee " + empno) : result.ToString().Trim();
                }
            }
        }
    }
```

- [ ] **Step 2: Write `MinDayDiffCalculator`**

```csharp
using System.Collections.Generic;
using System.Linq;
using SFICTDataAccess;

namespace PairingInspect
    {
    public class MinDayAmount
        {
        public int Credit;
        public int Pay;
        public bool HasMinDay { get { return Credit > 0 || Pay > 0; } }
        }

    public static class MinDayDiffCalculator
        {
        public static MinDayAmount CalculateForDuty(PairingDuty duty, IEnumerable<PairingLegItem> dutyLegs)
            {
            int legCreditSum = 0;
            int legPaySum = 0;
            foreach (PairingLegItem leg in dutyLegs)
                {
                if (leg is AirlinePairingLeg)
                    {
                    AirlinePairingLeg airLeg = (AirlinePairingLeg)leg;
                    legCreditSum += airLeg.ActCredit;
                    legPaySum += airLeg.ActDhdPay;
                    }
                }

            MinDayAmount result = new MinDayAmount();
            result.Credit = duty.ActCredit > legCreditSum ? duty.ActCredit - legCreditSum : 0;
            result.Pay = duty.ActPay > legPaySum ? duty.ActPay - legPaySum : 0;
            return result;
            }
        }
    }
```

- [ ] **Step 3: Build to confirm both files compile**

Run: `MSBuild.exe "PairingInspect\PairingInspect.csproj" -p:Configuration=Debug -nologo -v:minimal`
Expected: zero `error CS` lines.

- [ ] **Step 4: Manual verification of `MarkerNameResolver`**

Using the established ODBC pattern, confirm `Resolve` returns
`"MinDay - Updated"`/`"MinDay - No Update Needed"`/`"MinDay - Exception"` for
99901/99902/99903 without a DB round trip (verify by code inspection — those
three branches return before touching `Connection`), and returns a real
employee's `T09username` for a genuine employee number pulled from a live
`PM.Updateid_Updempno` value that isn't one of the three markers.

- [ ] **Step 5: Manual verification of `MinDayDiffCalculator`**

Pick a pairing known to have had a real min-day update (a `PM` row marked
99901 from the examination-markers work), assemble it via `CTPairing`, and
confirm `CalculateForDuty` reports `HasMinDay = true` with a sensible
`Credit`/`Pay` amount for the duty that was actually topped up. Cross-check
against a pairing marked 99902 (no update) and confirm all its duties report
`HasMinDay = false`.

- [ ] **Step 6: Commit**

```bash
cd "D:/data/vs/psamindaycalc"
git add PairingInspect/MarkerNameResolver.cs PairingInspect/MinDayDiffCalculator.cs
git commit -m "Add marker-decode and min-day-diff helpers to PairingInspect"
```

---

## Task 4: Header panel and lookup wiring

**Files:**
- Modify: `PairingInspect/PairingInspectForm.cs`

**Interfaces:**
- Consumes: `CTPairing.Assemble(string, string)`, `CTPairing.PrgHdr`
  (`PairingHeader`), `MarkerNameResolver.Resolve` (Task 3).
- Produces: a working "Look Up" flow that loads a pairing and displays its
  header — Task 5 adds the grid content on top of this.

- [ ] **Step 1: Replace `PairingInspectForm.cs` with the input/header UI**

```csharp
using System;
using System.Windows.Forms;
using SFICTDataAccess;
using Telerik.WinControls;
using Telerik.WinControls.UI;

namespace PairingInspect
    {
    public partial class PairingInspectForm : CTAppNS.FormBase
        {
        RadTextBox txtPairingID;
        RadTextBox txtPairingDate;
        RadButton btnLookUp;
        Label lblHeader;
        CTPairing prg;

        public PairingInspectForm()
            {
            this.Text = "Pairing Inspect";
            this.Width = 1200;
            this.Height = 750;
            RadMessageBox.ThemeName = (new Telerik.WinControls.Themes.Office2010BlackTheme()).ThemeName;

            prg = new CTPairing();

            txtPairingID = new RadTextBox { Left = 20, Top = 20, Width = 150 };
            txtPairingDate = new RadTextBox { Left = 190, Top = 20, Width = 120 };
            btnLookUp = new RadButton { Left = 330, Top = 18, Width = 100, Text = "Look Up" };
            btnLookUp.Click += btnLookUp_Click;

            lblHeader = new Label { Left = 20, Top = 55, Width = 1150, Height = 60, AutoSize = false };

            this.Controls.Add(txtPairingID);
            this.Controls.Add(txtPairingDate);
            this.Controls.Add(btnLookUp);
            this.Controls.Add(lblHeader);
            }

        private void btnLookUp_Click(object sender, EventArgs e)
            {
            try
                {
                int result = prg.Assemble(txtPairingID.Text.Trim(), txtPairingDate.Text.Trim());
                if (result != 1)
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
                hdr.PrgID, hdr.PrgDate, hdr.ActEnd.AsDisplayDate, hdr.NumDP, hdr.Canceled, hdr.CrewType, markerName);
            }
        }
    }
```

- [ ] **Step 2: Build to confirm it compiles**

Run: `MSBuild.exe "PairingInspect\PairingInspect.csproj" -p:Configuration=Debug -nologo -v:minimal`
Expected: zero `error CS` lines. If `PairingHeader` field names (`PrgID`,
`PrgDate`, `NumDP`, `Canceled`) don't match exactly, correct them against the
actual `PairingHeader` class definition (`CTPairing.cs`, class starting at
the line found via `grep "class PairingHeader"`) before proceeding — this
step's code was written from the constructor call signature, not the full
field list, so names need confirming against the real class.

- [ ] **Step 3: Manual smoke test**

Run the built `PairingInspect.exe`, enter a known real pairing ID/date (e.g.
one of the ones inspected earlier this session, like `L7755`/`20240715`),
click "Look Up", confirm the header line populates with real data and the
correct "Last touched by" value (compare against a direct SQL query on that
pairing's `Updateid_Updempno`).

- [ ] **Step 4: Commit**

```bash
cd "D:/data/vs/psamindaycalc"
git add PairingInspect/PairingInspectForm.cs
git commit -m "Add pairing lookup and header display to PairingInspect"
```

---

## Task 5: Duty/leg grid with min-day flagging and totals

**Files:**
- Modify: `PairingInspect/PairingInspectForm.cs`

**Interfaces:**
- Consumes: `CTPairing.PairingDuties` (`List<PairingDutyItem>`),
  `CTPairing.PairingLegs` (`List<PairingLegItem>`),
  `MinDayDiffCalculator.CalculateForDuty` (Task 3).
- Produces: the finished tool — no further tasks depend on this one.

- [ ] **Step 1: Add the grid and a `SetupGrid`/`PopulateGrid` pair**

Add to `PairingInspectForm`:

```csharp
        RadGridView grid;

        // in the constructor, after lblHeader setup:
        grid = new RadGridView { Left = 20, Top = 120, Width = 1150, Height = 560 };
        SetupGrid();
        this.Controls.Add(grid);

        private void SetupGrid()
            {
            grid.MasterTemplate.AutoGenerateColumns = false;
            grid.Columns.Add(new GridViewTextBoxColumn("DutyPeriod", "DutyPeriod") { HeaderText = "Duty" });
            grid.Columns.Add(new GridViewTextBoxColumn("RowKind", "RowKind") { HeaderText = "" });
            grid.Columns.Add(new GridViewTextBoxColumn("Date", "Date") { HeaderText = "Date" });
            grid.Columns.Add(new GridViewTextBoxColumn("FlightNum", "FlightNum") { HeaderText = "Flight #" });
            grid.Columns.Add(new GridViewTextBoxColumn("DeptCity", "DeptCity") { HeaderText = "Org" });
            grid.Columns.Add(new GridViewTextBoxColumn("ArrvCity", "ArrvCity") { HeaderText = "Dst" });
            grid.Columns.Add(new GridViewTextBoxColumn("DeptTime", "DeptTime") { HeaderText = "Dept" });
            grid.Columns.Add(new GridViewTextBoxColumn("ArrvTime", "ArrvTime") { HeaderText = "Arrv" });
            grid.Columns.Add(new GridViewTextBoxColumn("Credit", "Credit") { HeaderText = "Credit" });
            grid.Columns.Add(new GridViewTextBoxColumn("Summary", "Summary") { HeaderText = "Duty Summary" });
            grid.Columns.Add(new GridViewTextBoxColumn("MinDayFlag", "MinDayFlag") { HeaderText = "Min-Day" });
            }
```

- [ ] **Step 2: Add a row-DTO and `PopulateGrid`**

```csharp
        public class InspectRow
            {
            public string DutyPeriod;
            public string RowKind;   // "Leg" or "Duty"
            public string Date;
            public string FlightNum;
            public string DeptCity;
            public string ArrvCity;
            public string DeptTime;
            public string ArrvTime;
            public int Credit;
            public string Summary;
            public string MinDayFlag;
            }

        private void PopulateGrid()
            {
            List<InspectRow> rows = new List<InspectRow>();
            int totalCredit = 0, totalPay = 0, totalMinDayCredit = 0, totalMinDayPay = 0;

            var dutiesByPeriod = prg.PairingDuties.OfType<PairingDuty>()
                .OrderBy(d => d.DutyPeriod).ToList();

            foreach (PairingDuty duty in dutiesByPeriod)
                {
                var dutyLegs = prg.PairingLegs.Where(l => l.DutyPeriod == duty.DutyPeriod).ToList();

                foreach (PairingLegItem leg in dutyLegs.OfType<AirlinePairingLeg>())
                    {
                    AirlinePairingLeg airLeg = (AirlinePairingLeg)leg;
                    rows.Add(new InspectRow
                        {
                        DutyPeriod = duty.DutyPeriod.ToString(),
                        RowKind = "Leg",
                        Date = airLeg.FlightDateDisplay,
                        FlightNum = airLeg.FlightNum,
                        DeptCity = airLeg.DeptCity,
                        ArrvCity = airLeg.ArrvCity,
                        DeptTime = airLeg.SkedDeptTimeasHHMM,
                        ArrvTime = airLeg.SkedArrvTimeasHHMM,
                        Credit = airLeg.ActCredit
                        });
                    }

                MinDayAmount minDay = MinDayDiffCalculator.CalculateForDuty(duty, dutyLegs);
                rows.Add(new InspectRow
                    {
                    DutyPeriod = duty.DutyPeriod.ToString(),
                    RowKind = "Duty",
                    Summary = string.Format("Report {0}  Release {1}  Credit {2}  Pay {3}  Layover {4}",
                        duty.Report.AsDisplayDate + " " + duty.Report.AsDisplayTime,
                        duty.ActEnd.AsDisplayDate + " " + duty.ActEnd.AsDisplayTime,
                        duty.ActCredit, duty.ActPay, duty.ActLayover),
                    MinDayFlag = minDay.HasMinDay
                        ? string.Format("Min-Day: +{0} credit / +{1} pay", minDay.Credit, minDay.Pay)
                        : ""
                    });

                totalCredit += duty.ActCredit;
                totalPay += duty.ActPay;
                totalMinDayCredit += minDay.Credit;
                totalMinDayPay += minDay.Pay;
                }

            rows.Add(new InspectRow
                {
                RowKind = "Totals",
                Summary = string.Format("TOTALS  Credit {0}  Pay {1}", totalCredit, totalPay),
                MinDayFlag = (totalMinDayCredit > 0 || totalMinDayPay > 0)
                    ? string.Format("Min-Day Credit: +{0} / +{1}", totalMinDayCredit, totalMinDayPay)
                    : ""
                });

            grid.DataSource = rows;
            }
```

- [ ] **Step 3: Call `PopulateGrid` from the lookup handler**

In `btnLookUp_Click`, after `DisplayHeader();`, add:
```csharp
PopulateGrid();
```

- [ ] **Step 4: Add the required `using` statements**

At the top of `PairingInspectForm.cs`:
```csharp
using System.Collections.Generic;
using System.Linq;
using Telerik.WinControls.UI;
```

- [ ] **Step 5: Build to confirm it compiles**

Run: `MSBuild.exe "PairingInspect\PairingInspect.csproj" -p:Configuration=Debug -nologo -v:minimal`
Expected: zero `error CS` lines. `DutyPeriod` on `PairingItem`/`PairingLegItem`
needs confirming against the actual base class (`PairingItem`, referenced in
`PairingDutyItem`/`PairingLegItem`'s constructors as the second constructor
argument) — correct the property name if it differs from what's used above.

- [ ] **Step 6: Manual end-to-end test**

Run `PairingInspect.exe`, look up a **multi-duty pairing known to have had a
min-day update** (a `PM` marked 99901 with `NumDuties > 1`). Confirm:
- Legs appear grouped under the correct duty period, in report order.
- Each duty's summary row appears immediately after its legs.
- The duty that actually got the min-day top-up shows a non-empty
  `MinDayFlag` with a plausible credit/pay amount; other duties in the same
  pairing show no flag.
- The Totals row's `MinDayFlag` matches the sum of the per-duty amounts.

Then look up a pairing marked 99902 (no update) and confirm **no** duty or
the totals row shows any min-day flag.

- [ ] **Step 7: Commit**

```bash
cd "D:/data/vs/psamindaycalc"
git add PairingInspect/PairingInspectForm.cs
git commit -m "Add duty/leg grid with min-day flagging and totals to PairingInspect"
```

---

## Self-Review Notes

- **Spec coverage:** Purpose/relation to PVA → untouched, confirmed nothing
  in this plan touches `PVA/`. Scaffolding conventions → Task 1. Data access
  reuse + the two additive gaps (marker decode, min-day amount) → Tasks 2-3.
  Grid layout (legs-then-duty-summary, repeated, plus totals) → Task 5.
  Min-day flagging per-duty and pairing-level → Task 5. Out-of-scope items
  (toolbar/buttons, PVA revival, write capability) — correctly absent from
  every task.
- **Placeholder scan:** no TBD/TODO. Two explicit "confirm against the real
  class" notes (Task 4 Step 2, Task 5 Step 5) are flagged because this plan
  was written from partial field visibility (constructor signatures and
  property getters seen directly, but not every single property name on
  `PairingHeader`/`PairingItem` was individually confirmed) — these are
  legitimate implementation-time verification points, not vague hand-waving,
  and the step tells the implementer exactly what to check and where.
- **Type consistency:** `MinDayAmount`, `MarkerNameResolver.Resolve`, and
  `InspectRow` are each defined once (Task 3, Task 5) and used identically
  wherever referenced afterward.
