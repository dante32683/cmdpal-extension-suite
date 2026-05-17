using System;
using System.Diagnostics;
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
    }

    public override IListItem[] GetItems()
    {
        if (_result == null && _errorMessage == null)
        {
            try
            {
                _result = _service.RewriteAsync(_inputText, _mode, _customInstruction).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Text Tools rewrite failed: {ex}");
                _errorMessage = ex.Message;
            }
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
}
