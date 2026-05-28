using System.Collections.Generic;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.TextTools.Services;

namespace NpuTools.TextTools.Pages;

internal sealed partial class TextToolsHubPage : ListPage
{
    private readonly TextRewriteService _service;
    private readonly PendingRewriteStore _pending;

    public TextToolsHubPage(TextRewriteService service, PendingRewriteStore pending)
    {
        _service = service;
        _pending = pending;
        Id    = "com.local.nputools.texttools.hub";
        Title = "Text Tools";
        Name  = "Open";
        Icon  = TextToolsVisuals.Hub;
    }

    public override IListItem[] GetItems()
    {
        var items = new List<IListItem>(8);

        // Surface pending review at the top of the hub as well.
        var snapshot = _pending.Peek();
        if (snapshot.HasValue)
        {
            var (input, result, mode) = snapshot.Value;
            string preview = result.Length > 80 ? result[..80] + "…" : result;
            items.Add(new ListItem(new PendingReviewPage(_pending, input, result, mode, _service))
            {
                Title    = $"Review Last Rewrite — {TextRewriteService.ModeLabel(mode)}",
                Subtitle = preview,
                Icon     = TextToolsVisuals.Check,
                Tags     = [TextToolsVisuals.StatusTag("pending review")],
            });
        }

        var standardModes = new List<(TextRewriteMode Mode, string Subtitle)>
        {
            (TextRewriteMode.FixGrammar,   "Correct grammar and spelling"),
            (TextRewriteMode.MakeFormal,   "Professional tone"),
            (TextRewriteMode.MakeConcise,  "Shorter while preserving meaning"),
            (TextRewriteMode.BulletPoints, "Convert prose to bullet list"),
            (TextRewriteMode.Simplify,     "Plain language for any audience"),
        };

        foreach (var (mode, subtitle) in standardModes)
        {
            items.Add(new ListItem(new RewriteInputPage(mode, _service))
            {
                Title    = TextRewriteService.ModeLabel(mode),
                Subtitle = subtitle,
                Icon     = TextToolsVisuals.Phi,
                Tags     = [TextToolsVisuals.MutedTag("type text")],
            });
        }

        // Custom mode uses a two-step flow: instruction page → text page → result.
        items.Add(new ListItem(new RewriteCustomInstructionPage(_service))
        {
            Title    = TextRewriteService.ModeLabel(TextRewriteMode.Custom),
            Subtitle = "Two steps: enter instruction, then paste text",
            Icon     = TextToolsVisuals.Phi,
            Tags     = [TextToolsVisuals.MutedTag("type instruction")],
        });

        items.Add(new ListItem(new QuickRewritePage(_service, pending: _pending))
        {
            Title    = "Quick Rewrite",
            Subtitle = "Leave empty to rewrite selected text, or type text directly",
            Icon     = TextToolsVisuals.Phi,
            Tags     = [TextToolsVisuals.MutedTag("select text first")],
        });

        return items.ToArray();
    }
}
