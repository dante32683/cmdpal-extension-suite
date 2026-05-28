using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NpuTools.Clipboard.Data;

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(List<ClipboardEntry>))]
[JsonSerializable(typeof(ClipboardEntry))]
[JsonSerializable(typeof(ClipboardAppSettings))]
[JsonSerializable(typeof(ClipboardKeeperState))]
public sealed partial class ClipboardJsonContext : JsonSerializerContext
{
}
