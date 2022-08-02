using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SFICTDataAccess;
using SFIConfigUtils;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Configuration;
using MSEval;
using System.Data.OleDb;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Runtime.Serialization;


namespace MinDayProcessNS
{

   public enum MinDayStatus { DetailedInfo, Info, Warning, Critical, CriticalStop };

   public class PairingProcessInfoEventArgs : EventArgs
   {
      public PairingProcessInfoEventArgs(bool Bypassed, String PairingID, String PairingDate,  ReadOnlyCollection<int> ModifiedDutiesList)
      {
         this.Bypassed = Bypassed;
         this.PairingID = PairingID;
         this.PairingDate = PairingDate;
         this.ModifiedDutiesList = ModifiedDutiesList;
      }
      public readonly bool Bypassed;
      public readonly String PairingID;
      public readonly String PairingDate;
      public ReadOnlyCollection<int> ModifiedDutiesList;
   }

   public class MinDayStatusEventArgs : EventArgs
       {
       public MinDayStatusEventArgs(MinDayStatus Status, String Message)
           {
           this.Status = Status;
           this.Message = Message;
           }
       public readonly MinDayStatus Status;
       public readonly String Message;
       }

    public delegate void PairingProcessDelegate(object Sender, PairingProcessInfoEventArgs Args);
    public delegate void MinDayStatusDelegate(object Sender, MinDayStatusEventArgs Args);


    public class MinDayProcess
        {
        public const Int16 MINDAYCREDIT = 210;
        public event PairingProcessDelegate PairingProcess;
        public event MinDayStatusDelegate StatusUpdate;
        CTPMTimestamps pmtss;
        CTABTimestamps abtss;
        CTPEs ctpes;
        CTPairing prg;
        public ProcessedPairings ProccessedPrgs;
        public EvaluateSkeds EvalSkeds;
        public CTBidPeriods bps;
        public EvaluateMS EvalMS;
        public CTMSs ctmss;
        TCTSecurityItem ReadSec;

        String _PMAfterDate;
        String _PMAfterTime;
        String _ABAfterDate;
        String _ABAfterTime;
        public SFICTDataAccess.CTDataSetTableAdapters.MSTableAdapter msTableAdapter;

        public MinDayProcess()
            {
            try {
                pmtss = new CTPMTimestamps();
                abtss = new CTABTimestamps();
                ctpes = new CTPEs();
                prg = new CTPairing();
                prg.IncludeOperatedFlights = false;
                ProccessedPrgs = new ProcessedPairings();
                EvalSkeds = new EvaluateSkeds();
                (bps = new CTBidPeriods()).Fill("1001");
                EvalMS = new EvaluateMS();
                ctmss = new CTMSs();
                ReadSec = CTSecurity.GetSavedSecurityProfile();
                if (ReadSec.iUserID == 0)
                    throw new Exception("Insufficient security privilege");
                }
            catch (Exception)
                {
                throw;
                }
            }


        public void ProcessPM()
            {
            try
                {
                ProcessPM(_PMAfterDate, Convert.ToInt32(_PMAfterTime));
                }
            catch (Exception ee)
                {
                String InnerMess = "";
                if (ee.InnerException != null)
                    InnerMess = " / " + ee.InnerException.Message;
                UpdateStatus(MinDayStatus.Critical, "General exception processing pairings<" + ee.Message + InnerMess + ">");
                }
            UpdateStatus(MinDayStatus.DetailedInfo, "Next call will look for pairings modified after " + _PMAfterDate + " " + _PMAfterTime);
            }

        public void ProcessPM(String PMAfterDate, int PMAfterTime)
            {
            ProccessedPrgs.List.Clear();
            EvalSkeds.Queue.Clear();
            UpdateStatus(MinDayStatus.Info, "Processing pairings updated after: " + PMAfterDate + " " + PMAfterTime);
            int x = pmtss.FillByLatestUpdate(PMAfterDate, PMAfterTime);

            UpdateStatus(MinDayStatus.Info, "Processing " + x.ToString() + " pairings");
            foreach (PMByTimestamp pmts in pmtss.List.OrderBy(d => d.Update_Date).ThenBy(d => d.Update_Time))
                {
                if (ProccessedPrgs.Processed(pmts))  // no need to process more than once
                    continue;

                if (ProcessPairing(pmts)) // if true, pairing was updated so eval all crew with this pairing on their sked
                    AddCrewToEvalList(pmts);
                    
                ProccessedPrgs.List.Add(new ProcessedPairing(pmts.PairingID, pmts.PairingDate));
                }

            EvaluateSkeds();
            }

