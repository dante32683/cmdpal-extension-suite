using NpuTools.Awake.Models;
using NpuTools.Awake.Services;
using Xunit;

namespace NpuTools.Tests;

public sealed class AwakeTimeTests
{
    [Fact]
    public void OvernightScheduleUsesPreviousDayAfterMidnight()
    {
        var mondayOnly = new AwakeSchedule
        {
            Enabled = true,
            Days = [1],
            Start = "22:00",
            End = "06:00",
        };

        Assert.False(AwakeTime.IsScheduleActiveNow(mondayOnly, new DateTimeOffset(2026, 7, 13, 21, 59, 0, TimeSpan.Zero)));
        Assert.True(AwakeTime.IsScheduleActiveNow(mondayOnly, new DateTimeOffset(2026, 7, 13, 22, 0, 0, TimeSpan.Zero)));
        Assert.True(AwakeTime.IsScheduleActiveNow(mondayOnly, new DateTimeOffset(2026, 7, 14, 5, 59, 0, TimeSpan.Zero)));
        Assert.False(AwakeTime.IsScheduleActiveNow(mondayOnly, new DateTimeOffset(2026, 7, 14, 6, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void HourMinuteParserRejectsExtraComponents()
    {
        Assert.False(AwakeTime.TryParseHourMinute("09:30:99", out _));
        Assert.True(AwakeTime.TryParseHourMinute("09:30", out _));
    }
}
