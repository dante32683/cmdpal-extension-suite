namespace NpuTools.Obsidian.Models;

internal sealed class ObsidianVaultSettings
{
    public const int DefaultMaxRecentNotes = 12;
    public const int DefaultMaxSearchResults = 50;

    public string VaultPath { get; set; } = "";

    public string VaultName { get; set; } = "";

    public string DailyNotesFolder { get; set; } = "";

    public string DefaultNewNoteFolder { get; set; } = "";

    public bool OpenAfterCreate { get; set; } = true;

    public int MaxRecentNotes { get; set; } = DefaultMaxRecentNotes;

    public int MaxSearchResults { get; set; } = DefaultMaxSearchResults;
}
