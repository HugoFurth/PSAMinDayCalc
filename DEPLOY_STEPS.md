# MinDay Calc — Deploy Steps

Building and deploying `MinDayProcess.dll` and `PairingInspect2` ("PrgInsp.exe") to
`d:\sfi\exe` for site testing. Last verified 2026-10-27.

## What ships

| Artifact | Built from | Merge tool | Notes |
|---|---|---|---|
| `MinDayProcess.dll` | `MinDayProcess\MinDayProcess.csproj` | ILMerge | Self-contained. Used externally by `PSAMinDay.exe` (unchanged, not rebuilt this cycle). |
| `PrgInsp.exe` | `PairingInspect2\PairingInspect2.csproj` | ILRepack | Fully self-contained — `MinDayProcess.dll` + `MSEval.dll` are merged *into* it (see note below), not shipped alongside. |
| `PSAMinDay.exe` | *(not rebuilt this cycle)* | — | Must already be present in `d:\sfi\exe` — loads `MinDayProcess.dll` externally, so a new `MinDayProcess.dll` build takes effect on its next launch with no rebuild of `PSAMinDay.exe` itself needed. |

**Why PrgInsp.exe's merge differs from PairingInspect/PSAMinDay's:** PairingInspect2 calls
`MinDayProcess.ComputeLayoverCreditForDisplay(CTPairing, List<PairingDuty>)` directly,
passing live `SFICTDataAccess` objects across the call. If `SFICTDataAccess` were merged
separately into both `PrgInsp.exe` and the standalone `MinDayProcess.dll`, `CTPairing`
would be two distinct CLR types and the call would throw at runtime (confirmed live via
ILRepack's own "likely invalid set of assemblies" warning). Folding `MinDayProcess.dll`
directly into `PrgInsp.exe`'s own merge avoids this, at the cost of needing a
rebuild+redeploy of `PrgInsp.exe` whenever `MinDayProcess.dll` changes. Revisit this
trade-off later if that becomes painful — the clean fix is to stop merging
`SFICTDataAccess`/`SFICTDateTimeUtils` into either binary and ship them loose instead,
shared by both.

## 1. Build

Close/stop any running `PrgInsp.exe` instance first — a live debug session locks
`bin\Debug\PrgInsp.exe` and the build will fail with `MSB3027`.

```powershell
$msbuild = "C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe"
& $msbuild "D:\Data\VS\PSAMinDayCalc\PSAMinDayCalc.sln" /p:Configuration=Debug /nologo /v:minimal
```

## 2. Merge & deploy MinDayProcess.dll

Run from `MinDayProcess\bin\Debug` (relative DLL names in the ILMerge args):

```batch
D:\VSMerge\ILMerge.exe "MinDayProcess.dll" /out:d:\sfi\exe\MinDayProcess.dll ^
  /target:dll /targetplatform:"v4,C:\Windows\Microsoft.NET\Framework\v4.0.30319" ^
  mseval.dll sfictdatetimeutils.dll sfictdataaccess.dll SFIConfigUtils.dll
```

Saved as `MinDayProcess\bin\Debug\0MinDayProcessMerge.bat` — unchanged from the
long-standing version also used to produce the copy `PSAMinDay.exe` loads externally.

## 3. Merge & deploy PrgInsp.exe

Run from `PairingInspect2\bin\Debug`:

First, capture `MinDayProcess.dll`'s version before it gets merged in — this is the only
moment that version is still readable as its own assembly manifest (see gotcha #4 below):

```batch
powershell -NoProfile -Command "[System.Reflection.AssemblyName]::GetAssemblyName((Resolve-Path 'MinDayProcess.dll')).Version.ToString() | Set-Content -NoNewline -Encoding ascii 'd:\sfi\exe\MinDayProcess.MergedVersion.txt'"
```

```batch
D:\VSMerge\ILRepack.exe "PrgInsp.exe" /out:d:\sfi\exe\PrgInsp.exe /target:winexe ^
  Telerik.WinControls.UI.dll Telerik.WinControls.dll Telerik.WinControls.GridView.dll ^
  TelerikCommon.dll Telerik.WinControls.Themes.Office2010Black.dll ^
  SFICTDataAccess.dll SFICTDateTimeUtils.dll SFIConfigUtils.dll ^
  MinDayProcess.dll MSEval.dll
```

Then overwrite ILRepack's own auto-merged config with the hand-corrected template —
**required every time**, see gotcha #1 below:

```batch
copy /Y "PrgInsp.exe.config.deploy" "d:\sfi\exe\PrgInsp.exe.config"
```

Both steps are saved together as `PairingInspect2\bin\Debug\0PairingInspect2Merge.bat`.

## 4. Required supporting files in d:\sfi\exe

| File | Purpose | Required for |
|---|---|---|
| `PrgInsp.exe.config` | Connection strings + `MinDayProcess.Properties.Settings/ExcludeableCodes` + `PairingInspect2.Properties.Settings` section. Written by step 3's copy, not hand-edited directly. | **required** |
| `MinDayProcess.MergedVersion.txt` | Written by step 3's PowerShell line. `PrgInsp.exe` reads it at startup to warn if the standalone `MinDayProcess.dll` next to it doesn't match what was merged in — see gotcha #4. | **required** |
| `MinDay.config` | The 4 MinDay marker empnos (`MINDAY_APPLIED_USER` etc.), read by `MinDayMarkerUsers.GetMinDayUser`. | **required** |
| `SFI.config` | Read via `SFIConfig.AppSetting("CTEXEDIR")` — needed by every button that launches CTWPM/CTWMS (Inquire, Modify, Evaluate, the exceptions-grid menu, crew-cell double-click). | **required** |
| `CTSecurityProfile.bin` | Only for unattended callers (`PSAMinDay.exe`). `PrgInsp.exe` runs interactively with a live CT session and never hits this fallback. | PSAMinDay only |
| `MinDayProcess.json` | The scheduled batch's "last processed" cursor. Single-pairing on-demand calls (Recalculate button) never touch it. | PSAMinDay only |
| `mseval.dll` (loose) | Vestigial from an earlier approach — both deployed binaries now embed their own copy. | unused |

> **TODO, revisit after testing:** `PrgInsp.exe.config` carries three things read through
> three different mechanisms — (1) connection strings, via `SFICTDataAccess`'s own
> auto-generated `ApplicationSettingsBase`, hardwired to the entry exe's own `.exe.config`;
> (2) `ExcludeableCodes`, via `SFIConfigUtils.AssemblyConfig.SetAppConfig`, which *also*
> opens the entry exe's own config but is custom code, not auto-generated; (3)
> `PairingInspect2.Properties.Settings` (`LastPairingID`, saved grid layouts), genuinely
> per-user UI state. Of these, only (2) is a real candidate to move into `SFI.config` or
> `MinDay.config` instead (same `OpenMappedExeConfiguration`-against-a-fixed-path pattern
> those already use) — (1) would need changing `SFICTDataAccess`'s own settings mechanism
> (touches every consumer), and (3) shouldn't move at all since it's per-app state, not
> shared config. Worth doing once the current merge/deploy approach is proven out — less
> duplicated config content means less surface area for the kind of config bugs this round
> of testing already turned up.

## 4b. External prerequisite: native CrewTrac client

Not something this deployment builds or copies, but `SFI.config`'s `CTEXEDIR` setting
(currently `d:\crewtrac\exe` — a different folder from `d:\sfi\exe`) must point at a real
native CrewTrac client install containing `ctwpm.exe`, `ctwms.exe`, and `CTWMSEVL.DLL`, or
every Inquire/Modify/Evaluate button and the crew-cell CTWMS launch in `PrgInsp.exe` will
fail. A new site needs this already installed and `CTEXEDIR` pointed at it correctly.

## 4c. Customer site's SFI.config connection string

This dev/test machine's `SFI.config` points at the local `DATPSA` Pervasive engine. At the
customer site, `SFI.config`'s `<connectionStrings>` entry needs to point at their real
server instead. They gave us an ODBC-formatted string
(`Driver={Pervasive ODBC Client Interface};TCPPort=1583;ServerName=esdayapt1009035.psa.local;
ServerDSN=CrewTracIR;TransportHint=TCP`) — `GlobalCTDataSet.cs` doesn't support raw ODBC at
all (only `OleDbConnection`, or the `Pervasive.Data.SqlClient` provider via
`DbProviderFactory`).

**Must be OleDb, not `Pervasive.Data.SqlClient`.** An earlier version of this note
recommended the `Pervasive.Data.SqlClient` format — confirmed live to be a real bug: that
provider routes `GlobalCTDataSet` into its `DbCommon` branch, which populates
`_DbConnection` but leaves the plain `OleDbConnection` field `null`. Almost every
data-access class in this codebase (`CTEmployees`, `CTPairing`,
`MinDayMarkerUsers.EnsureExist`, etc.) reads `.Connection` typed specifically as
`OleDbConnection` — so that null surfaces as `NullReferenceException: Object reference not
set to an instance of an object` the moment anything touches the database. `SFI.config`'s
own `Pervasive.Data.SqlClient` examples are commented out, which in hindsight was the
hint — only the plain OleDb entry is actually live-used anywhere.

Converted to the OleDb format instead, matching `SFI.config`'s own live pattern
(`Provider=PervasiveOLEDB;Data Source=DATPSA;Location=localhost`), confirmed with the
customer that the port suffix on `ServerName` isn't actually part of the real hostname:

```xml
<connectionStrings>
  <add name="SFICTDataAccess"
    connectionString="Provider=PervasiveOLEDB;Data Source=CrewTracIR;Location=esdayapt1009035.psa.local" />
</connectionStrings>
```

Not yet tested against the customer's actual server — verify this resolves and connects
once deployed there. If `Data Source=CrewTracIR` doesn't resolve, that value may need to be
whatever Pervasive engine/DSN name is actually registered on their server, not necessarily
the ODBC DSN name they gave us (`CrewTracIR` was `ServerDSN` in their ODBC string, so it's
a reasonable first guess, but OleDb's "Data Source" and ODBC's "DSN" aren't always the same
registry).

## 5. Launch & verify

`PrgInsp.exe` — working directory doesn't matter (no relative-path reads on this one):

```powershell
Start-Process -FilePath "D:\sfi\exe\PrgInsp.exe"
```

`PSAMinDay.exe` — **must** set the working directory, or `CTSecurityProfile.bin` (a
relative-path read) silently fails to resolve:

```powershell
Start-Process -FilePath "D:\sfi\exe\PSAMinDay.exe" -WorkingDirectory "D:\sfi\exe"
```

Confirm both stay running past ~10s (a config/type-identity failure crashes almost
immediately) and check Windows Event Viewer → Application log for a fresh `.NET Runtime`
error if either exits unexpectedly.

## Known gotchas

### 1. ILRepack's auto-merged config is invalid XML order

ILRepack combines the `.config` fragments from every merged assembly, but puts
`<configSections>` *after* `<startup>`/`<appSettings>` — invalid per .NET's config schema
(`configSections` must be the first child of `<configuration>`). This throws
`ConfigurationErrorsException` the instant anything touches `ConfigurationManager`, which
happens early in `RadForm1`'s constructor (`CTEmployees.FillAll()`). Step 3's copy
overwrites it with a hand-corrected, known-good version —
`PairingInspect2\bin\Debug\PrgInsp.exe.config.deploy`. If that template is ever
regenerated from scratch, it needs:

- `<configSections>` first, declaring `PairingInspect2.Properties.Settings` (userSettings)
  and `MinDayProcess.Properties.Settings` / `MinDayProcessNS.Properties.Settings` (both
  groups)
- `<applicationSettings><MinDayProcess.Properties.Settings>` with
  `ExcludeableCodes = GRS;LFT` — the section must have an actual value, not just be
  declared
- `<connectionStrings>` for `SFICTDataAccess.Properties.Settings.*`

### 2. AssemblyConfig section lookup breaks when merged

`MinDayProcess.cs`'s constructor used to call
`SetAppConfig(Assembly.GetExecutingAssembly().Location, Assembly.GetExecutingAssembly().GetName().Name)`.
Once `MinDayProcess` is merged into `PrgInsp.exe`, `GetExecutingAssembly()` resolves to
`PrgInsp.exe` itself, so it went looking for a nonexistent `PrgInsp.Properties.Settings`
section and threw on every "Recalculate Min Day" click. **Fixed** — the assembly name is
now hardcoded to `"MinDayProcess"`, correct regardless of which exe the code physically
ends up inside.

### 3. Working directory for PSAMinDay.exe

`CTSecurity.cs` opens `CTSecurityProfile.bin` by relative path, resolved against the
process's working directory at launch — not its own exe folder. Launching without
`-WorkingDirectory "D:\sfi\exe"` silently misses the real file.

### 4. The version check had to move outside the merged assembly

`GetMinDayProcess()` in `RadForm1.cs` used to compare
`typeof(MinDayProcess).Assembly.GetName().Version` against a hardcoded minimum — a guard
against a stale *standalone* `MinDayProcess.dll` being left behind. Once `MinDayProcess.dll`
is merged into `PrgInsp.exe`, that type's assembly *is* `PrgInsp.exe`, so its version is
`PrgInsp.exe`'s own (stuck at `1.0.0.0`) — the check always failed, and was removed.

But the underlying scenario came back in a different shape: `PSAMinDay.exe` still loads a
genuinely separate, standalone `MinDayProcess.dll`, so a deploy that updates only
`PrgInsp.exe` (or only that standalone DLL) leaves the two running different MinDay logic
silently. Since merging destroys `MinDayProcess.dll`'s own manifest (there's no API that
can recover "what version got merged in" from inside `PrgInsp.exe` after the fact), the
version has to be captured *before* the merge and stashed somewhere else — that's what
step 3's PowerShell line and `MinDayProcess.MergedVersion.txt` do. `GetMinDayProcess()` now
reads that sidecar file and compares it against the standalone `MinDayProcess.dll` sitting
next to `PrgInsp.exe` (via `AssemblyName.GetAssemblyName`, which only reads the file's
manifest — doesn't load it, so it can't clash with the copy already merged in), warning via
`RadMessageBox` on a mismatch. A missing sidecar file (an older deploy, pre-dating this
check) is treated as "nothing to compare" rather than an error.

---
PSAMinDayCalc solution · feature/pairing-inspect branch
