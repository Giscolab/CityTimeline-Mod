# CityTimelineMod

Mod Cities: Skylines II de visualisation GeoJSON projetée dans le monde CS2.

Le mod charge un paquet GeoJSON préparé hors du jeu, transforme les coordonnées longitude/latitude en mètres monde CS2, échantillonne le terrain du jeu, puis affiche un overlay de contrôle pour comparer les données sources avec la carte :

- lignes et surfaces d'eau ;
- routes et chemins ;
- polygones de zoning ;
- limites worldmap / heightmap ;
- panneau HUD interactif pour régler visibilité, transparence, budgets de rendu et calage.

Ce dépôt ne construit pas les données OSM lui-même. Il consomme des fichiers GeoJSON déjà exportés par un pipeline externe.

## État Fonctionnel Actuel

- Le point d'entrée du mod est `Mod.OnLoad`.
- `Mod.OnLoad` lance `GeoBundleBootstrap.RunOnce()`.
- `GeoBundleBootstrap` résout le dossier runtime du mod, charge `config.json`, sélectionne un dossier GeoJSON, charge les couches disponibles, puis installe `GeoDebugOverlay`.
- Le flux actif est centré sur `packPath` / `bundleManifestPath`, pas sur l'ancien bootstrap Irvine.
- Les anciennes copies `*.before-*` dans `src/` sont des sauvegardes historiques. Elles ne sont pas compilées comme logique active.
- Le rendu principal utilise des meshes batchés Unity, avec matériaux transparents et limites de segments/polygones configurables.
- Les anciens raccourcis clavier de calibration sont neutralisés. Le réglage live passe par le panneau HUD.

## Flux Runtime

```text
Cities: Skylines II
  -> CityTimelineMod.Mod.OnLoad()
  -> GeoBundleBootstrap.RunOnce()
  -> GeoOverlayConfig.Load(modDir)
  -> lecture config.json
  -> application éventuelle du bundleManifestPath
  -> sélection packPath\geojson ou data\legacy-geojson
  -> parsing GeoJSON
  -> GeoDebugOverlay.Install(...)
  -> GroundOverlayBehaviour
  -> échantillonnage TerrainSystem + rendu overlay
```

Le dossier GeoJSON utilisé est choisi ainsi :

1. si `packPath` est renseigné et que `packPath\geojson` existe, le mod utilise ce dossier ;
2. sinon, il retombe sur `data\legacy-geojson` dans le dossier du mod ;
3. si `bundleManifestPath` est renseigné, `GeoOverlayConfig` peut appliquer l'origine, les limites worldmap/heightmap et dériver `packPath` depuis le bundle.

## Données Attendues

Dans le mode nominal, `packPath` doit pointer vers un dossier qui contient un sous-dossier `geojson`.

```text
geojson_pack/
  geojson/
    water_lines_clipped.geojson
    water_areas_clipped.geojson
    roads_major_clipped.geojson
    roads_driveable_clipped.geojson
    paths.geojson
    zoning_polygons.geojson
  reports/
    extraction_report.json
    layer_index.json
```

Les fichiers eau sont obligatoires pour que le bootstrap actuel crée l'overlay :

| Fichier | Rôle |
| --- | --- |
| `water_lines_clipped.geojson` | Lignes d'eau et source des bounds de référence. |
| `water_areas_clipped.geojson` | Contours et remplissage des surfaces d'eau. |

Les autres couches sont chargées si elles existent :

| Fichier | Rôle |
| --- | --- |
| `roads_major_clipped.geojson` | Routes majeures, utilisées avec `roadGeometrySource: "major"`. |
| `roads_driveable_clipped.geojson` | Réseau routier driveable, utilisé avec `roadGeometrySource: "driveable"`. |
| `paths.geojson` | Chemins/piétons/cyclables, marqués comme `IsPath`. |
| `zoning_polygons.geojson` | Polygones de zonage avec propriétés `zone`, `cs2`, `name`. |

Le dépôt embarque aussi un fallback minimal :

