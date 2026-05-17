using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.TextTools.Services;

namespace NpuTools.TextTools.Pages;

/// <summary>
/// Step 2 of the Custom Rewrite two-step flow.
/// The rewrite instruction is fixed from step 1; the user pastes the text to rewrite here.
/// </summary>
internal sealed partial class RewriteCustomTextPage : ListPage
{
    private readonly string _instruction;
    private readonly TextRewriteService _service;

    public RewriteCustomTextPage(string instruction, TextRewriteService service)
    {
        _instruction    = instruction;
        _service        = service;
        Id              = "com.local.nputools.texttools.custom.text";
        Title           = "Custom Rewrite — Paste Text";
        Name            = "Next";
        Icon            = TextToolsVisuals.Phi;
        PlaceholderText = "Now paste or type the text to rewrite…";
    }

    public override IListItem[] GetItems()
    {
        string text = (SearchText ?? string.Empty).Trim();

        if (text.Length == 0)
        {
            return
            [
                new ListItem(new NoOpCommand())
                {
                    Title    = $"Instruction: {(_instruction.Length > 80 ? _instruction[..80] + "…" : _instruction)}",
                    Subtitle = "Paste or type the text you want to rewrite above, then press Enter",
                    Icon     = TextToolsVisuals.Phi,
                },
            ];
        }

        return
        [
            new ListItem(new RewriteResultPage(text, TextRewriteMode.Custom, _service, _instruction))
            {
                Title    = $"Custom Rewrite — {(text.Length > 100 ? text[..100] + "…" : text)}",
                Subtitle = $"Instruction: {(_instruction.Length > 60 ? _instruction[..60] + "…" : _instruction)}",
                Icon     = TextToolsVisuals.Phi,
                Tags     = [TextToolsVisuals.MutedTag("press Enter to rewrite")],
            },
        ];
    }
}
