using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace NpuTools.Common;

public sealed partial class Phase0CommandProvider : CommandProvider
{
    private readonly ICommandItem[] _commands;
    private readonly ICommandItem[]? _dockBands;

    public Phase0CommandProvider(ExtensionDescriptor descriptor)
    {
        Id = descriptor.ProviderId;
        DisplayName = descriptor.DisplayName;
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");

        _commands =
        [
            new CommandItem(new Phase0StatusPage(descriptor))
            {
                Title = descriptor.DisplayName,
                Icon = Icon,
            },
        ];

        _dockBands = descriptor.DockBandCommandId is null || descriptor.DockBandTitle is null
            ? null
            :
            [
                new CommandItem(new Phase0DockCommand(descriptor))
                {
                    Title = descriptor.DockBandTitle,
                    Icon = Icon,
                },
            ];
    }

    public override ICommandItem[] TopLevelCommands() => _commands;

    public override ICommandItem[]? GetDockBands() => _dockBands;
}
