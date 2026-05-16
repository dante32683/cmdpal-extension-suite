// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ActionCenterExtension.Pages;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace ActionCenterExtension;

internal sealed partial class ActionCenterExtensionPage : ListPage
{
    private readonly SettingsManager _settingsManager;

    public ActionCenterExtensionPage(SettingsManager settingsManager)
    {
        _settingsManager = settingsManager;
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        Title = "Action Center";
        Name = "Open";
    }

    public override IListItem[] GetItems()
    {
        return [
            new ListItem(new SettingsPage(_settingsManager))
            {
                Title = "Settings",
                Subtitle = "Configure Action Center options",
                Icon = new IconInfo("\uE713"),
            },
        ];
    }
}
