using System;
using System.IO;

namespace NpuTools.Notes.Models;

internal sealed class NotesAppSettings
{
    public const int DefaultMaxRecentNotes = 12;
    public const int DefaultMaxSearchResults = 50;

    public string NotesRoot { get; set; } = DefaultNotesRoot();

    public string DefaultCategory { get; set; } = "misc";

    public bool OpenAfterCreate { get; set; } = true;

    public bool AiCleanupOnCreate { get; set; } = true;

    public int MaxRecentNotes { get; set; } = DefaultMaxRecentNotes;

    public int MaxSearchResults { get; set; } = DefaultMaxSearchResults;

    public static string DefaultNotesRoot()
    {
        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Path.Combine(documents, "NpuNotes");
    }
}
