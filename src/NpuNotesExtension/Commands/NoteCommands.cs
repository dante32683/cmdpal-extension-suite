using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.Notes.Models;
using NpuTools.Notes.Services;
using Windows.ApplicationModel.DataTransfer;

namespace NpuTools.Notes.Commands;

internal sealed partial class OpenNoteCommand : InvokableCommand
{
    private readonly NotesStore _store;
    private readonly string _path;

    public OpenNoteCommand(NotesStore store, string path)
    {
        _store = store;
        _path = path;
        Name = "Open In Editor";
        Icon = NotesVisuals.Open;
    }

    public override CommandResult Invoke()
    {
        try
        {
            var entry = _store.GetByPath(_path);
            if (entry is not null)
                _store.RecordOpened(entry);

            Process.Start(new ProcessStartInfo(_path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"OpenNoteCommand failed: {ex.GetType().Name}: {ex.Message}");
        }

        return CommandResult.Dismiss();
    }
}

internal sealed partial class OpenNoteLocationCommand : InvokableCommand
{
    private readonly string _path;

    public OpenNoteLocationCommand(string path)
    {
        _path = path;
        Name = "Open File Location";
        Icon = NotesVisuals.Folder;
    }

    public override CommandResult Invoke()
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{_path}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"OpenNoteLocationCommand failed: {ex.GetType().Name}: {ex.Message}");
        }

        return CommandResult.Dismiss();
    }
}

internal sealed partial class OpenNotesFolderCommand : InvokableCommand
{
    private readonly NotesSettingsStore _settings;

    public OpenNotesFolderCommand(NotesSettingsStore settings)
    {
        _settings = settings;
        Name = "Open Notes Folder";
        Icon = NotesVisuals.Folder;
    }

    public override CommandResult Invoke()
    {
        try
        {
            string root = _settings.Current.NotesRoot;
            Directory.CreateDirectory(root);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{root}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"OpenNotesFolderCommand failed: {ex.GetType().Name}: {ex.Message}");
        }

        return CommandResult.Dismiss();
    }
}

internal sealed partial class TogglePinNoteCommand : InvokableCommand
{
    private readonly NotesStore _store;
    private readonly string _path;
    private readonly bool _pin;

    public TogglePinNoteCommand(NotesStore store, NoteEntry entry)
    {
        _store = store;
        _path = entry.FilePath;
        _pin = !entry.IsPinned;
        Name = _pin ? "Pin Note" : "Unpin Note";
        Icon = NotesVisuals.Pin;
    }

    public override CommandResult Invoke()
    {
        var entry = _store.GetByPath(_path);
        if (entry is null)
            return CommandResult.ShowToast("Note no longer exists.");

        _store.SetPinned(entry, _pin);
        return CommandResult.ShowToast(_pin ? "Note pinned." : "Note unpinned.");
    }
}

internal sealed partial class DeleteNoteCommand : InvokableCommand
{
    private readonly NotesStore _store;
    private readonly string _path;

    public DeleteNoteCommand(NotesStore store, string path)
    {
        _store = store;
        _path = path;
        Name = "Delete Note";
        Icon = NotesVisuals.Delete;
    }

    public override CommandResult Invoke()
    {
        var entry = _store.GetByPath(_path);
        if (entry is null)
            return CommandResult.ShowToast("Note no longer exists.");

        try
        {
            _store.DeleteToRecycleBin(entry);
            return CommandResult.ShowToast("Note moved to Recycle Bin.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"DeleteNoteCommand failed: {ex.GetType().Name}: {ex.Message}");
            return CommandResult.ShowToast("Could not delete note.");
        }
    }
}

internal sealed partial class CreateNoteCommand : InvokableCommand
{
    private readonly NotesStore _store;
    private readonly NotesSettingsStore _settings;
    private readonly NotesAiService _ai;
    private readonly string _text;
    private readonly string? _category;

    public CreateNoteCommand(NotesStore store, NotesSettingsStore settings, NotesAiService ai, string text, string? category = null)
    {
        _store = store;
        _settings = settings;
        _ai = ai;
        _text = text;
        _category = category;
        Name = string.IsNullOrWhiteSpace(text) ? "Create Blank Note" : "Create Note";
        Icon = NotesVisuals.Add;
    }

