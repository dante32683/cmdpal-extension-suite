using System;
using System.Collections.Generic;

namespace NpuTools.Obsidian.Models;

internal sealed class ObsidianNote
{
    public string AbsolutePath { get; set; } = "";

    public string RelativePath { get; set; } = "";

    public string VaultPath { get; set; } = "";

    public string Title { get; set; } = "";

    public List<string> Tags { get; set; } = [];

    public List<string> Aliases { get; set; } = [];

    public List<string> Headings { get; set; } = [];

    public string Body { get; set; } = "";

    public DateTimeOffset LastModifiedUtc { get; set; }

    public bool IsPinned { get; set; }

    public int? PinOrder { get; set; }

    public DateTimeOffset? LastOpenedUtc { get; set; }

    public string Snippet
    {
        get
        {
            string compact = string.Join(' ', Body.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            return compact.Length > 160 ? compact[..160] + "..." : compact;
        }
    }
}
