using System.Diagnostics;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.DevToolbox.Services;

namespace NpuTools.DevToolbox.Commands;

internal sealed partial class OpenInExplorerCommand : InvokableCommand
{
    private readonly string _path;
    private readonly RecentWorkspacesStore _recents;

    public OpenInExplorerCommand(string path, RecentWorkspacesStore recents)
    {
        _path = path;
        _recents = recents;
        Name = "Open in Explorer";
        Icon = DevToolboxVisuals.Explorer;
    }

    public override CommandResult Invoke()
    {
        _recents.Add(_path);
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"\"{_path}\"",
            UseShellExecute = false,
        });
        return CommandResult.Dismiss();
    }
}
