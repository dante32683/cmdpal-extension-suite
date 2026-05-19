using Microsoft.CommandPalette.Extensions.Toolkit;

namespace NpuTools.Awake.Services;

internal sealed class AwakeSettingsManager : JsonSettingsManager
{
    private const string DefaultAwakeModeKey = "defaultAwakeMode";
    private const string IndefiniteMode = "indefinite";
    private const string ScreenOffMode = "screen-off";

    private readonly ChoiceSetSetting _defaultAwakeMode;

    public AwakeSettingsManager(AwakeService awakeService)
    {
        FilePath = AwakePaths.SettingsPath;

        var current = awakeService.GetSettings();
        _defaultAwakeMode = new ChoiceSetSetting(
            DefaultAwakeModeKey,
            "Default Awake mode",
            "Mode used by the top-level Awake toggle.",
            [
                new ChoiceSetSetting.Choice("Keep PC and display awake", IndefiniteMode),
                new ChoiceSetSetting.Choice("Keep PC awake, allow display sleep", ScreenOffMode),
            ])
        {
            Value = NormalizeDefaultAwakeMode(current.DefaultAwakeMode),
        };

        Settings.Add(_defaultAwakeMode);

        LoadSettings();
        NormalizeLoadedValues();

        Settings.SettingsChanged += (_, _) =>
        {
            NormalizeLoadedValues();
            SaveSettings();
        };
    }

    private void NormalizeLoadedValues()
    {
        string normalized = NormalizeDefaultAwakeMode(_defaultAwakeMode.Value);
        if (_defaultAwakeMode.Value != normalized)
        {
            _defaultAwakeMode.Value = normalized;
        }
    }

    private static string NormalizeDefaultAwakeMode(string? mode) =>
        mode == ScreenOffMode ? ScreenOffMode : IndefiniteMode;
}
