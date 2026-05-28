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
// then stores the result for review. The user can open Quick Rewrite to review and copy.
internal sealed partial class SelectionRewriteCommand : InvokableCommand
{
    private readonly TextRewriteMode _mode;
    private readonly TextRewriteService _service;
    private readonly PendingRewriteStore _pending;
    private readonly CaptureDiagnosticsStore? _diag;
    private readonly string? _customInstruction;

    public SelectionRewriteCommand(TextRewriteMode mode, TextRewriteService service, PendingRewriteStore pending, CaptureDiagnosticsStore? diag = null, string? customInstruction = null)
    {
        _mode = mode;
        _service = service;
        _pending = pending;
        _diag = diag;
        _customInstruction = customInstruction;
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
                _diag?.RecordFailure(CaptureStatus.NoTextCaptured, "Clipboard did not change after Ctrl+C");
                ShowToast("Quick Rewrite", "No text was captured. Select text first, then try again.");
                return;
            }

            _diag?.RecordSuccess(selection);
            string result = await _service.RewriteAsync(selection, _mode, _customInstruction);

            if (string.IsNullOrWhiteSpace(result))
            {
                _diag?.RecordFailure(CaptureStatus.EmptyRewrite, "Phi returned empty output");
                ShowToast("Quick Rewrite", "Phi returned an empty result. Try selecting more text.");
                return;
            }

            _pending.Set(selection, result, _mode);
            ShowToast("Quick Rewrite — Done", $"Open Quick Rewrite to review, or press Ctrl+V to paste the {TextRewriteService.ModeLabel(_mode)} result.");
            Interop.ClipboardHelper.SetText(result);
        }
        catch (Exception ex)
        {
            _diag?.RecordFailure(CaptureStatus.Error, ex.Message);
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
