using System.Collections.Generic;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.TextTools.Commands;
using NpuTools.TextTools.Services;

namespace NpuTools.TextTools.Pages;

// DynamicListPage for quick text rewrite.
// - Empty search: shows mode list; selecting a mode captures selected text from the active window.
// - With search text: shows mode list; selecting a mode rewrites the typed text directly.
internal sealed partial class QuickRewritePage : DynamicListPage
{
    private readonly TextRewriteService _service;
    private IListItem[] _items;

    private static readonly (TextRewriteMode Mode, string Subtitle)[] StandardModes =
    [
        (TextRewriteMode.FixGrammar,   "Correct grammar and spelling"),
        (TextRewriteMode.MakeFormal,   "Rewrite in a professional tone"),
        (TextRewriteMode.MakeConcise,  "Shorten while preserving meaning"),
        (TextRewriteMode.BulletPoints, "Convert prose to bullet points"),
        (TextRewriteMode.Simplify,     "Plain language for any audience"),
        (TextRewriteMode.Custom,       "Two steps: enter instruction, then paste text"),
    ];

    public QuickRewritePage(TextRewriteService service)
    {
        _service = service;
        Id              = "com.local.nputools.texttools.quick";
        Title           = "Quick Rewrite";
        Name            = "Quick Rewrite";
        Icon            = TextToolsVisuals.Phi;
        PlaceholderText = "Type text to rewrite, or leave empty to rewrite selected text...";
        _items          = BuildItems(string.Empty);
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        _items = BuildItems(newSearch.Trim());
        RaiseItemsChanged(_items.Length);
    }

    public override IListItem[] GetItems() => _items;

    private IListItem[] BuildItems(string inputText)
    {
        bool hasText = inputText.Length > 0;
        var items = new List<IListItem>(StandardModes.Length);

        foreach (var (mode, subtitle) in StandardModes)
        {
            ICommand command = hasText
                ? new RewriteResultPage(inputText, mode, _service)
                : (ICommand)new SelectionRewriteCommand(mode, _service);

            string tagLabel = hasText ? "rewrite typed text" : "rewrite selected text";
            string fullSubtitle = hasText
                ? string.Concat(subtitle, " -- \"", Preview(inputText), "\"")
                : string.Concat(subtitle, " (captures selection)");

            items.Add(new ListItem(command)
            {
                Title    = TextRewriteService.ModeLabel(mode),
                Subtitle = fullSubtitle,
                Icon     = TextToolsVisuals.Phi,
                Tags     = [TextToolsVisuals.MutedTag(tagLabel)],
            });
        }

        return [.. items];
    }

    private static string Preview(string text) =>
        text.Length > 60 ? text[..60] + "..." : text;
}
