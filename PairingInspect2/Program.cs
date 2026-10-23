using System;
using System.Linq;
using System.Windows.Forms;

namespace PairingInspect2
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            if (!SFIConfigUtils.RunCheck.OkToRun())
                return;
            Application.Run(new RadForm1());
        }
    }
}