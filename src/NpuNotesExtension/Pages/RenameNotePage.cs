using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.Notes.Commands;
using NpuTools.Notes.Models;
using NpuTools.Notes.Services;

namespace NpuTools.Notes.Pages;

internal sealed partial class RenameNotePage : DynamicListPage
{
    private readonly NotesStore _store;
    private readonly NoteEntry _entry;
    private IListItem[] _items;

    public RenameNotePage(NotesStore store, NoteEntry entry)
    {
        _store = store;
        _entry = entry;
        Id = "com.local.nputools.notes.rename";
        Title = $"Rename: {entry.Title}";
        Name = "Rename";
        Icon = NotesVisuals.Note;
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
                    Subtitle = _entry.Title,
                    Icon = NotesVisuals.Note,
                },
            ];
        }

        string newTitle = text.Length > 120 ? text[..120].Trim() : text;
        return
        [
            new ListItem(new RenameNoteCommand(_store, _entry.FilePath, newTitle))
            {
                Title = $"Rename to: {newTitle}",
                Subtitle = $"Current: {_entry.Title}",
                Icon = NotesVisuals.Note,
                Tags = [NotesVisuals.MutedTag("press Enter")],
            },
        ];
    }
}
