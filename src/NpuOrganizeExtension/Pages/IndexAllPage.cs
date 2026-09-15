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
    private List<string> _filesToReconcile = [];
    private int _indexed = -1;
    private int _renamed;
    private int _failed = -1;
    private int _current;
    private string _currentFile = string.Empty;
    private int _started; // Interlocked flag: 0 = not started, 1 = started
    private bool _scanned;
    private bool _deferredOnBattery;

    public IndexAllPage(ScreenshotScannerService scanner, ScreenshotIndexService indexService)
    {
        _scanner = scanner;
        _indexService = indexService;
        Id = "com.local.nputools.organize.index-all";
        Title = "Index Screenshots";
        Name = "Index Screenshots";
        Icon = OrganizeVisuals.Search;
        IsLoading = true;
    }

    private void ScanForFilesToReconcile()
    {
        try
        {
            string folder = _scanner.ScreenshotsFolder;
            if (Directory.Exists(folder))
            {
                _filesToReconcile = Directory.EnumerateFiles(folder)
                    .Where(path =>
                    {
                        string ext = Path.GetExtension(path).ToLowerInvariant();
                        if (!Array.Exists(SupportedExtensions, e => e == ext)) return false;
                        return !SlugService.IsAlreadyOrganized(Path.GetFileName(path))
                            || !_indexService.IsIndexed(path);
                    })
                    .OrderBy(path => File.GetCreationTime(path))
                    .ToList();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to scan for screenshots to reconcile: {ex.GetType().Name}: {ex.Message}");
        }
    }

    public override IListItem[] GetItems()
    {
        if (Interlocked.Exchange(ref _started, 1) == 0)
        {
            _ = Task.Run(async () =>
            {
                if (OrganizePowerPolicy.ShouldDeferNpuWork())
                {
                    _deferredOnBattery = true;
                    IsLoading = false;
                    RaiseItemsChanged();
                    return;
                }

                ScanForFilesToReconcile();
                _scanned = true;
                if (_filesToReconcile.Count == 0)
                {
                    IsLoading = false;
                    RaiseItemsChanged();
                    return;
                }
                await RunReconcileAsync();
            });
        }

        if (_deferredOnBattery)
        {
            return
            [
                new ListItem(new IndexAllPage(_scanner, _indexService))
                {
                    Title    = "Paused on battery",
                    Subtitle = "Connect AC power, then press Enter to try again",
                    Icon     = OrganizeVisuals.Warning,
                },
            ];
        }

        if (IsLoading)
        {
            string progress = _current > 0 ? $" ({_current} / {_filesToReconcile.Count})" : string.Empty;
            string details = !_scanned
                ? "Scanning screenshots folder..."
                : (string.IsNullOrEmpty(_currentFile) ? "Starting AI description and OCR…" : $"Organizing: {_currentFile}");
            return
            [
                new ListItem(new NoOpCommand())
                {
                    Title    = $"Organizing and indexing screenshots{progress}…",
                    Subtitle = details,
                    Icon     = OrganizeVisuals.Search,
                },
            ];
        }

        if (_filesToReconcile.Count == 0)
        {
            return
            [
                new ListItem(new IndexAllPage(_scanner, _indexService))
                {
                    Title    = "Screenshots are organized and indexed",
                    Subtitle = "Every supported file is reconciled; press Enter to scan again",
                    Icon     = OrganizeVisuals.Check,
                },
            ];
        }

        return
        [
            new ListItem(new IndexAllPage(_scanner, _indexService))
            {
                Title    = $"Organized {_renamed} and indexed {_indexed} screenshot{(_indexed == 1 ? "" : "s")}",
                Subtitle = _failed > 0 ? $"{_failed} failed — press Enter to retry" : "Names and search index are up to date; press Enter to scan again",
                Icon     = _failed > 0 ? OrganizeVisuals.Warning : OrganizeVisuals.Check,
            },
        ];
    }

    private async Task RunReconcileAsync()
    {
        int indexed = 0;
        int renamed = 0;
        int failed = 0;

        for (int i = 0; i < _filesToReconcile.Count; i++)
        {
            if (OrganizePowerPolicy.ShouldDeferNpuWork())
            {
                _deferredOnBattery = true;
                break;
            }

            string path = _filesToReconcile[i];
            _current = i + 1;
            _currentFile = Path.GetFileName(path);
            RaiseItemsChanged();

            try
            {
                bool needsRename = !SlugService.IsAlreadyOrganized(Path.GetFileName(path));
                var (proposedPath, description, ocrText) = await AiNamingService.BuildProposedPathWithDataAsync(path);

                if (needsRename && !string.Equals(path, proposedPath, StringComparison.OrdinalIgnoreCase))
                {
                    File.Move(path, proposedPath, overwrite: false);
                    _indexService.RelocateAndUpsert(path, proposedPath, description, ocrText);
                    renamed++;
                }
                else
                {
                    _indexService.Upsert(path, description, ocrText);
                }
                indexed++;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to index screenshot '{path}': {ex.GetType().Name}: {ex.Message}");
                failed++;
            }
        }

        _indexed = indexed;
        _renamed = renamed;
        _failed = failed;
        IsLoading = false;
        RaiseItemsChanged();
    }
}
