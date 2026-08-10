# deploy.ps1 — safe deploy for CityTimelineMod (net48)
# Runtime user configuration lives under ModsSettings and is read-only here.
# The mod-root config.json is only a legacy/default migration seed.

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
$buildConfiguration = "Debug"
$bin = Join-Path $projectRoot "bin\$buildConfiguration\net48"
$dll = Join-Path $bin "CityTimelineMod.dll"
$harmonyDll = Join-Path $bin "0Harmony.dll"
$nativeArtifactNames = @(
    "CityTimelineMod_win_x86_64.dll",
    "CityTimelineMod_linux_x86_64.so",
    "CityTimelineMod_mac_x86_64.bundle"
)
$repoManifest = Join-Path $packagingRoot "mod.json"
$buildRunId = [Guid]::NewGuid().ToString("N")
$toolchainDeployRoot = Join-Path $projectRoot ("obj\deploy-toolchain-" + $buildRunId)
$msbuildTempRoot = Join-Path $projectRoot ("obj\msbuild-temp-" + $buildRunId)

$userProfile = $env:USERPROFILE
if ([string]::IsNullOrWhiteSpace($userProfile)) {
    $userProfile = [Environment]::GetFolderPath("UserProfile")
}

if ([string]::IsNullOrWhiteSpace($userProfile)) {
    throw "Cannot resolve Windows user profile path."
}

$gameUserDataRoot = Join-Path $userProfile "AppData\LocalLow\Colossal Order\Cities Skylines II"
$localModsRoot = Join-Path $gameUserDataRoot "Mods"
$installDst = Join-Path $localModsRoot "CityTimelineMod"
$stagingDst = Join-Path $localModsRoot "CityTimelineMod.__staging"
$backupDst = Join-Path $localModsRoot "CityTimelineMod.__previous"
$dst = $stagingDst
$legacyUiDst = Join-Path $localModsRoot "CityTimelineModUI"

$repoConfig = Join-Path $resourcesRoot "defaults\config.json"
$legacyConfig = Join-Path $dst "config.json"
$runtimeConfig = Join-Path $gameUserDataRoot "ModsSettings\CityTimelineMod\config.json"
$runtimeConfigHash = $null

if (Get-Process -Name "Cities2" -ErrorAction SilentlyContinue) {
    throw "Cities: Skylines II is running. Close the game before building and deploying CityTimelineMod."
}

if (Test-Path -LiteralPath $runtimeConfig) {
    if (!(Test-Path -LiteralPath $runtimeConfig -PathType Leaf)) {
        throw "Expected the runtime user config to be a file: $runtimeConfig"
    }

    $runtimeConfigHash = (Get-FileHash -LiteralPath $runtimeConfig -Algorithm SHA256).Hash
    [Console]::WriteLine("Runtime user config detected and protected (read-only): $runtimeConfig")
}
else {
    [Console]::WriteLine("Runtime user config is absent: $runtimeConfig")
    [Console]::WriteLine("CityTimelineMod will create it at runtime; deploy.ps1 will not create or modify it.")
}

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

function Assert-SameFileHash {
    param(
        [Parameter(Mandatory=$true)][string]$Expected,
        [Parameter(Mandatory=$true)][string]$Actual,
        [Parameter(Mandatory=$true)][string]$Label
    )

    if (!(Test-Path -LiteralPath $Expected -PathType Leaf)) {
        throw "$Label source file is missing: $Expected"
    }
    if (!(Test-Path -LiteralPath $Actual -PathType Leaf)) {
        throw "$Label deployed file is missing: $Actual"
    }

    $expectedHash = (Get-FileHash -LiteralPath $Expected -Algorithm SHA256).Hash
    $actualHash = (Get-FileHash -LiteralPath $Actual -Algorithm SHA256).Hash
    if ($expectedHash -ne $actualHash) {
        throw "$Label hash mismatch. source=$expectedHash deployed=$actualHash"
    }

    [Console]::WriteLine("Verified $Label SHA256: $actualHash")
}

function TryRemove-BuildPathIfExists {
    param(
        [Parameter(Mandatory=$true)][string]$Path,
        [Parameter(Mandatory=$true)][string]$Label
    )

    try {
        Remove-PathIfExists -Path $Path -ExpectedParent $projectRoot
        return $true
    }
    catch {
        Write-Warning (
            "$Label remains locked and was left for a later cleanup: " +
            $_.Exception.Message
        )
        return $false
    }
}

