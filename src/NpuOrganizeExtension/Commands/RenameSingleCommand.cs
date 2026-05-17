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
    private readonly ScreenshotIndexService _indexService;

    public RenameSingleCommand(RenameProposal proposal, ScreenshotIndexService indexService)
    {
        _proposal     = proposal;
        _indexService = indexService;
        Name          = "Rename";
        Icon          = OrganizeVisuals.Rename;
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
            var (destination, description, ocrText) = await AiNamingService.BuildProposedPathWithDataAsync(_proposal.OriginalPath);
            File.Move(_proposal.OriginalPath, destination, overwrite: false);
            _indexService.Upsert(destination, description, ocrText);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Rename failed for '{_proposal.OriginalPath}': {ex.GetType().Name}: {ex.Message}");
        }
    }
}
