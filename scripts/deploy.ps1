# deploy.ps1 — safe deploy for CityTimelineMod (net48)
# Runtime truth: AppData config.json
# Repository default config is resources\defaults\config.json.

$ErrorActionPreference = "Stop"

$scriptRoot = $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($scriptRoot)) {
    throw "Impossible de déterminer le dossier du script deploy.ps1."
}

$repoRoot = Split-Path -Parent $scriptRoot

$projectRoot = Join-Path $repoRoot "src\CityTimelineMod"
$uiSrc = Join-Path $repoRoot "src\CityTimelineMod.UI"
$uiOut = Join-Path $uiSrc "dist"
$resourcesRoot = Join-Path $repoRoot "resources"
$packagingRoot = Join-Path $repoRoot "packaging"

$csproj = Join-Path $projectRoot "CityTimelineMod.csproj"
$bin = Join-Path $projectRoot "bin\Debug\net48"
$dll = Join-Path $bin "CityTimelineMod.dll"
$harmonyDll = Join-Path $bin "0Harmony.dll"
$repoManifest = Join-Path $packagingRoot "mod.json"

$userProfile = $env:USERPROFILE
if ([string]::IsNullOrWhiteSpace($userProfile)) {
    $userProfile = [Environment]::GetFolderPath("UserProfile")
}

if ([string]::IsNullOrWhiteSpace($userProfile)) {
    throw "Cannot resolve Windows user profile path."
}

$localModsRoot = Join-Path $userProfile "AppData\LocalLow\Colossal Order\Cities Skylines II\Mods"
$dst = Join-Path $localModsRoot "CityTimelineMod"
$legacyUiDst = Join-Path $localModsRoot "CityTimelineModUI"

$repoConfig = Join-Path $resourcesRoot "defaults\config.json"
$appConfig = Join-Path $dst "config.json"

