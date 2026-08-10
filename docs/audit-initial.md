# Audit initial de structure

## Constat brut

L'archive reçue mélangeait cinq catégories incompatibles à la racine : code source, historique Git, dépendances frontend, sorties de compilation et copie locale AppData.

Principaux volumes observés avant nettoyage :

- `.git` : environ 242 Mo ;
- `ui-src` : environ 153 Mo, presque entièrement `node_modules` ;
- `bin` + `obj` : environ 38 Mo ;
- `dll appdata CityTimelineMod` : environ 567 Mo ;
- backend C# utile : moins de 2 Mo ;
- ressources GeoJSON de secours : environ 8,4 Mo.

## Problèmes architecturaux visibles

### Interfaces concurrentes

Trois surfaces UI coexistent :

1. `CityTimelineRuntimeController` : mini HUD IMGUI global, raccourci Alt+T ;
2. `GroundOverlayBehaviour.Hud` : HUD IMGUI principal, raccourci Alt+H ;
3. `CityTimelineMod.UI` : panneau React/CoHTML.

Le panneau React utilise actuellement surtout des états locaux et des valeurs de démonstration. Il n'est pas encore l'autorité fonctionnelle unique du mod.

### Configuration trop centrale

`GeoOverlayConfig` est réparti sur plusieurs classes partielles, avec un fichier de persistance d'environ 70 Ko. Les options officielles, le JSON runtime et les contrôles IMGUI manipulent des clés communes par des chemins différents.

### Import de routes trop concentré

Le sous-système routes mélange catalogue de prefabs, sélection, topologie, placement, diagnostics ECS, orchestration et undo. Plusieurs fichiers dépassent largement 20 à 50 Ko.

### Métadonnées divergentes

Le manifeste runtime et le module UI annonçaient encore la version `0.1.0`, tandis que la configuration de publication annonçait `0.1.2`. La passe de structure les aligne sur `0.1.2`.

## Décision pour cette passe

Aucun namespace, nom de classe ou algorithme n'a été modifié. Cette passe ne prétend donc pas réparer le comportement du mod. Elle crée une base propre et reproductible pour supprimer ensuite les couches concurrentes sans perdre la possibilité de comparer avec l'état précédent.

## Ordre recommandé pour la suite

1. Choisir une seule interface principale et désactiver les deux autres chemins d'écriture.
2. Créer un état applicatif unique entre UI, configuration et import.
3. Séparer overlay visuel et import réel de routes.
4. Réduire `GeoOverlayConfig` à des sections typées.
5. Découper le sous-système routes en services testables.
