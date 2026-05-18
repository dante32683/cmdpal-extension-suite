using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using NpuTools.Clipboard.Interop;
using NpuTools.Clipboard.Data;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Streams;

namespace NpuTools.Clipboard.Services;

internal sealed class ClipboardContentService
{
    [SuppressMessage("Performance", "CA1822", Justification = "Service method — uniform call site via injection.")]
    public async Task CopyAsync(ClipboardEntry entry, bool plainTextOnly)
    {
        if (plainTextOnly && !string.IsNullOrEmpty(entry.Text))
        {
            SetText(entry.Text);
            return;
        }

        switch (entry.Kind)
        {
            case ClipboardEntryKind.Image when !string.IsNullOrWhiteSpace(entry.ImagePath) && File.Exists(entry.ImagePath):
                await SetImageAsync(entry.ImagePath).ConfigureAwait(false);
                break;
            case ClipboardEntryKind.Files when entry.FilePaths.Count > 0:
                await SetFilesAsync(entry.FilePaths).ConfigureAwait(false);
                break;
            default:
                SetText(entry.Text ?? entry.OcrText ?? entry.DisplayName);
                break;
        }
    }

    public async Task PasteAsync(ClipboardEntry entry, bool plainTextOnly, int delayMs)
    {
        await CopyAsync(entry, plainTextOnly).ConfigureAwait(false);
        _ = Task.Run(async () =>
        {
            await Task.Delay(Math.Max(50, delayMs)).ConfigureAwait(false);
            SendPaste();
        });
    }

    private static void SetText(string text)
    {
        DataPackage package = new();
        package.SetText(text);
        SetClipboard(package);
    }

    private static async Task SetImageAsync(string imagePath)
    {
        StorageFile file = await StorageFile.GetFileFromPathAsync(imagePath);
        DataPackage package = new();
        package.SetBitmap(RandomAccessStreamReference.CreateFromFile(file));
        SetClipboard(package);
    }

    private static async Task SetFilesAsync(System.Collections.Generic.IReadOnlyList<string> paths)
    {
        var files = new System.Collections.Generic.List<IStorageItem>();
        foreach (string path in paths.Where(File.Exists))
            files.Add(await StorageFile.GetFileFromPathAsync(path));
        if (files.Count == 0)
            return;

        DataPackage package = new();
        package.SetStorageItems(files);
        SetClipboard(package);
    }

    private static void SetClipboard(DataPackage package)
    {
        RunSta(() =>
        {
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
            try { Windows.ApplicationModel.DataTransfer.Clipboard.Flush(); }
            catch (COMException ex) { Debug.WriteLine($"Clipboard Flush failed: {ex.HResult}: {ex.Message}"); }
        });
    }

    private static void SendPaste()
    {
        var inputs = new User32.INPUT[4];
        inputs[0] = Key(User32.VK_CONTROL, 0);
        inputs[1] = Key(User32.VK_V, 0);
        inputs[2] = Key(User32.VK_V, User32.KEYEVENTF_KEYUP);
        inputs[3] = Key(User32.VK_CONTROL, User32.KEYEVENTF_KEYUP);
        User32.SendInput((uint)inputs.Length, ref inputs[0], Marshal.SizeOf<User32.INPUT>());
    }

    private static User32.INPUT Key(ushort vk, uint flags) => new()
    {
        type = User32.INPUT_KEYBOARD,
        U = new User32.INPUTUNION { ki = new User32.KEYBDINPUT { wVk = vk, dwFlags = flags } },
    };

    private static void RunSta(Action action)
    {
        Exception? error = null;
        Thread thread = new(() =>
        {
            try { action(); }
            catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error is not null)
            throw error;
    }
}
