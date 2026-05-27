using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.CommandPalette.Extensions;

namespace NpuTools.DevToolbox;

[Guid("f9bfb2c8-9aa7-4247-b64c-d2381c892e40")]
public sealed partial class NpuDevToolboxExtension : IExtension, IDisposable
{
    private readonly ManualResetEvent _extensionDisposedEvent;
    private readonly NpuDevToolboxCommandsProvider _provider = new();

    public NpuDevToolboxExtension(ManualResetEvent extensionDisposedEvent)
    {
        _extensionDisposedEvent = extensionDisposedEvent;
    }

    public object? GetProvider(ProviderType providerType)
    {
        return providerType switch
        {
            ProviderType.Commands => _provider,
            _ => null,
        };
    }

    public void Dispose() => _extensionDisposedEvent.Set();
}
