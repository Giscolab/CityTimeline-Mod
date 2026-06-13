# Workflow runtime local

Cette page documente le workflow runtime local actuel de `CityTimelineMod` pour le développement et les tests sur la machine locale.

## Configuration lue au runtime

Le jeu lit la configuration runtime déployée dans le dossier local du mod :

```text
%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\Mods\CityTimelineMod\config.json
```

Le fichier `resources/defaults/config.json` du dépôt sert de valeur par défaut versionnée. Le workflow local courant distingue donc :

| Élément | Rôle |
| --- | --- |
| `repo/resources/defaults/config.json` | Configuration par défaut suivie par Git. |
| `runtime/config.json` | Configuration réellement lue par CS2 au chargement du mod. |
| `scripts/deploy-local.ps1` | Déploie le mod localement et préserve la configuration runtime existante. |

## Interrupteurs runtime

| Clé | Effet |
| --- | --- |
| `modEnabled` | Active ou désactive le mod complet au prochain chargement du mod. Si la valeur change pendant que CS2 tourne, redémarrer le jeu ou recharger le mod pour appliquer proprement l'état global. |
| `showOverlayHud` | Affiche ou masque seulement le HUD debug. Cette clé ne désactive pas le mod, les données, ni le rendu principal. |
| `verboseOverlayLogs` | Contrôle les logs détaillés de l'overlay. `false` garde les logs runtime plus courts ; `true` active davantage de détails utiles pour diagnostiquer le rendu et les reconstructions. |

## Composants runtime locaux

| Composant | Rôle actuel |
| --- | --- |
| `CityTimelineRuntimeController` | Contrôleur global local de développement. Il lit l'état runtime, expose les bascules locales de développement et centralise les actions globales comme l'activation ou la désactivation via `modEnabled`. |
| `GeoDebugOverlay` | Overlay de debug et de calibration séparé. Il gère le rendu de contrôle, le HUD de debug/calibration et les reconstructions live de l'overlay. |

`CityTimelineRuntimeController` et `GeoDebugOverlay` ne représentent pas la même responsabilité :

- `CityTimelineRuntimeController` pilote l'état runtime global local du mod ;
- `GeoDebugOverlay` pilote l'affichage de debug, la calibration visuelle et les contrôles live de l'overlay.

## Outils locaux

| Script | Usage |
| --- | --- |
| `scripts/runtime/audit-runtime.ps1` | Audit runtime local. Par défaut, il inspecte l'état Git, les fichiers runtime, `mod.json`, les configurations repo/runtime, le bundle GeoJSON et les logs. Avec `-Build`, il lance aussi une compilation pendant l'audit. |
| `scripts/runtime/set-runtime-enabled.ps1` | Toggle CLI de `modEnabled` dans le `config.json` runtime déployé. Le changement s'applique proprement au prochain chargement du mod. |

Exemples :

```powershell
.\scripts\runtime\audit-runtime.ps1
.\scripts\runtime\audit-runtime.ps1 -Build
.\scripts\runtime\set-runtime-enabled.ps1 -Enabled $false
.\scripts\runtime\set-runtime-enabled.ps1 -Enabled $true
```

## Limites actuelles

L'intégration aux Options officielles CS2 existe, mais elle n'est pas encore l'unique source d'état. Les Options, le fichier `config.json`, les scripts locaux et les HUD de développement peuvent encore agir sur des chemins concurrents.

Le workflow runtime local ne repose pas sur de la reflection DLL pour charger ou détourner le mod, et ne met en place aucun contournement de Smart App Control. Les actions prévues restent dans le cadre du déploiement local du mod, de la configuration runtime et des outils de diagnostic du dépôt.
