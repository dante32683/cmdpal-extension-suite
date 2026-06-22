using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.Clipboard.Data;
using NpuTools.Clipboard.Services;

namespace NpuTools.Clipboard.Pages;

internal sealed partial class ClipboardCategoriesPage : ListPage
{
    private readonly ClipboardStore _store;
    private readonly ClipboardSettingsStore _settings;
    private readonly ClipboardContentService _content;

    public ClipboardCategoriesPage(ClipboardStore store, ClipboardSettingsStore settings, ClipboardContentService content)
    {
        _store = store;
        _settings = settings;
        _content = content;

        Id = "com.local.nputools.clipboard.categories";
        Title = "Clipboard Categories";
        Name = "Browse Categories";
        Icon = ClipboardVisuals.Clipboard;
    }

    public override IListItem[] GetItems()
    {
        return
        [
            new ListItem(new ClipboardHistoryPage(_store, _settings, _content, ClipboardEntryKind.Text))
            {
                Title = "Text",
                Subtitle = "Browse and paste saved text entries",
                Icon = ClipboardVisuals.Text
            },
            new ListItem(new ClipboardHistoryPage(_store, _settings, _content, ClipboardEntryKind.Image))
            {
                Title = "Images",
                Subtitle = "Browse and paste saved image entries",
                Icon = ClipboardVisuals.Image
            },
            new ListItem(new ClipboardHistoryPage(_store, _settings, _content, ClipboardEntryKind.Files))
            {
                Title = "Files",
                Subtitle = "Browse saved file path entries",
                Icon = ClipboardVisuals.File
            },
            new ListItem(new ClipboardHistoryPage(_store, _settings, _content, ClipboardEntryKind.Link))
            {
                Title = "Links",
                Subtitle = "Browse saved URL entries",
                Icon = ClipboardVisuals.Link
            },
            new ListItem(new ClipboardHistoryPage(_store, _settings, _content, ClipboardEntryKind.Email))
            {
                Title = "Emails",
                Subtitle = "Browse saved email address entries",
                Icon = ClipboardVisuals.Mail
            },
            new ListItem(new ClipboardHistoryPage(_store, _settings, _content, ClipboardEntryKind.Color))
            {
                Title = "Colors",
                Subtitle = "Browse saved color code entries",
                Icon = ClipboardVisuals.Color
            }
        ];
    }
}
