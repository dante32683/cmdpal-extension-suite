using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.Obsidian.Commands;
using NpuTools.Obsidian.Services;
using static NpuTools.Obsidian.KeyChords;

namespace NpuTools.Obsidian.Pages;

internal sealed partial class NotePreviewPage : ListPage
{
    private readonly ObsidianVaultStore _store;
    private readonly ObsidianIndexStore _indexStore;
    private readonly ObsidianSettingsStore _settings;
    private readonly ObsidianAiService _ai;
    private readonly string _path;

    public NotePreviewPage(
        ObsidianVaultStore store,
        ObsidianIndexStore indexStore,
        ObsidianSettingsStore settings,
        ObsidianAiService ai,
        string path)
    {
        _store = store;
        _indexStore = indexStore;
        _settings = settings;
        _ai = ai;
        _path = path;
        Id = "com.local.nputools.obsidian.preview";
        Title = "Note";
        Name = "Open";
        Icon = ObsidianVisuals.Note;
        ShowDetails = true;
    }

    public override IListItem[] GetItems()
    {
        var note = _store.GetByPath(_path);
        if (note is null)
        {
            return
            [
                new ListItem(new NoOpCommand())
                {
                    Title = "Note no longer exists",
                    Subtitle = _path,
                    Icon = ObsidianVisuals.Warning,
                },
            ];
        }

        _store.RecordOpened(note);
        Title = note.Title;

        return
        [
            new ListItem(new OpenInObsidianBySettingsCommand(note, _settings))
            {
                Title = "Open in Obsidian",
                Subtitle = note.AbsolutePath,
                Icon = ObsidianVisuals.Open,
                Details = NoteItemFactory.BuildDetails(note),
                MoreCommands = NoteItemFactory.BuildMoreCommands(_store, _indexStore, _settings, _ai, note),
            },
            new ListItem(new OpenInEditorCommand(_store, note.AbsolutePath))
            {
                Title = "Open in Editor",
                Subtitle = note.AbsolutePath,
                Icon = ObsidianVisuals.Edit,
            },
            new ListItem(new QuickAppendPage(_store, note))
            {
                Title = "Quick Append",
                Subtitle = "Add text to the end of this note",
                Icon = ObsidianVisuals.Append,
                MoreCommands = [new CommandContextItem(new QuickAppendPage(_store, note)) { RequestedShortcut = QuickAppend }],
            },
            new ListItem(new SummarizeNotePage(note, _ai, _indexStore))
            {
                Title = "Summarize Note",
                Subtitle = string.IsNullOrWhiteSpace(note.AiSummary)
                    ? "Generate a 1–2 sentence Phi summary"
                    : "Regenerate summary",
                Icon = ObsidianVisuals.Ai,
                Tags = string.IsNullOrWhiteSpace(note.AiSummary) ? [] : [ObsidianVisuals.StatusTag("summarized")],
            },
            new ListItem(new FindRelatedNotesPage(note, _store, _indexStore, _settings, _ai))
            {
                Title = "Find Related Notes",
                Subtitle = "Discover linked and thematically similar notes",
                Icon = ObsidianVisuals.Related,
            },
            new ListItem(new CopyObsidianUriCommand(note, _settings))
            {
                Title = "Copy Obsidian URI",
                Subtitle = note.RelativePath,
                Icon = ObsidianVisuals.Copy,
                MoreCommands = [new CommandContextItem(new CopyObsidianUriCommand(note, _settings)) { RequestedShortcut = CopyUri }],
            },
            new ListItem(new CopyMarkdownLinkCommand(note, _settings))
            {
                Title = "Copy Markdown Link",
                Subtitle = $"[{note.Title}](...)",
                Icon = ObsidianVisuals.Link,
                MoreCommands = [new CommandContextItem(new CopyMarkdownLinkCommand(note, _settings)) { RequestedShortcut = CopyMarkdownLink }],
            },
            new ListItem(new TogglePinCommand(_store, note))
            {
                Title = note.IsPinned ? "Unpin Note" : "Pin Note",
                Icon = ObsidianVisuals.Pin,
                Tags = note.IsPinned ? [ObsidianVisuals.StatusTag("pinned")] : [],
                MoreCommands = [new CommandContextItem(new TogglePinCommand(_store, note)) { RequestedShortcut = Pin }],
            },
            new ListItem(new RevealNoteCommand(note.AbsolutePath))
            {
                Title = "Reveal in Explorer",
                Subtitle = note.AbsolutePath,
                Icon = ObsidianVisuals.Folder,
                MoreCommands = [new CommandContextItem(new RevealNoteCommand(note.AbsolutePath)) { RequestedShortcut = Reveal }],
            },
        ];
    }
}
