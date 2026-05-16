using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.Awake.Services;

namespace NpuTools.Awake.Commands;

internal sealed partial class AwakeFallbackCommandItem : FallbackCommandItem
{
    private readonly AwakeService _awakeService;

    public AwakeFallbackCommandItem(AwakeService awakeService)
        : base(new SmartAwakeQueryCommand(awakeService, ""), "Smart Awake", "com.local.nputools.awake.fallback.smart")
    {
        _awakeService = awakeService;
        Title = "Smart Awake";
        Subtitle = "Type a duration, time, schedule, status, or stop request";
        Icon = AwakeVisuals.Sparkle;
    }

    public override void UpdateQuery(string query)
    {
        string trimmed = query.Trim();
        Command = new SmartAwakeQueryCommand(_awakeService, trimmed);
        Title = string.IsNullOrWhiteSpace(trimmed) ? "Smart Awake" : $"Smart Awake: {trimmed}";
        Subtitle = "Press Enter to run this as an Awake request";
        Icon = AwakeVisuals.Sparkle;
    }
}