# Any failure after staging begins must leave neither a partial active mod nor
# an orphan staging directory. The active-directory swap has its own rollback.
trap {
    try {
        Remove-PathIfExists -Path $stagingDst -ExpectedParent $localModsRoot
    }
    catch {
        Write-Warning "Failed to clean deployment staging directory: $($_.Exception.Message)"
    }

    TryRemove-BuildPathIfExists `
        -Path $toolchainDeployRoot `
        -Label "SDK build staging directory" | Out-Null
    TryRemove-BuildPathIfExists `
        -Path $msbuildTempRoot `
        -Label "MSBuild temporary directory" | Out-Null

    throw
}

# 1) Clean and build DLL.
# The CS2 SDK normally runs its own DeployWIP target during a build. Redirect
# that target inside the repository so it cannot partially update the active
# game mod before this script has assembled and validated the complete payload.
TryRemove-BuildPathIfExists `
    -Path $toolchainDeployRoot `
    -Label "SDK build staging directory" | Out-Null
New-Item -ItemType Directory -Force -Path $toolchainDeployRoot | Out-Null
Remove-PathIfExists -Path $msbuildTempRoot -ExpectedParent $projectRoot
New-Item -ItemType Directory -Force -Path $msbuildTempRoot | Out-Null
$env:TEMP = $msbuildTempRoot
$env:TMP = $msbuildTempRoot

$buildStartedUtc = [DateTime]::UtcNow
$dotnetOutput = dotnet clean $csproj -c $buildConfiguration --nologo -v:q `
    "-p:UseSharedCompilation=false" `
    "-p:LocalModsPath=$toolchainDeployRoot" 2>&1
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

function Read-ActiveBundleId {
    param(
        [Parameter(Mandatory=$true)][string]$IndexPath,
        [Parameter(Mandatory=$true)][string]$Label
    )

    if (!(Test-Path -LiteralPath $IndexPath -PathType Leaf)) {
        return $null
    }

    try {
        $index = Get-Content -LiteralPath $IndexPath -Raw | ConvertFrom-Json
        $activeBundleId = [string]$index.activeBundleId
        if ([string]::IsNullOrWhiteSpace($activeBundleId)) {
            return $null
        }

        return $activeBundleId.Trim()
    }
    catch {
        Write-Warning "$Label active bundle pointer could not be read: $IndexPath ($($_.Exception.Message))"
        return $null
    }
}

function Set-StagedActiveBundleId {
    param(
        [Parameter(Mandatory=$true)][string]$IndexPath,
        [string]$PreferredActiveBundleId
    )

    $index = Get-Content -LiteralPath $IndexPath -Raw | ConvertFrom-Json
    $entries = @($index.bundles)
    if ($entries.Count -eq 0) {
        throw "Staged bundle catalog has no entries: $IndexPath"
    }

    $matchingEntry = $null
    $selectionSource = "installed runtime state"
    if (![string]::IsNullOrWhiteSpace($PreferredActiveBundleId)) {
        foreach ($entry in $entries) {
            if ([string]::Equals(
                [string]$entry.id,
                $PreferredActiveBundleId,
                [System.StringComparison]::OrdinalIgnoreCase
            )) {
                $matchingEntry = $entry
                break
            }
        }
    }

    if ($null -eq $matchingEntry -and
        ![string]::IsNullOrWhiteSpace($PreferredActiveBundleId)) {
        Write-Warning (
            "Previously selected bundle '$PreferredActiveBundleId' is absent from the new catalog. " +
            "A validated catalog fallback will be used."
        )
    }

    if ($null -eq $matchingEntry) {
        $sourceActiveBundleId = [string]$index.activeBundleId
        foreach ($entry in $entries) {
            if ([string]::Equals(
                [string]$entry.id,
                $sourceActiveBundleId,
                [System.StringComparison]::OrdinalIgnoreCase
            )) {
                $matchingEntry = $entry
                $selectionSource = "RealMap source default"
                break
            }
        }
    }

    if ($null -eq $matchingEntry) {
        $matchingEntry = $entries[0]
        $selectionSource = "first catalog entry fallback"
    }

    $canonicalId = ([string]$matchingEntry.id).Trim()
    Add-Member `
        -InputObject $index `
        -NotePropertyName "activeBundleId" `
        -NotePropertyValue $canonicalId `
        -Force

    $json = $index | ConvertTo-Json -Depth 100
    [System.IO.File]::WriteAllText(
        $IndexPath,
        $json + [Environment]::NewLine,
        [System.Text.UTF8Encoding]::new($false)
    )

    $verified = Get-Content -LiteralPath $IndexPath -Raw | ConvertFrom-Json
    if (![string]::Equals(
        [string]$verified.activeBundleId,
        $canonicalId,
        [System.StringComparison]::Ordinal
    )) {
        throw "Staged bundle active pointer verification failed: $IndexPath"
    }

    [Console]::WriteLine(
        "Staged bundle selection resolved: activeBundleId=$canonicalId, source=$selectionSource"
    )
    return $canonicalId
}

