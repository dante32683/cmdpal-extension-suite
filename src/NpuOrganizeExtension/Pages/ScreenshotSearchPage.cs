using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.Organize.Models;
using NpuTools.Organize.Services;

namespace NpuTools.Organize.Pages;

internal sealed partial class ScreenshotSearchPage : DynamicListPage
{
    private readonly ScreenshotIndexService _indexService;
    private readonly Dictionary<string, IconInfo> _imageIcons = new(StringComparer.OrdinalIgnoreCase);
    private IListItem[] _items;

    public ScreenshotSearchPage(ScreenshotIndexService indexService)
    {
        _indexService   = indexService;
        Id              = "com.local.nputools.organize.search";
        Title           = "Search Screenshots";
        Name            = "Search";
        Icon            = OrganizeVisuals.Search;
        PlaceholderText = "Search by content or description…";
        ShowDetails     = true;
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
            if (count == 0)
            {
                return
                [
                    new ListItem(new NoOpCommand())
                    {
                        Title    = "No screenshots indexed yet",
                        Subtitle = "Use 'Index All Screenshots' to build the search index",
                        Icon     = OrganizeVisuals.Search,
                    },
                ];
            }

            var recent = _indexService.Recent();
            return BuildResultItems(recent, $"{count} screenshot{(count == 1 ? "" : "s")} indexed — showing {recent.Count} most recent");
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

        return BuildResultItems(matches);
    }

    private IListItem[] BuildResultItems(IReadOnlyList<ScreenshotIndexEntry> entries, string? headerSubtitle = null)
    {
        int offset = headerSubtitle is null ? 0 : 1;
        var items = new IListItem[entries.Count + offset];

        if (headerSubtitle is not null)
        {
            items[0] = new ListItem(new NoOpCommand())
            {
                Title    = "Recent screenshots",
                Subtitle = headerSubtitle,
                Icon     = OrganizeVisuals.Search,
            };
        }

        for (int i = 0; i < entries.Count; i++)
        {
            ScreenshotIndexEntry entry = entries[i];
            string name    = Path.GetFileName(entry.FilePath);
            string preview = entry.Description.Length > 120
                ? entry.Description[..120] + "…"
                : entry.Description;
            items[i + offset] = new ListItem(new OpenFileCommand(entry.FilePath))
            {
                Title    = name,
                Subtitle = BuildSubtitle(entry, preview),
                Icon     = ImageIcon(entry.FilePath) ?? OrganizeVisuals.File,
                Details  = BuildDetails(entry, name),
                Tags     = [OrganizeVisuals.MutedTag("open")],
            };
        }
        return items;
    }

    private static string BuildSubtitle(ScreenshotIndexEntry entry, string preview)
    {
        if (string.IsNullOrWhiteSpace(entry.OcrText))
            return preview;

        return string.IsNullOrWhiteSpace(preview)
            ? "OCR searchable"
            : $"{preview} | OCR searchable";
    }

    private static Details BuildDetails(ScreenshotIndexEntry entry, string name)
    {
        return new Details
        {
            Title = name,
            Body = BuildImageMarkdown(entry.FilePath),
            Size = ContentSize.Small,
            Metadata = BuildDetailsMetadata(entry),
        };
    }

    private static IDetailsElement[] BuildDetailsMetadata(ScreenshotIndexEntry entry)
    {
        var metadata = new List<IDetailsElement>
        {
            new DetailsElement { Key = "Type", Data = new DetailsLink("Screenshot") },
        };

        AddTextMetadata(metadata, "Description", entry.Description);
        AddTextMetadata(metadata, "OCR", entry.OcrText);

        metadata.Add(new DetailsElement { Key = "Indexed", Data = new DetailsLink(entry.IndexedAt.ToString("g", CultureInfo.CurrentCulture)) });
        metadata.Add(new DetailsElement { Key = "Path", Data = new DetailsLink(entry.FilePath) });

        return [.. metadata];
    }

    private static void AddTextMetadata(List<IDetailsElement> metadata, string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        metadata.Add(new DetailsElement { Key = key, Data = new DetailsLink(value.Trim()) });
    }

    private static string BuildImageMarkdown(string imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            return string.Empty;

        return "![Screenshot preview](<" + BuildFitImageUri(imagePath) + ">)";
    }

    private static string BuildFitImageUri(string imagePath)
    {
        var builder = new UriBuilder(new Uri(imagePath))
        {
            Query = "--x-cmdpal-fit=fit",
        };
        return builder.Uri.AbsoluteUri;
    }

    private IconInfo? ImageIcon(string imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            return null;

        if (_imageIcons.TryGetValue(imagePath, out var icon))
            return icon;

        icon = new IconInfo(imagePath);
        _imageIcons[imagePath] = icon;
        return icon;
    }
}
