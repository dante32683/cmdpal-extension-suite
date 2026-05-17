using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace NpuTools.Organize.Pages;

internal sealed partial class RenameResultPage : ListPage
{
    private readonly int _success;
    private readonly int _failed;

    public RenameResultPage(int success, int failed)
    {
        _success = success;
        _failed  = failed;
        Id       = "com.local.nputools.organize.result";
        Title    = "Rename Complete";
        Name     = "Result";
        Icon     = OrganizeVisuals.Check;
    }

    public override IListItem[] GetItems()
    {
        return
        [
            new ListItem(new NoOpCommand())
            {
                Title    = $"Renamed {_success} file{(_success == 1 ? "" : "s")}",
                Subtitle = _failed > 0 ? $"{_failed} failed — check permissions" : "All files renamed successfully",
                Icon     = _failed > 0 ? OrganizeVisuals.Warning : OrganizeVisuals.Check,
            },
        ];
    }
}
