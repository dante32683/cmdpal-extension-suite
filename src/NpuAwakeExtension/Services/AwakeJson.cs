using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace NpuTools.Awake.Services;

internal static class AwakeJson
{
    public static T Read<T>(string path, T fallback, JsonTypeInfo<T> typeInfo)
    {
        try
        {
            if (!File.Exists(path))
                return fallback;

            string raw = File.ReadAllText(path).Trim();
            return raw.Length == 0 ? fallback : JsonSerializer.Deserialize(raw, typeInfo) ?? fallback;
        }
        catch
        {
            return fallback;
        }
    }

    public static void AtomicWrite<T>(string path, T value, JsonTypeInfo<T> typeInfo)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string tmp = $"{path}.{Environment.ProcessId}.{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(value, typeInfo));
        File.Move(tmp, path, true);
    }
}