    public override CommandResult Invoke()
    {
        try
        {
            var settings = _settings.Current;
            var entry = _store.Create(_text, _category);
            if (settings.OpenAfterCreate)
                Process.Start(new ProcessStartInfo(entry.FilePath) { UseShellExecute = true });

            if (settings.AiCleanupOnCreate)
                _ = Task.Run(() => CleanupAsync(entry));

            return CommandResult.ShowToast($"Created {entry.Title}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"CreateNoteCommand failed: {ex.GetType().Name}: {ex.Message}");
            return CommandResult.ShowToast("Could not create note.");
        }
    }

    private async Task CleanupAsync(NoteEntry entry)
    {
        try
        {
            var (newTitle, newBody) = await _ai.CleanupNoteAsync(entry.Title, entry.Body);
            if (!string.Equals(newTitle, entry.Title, StringComparison.Ordinal) || !string.Equals(newBody, entry.Body, StringComparison.Ordinal))
                _store.UpdateNote(entry, newTitle, newBody);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"CreateNoteCommand cleanup failed: {ex.GetType().Name}: {ex.Message}");
        }
    }
}

internal sealed partial class RenameNoteCommand : InvokableCommand
{
    private readonly NotesStore _store;
    private readonly string _path;
    private readonly string _newTitle;

    public RenameNoteCommand(NotesStore store, string path, string newTitle)
    {
        _store = store;
        _path = path;
        _newTitle = newTitle;
        Name = "Rename Note";
        Icon = NotesVisuals.Note;
    }

    public override CommandResult Invoke()
    {
        if (string.IsNullOrWhiteSpace(_newTitle))
            return CommandResult.ShowToast("Title cannot be empty.");

        var entry = _store.GetByPath(_path);
        if (entry is null)
            return CommandResult.ShowToast("Note no longer exists.");

        try
        {
            var renamed = _store.RenameNote(entry, _newTitle);
            return CommandResult.ShowToast($"Renamed to \"{renamed.Title}\".");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RenameNoteCommand failed: {ex.GetType().Name}: {ex.Message}");
            return CommandResult.ShowToast("Could not rename note.");
        }
    }
}

internal sealed partial class MoveNoteCommand : InvokableCommand
{
    private readonly NotesStore _store;
    private readonly string _path;
    private readonly string _targetCategory;

    public MoveNoteCommand(NotesStore store, string path, string targetCategory)
    {
        _store = store;
        _path = path;
        _targetCategory = targetCategory;
        Name = "Move Note";
        Icon = NotesVisuals.Folder;
    }

    public override CommandResult Invoke()
    {
        var entry = _store.GetByPath(_path);
        if (entry is null)
            return CommandResult.ShowToast("Note no longer exists.");

        try
        {
            var moved = _store.MoveNote(entry, _targetCategory);
            return CommandResult.ShowToast($"Moved to \"{moved.Category}\".");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MoveNoteCommand failed: {ex.GetType().Name}: {ex.Message}");
            return CommandResult.ShowToast("Could not move note.");
        }
    }
}

internal sealed partial class RebuildNotesIndexCommand : InvokableCommand
{
    private readonly NotesStore _store;

    public RebuildNotesIndexCommand(NotesStore store)
    {
        _store = store;
        Name = "Rebuild Index";
        Icon = NotesVisuals.Refresh;
    }

    public override CommandResult Invoke()
    {
        var entries = _store.GetAll();
        return CommandResult.ShowToast($"Index rebuilt — {entries.Count} note(s) found.");
    }
}

internal sealed partial class CreateNoteFromClipboardCommand : InvokableCommand
{
    private readonly NotesStore _store;
    private readonly NotesSettingsStore _settings;

    public CreateNoteFromClipboardCommand(NotesStore store, NotesSettingsStore settings)
    {
        _store = store;
        _settings = settings;
        Name = "Create Note From Clipboard";
        Icon = NotesVisuals.Add;
    }

    public override CommandResult Invoke()
    {
        _ = Task.Run(CreateAsync);
        return CommandResult.ShowToast("Creating note from clipboard...");
    }

    private async Task CreateAsync()
    {
        try
        {
            DataPackageView content = Clipboard.GetContent();
            string text = content.Contains(StandardDataFormats.Text)
                ? await content.GetTextAsync()
                : string.Empty;

            var entry = _store.Create(text, _settings.Current.DefaultCategory);
            if (_settings.Current.OpenAfterCreate)
                Process.Start(new ProcessStartInfo(entry.FilePath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"CreateNoteFromClipboardCommand failed: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
