# Lint-UxConventions.ps1
# Checks all implemented extensions for UX convention violations.
# Errors = must fix. Warnings = should fix.

$extensions = @(
    @{ Name = "ActionCenterExtension";   Dir = "ActionCenterExtension" },
    @{ Name = "TimeDateDockExtension";   Dir = "TimeDateDockExtension" },
    @{ Name = "MediaControlsExtension";  Dir = "MediaControlsExtension" },
    @{ Name = "SimpleAnalyticsExtension"; Dir = "SimpleAnalyticsExtension" },
    @{ Name = "NpuAwakeExtension";       Dir = "NpuAwakeExtension" },
    @{ Name = "NpuOrganizeExtension";    Dir = "NpuOrganizeExtension" },
    @{ Name = "NpuImageEditorExtension"; Dir = "NpuImageEditorExtension" },
    @{ Name = "NpuTextToolsExtension";   Dir = "NpuTextToolsExtension" },
    @{ Name = "NpuClipboardExtension";   Dir = "NpuClipboardExtension" }
)

$srcRoot = Join-Path $PSScriptRoot "..\src"
$errors   = [System.Collections.Generic.List[string]]::new()
$warnings = [System.Collections.Generic.List[string]]::new()

function Add-Error   ($msg) { $errors.Add($msg) }
function Add-Warning ($msg) { $warnings.Add($msg) }

foreach ($ext in $extensions) {
    $extPath = Join-Path $srcRoot $ext.Dir
    if (-not (Test-Path $extPath)) {
        Add-Error "[$($ext.Name)] Directory not found: $extPath"
        continue
    }

    # Locate CommandsProvider file
    $providerFile = Get-ChildItem $extPath -Filter "*CommandsProvider.cs" -Recurse |
                    Select-Object -First 1
    if (-not $providerFile) {
        Add-Error "[$($ext.Name)] No *CommandsProvider.cs found"
        continue
    }

    $provContent = Get-Content $providerFile.FullName -Raw
    $provLines   = Get-Content $providerFile.FullName

    # --- CHECK 1: SettingsPage instantiated as a top-level command ---
    # Catches extensions that create a new SettingsPage(...) for TopLevelCommands.
    # Does NOT flag "Quick Settings" dock band names or CommandProvider.Settings assignments.
    for ($i = 0; $i -lt $provLines.Count; $i++) {
        if ($provLines[$i] -match 'new\s+SettingsPage\s*\(') {
            Add-Error "[$($ext.Name)] SETTINGS-IN-TOPLEVEL (line $($i+1)): SettingsPage instantiated in provider — use CommandProvider.Settings only: $($provLines[$i].Trim())"
        }
    }

    # --- CHECK 2: Resource strings in provider ---
    if ($provContent -match 'Strings\.') {
        Add-Error "[$($ext.Name)] RESOURCE-STRINGS: Provider uses Strings.X resource references instead of hardcoded English"
    }

    # --- CHECK 3: Settings page exposed via MoreCommands ---
    # Look for CommandContextItem referencing a SettingsPage in the provider
    if ($provContent -match 'MoreCommands' -and $provContent -match 'SettingsPage') {
        Add-Error "[$($ext.Name)] SETTINGS-IN-MORECOMMANDS: Provider exposes settings via MoreCommands (use CommandProvider.Settings only)"
    }

    # --- CHECK 4: Em-dash in title strings (all .cs files) ---
    $allCs = Get-ChildItem $extPath -Filter "*.cs" -Recurse
    foreach ($file in $allCs) {
        $fc = Get-Content $file.FullName -Raw
        # Em-dash is U+2014 (—)
        if ($fc -match '(?<![a-zA-Z])Title\s*=\s*"[^"]*—[^"]*"') {
            Add-Error "[$($ext.Name)] EM-DASH-IN-TITLE: $($file.Name) has an em-dash (—) in a Title string — use subtitle for disambiguation"
        }
    }

    # --- CHECK 5: Missing subtitles on top-level commands ---
    # Heuristic: count Title= and Subtitle= in provider. If titles outnumber subtitles, flag.
    $titleCount    = ([regex]::Matches($provContent, 'Title\s*=\s*"')).Count
    $subtitleCount = ([regex]::Matches($provContent, 'Subtitle\s*=\s*"')).Count
    if ($titleCount -gt 0 -and $subtitleCount -eq 0) {
        Add-Warning "[$($ext.Name)] MISSING-SUBTITLES: $titleCount Title(s) found but no Subtitle in provider — every top-level command needs a subtitle"
    } elseif ($titleCount -gt $subtitleCount) {
        $diff = $titleCount - $subtitleCount
        Add-Warning "[$($ext.Name)] MISSING-SUBTITLES: $diff command(s) appear to be missing subtitles ($titleCount Title= vs $subtitleCount Subtitle= in provider)"
    }

    # --- CHECK 6: CommandContextItem without RequestedShortcut ---
    foreach ($file in $allCs) {
        $fc = Get-Content $file.FullName -Raw
        $contextItemCount = ([regex]::Matches($fc, 'new\s+CommandContextItem\s*\(')).Count
        if ($contextItemCount -gt 0 -and $fc -notmatch 'RequestedShortcut') {
            Add-Warning "[$($ext.Name)] NO-SHORTCUT: $($file.Name) has $contextItemCount CommandContextItem(s) with no RequestedShortcut"
        }
    }

    # --- CHECK 7: No KeyChords.cs despite having CommandContextItem usage ---
    $hasContextItems = $allCs | Where-Object {
        (Get-Content $_.FullName -Raw) -match 'new\s+CommandContextItem\s*\('
    }
    if ($hasContextItems) {
        $keyChordsFile = $allCs | Where-Object { $_.Name -eq "KeyChords.cs" }
        if (-not $keyChordsFile) {
            Add-Warning "[$($ext.Name)] NO-KEYCHORDS: Extension uses CommandContextItem but has no KeyChords.cs"
        }
    }
}

# --- REPORT ---
Write-Host ""
Write-Host "=== UX Convention Lint Report ===" -ForegroundColor Cyan
Write-Host ""

if ($errors.Count -eq 0 -and $warnings.Count -eq 0) {
    Write-Host "No violations found." -ForegroundColor Green
    exit 0
}

if ($errors.Count -gt 0) {
    Write-Host "ERRORS — must fix ($($errors.Count)):" -ForegroundColor Red
    foreach ($e in $errors) { Write-Host "  $e" -ForegroundColor Red }
    Write-Host ""
}

if ($warnings.Count -gt 0) {
    Write-Host "WARNINGS — should fix ($($warnings.Count)):" -ForegroundColor Yellow
    foreach ($w in $warnings) { Write-Host "  $w" -ForegroundColor Yellow }
    Write-Host ""
}

Write-Host "Total: $($errors.Count) error(s), $($warnings.Count) warning(s)"

if ($errors.Count -gt 0) { exit 1 } else { exit 0 }
