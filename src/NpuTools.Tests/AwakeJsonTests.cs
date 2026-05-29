using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using NpuTools.Awake.Models;
using NpuTools.Awake.Services;
using Xunit;

namespace NpuTools.Tests;

public sealed class AwakeJsonTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"AwakeJsonTests_{Guid.NewGuid():N}");

    public AwakeJsonTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    private string TempFile(string name) => Path.Combine(_dir, name);

    // ── AwakeSettings ──────────────────────────────────────────────────────────

    [Fact]
    public void AwakeSettings_RoundTrips_DefaultValues()
    {
        string path = TempFile("settings.json");
        var original = new AwakeSettings();
        AwakeJson.AtomicWrite(path, original, AwakeJsonContext.Default.AwakeSettings);

        var loaded = AwakeJson.Read(path, new AwakeSettings(), AwakeJsonContext.Default.AwakeSettings);

        Assert.Equal("indefinite", loaded.DefaultAwakeMode);
        Assert.True(loaded.ShowSuccessToasts);
        Assert.Equal(60, loaded.DefaultDurationMinutes);
        Assert.Equal("17:00", loaded.DefaultUntilTime);
    }

    [Fact]
    public void AwakeSettings_RoundTrips_CustomValues()
    {
        string path = TempFile("settings2.json");
        var original = new AwakeSettings
        {
            DefaultAwakeMode = "screen-off",
            ShowSuccessToasts = false,
            DefaultDurationMinutes = 120,
            DefaultUntilTime = "18:30",
            DefaultScheduleStart = "08:00",
            DefaultScheduleEnd = "16:00",
            DefaultScheduleDays = [1, 2, 3],
        };

        AwakeJson.AtomicWrite(path, original, AwakeJsonContext.Default.AwakeSettings);
        var loaded = AwakeJson.Read(path, new AwakeSettings(), AwakeJsonContext.Default.AwakeSettings);

        Assert.Equal("screen-off", loaded.DefaultAwakeMode);
        Assert.False(loaded.ShowSuccessToasts);
        Assert.Equal(120, loaded.DefaultDurationMinutes);
        Assert.Equal("18:30", loaded.DefaultUntilTime);
        Assert.Equal([1, 2, 3], loaded.DefaultScheduleDays);
    }

    [Fact]
    public void AwakeSettings_UsesJsonPropertyNames()
    {
        string path = TempFile("settings3.json");
        AwakeJson.AtomicWrite(path, new AwakeSettings { DefaultAwakeMode = "timed" }, AwakeJsonContext.Default.AwakeSettings);

        string json = File.ReadAllText(path);
        Assert.Contains("\"defaultAwakeMode\"", json);
        Assert.Contains("\"showSuccessToasts\"", json);
        Assert.DoesNotContain("\"DefaultAwakeMode\"", json);
    }

    [Fact]
    public void AwakeSettings_MissingFile_ReturnsFallback()
    {
        var fallback = new AwakeSettings { DefaultDurationMinutes = 999 };
        var result = AwakeJson.Read(TempFile("nonexistent.json"), fallback, AwakeJsonContext.Default.AwakeSettings);
        Assert.Equal(999, result.DefaultDurationMinutes);
    }

    // ── AwakeStateFile ─────────────────────────────────────────────────────────

    [Fact]
    public void AwakeStateFile_RoundTrips_WithOverride()
    {
        string path = TempFile("state.json");
        var original = new AwakeStateFile
        {
            Override = new AwakeOverride { Mode = "timed", ExpiryEpochSeconds = 9999999L },
        };

        AwakeJson.AtomicWrite(path, original, AwakeJsonContext.Default.AwakeStateFile);
        var loaded = AwakeJson.Read(path, new AwakeStateFile(), AwakeJsonContext.Default.AwakeStateFile);

        Assert.NotNull(loaded.Override);
        Assert.Equal("timed", loaded.Override.Mode);
        Assert.Equal(9999999L, loaded.Override.ExpiryEpochSeconds);
    }

    [Fact]
    public void AwakeStateFile_NullOverride_RoundTrips()
    {
        string path = TempFile("state2.json");
        AwakeJson.AtomicWrite(path, new AwakeStateFile(), AwakeJsonContext.Default.AwakeStateFile);
        var loaded = AwakeJson.Read(path, new AwakeStateFile(), AwakeJsonContext.Default.AwakeStateFile);
        Assert.Null(loaded.Override);
    }

    // ── List<AwakeSchedule> ────────────────────────────────────────────────────

    [Fact]
    public void AwakeSchedules_RoundTrip()
    {
        string path = TempFile("schedules.json");
        var original = new List<AwakeSchedule>
        {
            new() { Id = "s1", Enabled = true, Days = [1, 2, 3, 4, 5], Start = "09:00", End = "17:00" },
            new() { Id = "s2", Enabled = false, Days = [6], Start = "10:00", End = "14:00" },
        };

        AwakeJson.AtomicWrite(path, original, AwakeJsonContext.Default.ListAwakeSchedule);
        var loaded = AwakeJson.Read(path, new List<AwakeSchedule>(), AwakeJsonContext.Default.ListAwakeSchedule);

        Assert.Equal(2, loaded.Count);
        Assert.Equal("s1", loaded[0].Id);
        Assert.True(loaded[0].Enabled);
        Assert.Equal([1, 2, 3, 4, 5], loaded[0].Days);
        Assert.Equal("s2", loaded[1].Id);
        Assert.False(loaded[1].Enabled);
    }

    [Fact]
    public void AwakeSchedules_MissingFile_ReturnsEmptyList()
    {
        var result = AwakeJson.Read(TempFile("no-schedules.json"), new List<AwakeSchedule>(), AwakeJsonContext.Default.ListAwakeSchedule);
        Assert.Empty(result);
    }

    // ── AwakeHeartbeat ─────────────────────────────────────────────────────────

    [Fact]
    public void AwakeHeartbeat_RoundTrips()
    {
        string path = TempFile("heartbeat.json");
        var original = new AwakeHeartbeat { TimestampUtc = 1_700_000_000L, IsActive = true, Reason = "schedule" };

        AwakeJson.AtomicWrite(path, original, AwakeJsonContext.Default.AwakeHeartbeat);
        var loaded = AwakeJson.Read(path, null!, AwakeJsonContext.Default.AwakeHeartbeat);

        Assert.NotNull(loaded);
        Assert.Equal(1_700_000_000L, loaded.TimestampUtc);
        Assert.True(loaded.IsActive);
        Assert.Equal("schedule", loaded.Reason);
    }

    [Fact]
    public void AwakeHeartbeat_MissingFile_ReturnsNull()
    {
        AwakeHeartbeat? result = AwakeJson.Read(TempFile("no-heartbeat.json"), null!, AwakeJsonContext.Default.AwakeHeartbeat);
        Assert.Null(result);
    }

    // ── AwakeIntent (case-insensitive deserialization) ─────────────────────────

    [Fact]
    public void AwakeIntent_Deserializes_CaseInsensitive()
    {
        string json = """{"ACTION":"start","MODE":"indefinite","VALUE":null}""";
        var intent = JsonSerializer.Deserialize(json, AwakeJsonContext.Default.AwakeIntent);

        Assert.NotNull(intent);
        Assert.Equal("start", intent.Action);
        Assert.Equal("indefinite", intent.Mode);
    }

    [Fact]
    public void AwakeIntent_Deserializes_WithAllFields()
    {
        string json = """
            {
              "action": "schedule",
              "days": ["mon", "tue", "wed"],
              "start": "09:00",
              "end": "17:00"
            }
            """;
        var intent = JsonSerializer.Deserialize(json, AwakeJsonContext.Default.AwakeIntent);

        Assert.NotNull(intent);
        Assert.Equal("schedule", intent.Action);
        Assert.NotNull(intent.Days);
        Assert.Equal(["mon", "tue", "wed"], intent.Days);
        Assert.Equal("09:00", intent.Start);
        Assert.Equal("17:00", intent.End);
    }

    [Fact]
    public void AwakeIntent_Normalize_LowercasesAndNormalizesTime()
    {
        var intent = new AwakeIntent
        {
            Action = "START",
            Mode = "Timed",
            Time = "9:5",
            Start = "  8:30  ",
            End = "17:0",
        };

        intent.Normalize();

        Assert.Equal("start", intent.Action);
        Assert.Equal("timed", intent.Mode);
        Assert.Equal("09:05", intent.Time);
        Assert.Equal("08:30", intent.Start);
        Assert.Equal("17:00", intent.End);
    }

    [Fact]
    public void AwakeIntent_Normalize_InvalidTime_BecomesNull()
    {
        var intent = new AwakeIntent { Time = "25:99" };
        intent.Normalize();
        Assert.Null(intent.Time);
    }
}
