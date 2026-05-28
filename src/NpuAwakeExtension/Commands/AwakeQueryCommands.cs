using System.Diagnostics;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.Awake.Models;
using NpuTools.Awake.Services;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace NpuTools.Awake.Commands;

internal sealed partial class AwakeForMinutesCommand : InvokableCommand
{
    private readonly AwakeService _awakeService;
    private readonly int _minutes;

    public AwakeForMinutesCommand(AwakeService awakeService, int minutes)
    {
        _awakeService = awakeService;
        _minutes = minutes;
        Id = $"com.local.nputools.awake.for.{minutes}";
        Name = $"Start for {minutes} minutes";
        Icon = AwakeVisuals.Clock;
    }

    public override CommandResult Invoke()
    {
        bool ok = _awakeService.SetOverride(new AwakeOverride { Mode = "timed", ExpiryEpochSeconds = AwakeTime.FromMinutes(_minutes) });
        return CommandResult.ShowToast(ok ? $"PC will stay awake for {_minutes} minute(s)." : "Awake keeper could not be started.");
    }
}

internal sealed partial class AwakeUntilTimeCommand : InvokableCommand
{
    private readonly AwakeService _awakeService;
    private readonly string _time;

    public AwakeUntilTimeCommand(AwakeService awakeService, string time)
    {
        _awakeService = awakeService;
        _time = time;
        Id = $"com.local.nputools.awake.until.{time.Replace(':', '-')}";
        Name = $"Start until {time}";
        Icon = AwakeVisuals.Calendar;
    }

    public override CommandResult Invoke()
    {
        long? epoch = AwakeTime.ParseUntilToEpoch(_time);
        if (epoch is null)
        {
            return CommandResult.ShowToast("Use HH:mm, like 17:30.");
        }

        bool ok = _awakeService.SetOverride(new AwakeOverride { Mode = "until", ExpiryEpochSeconds = epoch });
        return CommandResult.ShowToast(ok ? $"PC will stay awake until {_time}." : "Awake keeper could not be started.");
    }
}

internal sealed partial class SmartAwakeQueryCommand : InvokableCommand
{
    private readonly AwakeService _awakeService;
    private readonly string _query;
    private readonly SmartAwakeService _smartAwakeService = new();

    public SmartAwakeQueryCommand(AwakeService awakeService, string query)
    {
        _awakeService = awakeService;
        _query = query;
        Id = $"com.local.nputools.awake.smart.query.{Math.Abs(query.GetHashCode())}";
        Name = string.IsNullOrWhiteSpace(query) ? "Run Smart Awake" : $"Run: {query}";
        Icon = AwakeVisuals.Sparkle;
    }

    public override CommandResult Invoke()
    {
        if (string.IsNullOrWhiteSpace(_query))
        {
            return CommandResult.ShowToast("Type a Smart Awake request first.");
        }

        _ = Task.Run(async () =>
        {
            try
            {
                var result = await _smartAwakeService.ExecuteAsync(_query, _awakeService);
                ShowToast("Smart Awake", result.Message);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SmartAwakeQueryCommand failed: {ex.GetType().Name}: {ex.Message}");
                ShowToast("Smart Awake Failed", ex.Message.Length > 100 ? ex.Message[..100] : ex.Message);
            }
        });
        return CommandResult.Dismiss();
    }

    private static void ShowToast(string title, string message)
    {
        try
        {
            string xml = $"<toast><visual><binding template=\"ToastGeneric\"><text>{EscapeXml(title)}</text><text>{EscapeXml(message)}</text></binding></visual></toast>";
            var doc = new XmlDocument();
            doc.LoadXml(xml);
            ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(doc));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ShowToast failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static string EscapeXml(string text) =>
        text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}
