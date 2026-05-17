param(
    [switch]$MoveOldFoldersToRecycleBin
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$packageNames = @(
    "ActionCenterExtension",
    "SimpleAnalyticsExtension",
    "NPUToolsExtension",
    "NpuAwakeExtension",
    "NpuOrganizeExtension",
    "NpuImageEditorExtension",
    "NpuTextToolsExtension",
    "NpuNotesExtension",
    "NpuDevToolboxExtension",
    "TimeDateDockExtension",
    "MediaControlsExtension"
)

$processNames = $packageNames + @("NpuAwakeKeeper")

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
foreach ($name in $packageNames) {
    $manifest = Join-Path $repoRoot "src\$name\bin\x64\Debug\net9.0-windows10.0.26100.0\win-x64\AppxManifest.xml"
    if (-not (Test-Path $manifest)) {
        Write-Warning "Skipping $name - no build at $manifest. Build first if this extension should be registered."
        continue
    }

    Write-Host "Registering $manifest"
    Add-AppxPackage -Register $manifest -ForceApplicationShutdown
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

$oldFolders = @(
    "C:\Portable\ActionCenterExtension",
    "C:\Portable\SimpleAnalyticsExtension"
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
