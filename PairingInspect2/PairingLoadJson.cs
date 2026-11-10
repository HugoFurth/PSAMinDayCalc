using System.Collections.Generic;

namespace PairingInspect2
{
    // Mirrors D:\Data\VS\CrewRec\customer-docs\pairing-load-schema.json (the format
    // CrewRec/the AA Solver use to hand a not-yet-loaded proposed pairing to CrewTrac).
    // Property names deliberately match the JSON keys verbatim (snake_case) so
    // JavaScriptSerializer can deserialize by reflection with no attributes needed --
    // only ever read, never re-serialized, so the naming-convention mismatch is a
    // one-way, contained cost.
    //
    // One flat leg class covers all three "type" variants (2=flight, 4=deadhead,
    // 5=activity) instead of true polymorphism -- JavaScriptSerializer has no
    // discriminator support, and the only consumer (BuildProposedPairingRows) already
    // has to switch on "type" itself to know which fields apply.

    public class PairingLoadRoot
    {
        public string version { get; set; }
        public List<PairingLoadPairing> pairings { get; set; }
    }

    public class PairingLoadPairing
    {
        public PairingLoadHeader header { get; set; }
        public List<PairingLoadDuty> dutyPeriods { get; set; }
    }

    public class PairingLoadHeader
    {
        public PairingLoadPairingId pairing_id { get; set; }
        public string report_date { get; set; }
        public string report_time { get; set; }
        public string skdend_date { get; set; }
        public string skdend_time { get; set; }
        public List<PairingLoadPosition> position { get; set; }
    }

    public class PairingLoadPairingId
    {
        public string pairing_number { get; set; }
        public string pairing_base { get; set; }
        public string pairing_eqpt { get; set; }
        public string pairing_date { get; set; }
    }

    public class PairingLoadPosition
    {
        public string code { get; set; }
        public int count { get; set; }
    }

    public class PairingLoadDuty
    {
        public int duty_num { get; set; }
        public string start_date { get; set; }
        public string start_time { get; set; }
        public string end_date { get; set; }
        public string end_time { get; set; }
        public string hotel_name { get; set; }
        public string hotel_phone { get; set; }
        public List<PairingLoadLeg> legs { get; set; }
    }

    public class PairingLoadLeg
    {
        public int type { get; set; } // 2 = flight, 4 = deadhead, 5 = non-flying activity
        public string flight_num { get; set; }
        public string transaction_code { get; set; } // type 5 only
        public string oad { get; set; } // type 4 only -- other-airline-deadhead code
        public string dept_city { get; set; }
        public string dept_city_date { get; set; }
        public string dept_city_time { get; set; }
        public string arrv_city { get; set; }
        public string arrv_city_date { get; set; }
        public string arrv_city_time { get; set; }
        public string block_time { get; set; } // type 2 only
        public string credit { get; set; } // type 2 required, type 4 optional
        public string deadhead_ind { get; set; } // "D" or "R" -- type 4, optionally type 2
    }
}
