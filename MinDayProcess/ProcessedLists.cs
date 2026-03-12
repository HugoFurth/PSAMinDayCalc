using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SFICTDataAccess;

namespace MinDayProcessNS
    {
    public class ProcessedPairings
        {
        public List<ProcessedPairing> List;

        public ProcessedPairings()
            {
            List = new List<ProcessedPairing>();
            }

        public bool Processed(PMByTimestamp pmts)
            {
     //       if (pmts.AbsenceCode != null || pmts.AssignCode == "RAS" || pmts.AssignCode == "TTA")
     //           return false;

            return List.Any(x => x.PairingID == pmts.PairingID && x.PairingDate == pmts.PairingDate);
            }

        }

    public class ProcessedPairing
        {
        private String _PairingID;
        private String _PairingDate;

        public string PairingID { get { return _PairingID; } }
        public string PairingDate { get { return _PairingDate; } }

        public ProcessedPairing(String PairingID, String PairingDate)
            {
            _PairingID = PairingID;
            _PairingDate = PairingDate;
            }
        }

    public class EvaluateSkeds
        {
        public EvalQueue Queue;

        public EvaluateSkeds()
            {
            Queue = new EvalQueue();
            }

        public bool Processed(uint EmpNum, CTBidPeriod BidPeriod)
            {
            return Queue.Any(x => x.EmpNum == EmpNum && x.BidPeriod == BidPeriod);
            }

        }

    public class EvaluateSkedParams
        {
        private uint _EmpNum ;
        private CTBidPeriod _BidPeriod;

        public uint EmpNum { get { return _EmpNum; } }
        public CTBidPeriod BidPeriod { get { return _BidPeriod; } }

        public EvaluateSkedParams(uint EmpNum, CTBidPeriod BidPeriod)
            {
            _EmpNum = EmpNum;
            _BidPeriod = BidPeriod;
            }
        }

    public class EvalQueue : Queue<EvaluateSkedParams>
         {
        public bool Add(EvaluateSkedParams item)
             {
             if (this.Any(x => x.EmpNum == item.EmpNum && x.BidPeriod == item.BidPeriod))
                 return false;
             else
                 base.Enqueue(item);
             return true;
             }
         }
    }

public class PMByTimestampx
    {
    private String _PairingID;
    private String _PairingDate;
    private String _Update_Date;
    private int _Update_Time;

    public string PairingID { get { return _PairingID; } }
    public string PairingDate { get { return _PairingDate; } }
    public string Update_Date { get { return _Update_Date; } }
    public int Update_Time { get { return _Update_Time; } }


    public PMByTimestampx(String inPairingID, String inPrgDate, String inUpdateDate, int inUpdateTime)
        {
        Initialize(inPairingID, inPrgDate, inUpdateDate, inUpdateTime);
        }

    private void Initialize(String inPairingID, String inPrgDate, String inUpdateDate, int inUpdateTime)
        {
        _PairingID = inPairingID;
        _PairingDate = inPrgDate;
        _Update_Date = inUpdateDate;
        _Update_Time = inUpdateTime;
        }
    }
