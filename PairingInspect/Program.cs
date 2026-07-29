using System;
using System.Windows.Forms;

namespace PairingInspect
    {
    static class Program
        {
        [STAThread]
        static void Main()
            {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            if (!SFIConfigUtils.RunCheck.OkToRun())
                return;
            Application.Run(new PairingInspectForm());
            }
        }
    }