        public void ProcessAB()
            {
            // parameters for ProcessAB set before calling this method
            try
                {
                ProcessAB(_ABAfterDate, Convert.ToInt32(_ABAfterTime));
                }
            catch (Exception ee)
                {
                String InnerMess = "";
                if (ee.InnerException != null)
                    InnerMess = " / " + ee.InnerException.Message;
                UpdateStatus(MinDayStatus.Critical, "General exception processing absences<" + ee.Message + InnerMess + ">");
                }
            UpdateStatus(MinDayStatus.DetailedInfo, "Next call will look for absences modified after " + _ABAfterDate + " " + _ABAfterTime);
            }

        public void ProcessAB(String ABAfterDate, int ABAfterTime)
            {
            UpdateStatus(MinDayStatus.Info, "Processing absences updated after: " + ABAfterDate + " " + ABAfterTime);
            int x = abtss.FillByLatestUpdate(ABAfterDate,ABAfterTime);

            UpdateStatus(MinDayStatus.Info, "Processing " + x.ToString() + " absences");
            // foreach record, insert an evaluate PE record
            foreach (ABByTimestamp abts in abtss.List.OrderBy(d => d.Update_Date).ThenBy(d => d.Update_Time))
                ProcessQueueForCrewPost(abts);
            }

        public void Process()
            {
            if (LoadUserSettingsJSON() == false)
                return;
            ProcessPM();
            ProcessAB();
            }


        private void ProcessQueueForCrewPost(ABByTimestamp abts)
            {
            SaveABUserSettings(abts.Update_Date, abts.Update_Time.ToString());
            DateTime InsDateTime = DateTime.Now;
            if (!(ctpes.DoesEvaluateExist(abts.PairingID,abts.PairingDate)))
                ctpes.Insert(abts.PairingID, abts.PairingDate, (uint)ReadSec.iUserID, SFICTDateTimeUtils.CTDateFormat.CTInternal(InsDateTime),(int) InsDateTime.TimeOfDay.TotalMilliseconds);
            } 

        private bool ProcessPairing(PMByTimestamp pmts)
            {
            SavePMUserSettings(pmts.Update_Date,pmts.Update_Time.ToString());
            bool Bypassed = false;

            List<ModDuty> ModDutiesList = new List<ModDuty>();

            // 27MAR20 - can no longer use 'Bypass1CrewPairing' because crewmember may qualify for multiday layover pay even if duy period min day does not apply
            // if (Bypass1CrewPairing(pmts))   // can bypass if a single crewmember only and meets the bypass criteria
            //    Bypassed = true;
            // else
            //  {
                UpdateStatus(MinDayStatus.DetailedInfo, "Assembling pairing: " + pmts.PairingID + " " + pmts.PairingDate);
                int iLineCount = prg.Assemble(pmts.PairingID, pmts.PairingDate);
                //      UpateStatus(MinDayStatus.DetailedInfo, "Lines in pairing: " + iLineCount.ToString());
                List<PairingDuty> AllTrueDuties = prg.FindAllDuties();

                List<LayoverModDuty> LayoverDutyList = CalcMultiDayLayoverPay(AllTrueDuties);
                int iLayoverPay = SumOfLayoverPay(LayoverDutyList);

                if ((AllTrueDuties.Count(z => z.ActCredit < MINDAYCREDIT) == 0 && AllTrueDuties.Count(z => z.ActPay < MINDAYCREDIT) == 0) && iLayoverPay == 0)
                    Bypassed = true;
                else
                    {
                    ModDutiesList = CreateModDutiesList(AllTrueDuties);
                    if (ModDutiesList.Count > 0 || iLayoverPay > 0)
                        {
                        if (BypassAndCreatePXIfNeeded(pmts) && iLayoverPay == 0)
                            Bypassed = true;
                        if (!Bypassed)
                            {
                            UpdateStatus(MinDayStatus.DetailedInfo, "Update started");
                            try {
                                prg.UpdateDutyCreditsAndPay(AllTrueDuties, ModDutiesList, MINDAYCREDIT, LayoverDutyList);
                                UpdateStatus(MinDayStatus.DetailedInfo, "Update completed");
                                }
                            catch (Exception ee)
                                {
                                String InnerMess= "";    
                                if (ee.InnerException != null)
                                    InnerMess = " / " + ee.InnerException.Message;
                                UpdateStatus(MinDayStatus.Critical, "Update aborted for " + pmts.PairingID + " " + pmts.PairingDate + " - " + ee.Message + InnerMess);
                                Bypassed = true;
                                }
                            }
                        }
                    else
                        Bypassed = true;
                //       } 27MAR20
                }

            ReadOnlyCollection<int> ModDutiesIntList = new ReadOnlyCollection<int>(ModDutiesList.ConvertAll(x => x.DutyPeriod));
   //         ModDutiesIntList = ModDutiesIntList.Distinct().ToList();

            PairingProcessInfoEventArgs Args = new PairingProcessInfoEventArgs(Bypassed, pmts.PairingID, pmts.PairingDate, ModDutiesIntList);
            OnProcess(Args);
            return !Bypassed ; // true if sked needs to be evaluated
            }

