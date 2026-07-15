# MinDayCalc PM & MS Examination Markers — Design

## Purpose

`MinDayProcess` (the min-day/minimum-day-guarantee calculation engine) currently
gives no durable, queryable signal for whether a given `PM` (pairing master) or
`MS` (master schedule summary) record has ever been examined by the min-day
calculator, or what it concluded. When a pairing needs a credit/pay correction,
`UpdateDutyCreditsAndPay` writes the new values — but when nothing needs to
change (the common case), or when an update is attempted and fails, **nothing
is written anywhere**. The same is true downstream: when a crewmember's
schedule (`MS`) is re-evaluated as a consequence of a pairing's min-day update,
success or failure of that evaluation leaves no trace either. There's no way to
look at a `PM` or `MS` record today and answer "has MinDayCalc looked at this,
and what did it decide?"

This design adds that signal by reusing each table's existing
`Updateid_Updempno` field as a marker, stamped with one of three new synthetic
"system user" identities depending on what `MinDayProcess` concluded — for
`PM`, based on `ProcessPairing`'s outcome; for `MS`, based on
`EvaluateSkeds`'/`EvalMS.EvaluateSked`'s outcome.

## Constraints

- **No database schema changes.** No new tables, no new fields. Every field
  used already exists in the live `PM`, `MS`, and `TR09` tables.
- **Must not re-trigger MinDayCalc's own change-detection poll.** See
  Background below — this drove the entire mechanism design, and applies
  identically to `PM` and `MS`.

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

3. **`MS` has the identical risk.** `CTMSTimestamps.FillAfter` (used by any
   consumer polling for changed schedules, though not by `MinDayProcess`
   itself — see MS Marking below) reads its cursor the same way:
   ```sql
   from ms
   where ((ms.updateid_upddate > ?) or (ms.updateid_upddate = ? and ms.updateid_updtime > ?))
   ```
   Confirmed live: `MS` lives in the same `DATPSA` database as `PM`/`TR09`, so
   the same resolution applies there too.

**Resolution:** stamp `Updateid_Updempno` only, on both `PM` and `MS`. Never
touch `Updateid_Upddate` / `Updateid_Updtime` on either table. Since both polls
only compare the date/time fields, a record whose `updempno` alone changes
will never look "newer" and will never be re-fetched. This keeps the whole
mechanism side-effect-free with respect to both polls, with no changes needed
to the shared `FillByPSAMinCredit` or `FillAfter` queries.

Separately noted, out of scope for this work: the poll's pre-filter
`actcdt_Domtime + actcdt_IntTime < 210 and numdp = 1` is hardcoded to the old
3:30 (210-minute) threshold. For single-duty-period pilot-only pairings under
the newer 4:00/240-minute rule (post 9/1/2022), a pairing with credit between
210–239 minutes is never even fetched by this query, so `MinDayProcess` never
sees it. This is a pre-existing correctness gap unrelated to the marker
mechanism and is not addressed here.

## The three markers

The same three synthetic identities are reused across both `PM` and `MS` —
no separate marker set for schedule evaluation.

| EmpNo | `T09username` | Meaning | Used on |
|---|---|---|---|
| 99901 | `MinDay - Updated` | `PM`: `UpdateDutyCreditsAndPay` succeeded — a real credit/pay adjustment was applied. `MS`: `EvalMS.EvaluateSked` succeeded. | `PM`, `MS` |
| 99902 | `MinDay - No Update Needed` | Examined; no min-day condition existed, **or** a PX was created for mixed-crew instead of a direct update. No credit/pay change was made either way. | `PM` only — never applies to `MS` (see MS Marking below) |
| 99903 | `MinDay - Exception` | An update/evaluation was attempted but threw or failed (caught, logged Critical). Needs human follow-up — distinct from a clean "nothing to do." | `PM`, `MS` |

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

## PM marking: call-site mapping (`MinDayProcess.ProcessPairing`)

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

## PM marking: implementation

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

All three markers are wired uniformly, as standalone follow-up calls to
`MarkPMExamined`, each wrapped in its own try/catch:

- **99901 ("Updated")**: called immediately after `UpdateDutyCreditsAndPay`
  returns successfully.
- **99902 ("No Update Needed")** and **99903 ("Exception")**: called at their
  respective bypass/catch exit points.

**Correction from the original design pass:** 99901 was originally planned to
be piggybacked as an extra `SET Updateid_Updempno=?` clause directly onto the
existing `UpdatePMActCredit` / `UpdatePMActCreditAndTripRig` UPDATE statements
(same transaction, atomic with the credit/pay write). That would require
hand-editing the auto-generated `CTDataSet.xsd` / `CTDataSet.Designer.cs` in
the separate `SFICTDataAccess` repo (`D:\data\vs\CTDataAccess`) to add the new
parameter — the exact fragile generated file this design otherwise avoids
touching. That repo also currently has ~194 lines of unrelated, uncommitted
changes sitting in `CTDataSet.Designer.cs`, making hand-edits there riskier.
Decision: use the same standalone `MarkPMExamined` call for 99901 as the other
two markers. Trade-off: the credit/pay update and the 99901 stamp become two
separate writes instead of one atomic transaction — if the process dies in the
narrow window between them, the pairing would have updated financials but no
99901 stamp yet. Acceptable given this codebase's existing error-handling
style (broad catch, log Critical, keep going), and strictly better than
today's behavior (no marker at all, ever, in that scenario).

