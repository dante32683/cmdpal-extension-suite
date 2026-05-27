using System.Collections.Generic;
using System.Text.Json.Serialization;
using NpuTools.Obsidian.Models;

namespace NpuTools.Obsidian.Shared;

[JsonSerializable(typeof(ObsidianVaultSettings))]
[JsonSerializable(typeof(ObsidianVaultIndex))]
[JsonSerializable(typeof(List<ObsidianNote>))]
[JsonSourceGenerationOptions(WriteIndented = false)]
internal sealed partial class ObsidianJsonContext : JsonSerializerContext
{
}