            private int SumOfLayoverPay(List<LayoverModDuty> LayoverDuties)
                {
                int TotalLayoverPay = 0;
                if (LayoverDuties == null)
                    return 0;
                else
                    {
                    foreach (LayoverModDuty l in LayoverDuties)
                        TotalLayoverPay += l.LayoverPay;
                    }
            return TotalLayoverPay;
                }

            private List<LayoverModDuty> CalcMultiDayLayoverPay(List<PairingDuty> Duties)
            {
            List<LayoverModDuty> LayoverList = new List<LayoverModDuty>();

            int DutyCount = Duties.Count;

            // From list of all true duties, find any that have more than 24 hour layovers. 
            // One duty pairings cannot qualify
            if (DutyCount < 2)
                return null;

            for (int i = DutyCount - 1; i > 0; i--) // iterate backwards from last duty to first
                {
                // find report of subject duty
                DateTime? DutyReport = Duties[i].Report.AsMSDate;

                // find release of prior duty
                DateTime? PriorDutyEnd = Duties[i - 1].ActEnd.AsMSDate;

                if (DutyReport.HasValue && PriorDutyEnd.HasValue)
                    {
                    DateTime drReportDate =  DutyReport.Value.Date;
                    DateTime drPriorEndDate = PriorDutyEnd.Value.Date;
                    int iSpan = (drReportDate - drPriorEndDate).Days;  // the date only difference

                    // now check report and release times
                    if ((DutyReport.Value.TimeOfDay).TotalMinutes < 119.0)
                        iSpan--;

                    if ((PriorDutyEnd.Value.TimeOfDay).TotalMinutes > 119.0)
                        iSpan--;

                    // give min day credit for that duty if meets multiday crtieria
                    if (iSpan > 0)

                        LayoverList.Add(new LayoverModDuty(i, true, true, iSpan * MINDAYCREDIT));
                    }
                }
            return LayoverList;
            }


            private void AddCrewToEvalList(PMByTimestamp pmts)
            {
            // find all crew on this pairing and add to the eval queue
            foreach (PMByTimestamp p in pmtss.List.Where(x => x.EmpNum > 0 &&  x.PairingID == pmts.PairingID && x.PairingDate == pmts.PairingDate).ToList())
                {
                if (EvalSkeds.Queue.Add(new EvaluateSkedParams(p.EmpNum, bps.FindBPItem(p.BidPeriod))))
                    UpdateStatus(MinDayStatus.Info, "Schedule of crewmember: " + p.EmpNum + " for " + p.BidPeriod + " queued for evaluation");

                String PairingEndBP = bps.FindBPForDate(p.ActEndDate);
                if (EvalSkeds.Queue.Add(new EvaluateSkedParams(p.EmpNum, bps.FindBPItem(PairingEndBP))))
                    UpdateStatus(MinDayStatus.Info, "Schedule of crewmember: " + p.EmpNum + " for " + PairingEndBP + " queued for evaluation");
                }
            }

