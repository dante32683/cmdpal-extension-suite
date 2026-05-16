using System.Globalization;
using NpuTools.Awake.Models;

namespace NpuTools.Awake.Services;

internal static class AwakeTime
{
    private static readonly string[] DayLabels = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];

    public static long FromMinutes(int minutes)
    {
        return DateTimeOffset.UtcNow.AddMinutes(minutes).ToUnixTimeSeconds();
    }

    public static long? ParseUntilToEpoch(string value)
    {
        if (!TryParseHourMinute(value, out var hm))
        {
            return null;
        }

        var now = DateTimeOffset.Now;
        var target = new DateTimeOffset(now.Year, now.Month, now.Day, hm.Hours, hm.Minutes, 0, now.Offset);
        if (target <= now)
        {
            target = target.AddDays(1);
        }

        return target.ToUnixTimeSeconds();
    }

    public static bool IsScheduleActiveNow(AwakeSchedule schedule, DateTimeOffset nowLocal)
    {
        int dow = (int)nowLocal.DayOfWeek;
        if (!schedule.Enabled || !schedule.Days.Contains(dow))
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

    public static string FormatDays(IEnumerable<int> days)
    {
        return string.Join(", ", days.Order().Select(day => day is >= 0 and <= 6 ? DayLabels[day] : day.ToString(CultureInfo.InvariantCulture)));
    }

    public static string FormatOverride(AwakeOverride? ov)
    {
        if (ov is null)
        {
            return "Sleeping allowed";
        }

        string mode = ov.Mode switch
        {
            "indefinite" => "Awake indefinitely",
            "screen-off" => "Awake, display can sleep",
            "timed" => "Awake for duration",
            "until" => "Awake until time",
            _ => $"Awake: {ov.Mode}",
        };

        return ov.ExpiryEpochSeconds is long epoch
            ? $"{mode} ({FormatRemaining(epoch)})"
            : mode;
    }

    public static string FormatRemaining(long epochSeconds)
    {
        long diff = epochSeconds - DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (diff <= 0)
        {
            return "expired";
        }

        var span = TimeSpan.FromSeconds(diff);
        return span.TotalHours >= 1
            ? $"{(int)span.TotalHours}h {span.Minutes}m left"
            : $"{span.Minutes}m {span.Seconds}s left";
    }

    public static bool TryParseHourMinute(string? value, out TimeSpan hourMinute)
    {
        hourMinute = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var parts = value.Trim().Split(':', StringSplitOptions.TrimEntries);
        if (parts.Length < 2 ||
            !int.TryParse(parts[0], out int h) ||
            !int.TryParse(parts[1], out int m) ||
            h is < 0 or > 23 ||
            m is < 0 or > 59)
        {
            return false;
        }

        hourMinute = new TimeSpan(h, m, 0);
        return true;
    }
}
