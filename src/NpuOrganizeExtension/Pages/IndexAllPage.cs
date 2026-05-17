using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.Organize.Services;

namespace NpuTools.Organize.Pages;

// Page-as-command: navigating here starts OCR + description indexing for all
// screenshots not yet in the index. First GetItems() triggers the async work.
internal sealed partial class IndexAllPage : ListPage
{
    private static readonly string[] SupportedExtensions = [".png", ".jpg", ".jpeg", ".webp"];

    private readonly ScreenshotScannerService _scanner;
    private readonly ScreenshotIndexService _indexService;
    private int _started;
    private int _indexed = -1;
    private int _skipped;
    private int _failed;

    public IndexAllPage(ScreenshotScannerService scanner, ScreenshotIndexService indexService)
    {
        _scanner      = scanner;
        _indexService = indexService;
        Id        = "com.local.nputools.organize.index-all";
        Title     = "Index All Screenshots";
        Name      = "Index All";
        Icon      = OrganizeVisuals.Watcher;
        IsLoading = true;
    }

    public override IListItem[] GetItems()
    {
        if (Interlocked.Exchange(ref _started, 1) == 0)
            _ = Task.Run(RunIndexAsync);

        if (_indexed < 0)
        {
            return
            [
                new ListItem(new NoOpCommand())
                {
                    Title    = "Indexing screenshots…",
                    Subtitle = "Running OCR and AI description — this may take a while",
                    Icon     = OrganizeVisuals.Watcher,
                },
            ];
        }

        return
        [
            new ListItem(new NoOpCommand())
            {
                Title    = $"Indexed {_indexed} screenshot{(_indexed == 1 ? "" : "s")}",
                Subtitle = $"Skipped {_skipped} already indexed · {_failed} failed",
                Icon     = _failed > 0 ? OrganizeVisuals.Warning : OrganizeVisuals.Check,
            },
        ];
    }

    private async Task RunIndexAsync()
    {
        int indexed = 0;
        int skipped = 0;
        int failed  = 0;

        string folder = _scanner.ScreenshotsFolder;
        if (!Directory.Exists(folder))
        {
            _indexed = 0; _skipped = 0; _failed = 0;
            IsLoading = false;
            RaiseItemsChanged();
            return;
        }

        var files = new List<string>();
        foreach (string path in Directory.EnumerateFiles(folder))
        {
            if (Array.Exists(SupportedExtensions, e => string.Equals(e, Path.GetExtension(path), StringComparison.OrdinalIgnoreCase)))
                files.Add(path);
        }

        foreach (string path in files)
        {
            if (_indexService.IsIndexed(path)) { skipped++; continue; }

            try
            {
                var (_, description, ocrText) = await AiNamingService.BuildProposedPathWithDataAsync(path);
                _indexService.Upsert(path, description, ocrText);
                indexed++;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Index failed for '{path}': {ex.GetType().Name}: {ex.Message}");
                failed++;
            }
        }

        _indexed = indexed;
        _skipped = skipped;
        _failed  = failed;
        IsLoading = false;
        RaiseItemsChanged();
    }
}
