using System.Collections.Generic;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.Obsidian.Commands;
using NpuTools.Obsidian.Models;
using NpuTools.Obsidian.Services;

namespace NpuTools.Obsidian.Pages;

internal sealed partial class MoveObsidianNotePage : ListPage
{
    private readonly ObsidianVaultStore _store;
    private readonly ObsidianIndexStore _indexStore;
    private readonly ObsidianNote _note;

    public MoveObsidianNotePage(ObsidianVaultStore store, ObsidianIndexStore indexStore, ObsidianNote note)
    {
        _store = store;
        _indexStore = indexStore;
        _note = note;
        Id = "com.local.nputools.obsidian.move";
        Title = $"Move: {note.Title}";
        Name = "Move";
        Icon = ObsidianVisuals.Folder;
    }

    public override IListItem[] GetItems()
    {
        var note = _store.GetByPath(_note.AbsolutePath);
        if (note is null)
        {
            return
            [
                new ListItem(new NoOpCommand())
                {
                    Title = "Note no longer exists",
                    Subtitle = _note.AbsolutePath,
                    Icon = ObsidianVisuals.Warning,
                },
            ];
        }

        string currentDir = System.IO.Path.GetDirectoryName(note.AbsolutePath) ?? string.Empty;
        var items = new List<IListItem>();

        // Vault root
        items.Add(BuildFolderItem(note, string.Empty, currentDir));

        foreach (string relative in _store.GetVaultSubfolders())
            items.Add(BuildFolderItem(note, relative, currentDir));

        return [.. items];
    }

    private ListItem BuildFolderItem(ObsidianNote note, string relativeDir, string currentDir)
    {
        string vaultPath = note.VaultPath;
        string absTarget = string.IsNullOrWhiteSpace(relativeDir)
            ? vaultPath
            : System.IO.Path.Combine(vaultPath, relativeDir);

        bool isCurrent = string.Equals(
            System.IO.Path.GetFullPath(absTarget),
            System.IO.Path.GetFullPath(currentDir),
            System.StringComparison.OrdinalIgnoreCase);

        string label = string.IsNullOrWhiteSpace(relativeDir) ? "Vault root" : relativeDir;

        return new ListItem(new MoveObsidianNoteCommand(_store, _indexStore, note.AbsolutePath, relativeDir))
        {
            Title = label,
            Subtitle = absTarget,
            Icon = ObsidianVisuals.Folder,
            Tags = isCurrent ? [ObsidianVisuals.StatusTag("current")] : [],
        };
    }
}
