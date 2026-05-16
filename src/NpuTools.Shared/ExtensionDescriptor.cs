namespace NpuTools.Common;

public sealed record ExtensionDescriptor(
    string Key,
    string DisplayName,
    string ProviderId,
    string SettingsDirectoryName,
    string[] PlannedFeatures,
    string? DockBandCommandId = null,
    string? DockBandTitle = null);
