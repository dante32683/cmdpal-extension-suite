using System;
using System.IO;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.Clipboard.Data;

namespace NpuTools.Clipboard.Pages;

// DynamicListPage for entering the sync folder path.
// Empty search clears the sync folder; any text that is a valid directory path sets it.
internal sealed partial class SetSyncFolderPage : DynamicListPage
{
    private readonly ClipboardSettingsStore _settings;
    private IListItem[] _items;

    public SetSyncFolderPage(ClipboardSettingsStore settings)
    {
        _settings = settings;
        Id              = "com.local.nputools.clipboard.sync-folder";
        Title           = "Cross-Device Sync Folder";
        Name            = "Set Sync Folder";
        Icon            = ClipboardVisuals.Sync;
        PlaceholderText = "Paste the path to your sync folder (e.g. C:\\Users\\You\\OneDrive\\ClipSync)...";
        _items          = BuildItems(string.Empty);
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        _items = BuildItems(newSearch.Trim());
        RaiseItemsChanged(_items.Length);
    }

    public override IListItem[] GetItems() => _items;

    private IListItem[] BuildItems(string input)
    {
        string? current = _settings.Current.SyncFolder;

        if (string.IsNullOrWhiteSpace(input))
        {
            var items = new System.Collections.Generic.List<IListItem>();

            if (!string.IsNullOrWhiteSpace(current))
            {
                items.Add(new ListItem(new ClearSyncFolderCommand(_settings))
                {
                    Title    = "Clear sync folder",
                    Subtitle = $"Current: {current}",
                    Icon     = ClipboardVisuals.Delete,
                    Tags     = [ClipboardVisuals.CriticalTag("removes sync")],
                });
            }

            items.Add(new ListItem(new NoOpCommand())
            {
                Title    = string.IsNullOrWhiteSpace(current) ? "No sync folder configured" : $"Current: {current}",
                Subtitle = "Type or paste a folder path to enable cross-device sync of text entries",
                Icon     = ClipboardVisuals.Sync,
                Details  = new Details
                {
                    Title = "How cross-device sync works",
                    Body  = "Each device writes new text clipboard entries as JSON files to the sync folder. " +
                            "Any sync service (OneDrive, Dropbox, Google Drive, etc.) copies those files to other devices. " +
                            "When you open Clipboard History on another device, it merges the new entries automatically.\n\n" +
                            "**Only text entries are synced** — images and file paths are device-specific.\n\n" +
                            "**Recommended**: create a dedicated subfolder inside your cloud sync drive, " +
                            "e.g. `C:\\Users\\You\\OneDrive\\ClipSync`.",
                },
            });
            return [.. items];
        }

        // Validate path format.
        if (input.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            return
            [
                new ListItem(new NoOpCommand())
                {
                    Title    = "Invalid path",
                    Subtitle = "The path contains characters not allowed in folder names.",
                    Icon     = ClipboardVisuals.Warning,
                },
            ];
        }

        string normalized = input.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        bool exists = Directory.Exists(normalized);
        string subtitle = exists ? "Folder exists — press Enter to set as sync folder" : "Folder does not exist yet — it will be created on first use";

        return
        [
            new ListItem(new ApplySyncFolderCommand(_settings, normalized))
            {
                Title    = $"Set sync folder: {normalized}",
                Subtitle = subtitle,
                Icon     = ClipboardVisuals.Sync,
                Tags     = exists ? [ClipboardVisuals.StatusTag("exists")] : [ClipboardVisuals.MutedTag("will create")],
            },
        ];
    }
}

internal sealed partial class ApplySyncFolderCommand : InvokableCommand
{
    private readonly ClipboardSettingsStore _settings;
    private readonly string _path;

    public ApplySyncFolderCommand(ClipboardSettingsStore settings, string path)
    {
        _settings = settings;
        _path     = path;
        Name      = "Set Sync Folder";
        Icon      = ClipboardVisuals.Sync;
    }

    public override CommandResult Invoke()
    {
        _settings.Update(s => s.SyncFolder = _path);
        return CommandResult.ShowToast($"Sync folder set: {_path}");
    }
}

internal sealed partial class ClearSyncFolderCommand : InvokableCommand
{
    private readonly ClipboardSettingsStore _settings;

    public ClearSyncFolderCommand(ClipboardSettingsStore settings)
    {
        _settings = settings;
        Name      = "Clear Sync Folder";
        Icon      = ClipboardVisuals.Delete;
    }

    public override CommandResult Invoke()
    {
        _settings.Update(s => s.SyncFolder = null);
        return CommandResult.ShowToast("Sync folder cleared. Cross-device sync disabled.");
    }
}
