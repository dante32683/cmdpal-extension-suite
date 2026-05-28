using System;

namespace NpuTools.TextTools.Services;

internal enum CaptureStatus
{
    None,
    Success,
    NoTextCaptured,
    EmptyRewrite,
    Error,
}

internal sealed class CaptureDiagnosticsStore
{
    private readonly object _lock = new();

    private DateTimeOffset _lastAttemptTime;
    private CaptureStatus _lastStatus = CaptureStatus.None;
    private string? _lastCapturedText;
    private string? _lastFailureReason;

    public void RecordSuccess(string capturedText)
    {
        lock (_lock)
        {
            _lastAttemptTime  = DateTimeOffset.Now;
            _lastStatus       = CaptureStatus.Success;
            _lastCapturedText = capturedText;
            _lastFailureReason = null;
        }
    }

    public void RecordFailure(CaptureStatus status, string reason)
    {
        lock (_lock)
        {
            _lastAttemptTime   = DateTimeOffset.Now;
            _lastStatus        = status;
            _lastCapturedText  = null;
            _lastFailureReason = reason;
        }
    }

    public (DateTimeOffset Time, CaptureStatus Status, string? CapturedText, string? FailureReason)? GetLast()
    {
        lock (_lock)
        {
            if (_lastStatus == CaptureStatus.None) return null;
            return (_lastAttemptTime, _lastStatus, _lastCapturedText, _lastFailureReason);
        }
    }
}
