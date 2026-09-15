<#
.SYNOPSIS
    Runs the repository's non-deploying quality gate.

.DESCRIPTION
    Restores with transitive vulnerability auditing, verifies formatting, validates
    repository and Command Palette conventions, checks docs, builds with warnings
    treated as errors, and runs the x64 unit suite. It never registers packages,
    starts extensions, or changes user data.

.PARAMETER Runtime
    Runtime identifier to build. Defaults to win-x64.

.PARAMETER Platform
    MSBuild platform paired with Runtime. Defaults to x64.

.PARAMETER BuildOnly
    Build packaged projects and keepers only. Used for the ARM64 compile gate.
#>
[CmdletBinding()]
param(
    [ValidateSet('win-x64', 'win-arm64')]
    [string]$Runtime = 'win-x64',
    [ValidateSet('x64', 'ARM64')]
    [string]$Platform = 'x64',
    [switch]$BuildOnly
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path $PSScriptRoot -Parent
$solution = Join-Path $repositoryRoot 'NpuCommandPaletteExtensions.sln'
$artifactRoot = Join-Path $repositoryRoot ".artifacts\check\$Runtime"
Set-Location $repositoryRoot

function Invoke-Stage {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][scriptblock]$Command
    )

    Write-Host ""
    Write-Host "==> $Name" -ForegroundColor Cyan
    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "$Name failed with exit code $LASTEXITCODE."
    }
}

$startedAt = Get-Date
Invoke-Stage 'Restore and dependency audit' {
    dotnet restore $solution --nologo -p:NuGetAudit=true -p:NuGetAuditMode=all -p:NuGetAuditLevel=moderate
}

if (-not $BuildOnly) {
    Invoke-Stage 'Format' {
        dotnet format $solution whitespace --verify-no-changes --no-restore --verbosity minimal
    }
    Invoke-Stage 'Repository conventions' {
        & (Join-Path $repositoryRoot 'tools\Test-RepositoryConventions.ps1')
    }
    Invoke-Stage 'Command Palette UX conventions' {
        & (Join-Path $repositoryRoot 'tools\Lint-UxConventions.ps1') -WarningsAsErrors
    }
    Invoke-Stage 'Documentation' {
        & (Join-Path $repositoryRoot 'scripts\check-docs.ps1')
    }
}

Invoke-Stage 'Restore isolated build outputs' {
    dotnet restore $solution --artifacts-path $artifactRoot --nologo `
        -p:NuGetAudit=true -p:NuGetAuditMode=all -p:NuGetAuditLevel=moderate
}

if ($BuildOnly) {
    $projects = @(
        Get-ChildItem -LiteralPath (Join-Path $repositoryRoot 'src') -Filter 'Package.appxmanifest' -File -Recurse |
            ForEach-Object { Get-ChildItem -LiteralPath $_.DirectoryName -Filter '*.csproj' -File | Select-Object -First 1 }
    ) | Sort-Object FullName -Unique

    Invoke-Stage "Build $Runtime" {
        foreach ($project in $projects) {
            dotnet build $project.FullName -r $Runtime -p:Platform=$Platform --artifacts-path $artifactRoot --no-restore --nologo -warnaserror
            if ($LASTEXITCODE -ne 0) { return }
        }
    }
}
else {
    Invoke-Stage "Build $Runtime" {
        dotnet build $solution -p:Platform=$Platform --artifacts-path $artifactRoot --no-restore --nologo -warnaserror
    }
    Invoke-Stage 'Unit tests' {
        dotnet test 'src\NpuTools.Tests\NpuTools.Tests.csproj' -r win-x64 -p:Platform=x64 --artifacts-path $artifactRoot --no-build --no-restore --nologo
    }
}

$elapsed = (Get-Date) - $startedAt
Write-Host ""
Write-Host ("Quality gate passed in {0:N1}s." -f $elapsed.TotalSeconds) -ForegroundColor Green
