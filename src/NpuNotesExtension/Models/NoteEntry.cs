using System;
using System.Collections.Generic;

namespace NpuTools.Notes.Models;

internal sealed class NoteEntry
{
    public string Id { get; set; } = "";

    public string Title { get; set; } = "";

    public string Category { get; set; } = "misc";

    public string FilePath { get; set; } = "";

    public string RelativePath { get; set; } = "";

    public DateTimeOffset CreatedUtc { get; set; }

    public DateTimeOffset UpdatedUtc { get; set; }

    public List<string> Tags { get; set; } = [];

    public string Body { get; set; } = "";

    public bool IsPinned { get; set; }

    public int? PinOrder { get; set; }

    public DateTimeOffset? LastOpenedUtc { get; set; }

    public string Snippet => BuildSnippet(Body);

    private static string BuildSnippet(string body)
    {
        string compact = string.Join(' ', body.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return compact.Length > 160 ? compact[..160] + "..." : compact;
    }
}
