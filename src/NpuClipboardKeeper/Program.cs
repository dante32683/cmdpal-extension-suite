using System.Diagnostics;
using System.Text.Json;
using NpuClipboardKeeper;
using NpuTools.Clipboard.Data;

WinRT.ComWrappersSupport.InitializeComWrappers();

string mode = args.Length > 0 ? args[0] : "watch";
Directory.CreateDirectory(ClipboardPaths.SupportDir());

try
{
    return mode switch
    {
        "watch" => await RunWatchAsync().ConfigureAwait(false),
        "capture-once" => await RunCaptureOnceAsync().ConfigureAwait(false),
        "status" => RunStatus(),
        _ => Error("Usage: NpuClipboardKeeper.exe <watch|capture-once|status>"),
    };
}
catch (Exception ex)
{
    AppendLog($"fatal  {ex}");
    Console.Error.WriteLine(ex);
    return 1;
}

static int Error(string message)
{
    Console.Error.WriteLine(message);
    return 1;
}

static async Task<int> RunWatchAsync()
{
    TryDelete(ClipboardPaths.StopFlagPath());
    var settings = new ClipboardSettingsStore();
    var store = new ClipboardStore();
    var capture = new ClipboardCaptureService(store, settings);
    var state = LoadState();
    state.StartedAt = DateTimeOffset.UtcNow.ToString("O");
    state.LastHeartbeatAt = state.StartedAt;
    SaveState(state);

    uint lastSequence = NativeMethods.GetClipboardSequenceNumber();
    DateTimeOffset lastPrune = DateTimeOffset.UtcNow;
    AppendLog($"watch  start sequence={lastSequence}");

    while (!File.Exists(ClipboardPaths.StopFlagPath()))
    {
        await Task.Delay(700).ConfigureAwait(false);
        state.LastHeartbeatAt = DateTimeOffset.UtcNow.ToString("O");

        try
        {
            settings.Reload();
            uint sequence = NativeMethods.GetClipboardSequenceNumber();
            if (sequence != lastSequence)
            {
                lastSequence = sequence;
                var result = await capture.TryCaptureCurrentAsync().ConfigureAwait(false);
                if (result.Captured)
                {
                    state.Captured++;
                    state.LastCapturedAt = DateTimeOffset.UtcNow.ToString("O");
                    state.LastSkippedReason = null;
                    AppendLog($"capture  {result.Message}");
                }
                else
                {
                    state.Skipped++;
                    state.LastSkippedReason = result.Message;
                    AppendLog($"skip  {result.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            state.Errors++;
            state.LastError = $"{ex.GetType().Name}: {ex.Message}";
            AppendLog($"error  {state.LastError}");
        }

        // Prune sync folder entries older than 30 days once per hour.
        if ((DateTimeOffset.UtcNow - lastPrune).TotalHours >= 1)
        {
            ClipboardSyncService.PruneOldEntries(settings.Current.SyncFolder, DateTimeOffset.UtcNow.AddDays(-30));
            lastPrune = DateTimeOffset.UtcNow;
        }

        SaveState(state);
    }

    TryDelete(ClipboardPaths.StopFlagPath());
    AppendLog("watch  stopped");
    return 0;
}

static async Task<int> RunCaptureOnceAsync()
{
    var settings = new ClipboardSettingsStore();
    var store = new ClipboardStore();
    var capture = new ClipboardCaptureService(store, settings);
    var result = await capture.TryCaptureCurrentAsync().ConfigureAwait(false);
    Console.WriteLine($"{result.Captured}: {result.Message}");
    return result.Captured ? 0 : 2;
}

static int RunStatus()
{
    Console.WriteLine(JsonSerializer.Serialize(LoadState(), ClipboardJsonContext.Default.ClipboardKeeperState));
    return 0;
}

static ClipboardKeeperState LoadState()
{
    try
    {
        string path = ClipboardPaths.StatePath();
        if (!File.Exists(path)) return new ClipboardKeeperState();
        return JsonSerializer.Deserialize(File.ReadAllText(path), ClipboardJsonContext.Default.ClipboardKeeperState) ?? new ClipboardKeeperState();
    }
    catch { return new ClipboardKeeperState(); }
}

static void SaveState(ClipboardKeeperState state)
{
    try
    {
        string path = ClipboardPaths.StatePath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(state, ClipboardJsonContext.Default.ClipboardKeeperState));
    }
    catch { }
}

static void AppendLog(string line)
{
    try
    {
        File.AppendAllText(ClipboardPaths.LogPath(), $"{DateTimeOffset.UtcNow:O}  {line}{Environment.NewLine}");
    }
    catch { }
}

static void TryDelete(string path)
{
    try { if (File.Exists(path)) File.Delete(path); }
    catch { }
}
