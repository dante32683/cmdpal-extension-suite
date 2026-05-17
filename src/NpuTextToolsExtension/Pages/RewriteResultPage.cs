using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.TextTools.Commands;
using NpuTools.TextTools.Services;

namespace NpuTools.TextTools.Pages;

internal sealed partial class RewriteResultPage : ListPage
{
    private readonly string _inputText;
    private readonly TextRewriteMode _mode;
    private readonly TextRewriteService _service;
    private readonly string? _customInstruction;

    private string? _result;
    private string? _errorMessage;
    private int _started; // Interlocked flag: 0 = not started, 1 = started

    public RewriteResultPage(string inputText, TextRewriteMode mode, TextRewriteService service, string? customInstruction = null)
    {
        _inputText         = inputText;
        _mode              = mode;
        _service           = service;
        _customInstruction = customInstruction;
        Id    = $"com.local.nputools.texttools.result.{mode.ToString().ToLowerInvariant()}";
        Title = $"Result — {TextRewriteService.ModeLabel(mode)}";
        Name  = "Result";
        Icon  = TextToolsVisuals.Check;
        IsLoading = true;
    }

    // GetItems() is only called after the user navigates to this page. Starting
    // the rewrite task here (rather than in the constructor) prevents AI calls
    // from firing for every keystroke in the input page that creates this page.
    public override IListItem[] GetItems()
    {
        if (Interlocked.Exchange(ref _started, 1) == 0)
        {
            _ = Task.Run(RewriteAsync);
        }

        if (_result == null && _errorMessage == null)
        {
            return
            [
                new ListItem(new NoOpCommand())
                {
                    Title    = "Processing…",
                    Subtitle = TextRewriteService.ModeLabel(_mode),
                    Icon     = TextToolsVisuals.Phi,
                },
            ];
        }

        if (_errorMessage is not null)
        {
            return
            [
                new ListItem(new NoOpCommand())
                {
                    Title    = "Rewrite failed",
                    Subtitle = _errorMessage,
                    Icon     = TextToolsVisuals.Check,
                },
            ];
        }

        string result  = _result!;
        string preview = result.Length > 200 ? result[..200] + "…" : result;

        return
        [
            new ListItem(new CopyResultCommand(result))
            {
                Title    = "Copy to Clipboard",
                Subtitle = preview,
                Icon     = TextToolsVisuals.Copy,
                Tags     = [TextToolsVisuals.MutedTag("copies full result")],
            },
            new ListItem(new NoOpCommand())
            {
                Title    = "Result",
                Subtitle = result,
                Icon     = TextToolsVisuals.Check,
            },
        ];
    }

    private async Task RewriteAsync()
    {
        try
        {
            _result = await _service.RewriteAsync(_inputText, _mode, _customInstruction);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Text Tools rewrite failed: {ex}");
            _errorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
            RaiseItemsChanged();
        }
    }
}
