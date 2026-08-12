# CityTimelineMod

Mod de visualisation par injection de coordonnéees geolocalisées pour Cities: Skylines II.

ce mod charge un bundle GeoJSON déjà généré par le repository realmap_cs 2 (https://github.com/Giscolab/cs2-realmap-generator), il affiche ses couches sur la carte du jeu, aide à régler le calage terrain, active par défaut le module expérimental LargeMap 57,344 km.

Le mod ne génère pas les données et ne télécharge rien. Le bundle GeoJSON doit être préparé avant de lancer le jeu.

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

## Utilisation

- `Alt + H` : ouvrir ou fermer le HUD IMGUI de rendu et de calage.
- `Alt + Z` par défaut, ou le bouton `CTM` : ouvrir ou fermer le HUD React/CoHTML. (en cours de construction).
- `Appliquer / reconstruire` : reconstruire l’overlay après un changement.
- `Sauvegarder visuels` : écrire les réglages dans le `config.json` runtime.
- `Recharger visuels` : relire le `config.json` runtime.

un systeme de spawn d'import runtime des routes est désactivé car considéré comme un module expérimental
LargeMap est activé par défaut.

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
[
## Logs

```powershell
$log = "$env:USERPROFILE\AppData\LocalLow\Colossal Order\Cities Skylines II\Player.log"
Get-Content $log -Wait -Tail 700 |
  Select-String "CityTimelineMod|LargeMap|PlayableWorld|Exception|Error"
```

Documentation plus détaillée : `docs/`.
](https://github.com/Giscolab/cs2-realmap-generator)