### Error handling

Every `MarkPMExamined` call site is wrapped in try/catch. A failure to write
the marker is logged via `UpdateStatus(MinDayStatus.Critical, ...)` and
processing continues to the next pairing — it never aborts the batch and never
throws back out of `ProcessPairing`. This matches the existing defensive style
throughout `MinDayProcess.cs` (broad catches, log Critical, keep going).

## PM marking: verification

This codebase has no automated test suite; validation is manual:

1. `SELECT Updateid_Updempno, COUNT(*) FROM PM WHERE Updateid_Updempno IN (99901,99902,99903) GROUP BY Updateid_Updempno`
   — confirm markers accumulate as expected during/after a run.
2. Over a couple of live poll cycles, confirm a stamped pairing does **not**
   reappear in `FillByPSAMinCredit`'s result set — the empirical check that
   "don't touch upddate/updtime" holds in practice, not just on paper.

## MS marking

`EvaluateSkeds()` is only ever invoked downstream of a confirmed `PM` update:
`EvalSkeds.Queue` is populated by `AddCrewToEvalList`, which `ProcessPM` only
calls when `ProcessPairing` returns `true` — i.e. a real 99901 min-day update
already happened upstream. So there's no `MS`-level analog to `PM`'s "examined,
no update needed": that determination already lives in the `PM` markers. This
is existing behavior, unchanged by this design — only the marking is new.

Within `EvaluateSkeds()`, the existing bypass check
(`ctmss.FindEmpBP(...) != 1` — "no schedule for crewmember") means no `MS`
record exists yet for that emp/bid-period, so there is nothing to stamp; it
stays unmarked, which is correct.

### `MS` key shape

Confirmed live against the `MS` table: keyed on `Mastid_Empno` (unsigned long)
+ `Mastid_Bidate` (`YYYYMM`) — the same two fields `ctmss.FindEmpBP` already
matches on.

### `CTPairing.MarkMSExamined` (new method)

```csharp
public bool MarkMSExamined(uint EmpNum, String BidPeriod, uint MarkerEmpNo)
{
    try {
        using (OleDbCommand cmd = Connection.CreateCommand())
        {
            cmd.CommandText = "UPDATE MS SET Updateid_Updempno = ? WHERE Mastid_Empno=? AND Mastid_Bidate=?";
            cmd.Parameters.AddWithValue("empno", MarkerEmpNo);
            cmd.Parameters.AddWithValue("mastid_empno", EmpNum);
            cmd.Parameters.AddWithValue("mastid_bidate", BidPeriod);  // BidPeriodValueMember (YYYYMM)
            return cmd.ExecuteNonQuery() == 1;
        }
    }
    catch (Exception) { throw; }  // caller decides how to log/swallow
}
```

### Call-site mapping (`MinDayProcess.EvaluateSkeds`)

| Existing code path | Marker |
|---|---|
| `EvalMS.EvaluateSked(...)` returns `true` | 99901 |
| `EvalMS.EvaluateSked(...)` returns `false` (logged via `EvalMS.LastErrorMsg`) | 99903 |
| Outer `catch (Exception ee)` around the eval loop body | 99903 |
| `ctmss.FindEmpBP(...) != 1` ("Bypassing - no schedule for crewmember") | *(no stamp — no `MS` record exists)* |

### Error handling

Same policy as `PM`: every `MarkMSExamined` call is wrapped in its own
try/catch. A failure to write the marker is logged via
`UpdateStatus(MinDayStatus.Critical, ...)` and processing continues to the
next queued crewmember — never aborts the batch, never throws back out of
`EvaluateSkeds`.

### MS marking: verification

1. `SELECT Updateid_Updempno, COUNT(*) FROM MS WHERE Updateid_Updempno IN (99901,99903) GROUP BY Updateid_Updempno`
   — confirm markers accumulate as expected.
2. Confirm a stamped `MS` record does not reappear in `CTMSTimestamps.FillAfter`
   results (the same empirical check as `PM`, for any consumer that polls `MS`
   by timestamp — `MinDayProcess` itself doesn't use this query today, but the
   guarantee should hold regardless of who does).

## Out of scope

- The `FillByPSAMinCredit` 210-minute hardcoded pre-filter gap for single-duty
  pilot-only pairings under the 240-minute rule (noted above, pre-existing).
- Any change to `DB` (duty record) `UPDATEID` fields — this design only
  touches `PM.Updateid_Updempno` and `MS.Updateid_Updempno`.
- Any change to the trigger/gating logic for when a schedule gets queued for
  re-evaluation (`AddCrewToEvalList`) — this design only adds marking on top
  of that existing, unchanged behavior.
- `PSAMinDayCalcService` (the orphaned, unbuildable Windows Service project),
  `PVA` (unfinished pairing-view app), and the zero-test-coverage state of the
  solution generally — all pre-existing, unrelated to this work.
