using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.CommandPalette.Extensions;
using NpuTools.Awake.Services;

namespace NpuTools.Awake;

[Guid("ce72eeb3-b491-4af7-9ced-b1e8e8fb03df")]
public sealed partial class NpuAwakeExtension : IExtension, IDisposable
{
    private readonly ManualResetEvent _extensionDisposedEvent;
    private readonly NpuAwakeCommandsProvider _provider = new(new AwakeService());

    public NpuAwakeExtension(ManualResetEvent extensionDisposedEvent)
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
