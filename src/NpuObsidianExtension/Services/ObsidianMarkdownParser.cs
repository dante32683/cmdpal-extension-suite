using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NpuTools.Obsidian.Models;

namespace NpuTools.Obsidian.Services;

// Pure Markdown parsing helpers. No SDK dependencies — safe to link directly in NpuTools.Tests.
internal sealed partial class ObsidianMarkdownParser
{
    public static ObsidianNote ParseMarkdown(string absolutePath, string vaultPath, string markdown, DateTime lastModifiedUtc)
    {
        string body = markdown.Replace("\r\n", "\n").Replace('\r', '\n');
        var frontmatter = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var frontmatterLists = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        if (body.StartsWith("---\n", StringComparison.Ordinal))
        {
            int end = body.IndexOf("\n---\n", 4, StringComparison.Ordinal);
            if (end >= 0)
            {
                string header = body[4..end];
                body = body[(end + 5)..];
                ParseFrontmatter(header, frontmatter, frontmatterLists);
            }
        }

        string relative = Path.GetRelativePath(vaultPath, absolutePath);
        string title = ResolveTitleFromFrontmatter(frontmatter, body, absolutePath);
        List<string> tags = ResolveTags(frontmatter, frontmatterLists, body);
        List<string> aliases = frontmatterLists.TryGetValue("aliases", out var aliasList)
            ? aliasList
            : (frontmatter.TryGetValue("alias", out string? alias) ? ParseCsv(alias) : []);
        List<string> headings = ExtractHeadings(body);

        return new ObsidianNote
        {
            AbsolutePath = absolutePath,
            RelativePath = relative,
            VaultPath = vaultPath,
            Title = title,
            Tags = tags,
            Aliases = aliases,
            Headings = headings,
            Body = body.Trim(),
            LastModifiedUtc = new DateTimeOffset(DateTime.SpecifyKind(lastModifiedUtc, DateTimeKind.Utc)),
        };
    }

    public static List<string> ExtractWikiLinks(string body)
    {
        var links = new List<string>();
        foreach (Match match in WikiLinkPattern().Matches(body))
        {
            string target = match.Groups[1].Value.Trim();
            if (target.Length > 0 && !links.Contains(target, StringComparer.OrdinalIgnoreCase))
                links.Add(target);
        }

        return links;
    }

