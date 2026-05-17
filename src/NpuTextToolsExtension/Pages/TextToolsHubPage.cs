using System.Collections.Generic;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.TextTools.Services;

namespace NpuTools.TextTools.Pages;

internal sealed partial class TextToolsHubPage : ListPage
{
    private readonly TextRewriteService _service;

    public TextToolsHubPage(TextRewriteService service)
    {
        _service = service;
        Id    = "com.local.nputools.texttools.hub";
        Title = "Text Tools";
        Name  = "Open";
        Icon  = TextToolsVisuals.Hub;
    }

    public override IListItem[] GetItems()
    {
        var modes = new List<(TextRewriteMode Mode, string Subtitle)>
        {
            (TextRewriteMode.FixGrammar,   "Correct grammar and spelling"),
            (TextRewriteMode.MakeFormal,   "Professional tone"),
            (TextRewriteMode.MakeConcise,  "Shorter while preserving meaning"),
            (TextRewriteMode.BulletPoints, "Convert prose to bullet list"),
            (TextRewriteMode.Simplify,     "Plain language for any audience"),
            (TextRewriteMode.Custom,       "Type your own rewrite instruction"),
        };

        var items = new List<IListItem>(modes.Count);
        foreach (var (mode, subtitle) in modes)
        {
            items.Add(new ListItem(new RewriteInputPage(mode, _service))
            {
                Title    = TextRewriteService.ModeLabel(mode),
                Subtitle = subtitle,
                Icon     = TextToolsVisuals.Phi,
                Tags     = [TextToolsVisuals.MutedTag("type text")],
            });
        }

        return items.ToArray();
    }
}
