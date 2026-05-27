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
    public static ListItem Build(
        ObsidianVaultStore store,
        ObsidianIndexStore indexStore,
        ObsidianSettingsStore settings,
        ObsidianAiService ai,
        ObsidianNote note)
    {
        return new ListItem(new NotePreviewPage(store, indexStore, settings, ai, note.AbsolutePath))
        {
            Title = note.Title,
            Subtitle = BuildSubtitle(note),
            Icon = ObsidianVisuals.Note,
            Tags = BuildTags(note),
            MoreCommands = BuildMoreCommands(store, indexStore, settings, ai, note),
            Details = BuildDetails(note),
        };
    }

    public static Details BuildDetails(ObsidianNote note)
    {
        var metadata = new List<DetailsElement>
        {
            new() { Key = "Folder",   Data = new DetailsLink(System.IO.Path.GetDirectoryName(note.RelativePath) ?? "") },
            new() { Key = "Modified", Data = new DetailsLink(note.LastModifiedUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)) },
            new() { Key = "Tags",     Data = new DetailsLink(note.Tags.Count > 0 ? string.Join(", ", note.Tags) : "none") },
        };

        if (note.Backlinks.Count > 0)
            metadata.Add(new DetailsElement { Key = "Backlinks", Data = new DetailsLink($"{note.Backlinks.Count} note{(note.Backlinks.Count == 1 ? "" : "s")} link here") });

        if (!string.IsNullOrWhiteSpace(note.AiSummary))
            metadata.Add(new DetailsElement { Key = "Summary", Data = new DetailsLink(note.AiSummary) });

        metadata.Add(new DetailsElement { Key = "Path", Data = new DetailsLink(note.AbsolutePath) });

        return new Details
        {
            Title = note.Title,
            Body = string.IsNullOrWhiteSpace(note.Body) ? "_Empty note_" : note.Body,
            Size = ContentSize.Large,
            Metadata = [.. metadata],
        };
    }

    public static IContextItem[] BuildMoreCommands(
        ObsidianVaultStore store,
        ObsidianIndexStore indexStore,
        ObsidianSettingsStore settings,
        ObsidianAiService ai,
        ObsidianNote note)
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
            new CommandContextItem(new QuickAppendPage(store, note)) { Icon = ObsidianVisuals.Append, RequestedShortcut = QuickAppend },
            new Separator(),
            new CommandContextItem(new SummarizeNotePage(note, ai, indexStore)) { Icon = ObsidianVisuals.Ai },
            new CommandContextItem(new FindRelatedNotesPage(note, store, indexStore, settings, ai)) { Icon = ObsidianVisuals.Related },
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
