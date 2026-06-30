using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.Clipboard.Commands;
using NpuTools.Clipboard.Data;

namespace NpuTools.Clipboard.Pages;

internal sealed partial class ClipboardSettingsPage : ListPage
{
    private readonly ClipboardSettingsStore _settings;
    private readonly ClipboardStore _store;

    public ClipboardSettingsPage(ClipboardSettingsStore settings, ClipboardStore store)
    {
        _settings = settings;
        _store = store;
        Id = "com.local.nputools.clipboard.settings";
        Title = "Clipboard Settings";
        Name = "Settings";
        Icon = ClipboardVisuals.Settings;
    }

    public override IListItem[] GetItems()
    {
        _settings.Reload();
        var settings = _settings.Current;
        bool running = IsKeeperRunning();
        var state = TryLoadState();

        return
        [
            new ListItem(new StartStopRecorderCommand(running))
            {
                Title = running ? "Recorder running" : "Recorder stopped",
                Subtitle = state is null ? "Start the background recorder to capture new clipboard changes." : $"Captured {state.Captured}, skipped {state.Skipped}, errors {state.Errors}",
                Icon = running ? ClipboardVisuals.Start : ClipboardVisuals.Stop,
                Tags = [ClipboardVisuals.MutedTag(running ? "running" : "stopped")],
            },
            new ListItem(new SetPrimaryActionCommand(_settings, ClipboardPrimaryAction.Paste))
            {
                Title = "Primary action: Paste",
                Subtitle = settings.PrimaryAction == ClipboardPrimaryAction.Paste ? "Current" : "Press Enter to use paste as the default entry action.",
                Icon = ClipboardVisuals.Paste,
                Tags = settings.PrimaryAction == ClipboardPrimaryAction.Paste ? [ClipboardVisuals.StatusTag("current")] : [],
            },
            new ListItem(new SetPrimaryActionCommand(_settings, ClipboardPrimaryAction.Copy))
            {
                Title = "Primary action: Copy",
                Subtitle = settings.PrimaryAction == ClipboardPrimaryAction.Copy ? "Current" : "Press Enter to use copy as the default entry action.",
                Icon = ClipboardVisuals.Copy,
                Tags = settings.PrimaryAction == ClipboardPrimaryAction.Copy ? [ClipboardVisuals.StatusTag("current")] : [],
            },
            RetentionItem(200, settings),
            RetentionItem(500, settings),
            RetentionItem(1000, settings),
            RetentionItem(-1, settings),
            new ListItem(new SetSyncFolderPage(_settings))
            {
                Title    = "Cross-Device Sync Folder",
                Subtitle = string.IsNullOrWhiteSpace(settings.SyncFolder) ? "Not configured — tap to set a shared folder path" : $"Sync folder: {settings.SyncFolder}",
                Icon     = ClipboardVisuals.Sync,
                Tags     = string.IsNullOrWhiteSpace(settings.SyncFolder) ? [] : [ClipboardVisuals.StatusTag("active")],
            },
            new ListItem(new DisabledApplicationsPage(_settings))
            {
                Title = "Disabled Applications",
                Subtitle = settings.DisabledApplicationNames.Count == 0 ? "None" : string.Join(", ", settings.DisabledApplicationNames.Take(6)),
                Icon = ClipboardVisuals.Settings,
            },
            new ListItem(new SecretPatternsPage(_settings))
            {
                Title = "Secret Patterns",
                Subtitle = !settings.SecretDetectionEnabled
                    ? "Disabled — toggle in extension settings to enable"
                    : settings.SecretPatterns.Count == 0
                        ? "Empty — capture will not be filtered"
                        : $"Active — {settings.SecretPatterns.Count} pattern{(settings.SecretPatterns.Count == 1 ? "" : "s")} loaded",
                Icon = ClipboardVisuals.Settings,
                Tags = settings.SecretDetectionEnabled ? [ClipboardVisuals.StatusTag("filtered")] : [ClipboardVisuals.MutedTag("off")],
            },
            new ListItem(new DeleteByWindowCommand(_store, TimeSpan.FromMinutes(5), "Last 5 Minutes")) { Title = "Delete Last 5 Minutes", Icon = ClipboardVisuals.Delete },
            new ListItem(new DeleteByWindowCommand(_store, TimeSpan.FromMinutes(15), "Last 15 Minutes")) { Title = "Delete Last 15 Minutes", Icon = ClipboardVisuals.Delete },
            new ListItem(new DeleteByWindowCommand(_store, TimeSpan.FromMinutes(30), "Last 30 Minutes")) { Title = "Delete Last 30 Minutes", Icon = ClipboardVisuals.Delete },
            new ListItem(new DeleteByWindowCommand(_store, TimeSpan.FromHours(1), "Last Hour")) { Title = "Delete Last Hour", Icon = ClipboardVisuals.Delete },
            new ListItem(new DeleteByWindowCommand(_store, TimeSpan.FromHours(24), "Last 24 Hours")) { Title = "Delete Last 24 Hours", Icon = ClipboardVisuals.Delete },
            new ListItem(new DeleteAllPage(_store))
            {
                Title = "Delete All Entries",
                Subtitle = "Requires typing DELETE on the confirmation page.",
                Icon = ClipboardVisuals.Delete,
                Tags = [ClipboardVisuals.CriticalTag("danger")],
            },
        ];
    }

    private ListItem RetentionItem(int limit, ClipboardAppSettings settings)
    {
        string title = limit < 0 ? "Retention: Unlimited" : $"Retention: {limit} entries";
        return new ListItem(new SetRetentionCommand(_settings, _store, limit))
        {
            Title = title,
            Subtitle = settings.NormalizedRetentionLimit == limit ? "Current" : "Press Enter to apply.",
            Icon = ClipboardVisuals.Settings,
            Tags = settings.NormalizedRetentionLimit == limit ? [ClipboardVisuals.StatusTag("current")] : [],
        };
    }

    private static bool IsKeeperRunning()
    {
        try { return Process.GetProcessesByName("NpuClipboardKeeper").Length > 0; }
        catch { return false; }
    }

    private static ClipboardKeeperState? TryLoadState()
    {
        try
        {
            string path = ClipboardPaths.StatePath();
            if (!File.Exists(path)) return null;
            return JsonSerializer.Deserialize(File.ReadAllText(path), ClipboardJsonContext.Default.ClipboardKeeperState);
        }
        catch { return null; }
    }
}
