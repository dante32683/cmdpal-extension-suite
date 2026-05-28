using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.DevToolbox.Models;
using NpuTools.DevToolbox.Services;

namespace NpuTools.DevToolbox.Commands;

// DynamicListPage that lists workspaces and immediately performs the target action on selection.
internal sealed partial class QuickOpenCommand : DynamicListPage
{
    private readonly string _action; // "explorer" | "terminal" | "ide"
    private readonly DevToolboxSettingsStore _settings;
    private readonly RecentWorkspacesStore _recents;
    private volatile List<WorkspaceEntry> _scannedWorkspaces = [];
    private IListItem[] _items;

    public QuickOpenCommand(string action, DevToolboxSettingsStore settings, RecentWorkspacesStore recents)
    {
        _action = action;
        _settings = settings;
        _recents = recents;

        (string title, string placeholder) = action switch
        {
            "terminal" => ("Open in Terminal", "Search workspaces..."),
            "ide"      => ("Open in IDE",      "Search workspaces..."),
            _          => ("Open in Explorer", "Search workspaces..."),
        };

        Id = $"com.local.nputools.devtoolbox.quick.{action}";
        Title = title;
        Name = title;
        Icon = action switch
        {
            "terminal" => DevToolboxVisuals.Terminal,
            "ide"      => DevToolboxVisuals.Ide,
            _          => DevToolboxVisuals.Explorer,
        };
        PlaceholderText = placeholder;
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
                Debug.WriteLine($"QuickOpenCommand scan failed: {ex.GetType().Name}: {ex.Message}");
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
        var recent = _recents.GetAll().Where(Directory.Exists).ToList();
        var scanned = _scannedWorkspaces;
        var recentSet = new HashSet<string>(recent, StringComparer.OrdinalIgnoreCase);

        var all = recent
            .Select(p => (Path: p, IsRecent: true, Type: string.Empty))
            .Concat(scanned
                .Where(w => !recentSet.Contains(w.Path))
                .Select(w => (Path: w.Path, IsRecent: false, Type: w.ProjectType)))
            .Where(w => string.IsNullOrWhiteSpace(query)
                        || Path.GetFileName(w.Path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                              ?.Contains(query, StringComparison.OrdinalIgnoreCase) == true
                        || w.Path.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(30)
            .ToList();

        if (all.Count == 0)
        {
            return
            [
                new ListItem(new NoOpCommand())
                {
                    Title = "No workspaces found",
                    Subtitle = "Workspaces are detected from ~/repos, ~/source/repos, and similar locations",
                    Icon = DevToolboxVisuals.Workspace,
                },
            ];
        }

        var items = new IListItem[all.Count];
        for (int i = 0; i < all.Count; i++)
        {
            var (path, isRecent, type) = all[i];
            string name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) ?? path;

            ICommand command = _action switch
            {
                "terminal" => new OpenInTerminalCommand(path, _settings, _recents),
                "ide"      => new OpenInIdeCommand(path, _settings, _recents),
                _          => new OpenInExplorerCommand(path, _recents),
            };

            var tags = new List<Tag>();
            if (isRecent) tags.Add(DevToolboxVisuals.StatusTag("recent"));
            if (!string.IsNullOrWhiteSpace(type)) tags.Add(DevToolboxVisuals.MutedTag(type));

            items[i] = new ListItem(command)
            {
                Title    = name,
                Subtitle = path,
                Icon     = DevToolboxVisuals.Workspace,
                Tags     = [.. tags],
            };
        }

        return items;
    }
}
