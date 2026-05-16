using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.CommandPalette.Extensions;
using NpuTools.Common;

namespace NpuTools.Organize;

[Guid("2d0ece61-b197-48a0-a64f-622e7c38f04f")]
public sealed partial class NpuOrganizeExtension : IExtension, IDisposable
{
    private static readonly ExtensionDescriptor Descriptor = new(
        "organize",
        "NPU Organize",
        "com.local.nputools.organize",
        "Organize",
        [
            "Rename new screenshots",
            "Dry run screenshot rename",
            "Screenshot watcher",
            "Watcher settings",
            "Dock watcher band",
        ],
        "com.local.nputools.organize.dock",
        "Organize");

    private readonly ManualResetEvent _extensionDisposedEvent;
    private readonly Phase0CommandProvider _provider = new(Descriptor);

    public NpuOrganizeExtension(ManualResetEvent extensionDisposedEvent)
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
