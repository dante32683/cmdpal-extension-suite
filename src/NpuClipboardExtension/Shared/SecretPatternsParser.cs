using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace NpuTools.Clipboard.Data;

// Parses the textarea payload of SecretPatternsPage into a list of SecretPattern
// rows. Lives in Shared/ so the test project can link this file directly without
// pulling in the Microsoft.CommandPalette.Extensions SDK that the page itself uses.
internal static class SecretPatternsParser
{
    // Returns the parsed list and the count of lines that were skipped because
    // they were empty, missing the `|` separator, missing name/regex, or had a
    // regex that would not compile.
    public static (List<SecretPattern> Patterns, int InvalidCount) ParseLines(string? raw)
    {
        var result = new List<SecretPattern>();
        int invalid = 0;
        if (string.IsNullOrWhiteSpace(raw)) return (result, 0);

        foreach (string line in raw.Split('\n'))
        {
            string trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;

            int sep = trimmed.IndexOf('|');
            if (sep <= 0 || sep == trimmed.Length - 1) { invalid++; continue; }

            string name = trimmed[..sep].Trim();
            string regex = trimmed[(sep + 1)..].Trim();
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(regex)) { invalid++; continue; }

            try { _ = new Regex(regex); }
            catch { invalid++; continue; }

            result.Add(new SecretPattern { Name = name, Regex = regex });
        }
        return (result, invalid);
    }
}
