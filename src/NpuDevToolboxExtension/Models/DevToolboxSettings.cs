namespace NpuTools.DevToolbox.Models;

public enum TerminalChoice
{
    WindowsTerminal,
    PowerShell,
    Cmd,
    Custom,
}

public enum IdeChoice
{
    VSCode,
    Cursor,
    Windsurf,
    Custom,
}

internal sealed class DevToolboxSettings
{
    public TerminalChoice PreferredTerminal { get; set; } = TerminalChoice.WindowsTerminal;
    public IdeChoice PreferredIde { get; set; } = IdeChoice.VSCode;
    public string CustomTerminalExe { get; set; } = string.Empty;
    public string CustomIdeExe { get; set; } = string.Empty;
}
