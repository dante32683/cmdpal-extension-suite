#pragma warning disable CS8305 // Type is for evaluation purposes only (ImageForegroundExtractor)

using System;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.Imaging;
using Windows.ApplicationModel.DataTransfer;
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
    private static SoftwareBitmap ApplyGray8MaskAsAlpha(SoftwareBitmap source, SoftwareBitmap mask)
    {
        int n = source.PixelWidth * source.PixelHeight;

        byte[] srcPixels  = new byte[n * 4];
        byte[] maskPixels = new byte[n];
        byte[] dstPixels  = new byte[n * 4];

        source.CopyToBuffer(srcPixels.AsBuffer());
        mask.CopyToBuffer(maskPixels.AsBuffer());

        for (int i = 0; i < n; i++)
        {
            byte a = maskPixels[i];
            dstPixels[i * 4 + 0] = (byte)(srcPixels[i * 4 + 0] * a / 255); // B premultiplied
            dstPixels[i * 4 + 1] = (byte)(srcPixels[i * 4 + 1] * a / 255); // G premultiplied
            dstPixels[i * 4 + 2] = (byte)(srcPixels[i * 4 + 2] * a / 255); // R premultiplied
            dstPixels[i * 4 + 3] = a;
        }

        var output = new SoftwareBitmap(
            BitmapPixelFormat.Bgra8,
            source.PixelWidth,
            source.PixelHeight,
            BitmapAlphaMode.Premultiplied);
        output.CopyFromBuffer(dstPixels.AsBuffer());
        return output;
    }

    public static async Task<string?> SaveClipboardImageAsync()
    {
        var content = Clipboard.GetContent();
        if (!content.Contains(StandardDataFormats.Bitmap))
            return null;

        string tempDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NpuImageEditor", "temp");
        Directory.CreateDirectory(tempDir);

        string fileName = $"clipboard_{DateTimeOffset.Now:yyyyMMdd_HHmmss_fff}.png";
        string outPath  = Path.Combine(tempDir, fileName);

        RandomAccessStreamReference reference = await content.GetBitmapAsync();
        using IRandomAccessStream source = await reference.OpenReadAsync();
        var decoder = await BitmapDecoder.CreateAsync(source);
        var bitmap  = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

        var folder   = await StorageFolder.GetFolderFromPathAsync(tempDir);
        var file     = await folder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
        using var outStream = await file.OpenAsync(FileAccessMode.ReadWrite);
        var encoder  = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, outStream);
        encoder.SetSoftwareBitmap(bitmap);
        await encoder.FlushAsync();

        return outPath;
    }

    private static string GetOutputPath(string input, string suffix, string extension)
    {
        string dir  = Path.GetDirectoryName(input)!;
        string stem = Path.GetFileNameWithoutExtension(input);
        return Path.Combine(dir, $"{stem}{suffix}{extension}");
    }
}
