using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.Clipboard.Data;

namespace NpuTools.Clipboard;

internal sealed class ClipboardSettingsManager : JsonSettingsManager
{
    private const string Namespace = "npuClipboard";

    private static string Namespaced(string propertyName) => $"{Namespace}.{propertyName}";

    private readonly ClipboardSettingsStore _runtimeSettings;
    private readonly ClipboardStore _store;
    private readonly ToggleSetting _recorderEnabled;
    private readonly ChoiceSetSetting _primaryAction;
    private readonly ChoiceSetSetting _previewMode;
    private readonly ChoiceSetSetting _retentionLimit;
    private readonly ChoiceSetSetting _pasteDelay;
    private readonly TextSetting _disabledApplications;

    public ClipboardSettingsManager(ClipboardSettingsStore runtimeSettings, ClipboardStore store)
    {
        _runtimeSettings = runtimeSettings;
        _store = store;

        var current = _runtimeSettings.Current;
        FilePath = SettingsJsonPath();

        _recorderEnabled = new ToggleSetting(
            Namespaced(nameof(ClipboardAppSettings.RecorderEnabled)),
            "Record clipboard changes",
            "When disabled, the background recorder ignores new clipboard changes.",
            current.RecorderEnabled);

        _primaryAction = new ChoiceSetSetting(
            Namespaced(nameof(ClipboardAppSettings.PrimaryAction)),
            "Primary action",
            "Choose what Enter does on a clipboard entry.",
            [
                new ChoiceSetSetting.Choice("Paste to active app", ClipboardPrimaryAction.Paste.ToString("G")),
                new ChoiceSetSetting.Choice("Copy to clipboard", ClipboardPrimaryAction.Copy.ToString("G")),
            ])
        {
            Value = current.PrimaryAction.ToString("G"),
        };

        _previewMode = new ChoiceSetSetting(
            Namespaced(nameof(ClipboardAppSettings.PreviewMode)),
            "Preview panel",
            "Choose whether entry details open automatically or only when manually requested.",
            [
                new ChoiceSetSetting.Choice("Always show", ClipboardPreviewMode.Always.ToString("G")),
                new ChoiceSetSetting.Choice("Manual only", ClipboardPreviewMode.Manual.ToString("G")),
            ])
        {
            Value = current.PreviewMode.ToString("G"),
        };

        _retentionLimit = new ChoiceSetSetting(
            Namespaced(nameof(ClipboardAppSettings.RetentionLimit)),
            "Keep history by count",
            "Pinned entries are kept outside this limit. Unlimited keeps everything until manually deleted.",
            [
                new ChoiceSetSetting.Choice("200 entries", "200"),
                new ChoiceSetSetting.Choice("500 entries", "500"),
                new ChoiceSetSetting.Choice("1000 entries", "1000"),
                new ChoiceSetSetting.Choice("Unlimited", "-1"),
            ])
        {
            Value = NormalizeRetentionValue(current.NormalizedRetentionLimit),
        };

        _pasteDelay = new ChoiceSetSetting(
            Namespaced(nameof(ClipboardAppSettings.PasteDelayMs)),
            "Paste focus delay",
            "Delay between putting an entry on the clipboard and sending Ctrl+V to the active app.",
            [
                new ChoiceSetSetting.Choice("150 ms", "150"),
                new ChoiceSetSetting.Choice("250 ms", "250"),
                new ChoiceSetSetting.Choice("400 ms", "400"),
                new ChoiceSetSetting.Choice("600 ms", "600"),
            ])
        {
            Value = NormalizePasteDelayValue(current.PasteDelayMs),
        };

        _disabledApplications = new TextSetting(
            Namespaced(nameof(ClipboardAppSettings.DisabledApplicationNames)),
            "Disabled applications",
            "Comma-separated process names that should not be recorded.",
            string.Join(", ", current.DisabledApplicationNames))
        {
            Placeholder = "1Password, Bitwarden, KeePass",
        };

        Settings.Add(_recorderEnabled);
        Settings.Add(_primaryAction);
        Settings.Add(_previewMode);
        Settings.Add(_retentionLimit);
        Settings.Add(_pasteDelay);
        Settings.Add(_disabledApplications);

        LoadSettings();
        ApplyToRuntimeSettings();

        Settings.SettingsChanged += (_, _) =>
        {
            SaveSettings();
            ApplyToRuntimeSettings();
        };
    }

    public ClipboardAppSettings Current => _runtimeSettings.Current;

    private static string SettingsJsonPath()
    {
        Directory.CreateDirectory(ClipboardPaths.SupportDir());
        return Path.Combine(ClipboardPaths.SupportDir(), "command-palette.settings.json");
    }

    private void ApplyToRuntimeSettings()
    {
        _runtimeSettings.Update(settings =>
        {
            settings.RecorderEnabled = _recorderEnabled.Value;
            settings.PrimaryAction = ParseEnum(_primaryAction.Value, ClipboardPrimaryAction.Paste);
            settings.PreviewMode = ParseEnum(_previewMode.Value, ClipboardPreviewMode.Always);
            settings.RetentionLimit = ParseInt(_retentionLimit.Value, ClipboardAppSettings.DefaultRetentionLimit);
            settings.PasteDelayMs = ParseInt(_pasteDelay.Value, 250);
            settings.DisabledApplicationNames = ParseDisabledApplications(_disabledApplications.Value);
        });

        _store.EnforceRetention(_runtimeSettings.Current);
    }

    private static T ParseEnum<T>(string? value, T fallback)
        where T : struct
    {
        return Enum.TryParse(value, out T parsed) ? parsed : fallback;
    }

    private static int ParseInt(string? value, int fallback)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) ? parsed : fallback;
    }

    private static string NormalizeRetentionValue(int value)
    {
        return value switch
        {
            200 or 500 or 1000 or -1 => value.ToString(CultureInfo.InvariantCulture),
            _ => ClipboardAppSettings.DefaultRetentionLimit.ToString(CultureInfo.InvariantCulture),
        };
    }

    private static string NormalizePasteDelayValue(int value)
    {
        return value switch
        {
            150 or 250 or 400 or 600 => value.ToString(CultureInfo.InvariantCulture),
            _ => "250",
        };
    }

    private static List<string> ParseDisabledApplications(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return [];

        return value.Split([',', ';', '\n', '\r'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