```text
data/legacy-geojson/
  bbox_manifest.json
  roads_major_clipped.geojson
  water_areas_clipped.geojson
  water_lines_clipped.geojson
  zoning_polygons.geojson
```

Ce fallback ne contient pas actuellement `roads_driveable_clipped.geojson` ni `paths.geojson`. Si le mod retombe dessus avec `roadGeometrySource: "driveable"`, les routes driveable ne seront pas affichées.

## Bundle Local Actuel

Le `config.json` versionné dans ce checkout pointe vers un bundle externe local :

```text
C:\Users\cadet\Documents\GitHub\cs2-minneapolis-zoning\exports\bundles\-117.723999_33.653495
```

Le manifest de ce bundle déclare notamment :

| Élément | Valeur |
| --- | --- |
| Centre | `lon=-117.723999`, `lat=33.653495` |
| Worldmap | `57.344 km` |
| Heightmap | `14.336 km`, PNG 4096 px |
| Bbox heightmap | `33.588870,-117.801275,33.718120,-117.646723` |

Le rapport d'extraction associé indique les volumes suivants :

| Couche | Nombre |
| --- | ---: |
| `zoning_polygons` | 2297 |
| `roads_major_clipped` | 7699 |
| `roads_driveable_clipped` | 29674 |
| `paths` | 7803 |
| `water_lines_clipped` | 423 |
| `water_areas_clipped` | 100 |
| `all_features` | 40297 |

Ces chemins sont locaux à la machine. Si le bundle est déplacé, modifier `packPath` et `bundleManifestPath` dans le `config.json` runtime.

## Arborescence Utile

```text
CityTimelineMod.sln
CityTimelineMod.csproj
Mod.cs
config.json
deploy.ps1
tools/
  Set-OverlayPreset.ps1
src/
  GeoBundleBootstrap.cs
  Config/GeoOverlayConfig.cs
  Diagnostics/TerrainApiProbe.cs
  Importers/GeoJson.cs
  Rendering/GeoDebugOverlay.cs
  Rendering/GeoTransform.cs
  Terrain/TerrainHeightSampler.cs
  Util/Log.cs
data/
  legacy-geojson/
    bbox_manifest.json
    roads_major_clipped.geojson
    water_areas_clipped.geojson
    water_lines_clipped.geojson
    zoning_polygons.geojson
Properties/
  PublishConfiguration.xml
  Thumbnail.png
  PublishProfiles/
```

`bin/`, `obj/` et `Library/` sont des artefacts de compilation/outillage.

## Documentation de développement

- [Workflow runtime local](docs/runtime-workflow.md)

## Responsabilités Des Fichiers

| Fichier | Rôle |
| --- | --- |
| `Mod.cs` | Implémentation `IMod`, initialise les logs et lance `GeoBundleBootstrap`. |
| `src/GeoBundleBootstrap.cs` | Résout le dossier du mod, choisit le bundle GeoJSON, charge eau/routes/chemins/zoning, installe l'overlay. |
| `src/Config/GeoOverlayConfig.cs` | Charge `config.json`, applique les profils, lit le manifest de bundle, valide les bornes de configuration. |
| `src/Importers/GeoJson.cs` | Parse `FeatureCollection`, `LineString`, `MultiLineString`, `Polygon`, `MultiPolygon` et propriétés routières/zoning. |
| `src/Rendering/GeoTransform.cs` | Convertit longitude/latitude en position monde CS2 selon origine, scale, flip et rotation. |
| `src/Rendering/GeoDebugOverlay.cs` | Crée le `GameObject` d'overlay, rend les couches, gère le HUD et les reconstructions live. |
| `src/Terrain/TerrainHeightSampler.cs` | Échantillonne la hauteur terrain via `TerrainSystem.GetHeightData(true)` et `TerrainUtils.SampleHeight`. |
| `src/Diagnostics/TerrainApiProbe.cs` | Diagnostic par reflection pour explorer les API terrain/heightmap. Non appelé par défaut. |
| `src/Util/Log.cs` | Journalisation double via `UnityEngine.Debug` et `Colossal.Logging`. |
| `tools/Set-OverlayPreset.ps1` | Modifie le `config.json` runtime déployé pour appliquer des presets de visibilité. |
| `deploy.ps1` | Compile en Debug, déploie DLL/data/config vers le dossier local CS2 et génère `mod.json`. |

