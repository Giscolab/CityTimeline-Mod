# CityTimelineMod

Mod de developpement pour Cities: Skylines II. Il sert a charger un bundle GeoJSON reel, afficher ses couches dans une carte CS2, regler le calage sur le terrain, puis tester un import manuel limite de routes CS2 depuis les routes GeoJSON mises en cache.

Le mod ne genere pas les donnees. Il ne lance pas Overpass. Il ne telecharge rien tout seul. Le bundle GeoJSON doit etre produit avant, hors du jeu.

## Arborescence

```text
CityTimelineMod/
├─ src/
│  ├─ CityTimelineMod/          # backend C# et projet .NET
│  └─ CityTimelineMod.UI/       # frontend React/TypeScript
├─ resources/
│  ├─ defaults/                 # configuration par défaut versionnée
│  └─ legacy-geojson/           # secours GeoJSON embarqués
├─ packaging/                   # manifeste du mod
├─ scripts/                     # build, déploiement et outils runtime
├─ docs/                        # documentation
└─ artifacts/                   # sorties générées, ignorées par Git
```

`bin`, `obj`, `node_modules`, `.git`, copies AppData et UI compilée ne font pas partie du code source.

## Ce Que Fait Le Mod

- Charge une configuration runtime `config.json`.
- Resout un bundle via `packPath`, `bundleManifestPath`, ou `useBundleIndex` + `bundlesRoot` + `activeBundleId`.
- Lit les couches GeoJSON du bundle.
- Affiche un overlay de controle dans CS2 :
  - routes ;
  - chemins ;
  - lignes et surfaces d'eau ;
  - zoning ;
  - limites heightmap / world map ;
  - centre de carte.
- Pose l'overlay sur le terrain CS2 via echantillonnage de hauteur.
- Fournit un HUD en jeu pour regler affichage, filtres, budgets, couleurs, alphas et calage.
- Met les routes GeoJSON en cache, meme si l'overlay est masque.
- Peut importer manuellement des routes CS2 runtime, avec garde-fous et limites.
- Ajoute des diagnostics ECS pour comprendre pourquoi une route importee existe, disparait, ou reste invisible.

## Ce Que Le Mod Ne Fait Pas

- Il ne fabrique pas les fichiers GeoJSON.
- Il ne lance pas Overpass, MVT ou un pipeline OSM.
- Il ne choisit pas automatiquement une ville.
- Il n'importe pas automatiquement les routes au chargement.
- Il ne convertit pas encore les chemins en routes CS2.
- L'undo destructif des routes importees est desactive en V1 par securite.

## Donnees Attendues

Un bundle nominal ressemble a ceci :

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

Fichiers minimaux pour l'overlay :

```text
water_lines_clipped.geojson
water_areas_clipped.geojson
zoning_polygons.geojson
```

Fichiers routes utiles :

```text
roads_major_clipped.geojson      # axes principaux
roads_driveable_clipped.geojson  # reseau routier testable en import runtime
paths.geojson                    # chemins affichables, pas importes en routes V1
```

Si aucun bundle valide n'est configure, le mod retombe sur les donnees de secours dans `resources/legacy-geojson`.

## Installation Locale

Prerequis :

- Cities: Skylines II installe.
- Toolchain de modding CS2 initialisee.
- `dotnet` disponible.
- Node/npm disponible pour compiler l'UI.
- Variable utilisateur CS2 `CSII_TOOLPATH` creee par l'outil de modding.

Depuis le repo :

```powershell
.\scripts\deploy-local.ps1
```

Le script :

- compile la DLL du mod ;
- compile l'UI React ;
- copie les fichiers dans le dossier local CS2 ;
- deploie `mod.json`, la DLL, `0Harmony.dll`, l'UI et les donnees de secours ;
- conserve le `config.json` runtime deja present.

Destination locale :

```text
%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\Mods\CityTimelineMod
```

Le fichier de configuration vraiment lu par le jeu est :

```text
%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\Mods\CityTimelineMod\config.json
```

Important : le fichier runtime est la source de vérité locale. Le déploiement le conserve et ne réécrit jamais `resources/defaults/config.json`.

Pour réinitialiser le runtime depuis `resources/defaults/config.json` :

```powershell
Remove-Item "$env:USERPROFILE\AppData\LocalLow\Colossal Order\Cities Skylines II\Mods\CityTimelineMod\config.json"
.\scripts\deploy-local.ps1
```

## Configuration D'un Bundle

Utilisation directe d'un bundle :

```json
{
  "packPath": "C:\\chemin\\vers\\mon_bundle",
  "bundleManifestPath": "C:\\chemin\\vers\\mon_bundle\\manifest.json",
  "useBundleIndex": false
}
```

Utilisation d'un catalogue :

```json
{
  "useBundleIndex": true,
  "bundlesRoot": "C:\\chemin\\vers\\bundles",
  "activeBundleId": "id_du_bundle"
}
```

`bundlesRoot` doit contenir un `bundle_index.json`.

Profils overlay acceptes :

```text
custom
major
driveable
zoning
water
full
off
```

Reglages routes courants :

```json
{
  "roadGeometrySource": "driveable",
  "renderRoads": true,
  "renderPaths": true,
  "minimumRoadDebugTier": 0
}
```

## Utilisation En Jeu

- `Alt + H` : ouvrir ou fermer le HUD principal.
- `Alt + T` : ouvrir ou fermer le mini controleur global CTM.
- `Appliquer / reconstruire` : reconstruire l'overlay apres changement de budget, filtre ou calage.
- `Sauvegarder visuels` : ecrire les reglages visuels dans le `config.json` runtime.
- `Recharger visuels` : relire les reglages visuels depuis le `config.json` runtime.

Le HUD permet notamment de :