        private void EvaluateSkeds()
            {
            UpdateStatus(MinDayStatus.Info, EvalSkeds.Queue.Count().ToString() + " schedules ready for evaluation");
            int iEvalCount = 0;
            foreach (EvaluateSkedParams ev in EvalSkeds.Queue)
                {
                try {
                    ++iEvalCount;
                    String MMMYYBP = ev.BidPeriod.BidPeriodMSDisplayMember;
                    String EmpNum = ev.EmpNum.ToString();

                    // find ms for this request. if does not exist, don't do the evaluate
                    if (ctmss.FindEmpBP(ev.EmpNum,ev.BidPeriod.BidPeriodValueMember) != 1)
                        {
                        UpdateStatus(MinDayStatus.Info, "Bypassing - no schedule for crewmember: " + EmpNum + " for " + MMMYYBP + " (" + iEvalCount.ToString() + "/" + EvalSkeds.Queue.Count().ToString() + ")");
                        continue;
                        }

                    UpdateStatus(MinDayStatus.Info, "Evaluation started for crewmember: " + EmpNum + " for " + MMMYYBP + " (" + iEvalCount.ToString() + "/" + EvalSkeds.Queue.Count().ToString() + ")");
                    bool bEval = EvalMS.EvaluateSked(EmpNum,MMMYYBP);
                    if (bEval)
                        UpdateStatus(MinDayStatus.Info, "Evaluated crewmember: " + EmpNum + " for " + MMMYYBP);
                    else
                        UpdateStatus(MinDayStatus.Critical, "Error evaluating crewmember: " + EmpNum + " for " + MMMYYBP + "<" + EvalMS.LastErrorMsg + ">");
                    }
                catch (Exception ee)
                    {
                    String InnerMess = "";
                    if (ee.InnerException != null)
                        InnerMess = " / " + ee.InnerException.Message;
                    UpdateStatus(MinDayStatus.Critical, "Exception evaluating crewmember <" + ee.Message + InnerMess + ">");
                    }
                }

            }

        // Bypass1PilotPairing no longer used 
        private bool Bypass1PilotPairing(PMByTimestamp pmts)
            {
            if ((pmts.PilotCount == 1 && pmts.FACount == 0) && (pmts.AbsenceCode != null || pmts.AssignCode == "RAS" || pmts.AssignCode == "TTA"))
                {
                UpdateStatus(MinDayStatus.Info, "1-pilot ab/RAS/TTA pairing bypassed");
                return true;
                }
            return false;
            }

        private bool Bypass1CrewPairing(PMByTimestamp pmts)
            {
            if ((pmts.PilotCount+pmts.FACount == 1) && (pmts.AbsenceCode != null || pmts.AssignCode == "RAS" || pmts.AssignCode == "TTA"))
                {
                UpdateStatus(MinDayStatus.Info, "1-crew ab/RAS/TTA pairing bypassed");
                return true;
                }
            return false;
            }

        // this routine works but last if in the create px if needed routine is simpler
        private bool BypassIfNeeded(PMByTimestamp pmts)
            {
            // if ALL pilots who have this pairing with absence code or RAS or TTA assign + the open pairings == total pilot count on trip, we need  to skip but not create a PX
            if (pmtss.List.Count(w => w.EmpNum != 0 && w.PairingID == pmts.PairingID && w.PairingDate == pmts.PairingDate && (w.AbsenceCode != null || w.AssignCode == "RAS" || w.AssignCode == "TTA")) +
                pmtss.List.Count(w => w.EmpNum == 0 && w.PairingID == pmts.PairingID && w.PairingDate == pmts.PairingDate)
                == pmts.PilotCount)
                return true;

            return false;
            }

        private bool BypassAndCreatePXIfNeeded(PMByTimestamp pmts)
            {
            // px for any FA on a trip with a duty under 3:30

            // FACount restriction lifted
/*            if (pmts.FACount > 0)
                {
                UpdateStatus(MinDayStatus.DetailedInfo, "PX due to FA on pairing");
                CreatePX(pmts);
                return true;
                }
                */

            // if pairing is assigned to one or more crew and has ab/RAS/TTA *AND* if pairing is assigned to one or more crew without ab/RAS/TTA, make a PX
            if (pmtss.List.Any(w => w.EmpNum != 0 && w.PairingID == pmts.PairingID && w.PairingDate == pmts.PairingDate && (w.AbsenceCode != null || w.AssignCode == "RAS" || w.AssignCode == "TTA")) 
                               && 
                pmtss.List.Any(w => w.EmpNum != 0 && w.PairingID == pmts.PairingID && w.PairingDate == pmts.PairingDate && (w.AbsenceCode == null && w.AssignCode != "RAS" && w.AssignCode != "TTA")))
                {
                UpdateStatus(MinDayStatus.DetailedInfo, "PX due to mixed ab/RAS/TTA crew");
                CreatePX(pmts);
                return true;
                }

            // if we reach here we don't need a px because there is not a mix of pilots assigned with and without ab/RAS/TTA. Only have assigned pilots + possibly open. 
            // either way, skip processing
            if (pmtss.List.Any(w => w.EmpNum != 0 && w.PairingID == pmts.PairingID && w.PairingDate == pmts.PairingDate && (w.AbsenceCode != null || w.AssignCode == "RAS" || w.AssignCode == "TTA")))
                {
                UpdateStatus(MinDayStatus.DetailedInfo, "Pairing bypassed due to all crew ab/RAS/TTA");
                return true;
                }

            return false;
            }

