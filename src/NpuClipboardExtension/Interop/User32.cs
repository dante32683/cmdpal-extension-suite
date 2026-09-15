using System;
using System.Runtime.InteropServices;

namespace NpuTools.Clipboard.Interop;

internal static partial class User32
{
    internal const ushort VK_CONTROL = 0x11;
    internal const ushort VK_LSHIFT = 0xA0;
    internal const ushort VK_RSHIFT = 0xA1;
    internal const ushort VK_LCONTROL = 0xA2;
    internal const ushort VK_RCONTROL = 0xA3;
    internal const ushort VK_LMENU = 0xA4;
    internal const ushort VK_RMENU = 0xA5;
    internal const ushort VK_LWIN = 0x5B;
    internal const ushort VK_RWIN = 0x5C;
    internal const ushort VK_V = 0x56;
    internal const uint KEYEVENTF_KEYUP = 0x0002;
    internal const int INPUT_KEYBOARD = 1;

    [LibraryImport("user32.dll", SetLastError = true)]
    internal static partial uint SendInput(uint nInputs, ref INPUT pInputs, int cbSize);

    [LibraryImport("user32.dll")]
    internal static partial nint GetForegroundWindow();

    [StructLayout(LayoutKind.Sequential)]
    internal struct INPUT
    {
        public int type;
        public INPUTUNION U;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct INPUTUNION
    {
        [FieldOffset(0)]
        public MOUSEINPUT mi;

        [FieldOffset(0)]
        public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public nuint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public nuint dwExtraInfo;
    }
}
