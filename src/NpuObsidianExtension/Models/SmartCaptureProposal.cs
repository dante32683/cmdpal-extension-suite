using System.Collections.Generic;

namespace NpuTools.Obsidian.Models;

internal sealed class SmartCaptureProposal
{
    public string Title { get; set; } = "";

    public string Folder { get; set; } = "";

    public List<string> Tags { get; set; } = [];

    public string Body { get; set; } = "";
}
