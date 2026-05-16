# deploy.ps1 — safe deploy for CityTimelineMod (net48)

$ErrorActionPreference = "Stop"

$proj = Split-Path -Parent $MyInvocation.MyCommand.Path
$csproj = Join-Path $proj "CityTimelineMod.csproj"
$bin  = Join-Path $proj "bin\Debug\net48"
$dll  = Join-Path $bin "CityTimelineMod.dll"

$dst = Join-Path $env:USERPROFILE "AppData\LocalLow\Colossal Order\Cities Skylines II\Mods\CityTimelineMod"

# 0) Preserve deployed runtime config BEFORE any build/clean step
$deployedConfig = Join-Path $dst "config.json"
$configBackup = $null

if (Test-Path $deployedConfig) {
    $configBackup = Join-Path ([System.IO.Path]::GetTempPath()) ("CityTimelineMod.config." + [System.Guid]::NewGuid().ToString("N") + ".json")
    Copy-Item -Force $deployedConfig $configBackup
    Write-Host "Preserved deployed runtime config before build: $deployedConfig"
}
else {
    Write-Host "No deployed runtime config found before build."
}

# 1) Build fresh
dotnet clean $csproj
if ($LASTEXITCODE -ne 0) {
    throw "dotnet clean failed"
}

dotnet build $csproj -c Debug
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed"
}

if (!(Test-Path $dll)) {
    throw "Build succeeded but DLL was not found: $dll"
}

# 2) Prepare destination only after successful build
Remove-Item -Recurse -Force $dst -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $dst | Out-Null

# 3) Copy the mod DLL
Copy-Item -Force $dll $dst

# 4) Copy data files, if present in the repo
$srcData = Join-Path $proj "data"
if (Test-Path $srcData) {
    $dstData = Join-Path $dst "data"
    New-Item -ItemType Directory -Force -Path $dstData | Out-Null

    robocopy $srcData $dstData /E /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -ge 8) {
        throw "robocopy data failed with exit code $LASTEXITCODE"
    }

    $global:LASTEXITCODE = 0
    Write-Host "Copied data files."
}
else {
    Write-Host "No data directory found in repo."
}

# 4b) Restore runtime config if it existed, otherwise copy repo default config
$srcConfig = Join-Path $proj "config.json"

if ($configBackup -ne $null -and (Test-Path $configBackup)) {
    Copy-Item -Force $configBackup (Join-Path $dst "config.json")
    Remove-Item -Force $configBackup -ErrorAction SilentlyContinue
    Write-Host "Restored deployed runtime config."
}
elseif (Test-Path $srcConfig) {
    Copy-Item -Force $srcConfig $dst
    Write-Host "Copied repo default config."
}
else {
    Write-Host "No config.json found in repo; deployed without config.json."
}

# 5) Write manifest
$manifestLines = @(
    '{',
    '  "id": "com.giscolab.CityTimelineMod",',
    '  "name": "CityTimelineMod",',
    '  "description": "Development overlay for real-world CS2 map bundles.",',
    '  "version": "0.1.0",',
    '  "type": "code",',
    '  "assemblies": ["CityTimelineMod.dll"],',
    '  "entryPoint": "CityTimelineMod.Mod"',
    '}'
)

$manifest = $manifestLines -join [Environment]::NewLine
Set-Content -LiteralPath (Join-Path $dst "mod.json") -Value $manifest -Encoding UTF8

# 6) Validate deployed runtime payload
$requiredFiles = @(
    (Join-Path $dst "CityTimelineMod.dll"),
    (Join-Path $dst "mod.json"),
    (Join-Path $dst "config.json"),
    (Join-Path $dst "data\legacy-geojson\bbox_manifest.json"),
    (Join-Path $dst "data\legacy-geojson\roads_major_clipped.geojson"),
    (Join-Path $dst "data\legacy-geojson\water_areas_clipped.geojson"),
    (Join-Path $dst "data\legacy-geojson\water_lines_clipped.geojson"),
    (Join-Path $dst "data\legacy-geojson\zoning_polygons.geojson")
)

foreach ($requiredFile in $requiredFiles) {
    if (!(Test-Path $requiredFile)) {
        throw "Deployment validation failed. Missing required file: $requiredFile"
    }
}

Write-Host "Deployment validation OK."

# 7) Clean temp files
Get-ChildItem $dst -Filter "ilpp.pid" -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue

# 8) Show deployed files
Write-Host "`nDeployed to: $dst"
Get-ChildItem $dst -Recurse | Select-Object FullName,Length,LastWriteTime
