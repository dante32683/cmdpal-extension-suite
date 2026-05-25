using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.CommandPalette.Extensions;

namespace NpuTools.Notes;

[Guid("4a272922-fdd8-42c6-92c1-f2e8a31cd37d")]
public sealed partial class NpuNotesExtension : IExtension, IDisposable
{
    private readonly ManualResetEvent _extensionDisposedEvent;
    private readonly NpuNotesCommandsProvider _provider = new();

    public NpuNotesExtension(ManualResetEvent extensionDisposedEvent)
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