## Configuration

Le fichier `config.json` à la racine du dépôt sert de configuration par défaut. Au runtime, le jeu lit la copie déployée :

```text
%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\Mods\CityTimelineMod\config.json
```

### Sources de données

| Clé | Rôle |
| --- | --- |
| `packPath` | Dossier contenant `geojson\`. Prioritaire si valide. |
| `bundleManifestPath` | Manifest de bundle exporté. Peut appliquer origine, limites et `packPath`. |
| `roadGeometrySource` | `major` ou `driveable`. |
| `overlayProfile` | Profil logique : `custom`, `major`, `driveable`, `zoning`, `water`, `full`, `off`. |

Effets des profils :

| Profil | Effet |
| --- | --- |
| `custom` | Respecte les flags explicites du JSON. |
| `major` | Routes majeures seules, sans chemins ni zoning/eau. |
| `driveable` | Routes driveable + chemins. |
| `zoning` | Zoning seul. |
| `water` | Eau seule. |
| `full` | Eau + routes driveable + chemins + zoning. |
| `off` | Toutes les couches désactivées. |

Limitation actuelle : le rendu démarre seulement si des lignes d'eau sont présentes dans les données passées à `GeoDebugOverlay`. Un profil qui désactive toute eau dès le bootstrap peut donc produire un overlay vide tant que ce garde-fou n'est pas assoupli dans le code.

### Transformation géographique

La projection est centralisée dans `GeoTransform.ToWorld(...)`.

Ordre appliqué :

1. conversion longitude/latitude en mètres autour de l'origine ;
2. application de `worldScale`, `overlayScaleX`, `overlayScaleZ` ;
3. application de `flipX` et `flipZ` ;
4. rotation horizontale via `overlayRotationDegrees` ;
5. translation finale via `worldOriginX` et `worldOriginZ`.

Clés principales :

| Clé | Rôle |
| --- | --- |
| `useGeoJsonCenter` | Utilise le centre calculé des géométries eau au lieu de `originLon/originLat`. |
| `originLon`, `originLat` | Origine géographique si `useGeoJsonCenter` vaut `false`. |
| `worldOriginX`, `worldOriginZ` | Décalage horizontal final en monde CS2. |
| `worldScale` | Échelle globale mètres réels vers mètres CS2. |
| `overlayRotationDegrees` | Rotation autour de l'origine. |
| `overlayScaleX`, `overlayScaleZ` | Correction non uniforme. |
| `flipX`, `flipZ` | Miroirs horizontaux. |

L'altitude n'est pas calculée dans `GeoTransform`. Chaque point rendu est ensuite posé sur le terrain via `TerrainHeightSampler`, puis relevé par `groundMargin` et les offsets de couche.

### Rendu et performance

| Clé | Rôle |
| --- | --- |
| `renderWaterLines` | Active les lignes d'eau. |
| `renderWaterAreas` | Active les surfaces d'eau. |
| `renderWaterAreaOutlines` | Active les contours des surfaces d'eau. |
| `renderWaterAreaFillMeshes` | Active le remplissage mesh des surfaces d'eau. |
| `renderRoads` | Active les routes. |
| `renderPaths` | Active les chemins, indépendamment de `renderRoads`. |
| `renderZoning` | Active les polygones de zoning. |
| `renderMapBounds` | Active les limites worldmap/heightmap. |
| `maxRoadSegmentsDebug` | Budget total de segments routes + chemins. |
| `maxWaterSegmentsDebug` | Budget de segments eau. |
| `maxWaterAreaFillMeshesDebug` | Budget de surfaces d'eau remplies. |
| `maxZoningFillMeshesDebug` | Budget de polygones zoning rendus. |
| `pointStride` | Pas d'échantillonnage des points GeoJSON. |
| `minimumRoadDebugTier` | Filtre hiérarchique routes de `0` à `4`. |

Les champs historiques `useSegmentCubes`, `useLineRenderer`, `lineWidth` et `maxRenderedSegments` sont encore lus ou sauvegardés, mais le rendu actif utilise des meshes batchés.

### Offsets et transparence

| Clé | Rôle |
| --- | --- |
| `roadYOffset`, `pathYOffset` | Décalage vertical routes/chemins. |
| `waterLineYOffset`, `waterAreaOutlineYOffset`, `waterAreaFillYOffset` | Décalages verticaux eau. |
| `zoningFillYOffset` | Décalage vertical zoning. |
| `mapBoundsYOffset` | Décalage vertical des limites worldmap/heightmap. |
| `roadAlpha`, `pathAlpha`, `waterLineAlpha`, `waterAreaOutlineAlpha`, `waterAreaFillAlpha`, `zoningAlpha`, `mapBoundsAlpha` | Transparence par couche. |

### Filtres

Routes :

| Clé | Rôle |
| --- | --- |
| `roadHighwayFilter` | `all`, `*` ou liste séparée par virgules de valeurs `highway`. |
| `pathHighwayFilter` | Même logique pour les chemins. |
| `minimumRoadDebugTier` | `0` tout afficher, `1` tertiaire+, `2` secondaire+, `3` primaire+, `4` trunk/motorway+. |

Zoning :

| Clé | Rôle |
| --- | --- |
| `zoningDebugFilterZone` | Filtre exact sur la propriété `zone`, ou liste séparée par virgules. |
| `zoningDebugFilterCs2Contains` | Filtre par sous-chaîne sur la propriété `cs2`. |
| `zoningDebugFilterMaterialKey` | Filtre exact sur le groupe matériau calculé. |

Groupes matériau zoning reconnus :

```text
residential_low
residential_medium
residential_high
commercial_low
retail_detail
industrial
office
parking_surface
parking_ramp
fallback
```

### Limites worldmap / heightmap

Si `renderMapBounds` vaut `true`, le mod dessine :

- un rectangle worldmap blanc ;
- un rectangle heightmap magenta ;
- un marqueur central rouge.

Deux modes existent :

| Clé | Rôle |
| --- | --- |
| `mapBoundsUseCenterAndSize: true` | Calcule les carrés depuis l'origine et `worldMapSizeKm` / `heightMapSizeKm`. |
| `mapBoundsUseCenterAndSize: false` | Utilise les bboxes explicites `worldMapMinLon`, `worldMapMinLat`, etc. |

## HUD En Jeu

Le panneau HUD est activé par défaut via `showOverlayHud` si la clé est absente ou vraie.

| Entrée | Action |
| --- | --- |
| `Alt + H` | Ouvre ou ferme le panneau HUD. |
| `Appliquer / reconstruire` | Reconstruit l'overlay avec les valeurs live. |
| `Fermer` | Cache le panneau HUD. |

Le panneau permet de régler :

- visibilité zoning/routes/chemins/eau/limites ;
- transparence des couches ;
- `groundMargin` ;
- budgets de rendu ;
- niveau minimum des routes ;
- `pointStride` ;
- calage avancé après déverrouillage : `worldOriginX`, `worldOriginZ`, `overlayRotationDegrees`, `overlayScaleX`, `overlayScaleZ`.

Les changements du HUD sont live uniquement. Ils ne sauvegardent pas `config.json`.

Les anciens raccourcis F6/F7/F8/F9 et les raccourcis de visibilité F10/F11/F12/Alt+B/Alt+D sont volontairement neutralisés dans l'état actuel. La méthode de sauvegarde de calibration existe encore côté code, mais elle n'est plus exposée par ces raccourcis.

## Presets Runtime

Le script `tools\Set-OverlayPreset.ps1` modifie le `config.json` déjà déployé, c'est-à-dire celui réellement lu par le jeu au prochain démarrage du mod.

Exemples :

```powershell
.\tools\Set-OverlayPreset.ps1 -Preset all
.\tools\Set-OverlayPreset.ps1 -Preset zoning
.\tools\Set-OverlayPreset.ps1 -Preset roads
.\tools\Set-OverlayPreset.ps1 -Preset water
.\tools\Set-OverlayPreset.ps1 -Preset commercial
.\tools\Set-OverlayPreset.ps1 -Preset residential
.\tools\Set-OverlayPreset.ps1 -Preset parking
.\tools\Set-OverlayPreset.ps1 -Preset off
```

Presets acceptés :

```text
all
off
noZoning
zoning
roads
water
commercial
residential
industrial
office
parking
```

Attention : le script agit sur les flags eau/routes/zoning et les filtres zoning, mais ne modifie pas actuellement `renderPaths`. Si les chemins doivent être coupés, vérifier ce champ dans le `config.json` runtime.

## Compilation et Déploiement

Ouvrir un terminal dans le dossier du projet :

```powershell
cd C:\Users\cadet\Documents\GitHub\CityTimelineMod
```

Compilation seule :

```powershell
dotnet build .\CityTimelineMod.sln
```

Déploiement local :

```powershell
.\deploy.ps1
```

Le script effectue :

1. sauvegarde temporaire du `config.json` runtime déjà déployé s'il existe ;
2. `dotnet clean` sur `CityTimelineMod.csproj` ;
3. `dotnet build` en `Debug` ;
4. vérification de `bin\Debug\net48\CityTimelineMod.dll` ;
5. suppression/recréation du dossier local du mod ;
6. copie de la DLL ;
7. copie de `data\` ;
8. restauration du `config.json` runtime existant, sinon copie du `config.json` du dépôt ;
9. génération de `mod.json` ;
10. validation des fichiers runtime requis.

Destination locale :

```text
%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\Mods\CityTimelineMod
```

Le script préserve volontairement la configuration runtime locale. Pour revenir aux valeurs par défaut du dépôt, supprimer le fichier runtime avant de relancer le déploiement :

```powershell
Remove-Item "$env:USERPROFILE\AppData\LocalLow\Colossal Order\Cities Skylines II\Mods\CityTimelineMod\config.json"
.\deploy.ps1
```

Règle de projet :

```text
repo/config.json      = valeurs par défaut versionnées
config runtime CS2    = valeurs locales de test réellement lues
deploy.ps1            = ne doit pas écraser un runtime config existant
```

## Prérequis De Compilation

- Cities: Skylines II installé avec le toolchain modding initialisé.
- SDK .NET disponible via `dotnet`.
- Variables utilisateur CS2 configurées par l'outil de modding du jeu, notamment :
  - `CSII_TOOLPATH`
  - `CSII_MANAGEDPATH`
  - `CSII_LOCALMODSPATH`
  - `CSII_USERDATAPATH`
  - `CSII_UNITYMODPROJECTPATH`
  - `CSII_MODPOSTPROCESSORPATH`
  - `CSII_MODPUBLISHERPATH`
  - `CSII_ENTITIESVERSION`
  - `CSII_MSCORLIBPATH`
- Accès aux assemblies Unity/CS2 référencées dans `CityTimelineMod.csproj`.
- `Newtonsoft.Json` est utilisé pour parser les GeoJSON, avec runtime exclu du package livré.

## Notes De Maintenance

- Garder le pipeline de génération OSM/GeoJSON hors de ce mod. Le mod consomme des exports prêts.
- `packPath` doit pointer vers le dossier parent de `geojson\`, pas directement vers `geojson\`.
- `bundleManifestPath` est utile pour synchroniser origine, bboxes worldmap/heightmap et chemin du bundle.
- Conserver `water_lines_clipped.geojson` disponible tant que `GeoDebugOverlay.CreateOverlay()` dépend de `_waterLines` pour démarrer.
- Les chemins peuvent s'afficher indépendamment de `renderRoads` si `renderPaths` vaut `true`.
- `TerrainApiProbe` est réservé au diagnostic terrain et n'est pas appelé par défaut.
- Les fichiers `*.before-*` sont des snapshots de travail ; ne pas les considérer comme comportement runtime.
- Les profils de publication `Properties\PublishProfiles\*.pubxml` ciblent les commandes Paradox/CS2 `Publish`, `NewVersion` et `Update`.
