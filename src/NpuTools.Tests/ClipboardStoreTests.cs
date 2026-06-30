using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using NpuTools.Clipboard.Data;
using Xunit;

namespace NpuTools.Tests;

public sealed class ClipboardStoreTests : IDisposable
{
    private readonly string _historyPath;
    private readonly string? _backupPath;

    public ClipboardStoreTests()
    {
        _historyPath = ClipboardPaths.HistoryPath();
        string dir = Path.GetDirectoryName(_historyPath)!;
        Directory.CreateDirectory(dir);

        // Backup existing user history.json to avoid destroying actual user data
        if (File.Exists(_historyPath))
        {
            _backupPath = Path.Combine(dir, $"history.json.backup_{Guid.NewGuid():N}");
            File.Move(_historyPath, _backupPath);
        }
    }

    public void Dispose()
    {
        // Cleanup test file
        try
        {
            if (File.Exists(_historyPath))
                File.Delete(_historyPath);
        }
        catch { }

        // Restore user backup
        if (_backupPath is not null && File.Exists(_backupPath))
        {
            try
            {
                File.Move(_backupPath, _historyPath, overwrite: true);
            }
            catch { }
        }
    }

    [Fact]
    public void ClipboardStore_LoadsDynamicallyWhenFileChanges()
    {
        // 1. Initialize store (should be empty initially)
        var store = new ClipboardStore();
        Assert.Equal(0, store.Count);

        // 2. Simulate keeper writing to history.json on disk
        var entry1 = new ClipboardEntry
        {
            Id = "clip_test_1",
            GroupId = "grp_test",
            Kind = ClipboardEntryKind.Text,
            CreatedAt = DateTimeOffset.UtcNow,
            Title = "Disk Item 1",
            Text = "Disk Item 1 Content",
            ContentHash = "hash1"
        };
        WriteHistoryFile(new List<ClipboardEntry> { entry1 });

        // 3. Verify store automatically reloads the disk changes on next read
        Assert.Equal(1, store.Count);
        var snapshot = store.Snapshot();
        Assert.Single(snapshot);
        Assert.Equal("clip_test_1", snapshot[0].Id);
        Assert.Equal("Disk Item 1", snapshot[0].Title);

        // 4. Simulate another write on disk (e.g. adding item 2)
        var entry2 = new ClipboardEntry
        {
            Id = "clip_test_2",
            GroupId = "grp_test",
            Kind = ClipboardEntryKind.Text,
            CreatedAt = DateTimeOffset.UtcNow,
            Title = "Disk Item 2",
            Text = "Disk Item 2 Content",
            ContentHash = "hash2"
        };
        WriteHistoryFile(new List<ClipboardEntry> { entry1, entry2 });

        // 5. Verify store updates again
        var searchResults = store.Search(null, "");
        Assert.Equal(2, searchResults.Count);
        Assert.Contains(searchResults, e => e.Id == "clip_test_2");
    }

    private void WriteHistoryFile(List<ClipboardEntry> entries)
    {
        string json = JsonSerializer.Serialize(entries, ClipboardJsonContext.Default.ListClipboardEntry);
        File.WriteAllText(_historyPath, json);
        
        // Ensure NTFS modification time is distinct so the check registers.
        // File.SetLastWriteTimeUtc is used to manually increment the write time.
        File.SetLastWriteTimeUtc(_historyPath, DateTime.UtcNow.AddSeconds(1));
    }
}
