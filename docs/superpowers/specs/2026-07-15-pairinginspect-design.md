# PairingInspect — Design

## Purpose

A new, interactive WinForms tool to look up a single pairing (by Pairing ID +
Pairing Date) and view its full detail: header info, duty periods, flight
legs, and — specifically — which duty periods (and how much) received a
min-day credit/pay top-up from `MinDayProcess`, both per-duty and for the
pairing as a whole.

There's an existing `PVA` ("Pairing View App") project in this solution that
was an earlier, unfinished attempt at something similar — `PairingViewForm.cs`
is an empty `RadForm` shell, `PrgDisplayLine.cs` an unwired leg-display DTO.
It never got built out. `PairingInspect` is a fresh project, not a revival of
`PVA` — `PVA` stays as-is, untouched, usable only as loose reference for field
shapes (its `PrgDisplayLine` DTO is a flat per-leg row, no duty-period
structure — informed the grid design below).

## Reference

A real Sabre CrewTrac "Inquire" screen (`pairing inquire example.jpg`,
provided during design) is the visual/functional target for the core grid.
Key structure observed: legs for a duty period listed first, followed by a
highlighted inline summary row for that duty (Report/Release
date-time-city, Duty time, FDP, Block/Credit/Dhd/Pay totals, layover),
repeated per duty, followed by a pairing-level Totals row at the bottom.
`PairingInspect` matches this grid/totals structure but skips the reference's
extra chrome — no time-format toggle, no Normal/Compact view toggle, and none
of the Exceptions/Crew/Comments/Hotel/Evaluate buttons (those launch other
Sabre CrewTrac workflows unrelated to a read-only inspector).

## Scaffolding

Project reference/config conventions follow `PSAMinDay` (references to
`SFICTDataAccess`, `SFICTDateTimeUtils`, `SFIConfigUtils`, `CTApp`; same
Telerik reference set and `Office2010Black` theme; `TargetFrameworkVersion`
v4.0; main form inherits `CTAppNS.FormBase`, same as `PSAMinDay`'s
`DetailsForm`). The app shape follows `PSAMinDayCalcViewer` instead of
`PSAMinDay`: a normal interactive window (`Application.Run(new
PairingInspectForm())` in `Program.cs`) — no `ApplicationContext`, no
`NotifyIcon`, no timer. `PSAMinDay`'s tray-icon/background-processing pattern
does not apply here.

