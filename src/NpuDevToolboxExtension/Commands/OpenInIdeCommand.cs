using System;
using System.Diagnostics;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.DevToolbox.Models;
using NpuTools.DevToolbox.Services;

namespace NpuTools.DevToolbox.Commands;

internal sealed partial class OpenInIdeCommand : InvokableCommand
{
    private readonly string _path;
    private readonly DevToolboxSettingsStore _settings;
    private readonly RecentWorkspacesStore _recents;

    public OpenInIdeCommand(string path, DevToolboxSettingsStore settings, RecentWorkspacesStore recents)
    {
        _path = path;
        _settings = settings;
        _recents = recents;
        Name = "Open in IDE";
        Icon = DevToolboxVisuals.Ide;
    }

    public override CommandResult Invoke()
    {
        _recents.Add(_path);
        var s = _settings.Current;
        try
        {
            Launch(s);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OpenInIdeCommand: {ex.GetType().Name}: {ex.Message}");
        }

        return CommandResult.Dismiss();
    }

    private void Launch(DevToolboxSettings s)
    {
        string exe = s.PreferredIde switch
        {
            IdeChoice.VSCode    => "code",
            IdeChoice.Cursor    => "cursor",
            IdeChoice.Windsurf  => "windsurf",
            IdeChoice.Custom when !string.IsNullOrWhiteSpace(s.CustomIdeExe) => s.CustomIdeExe,
            _                   => "code",
        };

        Process.Start(new ProcessStartInfo
        {
            FileName = exe,
            Arguments = $"\"{_path}\"",
            UseShellExecute = true,
        });
    }
}
