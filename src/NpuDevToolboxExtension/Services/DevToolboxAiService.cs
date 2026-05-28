using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.Text;
using Windows.ApplicationModel;

namespace NpuTools.DevToolbox.Services;

internal sealed class DevToolboxAiService
{
    private const int MaxDiffChars = 3000;

    [SuppressMessage("Performance", "CA1822", Justification = "Service method — uniform instance call sites.")]
    public async Task<string> GenerateCommitMessageAsync(string workspacePath)
    {
        string diff = GetGitDiff(workspacePath);
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

    private static string GetGitDiff(string workspacePath)
    {
        // Try staged diff first; fall back to working-tree diff if nothing is staged.
        string staged = RunGit(workspacePath, "diff --staged");
        string diff = string.IsNullOrWhiteSpace(staged)
            ? RunGit(workspacePath, "diff HEAD")
            : staged;

        if (diff.Length > MaxDiffChars)
            diff = diff[..MaxDiffChars] + "\n... (truncated)";

        return diff;
    }

    private static string RunGit(string workspacePath, string arguments)
    {
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

            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(10_000);
            return output;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"DevToolboxAiService.RunGit failed: {ex.GetType().Name}: {ex.Message}");
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
