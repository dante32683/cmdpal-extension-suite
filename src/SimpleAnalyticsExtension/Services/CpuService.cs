using System;
using System.Threading;

namespace SimpleAnalyticsExtension;

#pragma warning disable CA1001  // timer is process-lifetime — extension exits with the host
internal sealed class CpuService
{
    private const int SampleCount = 3;
    private readonly double[] _cpuBuffer = new double[SampleCount];
    private int _cpuIdx;
    private int _cpuFilled;
    private bool _cpuAvailable;
    private readonly object _lock = new();
    private readonly Timer _sampleTimer;

    private long _prevIdle;
    private long _prevTotal;

    public CpuService()
    {
        // Prime the baseline
        NativeMethods.GetSystemTimes(out _prevIdle, out var kernel, out var user);
        _prevTotal = kernel + user;

        // Sample immediately, then every 5 s
        _sampleTimer = new Timer(SampleCpu, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
    }

    private void SampleCpu(object? _)
    {
        try
        {
            NativeMethods.GetSystemTimes(out long idle, out long kernel, out long user);
            long total = kernel + user;

            long deltaIdle = idle - _prevIdle;
            long deltaTotal = total - _prevTotal;

            _prevIdle = idle;
            _prevTotal = total;

            double usage = 0;
            if (deltaTotal > 0)
            {
                usage = Math.Clamp((1.0 - (double)deltaIdle / deltaTotal) * 100.0, 0, 100);
            }

            lock (_lock)
            {
                _cpuBuffer[_cpuIdx] = usage;
                _cpuIdx = (_cpuIdx + 1) % SampleCount;
                if (_cpuFilled < SampleCount) _cpuFilled++;
                _cpuAvailable = true;
            }
        }
        catch { }
    }

    public double GetCpuPercent()
    {
        lock (_lock)
        {
            if (!_cpuAvailable || _cpuFilled == 0) return 0;
            double sum = 0;
            for (int i = 0; i < _cpuFilled; i++)
                sum += _cpuBuffer[i];
            return sum / _cpuFilled;
        }
    }
}
