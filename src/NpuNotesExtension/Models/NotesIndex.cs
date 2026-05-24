using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NpuTools.Notes.Models;

internal sealed class NotesIndex
{
    [JsonPropertyName("pinned")]
    public List<NotesPinEntry> Pinned { get; set; } = [];

    [JsonPropertyName("recent")]
    public List<NotesRecentEntry> Recent { get; set; } = [];
}

internal sealed class NotesPinEntry
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = "";

    [JsonPropertyName("pinOrder")]
    public int PinOrder { get; set; }
}

internal sealed class NotesRecentEntry
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = "";

    [JsonPropertyName("openedUtc")]
    public DateTimeOffset OpenedUtc { get; set; }
}
