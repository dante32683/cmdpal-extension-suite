using System;
using System.Runtime.InteropServices;

namespace NpuClipboardKeeper;

internal static partial class NativeMethods
{
    [LibraryImport("user32.dll")]
    internal static partial uint GetClipboardSequenceNumber();

    [LibraryImport("user32.dll")]
    private static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll")]
    private static partial uint GetWindowThreadProcessId(nint hWnd, out uint processId);

    internal static string ForegroundApplication()
    {
        try
        {
            nint hwnd = GetForegroundWindow();
            _ = GetWindowThreadProcessId(hwnd, out uint pid);
            using var process = System.Diagnostics.Process.GetProcessById((int)pid);
            return process.ProcessName;
        }
        catch
        {
            return string.Empty;
        }
    }
}
