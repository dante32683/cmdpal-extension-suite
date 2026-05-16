using System.Runtime.InteropServices;

namespace ActionCenterExtension.Interop;

internal static partial class User32
{
    internal const int INPUT_KEYBOARD = 1;
    internal const uint KEYEVENTF_KEYUP = 0x0002;
    internal const ushort VK_LWIN = 0x5B;
    internal const ushort VK_A = 0x41;

    [LibraryImport("user32.dll")]
    internal static partial uint SendInput(uint nInputs, ref INPUT pInputs, int cbSize);


    [StructLayout(LayoutKind.Sequential)]
    internal struct INPUT
    {
        public int type;
        public INPUTUNION union;
    }

    // MOUSEINPUT must be included so the union is the correct size (32 bytes on x64).
    // Without it, INPUT is 32 bytes total instead of 40, and SendInput rejects cbSize silently.
    [StructLayout(LayoutKind.Explicit)]
    internal struct INPUTUNION
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public nint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public nint dwExtraInfo;
    }
}
