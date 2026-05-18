using System.Globalization;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.ImageEditor.Services;

namespace NpuTools.ImageEditor;

internal sealed class ImageEditorSettingsManager : JsonSettingsManager
{
    private const string Namespace = "npuImageEditor";

    private static string Ns(string name) => $"{Namespace}.{name}";

    private readonly ImageEditorSettings _current = new();
    private readonly ChoiceSetSetting _defaultScaleFactor;
    private readonly ToggleSetting _autoOpenResult;
    private readonly ToggleSetting _ocrAutoCopyText;
    private readonly ToggleSetting _ocrAutoOpenTextFile;

    public ImageEditorSettings Current => _current;

    public ImageEditorSettingsManager()
    {
        FilePath = ImageEditorPaths.SettingsJsonPath();

        _defaultScaleFactor = new ChoiceSetSetting(
            Ns(nameof(ImageEditorSettings.DefaultScaleFactor)),
            "Default scale factor",
            "Scale factor used when launching Super Resolution from the top-level command.",
            [
                new ChoiceSetSetting.Choice("2×", "2"),
                new ChoiceSetSetting.Choice("4×", "4"),
                new ChoiceSetSetting.Choice("8×", "8"),
            ])
        {
            Value = "2",
        };

        _autoOpenResult = new ToggleSetting(
            Ns(nameof(ImageEditorSettings.AutoOpenResult)),
            "Auto-open output file",
            "Open the output image in the default viewer after processing completes.",
            false);

        _ocrAutoCopyText = new ToggleSetting(
            Ns(nameof(ImageEditorSettings.OcrAutoCopyText)),
            "Auto-copy OCR text",
            "Automatically copy extracted text to the clipboard after OCR completes.",
            false);

        _ocrAutoOpenTextFile = new ToggleSetting(
            Ns(nameof(ImageEditorSettings.OcrAutoOpenTextFile)),
            "Auto-open OCR text file",
            "Save extracted text to a .txt file and open it in Notepad after OCR completes.",
            false);

        Settings.Add(_defaultScaleFactor);
        Settings.Add(_autoOpenResult);
        Settings.Add(_ocrAutoCopyText);
        Settings.Add(_ocrAutoOpenTextFile);

        LoadSettings();
        ApplyToRuntime();

        Settings.SettingsChanged += (_, _) =>
        {
            SaveSettings();
            ApplyToRuntime();
        };
    }

    private void ApplyToRuntime()
    {
        _current.DefaultScaleFactor =
            int.TryParse(_defaultScaleFactor.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int sf)
            && (sf == 2 || sf == 4 || sf == 8) ? sf : 2;
        _current.AutoOpenResult      = _autoOpenResult.Value;
        _current.OcrAutoCopyText     = _ocrAutoCopyText.Value;
        _current.OcrAutoOpenTextFile = _ocrAutoOpenTextFile.Value;
    }
}
