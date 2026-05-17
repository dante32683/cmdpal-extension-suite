using System;
using System.IO;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.Organize.Models;
using NpuTools.Organize.Services;

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
            string destination = AiNamingService.BuildProposedPath(_proposal.OriginalPath);
            File.Move(_proposal.OriginalPath, destination, overwrite: false);
            return CommandResult.ShowToast($"Renamed: {Path.GetFileName(destination)}");
        }
        catch (Exception ex)
        {
            return CommandResult.ShowToast($"Rename failed: {ex.Message}");
        }
    }
}
