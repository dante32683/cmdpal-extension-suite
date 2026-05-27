using System.Collections.Generic;
using System.Globalization;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.Obsidian.Commands;
using NpuTools.Obsidian.Models;
using NpuTools.Obsidian.Services;
using static NpuTools.Obsidian.KeyChords;

namespace NpuTools.Obsidian.Pages;

internal static class NoteItemFactory
{
    public static ListItem Build(ObsidianVaultStore store, ObsidianSettingsStore settings, ObsidianNote note)
    {
        return new ListItem(new NotePreviewPage(store, settings, note.AbsolutePath))
        {
            Title = note.Title,
            Subtitle = BuildSubtitle(note),
            Icon = ObsidianVisuals.Note,
            Tags = BuildTags(note),
            MoreCommands = BuildMoreCommands(store, settings, note),
            Details = BuildDetails(note),
        };
    }

    public static Details BuildDetails(ObsidianNote note)
    {
        return new Details
        {
            Title = note.Title,
            Body = string.IsNullOrWhiteSpace(note.Body) ? "_Empty note_" : note.Body,
            Size = ContentSize.Large,
            Metadata =
            [
                new DetailsElement { Key = "Folder", Data = new DetailsLink(System.IO.Path.GetDirectoryName(note.RelativePath) ?? "") },
                new DetailsElement { Key = "Modified", Data = new DetailsLink(note.LastModifiedUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)) },
                new DetailsElement { Key = "Tags", Data = new DetailsLink(note.Tags.Count > 0 ? string.Join(", ", note.Tags) : "none") },
                new DetailsElement { Key = "Path", Data = new DetailsLink(note.AbsolutePath) },
            ],
        };
    }

    public static IContextItem[] BuildMoreCommands(ObsidianVaultStore store, ObsidianSettingsStore settings, ObsidianNote note)
    {
        return
        [
            new CommandContextItem(new OpenInObsidianBySettingsCommand(note, settings)) { RequestedShortcut = OpenInObsidian },
            new CommandContextItem(new OpenInEditorCommand(store, note.AbsolutePath)) { Icon = ObsidianVisuals.Edit },
            new CommandContextItem(new RevealNoteCommand(note.AbsolutePath)) { Icon = ObsidianVisuals.Folder, RequestedShortcut = Reveal },
            new Separator(),
            new CommandContextItem(new CopyObsidianUriCommand(note, settings)) { RequestedShortcut = CopyUri },
            new CommandContextItem(new CopyMarkdownLinkCommand(note, settings)) { RequestedShortcut = CopyMarkdownLink },
            new Separator(),
            new CommandContextItem(new QuickAppendPage(store, note))  { Icon = ObsidianVisuals.Append, RequestedShortcut = QuickAppend },
            new Separator(),
            new CommandContextItem(new TogglePinCommand(store, note)) { Icon = ObsidianVisuals.Pin, RequestedShortcut = Pin },
        ];
    }

    private static string BuildSubtitle(ObsidianNote note)
    {
        string prefix = note.IsPinned ? "Pinned" : (System.IO.Path.GetDirectoryName(note.RelativePath) ?? "");
        string snippet = string.IsNullOrWhiteSpace(note.Snippet)
            ? System.IO.Path.GetFileName(note.AbsolutePath)
            : note.Snippet;
        return string.IsNullOrWhiteSpace(prefix) ? snippet : $"{prefix} | {snippet}";
    }

    private static Tag[] BuildTags(ObsidianNote note)
    {
        var tags = new List<Tag>();
        if (note.Tags.Count > 0)
            tags.Add(ObsidianVisuals.VaultTag(note.Tags[0]));
        if (note.IsPinned)
            tags.Add(ObsidianVisuals.StatusTag("pinned"));
        return [.. tags];
    }
}
