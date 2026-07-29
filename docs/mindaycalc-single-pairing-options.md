# Min-Day Calculator Integration Options for PairingInspect

**Context:** PairingInspect's "Recalculate Min Day" / "Calculate Min Day" button needs to
actually trigger a min-day recalculation for the one pairing on screen. This document
captures what exists today and three ways to wire the button up.

## What exists today (research findings)

- **`MinDayProcess`** (`D:\data\vs\PSAMinDayCalc\MinDayProcess\MinDayProcess.cs`, namespace
  `MinDayProcessNS`, built to `MinDayProcess.dll`) has **no public, single-pairing entry
  point**. `ProcessPairing(PMByTimestamp pmts, PairingProcessAction ppAction)` is `private`
  and takes a pre-fetched row, not a bare `PrgNo`/`PrgDate`. The only public API is
  batch/cursor-driven: `Process()` loads a JSON cursor (`MinDayProcess.json`:
  `PMAfterDate/Time`, `ABAfterDate/Time`), calls `ProcessPM()`/`ProcessAB()` — which query
  **everything** modified after that stored timestamp, not a specific pairing — then
  advances and re-persists the cursor.
- The constructor requires a full environment: `CTSecurity.GetSavedSecurityProfile()`
  (throws if there's no saved security session), `SFIConfigUtils.AssemblyConfig` app
  settings, and `CTBidPeriods.Fill("1001")`. It's not usable standalone without that
  context, and it's unclear whether `GetSavedSecurityProfile()` behaves the same for an
  interactive desktop session as it does for a scheduled service/tray app.
- **`PSAMinDay.exe`** (`D:\data\vs\PSAMinDayCalc\PSAMinDay`) is a headless
  `ApplicationContext` tray app: a `Timer` fires `doProcess()` → `mdp.Process()` on an
  interval. `Program.cs.Main()` takes no arguments. `DetailsForm` is a scrollback log
  viewer, not a pairing-input screen. It calls `MinDayProcess` directly (no CLI parsing,
  no dialog to automate past the way `ctwpm.exe`'s selection screen was).
- **`PSAMinDayCalcService`** (a Windows service) is a second, separate scheduled consumer
  of the same `MinDayProcess` library — also out of scope, also batch-driven.
- No other tool in this codebase launches `PSAMinDay.exe` with arguments the way
  `PairingInspect/CtwpmSelectionAutomator.cs` launches `ctwpm.exe` (`<FUNCTION> <PrgNo>
  <PrgDate>` + automate past the selection dialog). There's no existing precedent for
  triggering a single pairing on demand, at any layer.
- PairingInspect's original design spec explicitly scoped it as **read-only** — it must
  never call `UpdateDutyCreditsAndPay` / `MarkPMExamined` / `MarkMSExamined`, and today it
  only has a hand-mirrored, read-only copy of the floor/marker logic
  (`MinDayDiffCalculator.cs`), not a real reference to `MinDayProcess`.

**Bottom line:** this isn't "wire up the existing thing" — every option below requires
writing genuinely new code in `MinDayProcess` and/or `PSAMinDay`, because a single-pairing
path doesn't exist anywhere today.

## Option 1 — New public single-pairing method in `MinDayProcess`, called in-process

Add e.g. `ProcessSinglePairing(prgNo, prgDate)` to `MinDayProcess` that fetches the one
`PMByTimestamp` row and calls the existing private `ProcessPairing` core directly.
PairingInspect references `MinDayProcess.dll` and calls it.

**Pros**
- Most direct — no external process, no polling for a result.
- Reuses the real floor/marker logic instead of `MinDayDiffCalculator`'s hand-mirrored copy.

**Cons**
- Explicitly breaks PairingInspect's declared read-only boundary — this is a scope change
  to the tool's core design principle, not just an implementation detail.
- Requires PairingInspect to satisfy `MinDayProcess`'s constructor prerequisites
  (`CTSecurity.GetSavedSecurityProfile()`, `CTBidPeriods.Fill`, config) — none of which it
  currently sets up, and it's unverified whether the security profile call behaves
  correctly outside a service/tray-app context.
- New code lands in a shared library also relied on by the tray app and Windows service —
  any mistake in the new single-pairing path risks a library used by unattended,
  scheduled jobs.

## Option 2 — New CLI mode on `PSAMinDay.exe` (or a small new executable), launched as a separate process

PairingInspect launches a process (à la `ctwpm.exe`) with the pairing's `PrgNo`/`PrgDate`;
that process does the one-pairing calculation and exits.

**Pros**
- PairingInspect itself stays literally read-only — it only launches a sanctioned external
  process; the mutation lives in a tool already responsible for this job.
- Consistent with the existing `CtwpmSelectionAutomator` precedent already in this codebase.

**Cons**
- `PSAMinDay` has no dialog to click-automate the way CTWPM's selection screen does — the
  new mode would need to run fully headless, so PairingInspect needs another way to learn
  whether it succeeded (exit code, tailing a log file, re-querying the marker after the
  process exits).
- Still requires writing the same new single-pairing logic somewhere (inside
  `MinDayProcess` or duplicated into `PSAMinDay`) — this doesn't avoid that work, it only
  relocates *where* the mutation happens, not *whether* new mutating code gets written.

## Option 3 — No bespoke on-demand path; just shrink the batch latency

Keep everything batch/cursor-driven. "Calculate Min Day" lowers the tray app's
`TimerInterval` (or nudges its cursor) so the pairing gets picked up on the next
(near-immediate) pass instead of getting its own code path.

**Pros**
- Zero new methods in `MinDayProcess` — reuses only well-tested, already-shipped batch logic.

**Cons**
- Not really "click and see the result now," which is what the button implies.
- `ProcessPM`/`ProcessAB` pull *everything* modified since the cursor, not one target
  pairing — there's no clean way to scope this to just the pairing on screen without the
  same kind of new-method work Options 1/2 require, and rewinding the cursor risks
  reprocessing other already-settled pairings as a side effect.

## Recommendation

Options 1 and 2 are the real contenders — Option 3 doesn't deliver what the button implies.
The deciding factor is whether PairingInspect is allowed to perform the mutation itself
(Option 1 — simpler, but crosses the exact boundary the original spec drew a hard line
against) versus keeping that boundary intact by delegating to a separate process (Option 2
— more consistent with the CTWPM precedent, but needs a way to report results back without
a UI to watch).
