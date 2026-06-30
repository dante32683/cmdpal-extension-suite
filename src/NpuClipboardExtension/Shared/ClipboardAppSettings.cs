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

    // When true, captured text (and entries arriving via cross-device sync) are
    // checked against the SecretPatterns list. A match silently drops the entry
    // from local history and from the sync folder so the secret never lands on disk.
    [JsonPropertyName("secretDetectionEnabled")]
    public bool SecretDetectionEnabled { get; set; } = true;

    [JsonPropertyName("secretPatterns")]
    public List<SecretPattern> SecretPatterns { get; set; } = DefaultSecretPatterns.Copy();

    public int NormalizedRetentionLimit => RetentionLimit < 0 ? -1 : RetentionLimit;
}
