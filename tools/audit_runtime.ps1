# tools/audit_runtime.ps1
# Audit runtime local CityTimelineMod — lecture seule par défaut.
# Usage:
#   .\tools\audit_runtime.ps1
#   .\tools\audit_runtime.ps1 -Build

param(
    [switch]$Build,
    [switch]$VerboseLogs
)

$ErrorActionPreference = "Continue"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path (Join-Path $scriptDir "..")
Set-Location $repoRoot

$modDir = Join-Path $env:USERPROFILE "AppData\LocalLow\Colossal Order\Cities Skylines II\Mods\CityTimelineMod"
$logsDir = Join-Path $env:USERPROFILE "AppData\LocalLow\Colossal Order\Cities Skylines II\Logs"

$repoConfig = Join-Path $repoRoot "config.json"
$runtimeConfig = Join-Path $modDir "config.json"
$runtimeModJson = Join-Path $modDir "mod.json"
$runtimeDll = Join-Path $modDir "CityTimelineMod.dll"
$repoCsproj = Join-Path $repoRoot "CityTimelineMod.csproj"

$cityTimelineLog = Join-Path $logsDir "CityTimelineMod.log"
$moddingLog = Join-Path $logsDir "Modding.log"

$issues = New-Object System.Collections.Generic.List[string]
$warnings = New-Object System.Collections.Generic.List[string]

function Write-Section {
    param([string]$Title)
    Write-Host ""
    Write-Host "============================================================"
    Write-Host $Title
    Write-Host "============================================================"
}

function Add-Issue {
    param([string]$Message)
    $issues.Add($Message) | Out-Null
    Write-Host "[ERREUR] $Message" -ForegroundColor Red
}

function Add-Warning {
    param([string]$Message)
    $warnings.Add($Message) | Out-Null
    Write-Host "[WARN] $Message" -ForegroundColor Yellow
}

function Read-Json {
    param([string]$Path)

    if (!(Test-Path $Path)) {
        return $null
    }

    try {
        return Get-Content $Path -Raw | ConvertFrom-Json
    }
    catch {
        Add-Issue "JSON invalide : $Path — $($_.Exception.Message)"
        return $null
    }
}

function Show-PathState {
    param(
        [string]$Label,
        [string]$Path,
        [bool]$Required = $true
    )

    if (Test-Path $Path) {
        Write-Host "[OK] $Label : $Path"
        return $true
    }

    if ($Required) {
        Add-Issue "$Label introuvable : $Path"
    }
    else {
        Add-Warning "$Label introuvable : $Path"
    }

    return $false
}

