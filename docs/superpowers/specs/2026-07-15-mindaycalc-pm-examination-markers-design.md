# MinDayCalc PM Examination Markers — Design

## Purpose

`MinDayProcess` (the min-day/minimum-day-guarantee calculation engine) currently
gives no durable, queryable signal for whether a given `PM` (pairing master)
record has ever been examined by the min-day calculator, or what it concluded.
When a pairing needs a credit/pay correction, `UpdateDutyCreditsAndPay` writes
the new values — but when nothing needs to change (the common case), or when an
update is attempted and fails, **nothing is written anywhere**. There's no way
to look at a `PM` record today and answer "has MinDayCalc looked at this, and
what did it decide?"

This design adds that signal by reusing the existing `PM.Updateid_Updempno`
field as a marker, stamped with one of three new synthetic "system user"
identities depending on what `MinDayProcess.ProcessPairing` concluded.

## Constraints

- **No database schema changes.** No new tables, no new fields. Every field
  used already exists in the live `PM` and `TR09` tables.
- **Must not re-trigger MinDayCalc's own change-detection poll.** See
  Background below — this drove the entire mechanism design.

## Background: why this is trickier than "just write a value"

Two things were confirmed by reading the live `SFICTDataAccess` source
(`D:\data\vs\CTDataAccess`, not part of this repo) and the live Pervasive/Actian
database directly:

1. **`CTPMTimestamps.FillByLatestUpdate`** (the query that finds pairings
   changed since MinDayCalc's last run) reads its cursor straight off
   `PM.Updateid_Upddate` / `PM.Updateid_Updtime`:
   ```sql
   where pm.Cancel<>'C' and
   ((actcdt_Domtime + actcdt_IntTime < 210 and numdp = 1) or numdp > 1) and
   ((pm.updateid_upddate > ?) or (pm.updateid_upddate = ? and pm.updateid_updtime > ?))
   ```
   If MinDayCalc's own marker write advanced those two fields, the pairing
   would look "newer than the cursor" and get pulled right back into the next
   poll — an unbounded reprocessing loop (examine → mark → look newer → get
   re-picked-up → examine → mark → ...).

2. **Today, MinDayCalc's real writes (`UpdateDutyCreditsAndPay` →
   `UpdateActCredit` / `UpdatePMActCredit` / `UpdatePMActCreditAndTripRig` /
   `UpdatePMActCreditCO`) never touch `Updateid_*` at all.** So the general
   premise "whenever a PM is updated, `Updateid_Updempno` records who did it"
   is true for other systems that touch `PM` (interactive scheduling edits,
   FAM processing, etc.) but was never true for MinDayCalc's own writes.

**Resolution:** stamp `PM.Updateid_Updempno` only. Never touch
`Updateid_Upddate` / `Updateid_Updtime`. Since the poll only compares the
date/time fields, a pairing whose `updempno` alone changes will never look
"newer" and will never be re-fetched. This keeps the whole mechanism
side-effect-free with respect to the poll, with no changes needed to the
shared `FillByPSAMinCredit` query.

Separately noted, out of scope for this work: the poll's pre-filter
`actcdt_Domtime + actcdt_IntTime < 210 and numdp = 1` is hardcoded to the old
3:30 (210-minute) threshold. For single-duty-period pilot-only pairings under
the newer 4:00/240-minute rule (post 9/1/2022), a pairing with credit between
210–239 minutes is never even fetched by this query, so `MinDayProcess` never
sees it. This is a pre-existing correctness gap unrelated to the marker
mechanism and is not addressed here.

## The three markers

| EmpNo | `T09username` | Meaning |
|---|---|---|
| 99901 | `MinDay - Updated` | `UpdateDutyCreditsAndPay` succeeded — a real credit/pay adjustment was applied. |
| 99902 | `MinDay - No Update Needed` | Examined; no min-day condition existed, **or** a PX was created for mixed-crew instead of a direct update. No credit/pay change was made either way. |
| 99903 | `MinDay - Exception` | An update was attempted but threw an exception (caught, logged Critical). Needs human follow-up — distinct from a clean "nothing to do." |

### New `TR09` rows (one-time data insert, not a schema change)

All three share the same shape, differing only in key/username:

