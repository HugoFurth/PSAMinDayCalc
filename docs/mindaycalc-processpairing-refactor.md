# `ProcessPairing` Refactor — Public Single-Pairing Overload

**Context:** verified against `D:\data\vs\PSAMinDayCalc\MinDayProcess\MinDayProcess.cs`
that `ProcessPairing`'s own body only truly needs `PairingID`/`PairingDate` as external
inputs. `PilotCount`/`FACount` (from `PMByTimestamp`) turn out to be replaceable:
`prg.PrgHdr.CrewType` ("P"/"C"/"B", available right after `prg.Assemble()`) already
encodes the same two decisions those fields were used for, and
`CTPairingUpdate.UpdateDutyCreditsAndPay`'s `PilotCount`/`FACount` parameters are only
ever tested as `> 0` (checked `D:\data\vs\CTDataAccess\CTPairingUpdate.cs:192,201` —
`FACount` isn't read there at all), so exact headcounts were never actually required.

## New public overload — this becomes the real logic

```csharp
public bool ProcessPairing(string prgID, string prgDate, PairingProcessAction ppAction)
    {
    bool Bypassed = false;
    uint ExaminationMarker = 0;

    UpdateStatus(MinDayStatus.DetailedInfo, "Assembling pairing: " + prgID + " " + prgDate);
    prg.Assemble(prgID, prgDate);

    // starting on 9/1/2022, mixed pairings no longer allowed
    if (string.Compare(prgDate, "20220901") >= 0 && prg.PrgHdr.CrewType == "B")
        {
        UpdateStatus(MinDayStatus.DetailedInfo, "PX due to mixed pilot/FA crew");
        if (ppAction == PairingProcessAction.EvalupateAndUpdate)
            {
            CreatePX(prgID, prgDate);
            MarkPMExaminedSafely(prgID, prgDate, MINDAY_NO_UPDATE_NEEDED);
            }
        return false; // pairing does not need to be evaluated
        }

    // Min day value is based on start date of pairing. New value used for pilot prgs starting 9/1/2022.
    if (string.Compare(prgDate, "20220901") >= 0 && prg.PrgHdr.CrewType == "P")
        MINDAYCREDIT = MINDAYCREDIT4HR;
    else
        MINDAYCREDIT = MINDAYCREDIT35HR;

    List<ModDuty> ModDutiesList = new List<ModDuty>();
    List<PairingDuty> AllTrueDuties = prg.FindAllDuties();

    List<LayoverModDuty> LayoverDutyList = CalcMultiDayLayoverPay(AllTrueDuties);
    int iLayoverPay = SumOfLayoverPay(LayoverDutyList);

    if ((AllTrueDuties.Count(z => z.ActCredit < MINDAYCREDIT) == 0 && AllTrueDuties.Count(z => z.ActPay < MINDAYCREDIT) == 0) && iLayoverPay == 0)
        {
        Bypassed = true;
        ExaminationMarker = MINDAY_NO_UPDATE_NEEDED;
        }
    else // prg contains a min day condition
        {
        ModDutiesList = CreateModDutiesList(AllTrueDuties);
        if (ModDutiesList.Count > 0 || iLayoverPay > 0)
            {
            if (BypassAndCreatePXIfNeeded(prgID, prgDate, ppAction) && iLayoverPay == 0)
                {
                Bypassed = true;
                ExaminationMarker = MINDAY_NO_UPDATE_NEEDED;
                }
            if (!Bypassed)
                {
                try
                    {
                    if (ppAction == PairingProcessAction.EvalupateAndUpdate)
                        {
                        UpdateStatus(MinDayStatus.DetailedInfo, "Update started");
                        short pilotFlag = (short)(prg.PrgHdr.CrewType == "P" ? 1 : 0);
                        short faFlag = (short)(prg.PrgHdr.CrewType == "C" ? 1 : 0);
                        prg.UpdateDutyCreditsAndPay(AllTrueDuties, ModDutiesList, MINDAYCREDIT, LayoverDutyList, pilotFlag, faFlag);
                        UpdateStatus(MinDayStatus.DetailedInfo, "Update completed");
                        ExaminationMarker = MINDAY_UPDATED;
                        }
                    }
                catch (Exception ee)
                    {
                    String InnerMess = "";
                    if (ee.InnerException != null)
                        InnerMess = " / " + ee.InnerException.Message;
                    UpdateStatus(MinDayStatus.Critical, "Update aborted for " + prgID + " " + prgDate + " - " + ee.Message + InnerMess);
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
        }

    if (ppAction == PairingProcessAction.EvalupateAndUpdate && ExaminationMarker != 0)
        MarkPMExaminedSafely(prgID, prgDate, ExaminationMarker);

    ReadOnlyCollection<int> ModDutiesIntList = new ReadOnlyCollection<int>(ModDutiesList.ConvertAll(x => x.DutyPeriod));
    PairingProcessInfoEventArgs Args = new PairingProcessInfoEventArgs(Bypassed, prgID, prgDate, ModDutiesIntList);
    OnProcess(Args);
    return !Bypassed;
    }
```

## Existing private overload — now a thin wrapper

Batch cursor-advance (`SavePMUserSettings`) stays here only — it must never run for an
on-demand single-pairing call, since that's what persists `MinDayProcess.json`'s "last
processed" position for the scheduled batch.

```csharp
private bool ProcessPairing(PMByTimestamp pmts, PairingProcessAction ppAction)
    {
    if (ppAction == PairingProcessAction.EvalupateAndUpdate)
        SavePMUserSettings(pmts.Update_Date, pmts.Update_Time.ToString());
    return ProcessPairing(pmts.PairingID, pmts.PairingDate, ppAction);
    }
```

## `CreatePX` / `MarkPMExaminedSafely` also drop `PMByTimestamp`

Same reasoning applies: `CreatePX` only used `pmts.PairingID`/`PairingDate` for a status
message (the real DB call already used `prg.PrgHdr.PrgID`/`PrgDate`); `MarkPMExaminedSafely`
only used those two fields too.

```csharp
private void CreatePX(string prgID, string prgDate)
    { /* body unchanged except pmts.PairingID/PairingDate -> prgID/prgDate */ }

private void MarkPMExaminedSafely(string prgID, string prgDate, uint MarkerEmpNo)
    { /* body unchanged except pmts.PairingID/PairingDate -> prgID/prgDate */ }
```

## Remaining open gap — needs a decision, not silently ignored

`BypassAndCreatePXIfNeeded` (called above) detects mixed ab/RAS/TTA crew by scanning
`pmtss.List` — the batch's already-loaded set of PM rows across the whole update window —
filtered to the target pairing. For the batch path that list is already populated by the
time `ProcessPairing` runs, so batch behavior is unaffected by this refactor.

But if the new public `ProcessPairing(prgID, prgDate, ppAction)` is called directly (e.g.
from PairingInspect) without first populating `pmtss.List` for that pairing,
`BypassAndCreatePXIfNeeded`'s `pmtss.List.Any(...)` checks will just see an empty list and
always return `false` — silently skipping the "mixed ab/RAS/TTA crew needs a PX instead"
business rule for on-demand calls.

**Two options:**
1. Accept the gap for now — it's a real business rule, not a null-ref risk, but it means
   the on-demand path and the batch path aren't behaviorally identical for that one edge case.
2. Add a small `pmtss.FillByPairing(prgID, prgDate)`-style call at the top of the new
   public method to populate `pmtss.List` scoped to just this pairing before proceeding,
   closing the gap. This still needs the new `CTPMTimestamps.FillByPairing` data-access
   query described in `mindaycalc-single-pairing-implementation-outline.md`.

**Decision: pending** — not yet chosen as of this writing.
