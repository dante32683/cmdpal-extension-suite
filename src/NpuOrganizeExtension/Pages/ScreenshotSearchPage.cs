using System.IO;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.Organize.Models;
using NpuTools.Organize.Services;

namespace NpuTools.Organize.Pages;

internal sealed partial class ScreenshotSearchPage : DynamicListPage
{
    private readonly ScreenshotIndexService _indexService;
    private IListItem[] _items;

    public ScreenshotSearchPage(ScreenshotIndexService indexService)
    {
        _indexService   = indexService;
        Id              = "com.local.nputools.organize.search";
        Title           = "Search Screenshots";
        Name            = "Search";
        Icon            = OrganizeVisuals.Search;
        PlaceholderText = "Search by content or description…";
        _items          = BuildItems(string.Empty);
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        _items = BuildItems(newSearch.Trim());
        RaiseItemsChanged(_items.Length);
    }

    public override IListItem[] GetItems() => _items;

    private IListItem[] BuildItems(string query)
    {
        if (string.IsNullOrEmpty(query))
        {
            int count = _indexService.Count;
            return
            [
                new ListItem(new NoOpCommand())
                {
                    Title    = count == 0
                        ? "No screenshots indexed yet"
                        : $"{count} screenshot{(count == 1 ? "" : "s")} indexed",
                    Subtitle = count == 0
                        ? "Use 'Index All Screenshots' to build the search index"
                        : "Type to search by visible text or AI description",
                    Icon     = OrganizeVisuals.Search,
                },
            ];
        }

        var matches = _indexService.Search(query);

        if (matches.Count == 0)
        {
            return
            [
                new ListItem(new NoOpCommand())
                {
                    Title    = $"No matches for \"{query}\"",
                    Subtitle = "Try a different word — searches OCR text and AI description",
                    Icon     = OrganizeVisuals.Search,
                },
            ];
        }

        var items = new IListItem[matches.Count];
        for (int i = 0; i < matches.Count; i++)
        {
            ScreenshotIndexEntry entry = matches[i];
            string name    = Path.GetFileName(entry.FilePath);
            string preview = entry.Description.Length > 120
                ? entry.Description[..120] + "…"
                : entry.Description;
            items[i] = new ListItem(new OpenFileCommand(entry.FilePath))
            {
                Title    = name,
                Subtitle = preview,
                Icon     = OrganizeVisuals.File,
                Tags     = [OrganizeVisuals.MutedTag("open")],
            };
        }
        return items;
    }
}