| Field | Value |
|---|---|
| `T09Key_Number` | 9 |
| `T09Key_Key` | packed binary(EmpNo) — 4-byte little-endian unsigned int + 6 bytes blank padding, matching the existing encoding for all `TR09` rows |
| `T09pswd` | blank |
| `T09secLevel1` / `T09secLevel2` | 0 / blank |
| `T09secCrewtype` | `B` |
| `T09username` | see table above |
| `T09usertype` | `O` (Office) |
| `T09Active` | `N` (not a real signon account) |
| `T09GroupId` | `DIRECTORS` (existing registered `TR23` group; reused as-is rather than registering a new group) |

These are inserted once via a setup script, run against production before the
code change ships. Not part of `MinDayProcess`'s runtime behavior.

## Call-site mapping (`MinDayProcess.ProcessPairing`)

Six exit points in the existing control flow collapse into the three markers:

| Exit point (existing code) | Marker |
|---|---|
| Mixed pilot/FA crew, post 9/1/2022 — PX created, early `return false` | 99902 |
| No min-day condition at all (`AllTrueDuties` already meet threshold, no layover pay) | 99902 |
| `BypassAndCreatePXIfNeeded` returns true (mixed ab/RAS/TTA → PX, or all-crew ab/RAS/TTA → pure bypass, no PX) | 99902 |
| `ModDutiesList` empty and no layover pay (defensive fallback branch) | 99902 |
| `UpdateDutyCreditsAndPay` succeeds | 99901 |
| `UpdateDutyCreditsAndPay` throws, caught | 99903 |

**`EvaluateOnly` exclusion:** `ProcessPairing` takes a `PairingProcessAction`
(`EvalupateAndUpdate` or `EvaluateOnly`). In `EvaluateOnly` mode the existing
code already skips both `CreatePX` and `UpdateDutyCreditsAndPay` — a
look-but-don't-touch dry-run mode. Marker writes only happen when
`ppAction == EvalupateAndUpdate`; `EvaluateOnly` never mutates `PM`, consistent
with what that mode already promises.

## Implementation

### `CTPairing.MarkPMExamined` (new method, `CTPairingUpdate.cs`)

A hand-written `OleDbCommand`, not a new typed `CTDataSet.xsd` query (avoids
touching the fragile VS-designer-generated dataset for a single-field update):

```csharp
public bool MarkPMExamined(String PrgID, String PrgDate, uint MarkerEmpNo)
{
    try {
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
    catch (Exception) { throw; }  // caller decides how to log/swallow
}
```

Same `PrgID.Substring` key-splitting already used and proven correct in the
existing `UpdatePMActCredit` call — no new key-matching logic invented.

### Wiring into `ProcessPairing`

- **99901 ("Updated")**: piggybacked as an extra `SET Updateid_Updempno=?`
  clause directly onto the existing `UpdatePMActCredit` /
  `UpdatePMActCreditAndTripRig` UPDATE statements in
  `UpdateDutyCreditsAndPay` — same transaction, atomic with the real
  credit/pay write. If the credit/pay update commits, the marker commits with
  it; if it rolls back, no marker is left behind (the exception path below
  handles that pairing instead).
- **99902 ("No Update Needed")** and **99903 ("Exception")**: standalone
  follow-up calls to `MarkPMExamined`, each wrapped in its own try/catch.

### Error handling

Every `MarkPMExamined` call site is wrapped in try/catch. A failure to write
the marker is logged via `UpdateStatus(MinDayStatus.Critical, ...)` and
processing continues to the next pairing — it never aborts the batch and never
throws back out of `ProcessPairing`. This matches the existing defensive style
throughout `MinDayProcess.cs` (broad catches, log Critical, keep going).

## Verification

This codebase has no automated test suite; validation is manual:

1. `SELECT Updateid_Updempno, COUNT(*) FROM PM WHERE Updateid_Updempno IN (99901,99902,99903) GROUP BY Updateid_Updempno`
   — confirm markers accumulate as expected during/after a run.
2. Over a couple of live poll cycles, confirm a stamped pairing does **not**
   reappear in `FillByPSAMinCredit`'s result set — the empirical check that
   "don't touch upddate/updtime" holds in practice, not just on paper.

## Out of scope

- The `FillByPSAMinCredit` 210-minute hardcoded pre-filter gap for single-duty
  pilot-only pairings under the 240-minute rule (noted above, pre-existing).
- Any change to `DB` (duty record) `UPDATEID` fields — this design only
  touches `PM.Updateid_Updempno`.
- `PSAMinDayCalcService` (the orphaned, unbuildable Windows Service project),
  `PVA` (unfinished pairing-view app), and the zero-test-coverage state of the
  solution generally — all pre-existing, unrelated to this work.
