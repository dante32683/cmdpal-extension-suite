using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
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
        _ = Task.Run(RenameAsync);
        return CommandResult.ShowToast("Renaming — AI description in progress…");
    }

    private async Task RenameAsync()
    {
        try
        {
            string destination = await AiNamingService.BuildProposedPathAsync(_proposal.OriginalPath);
            File.Move(_proposal.OriginalPath, destination, overwrite: false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Rename failed for '{_proposal.OriginalPath}': {ex.GetType().Name}: {ex.Message}");
        }
    }
}
