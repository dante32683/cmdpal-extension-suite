using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.ImageEditor.Commands;
using NpuTools.ImageEditor.Services;
using Windows.ApplicationModel.DataTransfer;

namespace NpuTools.ImageEditor.Pages;

internal enum ImageOperation { RemoveBackground, SuperResolution, Ocr }

// Multi-select image picker. Lists images most-recent-first; pressing Enter on a row toggles it
// in or out of the selection, and a pinned top row runs the operation over everything selected
// (one image or many). Toggling mutates the affected rows in place rather than rebuilding the
// list, so the cursor stays where it is — you can tick several files in a row without it jumping
// back to the top.
internal sealed partial class ImageInputPage : DynamicListPage
{
    private static readonly string[] ImageExtensions =
        [".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff", ".tif", ".webp", ".heic", ".heif"];

    private static readonly string PicturesPath =
        Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);

    private readonly ImageOperation _operation;
    private readonly int _scaleFactor;
    private readonly ImageEditorSettingsManager _settings;
    private readonly HashSet<string> _selected = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ListItem> _rowsByPath = new(StringComparer.OrdinalIgnoreCase);

    private int _initialized;
    private string _folder = PicturesPath;
    private string _query = string.Empty;
    private ListItem _header = null!;
    private ListItem[] _browseRows = [];
    private ListItem? _clipboardRow;

    public ImageInputPage(ImageOperation operation, int scaleFactor, ImageEditorSettingsManager settings)
    {
        _operation   = operation;
        _scaleFactor = scaleFactor;
        _settings    = settings;

        Id              = $"com.local.nputools.imageeditor.{operation.ToString().ToLowerInvariant()}.{scaleFactor}";
        Title           = OperationLabel(operation, scaleFactor);
        Name            = "Run";
        Icon            = OperationIcon(operation);
        PlaceholderText = "Tick images to process, or paste a file or folder path…";
        IsLoading       = true;

        _header = new ListItem(new NoOpCommand());
        RefreshHeader();
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        if (_initialized == 0) return;

        string q = newSearch.Trim().Trim('"');

        // A pasted directory switches which folder we scan (e.g. jump to Downloads).
        if (q.Length > 0 && Directory.Exists(q) && !string.Equals(q, _folder, StringComparison.OrdinalIgnoreCase))
        {
            _folder = q;
            _query  = string.Empty;
            IsLoading = true;
            _ = Task.Run(RescanAsync);
            return;
        }

        _query = q;
        RaiseItemsChanged();
    }

    public override IListItem[] GetItems()
    {
        if (Interlocked.Exchange(ref _initialized, 1) == 0)
            _ = Task.Run(RescanAsync);

        return BuildVisibleItems();
    }

    private async Task RescanAsync()
    {
        var infos = await ScanFolderAsync(_folder);

        _rowsByPath.Clear();
        _browseRows = infos.Select(MakeRow).ToArray();
        _clipboardRow = await TryBuildClipboardRowAsync();

        IsLoading = false;
        RaiseItemsChanged();
    }

    // Toggle selection WITHOUT rebuilding the list: we mutate the affected row and the header in
    // place. The Toolkit ListItem raises its own property-change notifications, so the UI redraws
    // the checkbox and counter while the highlighted row stays put.
    private void Toggle(string path)
    {
        if (!_selected.Remove(path))
            _selected.Add(path);

        if (_rowsByPath.TryGetValue(path, out var row))
            ApplyRowState(row, path);

        RefreshHeader();
    }

    private void RefreshHeader()
    {
        int n = _selected.Count;

        if (n == 0)
        {
            _header.Command  = new NoOpCommand();
            _header.Title    = "No images selected yet";
            _header.Subtitle = "Press Enter on an image below to add it";
            _header.Icon     = OperationIcon(_operation);
            _header.Tags     = [];
        }
        else if (n == 1)
        {
            // A single selection processes straight to the normal result page.
            string only = _selected.First();
            _header.Command  = new ImageResultPage(_operation, _scaleFactor, only, _settings);
            _header.Title    = $"{OperationLabel(_operation, _scaleFactor)} — 1 image";
            _header.Subtitle = Path.GetFileName(only);
            _header.Icon     = OperationIcon(_operation);
            _header.Tags     = [ImageEditorVisuals.MutedTag("press Enter")];
        }
        else
        {
            _header.Command  = new BatchResultPage(_operation, _scaleFactor, [.. _selected], _settings);
            _header.Title    = $"{OperationLabel(_operation, _scaleFactor)} — {n} images";
            _header.Subtitle = $"Outputs land in a new subfolder in {Path.GetFileName(_folder)}";
            _header.Icon     = ImageEditorVisuals.RunBatch;
            _header.Tags     = [ImageEditorVisuals.MutedTag("press Enter")];
        }
    }

    private IListItem[] BuildVisibleItems()
    {
        var rows = new List<ListItem>();

        // A pasted full file path surfaces that exact file, even if it lives outside the scanned
        // folder — pinned at the top so it is easy to tick.
        if (_query.Length > 0 && LooksLikeFilePath(_query) && File.Exists(_query))
        {
            string full = Path.GetFullPath(_query);
            rows.Add(_rowsByPath.TryGetValue(full, out var existing) ? existing : MakeRow(new FileInfo(full)));
        }

        if (_clipboardRow is not null) rows.Add(_clipboardRow);

        // Browse rows filter by name; the clipboard and pasted-path rows above always show.
        rows.AddRange(_browseRows.Where(r =>
            _query.Length == 0 || r.Title.Contains(_query, StringComparison.OrdinalIgnoreCase)));

        if (rows.Count == 0)
            return [_header, EmptyRow()];

        var items = new List<IListItem>(rows.Count + 1) { _header };
        items.AddRange(rows);
        return [.. items];
    }

    private ListItem MakeRow(FileInfo info)
    {
        var row = new ListItem(new NoOpCommand()) { Title = info.Name, Subtitle = FormatAge(info.LastWriteTime) };
        _rowsByPath[info.FullName] = row;
        ApplyRowState(row, info.FullName);
        return row;
    }

    private void ApplyRowState(ListItem row, string path)
    {
        bool selected = _selected.Contains(path);
        row.Command = new ToggleBatchSelectionCommand(path, selected, Toggle);
        row.Icon    = selected ? ImageEditorVisuals.Selected : ImageEditorVisuals.Unselected;
        row.Tags    = selected
            ? [ImageEditorVisuals.MutedTag("selected")]
            : [ImageEditorVisuals.MutedTag("press Enter to add")];
    }

    private async Task<ListItem?> TryBuildClipboardRowAsync()
    {
        try
        {
            var content = Clipboard.GetContent();
            if (!content.Contains(StandardDataFormats.Bitmap))
                return null;

            string? savedPath = await ImageEditorService.SaveClipboardImageAsync();
            if (savedPath is null)
                return null;

            var row = new ListItem(new NoOpCommand())
            {
                Title    = "From Clipboard",
                Subtitle = "Use the image currently in your clipboard",
            };
            _rowsByPath[savedPath] = row;
            ApplyRowState(row, savedPath);
            return row;
        }
        catch
        {
            return null;
        }
    }

    private ListItem EmptyRow() =>
        new(new NoOpCommand())
        {
            Title    = $"No images found in {Path.GetFileName(_folder)}",
            Subtitle = "Paste a folder path to scan a different location",
            Icon     = ImageEditorVisuals.Folder,
        };

    private static async Task<FileInfo[]> ScanFolderAsync(string folder)
    {
        return await Task.Run(() =>
        {
            if (!Directory.Exists(folder))
                return Array.Empty<FileInfo>();

            // Pictures is scanned recursively (deep library); a pasted folder is scanned shallow,
            // since "the things I just dropped here" live at the top level.
            var depth = string.Equals(folder, PicturesPath, StringComparison.OrdinalIgnoreCase)
                ? SearchOption.AllDirectories
                : SearchOption.TopDirectoryOnly;

            try
            {
                return Directory
                    .EnumerateFiles(folder, "*", depth)
                    .Where(f => ImageExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(fi => fi.LastWriteTime)
                    .Take(200)
                    .ToArray();
            }
            catch
            {
                return Array.Empty<FileInfo>();
            }
        });
    }

    private static bool LooksLikeFilePath(string s) =>
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
