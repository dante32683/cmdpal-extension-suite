using System.IO;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.Obsidian.Commands;
using NpuTools.Obsidian.Services;

namespace NpuTools.Obsidian.Pages;

internal sealed partial class CreateObsidianNotePage : DynamicListPage
{
    private readonly ObsidianVaultStore _store;
    private readonly ObsidianSettingsStore _settings;
    private IListItem[] _items;

    public CreateObsidianNotePage(ObsidianVaultStore store, ObsidianSettingsStore settings)
    {
        _store = store;
        _settings = settings;
        Id = "com.local.nputools.obsidian.create";
        Title = "New Obsidian Note";
        Name = "New Note";
        Icon = ObsidianVisuals.Add;
        PlaceholderText = "Note title...";
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

        var current = _settings.Current;

        if (text.Length == 0)
        {
            return
            [
                new ListItem(new OpenNewNoteInObsidianCommand(_settings))
                {
                    Title = "Open New Note in Obsidian",
                    Subtitle = "Use obsidian://new to launch Obsidian",
                    Icon = ObsidianVisuals.Open,
                },
                new ListItem(new CreateNoteAndOpenCommand(_store, _settings, "Untitled", string.Empty))
                {
                    Title = "Create Blank Note",
                    Subtitle = string.IsNullOrWhiteSpace(current.DefaultNewNoteFolder)
                        ? "Saved to vault root"
                        : $"Saved to {current.DefaultNewNoteFolder}",
                    Icon = ObsidianVisuals.Add,
                },
                new ListItem(new OpenVaultFolderCommand(_settings))
                {
                    Title = "Open Vault Folder",
                    Subtitle = current.VaultPath,
                    Icon = ObsidianVisuals.Folder,
                },
            ];
        }

        string title = DeriveTitle(text);
        return
        [
            new ListItem(new CreateNoteAndOpenCommand(_store, _settings, title, text))
            {
                Title = $"Create: {title}",
                Subtitle = Preview(text),
                Icon = ObsidianVisuals.Add,
                Tags = [ObsidianVisuals.MutedTag("press Enter")],
            },
            new ListItem(new OpenNewNoteInObsidianCommand(_settings))
            {
                Title = "Open New Note in Obsidian instead",
                Icon = ObsidianVisuals.Open,
            },
        ];
    }

    private static string DeriveTitle(string text)
    {
        foreach (string line in text.Split(['\r', '\n'], System.StringSplitOptions.TrimEntries | System.StringSplitOptions.RemoveEmptyEntries))
        {
            string trimmed = line.TrimStart('#').Trim();
            if (trimmed.Length > 0)
                return trimmed.Length > 80 ? trimmed[..80].Trim() : trimmed;
        }

        return "Untitled";
    }

    private static string Preview(string text)
    {
        string compact = string.Join(' ', text.Split(['\r', '\n'], System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries));
        return compact.Length > 120 ? compact[..120] + "..." : compact;
    }
}
