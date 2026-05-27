using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.TextTools.Interop;
using NpuTools.TextTools.Services;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace NpuTools.TextTools.Commands;

// Dismisses Command Palette, captures selected text via Ctrl+C, rewrites it with Phi,
// and places the result in the clipboard. Shows a toast notification with the outcome.
internal sealed partial class SelectionRewriteCommand : InvokableCommand
{
    private readonly TextRewriteMode _mode;
    private readonly TextRewriteService _service;

    public SelectionRewriteCommand(TextRewriteMode mode, TextRewriteService service)
    {
        _mode = mode;
        _service = service;
        Name = $"Rewrite Selection — {TextRewriteService.ModeLabel(mode)}";
        Icon = TextToolsVisuals.Phi;
    }

    public override CommandResult Invoke()
    {
        _ = Task.Run(CaptureAndRewriteAsync);
        return CommandResult.Dismiss();
    }

    private async Task CaptureAndRewriteAsync()
    {
        try
        {
            string? selection = await SelectionHelper.CaptureSelectionAsync();

            if (string.IsNullOrWhiteSpace(selection))
            {
                ShowToast("Quick Rewrite", "No text was captured. Select text first, then try again.");
                return;
            }

            string result = await _service.RewriteAsync(selection, _mode);

            if (string.IsNullOrWhiteSpace(result))
            {
                ShowToast("Quick Rewrite", "Phi returned an empty result. Try selecting more text.");
                return;
            }

            Interop.ClipboardHelper.SetText(result);
            ShowToast("Quick Rewrite — Done", "Rewritten text is in the clipboard. Press Ctrl+V to paste.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SelectionRewriteCommand failed: {ex.GetType().Name}: {ex.Message}");
            ShowToast("Quick Rewrite Failed", ex.Message.Length > 100 ? ex.Message[..100] : ex.Message);
        }
    }

    private static void ShowToast(string title, string message)
    {
        try
        {
            string xml = $"<toast><visual><binding template=\"ToastGeneric\"><text>{EscapeXml(title)}</text><text>{EscapeXml(message)}</text></binding></visual></toast>";
            var doc = new XmlDocument();
            doc.LoadXml(xml);
            ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(doc));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ShowToast failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static string EscapeXml(string text) =>
        text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}
