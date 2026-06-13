# Architecture du dépôt

## Règle principale

Le dépôt sépare strictement le code source, les ressources versionnées, le packaging, les scripts et les artefacts générés. Aucun dossier de déploiement AppData, aucune dépendance `node_modules`, aucun `bin/obj` et aucun historique `.git` ne doit être copié dans une archive de travail.

## Modules

- `src/CityTimelineMod`: backend C# et point d’entrée CS2.
- `src/CityTimelineMod.UI`: module CoHTML React/TypeScript.
- `resources/defaults`: configuration initiale uniquement.
- `resources/legacy-geojson`: données de secours embarquées.
- `packaging`: manifeste runtime unique.
- `scripts`: commandes reproductibles de build, déploiement et audit.
- `artifacts`: UI compilée et autres sorties temporaires, jamais versionnées.

## Limite de cette passe

Cette réorganisation ne change ni les namespaces C#, ni les classes, ni les algorithmes. Elle prépare le prochain refactor fonctionnel, notamment la suppression des HUD concurrents, la réduction de `GeoOverlayConfig`, et la séparation du rendu overlay de l’import de routes.
