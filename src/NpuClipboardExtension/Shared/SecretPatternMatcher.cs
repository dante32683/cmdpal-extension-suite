using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace NpuTools.Clipboard.Data;

// Compiles a list of SecretPattern rules from ClipboardAppSettings and runs them
// against captured text. A successful match returns the pattern's display name so
// the caller can log "skipped: matched secret pattern: <name>".
//
// Patterns are compiled once in the constructor; the regexes use a 100ms timeout
// as a hard guard against accidental ReDoS from a user-authored pattern. Malformed
// patterns are silently dropped at construction so a typo in one rule does not
// disable the entire filter.
public sealed class SecretPatternMatcher
{
    private readonly List<Compiled> _patterns = new();
    public bool Enabled { get; }
    public int CompiledCount => _patterns.Count;

    public SecretPatternMatcher(ClipboardAppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Enabled = settings.SecretDetectionEnabled;
        if (!Enabled) return;

        foreach (var p in settings.SecretPatterns)
        {
            if (string.IsNullOrWhiteSpace(p.Regex)) continue;
            try
            {
                var regex = new Regex(
                    p.Regex,
                    RegexOptions.CultureInvariant,
                    TimeSpan.FromMilliseconds(100));
                _patterns.Add(new Compiled(string.IsNullOrWhiteSpace(p.Name) ? p.Regex : p.Name, regex));
            }
            catch
            {
                // Malformed regex — skip. Validation surface in the settings page
                // surfaces compile errors before save; this is the runtime fallback.
            }
        }
    }

    public string? Match(string? text)
    {
        if (!Enabled || string.IsNullOrEmpty(text) || _patterns.Count == 0) return null;
        foreach (var c in _patterns)
        {
            try
            {
                if (c.Regex.IsMatch(text)) return c.Name;
            }
            catch (RegexMatchTimeoutException)
            {
                // Pattern took too long — treat as no match and continue.
            }
        }
        return null;
    }

    private sealed record Compiled(string Name, Regex Regex);
}
