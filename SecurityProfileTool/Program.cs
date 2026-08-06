using System;
using SFIConfigUtils;

namespace SecurityProfileTool
    {
    // Regenerates CTSecurityProfile.bin in the current directory, capturing whoever
    // is currently logged into CrewTrac on this machine. No existing tool in this
    // codebase does this -- CTSecurity.SaveSecurityProfile() was previously unused --
    // so MinDayProcess()'s parameterless constructor (the scheduled/unattended path)
    // had no supported way to rotate which identity it acts as.
    class Program
        {
        static void Main()
            {
            try
                {
                if (!RunCheck.OkToRun())
                    return;

                CTSecurity.SaveSecurityProfile();
                TCTSecurityItem saved = CTSecurity.Security;

                Console.WriteLine("CTSecurityProfile.bin saved to " + Environment.CurrentDirectory);
                Console.WriteLine("  iUserID    = " + saved.iUserID);
                Console.WriteLine("  szUserName = " + saved.szUserName);
                }
            catch (Exception ex)
                {
                Console.WriteLine("Failed to save security profile: " + ex.Message);
                }

            Console.WriteLine();
            Console.WriteLine("Press Enter to exit...");
            Console.ReadLine();
            }
        }
    }
