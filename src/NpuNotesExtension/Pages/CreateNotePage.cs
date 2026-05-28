using System;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.Notes.Commands;
using NpuTools.Notes.Services;

namespace NpuTools.Notes.Pages;

internal sealed partial class CreateNotePage : DynamicListPage
{
    private readonly NotesStore _store;
    private readonly NotesSettingsStore _settings;
    private readonly NotesAiService _ai;
    private IListItem[] _items;

    public CreateNotePage(NotesStore store, NotesSettingsStore settings, NotesAiService ai)
    {
        _store = store;
        _settings = settings;
        _ai = ai;
        Id = "com.local.nputools.notes.create";
        Title = "Create Note";
        Name = "Create";
        Icon = NotesVisuals.Add;
        PlaceholderText = "Type a note, or paste text here...";
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
        if (text.Length == 0)
        {
            return
            [
                new ListItem(new CreateNoteCommand(_store, _settings, _ai, string.Empty))
                {
                    Title = "Create Blank Note",
                    Subtitle = $"Default category: {_settings.Current.DefaultCategory}",
                    Icon = NotesVisuals.Add,
                },
                new ListItem(new CreateNoteFromClipboardCommand(_store, _settings))
                {
                    Title = "Create Note From Clipboard",
                    Subtitle = "Use current clipboard text",
                    Icon = NotesVisuals.Add,
                },
                new ListItem(new OpenNotesFolderCommand(_settings))
                {
                    Title = "Open Notes Folder",
                    Subtitle = _settings.Current.NotesRoot,
                    Icon = NotesVisuals.Folder,
                },
            ];
        }

        var parsed = NotesStore.ParseRawNote(text);
        return
        [
            new ListItem(new CreateNoteCommand(_store, _settings, _ai, text))
            {
                Title = $"Create: {parsed.Title}",
                Subtitle = Preview(text),
                Icon = NotesVisuals.Add,
                Tags = [NotesVisuals.MutedTag("press Enter")],
            },
        ];
    }

    private static string Preview(string text)
    {
        string compact = string.Join(' ', text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return compact.Length > 120 ? compact[..120] + "..." : compact;
    }
}
