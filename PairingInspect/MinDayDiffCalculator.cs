using SFICTDataAccess;

namespace PairingInspect
    {
    public class MinDayAmount
        {
        public int Credit;
        public bool HasMinDay { get { return Credit > 0; } }
        }

    public static class MinDayDiffCalculator
        {
        // Leg-level actual credit (FL.Actcdt) is never populated in this system --
        // confirmed empirically (3605/3605 FL rows on a sample date all read 0).
        // Actualized credit only exists at the duty level (DB.Actcdt_Domtime/Inttime),
        // so min-day is detected by comparing the duty's actualized credit against its
        // pre-actualization estimate rather than summing (nonexistent) leg actuals.
        public static MinDayAmount CalculateForDuty(PairingDuty duty)
            {
            MinDayAmount result = new MinDayAmount();
            result.Credit = duty.ActCredit > duty.EstCredit ? duty.ActCredit - duty.EstCredit : 0;
            return result;
            }
        }
    }
