# MinDayCalc PM & MS Examination Markers Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give `MinDayProcess` a durable, queryable record of whether it has
examined a given `PM` (pairing) or `MS` (schedule) record and what it
concluded, by stamping `Updateid_Updempno` with one of three synthetic
"system user" identities.

**Architecture:** Two new hand-written methods (`MarkPMExamined`,
`MarkMSExamined`) on the existing `CTPairing` class in the separate
`SFICTDataAccess` repo, called from new marking call-sites wired into
`MinDayProcess.ProcessPairing` and `MinDayProcess.EvaluateSkeds`. Three new
`TR09` rows (data only, no schema change) give the marker employee numbers
human-readable names.

**Tech Stack:** C# / .NET Framework 4.0, `System.Data.OleDb` (production code),
Pervasive/Actian PSQL (`DATPSA` database) accessed via ODBC (`DATPSADSN`, a
32-bit-only DSN — verification commands in this plan must run through
`C:\Windows\SysWOW64\WindowsPowerShell\v1.0\powershell.exe`, not the default
64-bit host).

## Global Constraints

- No database schema changes: no new tables, no new fields. (Spec, Constraints)
- Marker writes must never touch `Updateid_Upddate` / `Updateid_Updtime` on
  `PM` or `MS` — only `Updateid_Updempno`. (Spec, Background/Resolution)
- Marker writes only happen when `ppAction == PairingProcessAction.EvalupateAndUpdate`
  — never during `EvaluateOnly` dry-runs. (Spec, PM marking: call-site mapping)
- Every marker write is wrapped in its own try/catch; a failure is logged via
  `UpdateStatus(MinDayStatus.Critical, ...)` and processing continues — never
  aborts the batch, never throws back out of `ProcessPairing`/`EvaluateSkeds`.
  (Spec, PM/MS marking: error handling)
- The three marker employee numbers are fixed: 99901 (`MinDay - Updated`),
  99902 (`MinDay - No Update Needed`, `PM` only), 99903 (`MinDay - Exception`).
  (Spec, The three markers)
- No edits to `CTDataSet.xsd` / `CTDataSet.Designer.cs` (the generated
  dataset) — all new SQL is hand-written `OleDbCommand`/`OdbcCommand`.
  (Spec, PM marking: implementation; corrected during planning)

---

## Task 1: Seed the three `TR09` marker identities

**Files:**
- Create: `sql/2026-07-15-seed-mindaycalc-tr09-markers.ps1`

**Interfaces:**
- Consumes: nothing (first task, no code dependencies).
- Produces: three live rows in `TR09` (`T09Key_Number=9`,
  `T09Key_Key`=packed-binary(99901/99902/99903)) that later tasks' marker
  writes will point at via `Updateid_Updempno`. Nothing in later tasks reads
  this data programmatically — it exists purely so `Updateid_Updempno` values
  of 99901/99902/99903 resolve to a readable name in any tool that looks them
  up against `TR09`.

