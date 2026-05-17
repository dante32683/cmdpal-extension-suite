// NpuOrganizeKeeper — background watcher daemon for NPU Organize extension.
using System;
using System.IO;
using System.Threading.Tasks;
//
// Watches the configured Screenshots folder via FileSystemWatcher, debounces
// noisy Created/Changed bursts, and renames each stable file using
// ImageDescriptionGenerator (NPU vision model) to produce a meaningful slug.
// State lives in %LocalAppData%\NpuOrganize\state.json so the Watcher
// Dashboard page can show live status without IPC.
//
// Modes:
//   watch          (default) — runs until killed or stop.flag appears
//   process-one <path>       — synchronous one-shot rename, useful for tests
//   status                   — prints state.json to stdout and exits

using NpuOrganizeKeeper;

WinRT.ComWrappersSupport.InitializeComWrappers();

string mode = args.Length > 0 ? args[0] : "watch";

string supportDir = StateStore.DefaultSupportDir();
var store = new StateStore(supportDir);

try
{
    return mode switch
    {
        "watch"       => await RunWatchAsync(store).ConfigureAwait(false),
        "status"      => RunStatus(store),
        "process-one" => args.Length < 2
            ? Error("Usage: NpuOrganizeKeeper.exe process-one <imagePath>")
            : await RunProcessOneAsync(store, args[1]).ConfigureAwait(false),
        _ => Error($"Unknown mode: {mode}\nUsage: NpuOrganizeKeeper.exe <watch|status|process-one> [args]"),
    };
}
catch (Exception ex)
{
    store.AppendLog($"main  FATAL  {ex.Message}");
    Console.Error.WriteLine(ex);
    return 1;
}

static int Error(string msg)
{
    Console.Error.WriteLine(msg);
    return 1;
}

static async Task<int> RunWatchAsync(StateStore store)
{
    store.ClearStopFlag();

    var cfg = GetOrCreateConfig(store);

    var startState = store.LoadState();
    startState.StartedAt      = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
    startState.LastHeartbeatAt = startState.StartedAt;
    startState.LastError       = null;
    startState.WatchFolder     = cfg.WatchFolder;
    store.SaveState(startState);

    store.AppendLog($"watch  init  folder={cfg.WatchFolder}");

    DateTime lastConfigMtime = store.ConfigLastWriteUtc();
    var currentCfg = cfg;

    using var watcher = new ScreenshotWatcher(store, () => currentCfg);
    watcher.Start(currentCfg);

    try
    {
        while (true)
        {
            await Task.Delay(1000).ConfigureAwait(false);

            if (store.IsStopRequested())
            {
                store.AppendLog("watch  stop-flag-detected");
                store.ClearStopFlag();
                break;
            }

            // Hot-reload config when mtime changes.
            var mtime = store.ConfigLastWriteUtc();
            if (mtime > lastConfigMtime)
            {
                var updated = store.TryLoadConfig();
                if (updated is not null)
                {
                    currentCfg = updated;
                    watcher.Start(currentCfg);
                    store.AppendLog("watch  config-reloaded");
                }
                lastConfigMtime = mtime;
            }

            var st = store.LoadState();
            st.LastHeartbeatAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
            store.SaveState(st);
        }
    }
    finally
    {
        watcher.Stop();
        var finalState = store.LoadState();
        finalState.LastHeartbeatAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        store.SaveState(finalState);
    }

    return 0;
}

static int RunStatus(StateStore store)
{
    var state = store.LoadState();
    var cfg   = store.TryLoadConfig();
    var payload = new
    {
        supportDir = store.SupportDir,
        config = cfg,
        state,
    };
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(payload,
        new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    return 0;
}

static async Task<int> RunProcessOneAsync(StateStore store, string imagePath)
{
    if (!File.Exists(imagePath))
    {
        Console.Error.WriteLine($"File not found: {imagePath}");
        return 1;
    }

    var cfg  = GetOrCreateConfig(store);
    var info = new FileInfo(imagePath);

    Console.WriteLine($"Describing {info.Name}…");

    string description;
    try
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();
        description = await DescribeImageAsync(imagePath).ConfigureAwait(false);
        Console.WriteLine($"Description: {description}");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"AI description failed: {ex.Message}");
        description = string.Empty;
    }

    string slug = string.IsNullOrEmpty(description)
        ? SlugGenerator.BuildFallbackSlug($"{imagePath}:{info.CreationTimeUtc.Ticks}")
        : SlugGenerator.Slugify(description, cfg.MaxSlugTokens);

    if (slug.Length == 0)
        slug = SlugGenerator.BuildFallbackSlug($"{imagePath}:{info.CreationTimeUtc.Ticks}");

    DateTime captureLocal = info.CreationTime > DateTime.MinValue ? info.CreationTime : info.LastWriteTime;
    string   baseFilename = SlugGenerator.BuildTargetFilename(slug, info.Extension, captureLocal);

    Console.WriteLine($"{info.Name}  ->  {baseFilename}");
    return 0;
}

static KeeperConfig GetOrCreateConfig(StateStore store)
{
    var cfg = store.TryLoadConfig();
    if (cfg is not null) return cfg;

    cfg = new KeeperConfig
    {
        WatchFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Pictures", "Screenshots"),
    };
    store.SaveConfig(cfg);
    store.AppendLog($"watch  created-default-config  folder={cfg.WatchFolder}");
    return cfg;
}

static async Task<string> DescribeImageAsync(string imagePath)
{
    using Microsoft.Graphics.Imaging.ImageBuffer imageBuffer = await LoadImageBufferAsync(imagePath).ConfigureAwait(false);
    var generator = await Microsoft.Windows.AI.Imaging.ImageDescriptionGenerator.CreateAsync();
    var response  = await generator.DescribeAsync(
        imageBuffer,
        Microsoft.Windows.AI.Imaging.ImageDescriptionKind.BriefDescription,
        new Microsoft.Windows.AI.ContentSafety.ContentFilterOptions());
    return (response?.Description ?? string.Empty).Trim();
}

static async Task<Microsoft.Graphics.Imaging.ImageBuffer> LoadImageBufferAsync(string imagePath)
{
    var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(imagePath);
    Windows.Graphics.Imaging.SoftwareBitmap bitmap;
    using (var stream = await file.OpenAsync(Windows.Storage.FileAccessMode.Read))
    {
        var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(stream);
        bitmap = await decoder.GetSoftwareBitmapAsync(
            Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8,
            Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied);
    }
    return Microsoft.Graphics.Imaging.ImageBuffer.CreateForSoftwareBitmap(bitmap);
}
