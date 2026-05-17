// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace NPUToolsExtension;

public partial class NPUToolsExtensionCommandsProvider : CommandProvider
{
    private static readonly IconInfo ProviderIcon = new("\uEB9F");
    private readonly ICommandItem[] _commands;

    public NPUToolsExtensionCommandsProvider()
    {
        Id = "com.local.nputools";
        DisplayName = "NPU Tools";
        Icon = ProviderIcon;
        _commands = [
            new CommandItem(new NPUToolsExtensionPage()) { Title = DisplayName, Icon = Icon },
        ];
    }

    public override ICommandItem[] TopLevelCommands()
    {
        return _commands;
    }

}
