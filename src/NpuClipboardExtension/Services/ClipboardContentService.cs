using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.ComponentModel;
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
            case ClipboardEntryKind.Image:
                throw new FileNotFoundException("The saved clipboard image is no longer available.", entry.ImagePath);
            case ClipboardEntryKind.Files when entry.FilePaths.Count > 0:
                await SetFilesAsync(entry.FilePaths).ConfigureAwait(false);
                break;
            case ClipboardEntryKind.Files:
                throw new InvalidOperationException("The clipboard entry does not contain any files.");
            default:
                SetText(entry.Text ?? entry.OcrText ?? entry.DisplayName);
                break;
        }
    }

    public async Task PasteAsync(ClipboardEntry entry, bool plainTextOnly, int delayMs, nint paletteWindow)
    {
        await CopyAsync(entry, plainTextOnly).ConfigureAwait(false);
        await WaitForPasteTargetAsync(paletteWindow, delayMs).ConfigureAwait(false);
        SendPaste();
    }

    public static nint CaptureForegroundWindow() => User32.GetForegroundWindow();

    private static async Task WaitForPasteTargetAsync(nint paletteWindow, int delayMs)
    {
        await Task.Delay(Math.Clamp(delayMs, 50, 1000)).ConfigureAwait(false);

        if (paletteWindow == 0)
            return;

        long deadline = Environment.TickCount64 + 1500;
        nint foreground;
        do
        {
            foreground = User32.GetForegroundWindow();
            if (foreground != 0 && foreground != paletteWindow)
            {
                // Give the restored application one scheduler turn to settle its focused control.
                await Task.Delay(50).ConfigureAwait(false);
                return;
            }

            await Task.Delay(25).ConfigureAwait(false);
        }
        while (Environment.TickCount64 < deadline);

        throw new InvalidOperationException("Command Palette did not release focus to a paste target.");
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
            throw new FileNotFoundException("None of the files in this clipboard entry are still available.");

        DataPackage package = new();
        package.SetStorageItems(files);
        SetClipboard(package);
    }

    private static void SetClipboard(DataPackage package)
    {
        RunSta(() =>
        {
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
            COMException? lastError = null;
            for (int attempt = 1; attempt <= 5; attempt++)
            {
                try
                {
                    Windows.ApplicationModel.DataTransfer.Clipboard.Flush();
                    return;
                }
                catch (COMException ex)
                {
                    lastError = ex;
                    Debug.WriteLine($"Clipboard Flush attempt {attempt} failed: {ex.HResult}: {ex.Message}");
                    if (attempt < 5)
                        Thread.Sleep(attempt * 15);
                }
            }

            throw new InvalidOperationException("Windows did not accept ownership of the clipboard data.", lastError);
        });
    }

    private static void SendPaste()
    {
        User32.INPUT[] inputs = PasteInputSequence.Create();
        uint inserted = User32.SendInput((uint)inputs.Length, ref inputs[0], Marshal.SizeOf<User32.INPUT>());
        if (inserted != inputs.Length)
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"SendInput inserted {inserted} of {inputs.Length} paste keystrokes.");
    }

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
