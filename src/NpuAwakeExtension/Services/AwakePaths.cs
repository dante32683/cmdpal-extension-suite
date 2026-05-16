using NpuTools.Common;

namespace NpuTools.Awake.Services;

internal static class AwakePaths
{
    public static string SupportDirectory => NpuPaths.EnsureSettingsDirectory("Awake").FullName;

    public static string StatePath => Path.Combine(SupportDirectory, "state.json");

    public static string SchedulesPath => Path.Combine(SupportDirectory, "schedules.json");

    public static string SettingsPath => Path.Combine(SupportDirectory, "settings.json");

    public static string DaemonPidPath => Path.Combine(SupportDirectory, "daemon.pid");

    public static string HeartbeatPath => Path.Combine(SupportDirectory, "heartbeat.json");

    public static string StopFlagPath => Path.Combine(SupportDirectory, "stop.flag");

    public static string KeeperExePath
    {
        get
        {
            string baseDir = AppContext.BaseDirectory;
            string packagedPath = Path.Combine(baseDir, "Tools", "NpuAwakeKeeper.exe");
            if (File.Exists(packagedPath))
            {
                return packagedPath;
            }

            string devPath = Path.GetFullPath(Path.Combine(
                baseDir,
                "..",
                "..",
                "..",
                "..",
                "..",
                "NpuAwakeKeeper",
                "bin",
                "x64",
                "Debug",
                "net9.0-windows10.0.26100.0",
                "win-x64",
                "NpuAwakeKeeper.exe"));

            return File.Exists(devPath) ? devPath : packagedPath;
        }
    }
}