Files to create (mirroring `PSAMinDay`'s structure), added as a new project
in `PSAMinDayCalc.sln`:
```
PairingInspect/
  PairingInspect.csproj
  Program.cs
  PairingInspectForm.cs / .Designer.cs / .resx
  Properties/AssemblyInfo.cs, Resources.Designer.cs, Resources.resx,
             Settings.Designer.cs, Settings.settings, licenses.licx
  app.config
```

## Data access — no new layer needed

`CTPairing` (`SFICTDataAccess`, already used throughout `MinDayProcess`)
already exposes everything the core lookup needs:

- `Assemble(PrgNum, PrgDate)` — loads the pairing.
- `PrgHdr` (`PairingHeader`) — header info: PrgID, PrgDate, NumDuties,
  Canceled, Positions, IsStandUp, CrewType.
- `FindAllDuties()` — `List<PairingDuty>`, duty-period detail (Report,
  SkedEnd/EstEnd/ActEnd, ActCredit, ActPay) — the exact same fields
  `MinDayProcess.ProcessPairing` already reads.
- `FindAllOperatedLegs()`, `FindAllNonFlyingPairingLeg()`,
  `FindAllOtherAirlineDeadheadLegs()` — leg-level detail (flight #, city
  pair, times, block, credit).

Two small additive gaps to fill, both read-only:

1. **Marker decode ("last touched by")**: `PairingHeader` doesn't currently
   expose the underlying `PM.Updateid_Updempno` value. Needs a small
   additive field/property on `PairingHeader` (or a separate direct query)
   so it can be resolved against `TR09` — showing the friendly marker name
   (`MinDay - Updated` / `MinDay - No Update Needed` / `MinDay - Exception`)
   for 99901/99902/99903, or the real employee's name for any other value.
2. **Min-day amount detection** — see below.

## Min-day flagging (diff-based)

Once min-day has been applied to a duty, the original pre-adjustment credit
is gone — `UpdateDutyCreditsAndPay` overwrites `DB.Actcdt_Domtime`/
`Actpay_Domtime` directly with the floor value, and nothing persists "this
duty was topped up" or by how much.

However, `UpdateDutyCreditsAndPay` only ever overwrites the **duty-level**
(`DB`) and **pairing-level** (`PM`) aggregate credit/pay fields — never the
individual **flight leg** records' own credit fields. So for any duty:

```
MinDayAmount = duty.ActCredit - sum(that duty's legs' actual credit)
```

(and the equivalent for `ActPay`). A positive result means min-day was
applied, and the amount is exactly the top-up.

Confirmed this shouldn't produce false positives from other contractual
mechanisms: `MinDayProcess.cs` has zero references to duty period guarantee
anywhere, and `UpdateDutyCreditsAndPay`'s trip-rig branch writes to
`PM.Actthg_Domtime`/`Actguar` — a separate field from `DB.Actcdt` — never to
the duty-level credit field itself. So DPG and trip rig live in their own
fields and don't interfere with this diff. (Residual caveat: older non-.NET
legacy code outside what's readable in this session could theoretically also
write to `Actcdt` for other reasons — not something that can be fully ruled
out, but nothing found in this codebase suggests it.)

No cross-check against the `PM`/99901 marker is needed — the diff is
sufficient on its own, per explicit decision during design.

**Display**: each duty-summary row shows its min-day amount (if any) flagged
visually (e.g. distinct color/icon) alongside the normal Credit/Pay totals.
The pairing-level Totals row sums the per-duty min-day amounts into an
aggregate "Min-Day Credit" total, similarly flagged.

## Grid layout

Single `RadGridView`, one row source mixing two row kinds:

- **Leg rows**: Line, Day, Date, DA, Flight #, Dhd, Org, Dst, Dept, Arrv,
  Block, Credit, Drop/PU, Pos, Eqpt, Reg, Tail, Turn.
- **Duty-summary rows** (inserted right after that duty's legs): Report
  date/time/city, Release date/time/city, Duty time, FDP, Block/Credit/Dhd/
  Pay totals, layover, **plus the min-day flag/amount described above**.
  Exact cell-merging/formatting mechanism (Telerik `ViewCellFormatting`
  event vs. a simpler blank-columns-plus-one-summary-column approach) is a
  rendering-technique detail left to the implementation plan.

**Totals row** at the bottom: pairing-level Block, Credit, Dhd, Pay, TAFB,
and aggregate Min-Day Credit — summed from `FindAllDuties()` (or from
`PrgHdr` if pairing-level totals are already exposed there — to be confirmed
during implementation).

**Header panel** (from `PrgHdr`): Pairing ID, From/Thru dates, Freq,
Positions (CA/FO/FA/CF counts — derived from `PrgHdr.Positions`/`CrewType`
cross-referenced against the position-type mask, similar to how
`MinDayProcess` derives `PilotCount`/`FACount`), and the "Last touched by"
marker-decoded field.

## Out of scope

- Reviving or modifying `PVA` — left untouched.
- The reference image's toolbar (time-format toggle, Normal/Compact view)
  and action buttons (Exceptions, Crew, Comments, Hotel, Evaluate) — those
  launch other Sabre CrewTrac workflows, not a read-only inspector's job.
- Any write capability — `PairingInspect` is read-only; it never calls
  `UpdateDutyCreditsAndPay`, `MarkPMExamined`, or any other mutating method.
- Extending the PM/MS examination marker mechanism to duty-level (`DB`)
  records — the diff-based approach was chosen specifically so this isn't
  needed.
