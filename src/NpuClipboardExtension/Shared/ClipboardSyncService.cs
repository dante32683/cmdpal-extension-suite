using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace NpuTools.Clipboard.Data;

// Writes and reads text clipboard entries to/from a shared folder (e.g. OneDrive).
// Only text entries are synced — images and file paths are device-local.
public static class ClipboardSyncService
{
    public readonly record struct SyncEntry(ClipboardEntry Entry, string SourcePath);

    private static string SyncDir(string syncFolder) =>
        Path.Combine(syncFolder, "clipboard-sync");

    public static void WriteEntry(ClipboardEntry entry, string? syncFolder, ClipboardAppSettings? settings = null)
    {
        if (string.IsNullOrWhiteSpace(syncFolder)) return;
        if (entry.Kind != ClipboardEntryKind.Text || string.IsNullOrWhiteSpace(entry.Text)) return;
        if (string.IsNullOrWhiteSpace(entry.Id)) return;
        if (settings is not null && new SecretPatternMatcher(settings).Match(entry.Text) is not null)
            return;

        try
        {
            string dir = SyncDir(syncFolder);
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, $"{SanitizeId(entry.Id)}.json");
            string tmp = $"{path}.{Environment.ProcessId}.tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(entry, ClipboardJsonContext.Default.ClipboardEntry));
            File.Move(tmp, path, overwrite: true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ClipboardSyncService.WriteEntry failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    public static IReadOnlyList<ClipboardEntry> ReadNewEntries(string? syncFolder, ISet<string> knownIds)
        => ReadNewEntriesWithPaths(syncFolder, knownIds).Select(item => item.Entry).ToArray();

    public static IReadOnlyList<SyncEntry> ReadNewEntriesWithPaths(string? syncFolder, ISet<string> knownIds)
    {
        if (string.IsNullOrWhiteSpace(syncFolder)) return [];

        string dir = SyncDir(syncFolder);
        if (!Directory.Exists(dir)) return [];

        string thisMachine = Environment.MachineName;
        var result = new List<SyncEntry>();

        try
        {
            foreach (string file in Directory.EnumerateFiles(dir, "*.json"))
            {
                try
                {
                    string json = File.ReadAllText(file);
                    var entry = JsonSerializer.Deserialize(json, ClipboardJsonContext.Default.ClipboardEntry);
                    if (entry is null) continue;
                    if (string.IsNullOrWhiteSpace(entry.Id)) continue;
                    if (knownIds.Contains(entry.Id)) continue;

                    // Skip entries from this machine — they're already in local history.
                    if (string.Equals(entry.SourceDevice, thisMachine, StringComparison.OrdinalIgnoreCase)) continue;

                    // Only merge text entries — images/files don't transfer across devices.
                    if (entry.Kind != ClipboardEntryKind.Text) continue;
                    if (string.IsNullOrWhiteSpace(entry.Text)) continue;

                    result.Add(new SyncEntry(entry, file));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"ClipboardSyncService: skipping {file}: {ex.GetType().Name}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ClipboardSyncService.ReadNewEntries failed: {ex.GetType().Name}: {ex.Message}");
        }

        return result;
    }

    // Removes sync entries older than the given cutoff to keep the sync folder tidy.
    public static void PruneOldEntries(string? syncFolder, DateTimeOffset cutoff)
    {
        if (string.IsNullOrWhiteSpace(syncFolder)) return;

        string dir = SyncDir(syncFolder);
        if (!Directory.Exists(dir)) return;

        try
        {
            foreach (string file in Directory.EnumerateFiles(dir, "*.json"))
            {
                try
                {
                    if (File.GetLastWriteTimeUtc(file) < cutoff.UtcDateTime)
                        File.Delete(file);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"ClipboardSyncService.PruneOldEntries: skipping {file}: {ex.GetType().Name}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ClipboardSyncService.PruneOldEntries failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static string SanitizeId(string id)
    {
        // Entry IDs are already alphanumeric/underscore but strip anything unsafe for filenames.
        var sb = new System.Text.StringBuilder(id.Length);
        foreach (char c in id)
        {
            if (char.IsLetterOrDigit(c) || c == '_' || c == '-')
                sb.Append(c);
            else
                sb.Append('_');
        }
        return sb.ToString();
    }
}
