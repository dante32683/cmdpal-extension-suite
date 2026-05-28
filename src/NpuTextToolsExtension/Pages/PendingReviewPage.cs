using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.TextTools.Commands;
using NpuTools.TextTools.Services;

namespace NpuTools.TextTools.Pages;

// Shows the result of the most recent selection-rewrite for review before pasting.
// Clears the pending store on first GetItems() so the item disappears from QuickRewritePage
// once the user has seen it.
internal sealed partial class PendingReviewPage : ListPage
{
    private readonly PendingRewriteStore _pending;
    private readonly string _input;
    private readonly string _result;
    private readonly TextRewriteMode _mode;
    private readonly TextRewriteService _service;

    public PendingReviewPage(PendingRewriteStore pending, string input, string result, TextRewriteMode mode, TextRewriteService service)
    {
        _pending = pending;
        _input   = input;
        _result  = result;
        _mode    = mode;
        _service = service;

        Id          = "com.local.nputools.texttools.pending-review";
        Title       = $"Review — {TextRewriteService.ModeLabel(mode)}";
        Name        = "Review";
        Icon        = TextToolsVisuals.Check;
        ShowDetails = true;
    }

    public override IListItem[] GetItems()
    {
        _pending.Clear();

        string resultPreview = _result.Length > 200 ? _result[..200] + "…" : _result;
        string inputPreview  = _input.Length > 200  ? _input[..200]  + "…" : _input;

        return
        [
            new ListItem(new CopyResultCommand(_result))
            {
                Title    = "Copy to Clipboard",
                Subtitle = resultPreview,
                Icon     = TextToolsVisuals.Copy,
                Tags     = [TextToolsVisuals.MutedTag("copies full result")],
                Details  = new Details
                {
                    Title = $"Rewritten — {TextRewriteService.ModeLabel(_mode)}",
                    Body  = _result,
                },
            },
            new ListItem(new NoOpCommand())
            {
                Title    = "Original",
                Subtitle = inputPreview,
                Icon     = TextToolsVisuals.Phi,
                Details  = new Details
                {
                    Title = "Original text",
                    Body  = _input,
                },
            },
        ];
    }
}
