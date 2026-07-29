# Implementation Outline: On-Demand Single-Pairing Min-Day Calculation (Option 1)

**Decision:** Pursue Option 1 from `mindaycalc-single-pairing-options.md` — a new public
single-pairing entry point in `MinDayProcess`, called in-process from PairingInspect. The
read-only boundary concern is accepted: `CTSecurityProfile` will enforce any relevant
security on the write itself (pending tests once implemented). Since `MinDayProcess.dll`
is shared by `PSAMinDay` (tray app) and `PSAMinDayCalcService` (Windows service), any
change to it ships to all three consumers simultaneously the next time each is rebuilt —
the additive new method is zero-risk to the other two; the one refactor described below
touches code they already depend on and needs care.

## 1. New entry point in `MinDayProcess`

Add: `public bool ProcessSinglePairing(string prgNo, string prgDate)`
(`D:\data\vs\PSAMinDayCalc\MinDayProcess\MinDayProcess.cs`).

**Why it's not a one-line wrapper around the existing `ProcessPairing`:**

- `ProcessPairing(PMByTimestamp pmts, PairingProcessAction ppAction)` is `private` and its
  helper methods (`BypassAndCreatePXIfNeeded`, the mixed ab/RAS/TTA crew checks) don't only
  use the one `pmts` row passed in — they query the class-level `pmtss.List` filtered by
  `PairingID`/`PairingDate`, because a single pairing can have multiple `PMByTimestamp` rows
  (one per assigned crew position). `pmtss.List` today is only ever populated by
  `CTPMTimestamps.FillByLatestUpdate(date, time)` — a time-window query, not a
  fetch-by-pairing query. The new entry point must fully populate `pmtss.List` with *every*
  PM row for the target pairing before calling `ProcessPairing`, not synthesize a single row.

- **Correctness issue found in the existing code:** `ProcessPairing` unconditionally calls
  `SavePMUserSettings(pmts.Update_Date, pmts.Update_Time)` at its top whenever
  `ppAction == EvalupateAndUpdate` — that's what advances and persists the batch cursor
  (`MinDayProcess.json`). Calling `ProcessPairing` as-is from the new entry point would let
  an on-demand click **silently move the scheduled batch's cursor forward**, potentially
  causing the next tray-app/service run to skip other pairings modified in between. Fix:
  split `ProcessPairing` so the cursor-advance is the *caller's* responsibility — only
  `ProcessPM` (the batch path) should still call `SavePMUserSettings`. The on-demand path
  must never touch `MinDayProcess.json`.

**Open design question:** the batch path also runs `AddCrewToEvalList` +
`EvaluateSkeds()` afterward (recomputes affected crew's monthly schedule stats). Decide
whether the on-demand path should do this too (more correct, but adds real runtime and
touches `EvalMS`/`ctmss`) or defer it to the next scheduled batch pass (simpler — "just fix
this one pairing's PM row now").

## 2. New data-access query

`CTPMTimestamps` (`D:\data\vs\CTDataAccess\CTPMTimestamp.cs`) needs a
`FillByPairing(prgNo, prgDate)` counterpart to its existing `FillByLatestUpdate(date, time)`
— same shape (a new TableAdapter query against the same `PMByTimestamp` source, filtered by
pairing instead of by update-timestamp window). This lives in the `SFICTDataAccess` repo,
separate from `MinDayProcess`.

## 3. PairingInspect-side wiring

- Reference `MinDayProcess.csproj`/`MinDayProcess.dll` from `PairingInspect.csproj`.
- `btnRecalculateMinDay_Click`: construct `new MinDayProcess()` (non-trivial constructor —
  security profile, bid periods, config — wrap in try/catch, surface failures via
  `RadMessageBox`, same pattern as `LaunchCtwpm`), call
  `ProcessSinglePairing(prg.PrgHdr.PrgID, prg.PrgHdr.PrgDate)`, then re-run the existing
  lookup (`DisplayHeader`/`PopulateGrid`) so the button/status/grid immediately reflect the
  new marker and credit values without the user re-typing the pairing.
- Confirm before firing — this is now a genuine write, one click away from a real DB
  update, so a `RadMessageBox` confirmation makes sense.

## 4. Prerequisite fix: `CTSecurityProfile.bin` path

`CTSecurity.GetSavedSecurityProfile()` (`D:\data\vs\SFIConfigUtils\CTSecurity.cs:114`) opens
`"CTSecurityProfile.bin"` as a **relative** path — works today only because
`PSAMinDay.exe`/the service run with `D:\SFI\EXE` as their working directory. PairingInspect
runs from `D:\data\vs\psamindaycalc\...\bin\Debug`, so this needs either an overload taking
an explicit directory, or config-driven resolution (same pattern already used for
`ctwpm.exe`'s path via `CTEXEDIR`). This has to be sorted out before `new MinDayProcess()`
will even construct successfully from PairingInspect.

## 5. Shared-DLL risk and testing

- The new `ProcessSinglePairing` method is purely additive — zero risk to `PSAMinDay`/
  `PSAMinDayCalcService`, since they'll simply never call it.
- The *refactor* of `ProcessPairing` (splitting out the cursor-advance) is the one change
  that touches code the other two consumers already depend on. It needs to leave the batch
  path's behavior provably identical — verify against the existing "Credit/Pay Discrepancy
  Test" regression script already in this repo before either consumer picks up the new DLL
  build.

## Suggested build order

1. `CTPMTimestamps.FillByPairing` (data access) — small, isolated, testable on its own.
2. `MinDayProcess` refactor (extract cursor-advance out of `ProcessPairing`) + new
   `ProcessSinglePairing` — verify the batch path is unaffected via the regression script.
3. `CTSecurityProfile.bin` path fix — needed before PairingInspect can construct
   `MinDayProcess` at all.
4. PairingInspect wiring (`btnRecalculateMinDay_Click`) — the highest-visibility piece, but
   depends on all three above.
