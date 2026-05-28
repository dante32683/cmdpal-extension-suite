using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.Obsidian.Commands;
using NpuTools.Obsidian.Models;
using NpuTools.Obsidian.Services;

namespace NpuTools.Obsidian.Pages;

internal sealed partial class RenameObsidianNotePage : DynamicListPage
{
    private readonly ObsidianVaultStore _store;
    private readonly ObsidianIndexStore _indexStore;
    private readonly ObsidianNote _note;
    private IListItem[] _items;

    public RenameObsidianNotePage(ObsidianVaultStore store, ObsidianIndexStore indexStore, ObsidianNote note)
    {
        _store = store;
        _indexStore = indexStore;
        _note = note;
        Id = "com.local.nputools.obsidian.rename";
        Title = $"Rename: {note.Title}";
        Name = "Rename";
        Icon = ObsidianVisuals.Edit;
        PlaceholderText = "New note title...";
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
        if (string.IsNullOrWhiteSpace(text))
        {
            return
            [
                new ListItem(new NoOpCommand())
                {
                    Title = "Type a new title for this note",
                    Subtitle = _note.Title,
                    Icon = ObsidianVisuals.Note,
                },
            ];
        }

        string newTitle = text.Length > 120 ? text[..120].Trim() : text;
        return
        [
            new ListItem(new RenameObsidianNoteCommand(_store, _indexStore, _note.AbsolutePath, newTitle))
            {
                Title = $"Rename to: {newTitle}",
                Subtitle = $"Current: {_note.Title}",
                Icon = ObsidianVisuals.Edit,
                Tags = [ObsidianVisuals.MutedTag("press Enter")],
            },
        ];
    }
}
