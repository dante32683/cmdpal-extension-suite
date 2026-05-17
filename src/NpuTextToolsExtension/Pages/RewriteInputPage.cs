using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.TextTools.Services;

namespace NpuTools.TextTools.Pages;

internal sealed partial class RewriteInputPage : ListPage
{
    private readonly TextRewriteMode _mode;
    private readonly TextRewriteService _service;
    private readonly string? _customInstruction;

    public RewriteInputPage(TextRewriteMode mode, TextRewriteService service, string? customInstruction = null)
    {
        _mode              = mode;
        _service           = service;
        _customInstruction = customInstruction;
        Id              = $"com.local.nputools.texttools.{mode.ToString().ToLowerInvariant()}";
        Title           = TextRewriteService.ModeLabel(mode);
        Name            = "Rewrite";
        Icon            = TextToolsVisuals.Phi;
        PlaceholderText = "Paste or type your text here…";
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
                    Title    = TextRewriteService.ModeLabel(_mode),
                    Subtitle = "Type or paste text in the search box above, then press Enter to rewrite",
                    Icon     = TextToolsVisuals.Phi,
                },
            ];
        }

        return
        [
            new ListItem(new RewriteResultPage(text, _mode, _service, _customInstruction))
            {
                Title    = $"Rewrite — {TextRewriteService.ModeLabel(_mode)}",
                Subtitle = text.Length > 100 ? text[..100] + "…" : text,
                Icon     = TextToolsVisuals.Phi,
                Tags     = [TextToolsVisuals.MutedTag("press Enter")],
            },
        ];
    }
}
