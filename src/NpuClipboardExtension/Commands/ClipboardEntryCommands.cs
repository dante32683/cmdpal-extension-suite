using System;
using System.Diagnostics;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.Clipboard.Services;
using NpuTools.Clipboard.Data;

namespace NpuTools.Clipboard.Commands;

internal sealed partial class CopyEntryCommand : InvokableCommand
{
    private readonly ClipboardStore _store;
    private readonly ClipboardSettingsStore _settings;
    private readonly ClipboardContentService _content;
    private readonly string _id;
    private readonly bool _plainTextOnly;

    public CopyEntryCommand(ClipboardStore store, ClipboardSettingsStore settings, ClipboardContentService content, string id, bool plainTextOnly = false)
    {
        _store = store;
        _settings = settings;
        _content = content;
        _id = id;
        _plainTextOnly = plainTextOnly;
        Name = plainTextOnly ? "Copy as Plain Text" : "Copy to Clipboard";
        Icon = ClipboardVisuals.Copy;
    }

    public override CommandResult Invoke()
    {
        _ = System.Threading.Tasks.Task.Run(async () =>
        {
            try
            {
                var entry = _store.Get(_id);
                if (entry is null) return;
                await _content.CopyAsync(entry, _plainTextOnly).ConfigureAwait(false);
                _store.MarkUsed(_id, _settings.Current);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CopyEntryCommand failed: {ex.GetType().Name}: {ex.Message}");
            }
        });
        return CommandResult.Dismiss();
    }
}

internal sealed partial class PasteEntryCommand : InvokableCommand
{
    private readonly ClipboardStore _store;
    private readonly ClipboardSettingsStore _settings;
    private readonly ClipboardContentService _content;
    private readonly string _id;
    private readonly bool _plainTextOnly;

    public PasteEntryCommand(ClipboardStore store, ClipboardSettingsStore settings, ClipboardContentService content, string id, bool plainTextOnly = false)
    {
        _store = store;
        _settings = settings;
        _content = content;
        _id = id;
        _plainTextOnly = plainTextOnly;
        Name = plainTextOnly ? "Paste as Plain Text" : "Paste to Active App";
        Icon = ClipboardVisuals.Paste;
    }

    public override CommandResult Invoke()
    {
        _ = System.Threading.Tasks.Task.Run(async () =>
        {
            try
            {
                var entry = _store.Get(_id);
                if (entry is null) return;
                var settings = _settings.Current;
                await _content.PasteAsync(entry, _plainTextOnly, settings.PasteDelayMs).ConfigureAwait(false);
                _store.MarkUsed(_id, settings);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PasteEntryCommand failed: {ex.GetType().Name}: {ex.Message}");
            }
        });
        return CommandResult.Dismiss();
    }
}

internal sealed partial class DeleteEntryCommand : InvokableCommand
{
    private readonly ClipboardStore _store;
    private readonly string _id;

    public DeleteEntryCommand(ClipboardStore store, string id)
    {
        _store = store;
        _id = id;
        Name = "Delete Entry";
        Icon = ClipboardVisuals.Delete;
    }

    public override CommandResult Invoke()
    {
        _store.Delete(_id);
        return CommandResult.ShowToast("Clipboard entry deleted.");
    }
}

internal sealed partial class PinEntryCommand : InvokableCommand
{
    private readonly ClipboardStore _store;
    private readonly string _id;
    private readonly bool _pin;

    public PinEntryCommand(ClipboardStore store, string id, bool pin)
    {
        _store = store;
        _id = id;
        _pin = pin;
        Name = pin ? "Pin Entry" : "Unpin Entry";
        Icon = ClipboardVisuals.Pin;
    }

    public override CommandResult Invoke()
    {
        _store.SetPinned(_id, _pin);
        return CommandResult.ShowToast(_pin ? "Clipboard entry pinned." : "Clipboard entry unpinned.");
    }
}
