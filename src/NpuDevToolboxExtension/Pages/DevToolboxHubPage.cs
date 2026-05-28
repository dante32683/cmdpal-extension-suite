using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.DevToolbox.Commands;
using NpuTools.DevToolbox.Models;
using NpuTools.DevToolbox.Services;

namespace NpuTools.DevToolbox.Pages;

internal sealed partial class DevToolboxHubPage : DynamicListPage
{
    private readonly DevToolboxSettingsStore _settings;
    private readonly RecentWorkspacesStore _recents;
    private readonly DevToolboxAiService _ai;
    private volatile List<WorkspaceEntry> _scannedWorkspaces = [];
    private IListItem[] _items;

    public DevToolboxHubPage(DevToolboxSettingsStore settings, RecentWorkspacesStore recents, DevToolboxAiService ai)
    {
        _settings = settings;
        _recents = recents;
        _ai = ai;
        Id = "com.local.nputools.devtoolbox.hub";
        Title = "Dev Toolbox";
        Name = "Open";
        Icon = DevToolboxVisuals.Toolbox;
        PlaceholderText = "Search workspaces or type a folder path...";
        _items = BuildItems(string.Empty);
        _ = Task.Run(() =>
        {
            try
            {
                _scannedWorkspaces = WorkspaceScanner.Scan([]);
                _items = BuildItems(SearchText?.Trim() ?? string.Empty);
                RaiseItemsChanged(_items.Length);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DevToolboxHubPage scan failed: {ex.GetType().Name}: {ex.Message}");
            }
        });
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        _items = BuildItems(newSearch.Trim());
        RaiseItemsChanged(_items.Length);
    }

    public override IListItem[] GetItems() => _items;

    private IListItem[] BuildItems(string query)
    {
        var recent = _recents.GetAll();
        var scanned = _scannedWorkspaces;

        var items = new List<IListItem>();

        // If query looks like a path, offer direct open
        if (query.Length > 0 && (query.Contains('\\') || query.Contains('/') || query.Length > 2))
        {
            string expanded = Environment.ExpandEnvironmentVariables(query);
            if (Directory.Exists(expanded))
            {
                items.Add(BuildWorkspaceItem(new WorkspaceEntry { Path = expanded, IsRecent = false, ProjectType = string.Empty }, isExplicit: true));
            }
        }

        // Recent workspaces
        var filteredRecent = recent
            .Where(p => Directory.Exists(p))
            .Where(p => MatchesQuery(p, query))
            .Take(8)
            .ToList();

        if (filteredRecent.Count > 0)
        {
            items.Add(SectionHeader("Recent Workspaces"));
            foreach (string path in filteredRecent)
            {
                items.Add(BuildWorkspaceItem(new WorkspaceEntry
                {
                    Path = path,
                    IsRecent = true,
                    ProjectType = string.Empty,
                }));
            }
        }

        // Scanned workspaces (exclude those already shown as recent)
        var recentSet = new HashSet<string>(recent, StringComparer.OrdinalIgnoreCase);
        var filteredScanned = scanned
            .Where(w => !recentSet.Contains(w.Path))
            .Where(w => MatchesQuery(w.Path, query))
            .Take(string.IsNullOrWhiteSpace(query) ? 20 : 50)
            .ToList();

        if (filteredScanned.Count > 0)
        {
            items.Add(SectionHeader("Detected Workspaces"));
            foreach (var ws in filteredScanned)
                items.Add(BuildWorkspaceItem(ws));
        }

        if (items.Count == 0)
        {
            items.Add(new ListItem(new NoOpCommand())
            {
                Title = string.IsNullOrWhiteSpace(query)
                    ? "No workspaces detected"
                    : $"No workspaces matching \"{query}\"",
                Subtitle = "Workspaces are detected from ~/repos, ~/source/repos, and similar locations",
                Icon = DevToolboxVisuals.Workspace,
            });
        }

        return [.. items];
    }

    private ListItem BuildWorkspaceItem(WorkspaceEntry ws, bool isExplicit = false)
    {
        string name = Path.GetFileName(ws.Path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                      ?? ws.Path;

        var tags = new List<Tag>();
        if (ws.IsRecent)
            tags.Add(DevToolboxVisuals.StatusTag("recent"));
        if (!string.IsNullOrWhiteSpace(ws.ProjectType))
            tags.Add(DevToolboxVisuals.MutedTag(ws.ProjectType));
        if (isExplicit)
            tags.Add(DevToolboxVisuals.StatusTag("path"));

        return new ListItem(new WorkspaceActionsPage(ws.Path, _settings, _recents, _ai, ws.IsRecent))
        {
            Title = isExplicit ? $"Open: {ws.Path}" : name,
            Subtitle = ws.Path,
            Icon = DevToolboxVisuals.Workspace,
            Tags = [.. tags],
            MoreCommands =
            [
                new CommandContextItem(new OpenInExplorerCommand(ws.Path, _recents))
                {
                    Icon = DevToolboxVisuals.Explorer,
                    RequestedShortcut = KeyChords.Explorer,
                },
                new CommandContextItem(new OpenInTerminalCommand(ws.Path, _settings, _recents))
                {
                    Icon = DevToolboxVisuals.Terminal,
                    RequestedShortcut = KeyChords.Terminal,
                },
                new CommandContextItem(new OpenInIdeCommand(ws.Path, _settings, _recents))
                {
                    Icon = DevToolboxVisuals.Ide,
                    RequestedShortcut = KeyChords.Ide,
                },
                new CommandContextItem(new GenerateCommitMessagePage(ws.Path, _ai))
                {
                    Icon = DevToolboxVisuals.Commit,
                    RequestedShortcut = KeyChords.CommitMessage,
                },
            ],
        };
    }

    private static ListItem SectionHeader(string title) =>
        new(new NoOpCommand())
        {
            Title = title,
            Tags = [DevToolboxVisuals.MutedTag("section")],
        };

    private static bool MatchesQuery(string path, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return true;

        string name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) ?? path;
        return name.Contains(query, StringComparison.OrdinalIgnoreCase)
            || path.Contains(query, StringComparison.OrdinalIgnoreCase);
    }
}
