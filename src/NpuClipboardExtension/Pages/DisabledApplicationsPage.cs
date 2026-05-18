using System;
using System.Linq;
using System.Text.Json.Nodes;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.Clipboard.Data;

namespace NpuTools.Clipboard.Pages;

internal sealed partial class DisabledApplicationsPage : ContentPage
{
    private readonly DisabledApplicationsForm _form;

    public DisabledApplicationsPage(ClipboardSettingsStore settings)
    {
        _form = new DisabledApplicationsForm(settings);
        Id = "com.local.nputools.clipboard.disabled-apps";
        Title = "Disabled Applications";
        Name = "Disabled Apps";
        Icon = ClipboardVisuals.Settings;
    }

    public override IContent[] GetContent() => [_form];
}

internal sealed partial class DisabledApplicationsForm : FormContent
{
    private readonly ClipboardSettingsStore _settings;

    public DisabledApplicationsForm(ClipboardSettingsStore settings)
    {
        _settings = settings;
        string value = string.Join(", ", settings.Current.DisabledApplicationNames).Replace("\"", "\\\"");
        TemplateJson = $$"""
{
  "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
  "type": "AdaptiveCard",
  "version": "1.6",
  "body": [
    { "type": "TextBlock", "text": "Disabled Applications", "weight": "Bolder", "size": "Medium", "wrap": true },
    { "type": "Input.Text", "id": "apps", "label": "Process names or title fragments, comma separated", "value": "{{value}}", "isMultiline": true }
  ],
  "actions": [{ "type": "Action.Submit", "title": "Save" }]
}
""";
    }

    public override CommandResult SubmitForm(string payload)
    {
        string raw = JsonNode.Parse(payload)?["apps"]?.ToString() ?? string.Empty;
        _settings.Update(s =>
        {
            s.DisabledApplicationNames = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        });
        return CommandResult.ShowToast("Disabled applications updated.");
    }
}
