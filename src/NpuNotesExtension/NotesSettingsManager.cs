using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.Notes.Models;
using NpuTools.Notes.Services;

namespace NpuTools.Notes;

internal sealed class NotesSettingsManager : JsonSettingsManager
{
    private const string Namespace = "npuNotes";

    private static string Namespaced(string propertyName) => $"{Namespace}.{propertyName}";

    private readonly NotesSettingsStore _runtimeSettings;
    private readonly TextSetting _notesRoot;
    private readonly ChoiceSetSetting _defaultCategory;
    private readonly ToggleSetting _openAfterCreate;
    private readonly ChoiceSetSetting _maxRecentNotes;
    private readonly ChoiceSetSetting _maxSearchResults;

    public NotesSettingsManager(NotesSettingsStore runtimeSettings)
    {
        _runtimeSettings = runtimeSettings;
        var current = _runtimeSettings.Current;
        FilePath = NotesPaths.CommandPaletteSettingsPath();

        _notesRoot = new TextSetting(
            Namespaced(nameof(NotesAppSettings.NotesRoot)),
            "Notes folder",
            "Folder where Markdown notes are stored.",
            current.NotesRoot)
        {
            Placeholder = NotesAppSettings.DefaultNotesRoot(),
        };

        _defaultCategory = new ChoiceSetSetting(
            Namespaced(nameof(NotesAppSettings.DefaultCategory)),
            "Default category",
            "Category used when creating a note without choosing one.",
            BuildCategoryChoices())
        {
            Value = current.DefaultCategory,
        };

        _openAfterCreate = new ToggleSetting(
            Namespaced(nameof(NotesAppSettings.OpenAfterCreate)),
            "Open after create",
            "Open new notes in the default Markdown editor immediately after creation.",
            current.OpenAfterCreate);

        _maxRecentNotes = new ChoiceSetSetting(
            Namespaced(nameof(NotesAppSettings.MaxRecentNotes)),
            "Recent notes",
            "Number of recent notes shown in the hub and empty search.",
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
            Namespaced(nameof(NotesAppSettings.MaxSearchResults)),
            "Search results",
            "Maximum number of note search results.",
            [
                new ChoiceSetSetting.Choice("25 results", "25"),
                new ChoiceSetSetting.Choice("50 results", "50"),
                new ChoiceSetSetting.Choice("100 results", "100"),
                new ChoiceSetSetting.Choice("150 results", "150"),
            ])
        {
            Value = NormalizeSearchValue(current.MaxSearchResults),
        };

        Settings.Add(_notesRoot);
        Settings.Add(_defaultCategory);
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

    public NotesAppSettings Current => _runtimeSettings.Current;

    private void ApplyToRuntimeSettings()
    {
        _runtimeSettings.Update(settings =>
        {
            settings.NotesRoot = string.IsNullOrWhiteSpace(_notesRoot.Value) ? NotesAppSettings.DefaultNotesRoot() : _notesRoot.Value.Trim();
            settings.DefaultCategory = NotesStore.NormalizeCategory(_defaultCategory.Value);
            settings.OpenAfterCreate = _openAfterCreate.Value;
            settings.MaxRecentNotes = ParseInt(_maxRecentNotes.Value, NotesAppSettings.DefaultMaxRecentNotes);
            settings.MaxSearchResults = ParseInt(_maxSearchResults.Value, NotesAppSettings.DefaultMaxSearchResults);
        });

        Directory.CreateDirectory(_runtimeSettings.Current.NotesRoot);
    }

    private static List<ChoiceSetSetting.Choice> BuildCategoryChoices()
    {
        var choices = new List<ChoiceSetSetting.Choice>(NotesStore.KnownCategories.Count);
        for (int i = 0; i < NotesStore.KnownCategories.Count; i++)
        {
            string category = NotesStore.KnownCategories[i];
            choices.Add(new ChoiceSetSetting.Choice(TitleCase(category), category));
        }

        return choices;
    }

    private static string TitleCase(string value) => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(value);

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
