using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Streams;

namespace NpuTools.Organize.Commands;

internal sealed partial class CopyImageToClipboardCommand : InvokableCommand
{
    private readonly string _path;

    public CopyImageToClipboardCommand(string path)
    {
        _path = path;
        Name  = "Copy Image";
        Icon  = OrganizeVisuals.Copy;
    }

    public override CommandResult Invoke()
    {
        _ = Task.Run(CopyAsync);
        return CommandResult.ShowToast("Image copied to clipboard");
    }

    private async Task CopyAsync()
    {
        try
        {
            var file = await StorageFile.GetFileFromPathAsync(_path);
            var dp   = new DataPackage();
            dp.SetBitmap(RandomAccessStreamReference.CreateFromFile(file));
            Clipboard.SetContent(dp);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"CopyImageToClipboardCommand failed: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
