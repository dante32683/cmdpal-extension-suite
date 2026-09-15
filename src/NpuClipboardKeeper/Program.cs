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
    using var singleInstance = new Semaphore(1, 1, "Local\\NpuClipboardKeeper");
    if (!singleInstance.WaitOne(TimeSpan.FromSeconds(3)))
    {
        AppendLog("watch  another recorder instance remained active after the startup grace period");
        return 0;
    }

    try
    {
        TryDelete(ClipboardPaths.StopFlagPath());
        var settings = new ClipboardSettingsStore();
        var store = new ClipboardStore();
        var capture = new ClipboardCaptureService(store, settings);
        var state = LoadState();
        state.StartedAt = DateTimeOffset.UtcNow.ToString("O");
        SaveState(state);

        uint lastSequence = NativeMethods.GetClipboardSequenceNumber();
        DateTimeOffset lastPrune = DateTimeOffset.UtcNow;
        AppendLog($"watch  start sequence={lastSequence}");

        while (!File.Exists(ClipboardPaths.StopFlagPath()))
        {
            await Task.Delay(700).ConfigureAwait(false);
            bool stateChanged = false;

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
                        stateChanged = true;
                    }
                    else
                    {
                        state.Skipped++;
                        state.LastSkippedReason = result.Message;
                        AppendLog($"skip  {result.Message}");
                        stateChanged = true;
                    }
                }
            }
            catch (Exception ex)
            {
                state.Errors++;
                state.LastError = $"{ex.GetType().Name}: {ex.Message}";
                AppendLog($"error  {state.LastError}");
                stateChanged = true;
            }

            // Prune sync folder entries older than 30 days once per hour.
            if ((DateTimeOffset.UtcNow - lastPrune).TotalHours >= 1)
            {
                ClipboardSyncService.PruneOldEntries(settings.Current.SyncFolder, DateTimeOffset.UtcNow.AddDays(-30));
                lastPrune = DateTimeOffset.UtcNow;
            }

            if (stateChanged)
                SaveState(state);
        }

        TryDelete(ClipboardPaths.StopFlagPath());
        AppendLog("watch  stopped");
        return 0;
    }
    finally
    {
        singleInstance.Release();
    }
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
    catch (Exception ex)
    {
        Debug.WriteLine($"Clipboard keeper state load failed: {ex.GetType().Name}: {ex.Message}");
        return new ClipboardKeeperState();
    }
}

static void SaveState(ClipboardKeeperState state)
{
    string? tmp = null;
    try
    {
        string path = ClipboardPaths.StatePath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        tmp = $"{path}.{Environment.ProcessId}.{DateTime.UtcNow.Ticks}.tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(state, ClipboardJsonContext.Default.ClipboardKeeperState));
        File.Move(tmp, path, overwrite: true);
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"Clipboard keeper state save failed: {ex.GetType().Name}: {ex.Message}");
    }
    finally
    {
        if (tmp is not null)
            TryDelete(tmp);
    }
}

static void AppendLog(string line)
{
    try
    {
        File.AppendAllText(ClipboardPaths.LogPath(), $"{DateTimeOffset.UtcNow:O}  {line}{Environment.NewLine}");
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"Clipboard keeper log write failed: {ex.GetType().Name}: {ex.Message}");
    }
}

static void TryDelete(string path)
{
    try { if (File.Exists(path)) File.Delete(path); }
    catch (Exception ex)
    {
        Debug.WriteLine($"Clipboard keeper cleanup failed for '{path}': {ex.GetType().Name}: {ex.Message}");
    }
}
