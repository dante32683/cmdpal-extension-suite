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

    [Fact]
    public void ClipboardStore_ChangedEvent_FiresOnMarkUsed()
    {
        var store = new ClipboardStore();
        var entry = NewEntry("clip_changed_1", "changed item");
        store.AddOrPromote(entry, SettingsWith(limit: 200));

        int fired = 0;
        store.Changed += () => fired++;

        store.MarkUsed("clip_changed_1", SettingsWith(limit: 200));

        Assert.Equal(1, fired);
    }

    [Fact]
    public void ClipboardStore_ChangedEvent_DoesNotFireWhenMarkUsedTargetMissing()
    {
        var store = new ClipboardStore();
        store.AddOrPromote(NewEntry("clip_present", "present"), SettingsWith(limit: 200));

        int fired = 0;
        store.Changed += () => fired++;

        store.MarkUsed("clip_does_not_exist", SettingsWith(limit: 200));

        Assert.Equal(0, fired);
    }

    [Fact]
    public void ClipboardStore_ChangedEvent_FiresOnAddOrPromote()
    {
        var store = new ClipboardStore();
        int fired = 0;
        store.Changed += () => fired++;

        store.AddOrPromote(NewEntry("clip_aop_1", "first"), SettingsWith(limit: 200));
        store.AddOrPromote(NewEntry("clip_aop_1", "first duplicate"), SettingsWith(limit: 200));

        Assert.Equal(2, fired);
    }

    [Fact]
    public void ClipboardStore_ChangedEvent_FiresOnSetPinnedAndDelete()
    {
        var store = new ClipboardStore();
        store.AddOrPromote(NewEntry("clip_pin", "pin me"), SettingsWith(limit: 200));

        int fired = 0;
        store.Changed += () => fired++;

        store.SetPinned("clip_pin", true);
        store.Delete("clip_pin");

        Assert.Equal(2, fired);
    }

    [Fact]
    public void ClipboardStore_ChangedEvent_DoesNotFireWhenSetPinnedTargetMissing()
    {
        var store = new ClipboardStore();
        int fired = 0;
        store.Changed += () => fired++;

        store.SetPinned("clip_ghost", true);

        Assert.Equal(0, fired);
    }

    [Fact]
    public void ClipboardStore_ChangedEvent_FiresOnRename()
    {
        var store = new ClipboardStore();
        store.AddOrPromote(NewEntry("clip_rename", "renamable"), SettingsWith(limit: 200));

        int fired = 0;
        store.Changed += () => fired++;

        store.Rename("clip_rename", "  new name  ");
        store.Rename("clip_rename", "");

        Assert.Equal(2, fired);
    }

    [Fact]
    public void ClipboardStore_ChangedEvent_FiresOnDeleteAll()
    {
        var store = new ClipboardStore();
        store.AddOrPromote(NewEntry("clip_a", "a"), SettingsWith(limit: 200));
        store.AddOrPromote(NewEntry("clip_b", "b"), SettingsWith(limit: 200));

        int fired = 0;
        store.Changed += () => fired++;

        int removed = store.DeleteAll();

        Assert.Equal(2, removed);
        Assert.Equal(1, fired);
    }

    [Fact]
    public void ClipboardStore_ChangedEvent_FiresOnEnforceRetentionWhenSomethingRemoved()
    {
        var store = new ClipboardStore();
        store.AddOrPromote(NewEntry("clip_r1", "1"), SettingsWith(limit: 1));
        store.AddOrPromote(NewEntry("clip_r2", "2"), SettingsWith(limit: 1));

        int fired = 0;
        store.Changed += () => fired++;

        store.EnforceRetention(SettingsWith(limit: 0));

        Assert.Equal(1, fired);
    }

    [Fact]
    public void ClipboardStore_ChangedEvent_DoesNotFireOnEnforceRetentionWhenNothingRemoved()
    {
        var store = new ClipboardStore();
        store.AddOrPromote(NewEntry("clip_keep", "keep"), SettingsWith(limit: 10));

        int fired = 0;
        store.Changed += () => fired++;

        store.EnforceRetention(SettingsWith(limit: 10));

        Assert.Equal(0, fired);
    }

    [Fact]
    public void ClipboardStore_SyncFrom_DropsEntriesMatchingSecretPattern()
    {
        // Simulate cross-device sync: another machine wrote a JSON entry to a
        // shared sync folder. Even though the entry arrived via SyncFrom rather
        // than via local capture, the secret-pattern filter must apply so a
        // secret on device A cannot leak into local history on device B.
        string syncRoot = Path.Combine(Path.GetTempPath(), "npu-clipboard-sync-" + Guid.NewGuid().ToString("N")[..8]);
        string syncDir = Path.Combine(syncRoot, "clipboard-sync");
        Directory.CreateDirectory(syncDir);

        try
        {
            var secretEntry = new ClipboardEntry
            {
                Id = "clip_sync_secret",
                GroupId = "grp_sync_secret",
                Kind = ClipboardEntryKind.Text,
                CreatedAt = DateTimeOffset.UtcNow,
                Title = "secret",
                Text = "export CF_API_KEY=abcd1234efgh5678ijkl9012mnop",
                ContentHash = "hash_secret",
                SourceDevice = "TestRemoteDevice",
            };
            File.WriteAllText(
                Path.Combine(syncDir, "clip_sync_secret.json"),
                JsonSerializer.Serialize(secretEntry, ClipboardJsonContext.Default.ClipboardEntry));
            File.SetLastWriteTimeUtc(Path.Combine(syncDir, "clip_sync_secret.json"), DateTime.UtcNow.AddSeconds(1));

            var store = new ClipboardStore();
            var settings = new ClipboardAppSettings(); // defaults: SecretDetectionEnabled = true
            store.SyncFrom(syncRoot, settings);

            Assert.Equal(0, store.Count);
            Assert.DoesNotContain(store.Snapshot(), e => e.Id == "clip_sync_secret");

            // And the benign entry on the same device must still be merged in —
            // the filter is targeted, not a blanket drop.
            var benignEntry = new ClipboardEntry
            {
                Id = "clip_sync_benign",
                GroupId = "grp_sync_benign",
                Kind = ClipboardEntryKind.Text,
                CreatedAt = DateTimeOffset.UtcNow,
                Title = "hello",
                Text = "Hello, world!",
                ContentHash = "hash_benign",
                SourceDevice = "TestRemoteDevice",
            };
            File.WriteAllText(
                Path.Combine(syncDir, "clip_sync_benign.json"),
                JsonSerializer.Serialize(benignEntry, ClipboardJsonContext.Default.ClipboardEntry));
            File.SetLastWriteTimeUtc(Path.Combine(syncDir, "clip_sync_benign.json"), DateTime.UtcNow.AddSeconds(1));

            store.SyncFrom(syncRoot, settings);

            Assert.Equal(1, store.Count);
            Assert.Contains(store.Snapshot(), e => e.Id == "clip_sync_benign");
        }
        finally
        {
            try { Directory.Delete(syncRoot, recursive: true); } catch { }
        }
    }

    [Fact]
    public void ClipboardStore_Save_AdvancesLastWriteTimeForRapidSuccessiveWrites()
    {
        // Simulate two writes within the same millisecond. The keeper writing then the
        // extension writing MarkUsed can land on the same NTFS mtime, hiding the change
        // from EnsureFresh's writeTime != _lastWriteTime check. Save() now forces a fresh
        // mtime so the second write is reliably detected on the next read.
        var store = new ClipboardStore();
        var entry = NewEntry("clip_mtime_1", "mtime");
        store.AddOrPromote(entry, SettingsWith(limit: 200));

        // Force a second mutation whose Save() is called immediately after the first.
        store.MarkUsed("clip_mtime_1", SettingsWith(limit: 200));

        // Simulate an external process overwriting the file with the same content but
        // forcing an identical mtime to the last Save() — without the SetLastWriteTimeUtc
        // bump in Save(), EnsureFresh would miss this. We use the same second offset trick
        // the test author added to WriteHistoryFile, but the goal is different: we are
        // verifying that the second internal MarkUsed produced a later mtime.
        DateTime after = File.GetLastWriteTimeUtc(_historyPath);

        store.MarkUsed("clip_mtime_1", SettingsWith(limit: 200));

        DateTime later = File.GetLastWriteTimeUtc(_historyPath);
        Assert.True(later > after, $"Expected later mtime but got {later:o} vs {after:o}");
    }

    private static ClipboardEntry NewEntry(string id, string text) => new()
    {
        Id = id,
        GroupId = "grp_" + id,
        Kind = ClipboardEntryKind.Text,
        CreatedAt = DateTimeOffset.UtcNow,
        Title = text,
        Text = text,
        ContentHash = ClipboardStore.BuildHash("text", text),
    };

    private static ClipboardAppSettings SettingsWith(int limit) => new()
    {
        RetentionLimit = limit,
    };

    private void WriteHistoryFile(List<ClipboardEntry> entries)
    {
        string json = JsonSerializer.Serialize(entries, ClipboardJsonContext.Default.ListClipboardEntry);
        File.WriteAllText(_historyPath, json);

        // Ensure NTFS modification time is distinct so the check registers.
        // File.SetLastWriteTimeUtc is used to manually increment the write time.
        File.SetLastWriteTimeUtc(_historyPath, DateTime.UtcNow.AddSeconds(1));
    }
}