    public static List<string> ExtractMarkdownLinks(string body)
    {
        var links = new List<string>();
        foreach (Match match in MarkdownLinkPattern().Matches(body))
        {
            string target = match.Groups[2].Value.Trim();

            // Skip external URLs and special schemes.
            if (target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                target.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                target.StartsWith("obsidian://", StringComparison.OrdinalIgnoreCase) ||
                target.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
                continue;

            // Strip fragment anchor.
            int hash = target.IndexOf('#', StringComparison.Ordinal);
            if (hash >= 0)
                target = target[..hash].Trim();

            // Strip .md extension.
            if (target.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                target = target[..^3];

            // Strip leading ./ path prefix.
            if (target.StartsWith("./", StringComparison.Ordinal))
                target = target[2..];

            target = target.Replace('\\', '/');

            if (target.Length > 0 && !links.Contains(target, StringComparer.OrdinalIgnoreCase))
                links.Add(target);
        }

        return links;
    }

    public static string Slugify(string value, int maxLength = 80)
    {
        string normalized = value.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        bool lastWasDash = false;
        foreach (char c in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category == UnicodeCategory.NonSpacingMark)
                continue;

            char lower = char.ToLowerInvariant(c);
            if (char.IsLetterOrDigit(lower))
            {
                sb.Append(lower);
                lastWasDash = false;
            }
            else if (!lastWasDash && sb.Length > 0)
            {
                sb.Append('-');
                lastWasDash = true;
            }

            if (sb.Length >= maxLength)
                break;
        }

        return sb.ToString().Trim('-');
    }

    private static void ParseFrontmatter(string header, Dictionary<string, string> scalars, Dictionary<string, List<string>> lists)
    {
        string? currentListKey = null;
        var currentListValues = new List<string>();

        foreach (string rawLine in header.Split('\n'))
        {
            string line = rawLine.TrimEnd();

            if (line.StartsWith("  - ", StringComparison.Ordinal) || line.StartsWith("- ", StringComparison.Ordinal))
            {
                string item = line.TrimStart().TrimStart('-').Trim();
                if (currentListKey is not null && item.Length > 0)
                    currentListValues.Add(item);
                continue;
            }

            if (currentListKey is not null)
            {
                lists[currentListKey] = new List<string>(currentListValues);
                currentListValues.Clear();
                currentListKey = null;
            }

            int colon = line.IndexOf(':', StringComparison.Ordinal);
            if (colon <= 0)
                continue;

            string key = line[..colon].Trim();
            string value = line[(colon + 1)..].Trim();

            if (value.Length == 0)
            {
                currentListKey = key;
            }
            else if (value.StartsWith('[') && value.EndsWith(']'))
            {
                string inner = value[1..^1];
                lists[key] = ParseCsv(inner);
            }
            else
            {
                scalars[key] = value;
            }
        }

        if (currentListKey is not null && currentListValues.Count > 0)
            lists[currentListKey] = new List<string>(currentListValues);
    }

    private static string ResolveTitleFromFrontmatter(Dictionary<string, string> frontmatter, string body, string absolutePath)
    {
        if (frontmatter.TryGetValue("title", out string? fmTitle) && !string.IsNullOrWhiteSpace(fmTitle))
            return TrimTitle(fmTitle.Trim('"').Trim());

        foreach (string rawLine in body.Split('\n', StringSplitOptions.TrimEntries))
        {
            string line = rawLine.Trim();
            if (line.Length == 0)
                continue;
            if (line.StartsWith("# ", StringComparison.Ordinal))
                return TrimTitle(line[2..].Trim());
            break;
        }

        return TrimTitle(Path.GetFileNameWithoutExtension(absolutePath));
    }

    private static List<string> ResolveTags(
        Dictionary<string, string> frontmatter,
        Dictionary<string, List<string>> frontmatterLists,
        string body)
    {
        var tags = new List<string>();

        if (frontmatterLists.TryGetValue("tags", out var tagList))
            tags.AddRange(tagList);
        else if (frontmatter.TryGetValue("tags", out string? tagsCsv))
            tags.AddRange(ParseCsv(tagsCsv));

        foreach (Match match in InlineTagPattern().Matches(body))
        {
            string tag = match.Groups[1].Value;
            if (!tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
                tags.Add(tag);
        }

        return tags;
    }

    private static List<string> ExtractHeadings(string body)
    {
        var headings = new List<string>();
        foreach (string rawLine in body.Split('\n', StringSplitOptions.TrimEntries))
        {
            string line = rawLine.Trim();
            if (line.StartsWith('#'))
            {
                string heading = line.TrimStart('#').Trim();
                if (heading.Length > 0)
                    headings.Add(heading);
            }
        }

        return headings;
    }

    private static List<string> ParseCsv(string value)
    {
        return value.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => t.Length > 0)
            .Select(t => t.Trim('"').Trim('\'').Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string TrimTitle(string title)
    {
        string trimmed = string.IsNullOrWhiteSpace(title) ? "Untitled" : title.Trim();
        return trimmed.Length > 120 ? trimmed[..120].Trim() : trimmed;
    }

    [GeneratedRegex(@"(?<!\w)#([A-Za-z][A-Za-z0-9_/-]*)")]
    private static partial Regex InlineTagPattern();

    // Captures the link target from [[Target]], [[Target|Display]], [[Target#Heading]].
    [GeneratedRegex(@"\[\[([^\]|#]+)(?:[|#][^\]]*)?\]\]")]
    private static partial Regex WikiLinkPattern();

    // Captures display text and target from [Display](target).
    [GeneratedRegex(@"\[([^\]]*)\]\(([^)]+)\)")]
    private static partial Regex MarkdownLinkPattern();
}
