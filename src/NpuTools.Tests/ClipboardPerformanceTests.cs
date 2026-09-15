using System.Diagnostics;
using System.Text.Json;
using NpuTools.Clipboard.Data;
using Xunit;

namespace NpuTools.Tests;

public sealed class ClipboardPerformanceTests : IDisposable
{
    private const int LargeHistorySize = 5_000;
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"NpuClipboardPerformanceTests_{Guid.NewGuid():N}");

    public ClipboardPerformanceTests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        try { Directory.Delete(_directory, recursive: true); } catch { }
    }

    [Fact]
    public void BoundedSearch_ReportsTotalWithoutMaterializingEveryMatch()
    {
        var store = CreateLargeStore(200);

        var results = store.Search(null, string.Empty, 50, out int totalMatches);

        Assert.Equal(200, totalMatches);
        Assert.Equal(50, results.Count);
        Assert.Equal("clip_0199", results[0].Id);
    }

    [Fact]
    public void LargeHistory_ColdLoadAndBoundedSearch_StayWithinInteractiveBudget()
    {
        string historyPath = WriteHistory(LargeHistorySize);
        var stopwatch = Stopwatch.StartNew();

        var store = new ClipboardStore(historyPath, Path.Combine(_directory, "blobs"));
        var results = store.Search(null, "common searchable text", 100, out int totalMatches);

        stopwatch.Stop();
        Assert.Equal(LargeHistorySize, totalMatches);
        Assert.Equal(100, results.Count);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2), $"Cold clipboard query took {stopwatch.Elapsed.TotalMilliseconds:N0} ms.");
    }

    [Fact]
    public void LargeHistory_RepeatedBoundedSearches_StayWithinTypingBudget()
    {
        var store = CreateLargeStore(LargeHistorySize);
        _ = store.Search(null, "common", 100, out _); // JIT and warm caches.
        var stopwatch = Stopwatch.StartNew();

        for (int i = 0; i < 25; i++)
            _ = store.Search(null, $"common searchable text {i}", 100, out _);

        stopwatch.Stop();
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1), $"Twenty-five clipboard queries took {stopwatch.Elapsed.TotalMilliseconds:N0} ms.");
    }

    private ClipboardStore CreateLargeStore(int count)
    {
        string historyPath = WriteHistory(count);
        return new ClipboardStore(historyPath, Path.Combine(_directory, "blobs"));
    }

    private string WriteHistory(int count)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var entries = Enumerable.Range(0, count).Select(i => new ClipboardEntry
        {
            Id = $"clip_{i:D4}",
            GroupId = $"grp_{i / 3:D4}",
            Kind = ClipboardEntryKind.Text,
            CreatedAt = now.AddMilliseconds(i),
            Title = $"Item {i}",
            Text = $"common searchable text {i}",
            ContentHash = $"hash_{i:D4}",
        }).ToList();
        string historyPath = Path.Combine(_directory, $"history-{count}.json");
        File.WriteAllText(historyPath, JsonSerializer.Serialize(entries, ClipboardJsonContext.Default.ListClipboardEntry));
        return historyPath;
    }
}
