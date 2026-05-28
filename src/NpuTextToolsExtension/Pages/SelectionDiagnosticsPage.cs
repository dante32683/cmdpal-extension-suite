using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.TextTools.Commands;
using NpuTools.TextTools.Services;

namespace NpuTools.TextTools.Pages;

// Shows the result of the last selection capture attempt and troubleshooting tips.
// Useful when Quick Rewrite fails to capture selected text.
internal sealed partial class SelectionDiagnosticsPage : ListPage
{
    private readonly CaptureDiagnosticsStore _diag;
    private readonly PendingRewriteStore _pending;
    private readonly TextRewriteService _service;

    public SelectionDiagnosticsPage(CaptureDiagnosticsStore diag, PendingRewriteStore pending, TextRewriteService service)
    {
        _diag    = diag;
        _pending = pending;
        _service = service;
        Id    = "com.local.nputools.texttools.diagnostics";
        Title = "Selection Diagnostics";
        Name  = "Diagnostics";
        Icon  = TextToolsVisuals.Hub;
    }

    public override IListItem[] GetItems()
    {
        var items = new List<IListItem>();
        var last  = _diag.GetLast();

        if (last.HasValue)
        {
            var (time, status, captured, reason) = last.Value;
            string timeStr = time.LocalDateTime.ToString("g", CultureInfo.CurrentCulture);

            switch (status)
            {
                case CaptureStatus.Success:
                    string preview = captured is not null && captured.Length > 100
                        ? captured[..100] + "…"
                        : captured ?? string.Empty;
                    items.Add(new ListItem(new NoOpCommand())
                    {
                        Title    = "Last capture: success",
                        Subtitle = $"{timeStr} — \"{preview}\"",
                        Icon     = TextToolsVisuals.Check,
                        Tags     = [TextToolsVisuals.StatusTag("ok")],
                    });
                    break;

                case CaptureStatus.NoTextCaptured:
                    items.Add(new ListItem(new NoOpCommand())
                    {
                        Title    = "Last capture: no text captured",
                        Subtitle = timeStr,
                        Icon     = TextToolsVisuals.Phi,
                        Tags     = [TextToolsVisuals.CriticalTag("failed")],
                        Details  = TipsDetails("No text was captured",
                            "Select text before opening Command Palette.",
                            "Make sure the text field is active (click into it first).",
                            "Some apps block clipboard access (e.g. password managers, games).",
                            "Elevated apps may not respond to simulated key input.",
                            "Try selecting text in Notepad or a browser first to verify."),
                    });
                    break;

                case CaptureStatus.EmptyRewrite:
                    items.Add(new ListItem(new NoOpCommand())
                    {
                        Title    = "Last rewrite: empty result",
                        Subtitle = timeStr,
                        Icon     = TextToolsVisuals.Phi,
                        Tags     = [TextToolsVisuals.CriticalTag("failed")],
                        Details  = TipsDetails("Phi returned an empty result",
                            "Try selecting more text — very short snippets may be skipped.",
                            "Ensure the Phi Silica model is available on this device.",
                            "Try a different rewrite mode."),
                    });
                    break;

                case CaptureStatus.Error:
                    items.Add(new ListItem(new NoOpCommand())
                    {
                        Title    = "Last capture: error",
                        Subtitle = $"{timeStr} — {reason ?? "unknown error"}",
                        Icon     = TextToolsVisuals.Phi,
                        Tags     = [TextToolsVisuals.CriticalTag("error")],
                    });
                    break;
            }
        }
        else
        {
            items.Add(new ListItem(new NoOpCommand())
            {
                Title    = "No capture attempt recorded yet",
                Subtitle = "Use Quick Rewrite with selected text to see diagnostics",
                Icon     = TextToolsVisuals.Phi,
            });
        }

        items.Add(new ListItem(new SelectionRewriteCommand(TextRewriteMode.FixGrammar, _service, _pending))
        {
            Title    = "Run Test Capture — Fix Grammar",
            Subtitle = "Select text before running, then check above for result",
            Icon     = TextToolsVisuals.Phi,
            Tags     = [TextToolsVisuals.MutedTag("test capture")],
        });

        items.Add(new ListItem(new NoOpCommand())
        {
            Title    = "How selection capture works",
            Subtitle = "CP dismisses → previous app regains focus → Ctrl+C sent → clipboard polled",
            Icon     = TextToolsVisuals.Hub,
            Details  = TipsDetails("How it works",
                "1. You select text in any app, then open Command Palette.",
                "2. When you pick a Quick Rewrite mode, CP dismisses itself.",
                "3. The extension waits 200 ms for the previous app to regain focus.",
                "4. It simulates Ctrl+C to copy the selection to clipboard.",
                "5. It polls the clipboard sequence number for up to 800 ms.",
                "6. If the clipboard changed, the new text is captured and rewritten."),
        });

        items.Add(new ListItem(new NoOpCommand())
        {
            Title    = "Known limitations",
            Subtitle = "Apps that block Ctrl+C, elevated processes, RDP sessions",
            Icon     = TextToolsVisuals.Hub,
            Details  = TipsDetails("Known limitations",
                "Elevated apps (Run as Administrator) block simulated key input.",
                "RDP sessions may intercept Ctrl+C before it reaches the remote app.",
                "Apps with custom clipboard handling (e.g. terminals) may not respond.",
                "Game overlays and screen readers may grab focus before the extension.",
                "Rich-text clipboard formats are lost — only plain text is captured."),
        });

        return [.. items];
    }

    private static Details TipsDetails(string title, params string[] tips) =>
        new()
        {
            Title = title,
            Body  = string.Join("\n\n", tips),
        };
}
