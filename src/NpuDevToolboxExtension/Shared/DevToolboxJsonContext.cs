using System.Collections.Generic;
using System.Text.Json.Serialization;
using NpuTools.DevToolbox.Models;

namespace NpuTools.DevToolbox.Shared;

[JsonSerializable(typeof(DevToolboxSettings))]
[JsonSerializable(typeof(List<string>))]
[JsonSourceGenerationOptions(WriteIndented = false, UseStringEnumConverter = true)]
internal sealed partial class DevToolboxJsonContext : JsonSerializerContext
{
}