        private void CreatePX(PMByTimestamp pmts)
            {
            try {
                PairingDuty pdlast = prg.FindAllDuties().Find(s => s.DutyPeriod == prg.NumDuties);

                int i = prg.InsertException(prg.PrgHdr.PrgID,prg.PrgHdr.PrgDate,pdlast.Report,pdlast.ActEnd,6500,ReadSec.iUserID);
                if (i == 1)
                    UpdateStatus(MinDayStatus.DetailedInfo, "PX created for " + pmts.PairingID + " " + pmts.PairingDate);
                else
                    UpdateStatus(MinDayStatus.Critical, "Failed to create PX for " + pmts.PairingID + " " + pmts.PairingDate);
                }
            catch (Exception ee)
                {
                String InnerMess = "";
                if (ee.InnerException != null)
                    InnerMess = " / " + ee.InnerException.Message;
                UpdateStatus(MinDayStatus.Critical, "PX creation aborted - " + ee.Message + InnerMess);
                }
            }

        private List<ModDuty> CreateModDutiesList(List<PairingDuty> pdList)
            {
            List<ModDuty> ModDutyList = new List<ModDuty>();

            // special check for 1-duty trips
            if (pdList.Count() == 1)
                {
                if (AreAnyLegsInDutyFakeDeadhead(1) || 
                   (pdList[0].Report.TimeAsMins > 720 && 
                   (pdList[0].ActEnd.TimeAsMins < 1020 && pdList[0].EstEnd.TimeAsMins < 1020 && pdList[0].SkedEnd.TimeAsMins < 1020)))
                    { }  // no min day
                else
                    {
                    AddDutyPeriodToListIfNeeded(ModDutyList,pdList[0]);
                    }
                }
            else // multiple duty trip 
                {
                foreach (PairingDuty pd in pdList)
                    {
                    if (AreAnyLegsInDutyFakeDeadhead(pd.DutyPeriod))
                        continue;
                    if (pd.DutyPeriod == 1 && pd.Report.TimeAsMins >= 720) // no calc needed if reports after 12:00 local on first day of trip
                        continue;
                    if (pd.DutyPeriod == pdList.Count() && // no calc needed if releases before or equal to 17:00 local on last day of trip
                        (pd.ActEnd.TimeAsMins <= 1020 && pd.EstEnd.TimeAsMins <= 1020 && pd.SkedEnd.TimeAsMins <= 1020))
                        continue;

                    AddDutyPeriodToListIfNeeded(ModDutyList, pd);
                    }
                }

            return ModDutyList;
            }

        private void AddDutyPeriodToListIfNeeded(List<ModDuty> ModDutyList,PairingDuty pd)
            {
            bool IncludeCredit = false;
            bool IncludePay = false;
            if (pd.ActCredit < MINDAYCREDIT)
                IncludeCredit = true;
            if (pd.ActPay < MINDAYCREDIT)
                IncludePay = true;

            if (IncludeCredit || IncludePay)
                ModDutyList.Add(new ModDuty(pd.DutyPeriod, IncludeCredit, IncludePay));
            }

        private bool AreAnyLegsInDutyFakeDeadhead(int ipd)
            {
            List<OtherAirlineDeadheadPairingLeg> OADLegs = prg.FindAllOtherAirlineDeadheadLegsByDuty(ipd);
            int iFakeLegCount = OADLegs.Count(x => x.DeadheadCode == "K");
            if (iFakeLegCount > 0)
                return true;
            return false;
            }

        protected void OnProcess(PairingProcessInfoEventArgs Args)
            {
            if (PairingProcess != null)
                {
                PairingProcess(this,Args);
                }
            }

