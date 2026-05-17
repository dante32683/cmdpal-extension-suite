using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Graphics.Imaging;
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.ContentSafety;
using Microsoft.Windows.AI.Imaging;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;
using Windows.Storage.Streams;

namespace NpuTools.Organize.Services;

internal static partial class AiNamingService
{
    private static readonly HashSet<string> Stopwords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "the", "of", "is", "in", "on", "at", "and", "or", "to",
        "with", "this", "that", "it", "its", "for", "as", "by", "from", "into",
        "be", "are", "was", "were", "image", "picture", "photo", "photograph",
        "shows", "showing", "depicts", "displays", "screenshot",
    };

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex NonAlphanumeric();

    internal static string BuildProposedPath(string originalPath)
    {
        string slug = GenerateSlug(originalPath);
        if (!string.IsNullOrEmpty(slug))
        {
            string dir  = Path.GetDirectoryName(originalPath) ?? string.Empty;
            string ext  = Path.GetExtension(originalPath);
            string date = File.GetCreationTime(originalPath).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            return SlugService.CollisionSafe(dir, $"{date}_{slug}{ext}", ext);
        }

        // Fallback to time-digit slug when image description unavailable.
        return SlugService.BuildProposedPath(originalPath);
    }

    internal static async Task<string> BuildProposedPathAsync(string originalPath)
    {
        string slug = await GenerateSlugAsync(originalPath);
        if (!string.IsNullOrEmpty(slug))
        {
            string dir  = Path.GetDirectoryName(originalPath) ?? string.Empty;
            string ext  = Path.GetExtension(originalPath);
            string date = File.GetCreationTime(originalPath).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            return SlugService.CollisionSafe(dir, $"{date}_{slug}{ext}", ext);
        }

        // Fallback to time-digit slug when image description unavailable.
        return SlugService.BuildProposedPath(originalPath);
    }

    // Returns proposed path, AI description, and raw OCR text in one pass.
    // Used by rename commands that also need to index the screenshot.
    internal static async Task<(string ProposedPath, string Description, string OcrText)> BuildProposedPathWithDataAsync(string originalPath)
    {
        try
        {
            var (description, ocrText) = await GetScreenshotDataAsync(originalPath);
            string slug = Slugify(description);
            string proposedPath;
            if (!string.IsNullOrEmpty(slug))
            {
                string dir  = Path.GetDirectoryName(originalPath) ?? string.Empty;
                string ext  = Path.GetExtension(originalPath);
                string date = File.GetCreationTime(originalPath).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                proposedPath = SlugService.CollisionSafe(dir, $"{date}_{slug}{ext}", ext);
            }
            else
            {
                proposedPath = SlugService.BuildProposedPath(originalPath);
            }
            return (proposedPath, description, ocrText);
        }
        catch
        {
            return (SlugService.BuildProposedPath(originalPath), string.Empty, string.Empty);
        }
    }

    // Runs description and OCR concurrently; either may return empty on failure.
    private static async Task<(string Description, string OcrText)> GetScreenshotDataAsync(string imagePath)
    {
        var descTask = SafeDescribeAsync(imagePath);
        var ocrTask  = SafeOcrAsync(imagePath);
        await Task.WhenAll(descTask, ocrTask);
        return (descTask.Result, ocrTask.Result);
    }

    private static async Task<string> SafeDescribeAsync(string imagePath)
    {
        try   { return await DescribeAsync(imagePath); }
        catch { return string.Empty; }
    }

    private static async Task<string> SafeOcrAsync(string imagePath)
    {
        try   { return await RunOcrAsync(imagePath); }
        catch { return string.Empty; }
    }

    internal static async Task<string> RunOcrAsync(string imagePath)
    {
        var file = await StorageFile.GetFileFromPathAsync(imagePath);
        SoftwareBitmap bitmap;
        using (var stream = await file.OpenAsync(FileAccessMode.Read))
        {
            var decoder = await BitmapDecoder.CreateAsync(stream);
            bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        }

        var engine = OcrEngine.TryCreateFromUserProfileLanguages()
            ?? OcrEngine.TryCreateFromLanguage(new Language("en-US"))
            ?? throw new InvalidOperationException("OCR engine unavailable.");

        var result = await engine.RecognizeAsync(bitmap);
        return result.Text;
    }

    private static string GenerateSlug(string imagePath)
    {
        try
        {
            string description = DescribeAsync(imagePath).GetAwaiter().GetResult();
            return Slugify(description);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static async Task<string> GenerateSlugAsync(string imagePath)
    {
        try
        {
            string description = await DescribeAsync(imagePath);
            return Slugify(description);
        }
        catch
        {
            return string.Empty;
        }
    }

    // Port of slug.ts slugify() — stopword-filtered, max 5 tokens, max 60 chars.
    internal static string Slugify(string description, int maxTokens = 5, int maxLength = 60)
    {
        if (string.IsNullOrWhiteSpace(description)) return string.Empty;

        string nfkd = description.Normalize(NormalizationForm.FormKD);
        var stripped = new StringBuilder(nfkd.Length);
        foreach (char c in nfkd)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                stripped.Append(c);
        }

        string lower   = stripped.ToString().ToLowerInvariant();
        string cleaned = NonAlphanumeric().Replace(lower, " ").Trim();
        if (cleaned.Length == 0) return string.Empty;

        string[] rawTokens = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var filtered = new List<string>(rawTokens.Length);
        foreach (string t in rawTokens)
            if (!Stopwords.Contains(t)) filtered.Add(t);

        IList<string> source = filtered.Count > 0 ? filtered : rawTokens;
        int count = Math.Min(source.Count, maxTokens);

        var sb = new StringBuilder();
        for (int i = 0; i < count; i++)
        {
            if (i > 0) sb.Append('-');
            sb.Append(source[i]);
        }

        string result = sb.ToString();
        return result.Length > maxLength ? result[..maxLength].TrimEnd('-') : result;
    }

    private static async Task<string> DescribeAsync(string imagePath)
    {
        if (ImageDescriptionGenerator.GetReadyState() != AIFeatureReadyState.Ready)
        {
            var ready = await ImageDescriptionGenerator.EnsureReadyAsync();
            if (ready.Status != AIFeatureReadyResultState.Success)
                throw new InvalidOperationException($"ImageDescriptionGenerator unavailable: {ready.Status}");
        }

        var file = await StorageFile.GetFileFromPathAsync(imagePath);
        SoftwareBitmap bitmap;
        using (var stream = await file.OpenAsync(FileAccessMode.Read))
        {
            var decoder = await BitmapDecoder.CreateAsync(stream);
            bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        }

        using var imageBuffer = ImageBuffer.CreateForSoftwareBitmap(bitmap);
        var generator = await ImageDescriptionGenerator.CreateAsync();
        var response  = await generator.DescribeAsync(imageBuffer, ImageDescriptionKind.BriefDescription, new ContentFilterOptions());
        return (response?.Description ?? string.Empty).Trim();
    }
}
