using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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
        string image = BuildImageMarkdown(entry.FilePath);
        string description = string.IsNullOrWhiteSpace(entry.Description)
            ? string.Empty
            : "Description:\n" + entry.Description.Trim();
        string ocr = string.IsNullOrWhiteSpace(entry.OcrText)
            ? string.Empty
            : "OCR:\n```text\n" + entry.OcrText.Trim() + "\n```";

        return new Details
        {
            Title = name,
            Body = string.Join("\n\n", new[] { image, description, ocr }.Where(static part => !string.IsNullOrWhiteSpace(part))),
            Size = ContentSize.Small,
            Metadata =
            [
                new DetailsElement { Key = "Indexed", Data = new DetailsLink(entry.IndexedAt.ToString("g", CultureInfo.CurrentCulture)) },
                new DetailsElement { Key = "File", Data = new DetailsLink(entry.FilePath) },
            ],
        };
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