function Assert-UnderPath {
    param(
        [Parameter(Mandatory=$true)][string]$Parent,
        [Parameter(Mandatory=$true)][string]$Child
    )

    $parentFull = [System.IO.Path]::GetFullPath($Parent).TrimEnd('\') + '\'
    $childFull = [System.IO.Path]::GetFullPath($Child)

    if (!$childFull.StartsWith($parentFull, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to touch path outside expected parent. Parent: $parentFull Child: $childFull"
    }
}

function Remove-PathIfExists {
    param(
        [Parameter(Mandatory=$true)][string]$Path,
        [Parameter(Mandatory=$true)][string]$ExpectedParent
    )

    Assert-UnderPath -Parent $ExpectedParent -Child $Path

    if (Test-Path -LiteralPath $Path) {
        Remove-Item -LiteralPath $Path -Recurse -Force
        Write-Host "Removed obsolete path: $Path"
    }
}

Write-Host "Repo:    $repoRoot"
Write-Host "AppData: $dst"
Write-Host "Runtime config source of truth: $appConfig"

$deployTemp = Join-Path $repoRoot ".tmp\deploy-temp"
New-Item -ItemType Directory -Force -Path $deployTemp | Out-Null
$env:TEMP = $deployTemp
$env:TMP = $deployTemp

# 1) Build DLL.
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

# 2) Build frontend. Webpack cleans the generated UI folder before emitting files.
if (!(Test-Path $uiSrc)) {
    throw "UI source directory missing: $uiSrc"
}

Push-Location $uiSrc
try {
    npm run build
    if ($LASTEXITCODE -ne 0) {
        throw "npm run build failed"
    }
}
finally {
    Pop-Location
}

$requiredGeneratedUIFiles = @(
    (Join-Path $uiOut "CityTimelineMod.mjs"),
    (Join-Path $uiOut "CityTimelineMod.css"),
    (Join-Path $uiOut "fonts\overpass.ttf")
)

foreach ($requiredGeneratedUIFile in $requiredGeneratedUIFiles) {
    if (!(Test-Path $requiredGeneratedUIFile)) {
        throw "Frontend build validation failed. Missing generated file: $requiredGeneratedUIFile"
    }
}

# 3) Ensure destination exists.
# IMPORTANT: do NOT delete $dst, because AppData config.json is the runtime truth.
New-Item -ItemType Directory -Force -Path $dst | Out-Null

# 4) Clean obsolete UI deployment attempts.
Remove-PathIfExists -Path (Join-Path $dst "UI") -ExpectedParent $dst
Remove-PathIfExists -Path (Join-Path $dst "vendor") -ExpectedParent $dst
Remove-PathIfExists -Path (Join-Path $dst "fonts") -ExpectedParent $dst
Remove-PathIfExists -Path $legacyUiDst -ExpectedParent $localModsRoot

$obsoleteRootFiles = @(
    "CityTimelineMod.js",
    "CityTimelineModUI.js",
    "citytimeline-hud.css",
    "index.html",
    "react-browser-globals.js"
)

foreach ($obsoleteRootFile in $obsoleteRootFiles) {
    $path = Join-Path $dst $obsoleteRootFile
    Assert-UnderPath -Parent $dst -Child $path

    if (Test-Path -LiteralPath $path) {
        Remove-Item -LiteralPath $path -Force
        Write-Host "Removed obsolete UI file: $path"
    }
}

# 5) Deploy DLL and Harmony.
Copy-Item -Force $dll (Join-Path $dst "CityTimelineMod.dll")
Write-Host "Copied DLL."

if (Test-Path $harmonyDll) {
    Copy-Item -Force $harmonyDll (Join-Path $dst "0Harmony.dll")
    Write-Host "Copied 0Harmony.dll."
}
else {
    Write-Warning "0Harmony.dll not found in build output. CityTimelineMod may fail to load."
}

# 6) Deploy data folder safely.
# Mirror repository GeoJSON resources to runtime data\legacy-geojson.
$srcData = Join-Path $resourcesRoot "legacy-geojson"
$dstData = Join-Path $dst "data\legacy-geojson"

if (Test-Path $srcData) {
    New-Item -ItemType Directory -Force -Path $dstData | Out-Null

    robocopy $srcData $dstData /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -ge 8) {
        throw "robocopy data failed with exit code $LASTEXITCODE"
    }

    $global:LASTEXITCODE = 0
    Write-Host "Mirrored data files."
}
else {
    Write-Host "No legacy GeoJSON resource directory found. Existing runtime data left untouched."
}

# 7) Deploy the single CS2 UI module to the code mod root.
Copy-Item -Force (Join-Path $uiOut "CityTimelineMod.mjs") (Join-Path $dst "CityTimelineMod.mjs")
Copy-Item -Force (Join-Path $uiOut "CityTimelineMod.css") (Join-Path $dst "CityTimelineMod.css")

$dstFonts = Join-Path $dst "fonts"
New-Item -ItemType Directory -Force -Path $dstFonts | Out-Null
Copy-Item -Force (Join-Path $uiOut "fonts\overpass.ttf") (Join-Path $dstFonts "overpass.ttf")
Write-Host "Copied CS2 UI module files."

# 8) Config policy.
# AppData config.json is authoritative at runtime.
# The repository default is a read-only initialization template.
if (Test-Path $appConfig) {
    Write-Host "Kept existing AppData runtime config."
}
elseif (Test-Path $repoConfig) {
    Copy-Item -Force $repoConfig $appConfig
    Write-Host "AppData config was missing. Initialized from repository default:"
    Write-Host "  $repoConfig"
    Write-Host "  -> $appConfig"
}
else {
    throw "No config.json found in AppData or repository defaults. Cannot deploy safely."
}

# 9) Deploy the code mod manifest from the repository source of truth.
if (!(Test-Path -LiteralPath $repoManifest)) {
    throw "Repository mod.json not found: $repoManifest"
}

Copy-Item -Force $repoManifest (Join-Path $dst "mod.json")
Write-Host "Copied code mod.json from packaging."

# 10) Validate deployed runtime payload.
$requiredFiles = @(
    (Join-Path $dst "CityTimelineMod.dll"),
    (Join-Path $dst "0Harmony.dll"),
    (Join-Path $dst "mod.json"),
    (Join-Path $dst "config.json"),
    (Join-Path $dst "data\legacy-geojson\bbox_manifest.json"),
    (Join-Path $dst "data\legacy-geojson\roads_major_clipped.geojson"),
    (Join-Path $dst "data\legacy-geojson\water_areas_clipped.geojson"),
    (Join-Path $dst "data\legacy-geojson\water_lines_clipped.geojson"),
    (Join-Path $dst "data\legacy-geojson\zoning_polygons.geojson"),
    (Join-Path $dst "CityTimelineMod.mjs"),
    (Join-Path $dst "CityTimelineMod.css"),
    (Join-Path $dst "fonts\overpass.ttf")
)

foreach ($requiredFile in $requiredFiles) {
    if (!(Test-Path $requiredFile)) {
        throw "Deployment validation failed. Missing required file: $requiredFile"
    }
}

$forbiddenPaths = @(
    (Join-Path $dst "CityTimelineMod.js"),
    (Join-Path $dst "CityTimelineModUI.js"),
    (Join-Path $dst "citytimeline-hud.css"),
    (Join-Path $dst "index.html"),
    (Join-Path $dst "react-browser-globals.js"),
    (Join-Path $dst "UI"),
    (Join-Path $dst "vendor"),
    $legacyUiDst
)

foreach ($forbiddenPath in $forbiddenPaths) {
    if (Test-Path -LiteralPath $forbiddenPath) {
        throw "Deployment validation failed. Obsolete UI artifact still exists: $forbiddenPath"
    }
}

Write-Host "Deployment validation OK."

# 11) Clean temp files only.
Get-ChildItem $dst -Filter "ilpp.pid" -ErrorAction SilentlyContinue |
    Remove-Item -Force -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "Deployed to: $dst"
Write-Host ""
Write-Host "Config status:"
Get-Item $appConfig, $repoConfig |
    Select-Object FullName, Length, LastWriteTime

Write-Host ""
Write-Host "Runtime UI files:"
Get-Item (Join-Path $dst "CityTimelineMod.mjs"), (Join-Path $dst "CityTimelineMod.css"), (Join-Path $dst "fonts\overpass.ttf") |
    Select-Object FullName, Length, LastWriteTime
