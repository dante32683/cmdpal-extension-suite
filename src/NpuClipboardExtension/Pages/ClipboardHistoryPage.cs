using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.Clipboard.Commands;
using NpuTools.Clipboard.Services;
using NpuTools.Clipboard.Data;

namespace NpuTools.Clipboard.Pages;

internal sealed partial class ClipboardHistoryPage : DynamicListPage
{
    private readonly ClipboardStore _store;
    private readonly ClipboardSettingsStore _settings;
    private readonly ClipboardContentService _content;
    private readonly ClipboardEntryKind? _filter;
    private readonly Dictionary<string, IconInfo> _imageIcons = new(StringComparer.OrdinalIgnoreCase);
    private IListItem[] _items;

    public ClipboardHistoryPage(ClipboardStore store, ClipboardSettingsStore settings, ClipboardContentService content, ClipboardEntryKind? filter = null)
    {
        _store = store;
        _settings = settings;
        _content = content;
        _filter = filter;
        Id = filter is null ? "com.local.nputools.clipboard.history" : $"com.local.nputools.clipboard.history.{filter.Value.ToString().ToLowerInvariant()}";
        Title = filter is null ? "Clipboard History" : $"{filter} Clipboard History";
        Name = "Open";
        Icon = ClipboardVisuals.Clipboard;
        PlaceholderText = "Search clipboard history...";
        ShowDetails = _settings.Current.PreviewMode == ClipboardPreviewMode.Always;
        _items = BuildItems(string.Empty);
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        _items = BuildItems(newSearch.Trim());
        RaiseItemsChanged(_items.Length);
    }

    public override IListItem[] GetItems()
    {
        _settings.Reload();
        ShowDetails = _settings.Current.PreviewMode == ClipboardPreviewMode.Always;
        _items = BuildItems(SearchText?.Trim() ?? string.Empty);
        return _items;
    }

    private IListItem[] BuildItems(string query)
    {
        var entries = _store.Search(_filter, query);
        if (entries.Count == 0)
        {
            return
            [
                new ListItem(new NoOpCommand())
                {
                    Title = string.IsNullOrWhiteSpace(query) ? "No clipboard entries yet" : $"No matches for \"{query}\"",
                    Subtitle = "Start the recorder from Clipboard Settings, then copy text, images, or files.",
                    Icon = ClipboardVisuals.Search,
                },
            ];
        }

        var items = new List<IListItem>();
        string? lastGroup = null;
        foreach (var entry in entries)
        {
            if (entry.GroupId != lastGroup)
            {
                lastGroup = entry.GroupId;
                items.Add(new ListItem(new NoOpCommand())
                {
                    Title = FormatGroupTitle(entry.CreatedAt),
                    Subtitle = "Grouped by clipboard activity burst",
                    Tags = [ClipboardVisuals.MutedTag("group")],
                });
            }

            items.Add(BuildEntryItem(entry));
        }
        return [.. items];
    }

    private ListItem BuildEntryItem(ClipboardEntry entry)
    {
        var settings = _settings.Current;
        InvokableCommand primary = settings.PrimaryAction == ClipboardPrimaryAction.Copy
            ? new CopyEntryCommand(_store, _settings, _content, entry.Id)
            : new PasteEntryCommand(_store, _settings, _content, entry.Id);

        var item = new ListItem(primary)
        {
            Title = DisplayTitle(entry),
            Subtitle = BuildSubtitle(entry),
            Icon = IconFor(entry),
            Tags = BuildTags(entry),
            MoreCommands =
            [
                new CommandContextItem(new CopyEntryCommand(_store, _settings, _content, entry.Id)) { Icon = ClipboardVisuals.Copy },
                new CommandContextItem(new PasteEntryCommand(_store, _settings, _content, entry.Id)) { Icon = ClipboardVisuals.Paste },
                new CommandContextItem(new PasteEntryCommand(_store, _settings, _content, entry.Id, plainTextOnly: true)) { Icon = ClipboardVisuals.Text },
                new CommandContextItem(new CopyEntryCommand(_store, _settings, _content, entry.Id, plainTextOnly: true)) { Icon = ClipboardVisuals.Text },
                new Separator(),
                new CommandContextItem(new RenameEntryPage(_store, entry.Id, entry.DisplayName)) { Icon = ClipboardVisuals.Rename },
                new CommandContextItem(new PinEntryCommand(_store, entry.Id, !entry.IsPinned)) { Icon = ClipboardVisuals.Pin },
                new Separator(),
                new CommandContextItem(new DeleteEntryCommand(_store, entry.Id)) { Icon = ClipboardVisuals.Delete, IsCritical = true },
            ],
        };

        var details = new Details
        {
            Title = DetailsTitle(entry),
            Body = BuildDetails(entry),
            Size = ContentSize.Small,
            Metadata = BuildDetailsMetadata(entry),
        };
        var heroImage = HeroImageFor(entry);
        if (heroImage is not null)
            details.HeroImage = heroImage;
        item.Details = details;

        return item;
    }

    private static string FormatGroupTitle(DateTimeOffset value)
    {
        var now = DateTimeOffset.Now;
        if (value.Date == now.Date)
            return "Today " + value.ToString("t", CultureInfo.CurrentCulture);
        if (value.Date == now.AddDays(-1).Date)
            return "Yesterday " + value.ToString("t", CultureInfo.CurrentCulture);
        return value.ToString("g", CultureInfo.CurrentCulture);
    }

