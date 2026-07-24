using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace PairingInspect
    {
    // Launches ctwpm.exe and automates past its "Pairing Maintenance Selection" dialog, so the
    // user lands directly on the interactive pairing screen instead of having to click OK
    // themselves.
    public static class CtwpmSelectionAutomator
        {
        private const string CtwpmExeName = "ctwpm.exe";

        // ctwpm.exe parses positional args: <FUNCTION> <PrgNo> <PrgDate:YYYYMMDD> -- see
        // PMSelectionForm.cpp:654-708 in the CTWPM source. INQUIRE opens read-only; MODIFY allows
        // edits, so its selection screen is left for the user to confirm by hand rather than
        // auto-clicked past.
        public const string FunctionInquire = "INQUIRE";
        public const string FunctionModify = "MODIFY";

        public static void Launch(string ctExeDir, string function, string prgId, string prgDate)
            {
            Process ctwpmProcess = Process.Start(Path.Combine(ctExeDir, CtwpmExeName),
                function + " " + prgId + " " + prgDate);
            if (function == FunctionInquire)
                ClickOkButton(ctwpmProcess.Id);
            }

        // No pairing to pre-fill, so nothing to auto-advance past -- just launch.
        public static void Launch(string ctExeDir)
            {
            Process.Start(Path.Combine(ctExeDir, CtwpmExeName));
            }

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern IntPtr PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        private const uint BM_CLICK = 0x00F5;
        private const string SelectionFormClass = "TfrmPMSelection";

        // Prefills the fields (positional args, handled in CTWPM's FormCreate) but still leaves
        // the user to click OK themselves -- this skips that click so they land straight on the
        // interactive pairing screen. The selection dialog's OK button is a real native Win32
        // HWND (a VCL TButton, not owner-drawn like Telerik's grids), registered under VCL's own
        // window class "TButton" -- NOT the generic system "Button" class -- so BM_CLICK reliably
        // fires its OnClick exactly as a mouse click would.
        //
        // Uses EnumWindows/EnumChildWindows rather than FindWindow/FindWindowEx: testing showed
        // FindWindow fails to locate this exact window even when EnumWindows finds the identical
        // HWND (matching class and title) moments apart, in-process and from an external harness
        // alike. Root cause unconfirmed, but EnumWindows/EnumChildWindows proved reliable, so this
        // avoids FindWindow/FindWindowEx entirely. Scoped to the PID we just launched, in case
        // another CTWPM instance is already open.
        //
        // Deliberately does NOT use CTWPM's own "launched by MS" auto-accept mode (SW_SHOWMINIMIZED
        // startup flag): that mode also runs the selected function to completion and closes the
        // whole app afterward, which would defeat the purpose here of leaving CTWPM open for the
        // user.
        //
        // Re-resolves the selection window AND its OK button together on every retry, rather than
        // caching the window handle once found -- testing showed CTWPM's selection form gets
        // destroyed and recreated shortly after it first appears (the first HWND we find fails
        // IsWindow moments later), so a button lookup scoped to a once-found window handle can
        // silently target a form that's already gone. Re-resolving both each pass means a
        // recreated window is picked up naturally instead of leaving the search stuck on a stale
        // handle.
        private static void ClickOkButton(int ctwpmProcessId)
            {
            uint pid = (uint)ctwpmProcessId;
            IntPtr hBtn = IntPtr.Zero;
            for (int i = 0; i < 200 && hBtn == IntPtr.Zero; i++)
                {
                IntPtr hWnd = FindSelectionWindow(pid);
                if (hWnd != IntPtr.Zero)
                    hBtn = FindOkButton(hWnd);

                if (hBtn == IntPtr.Zero)
                    {
                    Thread.Sleep(50);
                    Application.DoEvents();
                    }
                }

            if (hBtn != IntPtr.Zero)
                PostMessage(hBtn, BM_CLICK, IntPtr.Zero, IntPtr.Zero);
            }

        private static IntPtr FindSelectionWindow(uint pid)
            {
            IntPtr found = IntPtr.Zero;
            EnumWindows(delegate(IntPtr h, IntPtr l)
                {
                uint winPid;
                GetWindowThreadProcessId(h, out winPid);
                if (winPid == pid)
                    {
                    StringBuilder sbClass = new StringBuilder(256);
                    GetClassName(h, sbClass, 256);
                    if (sbClass.ToString() == SelectionFormClass)
                        {
                        found = h;
                        return false;
                        }
                    }
                return true;
                }, IntPtr.Zero);
            return found;
            }

        private static IntPtr FindOkButton(IntPtr hWndParent)
            {
            IntPtr found = IntPtr.Zero;
            EnumChildWindows(hWndParent, delegate(IntPtr h, IntPtr l)
                {
                StringBuilder sbClass = new StringBuilder(256);
                StringBuilder sbText = new StringBuilder(256);
                GetClassName(h, sbClass, 256);
                GetWindowText(h, sbText, 256);
                if (sbClass.ToString() == "TButton" && sbText.ToString() == "OK")
                    {
                    found = h;
                    return false;
                    }
                return true;
                }, IntPtr.Zero);
            return found;
            }
        }
    }
