using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.Text;
using NpuTools.Awake.Models;
using Windows.ApplicationModel;

namespace NpuTools.Awake.Services;

internal sealed partial class SmartAwakeService
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822", Justification = "Service method — uniform call site via injection.")]
    public SmartAwakeResult Execute(string text, AwakeService awakeService)
    {
        string input = text.Trim();
        if (input.Length == 0)
        {
            return SmartAwakeResult.Failure("Try: keep awake for 90 minutes.");
        }

        // Local deterministic parser first — regex patterns are reliable and never hallucinate.
        // Only fall through to Phi for phrasing the local parser cannot definitively map.
        var localResult = ExecuteWithLocalParser(input, awakeService);
        if (localResult.IsSuccess)
        {
            return localResult;
        }

        // Phi-Silica for ambiguous natural language the local parser returned Failure for.
        try
        {
            var intent = ExtractIntentWithPhiAsync(input).GetAwaiter().GetResult();
            var phiResult = ExecuteIntent(intent, awakeService);
            if (phiResult.IsSuccess)
            {
                return phiResult;
            }
        }
        catch
        {
            // Phi unavailable — return local parser's failure message below.
        }

        return localResult;
    }

    private static async Task<AwakeIntent> ExtractIntentWithPhiAsync(string input)
    {
        _ = TryUnlockNpuFeature();

        if (LanguageModel.GetReadyState() != AIFeatureReadyState.Ready)
        {
            var ready = await LanguageModel.EnsureReadyAsync();
            if (ready.Status != AIFeatureReadyResultState.Success)
            {
                throw new InvalidOperationException($"Phi-Silica model unavailable: {ready.Status}");
            }
        }

        using var model = await LanguageModel.CreateAsync();
        var now = DateTimeOffset.Now;
        string prompt = $@"You are a routing assistant for a Windows keep-awake tool.

Current local time: {now:yyyy-MM-dd HH:mm:ss}
Current day of week: {now:dddd}
Timezone offset minutes: {(int)TimeZoneInfo.Local.GetUtcOffset(now).TotalMinutes}

Output ONLY valid JSON with exactly this schema:
{{
  ""action"": ""status"" | ""start"" | ""stop"" | ""schedule"" | ""unschedule"" | ""help"",
  ""mode"": ""indefinite"" | ""timed"" | ""until"" | ""screen-off"" | null,
  ""unit"": ""minutes"" | ""hours"" | null,
  ""value"": number | null,
  ""time"": ""HH:mm"" | null,
  ""days"": [""sun"",""mon"",""tue"",""wed"",""thu"",""fri"",""sat""] | null,
  ""start"": ""HH:mm"" | null,
  ""end"": ""HH:mm"" | null
}}

Rules:
- Do not compute timestamps.
- ""for 90 minutes"" => action=start, mode=timed, unit=minutes, value=90
- ""until 5pm"" => action=start, mode=until, time=""17:00""
- ""keep awake"" => action=start, mode=indefinite
- ""screen off mode"" => action=start, mode=screen-off
- ""weekdays 09:00 to 17:00"" => action=schedule, days=[""mon"",""tue"",""wed"",""thu"",""fri""], start=""09:00"", end=""17:00""
- stop schedules => action=unschedule
- If unclear, action=help

User request: {input}";

        var response = await model.GenerateResponseAsync(prompt);
        string json = ExtractJsonObject(response.Text ?? "");
        var intent = JsonSerializer.Deserialize<AwakeIntent>(json, AwakeJson.Options)
            ?? throw new InvalidOperationException("Phi-Silica returned an empty intent.");
        intent.Normalize();
        return intent;
    }

    private static SmartAwakeResult ExecuteIntent(AwakeIntent intent, AwakeService awakeService)
    {
        var settings = awakeService.GetSettings();

        return intent.Action switch
        {
            "help" => SmartAwakeResult.Success("Try: keep awake, keep awake for 90 minutes, until 17:30, weekdays 09:00 to 17:00, stop schedules."),
            "status" => SmartAwakeResult.Success(AwakeTime.FormatOverride(awakeService.GetStatus().Override)),
            "stop" => StopAwake(awakeService),
            "unschedule" => ClearSchedules(awakeService),
            "start" => ExecuteStartIntent(intent, awakeService),
            "schedule" => ExecuteScheduleIntent(intent, awakeService, settings),
            _ => SmartAwakeResult.Failure("I could not map that request. Try: keep awake for 90 minutes."),
        };
    }

    private static SmartAwakeResult ExecuteStartIntent(AwakeIntent intent, AwakeService awakeService)
    {
        switch (intent.Mode)
        {
            case "indefinite":
                awakeService.SetOverride(new AwakeOverride { Mode = "indefinite" });
                return SmartAwakeResult.Success("PC will stay awake indefinitely.");
            case "screen-off":
                awakeService.SetOverride(new AwakeOverride { Mode = "screen-off" });
                return SmartAwakeResult.Success("PC awake; display can sleep.");
            case "timed":
                if (intent.Value is not double value || value <= 0 || intent.Unit is not ("minutes" or "hours"))
                {
                    return SmartAwakeResult.Failure("Missing duration. Try: keep awake for 90 minutes.");
                }

                int minutes = intent.Unit == "hours" ? (int)Math.Round(value * 60) : (int)Math.Round(value);
                awakeService.SetOverride(new AwakeOverride { Mode = "timed", ExpiryEpochSeconds = AwakeTime.FromMinutes(minutes) });
                return SmartAwakeResult.Success($"PC will stay awake for {minutes} minute(s).");
            case "until":
                if (intent.Time is null || AwakeTime.ParseUntilToEpoch(intent.Time) is not long epoch)
                {
                    return SmartAwakeResult.Failure("Missing time. Try: keep awake until 17:30.");
                }

                awakeService.SetOverride(new AwakeOverride { Mode = "until", ExpiryEpochSeconds = epoch });
                return SmartAwakeResult.Success($"PC will stay awake until {intent.Time}.");
            default:
                return SmartAwakeResult.Failure("Unsupported start request.");
        }
    }

    private static SmartAwakeResult ExecuteScheduleIntent(AwakeIntent intent, AwakeService awakeService, AwakeSettings settings)
    {
        int[] days = (intent.Days ?? []).Select(DayStrToDow).Where(d => d.HasValue).Select(d => d!.Value).Distinct().Order().ToArray();
        if (days.Length == 0)
        {
            days = settings.DefaultScheduleDays;
        }

        string? start = intent.Start ?? settings.DefaultScheduleStart;
        string? end = intent.End ?? settings.DefaultScheduleEnd;
        if (!AwakeTime.TryParseHourMinute(start, out _) || !AwakeTime.TryParseHourMinute(end, out _))
        {
            return SmartAwakeResult.Failure("Missing schedule fields. Try: weekdays 09:00 to 17:00.");
        }

        var schedule = new AwakeSchedule
        {
            Id = $"sched_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            Enabled = true,
            Days = days,
            Start = start!,
            End = end!,
        };

        awakeService.SetSchedules([schedule]);
        return SmartAwakeResult.Success($"Schedule saved: {AwakeTime.FormatDays(days)} {start}-{end}.");
    }

    private static SmartAwakeResult StopAwake(AwakeService awakeService)
    {
        awakeService.SetOverride(null);
        return SmartAwakeResult.Success("PC can now sleep.");
    }

    private static SmartAwakeResult ClearSchedules(AwakeService awakeService)
    {
        awakeService.SetSchedules([]);
        return SmartAwakeResult.Success("Schedules cleared.");
    }

    private static string ExtractJsonObject(string text)
    {
        int start = text.IndexOf('{', StringComparison.Ordinal);
        int end = text.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            throw new InvalidOperationException("Could not extract JSON from Phi-Silica output.");
        }

        return text[start..(end + 1)].Trim();
    }

    private static bool TryUnlockNpuFeature()
    {
        try
        {
            const string featureId = "com.microsoft.windows.ai.languagemodel";
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\AppModel\LimitedAccessFeatures\{featureId}");
            string? lafKey = key?.GetValue("")?.ToString();
            if (string.IsNullOrEmpty(lafKey))
            {
                return false;
            }

            string pfn = Package.Current.Id.FamilyName;
            string input = $"{featureId}!{lafKey}!{pfn}";
            byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            string token = Convert.ToBase64String(hashBytes[..16]);
            string publisherId = pfn.Split('_')[1];
            string attestation = $"{publisherId} has registered their use of {featureId} with Microsoft and agrees to the terms of use.";
            var result = Windows.ApplicationModel.LimitedAccessFeatures.TryUnlockFeature(featureId, token, attestation);
            return result.Status is LimitedAccessFeatureStatus.Available or LimitedAccessFeatureStatus.AvailableWithoutToken;
        }
        catch
        {
            return false;
        }
    }

    private static SmartAwakeResult ExecuteWithLocalParser(string input, AwakeService awakeService)
    {
        var settings = awakeService.GetSettings();
        string lower = input.ToLowerInvariant();

        if (ContainsAny(lower, "help", "what can"))
        {
            return SmartAwakeResult.Success("Try: keep awake, keep awake for 90 minutes, until 17:30, weekdays 09:00 to 17:00, stop, clear schedules.");
        }

        if (ContainsAny(lower, "status", "state"))
        {
            return SmartAwakeResult.Success(AwakeTime.FormatOverride(awakeService.GetStatus().Override));
        }

        if (ContainsAny(lower, "clear schedules", "stop schedules", "delete schedules", "unschedule"))
        {
            awakeService.SetSchedules([]);
            return SmartAwakeResult.Success("Schedules cleared.");
        }

        if (ContainsAny(lower, "stop", "let sleep", "sleep normally", "cancel"))
        {
            awakeService.SetOverride(null);
            return SmartAwakeResult.Success("PC can now sleep.");
        }

        var schedule = TryParseSchedule(lower, settings);
        if (schedule is not null)
        {
            awakeService.SetSchedules([schedule]);
            return SmartAwakeResult.Success($"Schedule saved: {AwakeTime.FormatDays(schedule.Days)} {schedule.Start}-{schedule.End}.");
        }

        var duration = DurationRegex().Match(lower);
        if (duration.Success)
        {
            double value = double.Parse(duration.Groups["value"].Value, CultureInfo.InvariantCulture);
            string unit = duration.Groups["unit"].Value;
            int minutes = unit.StartsWith("hour", StringComparison.OrdinalIgnoreCase)
                ? (int)Math.Round(value * 60)
                : (int)Math.Round(value);
            if (minutes <= 0)
            {
                return SmartAwakeResult.Failure("Duration must be positive.");
            }

            awakeService.SetOverride(new AwakeOverride { Mode = "timed", ExpiryEpochSeconds = AwakeTime.FromMinutes(minutes) });
            return SmartAwakeResult.Success($"PC will stay awake for {minutes} minute(s).");
        }

        var until = UntilRegex().Match(lower);
        if (until.Success)
        {
            string hm = NormalizeTime(until.Groups["time"].Value);
            long? epoch = AwakeTime.ParseUntilToEpoch(hm);
            if (epoch is null)
            {
                return SmartAwakeResult.Failure("Invalid time. Use HH:mm, like 17:30.");
            }

            awakeService.SetOverride(new AwakeOverride { Mode = "until", ExpiryEpochSeconds = epoch });
            return SmartAwakeResult.Success($"PC will stay awake until {hm}.");
        }

        if (ContainsAny(lower, "screen off", "display can sleep", "display off"))
        {
            awakeService.SetOverride(new AwakeOverride { Mode = "screen-off" });
            return SmartAwakeResult.Success("PC awake; display can sleep.");
        }

        if (ContainsAny(lower, "awake", "keep up", "stay on"))
        {
            awakeService.SetOverride(new AwakeOverride { Mode = "indefinite" });
            return SmartAwakeResult.Success("PC will stay awake indefinitely.");
        }

        return SmartAwakeResult.Failure("I could not map that request. Try: keep awake for 90 minutes.");
    }

    private static AwakeSchedule? TryParseSchedule(string lower, AwakeSettings settings)
    {
        var match = ScheduleRegex().Match(lower);
        if (!match.Success)
        {
            return null;
        }

        string start = NormalizeTime(match.Groups["start"].Value);
        string end = NormalizeTime(match.Groups["end"].Value);
        if (!AwakeTime.TryParseHourMinute(start, out _) || !AwakeTime.TryParseHourMinute(end, out _))
        {
            return null;
        }

        int[] days = ParseDays(lower);
        if (days.Length == 0)
        {
            days = settings.DefaultScheduleDays;
        }

        return new AwakeSchedule
        {
            Id = $"sched_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            Enabled = true,
            Days = days,
            Start = start,
            End = end,
        };
    }

    private static int[] ParseDays(string value)
    {
        if (value.Contains("weekday", StringComparison.OrdinalIgnoreCase))
        {
            return [1, 2, 3, 4, 5];
        }

        if (value.Contains("weekend", StringComparison.OrdinalIgnoreCase))
        {
            return [0, 6];
        }

        var days = new List<int>();
        string[] labels = ["sun", "mon", "tue", "wed", "thu", "fri", "sat"];
        for (int i = 0; i < labels.Length; i++)
        {
            if (value.Contains(labels[i], StringComparison.OrdinalIgnoreCase))
            {
                days.Add(i);
            }
        }

        return days.Distinct().Order().ToArray();
    }

    private static string NormalizeTime(string raw)
    {
        var match = TimeRegex().Match(raw.Trim().ToLowerInvariant());
        if (!match.Success)
        {
            return raw;
        }

        int h = int.Parse(match.Groups["hour"].Value, CultureInfo.InvariantCulture);
        int m = match.Groups["minute"].Success ? int.Parse(match.Groups["minute"].Value, CultureInfo.InvariantCulture) : 0;
        string suffix = match.Groups["suffix"].Value;
        if (suffix == "pm" && h < 12)
        {
            h += 12;
        }
        else if (suffix == "am" && h == 12)
        {
            h = 0;
        }

        return $"{h:00}:{m:00}";
    }

    private static bool ContainsAny(string value, params string[] needles)
    {
        return needles.Any(n => value.Contains(n, StringComparison.OrdinalIgnoreCase));
    }

    private static int? DayStrToDow(string day)
    {
        return day.Trim().ToLowerInvariant() switch
        {
            "sun" => 0,
            "mon" => 1,
            "tue" => 2,
            "wed" => 3,
            "thu" => 4,
            "fri" => 5,
            "sat" => 6,
            _ => null,
        };
    }

    [GeneratedRegex(@"(?<value>\d+(?:\.\d+)?)\s*(?<unit>minute|minutes|min|mins|hour|hours|hr|hrs)")]
    private static partial Regex DurationRegex();

    [GeneratedRegex(@"until\s+(?<time>\d{1,2}(?::\d{2})?\s*(?:am|pm)?)")]
    private static partial Regex UntilRegex();

    [GeneratedRegex(@"(?<start>\d{1,2}(?::\d{2})?\s*(?:am|pm)?)\s*(?:-|to|through|until)\s*(?<end>\d{1,2}(?::\d{2})?\s*(?:am|pm)?)")]
    private static partial Regex ScheduleRegex();

    [GeneratedRegex(@"(?<hour>\d{1,2})(?::(?<minute>\d{2}))?\s*(?<suffix>am|pm)?")]
    private static partial Regex TimeRegex();
}

internal sealed record SmartAwakeResult(bool IsSuccess, string Message)
{
    public static SmartAwakeResult Success(string message) => new(true, message);

    public static SmartAwakeResult Failure(string message) => new(false, message);
}

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
        {
            return null;
        }

        var parts = hm.Trim().Split(':', StringSplitOptions.TrimEntries);
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
