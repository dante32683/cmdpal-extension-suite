using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace NpuTools.Organize.Services;

internal static partial class SlugService
{
    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex NonAlphanumeric();

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}")]
    private static partial Regex AlreadyDatePrefixed();

    // Strips leading date/time noise from Windows screenshot filenames like "Screenshot 2025-11-15 013016"
    [GeneratedRegex(@"^(screenshot\s+)?\d{4}[-\s]\d{2}[-\s]\d{2}\s*\d{0,6}\s*", RegexOptions.IgnoreCase)]
    private static partial Regex LeadingDateNoise();

    internal static bool IsAlreadyOrganized(string fileName) =>
        AlreadyDatePrefixed().IsMatch(fileName);

    internal static string BuildProposedPath(string originalPath)
    {
        string dir      = Path.GetDirectoryName(originalPath) ?? string.Empty;
        string stem     = Path.GetFileNameWithoutExtension(originalPath);
        string ext      = Path.GetExtension(originalPath);
        string date     = File.GetCreationTime(originalPath).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        string slug     = Slugify(stem);
        string proposed = string.IsNullOrEmpty(slug)
            ? $"{date}{ext}"
            : $"{date}_{slug}{ext}";

        return CollisionSafe(dir, proposed, ext);
    }

    private static string Slugify(string stem)
    {
        // Strip leading Windows screenshot date noise; if nothing meaningful remains, return empty
        // so the output is just the date prefix with no redundant slug.
        string stripped = LeadingDateNoise().Replace(stem, string.Empty).Trim();
        if (stripped.Length == 0) return string.Empty;

        string lower = stripped.ToLowerInvariant();
        string clean = NonAlphanumeric().Replace(lower, "-").Trim('-');
        return clean.Length > 80 ? clean[..80].TrimEnd('-') : clean;
    }

    private static string CollisionSafe(string dir, string proposed, string ext)
    {
        string candidate = Path.Combine(dir, proposed);
        if (!File.Exists(candidate)) return candidate;

        string nameNoExt = Path.GetFileNameWithoutExtension(proposed);
        for (int i = 2; i < 1000; i++)
        {
            string next = Path.Combine(dir, $"{nameNoExt}-{i}{ext}");
            if (!File.Exists(next)) return next;
        }

        return Path.Combine(dir, $"{nameNoExt}-{Guid.NewGuid():N}{ext}");
    }
}
