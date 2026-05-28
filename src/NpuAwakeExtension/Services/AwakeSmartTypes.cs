using System.Linq;
using System.Text.Json.Serialization;

namespace NpuTools.Awake.Services;

internal sealed class AwakeIntent
{
    [JsonPropertyName("action")]
    public string? Action { get; set; }

    [JsonPropertyName("mode")]
    public string? Mode { get; set; }

    [JsonPropertyName("unit")]
    public string? Unit { get; set; }

    [JsonPropertyName("value")]
    public double? Value { get; set; }

    [JsonPropertyName("time")]
    public string? Time { get; set; }

    [JsonPropertyName("days")]
    public string[]? Days { get; set; }

    [JsonPropertyName("start")]
    public string? Start { get; set; }

    [JsonPropertyName("end")]
    public string? End { get; set; }

    public void Normalize()
    {
        Action = Action?.Trim().ToLowerInvariant();
        Mode = Mode?.Trim().ToLowerInvariant();
        Unit = Unit?.Trim().ToLowerInvariant();
        Time = NormalizeHm(Time);
        Start = NormalizeHm(Start);
        End = NormalizeHm(End);
        Days = Days?.Select(d => d.Trim().ToLowerInvariant()).Where(d => d.Length > 0).Distinct().ToArray();
    }

    private static string? NormalizeHm(string? hm)
    {
        if (string.IsNullOrWhiteSpace(hm))
            return null;

        var parts = hm.Trim().Split(':', System.StringSplitOptions.TrimEntries);
        if (parts.Length < 2 ||
            !int.TryParse(parts[0], out int h) ||
            !int.TryParse(parts[1], out int m) ||
            h is < 0 or > 23 ||
            m is < 0 or > 59)
        {
            return null;
        }

        return $"{h:00}:{m:00}";
    }
}

internal sealed record SmartAwakeResult(bool IsSuccess, string Message)
{
    public static SmartAwakeResult Success(string message) => new(true, message);

    public static SmartAwakeResult Failure(string message) => new(false, message);
}
