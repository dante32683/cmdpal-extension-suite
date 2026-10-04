using System;
using System.Diagnostics;
using System.Threading;
using ActionCenterExtension.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace ActionCenterExtension.Bands;

/// <summary>Dock band showing the name of the active virtual desktop.</summary>
internal sealed partial class VirtualDesktopPage : ListPage, IDisposable
{
    private static readonly IconInfo DesktopIcon = new("");
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);

    private readonly VirtualDesktopReader _reader;
    private readonly ListItem _item;
    private readonly Timer _timer;
    private VirtualDesktopInfo _last;

    internal static IconInfo AddBandIcon => DesktopIcon;

    public VirtualDesktopPage(VirtualDesktopReader reader)
    {
        _reader = reader;
        Id = "com.dziad.actioncenterextension.dock.virtualdesktop";
        Name = "Virtual Desktop";
        Icon = DesktopIcon;

        _last = _reader.Read();
        _item = new ListItem(new NoOpCommand()) { Title = DockLabel.PadToCenter(_last.Name) };

        _timer = new Timer(Refresh, null, PollInterval, PollInterval);
    }

    public override IListItem[] GetItems() => [_item];

    public void Dispose() => _timer.Dispose();

    private void Refresh(object? _)
    {
        try
        {
            var info = _reader.Read();
            if (info == _last)
            {
                return;
            }

            _last = info;
            _item.Title = DockLabel.PadToCenter(info.Name);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Virtual desktop band refresh failed: {ex}");
        }
    }
}
