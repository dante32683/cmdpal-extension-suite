using NpuTools.TextTools;

namespace NpuTools.TextTools.Services;

internal sealed class PendingRewriteStore
{
    private readonly object _lock = new();
    private string? _result;
    private string? _input;
    private TextRewriteMode _mode;

    public bool HasPending
    {
        get { lock (_lock) return _result is not null; }
    }

    public void Set(string input, string result, TextRewriteMode mode)
    {
        lock (_lock)
        {
            _input  = input;
            _result = result;
            _mode   = mode;
        }
    }

    public (string Input, string Result, TextRewriteMode Mode)? Peek()
    {
        lock (_lock)
        {
            if (_result is null) return null;
            return (_input!, _result, _mode);
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _input  = null;
            _result = null;
        }
    }
}
