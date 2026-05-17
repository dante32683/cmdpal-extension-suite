using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.Organize.Commands;

namespace NpuTools.Organize.Pages;

internal sealed partial class WatcherDashboardPage : ListPage
{
    private static readonly string KeeperPath = Path.Combine(
        AppContext.BaseDirectory, "NpuOrganizeKeeper.exe");

    private static readonly string StatePath = Path.Combine(
        Environment.GetEnvironmentVariable("LOCALAPPDATA")
            ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NpuOrganize", "state.json");

    public WatcherDashboardPage()
    {
        Id    = "com.local.nputools.organize.watcher";
        Title = "Screenshot Watcher";
        Name  = "Watcher";
        Icon  = OrganizeVisuals.Watcher;
    }

    public override IListItem[] GetItems()
    {
        bool keeperInstalled = File.Exists(KeeperPath);
        int? keeperPid       = FindKeeperPid();
        bool running         = keeperPid.HasValue;

        if (!keeperInstalled)
        {
            return
            [
                new ListItem(new NoOpCommand())
                {
                    Title    = "OrganizeKeeper not installed",
                    Subtitle = "Build the solution first — NpuOrganizeKeeper.exe is copied to this extension's output on build.",
                    Icon     = OrganizeVisuals.Warning,
                    Tags     = [OrganizeVisuals.MutedTag("not available")],
                },
            ];
        }

        var state = TryLoadState();
        var items = new System.Collections.Generic.List<IListItem>
        {
            new ListItem(new NoOpCommand())
            {
                Title    = running ? $"Running — PID {keeperPid}" : "Stopped",
                Subtitle = running
                    ? $"Watching: {state?.WatchFolder ?? "unknown"}"
                    : "OrganizeKeeper is not running.",
                Icon     = running ? OrganizeVisuals.Start : OrganizeVisuals.Stop,
                Tags     = [OrganizeVisuals.MutedTag(running ? "running" : "stopped")],
            },
            new ListItem(new StartStopKeeperCommand(KeeperPath, running))
            {
                Title = running ? "Stop Watcher" : "Start Watcher",
                Icon  = running ? OrganizeVisuals.Stop : OrganizeVisuals.Start,
            },
        };

        if (state is not null)
        {
            items.Add(new ListItem(new NoOpCommand())
            {
                Title    = $"Renamed: {state.Processed}   Skipped: {state.Skipped}   Errors: {state.Errors}",
                Subtitle = state.LastProcessedPath is not null
                    ? $"Last: {Path.GetFileName(state.LastProcessedPath)}"
                    : "No files renamed yet.",
                Tags     = [OrganizeVisuals.MutedTag("stats")],
            });

            if (state.LastError is not null)
            {
                items.Add(new ListItem(new NoOpCommand())
                {
                    Title = state.LastError,
                    Icon  = OrganizeVisuals.Warning,
                    Tags  = [OrganizeVisuals.MutedTag("last error")],
                });
            }
        }

        return [.. items];
    }

    private static int? FindKeeperPid()
    {
        try
        {
            foreach (var proc in Process.GetProcessesByName("NpuOrganizeKeeper"))
                return proc.Id;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Organize watcher pid check failed: {ex}");
        }
        return null;
    }

    private static WatcherState? TryLoadState()
    {
        try
        {
            if (!File.Exists(StatePath)) return null;
            string json = File.ReadAllText(StatePath);
            return JsonSerializer.Deserialize(json, WatcherStateContext.Default.WatcherState);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Organize state.json read failed: {ex}");
            return null;
        }
    }

    private sealed class WatcherState
    {
        [JsonPropertyName("processed")]         public int     Processed         { get; set; }
        [JsonPropertyName("skipped")]           public int     Skipped           { get; set; }
        [JsonPropertyName("errors")]            public int     Errors            { get; set; }
        [JsonPropertyName("watchFolder")]       public string? WatchFolder       { get; set; }
        [JsonPropertyName("lastProcessedPath")] public string? LastProcessedPath { get; set; }
        [JsonPropertyName("lastError")]         public string? LastError         { get; set; }
    }

    [JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
    [JsonSerializable(typeof(WatcherState))]
    private sealed partial class WatcherStateContext : JsonSerializerContext { }
}