[Console]::WriteLine("Vérification des dépendances du projet en cours...")
$dotnetOutput = dotnet build $csproj -c $buildConfiguration --no-restore --nologo -v:q `
    "-p:UseSharedCompilation=false" `
    "-p:LocalModsPath=$toolchainDeployRoot" 2>&1
if ($LASTEXITCODE -ne 0) {
    $dotnetOutput | Out-Host
    throw "dotnet build failed"
}

if (!(Test-Path $dll)) {
    throw "Build succeeded but DLL was not found: $dll"
}

foreach ($nativeArtifactName in $nativeArtifactNames) {
    $nativeArtifact = Join-Path $bin $nativeArtifactName
    if (!(Test-Path -LiteralPath $nativeArtifact -PathType Leaf)) {
        throw "Build succeeded but a native runtime artifact was not found: $nativeArtifact"
    }

    if ((Get-Item -LiteralPath $nativeArtifact).LastWriteTimeUtc -lt $buildStartedUtc.AddSeconds(-2)) {
        throw "Build output is stale: $nativeArtifact"
    }
}

if ((Get-Item -LiteralPath $dll).LastWriteTimeUtc -lt $buildStartedUtc.AddSeconds(-2)) {
    throw "Build output is stale: $dll"
}

TryRemove-BuildPathIfExists `
    -Path $toolchainDeployRoot `
    -Label "SDK build staging directory" | Out-Null

[Console]::WriteLine("Compilation C# réussie — les fichiers source n’ont pas été restaurés ni modifiés.")

# 2) Build frontend. Webpack cleans the generated UI folder before emitting files.
if (!(Test-Path $uiSrc)) {
    throw "UI source directory missing: $uiSrc"
}

