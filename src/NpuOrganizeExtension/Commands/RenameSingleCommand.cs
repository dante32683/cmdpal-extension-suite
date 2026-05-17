using System;
using System.Diagnostics;
using System.IO;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.Organize.Models;

namespace NpuTools.Organize.Commands;

internal sealed partial class RenameSingleCommand : InvokableCommand
{
    private readonly RenameProposal _proposal;

    public RenameSingleCommand(RenameProposal proposal)
    {
        _proposal = proposal;
        Name      = "Rename";
        Icon      = OrganizeVisuals.Rename;
    }

    public override CommandResult Invoke()
    {
        try
        {
            File.Move(_proposal.OriginalPath, _proposal.ProposedPath, overwrite: false);
            return CommandResult.ShowToast($"Renamed: {_proposal.ProposedName}");
        }
        catch (Exception ex)
        {
            return CommandResult.ShowToast($"Rename failed: {ex.Message}");
        }
    }
}
