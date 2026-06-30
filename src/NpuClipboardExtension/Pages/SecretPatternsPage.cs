using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.Clipboard.Data;

namespace NpuTools.Clipboard.Pages;

// Lists and edits the SecretPattern rules used by SecretPatternMatcher. The form
// shows one rule per line in `Name | Regex` format. Blank lines and lines that
// do not parse are ignored. A line with an uncompilable regex is reported in the
// success toast so the user knows which entry was dropped.
//
// The "Filter secret patterns from capture" toggle lives in the standard
// Command Palette settings page (ClipboardSettingsManager) so the master
// on/off control is always discoverable; this page only manages the rule list.
internal sealed partial class SecretPatternsPage : ContentPage
{
    public SecretPatternsPage(ClipboardSettingsStore settings)
    {
        Id = "com.local.nputools.clipboard.secret-patterns";
        Title = "Secret Patterns";
        Name = "Secret Patterns";
        Icon = ClipboardVisuals.Settings;
    }

    public override IContent[] GetContent() => [new SecretPatternsForm(settings: new ClipboardSettingsStore())];
}

internal sealed partial class SecretPatternsForm : FormContent
{
    public SecretPatternsForm(ClipboardSettingsStore settings)
    {
        TemplateJson = BuildTemplateJson(settings.Current.SecretPatterns);
    }

    public override CommandResult SubmitForm(string payload)
    {
        string raw = JsonNode.Parse(payload)?["patterns"]?.ToString() ?? string.Empty;
        var parsed = SecretPatternsParser.ParseLines(raw);
        new ClipboardSettingsStore().Update(s => s.SecretPatterns = parsed.Patterns);
        string message = parsed.InvalidCount == 0
            ? $"Saved {parsed.Patterns.Count} pattern{(parsed.Patterns.Count == 1 ? "" : "s")}."
            : $"Saved {parsed.Patterns.Count} pattern{(parsed.Patterns.Count == 1 ? "" : "s")}. {parsed.InvalidCount} line{(parsed.InvalidCount == 1 ? "" : "s")} skipped (invalid regex or format).";
        return CommandResult.ShowToast(message);
    }

    private static string BuildTemplateJson(IReadOnlyList<SecretPattern> patterns)
    {
        string body = string.Join("\n", patterns.Select(p => $"{p.Name} | {p.Regex}")).Replace("\"", "\\\"");
        return $$"""
{
  "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
  "type": "AdaptiveCard",
  "version": "1.6",
  "body": [
    { "type": "TextBlock", "text": "Secret Patterns", "weight": "Bolder", "size": "Medium", "wrap": true },
    { "type": "TextBlock", "text": "One pattern per line in `Name | Regex` format. Captured text matching any regex is silently dropped before it lands in history or the sync folder.", "wrap": true, "isSubtle": true, "spacing": "Small" },
    { "type": "Input.Text", "id": "patterns", "label": "Patterns", "value": "{{body}}", "isMultiline": true, "height": "stretch" }
  ],
  "actions": [{ "type": "Action.Submit", "title": "Save" }]
}
""";
    }
}