- choisir un bundle depuis le catalogue ;
- afficher ou masquer routes, chemins, eau, zoning et limites ;
- regler alphas, couleurs et modes de rendu ;
- changer les budgets de rendu ;
- filtrer les routes et labels ;
- afficher fleches one-way et noms de routes ;
- regler decalage X/Z, rotation, echelle et inversion ;
- lancer l'import manuel de routes runtime ;
- lancer les diagnostics de contenu, mapping prefab et routes importees.

## Import Manuel De Routes CS2

L'import runtime est experimental, manuel et volontairement bride.

Regles V1 :

```text
GeoJSON bundle = source officielle
Commande manuelle = seul declencheur
runtimeRoadImportEnabled = garde-fou global
paths.geojson = exclu de l'import routes
undo destructif = desactive par securite
```

Configuration prudente pour un premier test :

```json
{
  "runtimeRoadImportEnabled": true,
  "runtimeRoadImportRunOnce": false,
  "runtimeRoadImportMaxSegments": 5,
  "runtimeRoadImportStride": 1,
  "runtimeRoadImportMinSegmentLengthMeters": 2.0,
  "runtimeRoadImportSnapToleranceMeters": 4.0,
  "runtimeRoadImportYOffset": 0.15,
  "runtimeRoadImportIncludeBridgeTunnel": false,
  "runtimeRoadImportSourceFilter": "roads",
  "runtimeRoadImportHighwayFilter": "all",
  "runtimeRoadImportStageFilter": "all",
  "runtimeRoadImportSelectionMode": "staged-cell-priority",
  "runtimeRoadImportBatchSize": 256,
  "runtimeRoadImportPipelineMode": "fast-flush"
}
```

Flux attendu :

```text
bundle charge
-> routes GeoJSON mises en cache
-> clic "Importer routes runtime"
-> selection / filtrage / priorisation
-> topologie runtime
-> RuntimeRoadToolSystem cree les edges/nodes CS2
-> progression visible dans le HUD
```

Conseil pratique : tester avec `runtimeRoadImportMaxSegments=5`, puis monter progressivement a `50`, `500`, puis plus seulement si les logs restent propres.

## Logs Et Diagnostic

Log joueur :

```powershell
$log = "$env:USERPROFILE\AppData\LocalLow\Colossal Order\Cities Skylines II\Player.log"
Get-Content $log -Wait -Tail 700 |
  Select-String "\[RoadImport\]|\[RoadTopology\]|\[RoadImportDiag\]|EdgeGeometry|Temp|Updated|verdict|Exception|Error"
```

Signaux utiles :

```text
cached road lines
manual import requested
road import order prepared
queuedSegments > 0
tagged imported road edges > 0
EdgeGeometry=True
```

Lecture rapide :

```text
Edge/Road existe mais EdgeGeometry=False -> probleme prefab/finalisation
EdgeGeometry=True mais invisible -> probleme position/hauteur/camera
queuedSegments=0 -> filtres, budget, prefab ou limites de carte a verifier
```

Audit local :

```powershell
.\scripts\runtime\audit-runtime.ps1
.\scripts\runtime\audit-runtime.ps1 -Build
```

## Scripts Utiles

Activer ou desactiver le mod au prochain chargement :

```powershell
.\scripts\runtime\set-runtime-enabled.ps1 -Enabled $true
.\scripts\runtime\set-runtime-enabled.ps1 -Enabled $false
```

Changer un preset d'affichage runtime :

```powershell
.\scripts\runtime\set-overlay-preset.ps1 -Preset roads
.\scripts\runtime\set-overlay-preset.ps1 -Preset water
.\scripts\runtime\set-overlay-preset.ps1 -Preset zoning
.\scripts\runtime\set-overlay-preset.ps1 -Preset all
.\scripts\runtime\set-overlay-preset.ps1 -Preset off
```

## Build Manuel

Build DLL seul :

```powershell
dotnet build .\src\CityTimelineMod\CityTimelineMod.csproj -c Debug
```

Build UI seul :

```powershell
cd .\src\CityTimelineMod.UI
npm install
npm run build
```

Build + deploiement complet :

```powershell
.\scripts\deploy-local.ps1
```

Dans un shell minimal, il peut etre necessaire de definir les variables Windows usuelles avant `dotnet` :

```powershell
$env:USERPROFILE = "C:\Users\cadet"
$env:APPDATA = "$env:USERPROFILE\AppData\Roaming"
$env:LOCALAPPDATA = "$env:USERPROFILE\AppData\Local"
$env:TEMP = "$env:LOCALAPPDATA\Temp"
$env:TMP = $env:TEMP
```

## Workflow Recommande

1. Generer le bundle GeoJSON hors du mod.
2. Verifier qu'il contient `geojson/` et les fichiers attendus.
3. Configurer `packPath` ou `useBundleIndex`.
4. Lancer `.\scripts\deploy-local.ps1`.
5. Ouvrir CS2 et charger une carte.
6. Ouvrir le HUD avec `Alt + H`.
7. Charger le bundle voulu si un catalogue est utilise.
8. Regler affichage, filtres et calage.
9. Sauvegarder les visuels quand le rendu est bon.
10. Tester l'import routes avec un petit budget.
11. Augmenter les budgets seulement apres verification des logs.

Pour garder le mod stable :

- garder l'import routes manuel ;
- importer `roads_driveable_clipped.geojson` ou `roads_major_clipped.geojson`, pas `paths.geojson` ;
- ne pas remettre d'auto-import au chargement ;
- ne pas deplacer Overpass/MVT/generation de bundle dans ce mod ;
- verifier les logs avant d'augmenter les budgets ;
- garder l'undo destructif des routes desactive tant que la suppression ECS n'est pas fiabilisee.
