using System.Text.Json.Serialization;

namespace NpuTools.Clipboard.Data;

// A user-configurable secret-detection rule. The Regex is compiled and run against
// captured text; if it matches, the entry is silently dropped from the clipboard
// history (and from any sync folder) so the secret never lands on disk.
public sealed class SecretPattern
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("regex")]
    public string Regex { get; set; } = "";
}
