using System;
using System.Globalization;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.Obsidian.Models;

namespace NpuTools.Obsidian.Services;

internal sealed class ObsidianSettingsManager : JsonSettingsManager
{
    private const string Namespace = "npuObsidian";

    private static string Namespaced(string propertyName) => $"{Namespace}.{propertyName}";

    private readonly ObsidianSettingsStore _runtimeSettings;
    private readonly TextSetting _vaultPath;
    private readonly TextSetting _vaultName;
    private readonly TextSetting _dailyNotesFolder;
    private readonly TextSetting _defaultNewNoteFolder;
    private readonly ToggleSetting _openAfterCreate;
    private readonly ChoiceSetSetting _maxRecentNotes;
    private readonly ChoiceSetSetting _maxSearchResults;

    public ObsidianSettingsManager(ObsidianSettingsStore runtimeSettings)
    {
        _runtimeSettings = runtimeSettings;
        var current = _runtimeSettings.Current;
        FilePath = ObsidianPaths.CommandPaletteSettingsPath();

        _vaultPath = new TextSetting(
            Namespaced(nameof(ObsidianVaultSettings.VaultPath)),
            "Vault path",
            "Absolute path to your Obsidian vault folder.",
            current.VaultPath)
        {
            Placeholder = @"C:\Users\you\Documents\MyVault",
        };

        _vaultName = new TextSetting(
            Namespaced(nameof(ObsidianVaultSettings.VaultName)),
            "Vault name",
            "Obsidian vault name used in URIs. Leave blank to use the folder name.",
            current.VaultName)
        {
            Placeholder = "MyVault",
        };

        _dailyNotesFolder = new TextSetting(
            Namespaced(nameof(ObsidianVaultSettings.DailyNotesFolder)),
            "Daily notes folder",
            "Relative path inside the vault where daily notes are stored.",
            current.DailyNotesFolder)
        {
            Placeholder = "Daily Notes",
        };

        _defaultNewNoteFolder = new TextSetting(
            Namespaced(nameof(ObsidianVaultSettings.DefaultNewNoteFolder)),
            "New note folder",
            "Relative path inside the vault for new notes created from Command Palette. Leave blank for vault root.",
            current.DefaultNewNoteFolder)
        {
            Placeholder = "",
        };

        _openAfterCreate = new ToggleSetting(
            Namespaced(nameof(ObsidianVaultSettings.OpenAfterCreate)),
            "Open in Obsidian after create",
            "Open the newly created note in Obsidian immediately.",
            current.OpenAfterCreate);

        _maxRecentNotes = new ChoiceSetSetting(
            Namespaced(nameof(ObsidianVaultSettings.MaxRecentNotes)),
            "Recent notes",
            "Number of pinned and recent notes shown in the hub.",
            [
                new ChoiceSetSetting.Choice("8 notes", "8"),
                new ChoiceSetSetting.Choice("12 notes", "12"),
                new ChoiceSetSetting.Choice("20 notes", "20"),
                new ChoiceSetSetting.Choice("40 notes", "40"),
            ])
        {
            Value = NormalizeRecentValue(current.MaxRecentNotes),
        };

        _maxSearchResults = new ChoiceSetSetting(
            Namespaced(nameof(ObsidianVaultSettings.MaxSearchResults)),
            "Search results",
            "Maximum number of search results.",
            [
                new ChoiceSetSetting.Choice("25 results", "25"),
                new ChoiceSetSetting.Choice("50 results", "50"),
                new ChoiceSetSetting.Choice("100 results", "100"),
                new ChoiceSetSetting.Choice("150 results", "150"),
            ])
        {
            Value = NormalizeSearchValue(current.MaxSearchResults),
        };

        Settings.Add(_vaultPath);
        Settings.Add(_vaultName);
        Settings.Add(_dailyNotesFolder);
        Settings.Add(_defaultNewNoteFolder);
        Settings.Add(_openAfterCreate);
        Settings.Add(_maxRecentNotes);
        Settings.Add(_maxSearchResults);

        LoadSettings();
        ApplyToRuntimeSettings();

        Settings.SettingsChanged += (_, _) =>
        {
            SaveSettings();
            ApplyToRuntimeSettings();
        };
    }

    private void ApplyToRuntimeSettings()
    {
        _runtimeSettings.Update(settings =>
        {
            settings.VaultPath = _vaultPath.Value?.Trim() ?? string.Empty;
            settings.VaultName = _vaultName.Value?.Trim() ?? string.Empty;
            settings.DailyNotesFolder = _dailyNotesFolder.Value?.Trim() ?? string.Empty;
            settings.DefaultNewNoteFolder = _defaultNewNoteFolder.Value?.Trim() ?? string.Empty;
            settings.OpenAfterCreate = _openAfterCreate.Value;
            settings.MaxRecentNotes = ParseInt(_maxRecentNotes.Value, ObsidianVaultSettings.DefaultMaxRecentNotes);
            settings.MaxSearchResults = ParseInt(_maxSearchResults.Value, ObsidianVaultSettings.DefaultMaxSearchResults);
        });
    }

    private static int ParseInt(string? value, int fallback)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) ? parsed : fallback;
    }

    private static string NormalizeRecentValue(int value)
    {
        return value is 8 or 12 or 20 or 40 ? value.ToString(CultureInfo.InvariantCulture) : "12";
    }

    private static string NormalizeSearchValue(int value)
    {
        return value is 25 or 50 or 100 or 150 ? value.ToString(CultureInfo.InvariantCulture) : "50";
    }
}
