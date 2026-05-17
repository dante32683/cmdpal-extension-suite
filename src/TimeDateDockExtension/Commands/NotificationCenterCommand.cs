using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using TimeDateDockExtension.Services;

namespace TimeDateDockExtension.Commands;

internal sealed partial class NotificationCenterCommand : InvokableCommand
{
    private readonly NotificationCenterService _notificationCenter;

    public NotificationCenterCommand(string id, string name, string? icon, NotificationCenterService notificationCenter)
    {
        _notificationCenter = notificationCenter;
        Id = id;
        Name = name;
        if (icon is not null)
        {
            Icon = new IconInfo(icon);
        }
    }

    public override CommandResult Invoke()
    {
        _notificationCenter.Toggle();
        return CommandResult.KeepOpen();
    }
}
