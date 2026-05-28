using System.Collections.Generic;
using System.Text.Json.Serialization;
using NpuTools.Awake.Models;

namespace NpuTools.Awake.Services;

[JsonSerializable(typeof(AwakeSettings))]
[JsonSerializable(typeof(AwakeStateFile))]
[JsonSerializable(typeof(AwakeHeartbeat))]
[JsonSerializable(typeof(List<AwakeSchedule>))]
[JsonSerializable(typeof(AwakeIntent))]
[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = true)]
internal sealed partial class AwakeJsonContext : JsonSerializerContext
{
}
