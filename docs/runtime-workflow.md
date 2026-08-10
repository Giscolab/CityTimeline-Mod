# Workflow runtime local

Cette page documente le workflow runtime local actuel de `CityTimelineMod` pour le développement et les tests sur la machine locale.

## Configuration lue au runtime

Le mod lit et écrit la configuration utilisateur persistante suivante :

```text
%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\ModsSettings\CityTimelineMod\config.json
```

Le fichier `resources/defaults/config.json` du dépôt sert de valeur par défaut versionnée. Le workflow local courant distingue donc :

| Élément | Rôle |
| --- | --- |
| `repo/resources/defaults/config.json` | Configuration par défaut suivie par Git. |
| `%USERPROFILE%/AppData/LocalLow/Colossal Order/Cities Skylines II/ModsSettings/CityTimelineMod/config.json` | Configuration utilisateur réellement lue et écrite au chargement du mod. |
| `scripts/deploy.ps1` | Déploie les artefacts du dépôt sans écraser la configuration utilisateur sous `ModsSettings`. |

## Interrupteurs runtime

| Clé | Effet |
| --- | --- |
| `modEnabled` | Active ou désactive le mod complet au prochain chargement du mod. Si la valeur change pendant que CS2 tourne, redémarrer le jeu ou recharger le mod pour appliquer proprement l'état global. |
| `largeMapEnabled` | Active le module expérimental LargeMap au prochain chargement. La valeur par défaut est `true`. |
| `playableWorldEnabled` | Active le module expérimental PlayableWorld au prochain chargement. La valeur par défaut est `false`; le module dépend de LargeMap. |
| `showOverlayHud` | Affiche ou masque seulement le HUD debug. Cette clé ne désactive pas le mod, les données, ni le rendu principal. |
| `verboseOverlayLogs` | Contrôle les logs détaillés de l'overlay. `false` garde les logs runtime plus courts ; `true` active davantage de détails utiles pour diagnostiquer le rendu et les reconstructions. |

## Composants runtime locaux

| Composant | Rôle actuel |
| --- | --- |
| `CityTimelineUISystem` | Système CoHTML actif. Il expose les bindings du HUD et respecte le gate runtime du snapshot de démarrage. |
| `GeoDebugOverlay` | Overlay de debug et de calibration séparé. Il gère le rendu de contrôle, le HUD de debug/calibration et les reconstructions live de l'overlay. |
| `CityTimelineRuntimeController` | Composant historique non réactivé. Le bootstrap supprime seulement les anciennes instances éventuellement laissées par un hot reload. |

Le bootstrap produit un snapshot strict unique avant d'ouvrir le gate runtime :

- `CityTimelineUISystem` publie les bindings CoHTML seulement lorsque le gate est ouvert ;
- `GeoDebugOverlay` pilote l'affichage, la calibration visuelle et les reconstructions live ;
- `CityTimelineRuntimeController` n'est pas réinstallé.

## Outils locaux

| Script | Usage |
| --- | --- |
| `scripts/deploy.ps1` | Déploiement local explicite. À utiliser seulement lorsqu'un déploiement dans le dossier live du jeu est voulu. |

Exemples :

```powershell
.\scripts\deploy.ps1
```

## Limites actuelles

Les Options officielles et les HUD écrivent le même `config.json` sous `ModsSettings`; le démarrage produit ensuite un snapshot strict unique. `modEnabled`, `largeMapEnabled` et `playableWorldEnabled` restent des décisions de cycle de vie appliquées au prochain chargement du mod.

Le workflow runtime local ne repose pas sur de la reflection DLL pour charger ou détourner le mod, et ne met en place aucun contournement de Smart App Control. Les actions prévues restent dans le cadre du déploiement local du mod, de la configuration runtime et des outils de diagnostic du dépôt.
