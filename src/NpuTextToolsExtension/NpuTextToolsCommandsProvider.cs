using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.TextTools.Pages;
using NpuTools.TextTools.Services;

namespace NpuTools.TextTools;

internal sealed partial class NpuTextToolsCommandsProvider : CommandProvider
{
    private readonly TextRewriteService _service = new();
    private readonly ICommandItem[] _commands;

    public NpuTextToolsCommandsProvider()
    {
        Id          = "com.local.nputools.texttools";
        DisplayName = "NPU Text Tools";
        Icon        = TextToolsVisuals.Pen;

        _commands =
        [
            new CommandItem(new TextToolsHubPage(_service))
            {
                Title    = "Text Tools",
                Subtitle = "Six Phi-Silica rewrite modes",
                Icon     = TextToolsVisuals.Hub,
            },
            new CommandItem(new QuickRewritePage(_service))
            {
                Title    = "Quick Rewrite",
                Subtitle = "Select text, open Command Palette, pick a mode — result goes to clipboard",
                Icon     = TextToolsVisuals.Phi,
            },
            new CommandItem(new RewriteInputPage(TextRewriteMode.FixGrammar, _service))
            {
                Title    = "Fix Grammar",
                Subtitle = "Correct grammar and spelling",
                Icon     = TextToolsVisuals.Phi,
            },
            new CommandItem(new RewriteInputPage(TextRewriteMode.MakeFormal, _service))
            {
                Title    = "Make Formal",
                Subtitle = "Rewrite in a professional tone",
                Icon     = TextToolsVisuals.Phi,
            },
            new CommandItem(new RewriteInputPage(TextRewriteMode.MakeConcise, _service))
            {
                Title    = "Make Concise",
                Subtitle = "Shorten while preserving meaning",
                Icon     = TextToolsVisuals.Phi,
            },
            new CommandItem(new RewriteInputPage(TextRewriteMode.BulletPoints, _service))
            {
                Title    = "Bullet Points",
                Subtitle = "Convert prose to a bullet list",
                Icon     = TextToolsVisuals.Phi,
            },
            new CommandItem(new RewriteInputPage(TextRewriteMode.Simplify, _service))
            {
                Title    = "Simplify",
                Subtitle = "Plain language for any audience",
                Icon     = TextToolsVisuals.Phi,
            },
            // Custom Rewrite uses a two-step flow: instruction page → text page → result.
            new CommandItem(new RewriteCustomInstructionPage(_service))
            {
                Title    = "Custom Rewrite",
                Subtitle = "Two steps: type instruction, then paste text",
                Icon     = TextToolsVisuals.Phi,
            },
        ];
    }

    public override ICommandItem[] TopLevelCommands() => _commands;
}
