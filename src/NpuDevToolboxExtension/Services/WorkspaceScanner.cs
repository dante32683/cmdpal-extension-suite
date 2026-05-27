using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NpuTools.DevToolbox.Models;

namespace NpuTools.DevToolbox.Services;

internal static class WorkspaceScanner
{
    private static readonly string[] ProjectMarkers =
    [
        ".git",
        ".sln",
        ".csproj",
        "package.json",
        "Cargo.toml",
        "pyproject.toml",
        "go.mod",
        "pom.xml",
        "build.gradle",
        ".xcode",
    ];

    private static readonly string[] DefaultScanRoots =
    [
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "source", "repos"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "source"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "repos"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "dev"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "projects"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "code"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "repos"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Desktop"),
    ];

    public static List<WorkspaceEntry> Scan(IReadOnlyList<string> additionalRoots)
    {
        var roots = new List<string>(DefaultScanRoots);
        roots.AddRange(additionalRoots);

        var results = new List<WorkspaceEntry>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string root in roots)
        {
            if (!Directory.Exists(root))
                continue;

            try
            {
                // Check if the root itself is a project
                string? rootType = DetectProjectType(root);
                if (rootType is not null && seen.Add(root))
                {
                    results.Add(new WorkspaceEntry
                    {
                        Path = root,
                        ProjectType = rootType,
                        IsRecent = false,
                    });
                }
                else
                {
                    // Scan one level deep
                    foreach (string dir in Directory.EnumerateDirectories(root))
                    {
                        try
                        {
                            string? type = DetectProjectType(dir);
                            if (type is not null && seen.Add(dir))
                            {
                                results.Add(new WorkspaceEntry
                                {
                                    Path = dir,
                                    ProjectType = type,
                                    IsRecent = false,
                                });
                            }
                        }
                        catch (UnauthorizedAccessException) { }
                        catch (IOException) { }
                    }
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }
        }

        results.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return results;
    }

    private static string? DetectProjectType(string dir)
    {
        foreach (string marker in ProjectMarkers)
        {
            string candidate = Path.Combine(dir, marker);
            if (File.Exists(candidate) || Directory.Exists(candidate))
                return MarkerToType(marker);
        }

        return null;
    }

    private static string MarkerToType(string marker) => marker switch
    {
        ".git"          => "git",
        ".sln" or ".csproj" => "dotnet",
        "package.json"  => "node",
        "Cargo.toml"    => "rust",
        "pyproject.toml" => "python",
        "go.mod"        => "go",
        "pom.xml" or "build.gradle" => "java",
        _               => "project",
    };
}
