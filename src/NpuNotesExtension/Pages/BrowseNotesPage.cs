using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.Notes.Services;

namespace NpuTools.Notes.Pages;

internal sealed partial class BrowseNotesPage : DynamicListPage
{
    private readonly NotesStore _store;
    private readonly NotesSettingsStore _settings;
    private readonly NotesAiService _ai;
    private readonly string? _category;
    private IListItem[] _items;

    public BrowseNotesPage(NotesStore store, NotesSettingsStore settings, NotesAiService ai, string? category = null)
    {
        _store = store;
        _settings = settings;
        _ai = ai;
        _category = category;
        Id = category is null ? "com.local.nputools.notes.browse" : $"com.local.nputools.notes.browse.{category}";
        Title = category is null ? "Browse Notes" : $"{TitleCase(category)} Notes";
        Name = "Browse";
        Icon = NotesVisuals.Browse;
        PlaceholderText = "Filter notes...";
        ShowDetails = category is not null;
        _items = BuildItems(string.Empty);
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        _items = BuildItems(newSearch.Trim());
        RaiseItemsChanged(_items.Length);
    }

    public override IListItem[] GetItems()
    {
        _items = BuildItems(SearchText?.Trim() ?? string.Empty);
        return _items;
    }

    private IListItem[] BuildItems(string query)
    {
        if (_category is null && string.IsNullOrWhiteSpace(query))
            return BuildCategoryItems();

        var notes = _category is null || string.Equals(_category, "all", StringComparison.OrdinalIgnoreCase)
            ? _store.GetAll()
            : _store.GetByCategory(_category);
        if (!string.IsNullOrWhiteSpace(query))
        {
            notes = notes
                .Where(n => n.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                            n.Body.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                            n.Category.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (notes.Count == 0)
        {
            return
            [
                new ListItem(new CreateNotePage(_store, _settings))
                {
                    Title = "No notes found",
                    Subtitle = _category is null ? "Create a note to start" : $"No notes in {_category}",
                    Icon = NotesVisuals.Note,
                },
            ];
        }

        var items = new IListItem[notes.Count];
        for (int i = 0; i < notes.Count; i++)
            items[i] = NoteItemFactory.Build(_store, _ai, notes[i]);
        return items;
    }

    private IListItem[] BuildCategoryItems()
    {
        var notes = _store.GetAll();
        var counts = notes.GroupBy(n => n.Category, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);
        var items = new List<IListItem>
        {
            new ListItem(new BrowseNotesPage(_store, _settings, _ai, "all"))
            {
                Title = "All Notes",
                Subtitle = $"{notes.Count} notes",
                Icon = NotesVisuals.Note,
            },
        };

        foreach (string category in NotesStore.KnownCategories)
        {
            int count = counts.TryGetValue(category, out int value) ? value : 0;
            items.Add(new ListItem(new BrowseNotesPage(_store, _settings, _ai, category))
            {
                Title = TitleCase(category),
                Subtitle = $"{count} notes",
                Icon = NotesVisuals.Folder,
                Tags = count > 0 ? [NotesVisuals.MutedTag("category")] : [],
            });
        }

        return [.. items];
    }

    private static string TitleCase(string value) => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(value);
}
