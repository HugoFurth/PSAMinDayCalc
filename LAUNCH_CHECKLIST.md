# MinDay Calc — Launch Checklist

Assumes `MinDayProcess.dll` and `PrgInsp.exe` are already built, merged, and copied to
`d:\sfi\exe`. For build/merge steps, see `DEPLOY_STEPS.md`.

## Files that need to be in d:\sfi\exe

- `MinDayProcess.dll`
- `MinDayProcess.MergedVersion.txt` (written automatically by `0PairingInspect2Merge.bat` -- records the `MinDayProcess.dll` version that got merged into this `PrgInsp.exe` build, so `PrgInsp.exe` can warn if the standalone `MinDayProcess.dll` next to it doesn't match)
- `PrgInsp.exe` + `PrgInsp.exe.config`
- `PSAMinDay.exe` (not rebuilt this cycle — loads `MinDayProcess.dll` externally, so it just needs to already be present)
- `MinDay.config`
- `SFI.config`
- `CTSecurityProfile.bin`
- `MinDayProcess.json` (only needed for `PSAMinDay.exe`'s scheduled batch)

## Also required: a native CrewTrac client install

Not part of this deployment, but `PrgInsp.exe`'s Inquire/Modify/Evaluate buttons and the
crew-cell CTWMS launch won't work without it. `SFI.config`'s `CTEXEDIR` setting points at
it (currently `d:\crewtrac\exe` — confirm the actual path on the target machine). Needs:

- `ctwpm.exe`
- `ctwms.exe`
- `CTWMSEVL.DLL`

## Set the customer's connection string in SFI.config

This machine's `SFI.config` points at the local test database. At the customer site, set
its `<connectionStrings>` entry to their real server instead — **must use the
`PervasiveOLEDB` provider, not `Pervasive.Data.SqlClient`** (the latter leaves
`GlobalCTDataSet`'s `OleDbConnection` field null, which crashes almost everything with
`NullReferenceException` — confirmed live, see `DEPLOY_STEPS.md` section 4c):

```xml
<connectionStrings>
  <add name="SFICTDataAccess"
    connectionString="Provider=PervasiveOLEDB;Data Source=CrewTracIR;Location=esdayapt1009035.psa.local" />
</connectionStrings>
```

Not yet tested against the customer's actual server — see `DEPLOY_STEPS.md` section 4c
for how this was derived.

## Launch PrgInsp.exe

```powershell
Start-Process -FilePath "D:\sfi\exe\PrgInsp.exe"
```

## Launch PSAMinDay.exe

Must set the working directory, or it won't find `CTSecurityProfile.bin`:

```powershell
Start-Process -FilePath "D:\sfi\exe\PSAMinDay.exe" -WorkingDirectory "D:\sfi\exe"
```

## Confirm it worked

- Window opens and stays open past ~10 seconds (a config problem crashes almost
  immediately, often with no visible error).
- If it closes on its own, check Windows Event Viewer → Windows Logs → Application for a
  recent `.NET Runtime` error — that'll name the exact exception.
