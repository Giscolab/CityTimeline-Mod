# CityTimelineMod

Mod de développement pour Cities: Skylines II.

Il charge un bundle GeoJSON déjà généré, affiche ses couches sur la carte du jeu, aide à régler le calage terrain et peut activer explicitement les modules expérimentaux LargeMap 57,344 km et PlayableWorld. L’ancien import runtime automatique des routes n’est pas inclus dans l’assembly distribué.

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
%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\ModsSettings\CityTimelineMod\config.json
```

Le `config.json` placé avec le mod est uniquement la valeur par défaut et la source de migration d’une ancienne installation. Le fichier sous `ModsSettings` est l’état utilisateur réellement lu et écrit ; les builds et packages ne doivent jamais l’écraser.

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

- `Alt + H` : ouvrir ou fermer le HUD IMGUI de rendu et de calage.
- `Alt + Z` par défaut, ou le bouton `CTM` : ouvrir ou fermer le HUD React/CoHTML.
- `Appliquer / reconstruire` : reconstruire l’overlay après un changement.
- `Sauvegarder visuels` : écrire les réglages dans le `config.json` runtime.
- `Recharger visuels` : relire le `config.json` runtime.

L’ancien import runtime des routes est exclu du build. Les modules expérimentaux
LargeMap et PlayableWorld sont désactivés par défaut et ne sont appliqués qu’au
prochain chargement après activation explicite de `largeMapEnabled` et
`playableWorldEnabled`.

## Commandes utiles

Build C# seul :

```powershell
$isolatedMods = Join-Path (Resolve-Path .) "src\CityTimelineMod\obj\debug-staging"
dotnet build .\src\CityTimelineMod\CityTimelineMod.csproj `
  -c Debug `
  -p:LocalModsPath="$isolatedMods"
```

Le projet CS2 déploie automatiquement après compilation. Fournir un
`LocalModsPath` isolé évite donc de remplacer le mod live pendant un simple
contrôle de build.

Build UI seul :

```powershell
cd .\src\CityTimelineMod.UI
npm.cmd ci
npm.cmd run build
```

Les options officielles ou le fichier suivant pilotent le prochain démarrage :

```powershell
$config = "$env:USERPROFILE\AppData\LocalLow\Colossal Order\Cities Skylines II\ModsSettings\CityTimelineMod\config.json"
Get-Content -LiteralPath $config
```

## Logs

```powershell
$log = "$env:USERPROFILE\AppData\LocalLow\Colossal Order\Cities Skylines II\Player.log"
Get-Content $log -Wait -Tail 700 |
  Select-String "CityTimelineMod|LargeMap|PlayableWorld|Exception|Error"
```

Documentation plus détaillée : `docs/`.
