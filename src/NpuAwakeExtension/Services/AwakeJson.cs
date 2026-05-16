using System.Text.Json;
using System.Text.Json.Serialization;

namespace NpuTools.Awake.Services;

internal static class AwakeJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };

    public static T Read<T>(string path, T fallback)
    {
        try
        {
            if (!File.Exists(path))
            {
                return fallback;
            }

            string raw = File.ReadAllText(path).Trim();
            return raw.Length == 0 ? fallback : JsonSerializer.Deserialize<T>(raw, Options) ?? fallback;
        }
        catch
        {
            return fallback;
        }
    }

    public static void AtomicWrite<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string tmp = $"{path}.{Environment.ProcessId}.{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(value, Options));
        File.Move(tmp, path, true);
    }
}
