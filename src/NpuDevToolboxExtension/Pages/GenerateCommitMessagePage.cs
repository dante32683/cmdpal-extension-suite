using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.DevToolbox.Services;

namespace NpuTools.DevToolbox.Pages;

internal sealed partial class GenerateCommitMessagePage : ListPage
{
    private readonly string _workspacePath;
    private readonly DevToolboxAiService _ai;
    private int _started;
    private IListItem[] _items =
    [
        new ListItem(new NoOpCommand())
        {
            Title = "Generating commit message...",
            Subtitle = "Running git diff and asking Phi to suggest a message.",
            Icon = DevToolboxVisuals.Commit,
        },
    ];

    public GenerateCommitMessagePage(string workspacePath, DevToolboxAiService ai)
    {
        _workspacePath = workspacePath;
        _ai = ai;
        Id = "com.local.nputools.devtoolbox.commitmsg";
        Title = "Commit Message";
        Name = "Generate Commit Message";
        Icon = DevToolboxVisuals.Commit;
        IsLoading = true;
    }

    public override IListItem[] GetItems()
    {
        if (Interlocked.Exchange(ref _started, 1) == 0)
            _ = Task.Run(LoadAsync);
        return _items;
    }

    private async Task LoadAsync()
    {
        try
        {
            string message = await _ai.GenerateCommitMessageAsync(_workspacePath);

            if (string.IsNullOrWhiteSpace(message))
            {
                _items =
                [
                    new ListItem(new NoOpCommand())
                    {
                        Title = "No changes detected",
                        Subtitle = "Stage or make changes to the workspace first, then try again.",
                        Icon = DevToolboxVisuals.Commit,
                    },
                ];
            }
            else
            {
                _items =
                [
                    new ListItem(new CopyTextCommand(message) { Name = "Copy Message" })
                    {
                        Title = FirstLine(message),
                        Subtitle = message.Contains('\n') ? message[(message.IndexOf('\n') + 1)..].Trim() : string.Empty,
                        Icon = DevToolboxVisuals.Commit,
                    },
                ];
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"GenerateCommitMessagePage.LoadAsync failed: {ex.GetType().Name}: {ex.Message}");
            _items =
            [
                new ListItem(new NoOpCommand())
                {
                    Title = "Could not generate commit message",
                    Subtitle = ex.Message,
                    Icon = DevToolboxVisuals.Commit,
                },
            ];
        }

        IsLoading = false;
        RaiseItemsChanged(_items.Length);
    }

    private static string FirstLine(string text)
    {
        int nl = text.IndexOf('\n');
        return nl >= 0 ? text[..nl].Trim() : text;
    }
}
