using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.TextTools.Services;

namespace NpuTools.TextTools.Commands;

internal sealed partial class TestAiCommand : InvokableCommand
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NpuTools", "ai-test.log");

    private readonly TextRewriteService _service;

    public TestAiCommand(TextRewriteService service)
    {
        _service = service;
        Name     = "Test AI Connection";
        Icon     = TextToolsVisuals.Phi;
    }

    public override CommandResult Invoke()
    {
        _ = RunAsync();
        return CommandResult.ShowToast("AI test running — check result shortly…");
    }

    private async Task RunAsync()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
        try
        {
            string result = await _service.RewriteAsync(
                "Hello world, this is a test message.",
                TextRewriteMode.MakeConcise);

            string entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] PASS\nInput:  Hello world, this is a test message.\nOutput: {result}\n";
            await File.WriteAllTextAsync(LogPath, entry);
        }
        catch (Exception ex)
        {
            string entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] FAIL\nError: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}\n";
            await File.WriteAllTextAsync(LogPath, entry);
        }
    }
}
