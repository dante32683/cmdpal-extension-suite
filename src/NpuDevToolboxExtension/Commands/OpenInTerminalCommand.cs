using System;
using System.Diagnostics;
using System.IO;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.DevToolbox.Models;
using NpuTools.DevToolbox.Services;

namespace NpuTools.DevToolbox.Commands;

internal sealed partial class OpenInTerminalCommand : InvokableCommand
{
    private readonly string _path;
    private readonly DevToolboxSettingsStore _settings;
    private readonly RecentWorkspacesStore _recents;

    public OpenInTerminalCommand(string path, DevToolboxSettingsStore settings, RecentWorkspacesStore recents)
    {
        _path = path;
        _settings = settings;
        _recents = recents;
        Name = "Open in Terminal";
        Icon = DevToolboxVisuals.Terminal;
    }

    public override CommandResult Invoke()
    {
        _recents.Add(_path);
        var s = _settings.Current;
        try
        {
            Launch(s);
        }
        catch (Exception)
        {
            // Fallback to cmd.exe which is always available
            LaunchCmd();
        }

        return CommandResult.Dismiss();
    }

    private void Launch(DevToolboxSettings s)
    {
        switch (s.PreferredTerminal)
        {
            case TerminalChoice.WindowsTerminal:
                LaunchWindowsTerminal();
                break;
            case TerminalChoice.PowerShell:
                LaunchPowerShell();
                break;
            case TerminalChoice.Custom when !string.IsNullOrWhiteSpace(s.CustomTerminalExe):
                Process.Start(new ProcessStartInfo
                {
                    FileName = s.CustomTerminalExe,
                    Arguments = $"\"{_path}\"",
                    UseShellExecute = true,
                });
                break;
            default:
                LaunchCmd();
                break;
        }
    }

    private void LaunchWindowsTerminal()
    {
        // wt.exe -d "<path>"
        Process.Start(new ProcessStartInfo
        {
            FileName = "wt.exe",
            Arguments = $"-d \"{_path}\"",
            UseShellExecute = true,
        });
    }

    private void LaunchPowerShell()
    {
        // Try pwsh (PowerShell 7+) first, fall back to powershell
        string exe = File.Exists(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "PowerShell", "7", "pwsh.exe")) ? "pwsh.exe" : "powershell.exe";

        Process.Start(new ProcessStartInfo
        {
            FileName = exe,
            Arguments = $"-NoExit -Command \"Set-Location '{_path.Replace("'", "''")}' \"",
            UseShellExecute = false,
        });
    }

    private void LaunchCmd()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/K \"cd /d \"{_path}\"\"",
            UseShellExecute = false,
        });
    }
}
