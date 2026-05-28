using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NpuTools.Clipboard.Data;
using Xunit;

namespace NpuTools.Tests;

public sealed class ClipboardSyncServiceTests : IDisposable
{
    private readonly string _syncFolder;
    private readonly string _syncDir;

    public ClipboardSyncServiceTests()
    {
        _syncFolder = Path.Combine(Path.GetTempPath(), "NpuSyncTest_" + Guid.NewGuid().ToString("N")[..8]);
        _syncDir    = Path.Combine(_syncFolder, "clipboard-sync");
        Directory.CreateDirectory(_syncFolder);
    }

    public void Dispose()
    {
        try { Directory.Delete(_syncFolder, recursive: true); } catch { }
    }

    // ── WriteEntry ──────────────────────────────────────────────────────────

    [Fact]
    public void WriteEntry_CreatesJsonFileForTextEntry()
    {
        var entry = TextEntry("clip_1", "hello world", device: "REMOTE-PC");
        ClipboardSyncService.WriteEntry(entry, _syncFolder);
        Assert.Single(Directory.GetFiles(_syncDir, "*.json"));
    }

    [Fact]
    public void WriteEntry_DoesNotWriteImageEntry()
    {
        var entry = new ClipboardEntry
        {
            Id = "clip_img_1", Kind = ClipboardEntryKind.Image,
            Title = "Image", ContentHash = "h1", SourceDevice = "REMOTE-PC",
            ImagePath = "C:\\fake.png",
        };
        ClipboardSyncService.WriteEntry(entry, _syncFolder);
        Assert.False(Directory.Exists(_syncDir));
    }

    [Fact]
    public void WriteEntry_DoesNotWriteFilesEntry()
    {
        var entry = new ClipboardEntry
        {
            Id = "clip_files_1", Kind = ClipboardEntryKind.Files,
            Title = "Files", ContentHash = "h2", SourceDevice = "REMOTE-PC",
            FilePaths = ["C:\\foo.txt"],
        };
        ClipboardSyncService.WriteEntry(entry, _syncFolder);
        Assert.False(Directory.Exists(_syncDir));
    }

    [Fact]
    public void WriteEntry_SkipsWhenSyncFolderIsNull()
    {
        var entry = TextEntry("clip_2", "test", device: "REMOTE-PC");
        ClipboardSyncService.WriteEntry(entry, null);
        Assert.False(Directory.Exists(_syncDir));
    }

    // ── ReadNewEntries ───────────────────────────────────────────────────────

    [Fact]
    public void ReadNewEntries_ReturnsEntriesFromOtherDevices()
    {
        var entry = TextEntry("clip_3", "from other machine", device: "OTHER-PC");
        ClipboardSyncService.WriteEntry(entry, _syncFolder);

        var result = ClipboardSyncService.ReadNewEntries(_syncFolder, new HashSet<string>());

        Assert.Single(result);
        Assert.Equal("clip_3", result[0].Id);
        Assert.Equal("OTHER-PC", result[0].SourceDevice);
    }

    [Fact]
    public void ReadNewEntries_SkipsEntriesFromThisMachine()
    {
        string thisDevice = Environment.MachineName;
        var entry = TextEntry("clip_local", "local capture", device: thisDevice);
        ClipboardSyncService.WriteEntry(entry, _syncFolder);

        var result = ClipboardSyncService.ReadNewEntries(_syncFolder, new HashSet<string>());

        Assert.Empty(result);
    }

    [Fact]
    public void ReadNewEntries_SkipsKnownIds()
    {
        var entry = TextEntry("clip_known", "already imported", device: "OTHER-PC");
        ClipboardSyncService.WriteEntry(entry, _syncFolder);

        var knownIds = new HashSet<string> { "clip_known" };
        var result = ClipboardSyncService.ReadNewEntries(_syncFolder, knownIds);

        Assert.Empty(result);
    }

    [Fact]
    public void ReadNewEntries_ReturnsMixOfNewAndFiltersKnown()
    {
        ClipboardSyncService.WriteEntry(TextEntry("clip_a", "new one", device: "OTHER-PC"), _syncFolder);
        ClipboardSyncService.WriteEntry(TextEntry("clip_b", "already known", device: "OTHER-PC"), _syncFolder);

        var known = new HashSet<string> { "clip_b" };
        var result = ClipboardSyncService.ReadNewEntries(_syncFolder, known);

        Assert.Single(result);
        Assert.Equal("clip_a", result[0].Id);
    }

    [Fact]
    public void ReadNewEntries_ReturnsEmptyWhenFolderDoesNotExist()
    {
        var result = ClipboardSyncService.ReadNewEntries(Path.Combine(Path.GetTempPath(), "NpuNoSuchFolder_" + Guid.NewGuid()), new HashSet<string>());
        Assert.Empty(result);
    }

    [Fact]
    public void ReadNewEntries_ReturnsEmptyWhenSyncFolderIsNull()
    {
        var result = ClipboardSyncService.ReadNewEntries(null, new HashSet<string>());
        Assert.Empty(result);
    }

    // ── PruneOldEntries ─────────────────────────────────────────────────────

    [Fact]
    public void PruneOldEntries_DeletesFilesOlderThanCutoff()
    {
        var entry = TextEntry("clip_old", "old entry", device: "OTHER-PC");
        ClipboardSyncService.WriteEntry(entry, _syncFolder);

        string filePath = Path.Combine(_syncDir, "clip_old.json");
        Assert.True(File.Exists(filePath));
        File.SetLastWriteTimeUtc(filePath, DateTime.UtcNow.AddDays(-40));

        ClipboardSyncService.PruneOldEntries(_syncFolder, DateTimeOffset.UtcNow.AddDays(-30));

        Assert.False(File.Exists(filePath));
    }

    [Fact]
    public void PruneOldEntries_KeepsFilesNewerThanCutoff()
    {
        var entry = TextEntry("clip_new", "recent entry", device: "OTHER-PC");
        ClipboardSyncService.WriteEntry(entry, _syncFolder);

        ClipboardSyncService.PruneOldEntries(_syncFolder, DateTimeOffset.UtcNow.AddDays(-30));

        Assert.Single(Directory.GetFiles(_syncDir, "*.json"));
    }

    [Fact]
    public void PruneOldEntries_NoopsWhenFolderMissing()
    {
        ClipboardSyncService.PruneOldEntries(Path.Combine(Path.GetTempPath(), "NpuNoSuchFolder_" + Guid.NewGuid()), DateTimeOffset.UtcNow);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static ClipboardEntry TextEntry(string id, string text, string device) => new()
    {
        Id = id,
        GroupId = "grp_1",
        Kind = ClipboardEntryKind.Text,
        CreatedAt = DateTimeOffset.UtcNow,
        Title = text[..Math.Min(text.Length, 40)],
        Text = text,
        ContentHash = ClipboardStore_BuildHash("text", text),
        SourceDevice = device,
    };

    // Duplicate of ClipboardStore.BuildHash to avoid pulling in ClipboardStore (which has test-incompatible dependencies).
    private static string ClipboardStore_BuildHash(string prefix, string value)
    {
        byte[] bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(prefix + "\n" + value));
        return Convert.ToHexString(bytes);
    }
}
