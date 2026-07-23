using SFICTDataAccess;

namespace PairingInspect
    {
    public class MinDayAmount
        {
        public int Credit;
        public bool HasMinDay { get { return Credit > 0; } }
        }

    // Mirrors the guarantee-floor logic in MinDayProcess.ProcessPairing/AddDutyPeriodToListIfNeeded:
    // when a duty's actual credit is topped up, MinDayProcess replaces it outright with the flat
    // per-duty floor (MINDAYCREDIT) -- see UpdateDutyCreditsAndPay's "IncludeCredit ? NewAmount :
    // TrueDuty.ActDomCredit". So a topped-up duty's ActCredit is exactly the floor value, and is
    // higher than what its legs (including non-flying) actually summed to. Both conditions must
    // hold: the legs must sum to less than the duty's credit, AND that credit must equal the floor
    // exactly. Neither one alone is proof -- a duty whose actual credit grew past its legs' sum for
    // some other reason (e.g. a flight ran long) is not a min-day case unless it also lands exactly
    // on the floor.
    //
    // Even both conditions holding is only circumstantial -- MinDayProcess can still decide not to
    // apply a top-up (e.g. it defers to a PX for crew-assignment reasons; see
    // BypassAndCreatePXIfNeeded in MinDayProcess.cs). The PM.UpdateidUpdempno marker is the only
    // source of truth for whether a top-up actually happened, so unless the marker says
    // "MinDay - Updated", min day is zero by definition regardless of the numbers.
    public static class MinDayDiffCalculator
        {
        // Mirrors MinDayProcess.MINDAYCREDIT35HR / MINDAYCREDIT4HR.
        const int MinDayCredit35Hr = 210;
        const int MinDayCredit4Hr = 240;

        public static MinDayAmount CalculateForDuty(PairingDuty duty, string pairingDate, string crewType, string markerName, int legCreditSum)
            {
            MinDayAmount result = new MinDayAmount();
            if (markerName != "MinDay - Updated")
                return result;

            // Mirrors MinDayProcess.ProcessPairing: pilot-only pairings dated 9/1/2022 or later use
            // the 4hr floor; everything else (older pairings, or mixed/cabin crew) uses the 3.5hr floor.
            int minDayCredit = (string.Compare(pairingDate, "20220901") >= 0 && crewType == "P")
                ? MinDayCredit4Hr : MinDayCredit35Hr;

            if (legCreditSum < duty.ActCredit && duty.ActCredit == minDayCredit)
                result.Credit = duty.ActCredit - legCreditSum;

            return result;
            }
        }
    }
