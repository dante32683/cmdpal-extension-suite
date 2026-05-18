using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.TextTools.Services;

namespace NpuTools.TextTools.Pages;

/// <summary>
/// Step 1 of the Custom Rewrite two-step flow.
/// The user types their rewrite instruction in the search box, then presses Enter
/// to proceed to step 2 where they paste the text to rewrite.
/// </summary>
internal sealed partial class RewriteCustomInstructionPage : DynamicListPage
{
    private readonly TextRewriteService _service;
    private IListItem[] _items;

    public RewriteCustomInstructionPage(TextRewriteService service)
    {
        _service        = service;
        Id              = "com.local.nputools.texttools.custom.instruction";
        Title           = "Custom Rewrite: Instruction";
        Name            = "Custom Rewrite";
        Icon            = TextToolsVisuals.Phi;
        PlaceholderText = "Enter your rewrite instruction…";
        _items          = BuildItems(string.Empty);
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        _items = BuildItems(newSearch.Trim());
        RaiseItemsChanged(_items.Length);
    }

    public override IListItem[] GetItems() => _items;

    private IListItem[] BuildItems(string instruction)
    {
        if (instruction.Length == 0)
        {
            return
            [
                new ListItem(new NoOpCommand())
                {
                    Title    = "Custom Rewrite",
                    Subtitle = "Type your rewrite instruction above (e.g. \"Translate to Spanish\"), then press Enter",
                    Icon     = TextToolsVisuals.Phi,
                },
            ];
        }

        return
        [
            new ListItem(new RewriteCustomTextPage(instruction, _service))
            {
                Title    = instruction.Length > 80 ? instruction[..80] + "…" : instruction,
                Subtitle = "Press Enter — next: paste the text to rewrite",
                Icon     = TextToolsVisuals.Phi,
                Tags     = [TextToolsVisuals.MutedTag("step 1 of 2")],
            },
        ];
    }
}
