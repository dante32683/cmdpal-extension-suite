using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using NpuTools.Notes.Models;
using NpuTools.Notes.Shared;

namespace NpuTools.Notes.Services;

internal sealed class NotesSettingsStore
{
    private readonly object _lock = new();
    private NotesAppSettings _settings = new();

    public NotesSettingsStore()
    {
        Load();
    }

    public NotesAppSettings Current
    {
        get
        {
            lock (_lock)
                return Clone(_settings);
        }
    }

    public void Update(Action<NotesAppSettings> update)
    {
        lock (_lock)
        {
            update(_settings);
            Normalize(_settings);
            Save();
        }
    }

    private void Load()
    {
        try
        {
            string path = NotesPaths.SettingsPath();
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                _settings = JsonSerializer.Deserialize(json, NotesJsonContext.Default.NotesAppSettings) ?? new NotesAppSettings();
            }

            Normalize(_settings);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"NotesSettingsStore Load failed: {ex.GetType().Name}: {ex.Message}");
            _settings = new NotesAppSettings();
        }
    }

    private void Save()
    {
        try
        {
            string path = NotesPaths.SettingsPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string tmp = $"{path}.{Environment.ProcessId}.tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(_settings, NotesJsonContext.Default.NotesAppSettings));
            File.Move(tmp, path, true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"NotesSettingsStore Save failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void Normalize(NotesAppSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.NotesRoot))
            settings.NotesRoot = NotesAppSettings.DefaultNotesRoot();

        settings.NotesRoot = Environment.ExpandEnvironmentVariables(settings.NotesRoot.Trim());
        settings.DefaultCategory = NotesStore.NormalizeCategory(settings.DefaultCategory);

        if (settings.MaxRecentNotes is < 5 or > 100)
            settings.MaxRecentNotes = NotesAppSettings.DefaultMaxRecentNotes;

        if (settings.MaxSearchResults is < 10 or > 200)
            settings.MaxSearchResults = NotesAppSettings.DefaultMaxSearchResults;
    }

    private static NotesAppSettings Clone(NotesAppSettings s) => new()
    {
        NotesRoot = s.NotesRoot,
        DefaultCategory = s.DefaultCategory,
        OpenAfterCreate = s.OpenAfterCreate,
        MaxRecentNotes = s.MaxRecentNotes,
        MaxSearchResults = s.MaxSearchResults,
    };
}
