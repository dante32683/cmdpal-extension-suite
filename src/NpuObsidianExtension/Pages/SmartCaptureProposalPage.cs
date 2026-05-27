using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.Obsidian.Commands;
using NpuTools.Obsidian.Models;
using NpuTools.Obsidian.Services;

namespace NpuTools.Obsidian.Pages;

// Async page: calls Phi to structure rough text, then shows a proposal the user can accept.
internal sealed partial class SmartCaptureProposalPage : ListPage
{
    private readonly ObsidianVaultStore _store;
    private readonly ObsidianSettingsStore _settings;
    private readonly ObsidianAiService _ai;
    private readonly string _roughText;
    private int _started;
    private SmartCaptureProposal? _proposal;
    private string? _errorMessage;

    public SmartCaptureProposalPage(
        ObsidianVaultStore store,
        ObsidianSettingsStore settings,
        ObsidianAiService ai,
        string roughText)
    {
        _store = store;
        _settings = settings;
        _ai = ai;
        _roughText = roughText;
        Id = "com.local.nputools.obsidian.smart-capture-proposal";
        Title = "Smart Capture";
        Name = "Process";
        Icon = ObsidianVisuals.Ai;
        ShowDetails = true;
        IsLoading = true;
    }

    public override IListItem[] GetItems()
    {
        if (Interlocked.Exchange(ref _started, 1) == 0)
            _ = Task.Run(RunAsync);

        if (_errorMessage is not null)
        {
            return
            [
                new ListItem(new NoOpCommand())
                {
                    Title = "AI processing failed",
                    Subtitle = _errorMessage,
                    Icon = ObsidianVisuals.Warning,
                    Tags = [ObsidianVisuals.WarningTag("error")],
                },
            ];
        }

        if (_proposal is null)
        {
            return
            [
                new ListItem(new NoOpCommand())
                {
                    Title = "Thinking…",
                    Subtitle = "Phi is processing your text",
                    Icon = ObsidianVisuals.Ai,
                },
            ];
        }

        var p = _proposal;
        string tagsDisplay = p.Tags.Count > 0 ? string.Join(", ", p.Tags) : "none";
        string folderDisplay = string.IsNullOrWhiteSpace(p.Folder) ? "vault root" : p.Folder;
        string subfolder = string.Equals(p.Folder, "misc", StringComparison.OrdinalIgnoreCase) ? "" : p.Folder;

        return
        [
            new ListItem(new CreateNoteAndOpenCommand(_store, _settings, p.Title, BuildBody(p), subfolder))
            {
                Title = $"Create: {p.Title}",
                Subtitle = $"Folder: {folderDisplay} · Tags: {tagsDisplay}",
                Icon = ObsidianVisuals.Add,
                Tags = [ObsidianVisuals.StatusTag("Phi proposal")],
                Details = new Details
                {
                    Title = p.Title,
                    Body = string.IsNullOrWhiteSpace(p.Body) ? _roughText : p.Body,
                    Size = ContentSize.Large,
                    Metadata =
                    [
                        new DetailsElement { Key = "Folder", Data = new DetailsLink(folderDisplay) },
                        new DetailsElement { Key = "Tags",   Data = new DetailsLink(tagsDisplay) },
                    ],
                },
            },
        ];
    }

    private async Task RunAsync()
    {
        try
        {
            IReadOnlyList<string> existing = _store.IsVaultConfigured()
                ? [.. _store.GetAll()
                    .Select(n => System.IO.Path.GetDirectoryName(n.RelativePath) ?? "")
                    .Where(f => !string.IsNullOrWhiteSpace(f))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(f => f)]
                : [];

            _proposal = await _ai.SmartCaptureAsync(_roughText, existing);
        }
        catch (Exception ex)
        {
            _errorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
            RaiseItemsChanged();
        }
    }

    private static string BuildBody(SmartCaptureProposal p)
    {
        if (p.Tags.Count == 0)
            return p.Body;

        string tagYaml = string.Join("\n", p.Tags.Select(t => $"  - {t}"));
        return $"---\ntags:\n{tagYaml}\n---\n\n{p.Body}";
    }
}
