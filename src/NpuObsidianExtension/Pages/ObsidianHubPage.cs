using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.Obsidian.Commands;
using NpuTools.Obsidian.Models;
using NpuTools.Obsidian.Services;

namespace NpuTools.Obsidian.Pages;

internal sealed partial class ObsidianHubPage : ListPage
{
    private readonly ObsidianVaultStore _store;
    private readonly ObsidianSettingsStore _settings;
    private readonly ObsidianSearchService _search;

    public ObsidianHubPage(ObsidianVaultStore store, ObsidianSettingsStore settings, ObsidianSearchService search)
    {
        _store = store;
        _settings = settings;
        _search = search;
        Id = "com.local.nputools.obsidian.hub";
        Title = "Obsidian";
        Name = "Open";
        Icon = ObsidianVisuals.Hub;
    }

    public override IListItem[] GetItems()
    {
        var current = _settings.Current;
        bool vaultOk = _store.IsVaultConfigured();

        var items = new List<IListItem>();

        if (!vaultOk)
        {
            items.Add(new ListItem(new NoOpCommand())
            {
                Title = string.IsNullOrWhiteSpace(current.VaultPath)
                    ? "Vault path not configured"
                    : $"Vault not found: {current.VaultPath}",
                Subtitle = "Open settings and set the vault path first",
                Icon = ObsidianVisuals.Warning,
                Tags = [ObsidianVisuals.WarningTag("setup required")],
            });
        }
        else
        {
            string vaultName = !string.IsNullOrWhiteSpace(current.VaultName)
                ? current.VaultName
                : Path.GetFileName(current.VaultPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

            items.Add(new ListItem(new NoOpCommand())
            {
                Title = vaultName,
                Subtitle = current.VaultPath,
                Icon = ObsidianVisuals.Hub,
                Tags = [ObsidianVisuals.VaultTag("vault")],
            });
        }

        items.Add(new ListItem(new SearchObsidianNotesPage(_store, _settings, _search))
        {
            Title = "Search Notes",
            Subtitle = "Search by title, tags, headings, or content",
            Icon = ObsidianVisuals.Search,
        });

        items.Add(new ListItem(new CreateObsidianNotePage(_store, _settings))
        {
            Title = "New Note",
            Subtitle = "Create a new Markdown note",
            Icon = ObsidianVisuals.Add,
        });

        items.Add(new ListItem(new OpenDailyNoteCommand(_settings))
        {
            Title = "Open Daily Note",
            Subtitle = "Open today's daily note in Obsidian",
            Icon = ObsidianVisuals.Daily,
        });

        if (vaultOk)
        {
            items.Add(new ListItem(new OpenVaultFolderCommand(_settings))
            {
                Title = "Open Vault Folder",
                Subtitle = current.VaultPath,
                Icon = ObsidianVisuals.Folder,
            });
        }

        if (vaultOk)
        {
            var notes = _store.GetAll();
            var pinned = notes.Where(n => n.IsPinned).OrderBy(n => n.PinOrder ?? int.MaxValue).Take(current.MaxRecentNotes).ToList();
            var recent = notes.Where(n => !n.IsPinned).OrderByDescending(n => n.LastOpenedUtc ?? n.LastModifiedUtc).Take(current.MaxRecentNotes).ToList();

            AddSection(items, "Pinned Notes", pinned);
            AddSection(items, "Recent Notes", recent);

            if (notes.Count == 0)
            {
                items.Add(new ListItem(new NoOpCommand())
                {
                    Title = "No notes found in vault",
                    Subtitle = "Create a note or check the vault path in settings",
                    Icon = ObsidianVisuals.Note,
                });
            }
        }

        return [.. items];
    }

    private void AddSection(List<IListItem> items, string sectionTitle, List<ObsidianNote> notes)
    {
        if (notes.Count == 0)
            return;

        items.Add(new ListItem(new NoOpCommand())
        {
            Title = sectionTitle,
            Tags = [ObsidianVisuals.MutedTag("section")],
        });

        foreach (var note in notes)
            items.Add(NoteItemFactory.Build(_store, _settings, note));
    }
}