This is a one-time production data load, run manually (not part of
`MinDayProcess`'s runtime code). It must run through the 32-bit PowerShell
host because the `DATPSADSN` driver is 32-bit only.

- [ ] **Step 1: Write the seed script**

Create `sql/2026-07-15-seed-mindaycalc-tr09-markers.ps1`:

```powershell
# One-time seed of the three MinDayCalc synthetic TR09 "system user"
# identities used as PM/MS examination markers. Run once against production
# before the MarkPMExamined/MarkMSExamined code ships. Safe to re-run — it
# checks for existing rows first and skips them.
#
# Must run through the 32-bit PowerShell host (DATPSADSN is a 32-bit-only
# Pervasive ODBC driver):
#   C:\Windows\SysWOW64\WindowsPowerShell\v1.0\powershell.exe -File sql\2026-07-15-seed-mindaycalc-tr09-markers.ps1

Add-Type -AssemblyName System.Data

$markers = @(
    @{ EmpNo = 99901; Username = "MinDay - Updated" }
    @{ EmpNo = 99902; Username = "MinDay - No Update Needed" }
    @{ EmpNo = 99903; Username = "MinDay - Exception" }
)

$conn = New-Object System.Data.Odbc.OdbcConnection("DSN=DATPSADSN")
$conn.Open()

foreach ($m in $markers) {
    $checkCmd = $conn.CreateCommand()
    $checkCmd.CommandText = "SELECT COUNT(*) FROM TR09 WHERE T09Key_Number = 9 AND T09username = ?"
    [void]$checkCmd.Parameters.AddWithValue("username", $m.Username)
    $existing = $checkCmd.ExecuteScalar()
    if ($existing -gt 0) {
        Write-Output "SKIP: $($m.Username) already exists ($existing row(s))"
        continue
    }

    # Pack the employee number as a 10-byte key: 4-byte little-endian
    # unsigned int + 6 bytes of blank padding, matching the existing
    # encoding for every other TR09 row (confirmed empirically against
    # live data: e.g. empno 23560 -> bytes 08 5C 00 00 20 20 20 20 20 20).
    $empnoBytes = [BitConverter]::GetBytes([uint32]$m.EmpNo)
    $keyBytes = $empnoBytes + [byte[]](0x20,0x20,0x20,0x20,0x20,0x20)

    $insertCmd = $conn.CreateCommand()
    $insertCmd.CommandText = @'
INSERT INTO TR09
    (T09Key_Number, T09Key_Key, T09pswd, T09secLevel1, T09secLevel2,
     T09rptId, T09secCrewtype, T09username, T09usertype, T09RestrictCrewmsg,
     T09Active, T09GroupId)
VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
'@
    [void]$insertCmd.Parameters.AddWithValue("num", [int16]9)
    [void]$insertCmd.Parameters.AddWithValue("key", $keyBytes)
    [void]$insertCmd.Parameters.AddWithValue("pswd", "")
    [void]$insertCmd.Parameters.AddWithValue("sl1", [int16]0)
    [void]$insertCmd.Parameters.AddWithValue("sl2", "")
    [void]$insertCmd.Parameters.AddWithValue("rptid", "")
    [void]$insertCmd.Parameters.AddWithValue("crewtype", "B")
    [void]$insertCmd.Parameters.AddWithValue("username", $m.Username)
    [void]$insertCmd.Parameters.AddWithValue("usertype", "O")
    [void]$insertCmd.Parameters.AddWithValue("restrict", "")
    [void]$insertCmd.Parameters.AddWithValue("active", "N")
    [void]$insertCmd.Parameters.AddWithValue("groupid", "DIRECTORS")

    $rows = $insertCmd.ExecuteNonQuery()
    Write-Output "INSERTED: $($m.Username) (empno $($m.EmpNo)) - $rows row(s)"
}

$conn.Close()
```

- [ ] **Step 2: Confirm the script is syntactically valid**

Run:
```
powershell -NoProfile -Command "[void][System.Management.Automation.Language.Parser]::ParseFile('sql\2026-07-15-seed-mindaycalc-tr09-markers.ps1', [ref]$null, [ref]$null); Write-Output 'Parse OK'"
```

Expected output: `Parse OK`

- [ ] **Step 3: Run it against production (requires explicit confirmation before executing — this writes to the live `TR09` table)**

Run through the 32-bit host:
```
"C:\Windows\SysWOW64\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -File "sql\2026-07-15-seed-mindaycalc-tr09-markers.ps1"
```
Expected output: three `INSERTED: ...` lines (or `SKIP: ...` if re-run).

- [ ] **Step 4: Verify the three rows exist with correct decoded employee numbers**

Run through the 32-bit host:
```powershell
Add-Type -AssemblyName System.Data
$conn = New-Object System.Data.Odbc.OdbcConnection("DSN=DATPSADSN")
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT T09Key_Key, T09username, T09usertype, T09Active, T09GroupId FROM TR09 WHERE T09Key_Number = 9 AND T09username LIKE 'MinDay%'"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    $bytes = New-Object byte[] 4
    [void]$reader.GetBytes(0, 0, $bytes, 0, 4)
    $empno = [BitConverter]::ToUInt32($bytes, 0)
    Write-Output ("empno={0}  username=[{1}]  usertype={2}  active={3}  group={4}" -f $empno, $reader.GetValue(1).ToString().Trim(), $reader.GetValue(2), $reader.GetValue(3), $reader.GetValue(4).ToString().Trim())
}
$reader.Close()
$conn.Close()
```
Expected output (order may vary):
```
empno=99901  username=[MinDay - Updated]  usertype=O  active=N  group=DIRECTORS
empno=99902  username=[MinDay - No Update Needed]  usertype=O  active=N  group=DIRECTORS
empno=99903  username=[MinDay - Exception]  usertype=O  active=N  group=DIRECTORS
```

- [ ] **Step 5: Commit**

```bash
cd "D:/data/vs/psamindaycalc"
git add sql/2026-07-15-seed-mindaycalc-tr09-markers.ps1
git commit -m "Add one-time seed script for MinDayCalc TR09 marker identities"
```

---

## Task 2: Add `MarkPMExamined` to `CTPairing`

**Files:**
- Modify: `D:\data\vs\CTDataAccess\CTPairingUpdate.cs` (separate repo — `SFICTDataAccess`, not `psamindaycalc`)

**Interfaces:**
- Consumes: `Connection` (inherited `OleDbConnection` property from
  `CTDataAccesBase`, already used elsewhere in this file, e.g.
  `UpdateDutyCreditsAndPay`'s `Connection.BeginTransaction()`).
- Produces: `public bool MarkPMExamined(String PrgID, String PrgDate, uint MarkerEmpNo)`
  on the `CTPairing` class — Task 4 calls this as `prg.MarkPMExamined(...)`.

- [ ] **Step 1: Add the method**

In `D:\data\vs\CTDataAccess\CTPairingUpdate.cs`, add this method to the
`CTPairing` class (place it after `InsertException`, before
`UpdateDutyCreditsAndPay`, around line 32):

```csharp
public bool MarkPMExamined(String PrgID, String PrgDate, uint MarkerEmpNo)
    {
    using (OleDbCommand cmd = Connection.CreateCommand())
        {
        cmd.CommandText = "UPDATE PM SET Updateid_Updempno = ? " +
            "WHERE Prgid_Prgno_Base=? AND Prgid_Prgno_Eqpt=? AND Prgid_Prgno_3=? AND Prgid_Prgno_4_6=? AND Prgid_Prgdate=?";
        cmd.Parameters.AddWithValue("empno", MarkerEmpNo);
        cmd.Parameters.AddWithValue("base", PrgID.Substring(0,1));
        cmd.Parameters.AddWithValue("eqpt", PrgID.Substring(1,1));
        cmd.Parameters.AddWithValue("d3",   PrgID.Substring(2,1));
        cmd.Parameters.AddWithValue("d46",  PrgID.Substring(3, PrgID.Length - 3));
        cmd.Parameters.AddWithValue("date", PrgDate);
        return cmd.ExecuteNonQuery() == 1;
        }
    }
```

(Same `PrgID.Substring` key-splitting as the existing `UpdatePMActCredit`
call a few lines below — this is the proven-correct pattern for that table's
composite key, not new key-matching logic.)

- [ ] **Step 2: Build the `SFICTDataAccess` project to confirm it compiles**

Run (from a Developer Command Prompt or with `msbuild` on PATH):
```
msbuild "D:\data\vs\CTDataAccess\SFICTDataAccess.csproj" /p:Configuration=Debug
```
Expected: `Build succeeded.`

- [ ] **Step 3: Manually verify the method against a real, low-stakes `PM` row**

Pick an already-fully-processed, non-current pairing to avoid touching
anything financially live — e.g. the oldest row returned by:
```powershell
Add-Type -AssemblyName System.Data
$conn = New-Object System.Data.Odbc.OdbcConnection("DSN=DATPSADSN")
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT TOP 1 Prgid_Prgno_Base+Prgid_Prgno_Eqpt+Prgid_Prgno_3+Prgid_Prgno_4_6 AS PrgNo, Prgid_Prgdate, Updateid_Updempno, Updateid_Upddate, Updateid_Updtime FROM PM ORDER BY Updateid_Upddate ASC"
$reader = $cmd.ExecuteReader()
if ($reader.Read()) {
    Write-Output ("PrgNo={0}  PrgDate={1}  BEFORE: updempno={2} upddate={3} updtime={4}" -f $reader.GetValue(0).Trim(), $reader.GetValue(1), $reader.GetValue(2), $reader.GetValue(3), $reader.GetValue(4))
}
$reader.Close()
$conn.Close()
```
Note the `PrgNo`/`PrgDate`/before-values it prints, then write and run a
small throwaway console harness (or a PowerShell `OleDbCommand` call using
the exact same SQL as the new method) to call the equivalent of
`MarkPMExamined(PrgNo, PrgDate, 99902)` against that one row, then re-run the
same `SELECT` above filtered to that `PrgNo`/`PrgDate` and confirm:
- `Updateid_Updempno` is now `99902`
- `Updateid_Upddate` and `Updateid_Updtime` are **unchanged** from the
  "BEFORE" values noted above

Revert the test row back to its original `Updateid_Updempno` value afterward
(the "BEFORE" value captured above) so this test leaves no trace:
```powershell
$cmd2 = $conn.CreateCommand()
$cmd2.CommandText = "UPDATE PM SET Updateid_Updempno = ? WHERE Prgid_Prgno_Base=? AND Prgid_Prgno_Eqpt=? AND Prgid_Prgno_3=? AND Prgid_Prgno_4_6=? AND Prgid_Prgdate=?"
# fill in the original empno and the same key parts used above
```

- [ ] **Step 4: Commit (in the `SFICTDataAccess` repo)**

```bash
cd "D:/data/vs/CTDataAccess"
git add CTPairingUpdate.cs
git commit -m "Add MarkPMExamined for MinDayCalc PM examination markers"
```

Note: this repo currently has ~194 unrelated lines of uncommitted changes in
`CTDataSet.Designer.cs` (pre-existing, not touched by this task) — stage
only `CTPairingUpdate.cs` explicitly, never `git add -A` or `git add .`.

---

## Task 3: Add `MarkMSExamined` to `CTPairing`

**Files:**
- Modify: `D:\data\vs\CTDataAccess\CTPairingUpdate.cs` (same file as Task 2)

**Interfaces:**
- Consumes: `Connection` (same as Task 2).
- Produces: `public bool MarkMSExamined(uint EmpNum, String BidPeriod, uint MarkerEmpNo)`
  on `CTPairing` — Task 5 calls this as `prg.MarkMSExamined(...)`.

- [ ] **Step 1: Add the method**

Immediately after `MarkPMExamined` (added in Task 2):

```csharp
public bool MarkMSExamined(uint EmpNum, String BidPeriod, uint MarkerEmpNo)
    {
    using (OleDbCommand cmd = Connection.CreateCommand())
        {
        cmd.CommandText = "UPDATE MS SET Updateid_Updempno = ? WHERE Mastid_Empno=? AND Mastid_Bidate=?";
        cmd.Parameters.AddWithValue("empno", MarkerEmpNo);
        cmd.Parameters.AddWithValue("mastid_empno", EmpNum);
        cmd.Parameters.AddWithValue("mastid_bidate", BidPeriod);  // BidPeriodValueMember (YYYYMM)
        return cmd.ExecuteNonQuery() == 1;
        }
    }
```

- [ ] **Step 2: Build to confirm it compiles**

Run: `msbuild "D:\data\vs\CTDataAccess\SFICTDataAccess.csproj" /p:Configuration=Debug`
Expected: `Build succeeded.`

- [ ] **Step 3: Manually verify against a real, low-stakes `MS` row**

Same approach as Task 2 Step 3, but against `MS`:
```powershell
Add-Type -AssemblyName System.Data
$conn = New-Object System.Data.Odbc.OdbcConnection("DSN=DATPSADSN")
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT TOP 1 Mastid_Empno, Mastid_Bidate, Updateid_Updempno, Updateid_Upddate, Updateid_Updtime FROM MS ORDER BY Updateid_Upddate ASC"
$reader = $cmd.ExecuteReader()
if ($reader.Read()) {
    Write-Output ("Empno={0}  Bidate={1}  BEFORE: updempno={2} upddate={3} updtime={4}" -f $reader.GetValue(0), $reader.GetValue(1).ToString().Trim(), $reader.GetValue(2), $reader.GetValue(3), $reader.GetValue(4))
}
$reader.Close()
$conn.Close()
```
Note the before-values, call the equivalent of `MarkMSExamined(Empno, Bidate, 99901)` against that row, re-query and confirm `Updateid_Updempno` changed to `99901` while `Updateid_Upddate`/`Updateid_Updtime` are unchanged, then revert `Updateid_Updempno` back to its original value.

- [ ] **Step 4: Commit (in the `SFICTDataAccess` repo)**

```bash
cd "D:/data/vs/CTDataAccess"
git add CTPairingUpdate.cs
git commit -m "Add MarkMSExamined for MinDayCalc MS examination markers"
```

---

## Task 4: Wire PM markers into `MinDayProcess.ProcessPairing`

**Files:**
- Modify: `D:\data\vs\psamindaycalc\MinDayProcess\MinDayProcess.cs` (this repo)

**Interfaces:**
- Consumes: `CTPairing.MarkPMExamined(String PrgID, String PrgDate, uint MarkerEmpNo)`
  (Task 2).
- Produces: three `public const uint` marker fields on `MinDayProcess`
  (`MINDAY_UPDATED`, `MINDAY_NO_UPDATE_NEEDED`, `MINDAY_EXCEPTION`) — Task 5
  reuses `MINDAY_UPDATED` and `MINDAY_EXCEPTION`.

- [ ] **Step 1: Add the marker constants**

In `MinDayProcess.cs`, immediately after the existing credit constants
(currently lines 56–58):

```csharp
        public Int16 MINDAYCREDIT;
        public const Int16 MINDAYCREDIT35HR = 210;
        public const Int16 MINDAYCREDIT4HR = 240;
        public const uint MINDAY_UPDATED = 99901;
        public const uint MINDAY_NO_UPDATE_NEEDED = 99902;
        public const uint MINDAY_EXCEPTION = 99903;
```

- [ ] **Step 2: Add the `MarkPMExaminedSafely` helper**

Add this private method to the `MinDayProcess` class (place it right after
`ProcessPairing`, before `SumOfLayoverPay`):

```csharp
        private void MarkPMExaminedSafely(PMByTimestamp pmts, uint MarkerEmpNo)
            {
            try {
                prg.MarkPMExamined(pmts.PairingID, pmts.PairingDate, MarkerEmpNo);
                }
            catch (Exception ee)
                {
                String InnerMess = "";
                if (ee.InnerException != null)
                    InnerMess = " / " + ee.InnerException.Message;
                UpdateStatus(MinDayStatus.Critical, "Failed to mark PM examined for " + pmts.PairingID + " " + pmts.PairingDate + " - " + ee.Message + InnerMess);
                }
            }
```

- [ ] **Step 3: Wire the early-return mixed-crew exit point (currently lines 205–212)**

Replace:
```csharp
            // starting on 9/1/2022, mixed pairings no longer allowed
            if (string.Compare(pmts.PairingDate, "20220901") >= 0 && pmts.PilotCount > 0 && pmts.FACount > 0)
                {
                UpdateStatus(MinDayStatus.DetailedInfo, "PX due to mixed pilot/FA crew");
                prg.Assemble(pmts.PairingID, pmts.PairingDate); // must assemble pairing for exception creation
                if (ppAction == PairingProcessAction.EvalupateAndUpdate)
                    CreatePX(pmts);
                return false; // pairing does not need to be evaluated
                }
```
with:
```csharp
            // starting on 9/1/2022, mixed pairings no longer allowed
            if (string.Compare(pmts.PairingDate, "20220901") >= 0 && pmts.PilotCount > 0 && pmts.FACount > 0)
                {
                UpdateStatus(MinDayStatus.DetailedInfo, "PX due to mixed pilot/FA crew");
                prg.Assemble(pmts.PairingID, pmts.PairingDate); // must assemble pairing for exception creation
                if (ppAction == PairingProcessAction.EvalupateAndUpdate)
                    {
                    CreatePX(pmts);
                    MarkPMExaminedSafely(pmts, MINDAY_NO_UPDATE_NEEDED);
                    }
                return false; // pairing does not need to be evaluated
                }
```

- [ ] **Step 4: Add the `ExaminationMarker` local and wire the remaining exit points (currently lines 202–268)**

Replace:
```csharp
            bool Bypassed = false;
```
with:
```csharp
            bool Bypassed = false;
            uint ExaminationMarker = 0;
```

Replace:
```csharp
                if ((AllTrueDuties.Count(z => z.ActCredit < MINDAYCREDIT) == 0 && AllTrueDuties.Count(z => z.ActPay < MINDAYCREDIT) == 0) && iLayoverPay == 0)
                    Bypassed = true;
                else // prg contains in a min day condition
                    {
                    ModDutiesList = CreateModDutiesList(AllTrueDuties);
                    if (ModDutiesList.Count > 0 || iLayoverPay > 0)
                        {
                        if (BypassAndCreatePXIfNeeded(pmts,ppAction) && iLayoverPay == 0)
                            Bypassed = true;
                        if (!Bypassed)
                            {
                            try {
                                if (ppAction == PairingProcessAction.EvalupateAndUpdate)
                                    {
                                    UpdateStatus(MinDayStatus.DetailedInfo, "Update started");
                                    prg.UpdateDutyCreditsAndPay(AllTrueDuties, ModDutiesList, MINDAYCREDIT, LayoverDutyList, pmts.PilotCount, pmts.FACount);
                                    UpdateStatus(MinDayStatus.DetailedInfo, "Update completed");
                                    }
                                }
                            catch (Exception ee)
                                {
                                String InnerMess= "";    
                                if (ee.InnerException != null)
                                    InnerMess = " / " + ee.InnerException.Message;
                                UpdateStatus(MinDayStatus.Critical, "Update aborted for " + pmts.PairingID + " " + pmts.PairingDate + " - " + ee.Message + InnerMess);
                                Bypassed = true;
                                }
                            }
                        }
                    else
                        Bypassed = true;
                //       } 27MAR20
                    }
```
with:
```csharp
                if ((AllTrueDuties.Count(z => z.ActCredit < MINDAYCREDIT) == 0 && AllTrueDuties.Count(z => z.ActPay < MINDAYCREDIT) == 0) && iLayoverPay == 0)
                    {
                    Bypassed = true;
                    ExaminationMarker = MINDAY_NO_UPDATE_NEEDED;
                    }
                else // prg contains in a min day condition
                    {
                    ModDutiesList = CreateModDutiesList(AllTrueDuties);
                    if (ModDutiesList.Count > 0 || iLayoverPay > 0)
                        {
                        if (BypassAndCreatePXIfNeeded(pmts,ppAction) && iLayoverPay == 0)
                            {
                            Bypassed = true;
                            ExaminationMarker = MINDAY_NO_UPDATE_NEEDED;
                            }
                        if (!Bypassed)
                            {
                            try {
                                if (ppAction == PairingProcessAction.EvalupateAndUpdate)
                                    {
                                    UpdateStatus(MinDayStatus.DetailedInfo, "Update started");
                                    prg.UpdateDutyCreditsAndPay(AllTrueDuties, ModDutiesList, MINDAYCREDIT, LayoverDutyList, pmts.PilotCount, pmts.FACount);
                                    UpdateStatus(MinDayStatus.DetailedInfo, "Update completed");
                                    ExaminationMarker = MINDAY_UPDATED;
                                    }
                                }
                            catch (Exception ee)
                                {
                                String InnerMess= "";    
                                if (ee.InnerException != null)
                                    InnerMess = " / " + ee.InnerException.Message;
                                UpdateStatus(MinDayStatus.Critical, "Update aborted for " + pmts.PairingID + " " + pmts.PairingDate + " - " + ee.Message + InnerMess);
                                Bypassed = true;
                                ExaminationMarker = MINDAY_EXCEPTION;
                                }
                            }
                        }
                    else
                        {
                        Bypassed = true;
                        ExaminationMarker = MINDAY_NO_UPDATE_NEEDED;
                        }
                //       } 27MAR20
                    }
```

- [ ] **Step 5: Call the marker at the common tail (currently lines 270–276)**

Replace:
```csharp
            ReadOnlyCollection<int> ModDutiesIntList = new ReadOnlyCollection<int>(ModDutiesList.ConvertAll(x => x.DutyPeriod));
   //         ModDutiesIntList = ModDutiesIntList.Distinct().ToList();

            PairingProcessInfoEventArgs Args = new PairingProcessInfoEventArgs(Bypassed, pmts.PairingID, pmts.PairingDate, ModDutiesIntList);
            OnProcess(Args);
            return !Bypassed ; // true if sked needs to be evaluated
            }
```
with:
```csharp
            if (ppAction == PairingProcessAction.EvalupateAndUpdate && ExaminationMarker != 0)
                MarkPMExaminedSafely(pmts, ExaminationMarker);

            ReadOnlyCollection<int> ModDutiesIntList = new ReadOnlyCollection<int>(ModDutiesList.ConvertAll(x => x.DutyPeriod));
   //         ModDutiesIntList = ModDutiesIntList.Distinct().ToList();

            PairingProcessInfoEventArgs Args = new PairingProcessInfoEventArgs(Bypassed, pmts.PairingID, pmts.PairingDate, ModDutiesIntList);
            OnProcess(Args);
            return !Bypassed ; // true if sked needs to be evaluated
            }
```

- [ ] **Step 6: Build to confirm it compiles**

Run: `msbuild "D:\data\vs\psamindaycalc\MinDayProcess\MinDayProcess.csproj" /p:Configuration=Debug`
Expected: `Build succeeded.`

- [ ] **Step 7: Manual verification — confirm the marker lands correctly on a real bypass case**

Run `MinDayProcess` (via the `PSAMinDay` tray app or `PSAMinDayCalcViewer`)
against a small, known batch (e.g. temporarily point `MinDayProcess.json`'s
`PMAfterDate`/`PMAfterTime` at a narrow recent window — see the earlier
conversation on seeding that file for a bounded run). After it completes:

```powershell
Add-Type -AssemblyName System.Data
$conn = New-Object System.Data.Odbc.OdbcConnection("DSN=DATPSADSN")
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT Updateid_Updempno, COUNT(*) FROM PM WHERE Updateid_Updempno IN (99901,99902,99903) GROUP BY Updateid_Updempno"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) { Write-Output ("empno={0}  count={1}" -f $reader.GetValue(0), $reader.GetValue(1)) }
$reader.Close()
$conn.Close()
```
Expected: at least one row present for whichever markers the test batch's
pairings actually triggered (99902 is the most common case and should show
up in almost any real batch).

- [ ] **Step 8: Confirm no reprocessing loop**

Run the same batch a second time (same `MinDayProcess.json` cursor position
it advanced to after Step 7) and confirm the log shows **0** pairings
processed for the pairings marked in Step 7 — they should not reappear.

- [ ] **Step 9: Commit**

```bash
cd "D:/data/vs/psamindaycalc"
git add MinDayProcess/MinDayProcess.cs
git commit -m "Wire PM examination markers into ProcessPairing"
```

---

## Task 5: Wire MS markers into `MinDayProcess.EvaluateSkeds`

**Files:**
- Modify: `D:\data\vs\psamindaycalc\MinDayProcess\MinDayProcess.cs` (this repo)

**Interfaces:**
- Consumes: `CTPairing.MarkMSExamined(uint EmpNum, String BidPeriod, uint MarkerEmpNo)`
  (Task 3); `MINDAY_UPDATED`, `MINDAY_EXCEPTION` (Task 4, Step 1).
- Produces: nothing new consumed elsewhere — this is the last wiring task.

- [ ] **Step 1: Add the `MarkMSExaminedSafely` helper**

Add this private method to the `MinDayProcess` class, right after
`EvaluateSkeds`:

```csharp
        private void MarkMSExaminedSafely(EvaluateSkedParams ev, uint MarkerEmpNo)
            {
            try {
                prg.MarkMSExamined(ev.EmpNum, ev.BidPeriod.BidPeriodValueMember, MarkerEmpNo);
                }
            catch (Exception ee)
                {
                String InnerMess = "";
                if (ee.InnerException != null)
                    InnerMess = " / " + ee.InnerException.Message;
                UpdateStatus(MinDayStatus.Critical, "Failed to mark MS examined for " + ev.EmpNum + " " + ev.BidPeriod.BidPeriodValueMember + " - " + ee.Message + InnerMess);
                }
            }
```

- [ ] **Step 2: Wire the eval outcomes in `EvaluateSkeds` (currently the `foreach` loop body)**

Replace:
```csharp
                    UpdateStatus(MinDayStatus.Info, "Evaluation started for crewmember: " + EmpNum + " for " + MMMYYBP + " (" + iEvalCount.ToString() + "/" + EvalSkeds.Queue.Count().ToString() + ")");
                    bool bEval = EvalMS.EvaluateSked(EmpNum,MMMYYBP);
                    if (bEval)
                        UpdateStatus(MinDayStatus.Info, "Evaluated crewmember: " + EmpNum + " for " + MMMYYBP);
                    else
                        UpdateStatus(MinDayStatus.Critical, "Error evaluating crewmember: " + EmpNum + " for " + MMMYYBP + "<" + EvalMS.LastErrorMsg + ">");
                    }
                catch (Exception ee)
                    {
                    String InnerMess = "";
                    if (ee.InnerException != null)
                        InnerMess = " / " + ee.InnerException.Message;
                    UpdateStatus(MinDayStatus.Critical, "Exception evaluating crewmember <" + ee.Message + InnerMess + ">");
                    }
                }
```
with:
```csharp
                    UpdateStatus(MinDayStatus.Info, "Evaluation started for crewmember: " + EmpNum + " for " + MMMYYBP + " (" + iEvalCount.ToString() + "/" + EvalSkeds.Queue.Count().ToString() + ")");
                    bool bEval = EvalMS.EvaluateSked(EmpNum,MMMYYBP);
                    if (bEval)
                        {
                        UpdateStatus(MinDayStatus.Info, "Evaluated crewmember: " + EmpNum + " for " + MMMYYBP);
                        MarkMSExaminedSafely(ev, MINDAY_UPDATED);
                        }
                    else
                        {
                        UpdateStatus(MinDayStatus.Critical, "Error evaluating crewmember: " + EmpNum + " for " + MMMYYBP + "<" + EvalMS.LastErrorMsg + ">");
                        MarkMSExaminedSafely(ev, MINDAY_EXCEPTION);
                        }
                    }
                catch (Exception ee)
                    {
                    String InnerMess = "";
                    if (ee.InnerException != null)
                        InnerMess = " / " + ee.InnerException.Message;
                    UpdateStatus(MinDayStatus.Critical, "Exception evaluating crewmember <" + ee.Message + InnerMess + ">");
                    MarkMSExaminedSafely(ev, MINDAY_EXCEPTION);
                    }
                }
```

- [ ] **Step 3: Build to confirm it compiles**

Run: `msbuild "D:\data\vs\psamindaycalc\MinDayProcess\MinDayProcess.csproj" /p:Configuration=Debug`
Expected: `Build succeeded.`

- [ ] **Step 4: Manual verification**

After a run that triggers at least one schedule evaluation (i.e. at least one
`PM` marked `99901` in Task 4's run, which queues its crew for `MS` eval):

```powershell
Add-Type -AssemblyName System.Data
$conn = New-Object System.Data.Odbc.OdbcConnection("DSN=DATPSADSN")
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT Updateid_Updempno, COUNT(*) FROM MS WHERE Updateid_Updempno IN (99901,99903) GROUP BY Updateid_Updempno"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) { Write-Output ("empno={0}  count={1}" -f $reader.GetValue(0), $reader.GetValue(1)) }
$reader.Close()
$conn.Close()
```
Expected: at least one row for `99901` if any schedule evaluation succeeded
during the test run.

- [ ] **Step 5: Commit**

```bash
cd "D:/data/vs/psamindaycalc"
git add MinDayProcess/MinDayProcess.cs
git commit -m "Wire MS examination markers into EvaluateSkeds"
```

---

## Self-Review Notes

- **Spec coverage:** Purpose/Constraints → Global Constraints section above.
  Background/Resolution (never touch Updateid_Upddate/Updateid_Updtime) →
  enforced by construction in `MarkPMExamined`/`MarkMSExamined` (Tasks 2–3,
  the SQL only ever sets `Updateid_Updempno`). Three markers + TR09 rows →
  Task 1. PM call-site mapping + EvaluateOnly exclusion → Task 4. PM
  implementation + error handling → Tasks 2, 4. PM verification → Task 4
  Steps 7–8. MS marking (all subsections) → Tasks 3, 5. Out of scope items
  (DB records, OX, AB, trigger/gating logic, PSAMinDayCalcService, PVA) —
  correctly untouched by every task above.
- **Placeholder scan:** no TBD/TODO; every step has literal code or literal
  commands with expected output.
- **Type consistency:** `MarkPMExamined(String, String, uint)` used
  identically in Task 2 (definition) and Task 4 (call site). `MarkMSExamined(uint, String, uint)`
  used identically in Task 3 (definition) and Task 5 (call site). Marker
  constants (`MINDAY_UPDATED` etc.) defined once in Task 4 Step 1, consumed
  by both Task 4 and Task 5 without redefinition.
