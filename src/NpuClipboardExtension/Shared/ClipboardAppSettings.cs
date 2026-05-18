using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NpuTools.Clipboard.Data;

public sealed class ClipboardAppSettings
{
    public const int DefaultRetentionLimit = 500;

    [JsonPropertyName("primaryAction")]
    public ClipboardPrimaryAction PrimaryAction { get; set; } = ClipboardPrimaryAction.Paste;

    [JsonPropertyName("retentionLimit")]
    public int RetentionLimit { get; set; } = DefaultRetentionLimit;

    [JsonPropertyName("disabledApplicationNames")]
    public List<string> DisabledApplicationNames { get; set; } = ["1Password", "Bitwarden", "KeePass", "LastPass", "Password", "Keychain"];

    [JsonPropertyName("pasteDelayMs")]
    public int PasteDelayMs { get; set; } = 250;

    [JsonPropertyName("recorderEnabled")]
    public bool RecorderEnabled { get; set; } = true;

    [JsonPropertyName("previewMode")]
    public ClipboardPreviewMode PreviewMode { get; set; } = ClipboardPreviewMode.Always;

    [JsonPropertyName("syncFolder")]
    public string? SyncFolder { get; set; }

    public int NormalizedRetentionLimit => RetentionLimit < 0 ? -1 : RetentionLimit;
}
