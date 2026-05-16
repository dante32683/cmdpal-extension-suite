using System;
using System.Threading;
using Windows.Devices.Power;

namespace SimpleAnalyticsExtension;

internal sealed class BatteryInfo
{
    public int Percent { get; init; }
    public bool HasBattery { get; init; }
    public bool IsPluggedIn { get; init; }
    public bool IsCharging { get; init; }
    public bool IsCalculating { get; init; }
    public bool EnergySaverOn { get; init; }
    // Positive = charging in watts, negative = discharging in watts (15-second rolling average)
    public double ChargeRateWatts { get; init; }
    public double RemainingWh { get; init; }
    public double FullChargeWh { get; init; }
    public TimeSpan? TimeRemaining { get; init; }
}

#pragma warning disable CA1001  // timer is process-lifetime — extension exits with the host
internal sealed class BatteryService
{
    // 15-second rolling average: 3 samples × 5 s each
    private const int SampleCount = 3;
    private readonly double[] _wattBuffer = new double[SampleCount];
    private int _wattIdx    = 0;
    private int _wattFilled = 0;
    private bool _wattAvailable = false;   // false until first WinRT reading succeeds
    private readonly object _lock = new();
    private readonly Timer _sampleTimer;

    public BatteryService()
    {
        // Sample immediately, then every 5 s
        _sampleTimer = new Timer(SampleWattage, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
    }

    private void SampleWattage(object? _)
    {
        try
        {
            var report = Battery.AggregateBattery.GetReport();
            if (report.ChargeRateInMilliwatts is not { } mw) return;

            lock (_lock)
            {
                _wattBuffer[_wattIdx] = mw / 1000.0;
                _wattIdx = (_wattIdx + 1) % SampleCount;
                if (_wattFilled < SampleCount) _wattFilled++;
                _wattAvailable = true;
            }
        }
        catch { /* WinRT unavailable */ }
    }

    private (double watts, bool available) GetAverageWatts()
    {
        lock (_lock)
        {
            if (!_wattAvailable || _wattFilled == 0) return (0, false);
            double sum = 0;
            for (int i = 0; i < _wattFilled; i++)
                sum += _wattBuffer[i];
            return (sum / _wattFilled, true);
        }
    }

    public BatteryInfo GetBatteryInfo()
    {
        NativeMethods.GetSystemPowerStatus(out var status);

        var hasBattery    = (status.BatteryFlag & 0x80) == 0 && status.BatteryLifePercent != 255;
        var percent       = hasBattery ? (int)status.BatteryLifePercent : 0;
        var isPluggedIn   = status.ACLineStatus == 1;
        var isCharging    = (status.BatteryFlag & 0x08) != 0;
        var energySaverOn = (status.SystemStatusFlag & 0x01) != 0;

        TimeSpan? timeRemaining = null;
        if (!isPluggedIn && status.BatteryLifeTime != 0xFFFFFFFF && status.BatteryLifeTime > 0)
            timeRemaining = TimeSpan.FromSeconds(status.BatteryLifeTime);

        var (chargeRateWatts, wattAvailable) = GetAverageWatts();
        var isCalculating = !wattAvailable;

        double remainingWh  = 0;
        double fullChargeWh = 0;
        try
        {
            var report = Battery.AggregateBattery.GetReport();
            if (report.RemainingCapacityInMilliwattHours is { } rem)
                remainingWh = rem / 1000.0;
            if (report.FullChargeCapacityInMilliwattHours is { } full)
                fullChargeWh = full / 1000.0;
        }
        catch { /* WinRT battery unavailable (desktop without battery) */ }

        return new BatteryInfo
        {
            Percent         = percent,
            HasBattery      = hasBattery,
            IsPluggedIn     = isPluggedIn,
            IsCharging      = isCharging,
            IsCalculating   = isCalculating,
            EnergySaverOn   = energySaverOn,
            ChargeRateWatts = chargeRateWatts,
            RemainingWh     = remainingWh,
            FullChargeWh    = fullChargeWh,
            TimeRemaining   = timeRemaining,
        };
    }
}
