using System;
using System.Collections.Generic;

namespace NpuTools.Obsidian.Models;

internal sealed class ObsidianVaultIndex
{
    public List<ObsidianPinEntry> Pinned { get; set; } = [];

    public List<ObsidianRecentEntry> Recent { get; set; } = [];
}

internal sealed class ObsidianPinEntry
{
    public string Path { get; set; } = "";

    public int PinOrder { get; set; }
}

internal sealed class ObsidianRecentEntry
{
    public string Path { get; set; } = "";

    public DateTimeOffset OpenedUtc { get; set; }
}
