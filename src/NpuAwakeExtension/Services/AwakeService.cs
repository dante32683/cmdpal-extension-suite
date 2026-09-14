using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using NpuTools.Awake.Models;

namespace NpuTools.Awake.Services;

internal sealed class AwakeService
{
    [SuppressMessage("Performance", "CA1822", Justification = "Service method — uniform call site via injection.")]
    public AwakeSettings GetSettings()
    {
        var settings = AwakeJson.Read(AwakePaths.SettingsPath, new AwakeSettings(), AwakeJsonContext.Default.AwakeSettings);
        settings.DefaultAwakeMode = settings.DefaultAwakeMode == "screen-off" ? "screen-off" : "indefinite";
        settings.DefaultDurationMinutes = Math.Clamp(settings.DefaultDurationMinutes, 1, 24 * 60);
        return settings;
    }

    [SuppressMessage("Performance", "CA1822", Justification = "Service method — uniform call site via injection.")]
    public void SaveSettings(AwakeSettings settings)
    {
        AwakeJson.AtomicWrite(AwakePaths.SettingsPath, settings, AwakeJsonContext.Default.AwakeSettings);
    }

    public AwakeStatus GetStatus()
    {
        var state = AwakeJson.Read(AwakePaths.StatePath, new AwakeStateFile(), AwakeJsonContext.Default.AwakeStateFile);
        var schedules = GetSchedules();
        int? pid = ReadDaemonPid();
        // Only discard the pid file on a definite mismatch; an undecidable
        // answer must not delete the record of a daemon that is still running.
        if (pid is int value && InspectDaemon(value) == DaemonTrust.NotOurs)
        {
            pid = null;
            TryDelete(AwakePaths.DaemonPidPath);
        }

        return new AwakeStatus
        {
            DaemonPid = pid,
            Override = NormalizeOverride(state.Override),
            Schedules = schedules,
            Heartbeat = AwakeJson.Read(AwakePaths.HeartbeatPath, null!, AwakeJsonContext.Default.AwakeHeartbeat),
        };
    }

    [SuppressMessage("Performance", "CA1822", Justification = "Service method — uniform call site via injection.")]
    public IReadOnlyList<AwakeSchedule> GetSchedules()
    {
        return AwakeJson.Read(AwakePaths.SchedulesPath, new List<AwakeSchedule>(), AwakeJsonContext.Default.ListAwakeSchedule)
            .Where(s => s is not null && IsValidSchedule(s))
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
        var previous = AwakeJson.Read(AwakePaths.StatePath, new AwakeStateFile(), AwakeJsonContext.Default.AwakeStateFile);
        AwakeJson.AtomicWrite(AwakePaths.StatePath, awakeOverride is null ? new AwakeStateFile() : new AwakeStateFile { Override = awakeOverride }, AwakeJsonContext.Default.AwakeStateFile);

        var schedules = GetSchedules();
        if (awakeOverride is not null || schedules.Count > 0)
        {
            if (EnsureDaemonRunning())
            {
                return true;
            }

            AwakeJson.AtomicWrite(AwakePaths.StatePath, previous, AwakeJsonContext.Default.AwakeStateFile);
            return false;
        }

        StopDaemon();
        return true;
    }

    public void SetSchedules(IEnumerable<AwakeSchedule> schedules)
    {
        var safe = schedules.Where(IsValidSchedule).ToList();
        AwakeJson.AtomicWrite(AwakePaths.SchedulesPath, safe, AwakeJsonContext.Default.ListAwakeSchedule);
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

    [SuppressMessage("Performance", "CA1822", Justification = "Service method — uniform call site via injection.")]
    public bool EnsureDaemonRunning()
    {
        // Treat "cannot tell" as already running. Spawning a second daemon is
        // worse than skipping a start we did not need to make.
        int? currentPid = ReadDaemonPid();
        if (currentPid is int pid && InspectDaemon(pid) != DaemonTrust.NotOurs)
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

    [SuppressMessage("Performance", "CA1822", Justification = "Service method — uniform call site via injection.")]
    public void StopDaemon()
    {
        File.WriteAllText(AwakePaths.StopFlagPath, "stop");

        int? pid = ReadDaemonPid();
        if (pid is int value)
        {
            try
            {
                // Kill only on a positive identification. The stop flag written
                // above is what shuts down a daemon we cannot confirm.
                if (InspectDaemon(value) == DaemonTrust.Trusted)
                {
                    using var process = Process.GetProcessById(value);
                    process.Kill(entireProcessTree: true);
                }
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
            schedule.Days is { Length: > 0 } &&
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

    // Reading MainModule fails with Access Denied across an elevation or
    // bitness boundary, which is not the same answer as "this is not our
    // daemon" — the two call sites have opposite failure modes, so the
    // undecidable case has to stay distinguishable from a definite mismatch.
    private enum DaemonTrust
    {
        Trusted,
        NotOurs,
        Unknown,
    }

    private static DaemonTrust InspectDaemon(int pid)
    {
        Process process;
        try
        {
            process = Process.GetProcessById(pid);
        }
        catch (ArgumentException)
        {
            // No process with this id — the pid file is stale.
            return DaemonTrust.NotOurs;
        }
        catch
        {
            return DaemonTrust.Unknown;
        }

        using (process)
        {
            try
            {
                if (process.HasExited)
                    return DaemonTrust.NotOurs;
            }
            catch
            {
                return DaemonTrust.Unknown;
            }

            string expectedPath = Path.GetFullPath(AwakePaths.KeeperExePath);
            try
            {
                string? moduleName = process.MainModule?.FileName;
                if (string.IsNullOrEmpty(moduleName))
                    return DaemonTrust.Unknown;

                return string.Equals(Path.GetFullPath(moduleName), expectedPath, StringComparison.OrdinalIgnoreCase)
                    ? DaemonTrust.Trusted
                    : DaemonTrust.NotOurs;
            }
            catch (Win32Exception)
            {
                // Access denied. The image name is still readable and is enough
                // to rule the process out, but not enough to confirm it.
                return NameMatches(process, expectedPath) ? DaemonTrust.Unknown : DaemonTrust.NotOurs;
            }
            catch (InvalidOperationException)
            {
                return DaemonTrust.Unknown;
            }
        }
    }

    private static bool NameMatches(Process process, string expectedPath)
    {
        try
        {
            return string.Equals(
                process.ProcessName,
                Path.GetFileNameWithoutExtension(expectedPath),
                StringComparison.OrdinalIgnoreCase);
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
