<#
.SYNOPSIS
    Checks local links in the repository's authoritative Markdown documentation.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path $PSScriptRoot -Parent
$failures = [System.Collections.Generic.List[string]]::new()
$requiredDocs = @(
    'README.md',
    'AGENTS.md',
    'docs/README.md',
    'docs/CONTEXT.md',
    'docs/ARCHITECTURE.md',
    'docs/RUNBOOK.md',
    'docs/CONVENTIONS.md',
    'docs/BUGS.md',
    'docs/ROADMAP.md'
)

foreach ($relativePath in $requiredDocs) {
    $fullPath = Join-Path $repositoryRoot $relativePath
    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
        $failures.Add("Required documentation is missing: $relativePath")
    }
}

$docFiles = @(
    Get-ChildItem -LiteralPath (Join-Path $repositoryRoot 'docs') -Filter '*.md' -File
    Get-Item -LiteralPath (Join-Path $repositoryRoot 'README.md'), (Join-Path $repositoryRoot 'AGENTS.md') -ErrorAction SilentlyContinue
)
$linkPattern = '!?' + '\[[^\]]*\]\((?<target>[^)]+)\)'

foreach ($docFile in $docFiles) {
    $text = Get-Content -Raw -LiteralPath $docFile.FullName
    foreach ($linkMatch in [regex]::Matches($text, $linkPattern)) {
        $lineStart = $text.LastIndexOf("`n", $linkMatch.Index)
        $linePrefix = $text.Substring($lineStart + 1, $linkMatch.Index - $lineStart - 1)
        if (([regex]::Matches($linePrefix, '(?<!`)`(?!`)').Count % 2) -eq 1) {
            continue
        }

        $target = $linkMatch.Groups['target'].Value.Trim()
        if ($target.StartsWith('<') -and $target.Contains('>')) {
            $target = $target.Substring(1, $target.IndexOf('>') - 1)
        }
        else {
            $target = ($target -split '\s+', 2)[0]
        }

        if ([string]::IsNullOrWhiteSpace($target) -or $target.StartsWith('#') -or
            $target -match '^[A-Za-z][A-Za-z0-9+.-]*:' -or $target.StartsWith('//')) {
            continue
        }

        $pathWithoutAnchor = ($target -split '[#?]', 2)[0]
        $decodedPath = [System.Uri]::UnescapeDataString($pathWithoutAnchor)
        $resolvedTarget = [System.IO.Path]::GetFullPath((Join-Path $docFile.DirectoryName $decodedPath))
        if (-not (Test-Path -LiteralPath $resolvedTarget)) {
            $relativeDoc = [System.IO.Path]::GetRelativePath($repositoryRoot, $docFile.FullName)
            $line = [regex]::Matches($text.Substring(0, $linkMatch.Index), "\r?\n").Count + 1
            $failures.Add("$relativeDoc`:$line has a broken local link: $target")
        }
    }
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ -ErrorAction Continue }
    exit 1
}

Write-Host "Documentation: PASS ($($docFiles.Count) authoritative Markdown files checked)." -ForegroundColor Green
