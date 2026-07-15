using System.Collections.Generic;
using SFICTDataAccess;

namespace PairingInspect
    {
    public class MinDayAmount
        {
        public int Credit;
        public int Pay;
        public bool HasMinDay { get { return Credit > 0 || Pay > 0; } }
        }

    public static class MinDayDiffCalculator
        {
        public static MinDayAmount CalculateForDuty(PairingDuty duty, IEnumerable<PairingLegItem> dutyLegs)
            {
            int legCreditSum = 0;
            int legPaySum = 0;
            foreach (PairingLegItem leg in dutyLegs)
                {
                if (leg is AirlinePairingLeg)
                    {
                    AirlinePairingLeg airLeg = (AirlinePairingLeg)leg;
                    legCreditSum += airLeg.ActCredit;
                    legPaySum += airLeg.ActDhdPay;
                    }
                }

            MinDayAmount result = new MinDayAmount();
            result.Credit = duty.ActCredit > legCreditSum ? duty.ActCredit - legCreditSum : 0;
            result.Pay = duty.ActPay > legPaySum ? duty.ActPay - legPaySum : 0;
            return result;
            }
        }
    }
