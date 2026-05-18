using System;
using System.Collections.Generic;
using System.IO;
using NpuTools.Organize.Models;

namespace NpuTools.Organize.Services;

internal sealed class ScreenshotScannerService
{
    private static readonly string[] SupportedExtensions = [".png", ".jpg", ".jpeg", ".webp"];

    public string ScreenshotsFolder { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Screenshots");

    public IReadOnlyList<RenameProposal> Scan()
    {
        if (!Directory.Exists(ScreenshotsFolder))
            return [];

        var proposals = new List<RenameProposal>();
        foreach (string path in Directory.EnumerateFiles(ScreenshotsFolder))
        {
            string ext  = Path.GetExtension(path).ToLowerInvariant();
            string name = Path.GetFileName(path);
            if (!Array.Exists(SupportedExtensions, e => e == ext)) continue;
            if (SlugService.IsAlreadyOrganized(name)) continue;

            string proposed = SlugService.BuildProposedPath(path);
            if (!string.Equals(path, proposed, StringComparison.OrdinalIgnoreCase))
                proposals.Add(new RenameProposal(path, proposed));
        }

        proposals.Sort((a, b) =>
            File.GetCreationTime(b.OriginalPath).CompareTo(File.GetCreationTime(a.OriginalPath)));

        return proposals;
    }
}
