using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.Obsidian.Services;

namespace NpuTools.Obsidian.Pages;

// Async page-as-command: navigating here starts a full vault index rebuild.
// First GetItems() triggers the async work via Interlocked lazy start.
internal sealed partial class IndexVaultPage : ListPage
{
    private readonly ObsidianVaultStore _store;
    private readonly ObsidianIndexStore _indexStore;
    private readonly ObsidianSettingsStore _settings;
    private int _started;
    private int _indexed = -1;
    private int _skipped;
    private int _failed;
    private int _total;
    private string? _current;
    private string? _errorMessage;

    public IndexVaultPage(ObsidianVaultStore store, ObsidianIndexStore indexStore, ObsidianSettingsStore settings)
    {
        _store = store;
        _indexStore = indexStore;
        _settings = settings;
        Id = "com.local.nputools.obsidian.index-vault";
        Title = "Index Vault";
        Name = "Index";
        Icon = ObsidianVisuals.Index;
        IsLoading = true;
    }

    public override IListItem[] GetItems()
    {
        if (Interlocked.Exchange(ref _started, 1) == 0)
            _ = Task.Run(RunAsync);

        if (_errorMessage is not null)
        {
            return
            [
                new ListItem(new NoOpCommand())
                {
                    Title = "Indexing failed",
                    Subtitle = _errorMessage,
                    Icon = ObsidianVisuals.Warning,
                    Tags = [ObsidianVisuals.WarningTag("error")],
                },
            ];
        }

        if (_indexed < 0)
        {
            string progress = _total > 0 ? $"{_indexed + 1}/{_total}" : "scanning…";
            string current = _current is not null ? $" · {_current}" : "";
            return
            [
                new ListItem(new NoOpCommand())
                {
                    Title = "Indexing vault…",
                    Subtitle = $"Processing notes {progress}{current}",
                    Icon = ObsidianVisuals.Index,
                },
            ];
        }

        return
        [
            new ListItem(new NoOpCommand())
            {
                Title = $"Indexed {_indexed + _skipped} note{(_indexed + _skipped == 1 ? "" : "s")}",
                Subtitle = $"{_indexed} updated · {_skipped} unchanged · {_failed} failed",
                Icon = _failed > 0 ? ObsidianVisuals.Warning : ObsidianVisuals.Check,
                Tags = [_failed > 0 ? ObsidianVisuals.WarningTag("some failed") : ObsidianVisuals.StatusTag("complete")],
            },
        ];
    }

    private async Task RunAsync()
    {
        if (!_store.IsVaultConfigured())
        {
            _errorMessage = "Vault path is not configured. Open settings and set the vault path first.";
            IsLoading = false;
            RaiseItemsChanged();
            return;
        }

        try
        {
            var progress = new Progress<(int done, int total, string? current)>(p =>
            {
                _total = p.total;
                _current = p.current;
                RaiseItemsChanged();
            });

            var (indexed, skipped, failed) = await _indexStore.RebuildAsync(
                _settings.Current.VaultPath,
                progress,
                default);

            _indexed = indexed;
            _skipped = skipped;
            _failed = failed;
        }
        catch (Exception ex)
        {
            _errorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
            RaiseItemsChanged();
        }
    }
}
