using System;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.Obsidian.Commands;
using NpuTools.Obsidian.Services;

namespace NpuTools.Obsidian.Pages;

// DynamicListPage: user types rough text; pressing Enter on the AI item opens SmartCaptureProposalPage.
internal sealed partial class SmartCapturePage : DynamicListPage
{
    private readonly ObsidianVaultStore _store;
    private readonly ObsidianSettingsStore _settings;
    private readonly ObsidianAiService _ai;
    private IListItem[] _items;

    public SmartCapturePage(ObsidianVaultStore store, ObsidianSettingsStore settings, ObsidianAiService ai)
    {
        _store = store;
        _settings = settings;
        _ai = ai;
        Id = "com.local.nputools.obsidian.smart-capture";
        Title = "Smart Capture";
        Name = "Smart Capture";
        Icon = ObsidianVisuals.Ai;
        PlaceholderText = "Type or paste rough text to capture...";
        _items = BuildItems(string.Empty);
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        _items = BuildItems(newSearch.Trim());
        RaiseItemsChanged(_items.Length);
    }

    public override IListItem[] GetItems() => _items;

    private IListItem[] BuildItems(string text)
    {
        if (!_store.IsVaultConfigured())
        {
            return
            [
                new ListItem(new NoOpCommand())
                {
                    Title = "Vault path not configured",
                    Subtitle = "Open settings and set the vault path first",
                    Icon = ObsidianVisuals.Warning,
                    Tags = [ObsidianVisuals.WarningTag("setup required")],
                },
            ];
        }

        if (text.Length < 5)
        {
            return
            [
                new ListItem(new NoOpCommand())
                {
                    Title = "Type or paste rough text to capture",
                    Subtitle = "Phi will suggest a title, folder, tags, and clean body",
                    Icon = ObsidianVisuals.Ai,
                },
            ];
        }

        string preview = text.Length > 100 ? text[..100] + "..." : text;
        return
        [
            new ListItem(new SmartCaptureProposalPage(_store, _settings, _ai, text))
            {
                Title = "Process with AI",
                Subtitle = preview,
                Icon = ObsidianVisuals.Ai,
                Tags = [ObsidianVisuals.VaultTag("Phi-powered")],
            },
            new ListItem(new CreateNoteAndOpenCommand(_store, _settings, DeriveTitle(text), text))
            {
                Title = $"Save as: {DeriveTitle(text)}",
                Subtitle = "Skip AI - create note as-is",
                Icon = ObsidianVisuals.Add,
            },
        ];
    }

    private static string DeriveTitle(string text)
    {
        foreach (string line in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string trimmed = line.TrimStart('#').Trim();
            if (trimmed.Length > 0)
                return trimmed.Length > 80 ? trimmed[..80].Trim() : trimmed;
        }
        return "Untitled";
    }
}
