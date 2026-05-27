using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.Obsidian.Commands;
using NpuTools.Obsidian.Models;
using NpuTools.Obsidian.Services; // store param in ctor keeps callers consistent

namespace NpuTools.Obsidian.Pages;

internal sealed partial class QuickAppendPage : DynamicListPage
{
    private readonly ObsidianNote _note;
    private IListItem[] _items;

    public QuickAppendPage(ObsidianVaultStore store, ObsidianNote note)
    {
        _ = store;
        _note = note;
        Id = "com.local.nputools.obsidian.append";
        Title = $"Append: {note.Title}";
        Name = "Quick Append";
        Icon = ObsidianVisuals.Append;
        PlaceholderText = "Type text to append...";
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
                new ListItem(new NoOpCommand())
                {
                    Title = "Type text to append to this note",
                    Subtitle = _note.Title,
                    Icon = ObsidianVisuals.Note,
                },
            ];
        }

        return
        [
            new ListItem(new AppendToNoteCommand(_note, text))
            {
                Title = $"Append to: {_note.Title}",
                Subtitle = text.Length > 100 ? text[..100] + "..." : text,
                Icon = ObsidianVisuals.Append,
                Tags = [ObsidianVisuals.MutedTag("press Enter")],
            },
        ];
    }
}
