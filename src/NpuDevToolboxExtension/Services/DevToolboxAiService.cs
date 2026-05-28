using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.Text;
using Windows.ApplicationModel;

namespace NpuTools.DevToolbox.Services;

internal sealed class DevToolboxAiService
{
    private const int MaxDiffChars = 3000;

    private static readonly string[] SkippedPrefixes =
    [
        "diff --git a/bin/", "diff --git a/obj/",
        "+++ b/bin/",        "+++ b/obj/",
    ];

    [SuppressMessage("Performance", "CA1822", Justification = "Service method — uniform instance call sites.")]
    public async Task<string> GenerateCommitMessageAsync(string workspacePath)
    {
        // Verify this is a git repo before attempting anything.
        if (!Directory.Exists(Path.Combine(workspacePath, ".git")))
            return string.Empty;

        string diff = await GetGitDiffAsync(workspacePath);
        if (string.IsNullOrWhiteSpace(diff))
            return string.Empty;

        _ = TryUnlockNpuFeature();

        if (LanguageModel.GetReadyState() != AIFeatureReadyState.Ready)
            return string.Empty;

        try
        {
            string prompt = $"""
                Write a concise git commit message for the following diff.
                Use the conventional commit format: <type>: <short summary> (50 chars or fewer for the first line).
                If relevant, add a blank line followed by a short body explaining the why.
                Respond with ONLY the commit message text, no extra commentary.

                diff:
                {diff}
                """;

            using var model = await LanguageModel.CreateAsync();
            var response = await model.GenerateResponseAsync(prompt);
            return (response.Text ?? string.Empty).Trim();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"DevToolboxAiService.GenerateCommitMessageAsync failed: {ex.GetType().Name}: {ex.Message}");
            return string.Empty;
        }
    }

    private static async Task<string> GetGitDiffAsync(string workspacePath)
    {
        // Try staged diff first; fall back to working-tree diff if nothing is staged.
        string staged = await RunGitAsync(workspacePath, "diff --staged");
        string raw = string.IsNullOrWhiteSpace(staged)
            ? await RunGitAsync(workspacePath, "diff HEAD")
            : staged;

        // Strip build-output paths that add noise without semantic value.
        var filtered = new StringBuilder();
        bool skipHunk = false;
        foreach (string line in raw.Split('\n'))
        {
            if (line.StartsWith("diff --git", StringComparison.Ordinal))
                skipHunk = SkippedPrefixes.Any(p => line.Contains(p, StringComparison.OrdinalIgnoreCase));

            if (!skipHunk)
                filtered.Append(line).Append('\n');
        }

        string diff = filtered.ToString();
        if (diff.Length > MaxDiffChars)
            diff = diff[..MaxDiffChars] + "\n... (truncated)";

        return diff.Trim();
    }

    private static async Task<string> RunGitAsync(string workspacePath, string arguments)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            var psi = new ProcessStartInfo("git", arguments)
            {
                WorkingDirectory = workspacePath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi);
            if (process is null)
                return string.Empty;

            // Read stdout and stderr concurrently to prevent deadlock on large output.
            var stdoutTask = process.StandardOutput.ReadToEndAsync(cts.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(cts.Token);

            await Task.WhenAll(stdoutTask, stderrTask).WaitAsync(cts.Token);
            await process.WaitForExitAsync(cts.Token);

            return stdoutTask.Result;
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine($"DevToolboxAiService.RunGitAsync timed out for: {arguments}");
            return string.Empty;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"DevToolboxAiService.RunGitAsync failed: {ex.GetType().Name}: {ex.Message}");
            return string.Empty;
        }
    }

    private static bool TryUnlockNpuFeature()
    {
        try
        {
            const string featureId = "com.microsoft.windows.ai.languagemodel";
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                $@"SOFTWARE\Microsoft\Windows\CurrentVersion\AppModel\LimitedAccessFeatures\{featureId}");
            string? lafKey = key?.GetValue(string.Empty)?.ToString();
            if (string.IsNullOrEmpty(lafKey)) return false;
            string pfn = Package.Current.Id.FamilyName;
            string input = $"{featureId}!{lafKey}!{pfn}";
            byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            string token = Convert.ToBase64String(hashBytes[..16]);
            string publisherId = pfn.Split('_')[1];
            string attestation = $"{publisherId} has registered their use of {featureId} with Microsoft and agrees to the terms of use.";
            var result = LimitedAccessFeatures.TryUnlockFeature(featureId, token, attestation);
            return result.Status is LimitedAccessFeatureStatus.Available or LimitedAccessFeatureStatus.AvailableWithoutToken;
        }
        catch
        {
            return false;
        }
    }
}
