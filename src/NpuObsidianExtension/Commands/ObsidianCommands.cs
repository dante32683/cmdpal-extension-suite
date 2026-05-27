using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.Obsidian.Models;
using NpuTools.Obsidian.Services;
using Windows.ApplicationModel.DataTransfer;

namespace NpuTools.Obsidian.Commands;

internal sealed partial class OpenInObsidianCommand : InvokableCommand
{
    private readonly ObsidianNote _note;

    public OpenInObsidianCommand(ObsidianNote note)
    {
        _note = note;
        Name = "Open in Obsidian";
        Icon = ObsidianVisuals.Open;
    }

    public override CommandResult Invoke()
    {
        LaunchUri(ObsidianUriService.OpenNote(_note.VaultPath == _note.AbsolutePath ? "" : Path.GetFileName(_note.VaultPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)), _note.RelativePath));
        return CommandResult.Dismiss();
    }

    internal static void LaunchUri(string uri)
    {
        try
        {
            Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"LaunchUri failed for {uri}: {ex.GetType().Name}: {ex.Message}");
        }
    }
}

internal sealed partial class OpenInObsidianBySettingsCommand : InvokableCommand
{
    private readonly ObsidianNote _note;
    private readonly ObsidianSettingsStore _settings;

    public OpenInObsidianBySettingsCommand(ObsidianNote note, ObsidianSettingsStore settings)
    {
        _note = note;
        _settings = settings;
        Name = "Open in Obsidian";
        Icon = ObsidianVisuals.Open;
    }

    public override CommandResult Invoke()
    {
        string vaultName = ResolveVaultName(_settings.Current, _note.VaultPath);
        OpenInObsidianCommand.LaunchUri(ObsidianUriService.OpenNote(vaultName, _note.RelativePath));
        return CommandResult.Dismiss();
    }

    internal static string ResolveVaultName(ObsidianVaultSettings settings, string vaultPath)
    {
        if (!string.IsNullOrWhiteSpace(settings.VaultName))
            return settings.VaultName;

        return Path.GetFileName(vaultPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
    }
}

internal sealed partial class OpenInEditorCommand : InvokableCommand
{
    private readonly ObsidianVaultStore _store;
    private readonly string _path;

    public OpenInEditorCommand(ObsidianVaultStore store, string path)
    {
        _store = store;
        _path = path;
        Name = "Open in Editor";
        Icon = ObsidianVisuals.Edit;
    }

    public override CommandResult Invoke()
    {
        try
        {
            var note = _store.GetByPath(_path);
            if (note is not null)
                _store.RecordOpened(note);

            Process.Start(new ProcessStartInfo(_path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"OpenInEditorCommand failed: {ex.GetType().Name}: {ex.Message}");
        }

        return CommandResult.Dismiss();
    }
}

internal sealed partial class RevealNoteCommand : InvokableCommand
{
    private readonly string _path;

    public RevealNoteCommand(string path)
    {
        _path = path;
        Name = "Reveal in Explorer";
        Icon = ObsidianVisuals.Folder;
    }

    public override CommandResult Invoke()
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{_path}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"RevealNoteCommand failed: {ex.GetType().Name}: {ex.Message}");
        }

        return CommandResult.Dismiss();
    }
}

internal sealed partial class OpenVaultFolderCommand : InvokableCommand
{
    private readonly ObsidianSettingsStore _settings;

    public OpenVaultFolderCommand(ObsidianSettingsStore settings)
    {
        _settings = settings;
        Name = "Open Vault Folder";
        Icon = ObsidianVisuals.Folder;
    }

    public override CommandResult Invoke()
    {
        try
        {
            string vault = _settings.Current.VaultPath;
            if (!string.IsNullOrWhiteSpace(vault) && Directory.Exists(vault))
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{vault}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"OpenVaultFolderCommand failed: {ex.GetType().Name}: {ex.Message}");
        }

        return CommandResult.Dismiss();
    }
}

internal sealed partial class CopyObsidianUriCommand : InvokableCommand
{
    private readonly ObsidianNote _note;
    private readonly ObsidianSettingsStore _settings;

    public CopyObsidianUriCommand(ObsidianNote note, ObsidianSettingsStore settings)
    {
        _note = note;
        _settings = settings;
        Name = "Copy Obsidian URI";
        Icon = ObsidianVisuals.Copy;
    }

    public override CommandResult Invoke()
    {
        string vaultName = OpenInObsidianBySettingsCommand.ResolveVaultName(_settings.Current, _note.VaultPath);
        string uri = ObsidianUriService.OpenNote(vaultName, _note.RelativePath);
        SetClipboard(uri);
        return CommandResult.ShowToast("Obsidian URI copied.");
    }

    private static void SetClipboard(string text)
    {
        var package = new DataPackage();
        package.SetText(text);
        Clipboard.SetContent(package);
    }
}

internal sealed partial class CopyMarkdownLinkCommand : InvokableCommand
{
    private readonly ObsidianNote _note;
    private readonly ObsidianSettingsStore _settings;