        protected void OnStatusUpdate(MinDayStatusEventArgs Args)
            {
            if (StatusUpdate != null)
                {
                StatusUpdate(this, Args);
                }
            }

        private void UpdateStatus(MinDayStatus mds, String Message)
            {
            MinDayStatusEventArgs Args = new MinDayStatusEventArgs(mds, Message);
            OnStatusUpdate(Args);
            }

        public bool LoadUserSettingsJSON()
            {
            try
                {
                var js = new DataContractJsonSerializer(typeof(LastUpdate));
                LastUpdate lu = new LastUpdate();
                var dir = AppDomain.CurrentDomain.BaseDirectory + System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
                string path = dir + ".json";
                using (FileStream fs = new FileStream(path, FileMode.Open))
                    {
                    lu = (LastUpdate)js.ReadObject(fs);
                    }

                _PMAfterDate = lu.PMAfterDate;
                int x;
                if (Int32.TryParse(_PMAfterDate, out x) == false)
                    throw new Exception("PMAfterDate <" + _PMAfterDate + "> invalid in json file <" + path + ">");

                _PMAfterTime = lu.PMAfterTime;
                if (Int32.TryParse(_PMAfterTime, out x) == false)
                    throw new Exception("PMAfterTime <" + _PMAfterTime + "> invalid in json file <" + path + ">");

                _ABAfterDate = lu.ABAfterDate;
                if (Int32.TryParse(_ABAfterDate, out x) == false)
                    throw new Exception("ABAfterDate <" + _ABAfterDate + "> invalid in json file <" + path + ">");

                _ABAfterTime = lu.ABAfterTime;
                if (Int32.TryParse(_ABAfterTime, out x) == false)
                    throw new Exception("ABAfterTime <" + _ABAfterTime + "> invalid in json file <" + path + ">");
                }
            catch (Exception ee)
                {
                String InnerMess = "";
                if (ee.InnerException != null)
                    InnerMess = ee.InnerException.Message;
                UpdateStatus(MinDayStatus.CriticalStop, ee.Message + ". " + InnerMess);
                return false;
                }
            finally
                {
                UpdateStatus(MinDayStatus.Info, "Loaded PM parameters: " + _PMAfterDate + " " + _PMAfterTime);
                UpdateStatus(MinDayStatus.Info, "Loaded AB parameters: " + _ABAfterDate + " " + _ABAfterTime);
                }

            return true;
            }

        public bool LoadUserSettings()
            {
            try {
                Configuration conf = ConfigurationManager.OpenExeConfiguration(System.Reflection.Assembly.GetExecutingAssembly().Location);

                String SectionNameString = "userSettings/" + Assembly.GetExecutingAssembly().GetName().Name + ".Properties.Settings";
                var us = (System.Configuration.ClientSettingsSection)conf.GetSection(SectionNameString);

                _PMAfterDate = (us.Settings.Get("PMAfterDate")).Value.ValueXml.InnerXml;
                int x;
                if (Int32.TryParse(_PMAfterDate,out x) == false)
                    throw new Exception("PMAfterDate <" + _PMAfterDate + "> invalid in config file <" + conf.FilePath + ">");


                _PMAfterTime = (us.Settings.Get("PMAfterTime")).Value.ValueXml.InnerXml;
                if (Int32.TryParse(_PMAfterTime, out x) == false)
                    throw new Exception("PMAfterTime <" + _PMAfterTime + "> invalid in config file <" + conf.FilePath + ">");

                _ABAfterDate = (us.Settings.Get("ABAfterDate")).Value.ValueXml.InnerXml;
                if (Int32.TryParse(_ABAfterDate, out x) == false)
                    throw new Exception("ABAfterDate <" + _ABAfterDate + "> invalid in config file <" + conf.FilePath + ">");


                _ABAfterTime = (us.Settings.Get("ABAfterTime")).Value.ValueXml.InnerXml;
                if (Int32.TryParse(_ABAfterTime, out x) == false)
                    throw new Exception("ABAfterTime <" + _ABAfterTime + "> invalid in config file <" + conf.FilePath + ">");
                }
            catch (Exception ee)
                {
                String InnerMess = "";
                if (ee.InnerException != null)
                    InnerMess = ee.InnerException.Message;
                UpdateStatus(MinDayStatus.CriticalStop, ee.Message + ". " + InnerMess);
                return false;
                }
            finally
                {
                UpdateStatus(MinDayStatus.Info, "Loaded PM parameters: " + _PMAfterDate + " " + _PMAfterTime);
                UpdateStatus(MinDayStatus.Info, "Loaded AB parameters: " + _ABAfterDate + " " + _ABAfterTime);
                }

            return true;
            }

