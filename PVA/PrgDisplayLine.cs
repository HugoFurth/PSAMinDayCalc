using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SFICTDateTimeUtils;

namespace PVA
    {
    public class PrgDisplayLine
        {
        public DateTime Date { get; set;}
        public String OA { get; set;}
        public String Flight { get; set; }
        public String Dhd { get; set; }
        public String DeptCity { get; set;}
        public String ArrvCity { get; set;}
        public DateTime DeptTime { get; set; }
        public DateTime ArrvTime { get; set; }
        public int Block { get; set; }
        public int Credit { get; set; }
        public String PickDrop { get; set; }

        String SkedDeptTimeasHHMM { get; }
        String SkedArrvTimeasHHMM { get; }

        DateTimeWithGMTVar LatestDept { get; }
        DateTimeWithGMTVar LatestArrv { get; }


        String CodeType { get; }
        }
    }
