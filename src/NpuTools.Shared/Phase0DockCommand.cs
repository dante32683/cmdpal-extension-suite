using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace NpuTools.Common;

internal sealed partial class Phase0DockCommand : InvokableCommand
{
    public Phase0DockCommand(ExtensionDescriptor descriptor)
    {
        Id = descriptor.DockBandCommandId ?? $"{descriptor.ProviderId}.dock";
        Name = descriptor.DockBandTitle ?? descriptor.DisplayName;
        Icon = new IconInfo(descriptor.IconGlyph);
    }

    public override CommandResult Invoke()
    {
        return CommandResult.KeepOpen();
    }
}