        public bool LoadAppSettings()  // NOT BEING USED - UNDER DEVELOPMENT
            {
            try
                {
                Configuration conf = ConfigurationManager.OpenExeConfiguration(System.Reflection.Assembly.GetExecutingAssembly().Location);

                String SectionNameString = "appSettings/" + Assembly.GetExecutingAssembly().GetName().Name + ".Properties.Settings";
                var us = (System.Configuration.ClientSettingsSection)conf.GetSection(SectionNameString);

                _PMAfterDate = (us.Settings.Get("PMAfterDate")).Value.ValueXml.InnerXml;
                _PMAfterTime = (us.Settings.Get("PMAfterTime")).Value.ValueXml.InnerXml;
                }
            catch (Exception ee)
                {
                String InnerMess = "";
                if (ee.InnerException != null)
                    InnerMess = ee.InnerException.Message;
                UpdateStatus(MinDayStatus.CriticalStop, ee.Message + ". " + InnerMess);
                return false;
                }
            finally
                {
                UpdateStatus(MinDayStatus.Info, "Loaded parameters: " + _PMAfterDate + " " + _PMAfterTime);
                }

            return true;
            }

        public void SavePMUserSettings(String LatestPMAfterDate, String LatestPMAfterTime)
            {
            if (string.Compare(_PMAfterDate,LatestPMAfterDate) > 0)
                UpdateStatus(MinDayStatus.Critical, " New PMAfterDate is less than previous one:" + LatestPMAfterDate + " " + _PMAfterDate);
                
            _PMAfterDate = LatestPMAfterDate;
            _PMAfterTime = LatestPMAfterTime;

            SaveLatestParamSettingsAsJSON();
            return;
      /*      try
                {
                Configuration conf = ConfigurationManager.OpenExeConfiguration(System.Reflection.Assembly.GetExecutingAssembly().Location);

                String SectionNameString = "userSettings/" + Assembly.GetExecutingAssembly().GetName().Name + ".Properties.Settings";
                var us = (System.Configuration.ClientSettingsSection)conf.GetSection(SectionNameString);

                (us.Settings.Get("PMAfterDate")).Value.ValueXml.InnerXml = LatestPMAfterDate;
                (us.Settings.Get("PMAfterTime")).Value.ValueXml.InnerXml = LatestPMAfterTime;

                us.SectionInformation.ForceSave = true;
                conf.Save(ConfigurationSaveMode.Modified);
                }
            catch (Exception ee)
                {
                UpdateStatus(MinDayStatus.Critical, ee.Message + "- Failed attempt to save PM parameters: " + LatestPMAfterDate + " " + LatestPMAfterTime);
         //       Environment.Exit(1);  
                } */
            }


        public void SaveLatestParamSettingsAsJSON()
            {
            try
                {
                var js = new DataContractJsonSerializer(typeof(LastUpdate));
                LastUpdate lu = new LastUpdate();
                lu.PMAfterDate = _PMAfterDate;
                lu.PMAfterTime = _PMAfterTime;
                lu.ABAfterDate = _ABAfterDate;
                lu.ABAfterTime = _ABAfterTime;
                var dir = AppDomain.CurrentDomain.BaseDirectory + System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
                string path = dir + ".json";
                using (var stream = File.Create(path))
                    {
                    js.WriteObject(stream, lu);
                    }
                }
            catch (Exception ee)
                {
                UpdateStatus(MinDayStatus.Critical, ee.Message + "- Failed attempt to save PM parameters: " + _PMAfterDate + " " + _PMAfterTime);
                UpdateStatus(MinDayStatus.Critical, ee.Message + "- Failed attempt to save AB parameters: " + _ABAfterDate + " " + _ABAfterTime);
                //       Environment.Exit(1);  
                }

            }
        public void SavePMUserSettingsAsJSON(String LatestPMAfterDate, String LatestPMAfterTime)
            {
            if (string.Compare(_PMAfterDate, LatestPMAfterDate) > 0)
                UpdateStatus(MinDayStatus.Critical, " New PMAfterDate is less than previous one:" + LatestPMAfterDate + " " + _PMAfterDate);

            _PMAfterDate = LatestPMAfterDate;
            _PMAfterTime = LatestPMAfterTime;


           try
                {

                var js = new DataContractJsonSerializer(typeof(LastUpdate));
                LastUpdate lu = new LastUpdate();
                lu.PMAfterDate = _PMAfterDate;
                lu.PMAfterTime = _PMAfterTime;
                lu.ABAfterDate = _ABAfterDate;
                lu.ABAfterTime = _ABAfterTime;
                var dir = AppDomain.CurrentDomain.BaseDirectory + System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
                string path = dir + ".json";
                using (var stream = File.Create(path))
                    {
                    js.WriteObject(stream, lu);
                    }
                }
            catch (Exception ee)
                {
                UpdateStatus(MinDayStatus.Critical, ee.Message + "- Failed attempt to save PM parameters: " + LatestPMAfterDate + " " + LatestPMAfterTime);
                //       Environment.Exit(1);  
                }
            }