    private static string BuildSubtitle(ClipboardEntry entry)
    {
        string source = string.IsNullOrWhiteSpace(entry.SourceApplication) ? "" : $" from {entry.SourceApplication}";
        string ocr = string.IsNullOrWhiteSpace(entry.OcrText) ? "" : " | OCR searchable";
        return $"{entry.Kind}{source} | {entry.CreatedAt:g}{ocr}";
    }

    private static string BuildDetails(ClipboardEntry entry)
    {
        if (entry.Kind == ClipboardEntryKind.Files)
            return string.Empty;
        if (!string.IsNullOrWhiteSpace(entry.ImagePath))
        {
            return BuildImageMarkdown(entry.ImagePath);
        }
        return "```text\n" + (entry.Text ?? entry.OcrText ?? entry.DisplayName) + "\n```";
    }

    private static IDetailsElement[] BuildDetailsMetadata(ClipboardEntry entry)
    {
        var metadata = new List<IDetailsElement>
        {
            new DetailsElement { Key = "Type", Data = new DetailsLink(entry.Kind.ToString()) },
        };

        if (entry.Kind == ClipboardEntryKind.Image)
        {
            AddTextMetadata(metadata, "OCR", entry.OcrText);
            AddTextMetadata(metadata, "Path", entry.ImagePath);
        }
        else if (entry.Kind == ClipboardEntryKind.Files)
        {
            AddPathMetadata(metadata, entry.FilePaths);
        }

        metadata.Add(new DetailsElement { Key = "Copied", Data = new DetailsLink(entry.CreatedAt.ToString("g", CultureInfo.CurrentCulture)) });
        metadata.Add(new DetailsElement { Key = "Source", Data = new DetailsLink(entry.SourceApplication ?? "unknown") });

        return [.. metadata];
    }

    private static void AddTextMetadata(List<IDetailsElement> metadata, string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        metadata.Add(new DetailsElement { Key = key, Data = new DetailsLink(value.Trim()) });
    }

    private static void AddPathMetadata(List<IDetailsElement> metadata, IReadOnlyList<string> paths)
    {
        foreach (string path in paths)
            AddTextMetadata(metadata, "Path", path);
    }

    private static string BuildImageMarkdown(string? imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            return string.Empty;

        return "![Clipboard image preview](<" + BuildFitImageUri(imagePath) + ">)";
    }

    private static string BuildFitImageUri(string imagePath)
    {
        var builder = new UriBuilder(new Uri(imagePath))
        {
            Query = "--x-cmdpal-fit=fit",
        };
        return builder.Uri.AbsoluteUri;
    }

    private static Tag[] BuildTags(ClipboardEntry entry)
    {
        var tags = new List<Tag> { ClipboardVisuals.MutedTag(entry.Kind.ToString().ToLowerInvariant()) };
        if (entry.IsPinned)
            tags.Add(ClipboardVisuals.StatusTag("pinned"));
        if (!string.IsNullOrWhiteSpace(entry.CustomName))
            tags.Add(ClipboardVisuals.MutedTag("renamed"));
        return [.. tags];
    }

    private static string DisplayTitle(ClipboardEntry entry)
    {
        if (entry.Kind == ClipboardEntryKind.Image && string.IsNullOrWhiteSpace(entry.CustomName))
            return entry.Title.StartsWith("Image", StringComparison.OrdinalIgnoreCase) ? entry.Title : "Image";

        return entry.DisplayName;
    }

    private static string DetailsTitle(ClipboardEntry entry) => entry.Kind switch
    {
        ClipboardEntryKind.Image => DisplayTitle(entry),
        ClipboardEntryKind.Files => "Files",
        ClipboardEntryKind.Link => "Link",
        ClipboardEntryKind.Email => "Email",
        ClipboardEntryKind.Color => "Color",
        _ => "Text",
    };

    private IconInfo? HeroImageFor(ClipboardEntry entry)
    {
        return entry.Kind == ClipboardEntryKind.Image ? ImageIcon(entry.ImagePath) : null;
    }

    private IconInfo IconFor(ClipboardEntry entry)
    {
        if (entry.Kind == ClipboardEntryKind.Image)
            return ImageIcon(entry.ImagePath) ?? ClipboardVisuals.Image;

        return IconFor(entry.Kind);
    }

    private IconInfo? ImageIcon(string? imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            return null;

        if (_imageIcons.TryGetValue(imagePath, out var icon))
            return icon;

        icon = new IconInfo(imagePath);
        _imageIcons[imagePath] = icon;
        return icon;
    }

    private static IconInfo IconFor(ClipboardEntryKind kind) => kind switch
    {
        ClipboardEntryKind.Image => ClipboardVisuals.Image,
        ClipboardEntryKind.Files => ClipboardVisuals.File,
        ClipboardEntryKind.Link => ClipboardVisuals.Link,
        ClipboardEntryKind.Email => ClipboardVisuals.Mail,
        ClipboardEntryKind.Color => ClipboardVisuals.Color,
        _ => ClipboardVisuals.Text,
    };
}
