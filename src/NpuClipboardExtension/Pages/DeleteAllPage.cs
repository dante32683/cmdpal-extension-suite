using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.Clipboard.Data;

namespace NpuTools.Clipboard.Pages;

internal sealed partial class DeleteAllPage : DynamicListPage
{
    private readonly ClipboardStore _store;
    private IListItem[] _items;

    public DeleteAllPage(ClipboardStore store)
    {
        _store = store;
        Id = "com.local.nputools.clipboard.delete-all";
        Title = "Delete All Clipboard Entries";
        Name = "Delete All";
        Icon = ClipboardVisuals.Delete;
        PlaceholderText = "Type DELETE to enable";
        _items = BuildItems(string.Empty);
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        _items = BuildItems(newSearch.Trim());
        RaiseItemsChanged(_items.Length);
    }

    public override IListItem[] GetItems() => _items;

    private IListItem[] BuildItems(string query)
    {
        if (query == "DELETE")
        {
            return
            [
                new ListItem(new DeleteAllCommand(_store))
                {
                    Title = "Delete all clipboard entries now",
                    Subtitle = "This removes the local NPU Clipboard history. Pinned entries are included.",
                    Icon = ClipboardVisuals.Delete,
                    Tags = [ClipboardVisuals.CriticalTag("destructive")],
                },
            ];
        }

        return
        [
            new ListItem(new NoOpCommand())
            {
                Title = "Type DELETE exactly to enable delete all",
                Subtitle = $"{_store.Count} local clipboard entries would be removed.",
                Icon = ClipboardVisuals.Warning,
                Tags = [ClipboardVisuals.WarningTag("locked")],
            },
        ];
    }
}

internal sealed partial class DeleteAllCommand : InvokableCommand
{
    private readonly ClipboardStore _store;

    public DeleteAllCommand(ClipboardStore store)
    {
        _store = store;
        Name = "Delete All Entries";
        Icon = ClipboardVisuals.Delete;
    }

    public override CommandResult Invoke()
    {
        int deleted = _store.DeleteAll();
        return CommandResult.ShowToast($"Deleted {deleted} clipboard entr{(deleted == 1 ? "y" : "ies")}.");
    }
}
