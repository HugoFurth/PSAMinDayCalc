using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace PairingInspect2
    {
    // Launches ctwpm.exe and automates past its "Pairing Maintenance Selection" dialog, so the
    // user lands directly on the interactive pairing screen instead of having to click OK
    // themselves.
    public static class CtwpmSelectionAutomator
        {
        private const string CtwpmExeName = "ctwpm.exe";

        // ctwpm.exe parses positional args: <FUNCTION> <PrgNo> <PrgDate:YYYYMMDD> -- see
        // PMSelectionForm.cpp:654-708 in the CTWPM source.
        public const string FunctionInquire = "INQUIRE";
        public const string FunctionModify = "MODIFY";

        public static Process Launch(string ctExeDir, string function, string prgId, string prgDate)
            {
            Process ctwpmProcess = Process.Start(Path.Combine(ctExeDir, CtwpmExeName),
                function + " " + prgId + " " + prgDate);
            ClickOkButton(ctwpmProcess.Id);
            return ctwpmProcess;
            }

        // No pairing to pre-fill, so nothing to auto-advance past -- just launch.
        public static void Launch(string ctExeDir)
            {
            Process.Start(Path.Combine(ctExeDir, CtwpmExeName));
            }

        // Launches ctwpm.exe in Inquire mode, auto-clicks past the selection dialog, finds and
        // triggers a real Evaluate (Inquire's Process-Transaction button, captioned "Eval&uate",
        // calls actionEvaluate() -- PMPrgForm.cpp:2841 and :344), and closes the window
        // afterward, entirely hands-off.
        //
        // Two things this deliberately does NOT do, both tried and rejected during testing:
        //  - Launching with the SW_SHOWMINIMIZED STARTUPINFO flag (CTWPM.cpp:108-115's
        //    "ad-hoc cross process flag from ctwms", g_bLaunchedByMS): this hits a real VCL bug
        //    in CTWPM's own startup code (Application->MainForm->WindowState set from inside its
        //    own OnShow/OnHide handler) -- confirmed live: "Cannot change Visible in OnShow or
        //    OnHide". And even where it doesn't crash, it only auto-skips the selection dialog;
        //    it never runs Evaluate or closes the app by itself (confirmed against the actual
        //    production caller, MSSkedForm.cpp:2909-2923, which sets this same flag and then
        //    explicitly polls GetExitCodeProcess in a loop waiting for the user to finish).
        //  - Minimizing the pairing window (ShowWindow(hwnd, SW_MINIMIZE)) before the Evaluate
        //    click: btnProcessTranClick's own code does
        //    "ActiveControl = btnProcessTran; if (ActiveControl != btnProcessTran) Abort();" --
        //    that assignment silently fails when the form is minimized, so the click is
        //    delivered and processed but the handler no-ops before ever reaching
        //    actionEvaluate(). Confirmed live: clean exit, no crash, but the pairing's values
        //    were unchanged.
        //
        // What does work: moving the window off any visible monitor with SetWindowPos instead of
        // minimizing it. The window stays in its normal (non-minimized) state, so ActiveControl
        // assignment behaves exactly as it does when visible -- it's just not rendered anywhere
        // the user can see it. Confirmed live: real Evaluate ran (ActCredit/timestamp both
        // changed as expected), no crash, clean auto-close.
        public static Process LaunchAndEvaluate(string ctExeDir, string prgId, string prgDate)
            {
            Process ctwpmProcess = Process.Start(Path.Combine(ctExeDir, CtwpmExeName),
                FunctionInquire + " " + prgId + " " + prgDate);
            ClickOkButton(ctwpmProcess.Id);

            IntPtr pairingWnd = ClickButtonAnywhere((uint)ctwpmProcess.Id, "Eval&uate", 200, 50, moveOffscreenFirst: true);
            if (pairingWnd != IntPtr.Zero)
                {
                // Give the synchronous DB transaction (ct2710, a real BEGIN/END Btrieve
                // transaction) time to finish before closing, rather than racing it.
                Thread.Sleep(750);
                if (IsWindow(pairingWnd))
                    PostMessage(pairingWnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                }

            return ctwpmProcess;
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

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        private const uint BM_CLICK = 0x00F5;
        private const uint WM_CLOSE = 0x0010;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const int OFFSCREEN_XY = -32000;
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

        private static IntPtr FindButtonByCaption(IntPtr hWndParent, string caption)
            {
            IntPtr found = IntPtr.Zero;
            EnumChildWindows(hWndParent, delegate(IntPtr h, IntPtr l)
                {
                StringBuilder sbClass = new StringBuilder(256);
                StringBuilder sbText = new StringBuilder(256);
                GetClassName(h, sbClass, 256);
                GetWindowText(h, sbText, 256);
                if (sbClass.ToString() == "TButton" && sbText.ToString() == caption)
                    {
                    found = h;
                    return false;
                    }
                return true;
                }, IntPtr.Zero);
            return found;
            }

        // Unlike ClickOkButton/FindSelectionWindow, this doesn't assume a known parent window
        // class -- PMPrgForm's class name was never identified, so this scans every top-level
        // window belonging to the process instead. Retries and re-resolves everything on every
        // pass for the same reason ClickOkButton does (forms get destroyed/recreated shortly
        // after first appearing). Returns the top-level window the button was found on (so the
        // caller can close that same window afterward), or IntPtr.Zero if never found. When
        // moveOffscreenFirst is true, the window is repositioned off any visible monitor
        // (SetWindowPos, SWP_NOACTIVATE) immediately before the click -- NOT the same as
        // minimizing it, which breaks btnProcessTranClick's own ActiveControl check (see
        // LaunchAndEvaluate's comment for the full story).
        private static IntPtr ClickButtonAnywhere(uint pid, string caption, int maxAttempts, int delayMs, bool moveOffscreenFirst = false)
            {
            for (int i = 0; i < maxAttempts; i++)
                {
                IntPtr foundBtn = IntPtr.Zero;
                IntPtr foundParent = IntPtr.Zero;
                EnumWindows(delegate(IntPtr h, IntPtr l)
                    {
                    uint winPid;
                    GetWindowThreadProcessId(h, out winPid);
                    if (winPid == pid)
                        {
                        IntPtr btn = FindButtonByCaption(h, caption);
                        if (btn != IntPtr.Zero)
                            {
                            foundBtn = btn;
                            foundParent = h;
                            return false;
                            }
                        }
                    return true;
                    }, IntPtr.Zero);

                if (foundBtn != IntPtr.Zero)
                    {
                    if (moveOffscreenFirst)
                        SetWindowPos(foundParent, IntPtr.Zero, OFFSCREEN_XY, OFFSCREEN_XY, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
                    PostMessage(foundBtn, BM_CLICK, IntPtr.Zero, IntPtr.Zero);
                    return foundParent;
                    }

                Thread.Sleep(delayMs);
                Application.DoEvents();
                }
            return IntPtr.Zero;
            }
        }
    }
