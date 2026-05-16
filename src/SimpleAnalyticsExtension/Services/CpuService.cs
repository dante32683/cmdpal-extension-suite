using System;

namespace SimpleAnalyticsExtension;

internal sealed class CpuService
{
    private long _prevIdle;
    private long _prevTotal;

    public CpuService()
    {
        // Prime the baseline so the first call returns a real delta
        NativeMethods.GetSystemTimes(out _prevIdle, out var kernel, out var user);
        _prevTotal = kernel + user;
    }

    // Returns CPU usage as a percentage 0-100. Thread-safe for a single caller.
    public double GetCpuPercent()
    {
        NativeMethods.GetSystemTimes(out long idle, out long kernel, out long user);
        long total = kernel + user;

        long deltaIdle = idle - _prevIdle;
        long deltaTotal = total - _prevTotal;

        _prevIdle = idle;
        _prevTotal = total;

        if (deltaTotal <= 0)
            return 0;

        return Math.Clamp((1.0 - (double)deltaIdle / deltaTotal) * 100.0, 0, 100);
    }
}
