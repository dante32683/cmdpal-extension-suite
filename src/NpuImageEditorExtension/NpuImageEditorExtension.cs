using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.CommandPalette.Extensions;
using NpuTools.Common;

namespace NpuTools.ImageEditor;

[Guid("eded23a0-a423-4452-b522-1b169e72a751")]
public sealed partial class NpuImageEditorExtension : IExtension, IDisposable
{
    private static readonly ExtensionDescriptor Descriptor = new(
        "image-editor",
        "NPU Image Editor",
        "com.local.nputools.imageeditor",
        "ImageEditor",
        [
            "Remove background",
            "Super resolution",
            "OCR",
            "Image input settings",
        ]);

    private readonly ManualResetEvent _extensionDisposedEvent;
    private readonly Phase0CommandProvider _provider = new(Descriptor);

    public NpuImageEditorExtension(ManualResetEvent extensionDisposedEvent)
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
