using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.Notes.Commands;
using NpuTools.Notes.Models;
using NpuTools.Notes.Services;
using static NpuTools.Notes.KeyChords;

namespace NpuTools.Notes.Pages;

internal static class NoteItemFactory
{
    public static ListItem Build(NotesStore store, NoteEntry entry)
    {
        var item = new ListItem(new NoteDetailPage(store, entry.FilePath))
        {
            Title = entry.Title,
            Subtitle = BuildSubtitle(entry),
            Icon = NotesVisuals.Note,
            Tags = BuildTags(entry),
            MoreCommands = BuildMoreCommands(store, entry),
            Details = BuildDetails(entry),
        };

        return item;
    }

    public static Details BuildDetails(NoteEntry entry)
    {
        return new Details
        {
            Title = entry.Title,
            Body = string.IsNullOrWhiteSpace(entry.Body) ? "_Empty note_" : entry.Body,
            Size = ContentSize.Large,
            Metadata =
            [
                new DetailsElement { Key = "Category", Data = new DetailsLink(entry.Category) },
                new DetailsElement { Key = "Created", Data = new DetailsLink(entry.CreatedUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)) },
                new DetailsElement { Key = "Updated", Data = new DetailsLink(entry.UpdatedUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)) },
                new DetailsElement { Key = "Path", Data = new DetailsLink(entry.FilePath) },
            ],
        };
    }

    public static IContextItem[] BuildMoreCommands(NotesStore store, NoteEntry entry)
    {
        var items = new List<IContextItem>
        {
            new CommandContextItem(new OpenNoteCommand(store, entry.FilePath)) { Icon = NotesVisuals.Open },
            new CommandContextItem(new CopyTextCommand(entry.Body) { Name = "Copy Content", Icon = NotesVisuals.Copy }) { RequestedShortcut = CopyContent },
            new CommandContextItem(new CopyTextCommand(entry.FilePath) { Name = "Copy Path", Icon = NotesVisuals.Copy }) { RequestedShortcut = CopyPath },
            new CommandContextItem(new OpenNoteLocationCommand(entry.FilePath)) { Icon = NotesVisuals.Folder, RequestedShortcut = Reveal },
            new Separator(),
            new CommandContextItem(new TogglePinNoteCommand(store, entry)) { Icon = NotesVisuals.Pin, RequestedShortcut = Pin },
            new Separator(),
            new CommandContextItem(new DeleteNotePage(store, entry.FilePath)) { Icon = NotesVisuals.Delete, RequestedShortcut = Delete, IsCritical = true },
        };

        return [.. items];
    }

    private static string BuildSubtitle(NoteEntry entry)
    {
        string prefix = entry.IsPinned ? "Pinned" : entry.Category;
        string snippet = string.IsNullOrWhiteSpace(entry.Snippet) ? Path.GetFileName(entry.FilePath) : entry.Snippet;
        return $"{prefix} | {snippet}";
    }

    private static Tag[] BuildTags(NoteEntry entry)
    {
        var tags = new List<Tag> { NotesVisuals.MutedTag(entry.Category) };
        if (entry.IsPinned)
            tags.Add(NotesVisuals.StatusTag("pinned"));
        return [.. tags];
    }
}
