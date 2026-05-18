using System;
using System.Text.RegularExpressions;

namespace NpuTools.Clipboard.Data;

public static partial class ClipboardClassifier
{
    public static ClipboardEntryKind ClassifyText(string value)
    {
        string text = value.Trim();
        if (ColorRegex().IsMatch(text))
            return ClipboardEntryKind.Color;
        if (EmailRegex().IsMatch(text))
            return ClipboardEntryKind.Email;
        if (Uri.TryCreate(text, UriKind.Absolute, out var uri) &&
            (uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) ||
             uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) ||
             uri.Scheme.Equals("file", StringComparison.OrdinalIgnoreCase)))
            return ClipboardEntryKind.Link;
        return ClipboardEntryKind.Text;
    }

    public static string BuildTitle(ClipboardEntryKind kind, string? text, int fileCount)
    {
        if (kind == ClipboardEntryKind.Image)
            return "Image";
        if (kind == ClipboardEntryKind.Files)
            return fileCount == 1 ? "1 file" : $"{fileCount} files";

        string trimmed = (text ?? string.Empty).Trim().Replace('\r', ' ').Replace('\n', ' ');
        if (trimmed.Length == 0)
            return kind.ToString();
        return trimmed.Length <= 80 ? trimmed : trimmed[..80] + "...";
    }

    [GeneratedRegex("^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$|^rgba?\\([^\\)]{3,}\\)$|^hsla?\\([^\\)]{3,}\\)$", RegexOptions.Compiled)]
    private static partial Regex ColorRegex();

    [GeneratedRegex("^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$", RegexOptions.Compiled)]
    private static partial Regex EmailRegex();
}
