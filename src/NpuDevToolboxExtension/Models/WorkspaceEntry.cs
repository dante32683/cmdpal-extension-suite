using System.IO;

namespace NpuTools.DevToolbox.Models;

internal sealed class WorkspaceEntry
{
    public string Path { get; init; } = string.Empty;
    public string Name => System.IO.Path.GetFileName(Path.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar)) ?? Path;
    public string ProjectType { get; init; } = string.Empty;
    public bool IsRecent { get; init; }
}
