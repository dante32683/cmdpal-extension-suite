using System;
using System.IO;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace NpuTools.TextTools;

internal sealed class TextToolsSettingsManager : JsonSettingsManager
{
    private const string Namespace = "npuTextTools";
    private static string Namespaced(string name) => $"{Namespace}.{name}";

    internal readonly ChoiceSetSetting QuickMode;
    internal readonly TextSetting QuickCustomInstruction;

    public TextToolsSettingsManager()
    {
        FilePath = CommandPaletteSettingsPath();

        QuickMode = new ChoiceSetSetting(
            Namespaced("quickMode"),
            "Quick Rewrite default mode",
            "When set, Quick Rewrite uses this mode directly instead of showing the mode picker.",
            [
                new ChoiceSetSetting.Choice("Always ask (show list)", "Ask"),
                new ChoiceSetSetting.Choice("Fix Grammar", "FixGrammar"),
                new ChoiceSetSetting.Choice("Make Formal", "MakeFormal"),
                new ChoiceSetSetting.Choice("Make Concise", "MakeConcise"),
                new ChoiceSetSetting.Choice("Bullet Points", "BulletPoints"),
                new ChoiceSetSetting.Choice("Simplify", "Simplify"),
                new ChoiceSetSetting.Choice("Custom", "Custom"),
            ])
        {
            Value = "Ask",
        };

        QuickCustomInstruction = new TextSetting(
            Namespaced("quickCustomInstruction"),
            "Quick Rewrite custom instruction",
            "Instruction used when Quick Rewrite default mode is set to Custom.",
            string.Empty)
        {
            Placeholder = "e.g. Translate to Spanish",
        };

        Settings.Add(QuickMode);
        Settings.Add(QuickCustomInstruction);

        LoadSettings();
        Settings.SettingsChanged += (_, _) => SaveSettings();
    }

    internal TextRewriteMode? GetQuickMode()
    {
        return QuickMode.Value switch
        {
            "FixGrammar"   => TextRewriteMode.FixGrammar,
            "MakeFormal"   => TextRewriteMode.MakeFormal,
            "MakeConcise"  => TextRewriteMode.MakeConcise,
            "BulletPoints" => TextRewriteMode.BulletPoints,
            "Simplify"     => TextRewriteMode.Simplify,
            "Custom"       => TextRewriteMode.Custom,
            _              => null,
        };
    }

    internal string GetQuickCustomInstruction() =>
        QuickCustomInstruction.Value?.Trim() ?? string.Empty;

    private static string CommandPaletteSettingsPath()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "Microsoft", "PowerToys", "CommandPalette", "settings.json");
    }
}
