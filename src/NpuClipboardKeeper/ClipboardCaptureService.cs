using System.Security.Cryptography;
using System.Threading;
using NpuTools.Clipboard.Data;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;
using Windows.Storage.Streams;

namespace NpuClipboardKeeper;

internal sealed class ClipboardCaptureService
{
    private readonly ClipboardStore _store;
    private readonly ClipboardSettingsStore _settings;

    public ClipboardCaptureService(ClipboardStore store, ClipboardSettingsStore settings)
    {
        _store = store;
        _settings = settings;
    }

    public Task<CaptureResult> TryCaptureCurrentAsync()
    {
        var tcs = new TaskCompletionSource<CaptureResult>();
        Thread thread = new(() =>
        {
            try
            {
                var task = CaptureCurrentAsync();
                if (!task.IsCompleted)
                {
                    uint threadId = NativeMethods.GetCurrentThreadId();
                    task.ContinueWith(t =>
                    {
                        _ = NativeMethods.PostThreadMessage(threadId, NativeMethods.WM_QUIT, 0, 0);
                    }, TaskScheduler.Default);

                    while (NativeMethods.GetMessage(out var msg, 0, 0, 0))
                    {
                        _ = NativeMethods.TranslateMessage(ref msg);
                        _ = NativeMethods.DispatchMessage(ref msg);
                    }
                }
                tcs.SetResult(task.GetAwaiter().GetResult());
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return tcs.Task;
    }

    private async Task<CaptureResult> CaptureCurrentAsync()
    {
        var settings = _settings.Current;
        if (!settings.RecorderEnabled)
            return new(false, "recorder disabled");

        string sourceApp = NativeMethods.ForegroundApplication();
        if (settings.DisabledApplicationNames.Any(name => sourceApp.Contains(name, StringComparison.OrdinalIgnoreCase)))
            return new(false, $"disabled application: {sourceApp}");

        DataPackageView content = Clipboard.GetContent();
        DateTimeOffset now = DateTimeOffset.Now;

        if (content.Contains(StandardDataFormats.StorageItems))
        {
            var storageItems = await content.GetStorageItemsAsync();
            var paths = storageItems.OfType<StorageFile>().Select(f => f.Path).Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
            if (paths.Count > 0)
            {
                string key = string.Join("\n", paths.Order(StringComparer.OrdinalIgnoreCase));
                var entry = BuildBase(ClipboardEntryKind.Files, now, sourceApp, ClipboardStore.BuildHash("files", key));
                entry.FilePaths = paths;
                entry.Title = ClipboardClassifier.BuildTitle(ClipboardEntryKind.Files, null, paths.Count);
                _store.AddOrPromote(entry, settings);
                return new(true, entry.Title);
            }
        }

        if (content.Contains(StandardDataFormats.Bitmap))
        {
            var image = await SaveBitmapAsync(content, now).ConfigureAwait(false);
            string ocr = await TryOcrAsync(image.Path).ConfigureAwait(false);
            string hash = await HashFileAsync(image.Path).ConfigureAwait(false);
            var entry = BuildBase(ClipboardEntryKind.Image, now, sourceApp, ClipboardStore.BuildHash("image", hash));
            entry.ImagePath = image.Path;
            entry.OcrText = ocr;
            entry.Title = $"Image ({image.Width}x{image.Height})";
            _store.AddOrPromote(entry, settings);
            return new(true, entry.Title);
        }

        if (content.Contains(StandardDataFormats.Text))
        {
            string text = await content.GetTextAsync();
            if (string.IsNullOrWhiteSpace(text))
                return new(false, "empty text");

            ClipboardEntryKind kind = ClipboardClassifier.ClassifyText(text);
            var entry = BuildBase(kind, now, sourceApp, ClipboardStore.BuildHash("text", text));
            entry.Text = text;
            entry.Title = ClipboardClassifier.BuildTitle(kind, text, 0);
            _store.AddOrPromote(entry, settings);
            ClipboardSyncService.WriteEntry(entry, settings.SyncFolder);
            return new(true, entry.Title);
        }

        return new(false, "unsupported clipboard format");
    }

    private ClipboardEntry BuildBase(ClipboardEntryKind kind, DateTimeOffset now, string sourceApp, string hash) => new()
    {
        Id = "clip_" + now.ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture) + "_" + Guid.NewGuid().ToString("N")[..8],
        GroupId = _store.AllocateGroupId(kind, now),
        Kind = kind,
        CreatedAt = now,
        SourceApplication = sourceApp,
        ContentHash = hash,
        SourceDevice = Environment.MachineName,
    };

    private static async Task<CapturedImage> SaveBitmapAsync(DataPackageView content, DateTimeOffset now)
    {
        Directory.CreateDirectory(ClipboardPaths.BlobDir());
        string path = Path.Combine(ClipboardPaths.BlobDir(), $"clip_{now:yyyyMMdd_HHmmss_fff}.png");
        RandomAccessStreamReference reference = await content.GetBitmapAsync();
        using IRandomAccessStream source = await reference.OpenReadAsync();
        var decoder = await BitmapDecoder.CreateAsync(source);
        var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

        StorageFolder folder = await StorageFolder.GetFolderFromPathAsync(ClipboardPaths.BlobDir());
        StorageFile file = await folder.CreateFileAsync(Path.GetFileName(path), CreationCollisionOption.ReplaceExisting);
        using IRandomAccessStream output = await file.OpenAsync(FileAccessMode.ReadWrite);
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, output);
        encoder.SetSoftwareBitmap(bitmap);
        await encoder.FlushAsync();
        return new CapturedImage(path, decoder.PixelWidth, decoder.PixelHeight);
    }

    private static async Task<string> TryOcrAsync(string imagePath)
    {
        try
        {
            StorageFile file = await StorageFile.GetFileFromPathAsync(imagePath);
            using IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.Read);
            var decoder = await BitmapDecoder.CreateAsync(stream);
            var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
            var engine = OcrEngine.TryCreateFromUserProfileLanguages()
                ?? OcrEngine.TryCreateFromLanguage(new Windows.Globalization.Language("en-US"));
            if (engine is null)
                return string.Empty;
            var result = await engine.RecognizeAsync(bitmap);
            return result.Text.Trim();
        }
        catch
        {
            return string.Empty;
        }
    }

    private static async Task<string> HashFileAsync(string path)
    {
        await using var stream = File.OpenRead(path);
        byte[] hash = await SHA256.HashDataAsync(stream);
        return Convert.ToHexString(hash);
    }
}

internal readonly record struct CaptureResult(bool Captured, string Message);

internal readonly record struct CapturedImage(string Path, uint Width, uint Height);
