using System.IO;
using System.Text.Json;

namespace NpuTools.Organize.Services;

internal static class OrganizeSettings
{
    internal static bool IsSkipOnBatteryEnabled(string configPath)
    {
        try
        {
            if (!File.Exists(configPath)) return true;
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(configPath));
            return !document.RootElement.TryGetProperty("skipOnBattery", out JsonElement value)
                || value.ValueKind != JsonValueKind.False;
        }
        catch
        {
            return true;
        }
    }
}
