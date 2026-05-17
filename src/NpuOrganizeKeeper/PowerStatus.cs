using System.Runtime.InteropServices;

namespace NpuOrganizeKeeper;

internal static class PowerStatus
{
    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEM_POWER_STATUS
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public int BatteryLifeTime;
        public int BatteryFullLifeTime;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS lpSystemPowerStatus);

    /// <summary>
    /// Returns true when on AC power or AC status is unknown (255).
    /// Returns false only when confirmed on battery (0).
    /// </summary>
    public static bool IsOnAcPower()
    {
        if (!GetSystemPowerStatus(out var status)) return true;
        return status.ACLineStatus != 0;
    }
}
