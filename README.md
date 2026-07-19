# CityTimelineMod

Mod de développement pour Cities: Skylines II.

Il charge un bundle GeoJSON déjà généré, affiche ses couches sur la carte du jeu, aide à régler le calage terrain et permet de tester un import manuel limité de routes.

Le mod ne génère pas les données et ne télécharge rien. Le bundle GeoJSON doit être préparé avant de lancer le jeu.

## Contenu

```text
src/CityTimelineMod      backend C# du mod
src/CityTimelineMod.UI   interface React/TypeScript
resources/defaults       configuration par défaut
resources/legacy-geojson données de secours
packaging                manifeste du mod
scripts                  build, déploiement, outils runtime
docs                     documentation complémentaire
```

## Prérequis

- Cities: Skylines II installé.
- Toolchain de modding CS2 initialisée.
- `dotnet` disponible.
- Node.js 20.9+ et npm disponibles.
- Variable utilisateur `CSII_TOOLPATH` configurée par l’outil de modding.

## Installation locale

Depuis la racine du dépôt :

```powershell
.\scripts\deploy.ps1
```

Le script compile le mod, compile l’UI, puis déploie les fichiers dans :

```text
%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\Mods\CityTimelineMod
```

Le fichier réellement lu par le jeu est :

```text
%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\Mods\CityTimelineMod\config.json
```

Le déploiement conserve ce `config.json` runtime s’il existe déjà.

## Bundle GeoJSON

Structure attendue :

```text
mon_bundle/
  manifest.json
  geojson/
    water_lines_clipped.geojson
    water_areas_clipped.geojson
    zoning_polygons.geojson
    roads_major_clipped.geojson
    roads_driveable_clipped.geojson
    paths.geojson
```

Configuration directe :

```json
{
  "packPath": "C:\\chemin\\vers\\mon_bundle",
  "bundleManifestPath": "C:\\chemin\\vers\\mon_bundle\\manifest.json",
  "useBundleIndex": false
}
```

Configuration via catalogue :

```json
{
  "useBundleIndex": true,
  "bundlesRoot": "C:\\chemin\\vers\\bundles",
  "activeBundleId": "id_du_bundle"
}
```

`bundlesRoot` doit contenir un `bundle_index.json`.

## Utilisation

- `Alt + H` : ouvrir ou fermer le HUD principal.
- `Alt + T` : ouvrir ou fermer le mini contrôleur.
- `Appliquer / reconstruire` : reconstruire l’overlay après un changement.
- `Sauvegarder visuels` : écrire les réglages dans le `config.json` runtime.
- `Recharger visuels` : relire le `config.json` runtime.

L’import de routes est expérimental. Commencer avec un petit budget, par exemple `runtimeRoadImportMaxSegments: 5`, puis augmenter progressivement.

## Commandes utiles

Build C# seul :

```powershell
dotnet build .\src\CityTimelineMod\CityTimelineMod.csproj -c Debug
```

Build UI seul :

```powershell
cd .\src\CityTimelineMod.UI
npm install
npm run build
```

Activer ou désactiver le mod :

```powershell
.\scripts\runtime\set-runtime-enabled.ps1 -Enabled $true
.\scripts\runtime\set-runtime-enabled.ps1 -Enabled $false
```

Changer le preset d’overlay :

```powershell
.\scripts\runtime\set-overlay-preset.ps1 -Preset all
.\scripts\runtime\set-overlay-preset.ps1 -Preset roads
.\scripts\runtime\set-overlay-preset.ps1 -Preset water
.\scripts\runtime\set-overlay-preset.ps1 -Preset zoning
.\scripts\runtime\set-overlay-preset.ps1 -Preset off
```

Audit runtime :

```powershell
.\scripts\runtime\audit-runtime.ps1
```

## Logs

```powershell
$log = "$env:USERPROFILE\AppData\LocalLow\Colossal Order\Cities Skylines II\Player.log"
Get-Content $log -Wait -Tail 700 |
  Select-String "\[RoadImport\]|\[RoadTopology\]|\[RoadImportDiag\]|Exception|Error"
```

Documentation plus détaillée : `docs/`.
