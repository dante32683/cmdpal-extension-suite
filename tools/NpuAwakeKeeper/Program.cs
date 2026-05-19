using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

namespace NpuAwakeKeeper;

internal static class Program
{
    private const uint ES_CONTINUOUS = 0x80000000;
    private const uint ES_SYSTEM_REQUIRED = 0x00000001;
    private const uint ES_DISPLAY_REQUIRED = 0x00000002;

    private const uint POWER_REQUEST_CONTEXT_VERSION = 0;
    private const uint POWER_REQUEST_CONTEXT_SIMPLE_STRING = 0x1;
    private static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct REASON_CONTEXT
    {
        public uint Version;
        public uint Flags;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string SimpleReasonString;
    }

    private enum POWER_REQUEST_TYPE
    {
        PowerRequestDisplayRequired = 0,
        PowerRequestSystemRequired = 1,
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr PowerCreateRequest(ref REASON_CONTEXT Context);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PowerSetRequest(IntPtr PowerRequest, POWER_REQUEST_TYPE RequestType);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PowerClearRequest(IntPtr PowerRequest, POWER_REQUEST_TYPE RequestType);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    private static int Main(string[] args)
    {
        if (args.Length < 2 || args[0] != "daemon")
        {
            Console.Error.WriteLine("Usage: NpuAwakeKeeper.exe daemon <supportDir>");
            return 1;
        }

        RunDaemon(args[1]);
        return 0;
    }

    private static void RunDaemon(string supportDir)
    {
        Directory.CreateDirectory(supportDir);

        string schedulesPath = Path.Combine(supportDir, "schedules.json");
        string statePath = Path.Combine(supportDir, "state.json");
        string heartbeatPath = Path.Combine(supportDir, "heartbeat.json");
        string stopPath = Path.Combine(supportDir, "stop.flag");

        if (File.Exists(stopPath))
        {
            File.Delete(stopPath);
        }

        var ctx = new REASON_CONTEXT
        {
            Version = POWER_REQUEST_CONTEXT_VERSION,
            Flags = POWER_REQUEST_CONTEXT_SIMPLE_STRING,
            SimpleReasonString = "NPU Awake - keeping system awake as requested",
        };
        IntPtr powerRequest = PowerCreateRequest(ref ctx);

        bool systemActive = false;
        bool displayActive = false;

        var daemon = new DaemonState(schedulesPath, statePath);
        daemon.StartWatching();

        Console.WriteLine("MODE: daemon");
        Console.WriteLine($"SUPPORT_DIR: {supportDir}");

        try
        {
            while (!File.Exists(stopPath))
            {
                daemon.MaybeReload();
                var decision = daemon.Decide(DateTimeOffset.Now);

                bool wantSystem = (decision.Flags & ES_SYSTEM_REQUIRED) != 0;
                bool wantDisplay = (decision.Flags & ES_DISPLAY_REQUIRED) != 0;

                if (powerRequest != INVALID_HANDLE_VALUE && powerRequest != IntPtr.Zero)
                {
                    if (wantSystem && !systemActive)
                    {
                        PowerSetRequest(powerRequest, POWER_REQUEST_TYPE.PowerRequestSystemRequired);
                        systemActive = true;
                    }
                    else if (!wantSystem && systemActive)
                    {
                        PowerClearRequest(powerRequest, POWER_REQUEST_TYPE.PowerRequestSystemRequired);
                        systemActive = false;
                    }

                    if (wantDisplay && !displayActive)
                    {
                        PowerSetRequest(powerRequest, POWER_REQUEST_TYPE.PowerRequestDisplayRequired);
                        displayActive = true;
                    }
                    else if (!wantDisplay && displayActive)
                    {
                        PowerClearRequest(powerRequest, POWER_REQUEST_TYPE.PowerRequestDisplayRequired);
                        displayActive = false;
                    }
                }

                WriteHeartbeat(heartbeatPath, decision);
                Thread.Sleep(TimeSpan.FromSeconds(15));
            }
        }
        finally
        {
            if (powerRequest != INVALID_HANDLE_VALUE && powerRequest != IntPtr.Zero)
            {
                if (systemActive) PowerClearRequest(powerRequest, POWER_REQUEST_TYPE.PowerRequestSystemRequired);
                if (displayActive) PowerClearRequest(powerRequest, POWER_REQUEST_TYPE.PowerRequestDisplayRequired);
                CloseHandle(powerRequest);
            }
        }
    }

    private static void WriteHeartbeat(string path, AwakeDecision decision)
    {
        try
        {
            AtomicWriteJson(path, new
            {
                timestampUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                isActive = decision.IsActive,
                reason = decision.Reason,
            });
        }
        catch
        {
            // Heartbeat failure should not stop sleep prevention.
        }
    }

    private sealed class DaemonState
    {
        private readonly string _schedulesPath;
        private readonly string _statePath;
        private FileSystemWatcher? _watcher;
        private volatile int _reloadRequested = 1;
        private List<AwakeSchedule> _schedules = [];
        private AwakeStateFile _state = new();
        private DateTimeOffset _lastLoad = DateTimeOffset.MinValue;

        public DaemonState(string schedulesPath, string statePath)
        {
            _schedulesPath = schedulesPath;
            _statePath = statePath;
        }

