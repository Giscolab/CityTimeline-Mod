param(
    [Parameter(Mandatory = $true)]
    [bool]$Enabled
)

$ErrorActionPreference = "Stop"

$modDir = Join-Path $env:USERPROFILE "AppData\LocalLow\Colossal Order\Cities Skylines II\Mods\CityTimelineMod"
$configPath = Join-Path $modDir "config.json"

Write-Host "Runtime config : $configPath"

if (!(Test-Path $configPath)) {
    throw "config.json runtime introuvable. Lance d'abord .\deploy.ps1"
}

$json = Get-Content $configPath -Raw | ConvertFrom-Json

if ($null -eq $json.PSObject.Properties["modEnabled"]) {
    $json | Add-Member -NotePropertyName "modEnabled" -NotePropertyValue $Enabled
}
else {
    $json.modEnabled = $Enabled
}

$json | ConvertTo-Json -Depth 64 | Set-Content $configPath -Encoding UTF8

Write-Host ""
Write-Host "[OK] modEnabled runtime = $Enabled"

Write-Host ""
Write-Host "État rapide :"
Get-Content $configPath -Raw | ConvertFrom-Json |
    Select-Object modEnabled,bundleManifestPath,packPath |
    Format-List

Write-Host "Note : si CS2 est déjà lancé, redémarre le jeu ou recharge le mod pour appliquer proprement l'état global."
