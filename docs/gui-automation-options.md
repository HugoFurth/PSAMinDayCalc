# GUI automation options for PairingInspect (WinForms + Telerik)

Roughly lowest to highest effort/robustness, given this app runs on this machine and the
CLI session drives things via Bash (PowerShell is currently broken in this sandbox).

## 1. Win32 message posting (`PostMessage`/`SendMessage` with `BM_CLICK`)

Find the window by class/title (`FindWindow`), find the button's child HWND
(`FindWindowEx`), post a `BM_CLICK`. Works great for plain WinForms controls like
`btnLookUp` -- no extra libraries needed, just P/Invoke in a small C# console app (same
pattern as the reflection probes compiled with `csc.exe` this session). Doesn't need the
window focused or even visible.

Falls short for Telerik's `RadGridView`: it's one owner-drawn control, so individual
cells/rows aren't separate HWNDs -- you can click the grid as a whole but not "the Note
cell in row 5" this way.

## 2. UI Automation (UIA) via a .NET wrapper -- FlaUI or raw `System.Windows.Automation`

Attaches to the running process, walks the automation tree, finds elements by
Name/AutomationId/ControlType, and invokes them semantically ("find the button named
'Look Up', click it"). Standard WinForms controls expose this for free. Telerik WinForms
controls generally ship automation peers too, so `RadGridView` rows/cells are usually at
least partially visible to UIA -- better than raw Win32 for the grid, though
custom-formatted cells (our `ViewCellFormatting` tricks) can sometimes confuse generic
automation.

Requires adding the `FlaUI.UIA3` NuGet package (or referencing
`UIAutomationClient`/`UIAutomationTypes` directly) to a throwaway harness project.

## 3. Coordinate-based input simulation (`SendInput` at x,y)

Simulates real mouse/keyboard hardware events at screen pixel coordinates -- works
regardless of what toolkit drew the pixels, so it's the reliable fallback for Telerik's
custom-painted grid cells specifically.

Downside: fragile -- breaks if the window moves, resizes, or the OS DPI/theme changes;
you have to compute or look up the coordinates first (via UIA's bounding-rectangle
query, or visually).

## 4. Screenshot + vision-guided clicking ("computer use" style)

Capture the desktop/window (e.g. `Graphics.CopyFromScreen` in a small helper), feed the
image to Claude via the Read tool (which does support images), have it identify button
coordinates visually, then click via #3.

This is the most "actually see what you see" approach, but there's no built-in
screenshot or click tool wired into this Claude Code session for the native Windows
desktop today -- Artifact/browser tooling doesn't reach outside the sandboxed web view.
Both the screenshot capture and the click-injection pieces would need to be built as
throwaway executables.

## 5. Telerik's own test automation framework (ArtOfTest / ex-Telerik Testing Framework)

Purpose-built for Telerik WinForms controls, understands `RadGridView` internals (rows,
cells, columns) natively rather than guessing through generic UIA. Most "correct" for
this app's grid specifically, if it's still available/licensed for this old 2022.1
Telerik version -- not yet checked.

## 6. Skip GUI automation, test the logic directly

For anything that's really about business logic rather than pixels (does "Look Up" call
`Assemble` correctly, does the grid data come out right), the throwaway-harness pattern
used all session -- compile a small console app referencing the same DLLs, call the
methods directly -- verifies correctness without touching the UI at all. It just can't
tell you whether it *renders* right.

## Bottom line

Given this session's toolchain (Bash + `csc.exe`, no working PowerShell, no native
click/screenshot tool), **#1 for buttons** and **#2 for the grid** would be cheapest to
stand up if the goal is letting Claude drive the app. **#4** would be the most visually
satisfying but needs new tooling built first.
