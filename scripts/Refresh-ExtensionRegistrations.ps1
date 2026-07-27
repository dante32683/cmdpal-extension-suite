param(
    [ValidateSet("x64", "ARM64")]
    [string]$Platform = "x64",
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",
    [switch]$MoveOldFoldersToRecycleBin
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$installableProjects = Get-ChildItem (Join-Path $repoRoot "src") -Directory |
    Where-Object { Test-Path (Join-Path $_.FullName "Package.appxmanifest") } |
    Sort-Object Name

$packageNames = foreach ($project in $installableProjects) {
    [xml]$packageManifest = Get-Content (Join-Path $project.FullName "Package.appxmanifest") -Raw
    $identity = $packageManifest.Package.Identity
    if ($null -eq $identity -or [string]::IsNullOrWhiteSpace($identity.Name)) {
        throw "Package.appxmanifest has no Identity.Name: $($project.FullName)"
    }
    $identity.Name
}

$processNames = $packageNames + @("NpuAwakeKeeper", "NpuOrganizeKeeper", "NpuClipboardKeeper")

Write-Host "Stopping extension processes..."
foreach ($processName in $processNames) {
    Stop-Process -Name $processName -Force -ErrorAction SilentlyContinue
}

Write-Host "Unregistering existing app packages..."
foreach ($name in $packageNames) {
    $packages = Get-AppxPackage -Name $name -ErrorAction SilentlyContinue
    foreach ($package in $packages) {
        Write-Host "Removing $($package.PackageFullName)"
        Remove-AppxPackage -Package $package.PackageFullName -ErrorAction Continue
    }
}

Write-Host "Registering monorepo app manifests..."
foreach ($project in $installableProjects) {
    $name = $project.Name
    $runtime = if ($Platform -eq "ARM64") { "win-arm64" } else { "win-x64" }
    $manifestPath = Join-Path $project.FullName "bin\$Platform\$Configuration\net9.0-windows10.0.26100.0\$runtime\AppxManifest.xml"
    if (-not (Test-Path $manifestPath)) {
        Write-Warning "Skipping $name - no build at $manifestPath. Build first if this extension should be registered."
        continue
    }

    Write-Host "Registering $manifestPath"
    Add-AppxPackage -Register $manifestPath -ForceApplicationShutdown
}

Write-Host "Current registrations:"
foreach ($name in $packageNames) {
    Get-AppxPackage -Name $name |
        Select-Object Name, PackageFullName, InstallLocation |
        Format-List
}

if (-not $MoveOldFoldersToRecycleBin) {
    Write-Host "Old sibling folders were not moved. Re-run with -MoveOldFoldersToRecycleBin after verifying registrations."
    return
}

# Pre-monorepo sibling checkouts. These lived beside the old repo root, so they
# are resolved relative to the current repo's parent rather than hardcoded.
$repoParent = Split-Path -Parent $repoRoot
$oldFolders = @(
    (Join-Path $repoParent "ActionCenterExtension"),
    (Join-Path $repoParent "SimpleAnalyticsExtension")
)

Write-Host "Moving old sibling folders to Recycle Bin..."
$shell = New-Object -ComObject Shell.Application
$recycleBin = $shell.Namespace(10)
foreach ($folder in $oldFolders) {
    if (-not (Test-Path $folder)) {
        continue
    }

    $resolved = (Resolve-Path -LiteralPath $folder).Path
    if ($resolved -notin $oldFolders) {
        throw "Refusing to move unexpected path: $resolved"
    }

    Write-Host "Recycling $resolved"
    $recycleBin.MoveHere($resolved)
}
