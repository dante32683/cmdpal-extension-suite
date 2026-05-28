using System;
using System.Linq;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.Notes.Commands;
using NpuTools.Notes.Models;
using NpuTools.Notes.Services;

namespace NpuTools.Notes.Pages;

internal sealed partial class MoveNotePage : ListPage
{
    private readonly NotesStore _store;
    private readonly NoteEntry _entry;

    public MoveNotePage(NotesStore store, NoteEntry entry)
    {
        _store = store;
        _entry = entry;
        Id = "com.local.nputools.notes.move";
        Title = $"Move: {entry.Title}";
        Name = "Move";
        Icon = NotesVisuals.Folder;
    }

    public override IListItem[] GetItems()
    {
        return NotesStore.KnownCategories
            .Select(cat => BuildCategoryItem(cat))
            .ToArray();
    }

    private ListItem BuildCategoryItem(string category)
    {
        bool isCurrent = string.Equals(category, _entry.Category, StringComparison.OrdinalIgnoreCase);
        var item = new ListItem(new MoveNoteCommand(_store, _entry.FilePath, category))
        {
            Title = category,
            Icon = NotesVisuals.Folder,
        };

        if (isCurrent)
            item.Tags = [NotesVisuals.StatusTag("current")];

        return item;
    }
}
