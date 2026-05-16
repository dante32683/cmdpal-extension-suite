using System.Diagnostics;
using NpuTools.Awake.Models;

namespace NpuTools.Awake.Services;

internal sealed class AwakeService
{
    public AwakeSettings GetSettings()
    {
        var settings = AwakeJson.Read(AwakePaths.SettingsPath, new AwakeSettings());
        settings.DefaultAwakeMode = settings.DefaultAwakeMode == "screen-off" ? "screen-off" : "indefinite";
        settings.DefaultDurationMinutes = Math.Clamp(settings.DefaultDurationMinutes, 1, 24 * 60);
        return settings;
    }

    public void SaveSettings(AwakeSettings settings)
    {
        AwakeJson.AtomicWrite(AwakePaths.SettingsPath, settings);
    }

    public AwakeStatus GetStatus()
    {
        var state = AwakeJson.Read(AwakePaths.StatePath, new AwakeStateFile());
        var schedules = GetSchedules();
        int? pid = ReadDaemonPid();
        if (pid is int value && !IsPidAlive(value))
        {
            pid = null;
            TryDelete(AwakePaths.DaemonPidPath);
        }

        return new AwakeStatus
        {
            DaemonPid = pid,
            Override = NormalizeOverride(state.Override),
            Schedules = schedules,
            Heartbeat = AwakeJson.Read<AwakeHeartbeat?>(AwakePaths.HeartbeatPath, null),
        };
    }

    public IReadOnlyList<AwakeSchedule> GetSchedules()
    {
        return AwakeJson.Read(AwakePaths.SchedulesPath, new List<AwakeSchedule>())
            .Where(IsValidSchedule)
            .ToList();
    }

    public bool ToggleDefaultAwake()
    {
        var settings = GetSettings();
        var status = GetStatus();
        if (status.Override is { ExpiryEpochSeconds: null } ov && ov.Mode == settings.DefaultAwakeMode)
        {
            return SetOverride(null);
        }

        return SetOverride(new AwakeOverride { Mode = settings.DefaultAwakeMode });
    }

    public bool SetOverride(AwakeOverride? awakeOverride)
    {
        var previous = AwakeJson.Read(AwakePaths.StatePath, new AwakeStateFile());
        AwakeJson.AtomicWrite(AwakePaths.StatePath, awakeOverride is null ? new AwakeStateFile() : new AwakeStateFile { Override = awakeOverride });

        var schedules = GetSchedules();
        if (awakeOverride is not null || schedules.Count > 0)
        {
            if (EnsureDaemonRunning())
            {
                return true;
            }

            AwakeJson.AtomicWrite(AwakePaths.StatePath, previous);
            return false;
        }

        StopDaemon();
        return true;
    }

    public void SetSchedules(IEnumerable<AwakeSchedule> schedules)
    {
        var safe = schedules.Where(IsValidSchedule).ToList();
        AwakeJson.AtomicWrite(AwakePaths.SchedulesPath, safe);
        if (safe.Count > 0)
        {
            EnsureDaemonRunning();
            return;
        }

        if (GetStatus().Override is null)
        {
            StopDaemon();
        }
    }

    public void AddSchedule(AwakeSchedule schedule)
    {
        var schedules = GetSchedules().ToList();
        schedules.Add(schedule);
        SetSchedules(schedules);
    }

    public void ToggleSchedule(string id)
    {
        SetSchedules(GetSchedules().Select(s =>
        {
            if (s.Id == id)
            {
                s.Enabled = !s.Enabled;
            }

            return s;
        }));
    }

    public void DeleteSchedule(string id)
    {
        SetSchedules(GetSchedules().Where(s => s.Id != id));
    }

    public bool EnsureDaemonRunning()
    {
        int? currentPid = ReadDaemonPid();
        if (currentPid is int pid && IsPidAlive(pid))
        {
            return true;
        }

        TryDelete(AwakePaths.StopFlagPath);
        string keeperPath = AwakePaths.KeeperExePath;
        if (!File.Exists(keeperPath))
        {
            return false;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = keeperPath,
            WorkingDirectory = Path.GetDirectoryName(keeperPath)!,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        };
        startInfo.ArgumentList.Add("daemon");
        startInfo.ArgumentList.Add(AwakePaths.SupportDirectory);

        var process = Process.Start(startInfo);
        if (process?.Id is not int newPid)
        {
            return false;
        }

        File.WriteAllText(AwakePaths.DaemonPidPath, newPid.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return true;
    }

    public void StopDaemon()
    {
        File.WriteAllText(AwakePaths.StopFlagPath, "stop");

        int? pid = ReadDaemonPid();
        if (pid is int value)
        {
            try
            {
                using var process = Process.GetProcessById(value);
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Already stopped or inaccessible.
            }
        }

        TryDelete(AwakePaths.DaemonPidPath);
    }

    private static AwakeOverride? NormalizeOverride(AwakeOverride? ov)
    {
        if (ov is null)
        {
            return null;
        }

        if (ov.ExpiryEpochSeconds is long exp && DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= exp)
        {
            return null;
        }

        ov.Mode = ov.Mode is "timed" or "until" or "screen-off" ? ov.Mode : "indefinite";
        return ov;
    }

    private static bool IsValidSchedule(AwakeSchedule schedule)
    {
        return !string.IsNullOrWhiteSpace(schedule.Id) &&
            schedule.Days.All(d => d is >= 0 and <= 6) &&
            AwakeTime.TryParseHourMinute(schedule.Start, out _) &&
            AwakeTime.TryParseHourMinute(schedule.End, out _);
    }

    private static int? ReadDaemonPid()
    {
        try
        {
            if (!File.Exists(AwakePaths.DaemonPidPath))
            {
                return null;
            }

            string raw = File.ReadAllText(AwakePaths.DaemonPidPath).Trim();
            return int.TryParse(raw, out int pid) && pid > 0 ? pid : null;
        }
        catch
        {
            return null;
        }
    }

    private static bool IsPidAlive(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch
        {
            return false;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best effort cleanup.
        }
    }
}
