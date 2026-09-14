using System;
using System.IO;
using System.Runtime.InteropServices;

namespace NpuTools.Organize.Services;

internal static partial class OrganizePowerPolicy
{
    private static readonly string ConfigPath = Path.Combine(
        Environment.GetEnvironmentVariable("LOCALAPPDATA")
            ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NpuOrganize", "config.json");

    internal static bool ShouldDeferNpuWork() =>
        OrganizeSettings.IsSkipOnBatteryEnabled(ConfigPath) && !IsOnAcPower();

    private static bool IsOnAcPower()
    {
        if (!GetSystemPowerStatus(out SystemPowerStatus status)) return true;
        return status.ACLineStatus != 0;
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetSystemPowerStatus(out SystemPowerStatus systemPowerStatus);

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        internal byte ACLineStatus;
        internal byte BatteryFlag;
        internal byte BatteryLifePercent;
        internal byte SystemStatusFlag;
        internal int BatteryLifeTime;
        internal int BatteryFullLifeTime;
    }
}
