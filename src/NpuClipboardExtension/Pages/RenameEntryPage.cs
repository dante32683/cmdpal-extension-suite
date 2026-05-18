using System.Text.Json.Nodes;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.Clipboard.Data;

namespace NpuTools.Clipboard.Pages;

internal sealed partial class RenameEntryPage : ContentPage
{
    private readonly RenameEntryForm _form;

    public RenameEntryPage(ClipboardStore store, string id, string currentName)
    {
        _form = new RenameEntryForm(store, id, currentName);
        Id = "com.local.nputools.clipboard.rename";
        Title = "Rename Clipboard Entry";
        Name = "Rename";
        Icon = ClipboardVisuals.Rename;
    }

    public override IContent[] GetContent() => [_form];
}

internal sealed partial class RenameEntryForm : FormContent
{
    private readonly ClipboardStore _store;
    private readonly string _id;

    public RenameEntryForm(ClipboardStore store, string id, string currentName)
    {
        _store = store;
        _id = id;
        string escaped = currentName.Replace("\"", "\\\"");
        TemplateJson = $$"""
{
  "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
  "type": "AdaptiveCard",
  "version": "1.6",
  "body": [
    { "type": "TextBlock", "text": "Rename Entry", "weight": "Bolder", "size": "Medium", "wrap": true },
    { "type": "Input.Text", "id": "name", "label": "Name", "value": "{{escaped}}" }
  ],
  "actions": [{ "type": "Action.Submit", "title": "Save" }]
}
""";
    }

    public override CommandResult SubmitForm(string payload)
    {
        string name = JsonNode.Parse(payload)?["name"]?.ToString() ?? string.Empty;
        _store.Rename(_id, name);
        return CommandResult.ShowToast("Clipboard entry renamed.");
    }
}
