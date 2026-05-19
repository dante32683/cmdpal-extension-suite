using System;
using System.Diagnostics;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace NpuTools.Clipboard.Commands;

internal sealed partial class RevealInExplorerCommand : InvokableCommand
{
    private readonly string _path;

    public RevealInExplorerCommand(string path, string? displayName = null)
    {
        _path = path;
        Name  = displayName ?? "Open File Location";
        Icon  = ClipboardVisuals.Folder;
    }

    public override CommandResult Invoke()
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{_path}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"RevealInExplorerCommand failed: {ex.GetType().Name}: {ex.Message}");
        }

        return CommandResult.Dismiss();
    }
}
