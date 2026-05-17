using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.TextTools.Services;

namespace NpuTools.TextTools.Pages;

internal sealed partial class RewriteInputPage : DynamicListPage
{
    private readonly TextRewriteMode _mode;
    private readonly TextRewriteService _service;
    private readonly string? _customInstruction;
    private IListItem[] _items;

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
        _items          = BuildItems(string.Empty);
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        _items = BuildItems(newSearch.Trim());
        RaiseItemsChanged(_items.Length);
    }

    public override IListItem[] GetItems() => _items;

    private IListItem[] BuildItems(string text)
    {
        if (text.Length == 0)
        {
            return
            [
                new ListItem(new NoOpCommand())
                {
                    Title    = TextRewriteService.ModeLabel(_mode),
                    Subtitle = "Type or paste text above, then press Enter to rewrite",
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
