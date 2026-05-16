using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.CommandPalette.Extensions;
using NpuTools.Common;

namespace NpuTools.DevToolbox;

[Guid("f9bfb2c8-9aa7-4247-b64c-d2381c892e40")]
public sealed partial class NpuDevToolboxExtension : IExtension, IDisposable
{
    private static readonly ExtensionDescriptor Descriptor = new(
        "dev-toolbox",
        "NPU Dev Toolbox",
        "com.local.nputools.devtoolbox",
        "DevToolbox",
        [
            "Open workspace in Explorer",
            "Open workspace in terminal",
            "Open workspace in IDE",
            "Workspace detection settings",
        ]);

    private readonly ManualResetEvent _extensionDisposedEvent;
    private readonly Phase0CommandProvider _provider = new(Descriptor);

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
