using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Graphics.Imaging;
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.ContentSafety;
using Microsoft.Windows.AI.Imaging;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;

namespace NpuOrganizeKeeper;

/// <summary>
/// Debounced FileSystemWatcher that coalesces noisy Created/Changed/Renamed
/// bursts from Snipping Tool / Xbox Game Bar into a single rename per stable path.
/// </summary>
internal sealed class ScreenshotWatcher : IDisposable
{
    private readonly StateStore _store;
    private readonly Func<KeeperConfig> _readConfig;
    private readonly ConcurrentDictionary<string, PendingFile> _pending = new(StringComparer.OrdinalIgnoreCase);
    private FileSystemWatcher? _fsw;
    private CancellationTokenSource? _cts;
    private Task? _processor;
    private Regex? _ignoreRegex;

    public ScreenshotWatcher(StateStore store, Func<KeeperConfig> readConfig)
    {
        _store = store;
        _readConfig = readConfig;
    }

    public void Start(KeeperConfig cfg)
    {
        StopInternal();

        string watchedDir = cfg.WatchFolder;
        if (string.IsNullOrWhiteSpace(watchedDir) || !Directory.Exists(watchedDir))
        {
            _store.AppendLog($"watch  ERROR  watchFolder not found: '{cfg.WatchFolder}'");
            return;
        }

        _ignoreRegex = null;
        if (!string.IsNullOrWhiteSpace(cfg.IgnorePattern))
        {
            try
            {
                _ignoreRegex = new Regex(cfg.IgnorePattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
            }
            catch (Exception ex)
            {
                _store.AppendLog($"watch  WARN   ignorePattern invalid: {ex.Message}");
            }
        }

        _fsw = new FileSystemWatcher(watchedDir)
        {
            IncludeSubdirectories = false,
            EnableRaisingEvents   = true,
            NotifyFilter          = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.CreationTime,
        };
        _fsw.Created += (_, e) => Enqueue(e.FullPath);
        _fsw.Changed += (_, e) => Enqueue(e.FullPath);
        _fsw.Renamed += (_, e) => Enqueue(e.FullPath);

        _cts       = new CancellationTokenSource();
        _processor = Task.Run(() => ProcessLoopAsync(_cts.Token));

        _store.AppendLog($"watch  start  folder={watchedDir}  debounce={cfg.DebounceMs}ms  battery-skip={cfg.SkipOnBattery}");
    }

    public void Stop()
    {
        StopInternal();
        _store.AppendLog("watch  stop");
    }

    private void StopInternal()
    {
        try { _cts?.Cancel(); } catch { /* ignore */ }
        try
        {
            if (_fsw is not null)
            {
                _fsw.EnableRaisingEvents = false;
                _fsw.Dispose();
                _fsw = null;
            }
        }
        catch { /* ignore */ }
        try { _processor?.Wait(TimeSpan.FromSeconds(2)); } catch { /* ignore */ }
        _processor = null;
        _cts?.Dispose();
        _cts = null;
        _pending.Clear();
    }

    public void Dispose() => StopInternal();

    private void Enqueue(string fullPath)
    {
        var cfg      = _readConfig();
        var basename = Path.GetFileName(fullPath);
        var ext      = SlugGenerator.NormalizeExtension(Path.GetExtension(basename));

        if (cfg.FileExtensions.Count > 0 &&
            !cfg.FileExtensions.Any(e => string.Equals(SlugGenerator.NormalizeExtension(e), ext, StringComparison.Ordinal)))
            return;

        if (cfg.SkipAlreadyNamed && SlugGenerator.IsAlreadyDateNamed(basename)) return;
        if (_ignoreRegex is not null && _ignoreRegex.IsMatch(basename)) return;

        var now = DateTime.UtcNow;
        _pending.AddOrUpdate(fullPath, _ => new PendingFile(fullPath, now), (_, p) => p with { LastEventAt = now });
        TouchHeartbeat();
    }

    private void TouchHeartbeat()
    {
        var st = _store.LoadState();
        st.LastEventAt     = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        st.LastHeartbeatAt = st.LastEventAt;
        _store.SaveState(st);
    }

    private async Task ProcessLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(250, ct).ConfigureAwait(false);
                var cfg = _readConfig();
                foreach (var item in TakeDue(cfg.DebounceMs))
                {
                    if (ct.IsCancellationRequested) break;
                    await TryProcessOneAsync(item.FullPath, cfg, ct).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _store.AppendLog($"watch  ERROR  {ex.Message}");
                try { await Task.Delay(1000, ct).ConfigureAwait(false); } catch { /* ignore */ }
            }
        }
    }

    private List<PendingFile> TakeDue(int debounceMs)
    {
        var now = DateTime.UtcNow;
        var due = new List<PendingFile>();
        foreach (var kvp in _pending)
        {
            if ((now - kvp.Value.LastEventAt).TotalMilliseconds >= debounceMs)
            {
                if (_pending.TryRemove(kvp.Key, out var taken))
                    due.Add(taken);
            }
        }
        return due;
    }

    private async Task TryProcessOneAsync(string fullPath, KeeperConfig cfg, CancellationToken ct)
    {
        try
        {
            if (!File.Exists(fullPath)) return;

            var info = new FileInfo(fullPath);

            if (cfg.MaxFileSizeBytes.HasValue && info.Length > cfg.MaxFileSizeBytes.Value)
            {
                SkipAndCount($"{info.Name} (size {info.Length} > {cfg.MaxFileSizeBytes.Value})");
                return;
            }

            if (cfg.SkipOnBattery && !PowerStatus.IsOnAcPower())
            {
                SkipAndCount($"{info.Name} (on battery)");
                return;
            }

            if (!await TryWaitForReadableAsync(fullPath, TimeSpan.FromSeconds(5), ct).ConfigureAwait(false))
            {
                SkipAndCount($"{info.Name} (file still locked after 5s)");
                return;
            }

            string description = string.Empty;
            try
            {
                description = await DescribeImageAsync(fullPath).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _store.AppendLog($"watch  WARN   AI description failed for {info.Name}: {ex.Message}");
            }

            string slug;
            string confidence;
            if (!string.IsNullOrEmpty(description))
            {
                slug       = SlugGenerator.Slugify(description, cfg.MaxSlugTokens);
                confidence = "ai";
            }
            else
            {
                slug       = string.Empty;
                confidence = "fallback";
            }

            if (slug.Length == 0)
            {
                slug       = SlugGenerator.BuildFallbackSlug($"{fullPath}:{info.CreationTimeUtc.Ticks}");
                confidence = "fallback";
            }

            DateTime captureLocal = info.CreationTime > DateTime.MinValue ? info.CreationTime : info.LastWriteTime;
            string baseFilename   = SlugGenerator.BuildTargetFilename(slug, info.Extension, captureLocal);

            var existing = new HashSet<string>(
                Directory.EnumerateFiles(Path.GetDirectoryName(fullPath)!).Select(p => Path.GetFileName(p).ToLowerInvariant()),
                StringComparer.Ordinal);

            string finalBasename = SlugGenerator.ResolveCollision(baseFilename, n => existing.Contains(n.ToLowerInvariant()));
            string destPath      = Path.Combine(Path.GetDirectoryName(fullPath)!, finalBasename);

            if (string.Equals(destPath, fullPath, StringComparison.OrdinalIgnoreCase))
            {
                SkipAndCount($"{info.Name} (already at target name)");
                return;
            }

            if (File.Exists(destPath))
            {
                FailAndCount($"rename {info.Name} -> {finalBasename} (target already exists)");
                return;
            }

            try
            {
                File.Move(fullPath, destPath);
            }
            catch (Exception ex)
            {
                FailAndCount($"rename {info.Name} -> {finalBasename}  ERROR  {ex.Message}");
                return;
            }

            var st = _store.LoadState();
            st.Processed        += 1;
            st.LastProcessedAt   = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
            st.LastProcessedPath = destPath;
            st.LastHeartbeatAt   = st.LastProcessedAt;
            st.LastError         = null;
            _store.SaveState(st);

            _store.AppendLog($"rename  {info.Name}  ->  {finalBasename}  [{confidence}]");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            FailAndCount($"process {Path.GetFileName(fullPath)} ERROR {ex.Message}");
        }
    }

    private static async Task<string> DescribeImageAsync(string imagePath)
    {
        if (ImageDescriptionGenerator.GetReadyState() != AIFeatureReadyState.Ready)
        {
            var ready = await ImageDescriptionGenerator.EnsureReadyAsync();
            if (ready.Status != AIFeatureReadyResultState.Success)
                throw new InvalidOperationException($"ImageDescriptionGenerator unavailable: {ready.Status}");
        }

        var file = await StorageFile.GetFileFromPathAsync(imagePath);
        SoftwareBitmap bitmap;
        using (var stream = await file.OpenAsync(FileAccessMode.Read))
        {
            var decoder = await BitmapDecoder.CreateAsync(stream);
            bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        }

        using var imageBuffer = ImageBuffer.CreateForSoftwareBitmap(bitmap);
        var generator = await ImageDescriptionGenerator.CreateAsync();
        var response  = await generator.DescribeAsync(imageBuffer, ImageDescriptionKind.BriefDescription, new ContentFilterOptions());
        return (response?.Description ?? string.Empty).Trim();
    }

    private static async Task<bool> TryWaitForReadableAsync(string path, TimeSpan timeout, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
                return true;
            }
            catch (IOException)
            {
                try { await Task.Delay(150, ct).ConfigureAwait(false); } catch { return false; }
            }
            catch { return false; }
        }
        return false;
    }

    private void SkipAndCount(string message)
    {
        var st = _store.LoadState();
        st.Skipped        += 1;
        st.LastHeartbeatAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        _store.SaveState(st);
        _store.AppendLog($"skip   {message}");
    }

    private void FailAndCount(string message)
    {
        var st = _store.LoadState();
        st.Errors         += 1;
        st.LastError       = message;
        st.LastHeartbeatAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        _store.SaveState(st);
        _store.AppendLog("ERROR  " + message);
    }

    private readonly record struct PendingFile(string FullPath, DateTime LastEventAt);
}
