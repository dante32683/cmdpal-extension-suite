using Microsoft.CommandPalette.Extensions.Toolkit;

namespace NpuTools.TextTools.Commands;

internal sealed partial class CopyResultCommand : InvokableCommand
{
    private readonly string _text;

    public CopyResultCommand(string text)
    {
        _text = text;
        Name = "Copy to Clipboard";
        Icon = TextToolsVisuals.Copy;
    }

    public override CommandResult Invoke()
    {
        ClipboardHelper.SetText(_text);
        return CommandResult.Dismiss();
    }
}
