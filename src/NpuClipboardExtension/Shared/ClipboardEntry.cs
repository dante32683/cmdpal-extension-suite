using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NpuTools.Clipboard.Data;

public sealed class ClipboardEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("groupId")]
    public string GroupId { get; set; } = "";

    [JsonPropertyName("kind")]
    public ClipboardEntryKind Kind { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("lastUsedAt")]
    public DateTimeOffset? LastUsedAt { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("customName")]
    public string? CustomName { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("ocrText")]
    public string? OcrText { get; set; }

    [JsonPropertyName("imagePath")]
    public string? ImagePath { get; set; }

    [JsonPropertyName("filePaths")]
    public List<string> FilePaths { get; set; } = [];

    [JsonPropertyName("sourceApplication")]
    public string? SourceApplication { get; set; }

    [JsonPropertyName("contentHash")]
    public string ContentHash { get; set; } = "";

    [JsonPropertyName("isPinned")]
    public bool IsPinned { get; set; }

    [JsonIgnore]
    public string DisplayName => string.IsNullOrWhiteSpace(CustomName) ? Title : CustomName!;
}
