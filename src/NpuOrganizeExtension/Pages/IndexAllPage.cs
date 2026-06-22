using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.Organize.Services;

namespace NpuTools.Organize.Pages;

internal sealed partial class IndexAllPage : ListPage
{
    private static readonly string[] SupportedExtensions = [".png", ".jpg", ".jpeg", ".webp"];
    private readonly ScreenshotScannerService _scanner;
    private readonly ScreenshotIndexService _indexService;
    private List<string> _unindexedFiles = [];
    private int _success = -1;
    private int _failed  = -1;
    private int _current;
    private string _currentFile = string.Empty;
    private int _started; // Interlocked flag: 0 = not started, 1 = started
    private bool _scanned;

    public IndexAllPage(ScreenshotScannerService scanner, ScreenshotIndexService indexService)
    {
        _scanner      = scanner;
        _indexService = indexService;
        Id    = "com.local.nputools.organize.index-all";
        Title = "Index Screenshots";
        Name  = "Index Screenshots";
        Icon  = OrganizeVisuals.Search;
        IsLoading = true;
    }

    private void ScanForUnindexedFiles()
    {
        try
        {
            string folder = _scanner.ScreenshotsFolder;
            if (Directory.Exists(folder))
            {
                _unindexedFiles = Directory.EnumerateFiles(folder)
                    .Where(path =>
                    {
                        string ext = Path.GetExtension(path).ToLowerInvariant();
                        return Array.Exists(SupportedExtensions, e => e == ext) && !_indexService.IsIndexed(path);
                    })
                    .ToList();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to scan for unindexed screenshots: {ex.GetType().Name}: {ex.Message}");
        }
    }

    public override IListItem[] GetItems()
    {
        if (Interlocked.Exchange(ref _started, 1) == 0)
        {
            _ = Task.Run(async () =>
            {
                ScanForUnindexedFiles();
                _scanned = true;
                if (_unindexedFiles.Count == 0)
                {
                    IsLoading = false;
                    RaiseItemsChanged();
                    return;
                }
                await RunIndexAsync();
            });
        }

        if (IsLoading)
        {
            string progress = _current > 0 ? $" ({_current} / {_unindexedFiles.Count})" : string.Empty;
            string details = !_scanned
                ? "Scanning screenshots folder..."
                : (string.IsNullOrEmpty(_currentFile) ? "Starting AI description and OCR…" : $"Indexing: {_currentFile}");
            return
            [
                new ListItem(new NoOpCommand())
                {
                    Title    = $"Indexing screenshots{progress}…",
                    Subtitle = details,
                    Icon     = OrganizeVisuals.Search,
                },
            ];
        }

        if (_unindexedFiles.Count == 0)
        {
            return
            [
                new ListItem(new NoOpCommand())
                {
                    Title    = "No screenshots to index",
                    Subtitle = "All screenshots in your folder are already in the search index.",
                    Icon     = OrganizeVisuals.Check,
                },
            ];
        }

        return
        [
            new ListItem(new NoOpCommand())
            {
                Title    = $"Indexed {_success} screenshot{(_success == 1 ? "" : "s")}",
                Subtitle = _failed > 0 ? $"{_failed} failed — check permissions or AI models" : "Search index is now fully up to date",
                Icon     = _failed > 0 ? OrganizeVisuals.Warning : OrganizeVisuals.Check,
            },
        ];
    }

    private async Task RunIndexAsync()
    {
        int success = 0;
        int failed  = 0;

        for (int i = 0; i < _unindexedFiles.Count; i++)
        {
            string path = _unindexedFiles[i];
            _current = i + 1;
            _currentFile = Path.GetFileName(path);
            RaiseItemsChanged();

            try
            {
                // Retrieve AI description and OCR content concurrently
                var (_, description, ocrText) = await AiNamingService.BuildProposedPathWithDataAsync(path);
                _indexService.Upsert(path, description, ocrText);
                success++;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to index screenshot '{path}': {ex.GetType().Name}: {ex.Message}");
                failed++;
            }
        }

        _success = success;
        _failed  = failed;
        IsLoading = false;
        RaiseItemsChanged();
    }
}
