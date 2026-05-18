using System;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.Clipboard.Pages;
using NpuTools.Clipboard.Services;
using NpuTools.Clipboard.Data;

namespace NpuTools.Clipboard;

internal sealed partial class NpuClipboardCommandsProvider : CommandProvider
{
    private readonly ClipboardStore _store = new();
    private readonly ClipboardSettingsStore _settings = new();
    private readonly ClipboardContentService _content = new();
    private readonly ClipboardAskService _ask = new();
    private readonly ClipboardSettingsManager _settingsManager;
    private readonly ICommandItem[] _commands;

    public NpuClipboardCommandsProvider()
    {
        Id = "com.local.nputools.clipboard";
        DisplayName = "NPU Clipboard";
        Icon = ClipboardVisuals.Clipboard;
        _settingsManager = new ClipboardSettingsManager(_settings, _store);
        Settings = _settingsManager.Settings;

        _commands =
        [
            new CommandItem(new ClipboardHistoryPage(_store, _settings, _content))
            {
                Title = "Clipboard History",
                Subtitle = "Search and paste local clipboard entries",
                Icon = ClipboardVisuals.Clipboard,
                MoreCommands =
                [
                    new CommandContextItem(new Commands.StartStopRecorderCommand()),
                    new Separator(),
                    new CommandContextItem(new Commands.DeleteByWindowCommand(_store, TimeSpan.FromMinutes(5), "Last 5 Minutes")) { IsCritical = true },
                    new CommandContextItem(new Commands.DeleteByWindowCommand(_store, TimeSpan.FromMinutes(15), "Last 15 Minutes")) { IsCritical = true },
                    new CommandContextItem(new Commands.DeleteByWindowCommand(_store, TimeSpan.FromMinutes(30), "Last 30 Minutes")) { IsCritical = true },
                    new CommandContextItem(new Commands.DeleteByWindowCommand(_store, TimeSpan.FromHours(1), "Last Hour")) { IsCritical = true },
                    new CommandContextItem(new Commands.DeleteByWindowCommand(_store, TimeSpan.FromHours(24), "Last 24 Hours")) { IsCritical = true },
                    new Separator(),
                    new CommandContextItem(new DeleteAllPage(_store)) { Icon = ClipboardVisuals.Delete, IsCritical = true },
                ],
            },
            new CommandItem(new AskClipboardPage(_store, _settings, _content, _ask))
            {
                Title = "Ask Clipboard",
                Subtitle = "Natural-language search over text and image OCR history",
                Icon = ClipboardVisuals.Search,
            },
            new CommandItem(new ClipboardHistoryPage(_store, _settings, _content, ClipboardEntryKind.Text))  { Title = "Clipboard Text",   Subtitle = "Browse and paste saved text entries",         Icon = ClipboardVisuals.Text  },
            new CommandItem(new ClipboardHistoryPage(_store, _settings, _content, ClipboardEntryKind.Image)) { Title = "Clipboard Images", Subtitle = "Browse and paste saved image entries",        Icon = ClipboardVisuals.Image },
            new CommandItem(new ClipboardHistoryPage(_store, _settings, _content, ClipboardEntryKind.Files)) { Title = "Clipboard Files",  Subtitle = "Browse saved file path entries",              Icon = ClipboardVisuals.File  },
            new CommandItem(new ClipboardHistoryPage(_store, _settings, _content, ClipboardEntryKind.Link))  { Title = "Clipboard Links",  Subtitle = "Browse saved URL entries",                    Icon = ClipboardVisuals.Link  },
            new CommandItem(new ClipboardHistoryPage(_store, _settings, _content, ClipboardEntryKind.Email)) { Title = "Clipboard Emails", Subtitle = "Browse saved email address entries",          Icon = ClipboardVisuals.Mail  },
            new CommandItem(new ClipboardHistoryPage(_store, _settings, _content, ClipboardEntryKind.Color)) { Title = "Clipboard Colors", Subtitle = "Browse saved color code entries",             Icon = ClipboardVisuals.Color },
        ];
    }

    public override ICommandItem[] TopLevelCommands() => _commands;
}
