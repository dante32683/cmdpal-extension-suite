using System.Runtime.InteropServices;

namespace SimpleAnalyticsExtension;

internal static partial class NativeMethods
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct SYSTEM_POWER_STATUS
    {
        public byte ACLineStatus;       // 0=offline, 1=online, 255=unknown
        public byte BatteryFlag;        // bit 3=charging, bit 7=no battery
        public byte BatteryLifePercent; // 0-100, 255=unknown
        public byte SystemStatusFlag;   // bit 0=energy saver active
        public uint BatteryLifeTime;    // seconds remaining, 0xFFFFFFFF=unknown
        public uint BatteryFullLifeTime;
    }

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS status);

    // FILETIME structs map cleanly to long on x64 (little-endian low/high DWORD pair)
    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetSystemTimes(out long idleTime, out long kernelTime, out long userTime);
}
