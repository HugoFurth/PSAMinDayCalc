using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace PairingInspect2
    {
    // Launches ctwms.exe straight onto one employee's Master Schedule for a given
    // month. Unlike CtwpmSelectionAutomator, no window-click automation is needed here:
    // traced in Z:\Hugo\CT Source\Lib\Forms\Schedule\MSSkedForm.cpp (OpenSked,
    // commandLineLaunchValuesPassed, wasLaunchedViaCommandLine) -- when both command-line
    // params below are present, bBypassEmpPopup is set true and CTWMS skips its own
    // employee-selection popup (frmEmpPopup) entirely, calling Sked->Assemble() directly.
    public static class CtwmsSelectionAutomator
        {
        private const string CtwmsExeName = "ctwms.exe";

        // ctwms.exe reads named params via TParameterStringList (Z:\Hugo\CT
        // Source\Lib\CtSysUtil\ParameterStringClass.cpp): each process arg is
        // "/Name=Value" (leading slash optional, name matched case-insensitively).
        // InternalEmployeeNumber is the plain empno (MSSkedForm.cpp:258). BidPeriod must
        // be "MMMYY" -- 3-letter month abbreviation + 2-digit year, e.g. "SEP25" (parsed
        // via TCTBidPeriod(..., bfMMMYY), which matches it against
        // TCTBidPeriodPool::bidNumberToDisplay's output) -- NOT CrewTrac's usual 6-digit
        // YYYYMM.
        public static Process Launch(string ctExeDir, uint empNum, string internalDateYYYYMMDD)
            {
            string bidPeriod = ToMMMYY(internalDateYYYYMMDD);
            return Process.Start(Path.Combine(ctExeDir, CtwmsExeName),
                "/InternalEmployeeNumber=" + empNum + " /BidPeriod=" + bidPeriod);
            }

        private static string ToMMMYY(string internalDateYYYYMMDD)
            {
            DateTime date = DateTime.ParseExact(internalDateYYYYMMDD, "yyyyMMdd", CultureInfo.InvariantCulture);
            string month = date.ToString("MMM", CultureInfo.InvariantCulture).ToUpperInvariant();
            string year2 = (date.Year % 100).ToString("D2", CultureInfo.InvariantCulture);
            return month + year2;
            }
        }
    }
