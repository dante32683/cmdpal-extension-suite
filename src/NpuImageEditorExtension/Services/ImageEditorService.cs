#pragma warning disable CS8305 // Type is for evaluation purposes only (ImageForegroundExtractor)

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.Imaging;
using NpuTools.ImageEditor.Interop;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;
using Windows.Storage.Streams;

namespace NpuTools.ImageEditor.Services;

internal sealed class ImageEditorService
{
    public static async Task<string> RunOcrAsync(string imagePath)
    {
        var file = await StorageFile.GetFileFromPathAsync(imagePath);
        using var stream = await file.OpenAsync(FileAccessMode.Read);
        var decoder = await BitmapDecoder.CreateAsync(stream);
        var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

        var engine = OcrEngine.TryCreateFromUserProfileLanguages()
            ?? OcrEngine.TryCreateFromLanguage(new Windows.Globalization.Language("en-US"))
            ?? throw new InvalidOperationException("OCR engine unavailable.");

        var result = await engine.RecognizeAsync(bitmap);
        return result.Text;
    }

    public static async Task<string> RemoveBackgroundAsync(string imagePath)
    {
        var readyState = ImageForegroundExtractor.GetReadyState();
        if (readyState == AIFeatureReadyState.NotReady)
        {
            var ready = await ImageForegroundExtractor.EnsureReadyAsync();
            if (ready.Status != AIFeatureReadyResultState.Success)
                throw ready.ExtendedError ?? new InvalidOperationException("ImageForegroundExtractor model not available.");
        }

        var file = await StorageFile.GetFileFromPathAsync(imagePath);
        using var inStream = await file.OpenAsync(FileAccessMode.Read);
        var decoder = await BitmapDecoder.CreateAsync(inStream);
        var source = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

        var extractor = await ImageForegroundExtractor.CreateAsync();
        var mask = extractor.GetMaskFromSoftwareBitmap(source);
        var composited = ApplyGray8MaskAsAlpha(source, mask);

        string outputPath = GetOutputPath(imagePath, "_nobg", ".png");
        var folder = await StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(imagePath)!);
        var outputFile = await folder.CreateFileAsync(Path.GetFileName(outputPath), CreationCollisionOption.ReplaceExisting);

        using var outStream = await outputFile.OpenAsync(FileAccessMode.ReadWrite);
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, outStream);
        encoder.SetSoftwareBitmap(composited);
        await encoder.FlushAsync();

        return outputPath;
    }

    public static async Task<string> SuperResolutionAsync(string imagePath, int scaleFactor = 2)
    {
        var readyState = ImageScaler.GetReadyState();
        if (readyState == AIFeatureReadyState.NotReady)
        {
            var ready = await ImageScaler.EnsureReadyAsync();
            if (ready.Status != AIFeatureReadyResultState.Success)
                throw ready.ExtendedError ?? new InvalidOperationException("ImageScaler model not available.");
        }

        var file = await StorageFile.GetFileFromPathAsync(imagePath);
        using var inStream = await file.OpenAsync(FileAccessMode.Read);
        var decoder = await BitmapDecoder.CreateAsync(inStream);
        var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

        var scaler = await ImageScaler.CreateAsync();
        int newW = bitmap.PixelWidth  * scaleFactor;
        int newH = bitmap.PixelHeight * scaleFactor;
        var scaled = scaler.ScaleSoftwareBitmap(bitmap, newW, newH);

        string outputPath = GetOutputPath(imagePath, $"_{scaleFactor}x", ".png");
        var folder = await StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(imagePath)!);
        var outputFile = await folder.CreateFileAsync(Path.GetFileName(outputPath), CreationCollisionOption.ReplaceExisting);

        using var outStream = await outputFile.OpenAsync(FileAccessMode.ReadWrite);
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, outStream);
        encoder.SetSoftwareBitmap(scaled);
        await encoder.FlushAsync();

        return outputPath;
    }

    // mask is Gray8 (1 byte per pixel), source/output are Bgra8 premultiplied
    private static unsafe SoftwareBitmap ApplyGray8MaskAsAlpha(SoftwareBitmap source, SoftwareBitmap mask)
    {
        var output = new SoftwareBitmap(
            BitmapPixelFormat.Bgra8,
            source.PixelWidth,
            source.PixelHeight,
            BitmapAlphaMode.Premultiplied);

        using var srcBuf  = source.LockBuffer(BitmapBufferAccessMode.Read);
        using var maskBuf = mask.LockBuffer(BitmapBufferAccessMode.Read);
        using var dstBuf  = output.LockBuffer(BitmapBufferAccessMode.Write);
        using var srcRef  = srcBuf.CreateReference();
        using var maskRef = maskBuf.CreateReference();
        using var dstRef  = dstBuf.CreateReference();

        ((IMemoryBufferByteAccess)srcRef).GetBuffer(out byte* s, out _);
        ((IMemoryBufferByteAccess)maskRef).GetBuffer(out byte* m, out _);
        ((IMemoryBufferByteAccess)dstRef).GetBuffer(out byte* d, out _);

        int n = source.PixelWidth * source.PixelHeight;
        for (int i = 0; i < n; i++)
        {
            byte a = m[i];                               // Gray8: 1 byte per pixel
            d[i * 4 + 0] = (byte)(s[i * 4 + 0] * a / 255); // B premultiplied
            d[i * 4 + 1] = (byte)(s[i * 4 + 1] * a / 255); // G premultiplied
            d[i * 4 + 2] = (byte)(s[i * 4 + 2] * a / 255); // R premultiplied
            d[i * 4 + 3] = a;                               // A
        }

        return output;
    }

    private static string GetOutputPath(string input, string suffix, string extension)
    {
        string dir  = Path.GetDirectoryName(input)!;
        string stem = Path.GetFileNameWithoutExtension(input);
        return Path.Combine(dir, $"{stem}{suffix}{extension}");
    }
}
