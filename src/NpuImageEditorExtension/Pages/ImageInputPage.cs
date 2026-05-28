using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.ImageEditor.Services;
using Windows.ApplicationModel.DataTransfer;

namespace NpuTools.ImageEditor.Pages;

internal enum ImageOperation { RemoveBackground, SuperResolution, Ocr }

internal sealed partial class ImageInputPage : DynamicListPage
{
    private static readonly string[] ImageExtensions =
        [".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff", ".tif", ".webp", ".heic", ".heif"];

    private static readonly string PicturesPath =
        Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);

    private readonly ImageOperation _operation;
    private readonly int _scaleFactor;
    private readonly ImageEditorSettingsManager _settings;

    private int _initialized;
    private IListItem[] _browseItems = [];
    private IListItem?  _clipboardItem;
    private IListItem[] _items = [];

    public ImageInputPage(ImageOperation operation, int scaleFactor, ImageEditorSettingsManager settings)
    {
        _operation   = operation;
        _scaleFactor = scaleFactor;
        _settings    = settings;

        Id              = $"com.local.nputools.imageeditor.{operation.ToString().ToLowerInvariant()}.{scaleFactor}";
        Title           = OperationLabel(operation, scaleFactor);
        Name            = "Run";
        Icon            = OperationIcon(operation);
        PlaceholderText = "Search images or paste a full path…";
        IsLoading       = true;
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        if (_initialized == 0) return; // still loading

        _items = BuildItems(newSearch.Trim().Trim('"'));
        RaiseItemsChanged(_items.Length);
    }

    public override IListItem[] GetItems()
    {
        if (Interlocked.Exchange(ref _initialized, 1) == 0)
            _ = Task.Run(LoadBrowseItemsAsync);

        return _items;
    }

    private async Task LoadBrowseItemsAsync()
    {
        _browseItems   = ScanPictures();
        _clipboardItem = await TryBuildClipboardItemAsync();
        _items         = BuildItems(string.Empty);
        IsLoading      = false;
        RaiseItemsChanged(_items.Length);
    }

    private async Task<IListItem?> TryBuildClipboardItemAsync()
    {
        try
        {
            var content = Clipboard.GetContent();
            if (!content.Contains(StandardDataFormats.Bitmap))
                return null;

            string? path = await ImageEditorService.SaveClipboardImageAsync();
            if (path is null)
                return null;

            return new ListItem(new ImageResultPage(_operation, _scaleFactor, path, _settings))
            {
                Title    = "From Clipboard",
                Subtitle = "Use the image currently in your clipboard",
                Icon     = ImageEditorVisuals.Clipboard,
                Tags     = [ImageEditorVisuals.MutedTag("press Enter")],
            };
        }
        catch
        {
            return null;
        }
    }

    private IListItem[] BuildItems(string query)
    {
        if (query.Length == 0)
        {
            var defaults = new List<IListItem>();
            if (_clipboardItem is not null) defaults.Add(_clipboardItem);
            defaults.AddRange(_browseItems);
            return defaults.Count > 0 ? [.. defaults] : EmptyPicturesItem();
        }

        if (LooksLikePath(query))
            return BuildPathItems(query);

        var filtered = _browseItems
            .Where(item => item.Title.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        return filtered.Length > 0 ? filtered : NoMatchItems(query);
    }

    private IListItem[] BuildPathItems(string path)
    {
        if (!File.Exists(path))
        {
            return
            [
                new ListItem(new NoOpCommand())
                {
                    Title    = "File not found",
                    Subtitle = path,
                    Icon     = ImageEditorVisuals.Folder,
                },
            ];
        }

        return
        [
            new ListItem(new ImageResultPage(_operation, _scaleFactor, path, _settings))
            {
                Title    = $"{OperationLabel(_operation, _scaleFactor)} — {Path.GetFileName(path)}",
                Subtitle = path,
                Icon     = OperationIcon(_operation),
                Tags     = [ImageEditorVisuals.MutedTag("press Enter")],
            },
        ];
    }

    private IListItem[] ScanPictures()
    {
        if (!Directory.Exists(PicturesPath))
            return [];

        try
        {
            return Directory
                .EnumerateFiles(PicturesPath, "*", SearchOption.AllDirectories)
                .Where(f => ImageExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                .Select(f => new FileInfo(f))
                .OrderByDescending(fi => fi.LastWriteTime)
                .Take(100)
                .Select(fi => MakeBrowseItem(fi))
                .ToArray();
        }
        catch
        {
            return [];
        }
    }

    private ListItem MakeBrowseItem(FileInfo info)
    {
        string rel = Path.GetRelativePath(PicturesPath, info.FullName);
        string dir = Path.GetDirectoryName(rel) ?? string.Empty;
        string ago = FormatAge(info.LastWriteTime);
        string sub = string.IsNullOrEmpty(dir) || dir == "."
            ? ago
            : $"{dir} · {ago}";

        return new ListItem(new ImageResultPage(_operation, _scaleFactor, info.FullName, _settings))
        {
            Title    = info.Name,
            Subtitle = sub,
            Icon     = OperationIcon(_operation),
            Tags     = [ImageEditorVisuals.MutedTag("press Enter")],
        };
    }

    private static IListItem[] EmptyPicturesItem() =>
    [
        new ListItem(new NoOpCommand())
        {
            Title    = "No images found in Pictures",
            Subtitle = "Type a full file path to proceed",
            Icon     = ImageEditorVisuals.Folder,
        },
    ];

    private static IListItem[] NoMatchItems(string query) =>
    [
        new ListItem(new NoOpCommand())
        {
            Title    = $"No images matching \"{query}\"",
            Subtitle = "Type a full path with a backslash to open a specific file",
            Icon     = ImageEditorVisuals.Folder,
        },
    ];

    // Looks like an absolute path: drive letter, UNC, or contains backslash
    private static bool LooksLikePath(string s) =>
        (s.Length >= 3 && char.IsLetter(s[0]) && s[1] == ':' && (s[2] == '\\' || s[2] == '/'))
        || s.StartsWith(@"\\", StringComparison.Ordinal)
        || s.Contains('\\');

    private static string FormatAge(DateTime dt)
    {
        var diff = DateTime.Now - dt;
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalHours   < 24) return $"{(int)diff.TotalHours}h ago";
        if (diff.TotalDays    < 30) return $"{(int)diff.TotalDays}d ago";
        return dt.ToString("MMM d, yyyy", System.Globalization.CultureInfo.CurrentCulture);
    }

    internal static string OperationLabel(ImageOperation op, int scaleFactor = 2) => op switch
    {
        ImageOperation.RemoveBackground => "Remove Background",
        ImageOperation.SuperResolution  => $"Super Resolution ({scaleFactor}×)",
        ImageOperation.Ocr              => "Extract Text (OCR)",
        _                               => op.ToString(),
    };

    internal static IconInfo OperationIcon(ImageOperation op) => op switch
    {
        ImageOperation.RemoveBackground => ImageEditorVisuals.Eraser,
        ImageOperation.SuperResolution  => ImageEditorVisuals.Scale,
        ImageOperation.Ocr              => ImageEditorVisuals.Ocr,
        _                               => ImageEditorVisuals.Camera,
    };
}
