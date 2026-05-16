using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SimpleAnalyticsExtension;

internal sealed partial class SimpleAnalyticsExtensionPage : ListPage
{
    private readonly SettingsManager _settingsManager;

    public SimpleAnalyticsExtensionPage(SettingsManager settingsManager)
    {
        _settingsManager = settingsManager;
        Icon  = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        Title = "Simple Analytics";
        Name  = "Open";
    }

    public override IListItem[] GetItems()
    {
        return [
            new ListItem(new SettingsPage(_settingsManager))
            {
                Title    = "Settings",
                Subtitle = "Enable or disable battery, Wi-Fi, and CPU dock items",
                Icon     = new IconInfo("\uE713"),
            },
        ];
    }
}