        public void StartWatching()
        {
            string dir = Path.GetDirectoryName(_schedulesPath)!;
            _watcher = new FileSystemWatcher(dir)
            {
                IncludeSubdirectories = false,
                EnableRaisingEvents = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime | NotifyFilters.Size,
            };
            _watcher.Changed += (_, e) => OnWatchedFile(e.FullPath);
            _watcher.Created += (_, e) => OnWatchedFile(e.FullPath);
            _watcher.Renamed += (_, e) => OnWatchedFile(e.FullPath);
        }

        public void MaybeReload()
        {
            var now = DateTimeOffset.Now;
            bool periodic = now - _lastLoad > TimeSpan.FromSeconds(30);
            if (!periodic && Interlocked.Exchange(ref _reloadRequested, 0) != 1)
            {
                return;
            }

            _schedules = LoadSchedules(_schedulesPath);
            _state = LoadState(_statePath);
            _lastLoad = now;
        }

        public AwakeDecision Decide(DateTimeOffset nowLocal)
        {
            var ov = _state.Override;
            if (ov != null)
            {
                if (ov.ExpiryEpochSeconds is not long exp || DateTimeOffset.UtcNow.ToUnixTimeSeconds() < exp)
                {
                    return new AwakeDecision(true, GetFlags(ov.Mode), ov.Mode);
                }
            }

            bool activeSchedule = _schedules.Any(s => s.Enabled && IsScheduleActiveNow(s, nowLocal));
            return activeSchedule
                ? new AwakeDecision(true, ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED, "schedule")
                : new AwakeDecision(false, ES_CONTINUOUS, "inactive");
        }

        private void OnWatchedFile(string fullPath)
        {
            string path = Path.GetFullPath(fullPath);
            if (string.Equals(path, Path.GetFullPath(_schedulesPath), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(path, Path.GetFullPath(_statePath), StringComparison.OrdinalIgnoreCase))
            {
                Interlocked.Exchange(ref _reloadRequested, 1);
            }
        }
    }

    private static uint GetFlags(string? mode)
    {
        return string.Equals(mode, "screen-off", StringComparison.OrdinalIgnoreCase)
            ? ES_CONTINUOUS | ES_SYSTEM_REQUIRED
            : ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED;
    }

    private static bool IsScheduleActiveNow(AwakeSchedule schedule, DateTimeOffset nowLocal)
    {
        int dow = (int)nowLocal.DayOfWeek;
        if (schedule.Days is null || schedule.Days.Length == 0 || !schedule.Days.Contains(dow))
        {
            return false;
        }

        if (!TryParseHourMinute(schedule.Start, out var start) || !TryParseHourMinute(schedule.End, out var end))
        {
            return false;
        }

        var current = nowLocal.TimeOfDay;
        if (start == end)
        {
            return true;
        }

        return start < end
            ? current >= start && current < end
            : current >= start || current < end;
    }

    private static bool TryParseHourMinute(string? value, out TimeSpan hourMinute)
    {
        hourMinute = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var parts = value.Split(':', StringSplitOptions.TrimEntries);
        if (parts.Length < 2 ||
            !int.TryParse(parts[0], out int hour) ||
            !int.TryParse(parts[1], out int minute) ||
            hour is < 0 or > 23 ||
            minute is < 0 or > 59)
        {
            return false;
        }

        hourMinute = new TimeSpan(hour, minute, 0);
        return true;
    }

    private static List<AwakeSchedule> LoadSchedules(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return [];
            }

            var schedules = JsonSerializer.Deserialize<List<AwakeSchedule>>(ReadAllTextStable(path), JsonOptions) ?? [];
            return schedules.Where(s => !string.IsNullOrWhiteSpace(s.Id)).ToList();
        }
        catch
        {
            return [];
        }
    }

    private static AwakeStateFile LoadState(string path)
    {
        try
        {
            return File.Exists(path)
                ? JsonSerializer.Deserialize<AwakeStateFile>(ReadAllTextStable(path), JsonOptions) ?? new AwakeStateFile()
                : new AwakeStateFile();
        }
        catch
        {
            return new AwakeStateFile();
        }
    }

    private static string ReadAllTextStable(string path)
    {
        for (int i = 0; i < 5; i++)
        {
            try
            {
                return File.ReadAllText(path);
            }
            catch (IOException)
            {
                Thread.Sleep(30);
            }
        }

        return File.ReadAllText(path);
    }

    private static void AtomicWriteJson(string filePath, object value)
    {
        string tmp = $"{filePath}.{Environment.ProcessId}.{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(value, JsonOptions));
        File.Move(tmp, filePath, true);
    }

    private readonly record struct AwakeDecision(bool IsActive, uint Flags, string? Reason);

    private sealed class AwakeSchedule
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        [JsonPropertyName("days")]
        public int[]? Days { get; set; }

        [JsonPropertyName("start")]
        public string? Start { get; set; }

        [JsonPropertyName("end")]
        public string? End { get; set; }
    }

    private sealed class AwakeStateFile
    {
        [JsonPropertyName("override")]
        public AwakeOverride? Override { get; set; }
    }

    private sealed class AwakeOverride
    {
        [JsonPropertyName("mode")]
        public string Mode { get; set; } = "indefinite";

        [JsonPropertyName("expiryEpochSeconds")]
        public long? ExpiryEpochSeconds { get; set; }
    }
}
