using System;
using System.Diagnostics;
using System.Threading;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SimpleAnalyticsExtension;

#pragma warning disable CA1001  // timer is process-lifetime; extension exits with the host
internal sealed partial class CpuDockPage : ListPage
{
    private readonly CpuService _cpu;
    private readonly ListItem _cpuItem;
    private readonly Timer _timer;

    internal static readonly IconInfo AddBandIcon = new("\uEEA1");

    public CpuDockPage(CpuService cpu)
    {
        _cpu = cpu;

        Id = "com.dziad.simpleanalyticsextension.cpu";
        Name = "CPU";
        Icon = AddBandIcon;

        _cpuItem = new ListItem(new NoOpCommand())
        {
            Title = "CPU",
            Icon = AddBandIcon,
        };

        _timer = new Timer(Refresh, null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5));
    }

    public override IListItem[] GetItems() => [_cpuItem];

    private void Refresh(object? _)
    {
        try
        {
            var pct = _cpu.GetCpuPercent();
            _cpuItem.Title = $"{pct:F0}%";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Simple Analytics CPU refresh failed: {ex}");
            _cpuItem.Title = "CPU";
        }
    }
}
