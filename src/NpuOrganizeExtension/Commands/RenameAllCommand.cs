using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.Organize.Models;

namespace NpuTools.Organize.Commands;

internal sealed partial class RenameAllCommand : InvokableCommand
{
    private readonly IReadOnlyList<RenameProposal> _proposals;

    public RenameAllCommand(IReadOnlyList<RenameProposal> proposals)
    {
        _proposals = proposals;
        Name       = "Rename All";
        Icon       = OrganizeVisuals.Check;
    }

    public override CommandResult Invoke()
    {
        int success = 0;
        int failed  = 0;

        foreach (var p in _proposals)
        {
            try
            {
                File.Move(p.OriginalPath, p.ProposedPath);
                success++;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Organize rename failed '{p.OriginalPath}': {ex}");
                failed++;
            }
        }

        string msg = failed > 0
            ? $"Renamed {success} file(s). {failed} failed — check permissions."
            : $"Renamed {success} file(s).";
        return CommandResult.ShowToast(msg);
    }
}
