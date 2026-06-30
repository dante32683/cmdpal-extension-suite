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

    [StructLayout(LayoutKind.Sequential)]
    internal struct MSG
    {
        public nint hwnd;
        public uint message;
        public nint wParam;
        public nint lParam;
        public uint time;
        public int pt_x;
        public int pt_y;
    }

    [LibraryImport("user32.dll", EntryPoint = "GetMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetMessage(out MSG lpMsg, nint hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool TranslateMessage(ref MSG lpMsg);

    [LibraryImport("user32.dll", EntryPoint = "DispatchMessageW")]
    internal static partial nint DispatchMessage(ref MSG lpMsg);

    [LibraryImport("user32.dll", EntryPoint = "PostThreadMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool PostThreadMessage(uint idThread, uint Msg, nint wParam, nint lParam);

    [LibraryImport("kernel32.dll")]
    internal static partial uint GetCurrentThreadId();

    internal const uint WM_QUIT = 0x0012;

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
