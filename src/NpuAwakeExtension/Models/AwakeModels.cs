using System.Text.Json.Serialization;
using NpuTools.Awake.Services;

namespace NpuTools.Awake.Models;

internal sealed class AwakeSettings
{
    [JsonPropertyName("defaultAwakeMode")]
    public string DefaultAwakeMode { get; set; } = "indefinite";

    [JsonPropertyName("showSuccessToasts")]
    public bool ShowSuccessToasts { get; set; } = true;

    [JsonPropertyName("showLidNote")]
    public bool ShowLidNote { get; set; }

    [JsonPropertyName("defaultDurationMinutes")]
    public int DefaultDurationMinutes { get; set; } = 60;

    [JsonPropertyName("defaultUntilTime")]
    public string DefaultUntilTime { get; set; } = "17:00";

    [JsonPropertyName("defaultScheduleStart")]
    public string DefaultScheduleStart { get; set; } = "09:00";

    [JsonPropertyName("defaultScheduleEnd")]
    public string DefaultScheduleEnd { get; set; } = "17:00";

    [JsonPropertyName("defaultScheduleDays")]
    public int[] DefaultScheduleDays { get; set; } = [1, 2, 3, 4, 5];
}

internal sealed class AwakeStateFile
{
    [JsonPropertyName("override")]
    public AwakeOverride? Override { get; set; }
}

internal sealed class AwakeOverride
{
    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "indefinite";

    [JsonPropertyName("expiryEpochSeconds")]
    public long? ExpiryEpochSeconds { get; set; }
}

internal sealed class AwakeSchedule
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("days")]
    public int[] Days { get; set; } = [];

    [JsonPropertyName("start")]
    public string Start { get; set; } = "";

    [JsonPropertyName("end")]
    public string End { get; set; } = "";
}

internal sealed class AwakeHeartbeat
{
    [JsonPropertyName("timestampUtc")]
    public long TimestampUtc { get; set; }

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; }

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
}

internal sealed class AwakeStatus
{
    public int? DaemonPid { get; init; }

    public AwakeOverride? Override { get; init; }

    public IReadOnlyList<AwakeSchedule> Schedules { get; init; } = [];

    public AwakeHeartbeat? Heartbeat { get; init; }

    public bool HasActiveSchedule => Schedules.Any(s => AwakeTime.IsScheduleActiveNow(s, DateTimeOffset.Now));
}
