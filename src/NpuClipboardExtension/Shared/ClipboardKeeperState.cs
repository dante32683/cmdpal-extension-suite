using System.Text.Json.Serialization;

namespace NpuTools.Clipboard.Data;

public sealed class ClipboardKeeperState
{
    [JsonPropertyName("startedAt")]
    public string? StartedAt { get; set; }

    [JsonPropertyName("lastHeartbeatAt")]
    public string? LastHeartbeatAt { get; set; }

    [JsonPropertyName("lastCapturedAt")]
    public string? LastCapturedAt { get; set; }

    [JsonPropertyName("lastSkippedReason")]
    public string? LastSkippedReason { get; set; }

    [JsonPropertyName("lastError")]
    public string? LastError { get; set; }

    [JsonPropertyName("captured")]
    public int Captured { get; set; }

    [JsonPropertyName("skipped")]
    public int Skipped { get; set; }

    [JsonPropertyName("errors")]
    public int Errors { get; set; }
}