function Show-ConfigSummary {
    param(
        [string]$Label,
        [object]$Config
    )

    Write-Host ""
    Write-Host "--- $Label ---"

    if ($null -eq $Config) {
        Write-Host "(absent ou invalide)"
        return
    }

    $Config |
        Select-Object `
            modEnabled,
            bundleManifestPath,
            packPath,
            renderZoning,
            renderRoads,
            renderPaths,
            renderWaterLines,
            renderWaterAreas,
            renderWaterAreaOutlines,
            renderWaterAreaFillMeshes,
            renderMapBounds,
            zoningAlpha,
            roadAlpha,
            pathAlpha,
            waterLineAlpha,
            waterAreaOutlineAlpha,
            waterAreaFillAlpha,
            mapBoundsAlpha,
            groundMargin,
            pointStride,
            maxRenderedSegments,
            maxRoadSegmentsDebug,
            maxWaterSegmentsDebug |
        Format-List
}

function Test-GeoJsonPack {
    param([string]$PackPath)

    if ([string]::IsNullOrWhiteSpace($PackPath)) {
        Add-Issue "packPath vide."
        return
    }

    if (!(Test-Path $PackPath)) {
        Add-Issue "packPath introuvable : $PackPath"
        return
    }

    Write-Host "[OK] packPath existe : $PackPath"

    $geojsonRoot = Join-Path $PackPath "geojson"

    if (!(Test-Path $geojsonRoot)) {
        Add-Issue "Dossier geojson introuvable : $geojsonRoot"
        return
    }

    Write-Host "[OK] geojson root existe : $geojsonRoot"

    $requiredGeoJson = @(
        "water_lines_clipped.geojson",
        "water_areas_clipped.geojson",
        "roads_major_clipped.geojson",
        "roads_driveable_clipped.geojson",
        "paths.geojson",
        "zoning_polygons.geojson"
    )

    foreach ($name in $requiredGeoJson) {
        $path = Join-Path $geojsonRoot $name

        if (Test-Path $path) {
            $size = (Get-Item $path).Length
            Write-Host "[OK] $name ($size octets)"
        }
        else {
            Add-Issue "GeoJSON requis manquant : $path"
        }
    }
}

Write-Section "0. Contexte"

Write-Host "Repo       : $repoRoot"
Write-Host "Mod runtime: $modDir"
Write-Host "Logs       : $logsDir"
Write-Host "Build opt. : $Build"
Write-Host "Logs dét.  : $VerboseLogs"

Write-Section "1. Git"

try {
    Write-Host ""
    Write-Host "Branche :"
    git branch --show-current

    Write-Host ""
    Write-Host "Status :"
    $status = git status --short
    if ($status) {
        $status
        Add-Warning "Working tree non clean."
    }
    else {
        Write-Host "(clean)"
    }

    Write-Host ""
    Write-Host "Derniers commits :"
    git log --oneline -8
}
catch {
    Add-Warning "Impossible de lire l'état Git : $($_.Exception.Message)"
}

Write-Section "2. Build"

Show-PathState "csproj" $repoCsproj $true | Out-Null

if ($Build) {
    $runtimePayloadBackups = @{}

    foreach ($runtimePayloadName in @("config.json", "mod.json")) {
        $runtimePayloadPath = Join-Path $modDir $runtimePayloadName

        if (Test-Path $runtimePayloadPath) {
            $backupPath = Join-Path ([System.IO.Path]::GetTempPath()) (
                "CityTimelineMod.audit." +
                [System.Guid]::NewGuid().ToString("N") +
                "." +
                $runtimePayloadName
            )

            Copy-Item -LiteralPath $runtimePayloadPath -Destination $backupPath -Force
            $runtimePayloadBackups[$runtimePayloadName] = $backupPath

            Write-Host "Préservé avant build : $runtimePayloadPath"
        }
    }

    Write-Host "Build demandé : dotnet build"
    dotnet build $repoCsproj
    $buildExitCode = $LASTEXITCODE

    foreach ($entry in $runtimePayloadBackups.GetEnumerator()) {
        $runtimePayloadName = $entry.Key
        $backupPath = $entry.Value
        $runtimePayloadPath = Join-Path $modDir $runtimePayloadName

        if (Test-Path $backupPath) {
            if (!(Test-Path $modDir)) {
                New-Item -ItemType Directory -Force -Path $modDir | Out-Null
            }

            Copy-Item -LiteralPath $backupPath -Destination $runtimePayloadPath -Force
            Remove-Item -LiteralPath $backupPath -Force -ErrorAction SilentlyContinue

            Write-Host "Restauré après build : $runtimePayloadPath"
        }
    }

    if ($buildExitCode -ne 0) {
        Add-Issue "dotnet build a échoué avec exit code $buildExitCode"
    }
    else {
        Write-Host "[OK] dotnet build réussi."
    }
}
else {
    Write-Host "Build non lancé. Utilise -Build pour compiler pendant l'audit."
}

$repoDll = Join-Path $repoRoot "bin\Debug\net48\CityTimelineMod.dll"
Show-PathState "DLL repo Debug net48" $repoDll $false | Out-Null

Write-Section "3. Runtime files"

Show-PathState "Dossier runtime" $modDir $true | Out-Null
Show-PathState "DLL runtime" $runtimeDll $true | Out-Null
Show-PathState "mod.json runtime" $runtimeModJson $true | Out-Null
Show-PathState "config.json runtime" $runtimeConfig $true | Out-Null

Write-Section "4. mod.json runtime"

$modJson = Read-Json $runtimeModJson

if ($null -ne $modJson) {
    $modJson | Select-Object id,name,description,version,type,entryPoint | Format-List

    if ($modJson.id -ne "com.giscolab.CityTimelineMod") {
        Add-Warning "mod.json id inattendu : $($modJson.id)"
    }

    if ($modJson.entryPoint -ne "CityTimelineMod.Mod") {
        Add-Issue "entryPoint inattendu : $($modJson.entryPoint)"
    }

    if ($modJson.type -ne "code") {
        Add-Issue "type mod inattendu : $($modJson.type)"
    }

    if ($modJson.assemblies -notcontains "CityTimelineMod.dll") {
        Add-Issue "assemblies ne contient pas CityTimelineMod.dll"
    }
}

Write-Section "5. Config repo/runtime"

$repoConfigJson = Read-Json $repoConfig
$runtimeConfigJson = Read-Json $runtimeConfig

Show-ConfigSummary "Repo config" $repoConfigJson
Show-ConfigSummary "Runtime config" $runtimeConfigJson

Write-Section "6. Bundle manifest + GeoJSON"

if ($null -ne $runtimeConfigJson) {
    $manifestPath = $runtimeConfigJson.bundleManifestPath
    $packPath = $runtimeConfigJson.packPath

    if ([string]::IsNullOrWhiteSpace($manifestPath)) {
        Add-Issue "bundleManifestPath vide dans runtime config."
    }
    else {
        if (Test-Path $manifestPath) {
            Write-Host "[OK] Manifest bundle : $manifestPath"

            $manifestJson = Read-Json $manifestPath
            if ($null -ne $manifestJson) {
                $manifestJson |
                    Select-Object version,source,kind,city |
                    Format-List

                Write-Host "Center:"
                $manifestJson.center | Format-List

                Write-Host "TimelineMod:"
                $manifestJson.timelineMod |
                    Select-Object originLon,originLat,worldScale,configPath |
                    Format-List
            }
        }
        else {
            Add-Issue "bundleManifestPath introuvable : $manifestPath"
        }
    }

    Test-GeoJsonPack $packPath
}

Write-Section "7. Logs CityTimelineMod"

if (Test-Path $cityTimelineLog) {
    if ($VerboseLogs) {
        Write-Host "Mode logs : détaillé (-VerboseLogs)."

        Select-String -Path $cityTimelineLog `
            -Pattern "runtime controller|modEnabled|bundle manifest loaded|manifest applied|Loaded OK|GroundOverlay|display preset applied|visual settings|ERROR|Exception" `
            -CaseSensitive:$false |
            Select-Object -Last 120
    }
    else {
        Write-Host "Mode logs : essentiel. Utilise -VerboseLogs pour les batches GroundOverlay détaillés."

        Select-String -Path $cityTimelineLog `
            -Pattern "runtime controller|modEnabled=false|Overlay bootstrap skipped|bundle manifest loaded|manifest applied|Loaded OK|display preset applied|visual settings saved|visual settings loaded|visual settings reloaded|overlay rebuilt|ERROR|Exception" `
            -CaseSensitive:$false |
            Select-Object -Last 60
    }
}
else {
    Add-Warning "CityTimelineMod.log introuvable : $cityTimelineLog"
}

Write-Section "8. Logs Modding"

if (Test-Path $moddingLog) {
    Select-String -Path $moddingLog `
        -Pattern "Loaded CityTimelineMod|Mods initialized|ERROR|Exception" `
        -CaseSensitive:$false |
        Select-Object -Last 40
}
else {
    Add-Warning "Modding.log introuvable : $moddingLog"
}

Write-Section "9. Résumé"

Write-Host "Erreurs  : $($issues.Count)"
Write-Host "Warnings : $($warnings.Count)"

if ($warnings.Count -gt 0) {
    Write-Host ""
    Write-Host "Warnings :"
    foreach ($warning in $warnings) {
        Write-Host " - $warning" -ForegroundColor Yellow
    }
}

if ($issues.Count -gt 0) {
    Write-Host ""
    Write-Host "Erreurs :"
    foreach ($issue in $issues) {
        Write-Host " - $issue" -ForegroundColor Red
    }

    exit 1
}

Write-Host ""
Write-Host "[OK] Audit runtime terminé sans erreur critique."
exit 0
