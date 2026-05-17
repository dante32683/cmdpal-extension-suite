using System;
using System.Diagnostics;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace NpuTools.Organize.Commands;

internal sealed partial class OpenFolderCommand : InvokableCommand
{
    private readonly string _path;

    public OpenFolderCommand(string path)
    {
        _path = path;
        Name  = "Open Folder";
        Icon  = OrganizeVisuals.Folder;
    }

    public override CommandResult Invoke()
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_path}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Organize open folder failed: {ex}");
        }

        return CommandResult.Dismiss();
    }
}
