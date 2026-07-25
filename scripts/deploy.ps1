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
$realmapBundlesRoot = $env:CITYTIMELINE_REALMAP_BUNDLES_ROOT
if ([string]::IsNullOrWhiteSpace($realmapBundlesRoot)) {
    $githubRoot = Split-Path -Parent $repoRoot
    $realmapBundlesRoot = Join-Path $githubRoot "cs2-realmap-generator\exports\bundles"
}

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
    }
}

# 1) Clean and build DLL.
$dotnetOutput = dotnet clean $csproj --nologo -v:q 2>&1
if ($LASTEXITCODE -ne 0) {
    $dotnetOutput | Out-Host
    throw "dotnet clean failed"
}

function Assert-BundleCatalog {
    param(
        [Parameter(Mandatory=$true)][string]$Root,
        [Parameter(Mandatory=$true)][string]$Label
    )

    $rootFull = [System.IO.Path]::GetFullPath($Root)
    $indexPath = Join-Path $rootFull "bundle_index.json"
    if (!(Test-Path -LiteralPath $indexPath -PathType Leaf)) {
        throw "$Label bundle_index.json missing: $indexPath"
    }

    try {
        $index = Get-Content -LiteralPath $indexPath -Raw | ConvertFrom-Json
    }
    catch {
        throw "$Label bundle_index.json is invalid: $indexPath ($($_.Exception.Message))"
    }

    $entries = @($index.bundles)
    if ($entries.Count -eq 0) {
        throw "$Label bundle_index.json contains no bundles: $indexPath"
    }

    foreach ($entry in $entries) {
        $relativePath = [string]$entry.bundlePath
        if ([string]::IsNullOrWhiteSpace($relativePath)) {
            $relativePath = [string]$entry.relativePath
        }
        if ([string]::IsNullOrWhiteSpace($relativePath)) {
            $relativePath = [string]$entry.id
        }
        if ([string]::IsNullOrWhiteSpace($relativePath) -or [System.IO.Path]::IsPathRooted($relativePath)) {
            throw "$Label bundle entry has an invalid path: $($entry.id)"
        }

        $bundlePath = [System.IO.Path]::GetFullPath((Join-Path $rootFull $relativePath))
        Assert-UnderPath -Parent $rootFull -Child $bundlePath
        if (!(Test-Path -LiteralPath $bundlePath -PathType Container)) {
            throw "$Label bundle directory missing for '$($entry.id)': $bundlePath"
        }

        $manifestPath = Join-Path $bundlePath "manifest.json"
        if (!(Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
            throw "$Label manifest missing for '$($entry.id)': $manifestPath"
        }
    }

    return $entries.Count
}

[Console]::WriteLine("Vérification des dépendances du projet en cours...")
$dotnetOutput = dotnet build $csproj -c Debug --no-restore --nologo -v:q 2>&1
if ($LASTEXITCODE -ne 0) {
    $dotnetOutput | Out-Host
    throw "dotnet build failed"
}

if (!(Test-Path $dll)) {
    throw "Build succeeded but DLL was not found: $dll"
}

[Console]::WriteLine("Compilation C# réussie — les fichiers source n’ont pas été restaurés ni modifiés.")

# 2) Build frontend. Webpack cleans the generated UI folder before emitting files.
if (!(Test-Path $uiSrc)) {
    throw "UI source directory missing: $uiSrc"
}

Push-Location $uiSrc
try {
    $webpackOutput = npm run build 2>&1

    if ($LASTEXITCODE -ne 0) {
        $webpackOutput | Out-Host
        throw "npm run build failed"
    }

    [Console]::WriteLine("React OK")
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

# RealMap is the source of truth. Deploy the complete catalog, not only the
# active bundle: every directory listed in bundle_index.json is mirrored.
$canonicalBundles = Join-Path $dst "data\exports\bundles"

# Explicit precondition check to avoid a cryptic failure later.
if (-not (Test-Path -LiteralPath $realmapBundlesRoot)) {
    throw "RealMap source directory is missing or invalid: $realmapBundlesRoot (set CITYTIMELINE_REALMAP_BUNDLES_ROOT if needed)"
}

$sourceBundleCount = Assert-BundleCatalog -Root $realmapBundlesRoot -Label "RealMap source"
New-Item -ItemType Directory -Force -Path $canonicalBundles | Out-Null

robocopy $realmapBundlesRoot $canonicalBundles /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
if ($LASTEXITCODE -ge 8) {
    throw "robocopy RealMap bundles failed with exit code $LASTEXITCODE"
}
# Reset for subsequent commands (robocopy sets $LASTEXITCODE even on success).
$global:LASTEXITCODE = 0

$deployedBundleCount = Assert-BundleCatalog -Root $canonicalBundles -Label "Deployed"
if ($deployedBundleCount -ne $sourceBundleCount) {
    throw "Bundle catalog deployment mismatch: source=$sourceBundleCount deployed=$deployedBundleCount"
}

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
    }
}

# 5) Deploy DLL and Harmony.
Copy-Item -Force $dll (Join-Path $dst "CityTimelineMod.dll")

if (Test-Path $harmonyDll) {
    Copy-Item -Force $harmonyDll (Join-Path $dst "0Harmony.dll")
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
}
# else: no legacy-geojson to deploy (intentional no-op).

# 7) Deploy the single CS2 UI module to the code mod root.
Copy-Item -Force (Join-Path $uiOut "CityTimelineMod.mjs") (Join-Path $dst "CityTimelineMod.mjs")
Copy-Item -Force (Join-Path $uiOut "CityTimelineMod.css") (Join-Path $dst "CityTimelineMod.css")

$dstFonts = Join-Path $dst "fonts"
New-Item -ItemType Directory -Force -Path $dstFonts | Out-Null
Copy-Item -Force (Join-Path $uiOut "fonts\overpass.ttf") (Join-Path $dstFonts "overpass.ttf")

# 8) Config policy.
# AppData config.json is authoritative at runtime.
# The repository default is a read-only initialization template.
if (-not (Test-Path -LiteralPath $appConfig) -and (Test-Path -LiteralPath $repoConfig)) {
    Copy-Item -Force $repoConfig $appConfig
}
elseif (-not (Test-Path -LiteralPath $appConfig)) {
    throw "No config.json found in AppData or repository defaults. Cannot deploy safely."
}
# else: AppData config already exists, keep it (runtime truth).

# 9) Deploy the code mod manifest from the repository source of truth.
if (!(Test-Path -LiteralPath $repoManifest)) {
    throw "Repository mod.json not found: $repoManifest"
}

Copy-Item -Force $repoManifest (Join-Path $dst "mod.json")

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

if (Test-Path -LiteralPath $canonicalBundles) {
    $bundleIndex = Join-Path $canonicalBundles "bundle_index.json"
    if (!(Test-Path -LiteralPath $bundleIndex)) {
        throw "Deployment validation failed. Bundle directory exists without bundle_index.json: $canonicalBundles"
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

# 11) Clean temp files only.
Get-ChildItem $dst -Filter "ilpp.pid" -ErrorAction SilentlyContinue |
    Remove-Item -Force -ErrorAction SilentlyContinue

[Console]::WriteLine("Deploy OK: $dst")