# Set-OverlayPreset.ps1
# Runtime overlay presets for CityTimelineMod.
# This edits the deployed config.json read by Cities: Skylines II at mod startup.

param(
    [ValidateSet(
        "all",
        "off",
        "noZoning",
        "zoning",
        "roads",
        "water",
        "commercial",
        "residential",
        "industrial",
        "office",
        "parking"
    )]
    [string]$Preset = "all",

    [string]$ConfigPath = $(Join-Path $env:USERPROFILE "AppData\LocalLow\Colossal Order\Cities Skylines II\Mods\CityTimelineMod\config.json")
)

$ErrorActionPreference = "Stop"

if (!(Test-Path $ConfigPath)) {
    throw "Runtime config not found: $ConfigPath"
}

$json = Get-Content $ConfigPath -Raw -Encoding UTF8 | ConvertFrom-Json

function Set-JsonProperty {
    param(
        [Parameter(Mandatory = $true)] $Object,
        [Parameter(Mandatory = $true)] [string] $Name,
        [Parameter(Mandatory = $true)] $Value
    )

    if ($null -ne $Object.PSObject.Properties[$Name]) {
        $Object.$Name = $Value
    }
    else {
        $Object | Add-Member -MemberType NoteProperty -Name $Name -Value $Value
    }
}

function Set-OverlayAlphaDefaults {
    Set-JsonProperty $json "zoningAlpha" 0.05
    Set-JsonProperty $json "waterAreaFillAlpha" 0.05
    Set-JsonProperty $json "roadAlpha" 0.20
    Set-JsonProperty $json "waterLineAlpha" 0.20
    Set-JsonProperty $json "waterAreaOutlineAlpha" 0.20
}

function Set-BaseVisibleAll {
    Set-JsonProperty $json "renderWaterLines" $true
    Set-JsonProperty $json "renderWaterAreas" $true
    Set-JsonProperty $json "renderWaterAreaOutlines" $true
    Set-JsonProperty $json "renderWaterAreaFillMeshes" $true
    Set-JsonProperty $json "renderRoads" $true

    Set-JsonProperty $json "renderZoning" $true
    Set-JsonProperty $json "maxZoningFillMeshesDebug" 5000
    Set-JsonProperty $json "zoningDebugFilterZone" ""
    Set-JsonProperty $json "zoningDebugFilterCs2Contains" ""

    Set-JsonProperty $json "roadYOffset" 0.25
    Set-JsonProperty $json "waterLineYOffset" 0.35
    Set-JsonProperty $json "waterAreaOutlineYOffset" 0.30
    Set-JsonProperty $json "zoningFillYOffset" 0.05
    Set-JsonProperty $json "waterAreaFillYOffset" 0.03

    Set-OverlayAlphaDefaults
}

function Set-AllOff {
    Set-JsonProperty $json "renderWaterLines" $false
    Set-JsonProperty $json "renderWaterAreas" $false
    Set-JsonProperty $json "renderWaterAreaOutlines" $false
    Set-JsonProperty $json "renderWaterAreaFillMeshes" $false
    Set-JsonProperty $json "renderRoads" $false
    Set-JsonProperty $json "renderZoning" $false

    Set-OverlayAlphaDefaults
}

switch ($Preset) {
    "all" {
        Set-BaseVisibleAll
    }

    "off" {
        Set-AllOff
    }

    "noZoning" {
        Set-BaseVisibleAll
        Set-JsonProperty $json "renderZoning" $false
    }

    "zoning" {
        Set-AllOff
        Set-JsonProperty $json "renderZoning" $true
        Set-JsonProperty $json "maxZoningFillMeshesDebug" 5000
        Set-JsonProperty $json "zoningDebugFilterZone" ""
        Set-JsonProperty $json "zoningDebugFilterCs2Contains" ""
        Set-JsonProperty $json "zoningFillYOffset" 0.05
    }

    "roads" {
        Set-AllOff
        Set-JsonProperty $json "renderRoads" $true
        Set-JsonProperty $json "roadYOffset" 0.25
    }

    "water" {
        Set-AllOff
        Set-JsonProperty $json "renderWaterLines" $true
        Set-JsonProperty $json "renderWaterAreas" $true
        Set-JsonProperty $json "renderWaterAreaOutlines" $true
        Set-JsonProperty $json "renderWaterAreaFillMeshes" $true
        Set-JsonProperty $json "waterLineYOffset" 0.35
        Set-JsonProperty $json "waterAreaOutlineYOffset" 0.30
        Set-JsonProperty $json "waterAreaFillYOffset" 0.03
    }

    "commercial" {
        Set-BaseVisibleAll
        Set-JsonProperty $json "zoningDebugFilterCs2Contains" "Commer"
    }

    "residential" {
        Set-BaseVisibleAll
        Set-JsonProperty $json "zoningDebugFilterCs2Contains" "Résidentiel"
    }

    "industrial" {
        Set-BaseVisibleAll
        Set-JsonProperty $json "zoningDebugFilterCs2Contains" "Industrie"
    }

    "office" {
        Set-BaseVisibleAll
        Set-JsonProperty $json "zoningDebugFilterCs2Contains" "Bureaux"
    }

    "parking" {
        Set-BaseVisibleAll
        Set-JsonProperty $json "zoningDebugFilterCs2Contains" "Parking"
    }
}

$json | ConvertTo-Json -Depth 50 | Set-Content -LiteralPath $ConfigPath -Encoding utf8NoBOM

Write-Host "OK preset applied: $Preset"
Write-Host "Runtime config: $ConfigPath"

[pscustomobject]@{
    renderWaterLines = $json.renderWaterLines
    renderWaterAreas = $json.renderWaterAreas
    renderWaterAreaOutlines = $json.renderWaterAreaOutlines
    renderWaterAreaFillMeshes = $json.renderWaterAreaFillMeshes
    renderRoads = $json.renderRoads
    renderZoning = $json.renderZoning
    maxZoningFillMeshesDebug = $json.maxZoningFillMeshesDebug
    zoningDebugFilterZone = $json.zoningDebugFilterZone
    zoningDebugFilterCs2Contains = $json.zoningDebugFilterCs2Contains
    roadYOffset = $json.roadYOffset
    waterLineYOffset = $json.waterLineYOffset
    waterAreaOutlineYOffset = $json.waterAreaOutlineYOffset
    zoningFillYOffset = $json.zoningFillYOffset
    waterAreaFillYOffset = $json.waterAreaFillYOffset
    zoningAlpha = $json.zoningAlpha
    waterAreaFillAlpha = $json.waterAreaFillAlpha
    roadAlpha = $json.roadAlpha
    waterLineAlpha = $json.waterLineAlpha
    waterAreaOutlineAlpha = $json.waterAreaOutlineAlpha
} | Format-List
