using System;
using System.IO;
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
            // No handler existed at all before this -- confirmed live: looking up a
            // cancelled pairing closed the whole app with no error dialog whatsoever,
            // not even "Error looking up pairing" (btnLookUp_Click's own try/catch),
            // meaning whatever's throwing isn't reaching that handler at all. Both
            // hooks below log to a crash file before the process goes down --
            // AppDomain.UnhandledException still fires even for a StackOverflowException
            // (which no try/catch can ever intercept, and which crashes with zero
            // dialog by design -- matches this symptom exactly), though it can't
            // prevent termination either way.
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                LogCrash("AppDomain.UnhandledException", e.ExceptionObject as Exception, e.ExceptionObject);
            Application.ThreadException += (s, e) =>
                LogCrash("Application.ThreadException", e.Exception, null);
            // Without this, WinForms can route a UI-thread exception straight to the
            // OS-level crash handler instead of the ThreadException event above.
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            if (!SFIConfigUtils.RunCheck.OkToRun())
                return;
            Application.Run(new RadForm1());
        }

        private static void LogCrash(string source, Exception ex, object rawObject)
        {
            try
            {
                string text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  [" + source + "]" + Environment.NewLine +
                    (ex != null ? ex.ToString() : "(non-Exception object: " + rawObject + ")") + Environment.NewLine +
                    "----------------------------------------" + Environment.NewLine;
                File.AppendAllText("PrgInsp_crash.log", text);
            }
            catch
            {
                // Logging itself must never be what brings the process down.
            }
        }
    }
}