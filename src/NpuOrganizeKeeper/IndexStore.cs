using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NpuOrganizeKeeper;

// The keeper and the Command Palette extension share this small JSON contract.
// The named mutex and atomic replacement make a rename safe across processes.
internal sealed class OrganizeIndexStore
{
    private static readonly string IndexPath = Path.Combine(
        Environment.GetEnvironmentVariable("LOCALAPPDATA")
            ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NpuOrganize", "index.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
    };

    public static void UpdatePath(string oldPath, string newPath, string description)
    {
        using var mutex = new Mutex(false, MutexName());
        try { mutex.WaitOne(); } catch (AbandonedMutexException) { }

        try
        {
            List<IndexEntry> entries = [];
            if (File.Exists(IndexPath))
            {
                try
                {
                    entries = JsonSerializer.Deserialize<List<IndexEntry>>(File.ReadAllText(IndexPath), JsonOptions) ?? [];
                }
                catch { /* A malformed index is not allowed to block a successful rename. */ }
            }

            IndexEntry? existing = entries.FirstOrDefault(e =>
                string.Equals(e.FilePath, oldPath, StringComparison.OrdinalIgnoreCase));
            entries.RemoveAll(e => string.Equals(e.FilePath, oldPath, StringComparison.OrdinalIgnoreCase)
                || string.Equals(e.FilePath, newPath, StringComparison.OrdinalIgnoreCase));

            if (existing is not null)
                existing.FilePath = newPath;
            else
            {
                existing = new IndexEntry
                {
                    FilePath = newPath,
                    Description = description,
                    IndexedAt = DateTimeOffset.Now,
                };
            }

            entries.Add(existing);
            Directory.CreateDirectory(Path.GetDirectoryName(IndexPath)!);
            string tmp = $"{IndexPath}.{Environment.ProcessId}.{DateTime.UtcNow.Ticks}.tmp";
            try
            {
                File.WriteAllText(tmp, JsonSerializer.Serialize(entries, JsonOptions));
                File.Move(tmp, IndexPath, overwrite: true);
            }
            finally
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            }
        }
        finally
        {
            try { mutex.ReleaseMutex(); } catch { }
        }
    }

    private static string MutexName()
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(IndexPath));
        return $"Local\\NpuOrganizeIndex-{Convert.ToHexString(hash)[..24]}";
    }

    private sealed class IndexEntry
    {
        [JsonPropertyName("filePath")] public string FilePath { get; set; } = string.Empty;
        [JsonPropertyName("description")] public string Description { get; set; } = string.Empty;
        [JsonPropertyName("ocrText")] public string OcrText { get; set; } = string.Empty;
        [JsonPropertyName("indexedAt")] public DateTimeOffset IndexedAt { get; set; }
    }
}