        public void SaveABUserSettings(String LatestABAfterDate, String LatestABAfterTime)
            {
            if (string.Compare(_ABAfterDate, LatestABAfterDate) < 0)
                UpdateStatus(MinDayStatus.Critical, " New ABAfterDate is less than previous one:" + LatestABAfterDate + " " + _ABAfterDate);

            _ABAfterDate = LatestABAfterDate;
            _ABAfterTime = LatestABAfterTime;
            SaveLatestParamSettingsAsJSON();
            return;
/*            try
                {
                Configuration conf = ConfigurationManager.OpenExeConfiguration(System.Reflection.Assembly.GetExecutingAssembly().Location);

                String SectionNameString = "userSettings/" + Assembly.GetExecutingAssembly().GetName().Name + ".Properties.Settings";
                var us = (System.Configuration.ClientSettingsSection)conf.GetSection(SectionNameString);

                (us.Settings.Get("ABAfterDate")).Value.ValueXml.InnerXml = LatestABAfterDate;
                (us.Settings.Get("ABAfterTime")).Value.ValueXml.InnerXml = LatestABAfterTime;

                us.SectionInformation.ForceSave = true;
                conf.Save(ConfigurationSaveMode.Modified);
                }
            catch (Exception ee)
                {
                UpdateStatus(MinDayStatus.Critical, ee.Message + "- Failed attempt to save AB parameters: " + LatestABAfterDate + " " + LatestABAfterTime);
          //      Environment.Exit(1);
                } */
            }

        public void XLoadSettings()
            {
            try
                {
                //         SFIConfigUtils.AssemblyConfig.SetAppConfig(System.Reflection.Assembly.GetExecutingAssembly().Location, Assembly.GetExecutingAssembly().GetName().Name);
                Configuration conf = ConfigurationManager.OpenExeConfiguration(System.Reflection.Assembly.GetExecutingAssembly().Location);

                //     var g = conf.SectionGroups;
                String SectionNameString = "userSettings/" + Assembly.GetExecutingAssembly().GetName().Name + ".Properties.Settings";
                var us = (System.Configuration.ClientSettingsSection)conf.GetSection(SectionNameString);

                //        var pdateSetting = (us.Settings.Get("PMAfterDate"));
                //     var param = ((pdateSetting.Value.ValueXml).LastChild).InnerText.ToString();
                _PMAfterDate = (us.Settings.Get("PMAfterDate")).Value.ValueXml.InnerXml;

                //       us.SectionInformation.ForceSave = true;
                //  conf.Save(ConfigurationSaveMode.Modified);

                //      if (!SFIConfigUtils.AssemblyConfig.Settings.TryGetValue("PMAfterDate", out _PMAfterDate))
                //          throw new Exception("PMAfterDate not set");

                //       if (!SFIConfigUtils.AssemblyConfig.Settings.TryGetValue("PMAfterTime", out _PMAfterTime))
                //           throw new Exception("PMAfterTime not set");
                }
            catch (Exception ee)
                {
                //        MessageBox.Show("Error loading settings: " + ee.Message);
                Environment.Exit(1);
                }

            }



        }   


    [DataContract]
    internal class LastUpdate
        {
        [DataMember]
        internal string PMAfterDate;
        [DataMember]
        internal string PMAfterTime;
        [DataMember]
        internal string ABAfterDate;
        [DataMember]
        internal string ABAfterTime;
        }  


}
