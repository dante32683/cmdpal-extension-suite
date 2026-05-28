using System.Collections.Generic;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.TextTools.Commands;
using NpuTools.TextTools.Services;

namespace NpuTools.TextTools.Pages;

// DynamicListPage for quick text rewrite.
// - Empty search: shows pending review item (if any) then mode list; selecting a mode captures selected text.
// - With search text: shows mode list; selecting a mode rewrites the typed text directly.
// When a default Quick Mode is configured in settings, that mode is shown first with a "default" tag.
internal sealed partial class QuickRewritePage : DynamicListPage
{
    private readonly TextRewriteService _service;
    private readonly TextToolsSettingsManager? _settings;
    private readonly PendingRewriteStore? _pending;
    private IListItem[] _items;

    private static readonly (TextRewriteMode Mode, string Subtitle)[] StandardModes =
    [
        (TextRewriteMode.FixGrammar,   "Correct grammar and spelling"),
        (TextRewriteMode.MakeFormal,   "Rewrite in a professional tone"),
        (TextRewriteMode.MakeConcise,  "Shorten while preserving meaning"),
        (TextRewriteMode.BulletPoints, "Convert prose to bullet points"),
        (TextRewriteMode.Simplify,     "Plain language for any audience"),
        (TextRewriteMode.Custom,       "Two steps: enter instruction, then paste text"),
    ];

    public QuickRewritePage(TextRewriteService service, TextToolsSettingsManager? settings = null, PendingRewriteStore? pending = null)
    {
        _service  = service;
        _settings = settings;
        _pending  = pending;
        Id              = "com.local.nputools.texttools.quick";
        Title           = "Quick Rewrite";
        Name            = "Quick Rewrite";
        Icon            = TextToolsVisuals.Phi;
        PlaceholderText = "Type text to rewrite, or leave empty to rewrite selected text...";
        _items          = BuildItems(string.Empty);
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        _items = BuildItems(newSearch.Trim());
        RaiseItemsChanged(_items.Length);
    }

    public override IListItem[] GetItems() => _items;

    private IListItem[] BuildItems(string inputText)
    {
        bool hasText = inputText.Length > 0;

        TextRewriteMode? defaultMode = _settings?.GetQuickMode();
        string customInstruction = _settings?.GetQuickCustomInstruction() ?? string.Empty;

        var items = new List<IListItem>(StandardModes.Length + 2);

        // Show pending review item at top when empty search and a previous capture awaits review.
        if (!hasText && _pending is not null)
        {
            var snapshot = _pending.Peek();
            if (snapshot.HasValue)
            {
                var (input, result, mode) = snapshot.Value;
                string preview = result.Length > 80 ? result[..80] + "…" : result;
                var reviewPage = new PendingReviewPage(_pending, input, result, mode, _service);
                items.Add(new ListItem(reviewPage)
                {
                    Title    = $"Review Last Rewrite — {TextRewriteService.ModeLabel(mode)}",
                    Subtitle = preview,
                    Icon     = TextToolsVisuals.Check,
                    Tags     = [TextToolsVisuals.StatusTag("pending review")],
                });
            }
        }

        // Show default mode at top with "default" tag when configured.
        if (defaultMode.HasValue)
        {
            items.Add(BuildModeItem(defaultMode.Value, GetSubtitle(defaultMode.Value), inputText, hasText, customInstruction, isDefault: true));
        }

        foreach (var (mode, subtitle) in StandardModes)
        {
            if (defaultMode.HasValue && mode == defaultMode.Value)
                continue;

            items.Add(BuildModeItem(mode, subtitle, inputText, hasText, customInstruction, isDefault: false));
        }

        return [.. items];
    }

    private ListItem BuildModeItem(TextRewriteMode mode, string subtitle, string inputText, bool hasText, string customInstruction, bool isDefault)
    {
        ICommand command = hasText
            ? new RewriteResultPage(inputText, mode, _service, customInstruction: mode == TextRewriteMode.Custom ? customInstruction : null)
            : (ICommand)new SelectionRewriteCommand(mode, _service, _pending ?? new PendingRewriteStore(), customInstruction: mode == TextRewriteMode.Custom ? customInstruction : null);

        string tagLabel = hasText ? "rewrite typed text" : "rewrite selected text";
        string fullSubtitle = hasText
            ? string.Concat(subtitle, " -- \"", Preview(inputText), "\"")
            : string.Concat(subtitle, " (captures selection)");

        var tags = new List<Tag>();
        if (isDefault)
            tags.Add(TextToolsVisuals.StatusTag("default"));
        tags.Add(TextToolsVisuals.MutedTag(tagLabel));

        return new ListItem(command)
        {
            Title    = TextRewriteService.ModeLabel(mode),
            Subtitle = fullSubtitle,
            Icon     = TextToolsVisuals.Phi,
            Tags     = [.. tags],
        };
    }

    private static string GetSubtitle(TextRewriteMode mode) => mode switch
    {
        TextRewriteMode.FixGrammar   => "Correct grammar and spelling",
        TextRewriteMode.MakeFormal   => "Rewrite in a professional tone",
        TextRewriteMode.MakeConcise  => "Shorten while preserving meaning",
        TextRewriteMode.BulletPoints => "Convert prose to bullet points",
        TextRewriteMode.Simplify     => "Plain language for any audience",
        TextRewriteMode.Custom       => "Two steps: enter instruction, then paste text",
        _                            => mode.ToString(),
    };

    private static string Preview(string text) =>
        text.Length > 60 ? text[..60] + "..." : text;
}
