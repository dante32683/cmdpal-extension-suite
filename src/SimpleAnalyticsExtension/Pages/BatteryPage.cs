using System;
using System.Collections.Generic;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SimpleAnalyticsExtension;

internal sealed partial class BatteryPage : ListPage
{
    private readonly BatteryService _service;

    // Segoe Fluent Icons -- MobBattery0-10: EBA0-EBAA | MobBatteryCharging0-10: EBAB-EBB5 | MobBatterySaver0-10: EBB6-EBC0
    private const string IconLightning = "\uE945";   // LightningBolt
    private const string IconClock     = "\uE917";   // Clock

    // Semantic colors matching MenuBar app (SystemFillColor* approximate values)
    private static readonly Color GreenColor  = new Color { R = 108, G = 203, B = 95,  A = 255 }; // SystemFillColorSuccess
    private static readonly Color YellowColor = new Color { R = 255, G = 192, B = 0,   A = 255 }; // SystemFillColorCaution
    private static readonly Color RedColor    = new Color { R = 255, G = 95,  B = 95,  A = 255 }; // SystemFillColorCritical

    private static OptionalColor Colored(Color c) =>
        new OptionalColor { HasValue = true, Color = c };

    public BatteryPage(BatteryService service)
    {
        _service = service;
        Id = "com.dziad.simpleanalyticsextension.battery";
        Icon  = new IconInfo("\uEBA0");
        Title = "Battery";
        Name  = "Battery Details";
    }

    public override IListItem[] GetItems()
    {
        var info = _service.GetBatteryInfo();

        if (!info.HasBattery)
            return [Row("Battery", "No battery detected", "\uEBA0", default, null)];

        var level        = Math.Clamp(info.Percent / 10, 0, 10);
        var batteryIcon  = ((char)(0xEBA0 + level)).ToString();
        var chargingIcon = ((char)(0xEBAB + level)).ToString();
        var saverIcon    = ((char)(0xEBB6 + level)).ToString();

        OptionalColor chargeColor;
        string statusIcon;

        if (info.IsCharging)
        {
            chargeColor = Colored(GreenColor);
            statusIcon  = chargingIcon;
        }
        else if (info.IsPluggedIn && info.Percent < 99)
        {
            chargeColor = Colored(GreenColor);
            statusIcon  = batteryIcon;
        }
        else if (info.IsPluggedIn)
        {
            chargeColor = default;
            statusIcon  = batteryIcon;
        }
        else if (info.EnergySaverOn || info.Percent <= 20)
        {
            chargeColor = Colored(YellowColor);
            statusIcon  = saverIcon;
        }
        else
        {
            chargeColor = default;
            statusIcon  = batteryIcon;
        }

        // Page accent tints the flyout header
        AccentColor = chargeColor;

        var rows = new List<IListItem>
        {
            Row("Charge", $"{info.Percent}%", statusIcon, chargeColor, StatusText(info)),
        };

        if (info.IsCalculating)
        {
            rows.Add(Row("Power", "Calculating...", IconLightning, default, null));
        }
        else if (info.ChargeRateWatts != 0)
        {
            var watts = Math.Abs(info.ChargeRateWatts);
            OptionalColor wattColor;
            string wattLabel;
            if (info.ChargeRateWatts > 0)
            {
                wattColor = Colored(GreenColor); wattLabel = "+";
            }
            else if (watts <= 9.0)
            {
                wattColor = default; wattLabel = "Low";
            }
            else if (watts <= 15.0)
            {
                wattColor = Colored(YellowColor); wattLabel = "Med";
            }
            else
            {
                wattColor = Colored(RedColor); wattLabel = "High";
            }
            var sign = info.ChargeRateWatts > 0 ? "+" : string.Empty;
            rows.Add(Row("Power", $"{sign}{info.ChargeRateWatts:F1} W", IconLightning, wattColor, wattLabel));
        }

        if (info.TimeRemaining.HasValue)
        {
            var t = info.TimeRemaining.Value;
            var timeStr = t.TotalHours >= 1
                ? $"{(int)t.TotalHours}h {t.Minutes}m remaining"
                : $"{t.Minutes}m remaining";
            rows.Add(Row("Time Remaining", timeStr, IconClock, default, null));
        }
        else if (!info.IsPluggedIn)
        {
            rows.Add(Row("Time Remaining", "Calculating...", IconClock, default, null));
        }

        if (info.FullChargeWh > 0)
            rows.Add(Row("Capacity", $"{info.RemainingWh:F1} / {info.FullChargeWh:F1} Wh", IconLightning, default, null));

        if (info.EnergySaverOn)
            rows.Add(Row("Battery Saver", "On", saverIcon, Colored(YellowColor), "Saver"));

        return [.. rows];
    }

    private static string StatusText(BatteryInfo info)
    {
        if (info.IsCharging)                        return "Charging";
        if (info.IsPluggedIn && info.Percent >= 99) return "Plugged in, fully charged";
        if (info.IsPluggedIn)                       return "Smart charging";
        return "On battery";
    }

    private static ListItem Row(string title, string subtitle, string iconCode,
                                OptionalColor tagColor, string? tagText)
    {
        var item = new ListItem(new NoOpCommand())
        {
            Title    = title,
            Subtitle = subtitle,
            Icon     = new IconInfo(iconCode),
        };

        if (tagText is not null)
        {
            item.Tags = [new Tag { Text = tagText, Foreground = tagColor }];
        }

        return item;
    }
}
