using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;
using Windows.Storage.Streams;
using Microsoft.Windows.AI.Imaging;

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
        var file = await StorageFile.GetFileFromPathAsync(imagePath);
        using var inStream = await file.OpenAsync(FileAccessMode.Read);
        var decoder = await BitmapDecoder.CreateAsync(inStream);
        var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

        var extractor = await ImageObjectExtractor.CreateWithSoftwareBitmapAsync(bitmap);
        var result = extractor.GetSoftwareBitmapObjectMask(new ImageObjectExtractorHint([], [], []));

        string outputPath = GetOutputPath(imagePath, "_nobg", ".png");
        var outputFile = await StorageFile.GetFileFromPathAsync(outputPath)
            .AsTask()
            .ContinueWith(t => t.IsFaulted
                ? null
                : t.Result);

        if (outputFile is null)
        {
            var folder = await StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(imagePath)!);
            outputFile = await folder.CreateFileAsync(Path.GetFileName(outputPath), CreationCollisionOption.ReplaceExisting);
        }

        using var outStream = await outputFile!.OpenAsync(FileAccessMode.ReadWrite);
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, outStream);
        encoder.SetSoftwareBitmap(result);
        await encoder.FlushAsync();

        return outputPath;
    }

    public static async Task<string> SuperResolutionAsync(string imagePath, int scaleFactor = 2)
    {
        var file = await StorageFile.GetFileFromPathAsync(imagePath);
        using var inStream = await file.OpenAsync(FileAccessMode.Read);
        var decoder = await BitmapDecoder.CreateAsync(inStream);
        var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

        var scaler = await ImageScaler.CreateAsync();
        int newW = bitmap.PixelWidth  * scaleFactor;
        int newH = bitmap.PixelHeight * scaleFactor;
        var scaled = scaler.ScaleSoftwareBitmap(bitmap, newW, newH);

        string suffix = $"_{scaleFactor}x";
        string outputPath = GetOutputPath(imagePath, suffix, Path.GetExtension(imagePath));
        var folder = await StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(imagePath)!);
        var outputFile = await folder.CreateFileAsync(Path.GetFileName(outputPath), CreationCollisionOption.ReplaceExisting);

        using var outStream = await outputFile.OpenAsync(FileAccessMode.ReadWrite);
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, outStream);
        encoder.SetSoftwareBitmap(scaled);
        await encoder.FlushAsync();

        return outputPath;
    }

    private static string GetOutputPath(string input, string suffix, string extension)
    {
        string dir  = Path.GetDirectoryName(input)!;
        string stem = Path.GetFileNameWithoutExtension(input);
        return Path.Combine(dir, $"{stem}{suffix}{extension}");
    }
}
