using System.Collections.Generic;
using System.Linq;

namespace NpuTools.Clipboard.Data;

// Curated default set covering the most common secret formats the user is likely
// to accidentally copy. Each pattern is a complete .NET regex. Patterns that only
// match the `KEY=value` form (e.g. CF_API_KEY=...) are intentionally narrow so
// that an unrelated hex string of the same length does not trigger a false positive.
// Users can add, remove, or refine entries from the Secret Patterns settings page.
public static class DefaultSecretPatterns
{
    public static IReadOnlyList<SecretPattern> All { get; } = new List<SecretPattern>
    {
        new() { Name = "Cloudflare API Key",     Regex = @"CF_API_KEY\s*=\s*[A-Za-z0-9_-]{20,}" },
        new() { Name = "Cloudflare API Token",   Regex = @"CF_API_TOKEN\s*=\s*[A-Za-z0-9_-]{20,}" },
        new() { Name = "Cloudflare Zone ID",     Regex = @"CF_ZONE_ID\s*=\s*[a-f0-9]{32}" },
        new() { Name = "Cloudflare Account ID",  Regex = @"CF_ACCOUNT_ID\s*=\s*[a-f0-9]{32}" },
        new() { Name = "GitHub PAT",             Regex = @"\bghp_[A-Za-z0-9]{36}\b" },
        new() { Name = "GitHub OAuth Token",     Regex = @"\bgho_[A-Za-z0-9]{36}\b" },
        new() { Name = "GitHub Server Token",    Regex = @"\bghs_[A-Za-z0-9]{36}\b" },
        new() { Name = "GitHub User Token",      Regex = @"\bghu_[A-Za-z0-9]{36}\b" },
        new() { Name = "GitHub Refresh Token",   Regex = @"\bghr_[A-Za-z0-9]{36}\b" },
        new() { Name = "AWS Access Key ID",      Regex = @"\bAKIA[0-9A-Z]{16}\b" },
        new() { Name = "AWS Session Access Key", Regex = @"\bASIA[0-9A-Z]{16}\b" },
    };

    // Returns a fresh mutable list so the caller can edit it without mutating the
    // static instance (settings are deserialized into a new list on every load).
    public static List<SecretPattern> Copy() =>
        All.Select(p => new SecretPattern { Name = p.Name, Regex = p.Regex }).ToList();
}
