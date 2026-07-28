# Reasons a Trip Does Not Receive Min Day (as processed in `MinDayProcess`)

Reference: `D:\Data\VS\PSAMinDayCalc\MinDayProcess\MinDayProcess.cs`, primarily
`ProcessPairing(string prgID, string prgDate, PairingProcessAction ppAction)`,
`CreateModDutiesList`, and `BypassAndCreatePXIfNeeded`. Line numbers below refer
to the file's state as of 2026-07-28.

Applies to both the scheduled batch path (`ProcessPM` → `FillByLatestUpdate`)
and the on-demand single-pairing path (`ProcessSinglePairing`, called from
PairingInspect's "Recalculate Min Day" button) — both funnel into the same
shared `ProcessPairing(string, string, ppAction)` logic.

## Excluded before any credit/pay evaluation happens

1. **Single-duty pairing** (`NumDutyPeriods == 1`) — interim rule pending
   customer confirmation; marked "not applicable" outright, regardless of
   credit/pay (`:253-259`).
2. **Mixed pilot/FA crew on a post-9/1/2022 pairing** (`CrewType == "B"`) — no
   longer allowed under contract; a PX exception is created instead, marked
   "not applicable" (`:262-271`).
3. *(Batch scan only, not on-demand)* — `FillByPSAMinCredit`'s SQL pre-filter
   (`(actcdt_Domtime+actcdt_IntTime<210 and numdp=1) or numdp>1`) never even
   fetches certain single-duty pairings for the scheduled batch to see. Now
   moot in practice since #1 excludes all single-duty trips anyway, but it's a
   separate, pre-existing gate specific to the batch path, and it hardcodes
   210 without accounting for the 240/pilot threshold.

## No genuine min-day condition exists

4. **Every duty's credit and pay are already at/above the threshold, and no
   multiday layover pay applies** — threshold is 210 (FA-only or
   pre-9/1/2022) or 240 (pilot-only, post-9/1/2022) (`:285-289`).

## Filtered out while building the list of duties to fix (`CreateModDutiesList`)

5. **Duty has an excludeable code** — any duty where a leg carries one of the
   codes configured in `SFI.config`'s `ExcludeableCodes` setting is dropped
   from consideration entirely.
6. **Fake-deadhead leg on a single-effective-duty trip** —
   `DeadheadCode == 'K'` on any leg of a trip that (after excludeable-code
   filtering) is down to one duty.
7. **"Old rules" noon/5pm exemption** (only at the 210 threshold — never at
   240): a single-effective-duty trip reporting after 12:00 noon local with
   all end-times before 17:00; or on a multi-duty trip, duty #1 reporting
   at/after noon, or the *last* duty releasing at/before 17:00 — each gets
   skipped individually. **Open question, not yet resolved**: this is gated
   only by `MINDAYCREDIT == MINDAYCREDIT35HR` (a threshold *value* check), not
   by the pairing's actual date, so it currently also fires on modern
   FA-only pairings, not just pre-contract-change trips as the "(old rules
   only)" comment implies.
8. **Duty's own credit and pay are both already ≥ threshold** —
   `AddDutyPeriodToListIfNeeded` only flags a duty if its individual credit or
   pay is under threshold.

## Bypassed instead of updated, based on crew status (`BypassAndCreatePXIfNeeded`)

9. **All assigned crew already have an excuse** — every crew member with an
   absence code, or `AssignCode` RAS/TTA (at 210) / RAS (at 240) — bypassed as
   "not applicable," no PX.
10. **Mixed clean/excused crew** — some crew have ab/RAS/TTA (210) or RAS
    (240) and others don't — a PX is created instead, and the pairing is
    marked "not applicable" rather than updated.

## After a condition is found

11. **Nothing survives the filtering above and no layover pay** —
    `ModDutiesList` ends up empty with `iLayoverPay == 0` → bypassed as "not
    applicable" (`:324-328`).
12. **The actual DB write throws** — `UpdateDutyCreditsAndPay` fails; marked
    `MINDAY_EXCEPTION`, nothing written (`:313-321`).
13. **Caller passes `PairingProcessAction.EvaluateOnly`** — even when a real
    condition is found, the write and the PM marker update are both skipped;
    it's a dry-run mode, not a data-driven exclusion.

## Marker reference

| Constant | SFI.config key | Value |
|---|---|---|
| `MINDAY_UPDATED` | `MinDayMarkerUpdated` | 99901 |
| `MINDAY_NO_UPDATE_NEEDED` | `MinDayMarkerNoUpdateNeeded` | 99902 |
| `MINDAY_EXCEPTION` | `MinDayMarkerException` | 99903 |

`99999` is not one of the above — it's the userid of the automated
scheduler/CrewPost process that touches pairings as actual flight times post;
data-driven, not a MinDayProcess-defined sentinel.