Push-Location $uiSrc
try {
    $nodeOverride = $env:CITYTIMELINE_NODE_EXE
    if ([string]::IsNullOrWhiteSpace($nodeOverride)) {
        $webpackOutput = npm.cmd run build 2>&1
    }
    else {
        if (!(Test-Path -LiteralPath $nodeOverride -PathType Leaf)) {
            throw "CITYTIMELINE_NODE_EXE does not identify a Node executable: $nodeOverride"
        }

        $webpackOutput = @(& $nodeOverride "scripts\verify-bindings.mjs" 2>&1)
        if ($LASTEXITCODE -eq 0) {
            $webpackOutput += @(& $nodeOverride "scripts\verify-css-scope.mjs" 2>&1)
        }
        if ($LASTEXITCODE -eq 0) {
            $webpackOutput += @(
                & $nodeOverride "node_modules\webpack\bin\webpack.js" `
                    "--config" "webpack.config.js" "--mode" "production" 2>&1
            )
        }
    }

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
    (Join-Path $uiOut "CityTimelineMod.mjs.LICENSE.txt"),
    (Join-Path $uiOut "fonts\Overpass-Regular.ttf")
)

foreach ($requiredGeneratedUIFile in $requiredGeneratedUIFiles) {
    if (!(Test-Path $requiredGeneratedUIFile)) {
        throw "Frontend build validation failed. Missing generated file: $requiredGeneratedUIFile"
    }
}

# 3) Assemble a complete clean payload away from the active mod directory.
# RealMap is the source of truth. Deploy the complete catalog, not only the
# active bundle: every directory listed in bundle_index.json is mirrored. The
# catalog comes from RealMap, while the installed activeBundleId is runtime user
# state and is carried into staging when it still names a catalog entry.
if (-not (Test-Path -LiteralPath $realmapBundlesRoot)) {
    throw "RealMap source directory is missing or invalid: $realmapBundlesRoot (set CITYTIMELINE_REALMAP_BUNDLES_ROOT if needed)"
}

$sourceBundleCount = Assert-BundleCatalog -Root $realmapBundlesRoot -Label "RealMap source"
$installedBundleIndex = Join-Path $installDst "data\exports\bundles\bundle_index.json"
$preservedActiveBundleId = Read-ActiveBundleId `
    -IndexPath $installedBundleIndex `
    -Label "Installed"

New-Item -ItemType Directory -Force -Path $localModsRoot | Out-Null
Remove-PathIfExists -Path $stagingDst -ExpectedParent $localModsRoot
New-Item -ItemType Directory -Force -Path $dst | Out-Null

$canonicalBundles = Join-Path $dst "data\exports\bundles"
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

$stagedBundleIndex = Join-Path $canonicalBundles "bundle_index.json"
$resolvedStagedActiveBundleId = Set-StagedActiveBundleId `
    -IndexPath $stagedBundleIndex `
    -PreferredActiveBundleId $preservedActiveBundleId
$deployedBundleCount = Assert-BundleCatalog -Root $canonicalBundles -Label "Deployed"

# 4) Stage DLL and Harmony.
Copy-Item -Force $dll (Join-Path $dst "CityTimelineMod.dll")

foreach ($nativeArtifactName in $nativeArtifactNames) {
    Copy-Item -Force (Join-Path $bin $nativeArtifactName) (Join-Path $dst $nativeArtifactName)
}

if (Test-Path $harmonyDll) {
    Copy-Item -Force $harmonyDll (Join-Path $dst "0Harmony.dll")
}
else {
    throw "0Harmony.dll not found in build output: $harmonyDll"
}

# 5) Stage data safely.
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

# 6) Stage the single CS2 UI module in the code mod root.
Copy-Item -Force (Join-Path $uiOut "CityTimelineMod.mjs") (Join-Path $dst "CityTimelineMod.mjs")
Copy-Item -Force (Join-Path $uiOut "CityTimelineMod.css") (Join-Path $dst "CityTimelineMod.css")
Copy-Item -Force (Join-Path $uiOut "CityTimelineMod.mjs.LICENSE.txt") (Join-Path $dst "CityTimelineMod.mjs.LICENSE.txt")

$dstFonts = Join-Path $dst "fonts"
New-Item -ItemType Directory -Force -Path $dstFonts | Out-Null
Copy-Item -Force (Join-Path $uiOut "fonts\Overpass-Regular.ttf") (Join-Path $dstFonts "Overpass-Regular.ttf")

# 7) Config policy.
# The mod-root config.json is only a legacy/default migration seed.
# Runtime owns ModsSettings\CityTimelineMod\config.json and deploy.ps1 never writes it.
if (!(Test-Path -LiteralPath $repoConfig -PathType Leaf)) {
    throw "No legacy/default config migration seed or repository default was found. Cannot deploy safely."
}
Copy-Item -Force $repoConfig $legacyConfig
[Console]::WriteLine("Staged repository default config migration seed: $legacyConfig")

# 8) Stage the code mod manifest from the repository source of truth.
if (!(Test-Path -LiteralPath $repoManifest)) {
    throw "Repository mod.json not found: $repoManifest"
}

Copy-Item -Force $repoManifest (Join-Path $dst "mod.json")

# 9) Validate the complete staged runtime payload.
$requiredFiles = @(
    (Join-Path $dst "CityTimelineMod.dll"),
    (Join-Path $dst "CityTimelineMod_win_x86_64.dll"),
    (Join-Path $dst "CityTimelineMod_linux_x86_64.so"),
    (Join-Path $dst "CityTimelineMod_mac_x86_64.bundle"),
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
    (Join-Path $dst "CityTimelineMod.mjs.LICENSE.txt"),
    (Join-Path $dst "fonts\Overpass-Regular.ttf")
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
    (Join-Path $dst "vendor")
)

foreach ($forbiddenPath in $forbiddenPaths) {
    if (Test-Path -LiteralPath $forbiddenPath) {
        throw "Deployment validation failed. Obsolete UI artifact still exists: $forbiddenPath"
    }
}

Assert-SameFileHash -Expected $dll -Actual (Join-Path $dst "CityTimelineMod.dll") -Label "CityTimelineMod.dll staging"
foreach ($nativeArtifactName in $nativeArtifactNames) {
    Assert-SameFileHash -Expected (Join-Path $bin $nativeArtifactName) -Actual (Join-Path $dst $nativeArtifactName) -Label "$nativeArtifactName staging"
}
Assert-SameFileHash -Expected $harmonyDll -Actual (Join-Path $dst "0Harmony.dll") -Label "0Harmony.dll staging"
Assert-SameFileHash -Expected (Join-Path $uiOut "CityTimelineMod.mjs") -Actual (Join-Path $dst "CityTimelineMod.mjs") -Label "CityTimelineMod.mjs staging"
Assert-SameFileHash -Expected (Join-Path $uiOut "CityTimelineMod.css") -Actual (Join-Path $dst "CityTimelineMod.css") -Label "CityTimelineMod.css staging"
Assert-SameFileHash -Expected (Join-Path $uiOut "CityTimelineMod.mjs.LICENSE.txt") -Actual (Join-Path $dst "CityTimelineMod.mjs.LICENSE.txt") -Label "CityTimelineMod UI license staging"
Assert-SameFileHash -Expected (Join-Path $uiOut "fonts\Overpass-Regular.ttf") -Actual (Join-Path $dst "fonts\Overpass-Regular.ttf") -Label "Overpass font staging"
Assert-SameFileHash -Expected $repoConfig -Actual $legacyConfig -Label "default config staging"

# 10) Replace the previous mod directory only after the staged payload is complete.
Remove-PathIfExists -Path $backupDst -ExpectedParent $localModsRoot
$previousInstallMoved = $false
try {
    if (Test-Path -LiteralPath $installDst) {
        Move-Item -LiteralPath $installDst -Destination $backupDst
        $previousInstallMoved = $true
    }

    Move-Item -LiteralPath $stagingDst -Destination $installDst
}
catch {
    if ($previousInstallMoved -and
        !(Test-Path -LiteralPath $installDst) -and
        (Test-Path -LiteralPath $backupDst)) {
        Move-Item -LiteralPath $backupDst -Destination $installDst
    }
    throw
}

Remove-PathIfExists -Path $backupDst -ExpectedParent $localModsRoot
Remove-PathIfExists -Path $legacyUiDst -ExpectedParent $localModsRoot
$dst = $installDst

$deployedBundleIndex = Join-Path $dst "data\exports\bundles\bundle_index.json"
$deployedActiveBundleId = Read-ActiveBundleId `
    -IndexPath $deployedBundleIndex `
    -Label "Deployed"
if (![string]::Equals(
    $deployedActiveBundleId,
    $resolvedStagedActiveBundleId,
    [System.StringComparison]::Ordinal
)) {
    throw (
        "Deployed bundle selection differs from the validated staging pointer. " +
        "staging=$resolvedStagedActiveBundleId deployed=$deployedActiveBundleId"
    )
}
[Console]::WriteLine(
    "Verified deployed bundle selection: activeBundleId=$deployedActiveBundleId"
)

Assert-SameFileHash -Expected $dll -Actual (Join-Path $dst "CityTimelineMod.dll") -Label "CityTimelineMod.dll deployed"
foreach ($nativeArtifactName in $nativeArtifactNames) {
    Assert-SameFileHash -Expected (Join-Path $bin $nativeArtifactName) -Actual (Join-Path $dst $nativeArtifactName) -Label "$nativeArtifactName deployed"
}
Assert-SameFileHash -Expected $harmonyDll -Actual (Join-Path $dst "0Harmony.dll") -Label "0Harmony.dll deployed"
Assert-SameFileHash -Expected (Join-Path $uiOut "CityTimelineMod.mjs") -Actual (Join-Path $dst "CityTimelineMod.mjs") -Label "CityTimelineMod.mjs deployed"
Assert-SameFileHash -Expected (Join-Path $uiOut "CityTimelineMod.css") -Actual (Join-Path $dst "CityTimelineMod.css") -Label "CityTimelineMod.css deployed"
Assert-SameFileHash -Expected (Join-Path $uiOut "CityTimelineMod.mjs.LICENSE.txt") -Actual (Join-Path $dst "CityTimelineMod.mjs.LICENSE.txt") -Label "CityTimelineMod UI license deployed"
Assert-SameFileHash -Expected (Join-Path $uiOut "fonts\Overpass-Regular.ttf") -Actual (Join-Path $dst "fonts\Overpass-Regular.ttf") -Label "Overpass font deployed"

if ($null -ne $runtimeConfigHash) {
    if (!(Test-Path -LiteralPath $runtimeConfig -PathType Leaf)) {
        throw "Runtime user config disappeared during deployment: $runtimeConfig"
    }

    $runtimeConfigHashAfter = (Get-FileHash -LiteralPath $runtimeConfig -Algorithm SHA256).Hash
    if ($runtimeConfigHashAfter -ne $runtimeConfigHash) {
        throw "Runtime user config changed during deployment: $runtimeConfig"
    }

    [Console]::WriteLine("Runtime user config preserved byte-for-byte: $runtimeConfig")
}

# 11) Clean generated transient files only.
Get-ChildItem $dst -Filter "ilpp.pid" -ErrorAction SilentlyContinue |
    Remove-Item -Force -ErrorAction SilentlyContinue

TryRemove-BuildPathIfExists `
    -Path $toolchainDeployRoot `
    -Label "SDK build staging directory" | Out-Null
TryRemove-BuildPathIfExists `
    -Path $msbuildTempRoot `
    -Label "MSBuild temporary directory" | Out-Null

[Console]::WriteLine("Deploy OK: $dst")
