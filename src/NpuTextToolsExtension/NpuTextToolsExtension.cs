using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.CommandPalette.Extensions;
using NpuTools.Common;

namespace NpuTools.TextTools;

[Guid("58fbf7c0-2f51-4abf-8ac1-855b832aa7af")]
public sealed partial class NpuTextToolsExtension : IExtension, IDisposable
{
    private static readonly ExtensionDescriptor Descriptor = new(
        "text-tools",
        "NPU Text Tools",
        "com.local.nputools.texttools",
        "TextTools",
        [
            "Fix grammar",
            "Make formal",
            "Make concise",
            "Bullet points",
            "Simplify",
            "Custom rewrite",
            "Selected text quick rewrite",
        ],
        IconGlyph: "\uE8D2");

    private readonly ManualResetEvent _extensionDisposedEvent;
    private readonly Phase0CommandProvider _provider = new(Descriptor);

    public NpuTextToolsExtension(ManualResetEvent extensionDisposedEvent)
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
