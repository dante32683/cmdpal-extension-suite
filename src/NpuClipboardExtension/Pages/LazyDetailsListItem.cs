using System;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace NpuTools.Clipboard.Pages;

// Details can contain large text bodies, image previews, and metadata. Command Palette only
// needs them for the selected row, so constructing them for every history item wastes most of
// the page-open budget.
internal sealed partial class LazyDetailsListItem : ListItem
{
    private readonly Lazy<Details> _details;

    public LazyDetailsListItem(ICommand command, Func<Details> detailsFactory)
        : base(command)
    {
        _details = new Lazy<Details>(detailsFactory);
    }

    public override IDetails? Details
    {
        get => _details.Value;
        set { }
    }
}
