using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.DevToolbox.Commands;
using NpuTools.DevToolbox.Services;

namespace NpuTools.DevToolbox.Pages;

internal sealed partial class WorkspaceActionsPage : ListPage
{
    private readonly string _path;
    private readonly DevToolboxSettingsStore _settings;
    private readonly RecentWorkspacesStore _recents;
    private readonly DevToolboxAiService _ai;
    private readonly bool _isRecent;

    public WorkspaceActionsPage(string path, DevToolboxSettingsStore settings, RecentWorkspacesStore recents, DevToolboxAiService ai, bool isRecent = false)
    {
        _path = path;
        _settings = settings;
        _recents = recents;
        _ai = ai;
        _isRecent = isRecent;
        Id = "com.local.nputools.devtoolbox.workspace";
        Title = System.IO.Path.GetFileName(path.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar)) ?? path;
        Name = "Open";
        Icon = DevToolboxVisuals.Workspace;
    }

    public override IListItem[] GetItems()
    {
        var items = new System.Collections.Generic.List<IListItem>
        {
            new ListItem(new OpenInExplorerCommand(_path, _recents))
            {
                Title = "Open in Explorer",
                Subtitle = _path,
                Icon = DevToolboxVisuals.Explorer,
            },
            new ListItem(new OpenInTerminalCommand(_path, _settings, _recents))
            {
                Title = "Open in Terminal",
                Subtitle = TerminalSubtitle(_settings.Current),
                Icon = DevToolboxVisuals.Terminal,
            },
            new ListItem(new OpenInIdeCommand(_path, _settings, _recents))
            {
                Title = "Open in IDE",
                Subtitle = IdeLabel(_settings.Current.PreferredIde),
                Icon = DevToolboxVisuals.Ide,
            },
            new ListItem(new CopyTextCommand(_path) { Name = "Copy Path" })
            {
                Title = "Copy Path",
                Subtitle = _path,
                Icon = DevToolboxVisuals.Copy,
            },
            new ListItem(new GenerateCommitMessagePage(_path, _ai))
            {
                Title = "Generate Commit Message",
                Subtitle = "Use Phi to write a commit message from the current diff",
                Icon = DevToolboxVisuals.Commit,
            },
        };

        if (_isRecent)
        {
            items.Add(new ListItem(new RemoveRecentCommand(_path, _recents))
            {
                Title = "Remove from Recents",
                Subtitle = "Remove this workspace from the recent list",
                Icon = DevToolboxVisuals.Remove,
            });
        }

        return [.. items];
    }

    private static string TerminalSubtitle(Models.DevToolboxSettings s)
    {
        string label = TerminalLabel(s.PreferredTerminal);
        return s.PreferredTerminal == Models.TerminalChoice.WindowsTerminal && !string.IsNullOrWhiteSpace(s.WindowsTerminalProfile)
            ? $"{label} — {s.WindowsTerminalProfile}"
            : label;
    }

    private static string TerminalLabel(Models.TerminalChoice choice) => choice switch
    {
        Models.TerminalChoice.WindowsTerminal => "Windows Terminal",
        Models.TerminalChoice.PowerShell      => "PowerShell",
        Models.TerminalChoice.Cmd             => "Command Prompt",
        Models.TerminalChoice.Custom          => "Custom terminal",
        _                                     => "Terminal",
    };

    private static string IdeLabel(Models.IdeChoice choice) => choice switch
    {
        Models.IdeChoice.VSCode    => "VS Code",
        Models.IdeChoice.Cursor    => "Cursor",
        Models.IdeChoice.Windsurf  => "Windsurf",
        Models.IdeChoice.Custom    => "Custom IDE",
        _                          => "IDE",
    };

    internal sealed partial class RemoveRecentCommand : InvokableCommand
    {
        private readonly string _path;
        private readonly RecentWorkspacesStore _recents;

        public RemoveRecentCommand(string path, RecentWorkspacesStore recents)
        {
            _path = path;
            _recents = recents;
            Name = "Remove from Recents";
            Icon = DevToolboxVisuals.Remove;
        }

        public override CommandResult Invoke()
        {
            _recents.Remove(_path);
            return CommandResult.GoHome();
        }
    }
}
