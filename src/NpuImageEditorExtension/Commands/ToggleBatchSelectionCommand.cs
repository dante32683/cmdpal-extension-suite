using System;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace NpuTools.ImageEditor.Commands;

// Toggles whether an image is part of the batch and keeps the picker open so the
// user can keep ticking files. The page owns the actual selection set.
internal sealed partial class ToggleBatchSelectionCommand : InvokableCommand
{
    private readonly string _path;
    private readonly Action<string> _toggle;

    public ToggleBatchSelectionCommand(string path, bool selected, Action<string> toggle)
    {
        _path  = path;
        _toggle = toggle;
        Name   = selected ? "Remove from Batch" : "Add to Batch";
        Icon   = selected ? ImageEditorVisuals.Selected : ImageEditorVisuals.Unselected;
    }

    public override CommandResult Invoke()
    {
        _toggle(_path);
        return CommandResult.KeepOpen();
    }
}
