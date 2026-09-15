<#
.SYNOPSIS
    Validates package, COM, provider, and solution identities across the monorepo.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path $PSScriptRoot -Parent
$sourceRoot = Join-Path $repositoryRoot 'src'
$solutionPath = Join-Path $repositoryRoot 'NpuCommandPaletteExtensions.sln'
$failures = [System.Collections.Generic.List[string]]::new()

function Add-DuplicateFailures {
    param(
        [Parameter(Mandatory)][string]$Label,
        [Parameter(Mandatory)][object[]]$Values
    )

    $Values |
        Group-Object Value |
        Where-Object Count -gt 1 |
        ForEach-Object {
            $locations = ($_.Group | ForEach-Object Path) -join ', '
            $failures.Add("Duplicate $Label '$($_.Name)': $locations")
        }
}

$manifests = @(Get-ChildItem -LiteralPath $sourceRoot -Filter 'Package.appxmanifest' -File -Recurse | Sort-Object FullName)
if ($manifests.Count -eq 0) {
    throw 'No extension manifests were found.'
}

$packageNames = @()
$appExtensionIds = @()
$comClassIds = @()
$providerIds = @()
$solutionText = Get-Content -Raw -LiteralPath $solutionPath

foreach ($manifest in $manifests) {
    $relativeManifest = [System.IO.Path]::GetRelativePath($repositoryRoot, $manifest.FullName)
    $text = Get-Content -Raw -LiteralPath $manifest.FullName
    $packageMatch = [regex]::Match($text, '<Identity\s+[^>]*Name="(?<value>[^"]+)"')
    $extensionMatch = [regex]::Match($text, '<uap3:AppExtension\s+[^>]*Id="(?<value>[^"]+)"')
    $classMatch = [regex]::Match($text, '<com:Class\s+[^>]*Id="(?<value>[^"]+)"')

    foreach ($required in @(
        @{ Label = 'package identity'; Match = $packageMatch },
        @{ Label = 'app-extension ID'; Match = $extensionMatch },
        @{ Label = 'COM class ID'; Match = $classMatch }
    )) {
        if (-not $required.Match.Success) {
            $failures.Add("$relativeManifest has no $($required.Label).")
        }
    }

    if ($packageMatch.Success) { $packageNames += @{ Value = $packageMatch.Groups['value'].Value; Path = $relativeManifest } }
    if ($extensionMatch.Success) { $appExtensionIds += @{ Value = $extensionMatch.Groups['value'].Value; Path = $relativeManifest } }
    if ($classMatch.Success) {
        $classId = $classMatch.Groups['value'].Value.ToLowerInvariant()
        $comClassIds += @{ Value = $classId; Path = $relativeManifest }
        $sourceText = (Get-ChildItem -LiteralPath $manifest.DirectoryName -Filter '*.cs' -File |
            ForEach-Object { Get-Content -Raw -LiteralPath $_.FullName }) -join "`n"
        if ($sourceText -notmatch [regex]::Escape($classId)) {
            $failures.Add("$relativeManifest COM class ID $classId does not match a root extension [Guid].")
        }
    }

    $project = Get-ChildItem -LiteralPath $manifest.DirectoryName -Filter '*.csproj' -File | Select-Object -First 1
    if (-not $project) {
        $failures.Add("$relativeManifest has no project file.")
    }
    else {
        $relativeProject = [System.IO.Path]::GetRelativePath($repositoryRoot, $project.FullName)
        if ($solutionText -notmatch [regex]::Escape($relativeProject)) {
            $failures.Add("$relativeProject is not included in the solution.")
        }
    }

    $provider = Get-ChildItem -LiteralPath $manifest.DirectoryName -Filter '*CommandsProvider.cs' -File -Recurse | Select-Object -First 1
    if (-not $provider) {
        $failures.Add("$relativeManifest has no commands provider.")
    }
    else {
        $providerText = Get-Content -Raw -LiteralPath $provider.FullName
        $providerMatch = [regex]::Match($providerText, '(?m)^\s*(?:this\.)?Id\s*=\s*"(?<value>[^"]+)"')
        $relativeProvider = [System.IO.Path]::GetRelativePath($repositoryRoot, $provider.FullName)
        if (-not $providerMatch.Success) {
            $failures.Add("$relativeProvider has no non-empty provider ID.")
        }
        else {
            $providerId = $providerMatch.Groups['value'].Value
            $providerIds += @{ Value = $providerId; Path = $relativeProvider }
            $duplicateCommandIds = Get-ChildItem -LiteralPath $manifest.DirectoryName -Filter '*.cs' -File -Recurse |
                Where-Object FullName -ne $provider.FullName |
                Where-Object { (Get-Content -Raw -LiteralPath $_.FullName) -match ('\bId\s*=\s*"' + [regex]::Escape($providerId) + '"') }
            foreach ($duplicate in $duplicateCommandIds) {
                $relativeDuplicate = [System.IO.Path]::GetRelativePath($repositoryRoot, $duplicate.FullName)
                $failures.Add("$relativeDuplicate reuses provider ID '$providerId' as a command or page ID.")
            }
        }
    }
}

Add-DuplicateFailures 'package identity' $packageNames
Add-DuplicateFailures 'app-extension ID' $appExtensionIds
Add-DuplicateFailures 'COM class ID' $comClassIds
Add-DuplicateFailures 'provider ID' $providerIds

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ -ErrorAction Continue }
    exit 1
}

Write-Host "Repository conventions: PASS ($($manifests.Count) packaged extensions, all identities unique and wired)." -ForegroundColor Green
