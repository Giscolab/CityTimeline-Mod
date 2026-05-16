# Workflow runtime local

Cette page documente le workflow runtime local actuel de `CityTimelineMod` pour le développement et les tests sur la machine locale.

## Configuration lue au runtime

Le jeu lit la configuration runtime déployée dans le dossier local du mod :

```text
%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\Mods\CityTimelineMod\config.json
```

Le fichier `config.json` à la racine du dépôt sert de valeur par défaut versionnée. Le workflow local courant distingue donc :

| Élément | Rôle |
| --- | --- |
| `repo/config.json` | Configuration par défaut suivie par Git. |
| `runtime/config.json` | Configuration réellement lue par CS2 au chargement du mod. |
| `deploy.ps1` | Déploie le mod localement et préserve la configuration runtime existante. |

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
| `tools/audit_runtime.ps1` | Audit runtime local. Par défaut, il inspecte l'état Git, les fichiers runtime, `mod.json`, les configurations repo/runtime, le bundle GeoJSON et les logs. Avec `-Build`, il lance aussi une compilation pendant l'audit. |
| `tools/set_runtime_enabled.ps1` | Toggle CLI de `modEnabled` dans le `config.json` runtime déployé. Le changement s'applique proprement au prochain chargement du mod. |

Exemples :

```powershell
.\tools\audit_runtime.ps1
.\tools\audit_runtime.ps1 -Build
.\tools\set_runtime_enabled.ps1 -Enabled $false
.\tools\set_runtime_enabled.ps1 -Enabled $true
```

## Limites actuelles

L'intégration aux Options officielles CS2 / Paradox Mods n'est pas encore codée. Le workflow actuel passe par le fichier `config.json` runtime, les scripts locaux et les contrôles de développement exposés en jeu.

Le workflow runtime local ne repose pas sur de la reflection DLL pour charger ou détourner le mod, et ne met en place aucun contournement de Smart App Control. Les actions prévues restent dans le cadre du déploiement local du mod, de la configuration runtime et des outils de diagnostic du dépôt.
