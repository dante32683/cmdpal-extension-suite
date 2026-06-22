using System;
using System.Diagnostics;
using System.IO;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.Clipboard.Data;

namespace NpuTools.Clipboard.Commands;

internal sealed partial class SetPrimaryActionCommand : InvokableCommand
{
    private readonly ClipboardSettingsStore _settings;
    private readonly ClipboardPrimaryAction _action;

    public SetPrimaryActionCommand(ClipboardSettingsStore settings, ClipboardPrimaryAction action)
    {
        _settings = settings;
        _action = action;
        Name = action == ClipboardPrimaryAction.Paste ? "Use Paste as Primary Action" : "Use Copy as Primary Action";
        Icon = action == ClipboardPrimaryAction.Paste ? ClipboardVisuals.Paste : ClipboardVisuals.Copy;
    }

    public override CommandResult Invoke()
    {
        _settings.Update(s => s.PrimaryAction = _action);
        return CommandResult.ShowToast("Clipboard primary action updated.");
    }
}

internal sealed partial class SetRetentionCommand : InvokableCommand
{
    private readonly ClipboardSettingsStore _settings;
    private readonly ClipboardStore _store;
    private readonly int _limit;

    public SetRetentionCommand(ClipboardSettingsStore settings, ClipboardStore store, int limit)
    {
        _settings = settings;
        _store = store;
        _limit = limit;
        Name = limit < 0 ? "Keep Unlimited Entries" : $"Keep {limit} Entries";
        Icon = ClipboardVisuals.Settings;
    }

    public override CommandResult Invoke()
    {
        _settings.Update(s => s.RetentionLimit = _limit);
        _store.EnforceRetention(_settings.Current);
        return CommandResult.ShowToast(Name);
    }
}

internal sealed partial class StartStopRecorderCommand : InvokableCommand
{
    private static readonly string KeeperPath = Path.Combine(AppContext.BaseDirectory, "NpuClipboardKeeper.exe");
    private readonly bool? _isRunning;

    public StartStopRecorderCommand(bool? isRunning = null)
    {
        _isRunning = isRunning;
        Name = isRunning is null ? "Start or Stop Recorder" : isRunning.Value ? "Stop Recorder" : "Start Recorder";
        Icon = isRunning is null ? ClipboardVisuals.Settings : isRunning.Value ? ClipboardVisuals.Stop : ClipboardVisuals.Start;
    }

    public override CommandResult Invoke()
    {
        try
        {
            bool isRunning = _isRunning ?? IsKeeperRunning();
            if (isRunning)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ClipboardPaths.StopFlagPath())!);
                File.WriteAllText(ClipboardPaths.StopFlagPath(), string.Empty);
            }
            else
            {
                EnsureKeeperRunning(force: true);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Clipboard recorder toggle failed: {ex}");
        }

        return CommandResult.Dismiss();
    }

    public static void EnsureKeeperRunning(bool force = false)
    {
        try
        {
            var settings = new ClipboardSettingsStore();
            if (!settings.Current.RecorderEnabled && !force)
                return;

            if (IsKeeperRunning())
                return;

            if (File.Exists(KeeperPath))
            {
                try
                {
                    string stopFlag = ClipboardPaths.StopFlagPath();
                    if (File.Exists(stopFlag))
                        File.Delete(stopFlag);
                }
                catch { }

                Process.Start(new ProcessStartInfo(KeeperPath)
                {
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Clipboard keeper auto-start failed: {ex}");
        }
    }

    public static bool IsKeeperRunning()
    {
        try { return Process.GetProcessesByName("NpuClipboardKeeper").Length > 0; }
        catch { return false; }
    }
}

internal sealed partial class DeleteByWindowCommand : InvokableCommand
{
    private readonly ClipboardStore _store;
    private readonly TimeSpan _window;

    public DeleteByWindowCommand(ClipboardStore store, TimeSpan window, string label)
    {
        _store = store;
        _window = window;
        Name = $"Delete Entries From {label}";
        Icon = ClipboardVisuals.Delete;
    }

    public override CommandResult Invoke()
    {
        int deleted = _store.DeleteOlderThan(_window);
        return CommandResult.ShowToast($"Deleted {deleted} clipboard entr{(deleted == 1 ? "y" : "ies")}.");
    }
}
