using System;
using System.Text.Json.Serialization;

namespace NpuTools.Organize.Models;

internal sealed class ScreenshotIndexEntry
{
    [JsonPropertyName("filePath")]    public string         FilePath    { get; set; } = string.Empty;
    [JsonPropertyName("description")] public string         Description { get; set; } = string.Empty;
    [JsonPropertyName("ocrText")]     public string         OcrText     { get; set; } = string.Empty;
    [JsonPropertyName("indexedAt")]   public DateTimeOffset IndexedAt   { get; set; }
}
