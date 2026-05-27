using System;
using System.Collections.Generic;

namespace NpuTools.Obsidian.Models;

internal sealed class ObsidianIndexEntry
{
    public string AbsolutePath { get; set; } = "";

    public string RelativePath { get; set; } = "";

    public string Title { get; set; } = "";

    public List<string> Aliases { get; set; } = [];

    public List<string> Tags { get; set; } = [];

    public List<string> Headings { get; set; } = [];

    public List<string> WikiLinks { get; set; } = [];

    public List<string> Backlinks { get; set; } = [];

    public string BodyText { get; set; } = "";

    public string AiSummary { get; set; } = "";

    public DateTimeOffset LastModifiedUtc { get; set; }

    public DateTimeOffset IndexedUtc { get; set; }

    public long FileSizeBytes { get; set; }
}