    public CopyMarkdownLinkCommand(ObsidianNote note, ObsidianSettingsStore settings)
    {
        _note = note;
        _settings = settings;
        Name = "Copy Markdown Link";
        Icon = ObsidianVisuals.Link;
    }

    public override CommandResult Invoke()
    {
        string vaultName = OpenInObsidianBySettingsCommand.ResolveVaultName(_settings.Current, _note.VaultPath);
        string link = ObsidianUriService.MarkdownLink(_note.Title, vaultName, _note.RelativePath);
        var package = new DataPackage();
        package.SetText(link);
        Clipboard.SetContent(package);
        return CommandResult.ShowToast("Markdown link copied.");
    }
}

internal sealed partial class TogglePinCommand : InvokableCommand
{
    private readonly ObsidianVaultStore _store;
    private readonly string _path;
    private readonly bool _pin;

    public TogglePinCommand(ObsidianVaultStore store, ObsidianNote note)
    {
        _store = store;
        _path = note.AbsolutePath;
        _pin = !note.IsPinned;
        Name = _pin ? "Pin Note" : "Unpin Note";
        Icon = ObsidianVisuals.Pin;
    }

    public override CommandResult Invoke()
    {
        var note = _store.GetByPath(_path);
        if (note is null)
            return CommandResult.ShowToast("Note no longer exists.");

        _store.SetPinned(note, _pin);
        return CommandResult.ShowToast(_pin ? "Note pinned." : "Note unpinned.");
    }
}

internal sealed partial class OpenDailyNoteCommand : InvokableCommand
{
    private readonly ObsidianSettingsStore _settings;

    public OpenDailyNoteCommand(ObsidianSettingsStore settings)
    {
        _settings = settings;
        Name = "Open Daily Note";
        Icon = ObsidianVisuals.Daily;
    }

    public override CommandResult Invoke()
    {
        var current = _settings.Current;
        if (string.IsNullOrWhiteSpace(current.VaultPath))
            return CommandResult.ShowToast("Vault path not configured. Open settings.");

        string vaultName = OpenInObsidianBySettingsCommand.ResolveVaultName(current, current.VaultPath);
        OpenInObsidianCommand.LaunchUri(ObsidianUriService.DailyNote(vaultName));
        return CommandResult.Dismiss();
    }
}

internal sealed partial class OpenNewNoteInObsidianCommand : InvokableCommand
{
    private readonly ObsidianSettingsStore _settings;

    public OpenNewNoteInObsidianCommand(ObsidianSettingsStore settings)
    {
        _settings = settings;
        Name = "Open New Note in Obsidian";
        Icon = ObsidianVisuals.Add;
    }

    public override CommandResult Invoke()
    {
        var current = _settings.Current;
        if (string.IsNullOrWhiteSpace(current.VaultPath))
            return CommandResult.ShowToast("Vault path not configured. Open settings.");

        string vaultName = OpenInObsidianBySettingsCommand.ResolveVaultName(current, current.VaultPath);
        OpenInObsidianCommand.LaunchUri(ObsidianUriService.NewNote(vaultName, current.DefaultNewNoteFolder));
        return CommandResult.Dismiss();
    }
}

internal sealed partial class CreateNoteAndOpenCommand : InvokableCommand
{
    private readonly ObsidianVaultStore _store;
    private readonly ObsidianSettingsStore _settings;
    private readonly string _title;
    private readonly string _body;

    public CreateNoteAndOpenCommand(ObsidianVaultStore store, ObsidianSettingsStore settings, string title, string body)
    {
        _store = store;
        _settings = settings;
        _title = title;
        _body = body;
        Name = string.IsNullOrWhiteSpace(title) ? "Create Blank Note" : $"Create: {title}";
        Icon = ObsidianVisuals.Add;
    }

    public override CommandResult Invoke()
    {
        _ = Task.Run(CreateAsync);
        return CommandResult.ShowToast("Creating note...");
    }

    private async Task CreateAsync()
    {
        try
        {
            var note = _store.Create(_title, _body);
            if (_settings.Current.OpenAfterCreate)
            {
                string vaultName = OpenInObsidianBySettingsCommand.ResolveVaultName(_settings.Current, note.VaultPath);
                OpenInObsidianCommand.LaunchUri(ObsidianUriService.OpenNote(vaultName, note.RelativePath));
            }

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"CreateNoteAndOpenCommand failed: {ex.GetType().Name}: {ex.Message}");
        }
    }
}

internal sealed partial class AppendToNoteCommand : InvokableCommand
{
    private readonly ObsidianNote _note;
    private readonly string _text;

    public AppendToNoteCommand(ObsidianNote note, string text)
    {
        _note = note;
        _text = text;
        Name = "Append to Note";
        Icon = ObsidianVisuals.Append;
    }

    public override CommandResult Invoke()
    {
        if (string.IsNullOrWhiteSpace(_text))
            return CommandResult.ShowToast("Nothing to append.");

        try
        {
            ObsidianVaultStore.AppendToNote(_note, _text);
            return CommandResult.ShowToast("Appended to note.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"AppendToNoteCommand failed: {ex.GetType().Name}: {ex.Message}");
            return CommandResult.ShowToast("Could not append to note.");
        }
    }
}
