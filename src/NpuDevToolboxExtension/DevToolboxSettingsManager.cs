using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.DevToolbox.Models;
using NpuTools.DevToolbox.Services;

namespace NpuTools.DevToolbox;

internal sealed class DevToolboxSettingsManager : JsonSettingsManager
{
    private const string Namespace = "npuDevToolbox";

    private static string Namespaced(string name) => $"{Namespace}.{name}";

    private readonly DevToolboxSettingsStore _runtimeSettings;
    private readonly ChoiceSetSetting _terminal;
    private readonly ChoiceSetSetting _ide;
    private readonly TextSetting _customTerminalExe;
    private readonly TextSetting _customIdeExe;

    public DevToolboxSettingsManager(DevToolboxSettingsStore runtimeSettings)
    {
        _runtimeSettings = runtimeSettings;
        var current = _runtimeSettings.Current;
        FilePath = DevToolboxPaths.CommandPaletteSettingsPath();

        _terminal = new ChoiceSetSetting(
            Namespaced(nameof(DevToolboxSettings.PreferredTerminal)),
            "Preferred terminal",
            "Terminal application used when opening a workspace.",
            [
                new ChoiceSetSetting.Choice("Windows Terminal", "WindowsTerminal"),
                new ChoiceSetSetting.Choice("PowerShell 7 / 5", "PowerShell"),
                new ChoiceSetSetting.Choice("Command Prompt", "Cmd"),
                new ChoiceSetSetting.Choice("Custom", "Custom"),
            ])
        {
            Value = current.PreferredTerminal.ToString(),
        };

        _ide = new ChoiceSetSetting(
            Namespaced(nameof(DevToolboxSettings.PreferredIde)),
            "Preferred IDE",
            "Code editor used when opening a workspace.",
            [
                new ChoiceSetSetting.Choice("VS Code", "VSCode"),
                new ChoiceSetSetting.Choice("Cursor", "Cursor"),
                new ChoiceSetSetting.Choice("Windsurf", "Windsurf"),
                new ChoiceSetSetting.Choice("Custom", "Custom"),
            ])
        {
            Value = current.PreferredIde.ToString(),
        };

        _customTerminalExe = new TextSetting(
            Namespaced(nameof(DevToolboxSettings.CustomTerminalExe)),
            "Custom terminal executable",
            "Full path or executable name for a custom terminal (used when Preferred terminal is Custom).",
            current.CustomTerminalExe)
        {
            Placeholder = "e.g. alacritty.exe",
        };

        _customIdeExe = new TextSetting(
            Namespaced(nameof(DevToolboxSettings.CustomIdeExe)),
            "Custom IDE executable",
            "Full path or executable name for a custom IDE (used when Preferred IDE is Custom).",
            current.CustomIdeExe)
        {
            Placeholder = "e.g. C:\\Program Files\\MyIDE\\myide.exe",
        };

        Settings.Add(_terminal);
        Settings.Add(_ide);
        Settings.Add(_customTerminalExe);
        Settings.Add(_customIdeExe);

        LoadSettings();
        ApplyToRuntimeSettings();

        Settings.SettingsChanged += (_, _) =>
        {
            SaveSettings();
            ApplyToRuntimeSettings();
        };
    }

    private void ApplyToRuntimeSettings()
    {
        _runtimeSettings.Update(s =>
        {
            s.PreferredTerminal = ParseTerminal(_terminal.Value);
            s.PreferredIde      = ParseIde(_ide.Value);
            s.CustomTerminalExe = _customTerminalExe.Value?.Trim() ?? string.Empty;
            s.CustomIdeExe      = _customIdeExe.Value?.Trim() ?? string.Empty;
        });
    }

    private static TerminalChoice ParseTerminal(string? value) => value switch
    {
        "WindowsTerminal" => TerminalChoice.WindowsTerminal,
        "PowerShell"      => TerminalChoice.PowerShell,
        "Cmd"             => TerminalChoice.Cmd,
        "Custom"          => TerminalChoice.Custom,
        _                 => TerminalChoice.WindowsTerminal,
    };

    private static IdeChoice ParseIde(string? value) => value switch
    {
        "VSCode"    => IdeChoice.VSCode,
        "Cursor"    => IdeChoice.Cursor,
        "Windsurf"  => IdeChoice.Windsurf,
        "Custom"    => IdeChoice.Custom,
        _           => IdeChoice.VSCode,
    };
}
