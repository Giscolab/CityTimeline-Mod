using System.Collections.Generic;
using Colossal;
using Colossal.Localization;
using Game.Settings;

namespace CityTimelineMod.Options
{
    public sealed class CityTimelineSettingsLocale : IDictionarySource
    {
        private readonly CityTimelineSettings _settings;

        public CityTimelineSettingsLocale(CityTimelineSettings settings)
        {
            _settings = settings;
        }

        public IEnumerable<KeyValuePair<string, string>> ReadEntries(
            IList<IDictionaryEntryError> errors,
            Dictionary<string, int> indexCounts)
        {
            const string couleurs = "Noms : rouge, vertClair, vertMoyen, vertFonce, jaune, bleuClair, bleu, bleuFonce, violet, orange, grisClair, gris, grisFonce, blanc, cyan, magenta.";

            return new Dictionary<string, string>
            {
                { _settings.GetSettingsLocaleID(), "CityTimelineMod" },

                { _settings.GetOptionTabLocaleID(CityTimelineSettings.SectionGeneral), "Général" },
                { _settings.GetOptionTabLocaleID(CityTimelineSettings.SectionNetwork), "Réseaux" },
                { _settings.GetOptionTabLocaleID(CityTimelineSettings.SectionWater), "Eau" },
                { _settings.GetOptionTabLocaleID(CityTimelineSettings.SectionZoning), "Zonage" },

                                { _settings.GetOptionGroupLocaleID(CityTimelineSettings.GroupMain), "Activation" },
                { _settings.GetOptionGroupLocaleID(CityTimelineSettings.GroupWorldMapBounds), "Limite worldmap" },
                { _settings.GetOptionGroupLocaleID(CityTimelineSettings.GroupHeightMapBounds), "Limite heightmap" },
                { _settings.GetOptionGroupLocaleID(CityTimelineSettings.GroupMapCenter), "Centre carte" },
                { _settings.GetOptionGroupLocaleID(CityTimelineSettings.GroupBounds), "Limites worldmap / heightmap" },
{ _settings.GetOptionGroupLocaleID(CityTimelineSettings.GroupRoutes), "Routes" },
                { _settings.GetOptionGroupLocaleID(CityTimelineSettings.GroupPaths), "Chemins" },
                { _settings.GetOptionGroupLocaleID(CityTimelineSettings.GroupWater), "Eau" },
                { _settings.GetOptionGroupLocaleID(CityTimelineSettings.GroupZoning), "Zonage" },

                { _settings.GetOptionLabelLocaleID(nameof(CityTimelineSettings.ModEnabled)), "Activer CityTimelineMod" },
                { _settings.GetOptionDescLocaleID(nameof(CityTimelineSettings.ModEnabled)), "Active ou désactive le comportement principal du mod." },

                { _settings.GetOptionLabelLocaleID(nameof(CityTimelineSettings.KeyBindingToggleCohtmlHud)), "Ouvrir / fermer le HUD CoHTML" },
                { _settings.GetOptionDescLocaleID(nameof(CityTimelineSettings.KeyBindingToggleCohtmlHud)), "Raccourci du panneau React/CoHTML. Valeur par défaut : Alt+Z. Le HUD IMGUI Alt+H reste indépendant." },
                { _settings.GetBindingKeyLocaleID(nameof(CityTimelineSettings.KeyBindingToggleCohtmlHud)), "Ouvrir / fermer le HUD CoHTML" },
                { _settings.GetBindingKeyHintLocaleID(nameof(CityTimelineSettings.KeyBindingToggleCohtmlHud)), "Ouvrir / fermer le HUD CoHTML" },

                { _settings.GetOptionLabelLocaleID(nameof(CityTimelineSettings.ShowOverlayHud)), "Afficher le HUD debug" },
                { _settings.GetOptionDescLocaleID(nameof(CityTimelineSettings.ShowOverlayHud)), "Affiche ou masque le HUD IMGUI. Raccourci : Alt+H. Le mini CTM Alt+T reste géré par le runtime." },

                { _settings.GetOptionLabelLocaleID(nameof(CityTimelineSettings.RenderMapBounds)), "Afficher les limites worldmap / heightmap" },
                { _settings.GetOptionDescLocaleID(nameof(CityTimelineSettings.RenderMapBounds)), "Affiche les repères de limites : worldmap en blanc, heightmap en magenta, centre carte en rouge." },

                { _settings.GetOptionLabelLocaleID(nameof(CityTimelineSettings.MapBoundsAlpha)), "Transparence limites" },
                { _settings.GetOptionDescLocaleID(nameof(CityTimelineSettings.MapBoundsAlpha)), "Transparence des limites worldmap / heightmap, de 0.00 à 1.00." },

                { _settings.GetOptionLabelLocaleID(nameof(CityTimelineSettings.VerboseOverlayLogs)), "Logs overlay détaillés" },
                { _settings.GetOptionDescLocaleID(nameof(CityTimelineSettings.VerboseOverlayLogs)), "Active les logs détaillés de l'overlay. Option développeur, à laisser désactivée en usage normal." },

                                { _settings.GetOptionLabelLocaleID(nameof(CityTimelineSettings.RenderWorldMapBounds)), "Afficher la limite worldmap" },
                { _settings.GetOptionDescLocaleID(nameof(CityTimelineSettings.RenderWorldMapBounds)), "Affiche uniquement la limite worldmap. Couleur fixe : blanc." },

                { _settings.GetOptionLabelLocaleID(nameof(CityTimelineSettings.WorldMapBoundsAlpha)), "Transparence limite worldmap" },
                { _settings.GetOptionDescLocaleID(nameof(CityTimelineSettings.WorldMapBoundsAlpha)), "Transparence de la limite worldmap, de 0.00 à 1.00." },

                { _settings.GetOptionLabelLocaleID(nameof(CityTimelineSettings.RenderHeightMapBounds)), "Afficher la limite heightmap" },
                { _settings.GetOptionDescLocaleID(nameof(CityTimelineSettings.RenderHeightMapBounds)), "Affiche uniquement la limite heightmap. Couleur fixe : magenta." },

                { _settings.GetOptionLabelLocaleID(nameof(CityTimelineSettings.HeightMapBoundsAlpha)), "Transparence limite heightmap" },
                { _settings.GetOptionDescLocaleID(nameof(CityTimelineSettings.HeightMapBoundsAlpha)), "Transparence de la limite heightmap, de 0.00 à 1.00." },

                { _settings.GetOptionLabelLocaleID(nameof(CityTimelineSettings.RenderMapCenter)), "Afficher le centre carte" },
                { _settings.GetOptionDescLocaleID(nameof(CityTimelineSettings.RenderMapCenter)), "Affiche le marqueur du centre de carte. Couleur fixe : rouge." },

                { _settings.GetOptionLabelLocaleID(nameof(CityTimelineSettings.MapCenterAlpha)), "Transparence centre carte" },
                { _settings.GetOptionDescLocaleID(nameof(CityTimelineSettings.MapCenterAlpha)), "Transparence du centre carte, de 0.00 à 1.00." },

                { _settings.GetOptionLabelLocaleID(nameof(CityTimelineSettings.RenderRoads)), "Afficher les routes" },
                { _settings.GetOptionDescLocaleID(nameof(CityTimelineSettings.RenderRoads)), "Active le rendu des routes." },

                { _settings.GetOptionLabelLocaleID(nameof(CityTimelineSettings.RoadAlpha)), "Transparence routes" },
                { _settings.GetOptionDescLocaleID(nameof(CityTimelineSettings.RoadAlpha)), "Transparence globale des routes, de 0.00 à 1.00." },

                { _settings.GetOptionLabelLocaleID(nameof(CityTimelineSettings.RoadGeometrySource)), "Source géométrie routes" },
                { _settings.GetOptionDescLocaleID(nameof(CityTimelineSettings.RoadGeometrySource)), "driveable : réseau routier praticable. major : axes principaux simplifiés." },

                { _settings.GetOptionLabelLocaleID(nameof(CityTimelineSettings.MinimumRoadDebugTier)), "Niveau minimum routes" },
                { _settings.GetOptionDescLocaleID(nameof(CityTimelineSettings.MinimumRoadDebugTier)), "0 affiche tout. Plus la valeur est élevée, plus les petites routes sont filtrées." },

                { _settings.GetOptionLabelLocaleID(nameof(CityTimelineSettings.RuntimeRoadImportEnabled)), "Autoriser import runtime" },
                { _settings.GetOptionDescLocaleID(nameof(CityTimelineSettings.RuntimeRoadImportEnabled)), "Garde-fou global. Active seulement le bouton manuel, ne déclenche jamais d'import automatique." },

                { _settings.GetOptionLabelLocaleID(nameof(CityTimelineSettings.RuntimeRoadImportRunOnce)), "Refuser doublon import" },
                { _settings.GetOptionDescLocaleID(nameof(CityTimelineSettings.RuntimeRoadImportRunOnce)), "Empêche de réimporter la même source/config sans undo préalable." },

                { _settings.GetOptionLabelLocaleID(nameof(CityTimelineSettings.RuntimeRoadImportMaxSegments)), "Limite import runtime" },
                { _settings.GetOptionDescLocaleID(nameof(CityTimelineSettings.RuntimeRoadImportMaxSegments)), "Nombre maximum de segments routiers CS2 créés par clic manuel. 0 = illimité / tout importer." },

                { _settings.GetOptionLabelLocaleID(nameof(CityTimelineSettings.RuntimeRoadImportBatchSize)), "Taille lots import runtime" },
                { _settings.GetOptionDescLocaleID(nameof(CityTimelineSettings.RuntimeRoadImportBatchSize)), "Nombre de routes créées avant chaque finalisation CS2. 64 = meilleur snapping mais lent, 128/256 = compromis, 512+ = plus rapide." },

                { _settings.GetOptionLabelLocaleID(nameof(CityTimelineSettings.RuntimeRoadImportSelectionMode)), "Mode sélection import routes" },
                { _settings.GetOptionDescLocaleID(nameof(CityTimelineSettings.RuntimeRoadImportSelectionMode)), "staged-cell-priority : importe par familles de routes puis cellule spatiale complète. whole-map-priority : couvre toute la carte avec priorité OSM. near-center-priority : centre avec priorité. file-order : ordre brut du fichier." },

                { _settings.GetOptionLabelLocaleID(nameof(CityTimelineSettings.RuntimeRoadImportDistanceBucketMeters)), "Anneaux distance import" },
                { _settings.GetOptionDescLocaleID(nameof(CityTimelineSettings.RuntimeRoadImportDistanceBucketMeters)), "Taille des anneaux autour du centre de carte. Dans chaque anneau, les routes importantes passent avant les voies locales." },

                { _settings.GetOptionLabelLocaleID(nameof(CityTimelineSettings.RuntimeRoadImportPriorityWeight)), "Poids priorité OSM import" },
                { _settings.GetOptionDescLocaleID(nameof(CityTimelineSettings.RuntimeRoadImportPriorityWeight)), "0 ignore la priorité OSM. 1 utilise le tri normal. Plus haut renforce motorway, primary, secondary, tertiary avant service/local." },

                { _settings.GetOptionLabelLocaleID(nameof(CityTimelineSettings.RuntimeRoadImportSkipParkingAisles)), "Ignorer voies parking" },
                { _settings.GetOptionDescLocaleID(nameof(CityTimelineSettings.RuntimeRoadImportSkipParkingAisles)), "Évite d'importer les voies internes de parking comme réseau principal CS2." },

                { _settings.GetOptionLabelLocaleID(nameof(CityTimelineSettings.RuntimeRoadImportSkipClearlyUnpaved)), "Ignorer routes non revêtues" },
                { _settings.GetOptionDescLocaleID(nameof(CityTimelineSettings.RuntimeRoadImportSkipClearlyUnpaved)), "Évite d'importer les routes clairement non revêtues ou les tracks comme réseau principal CS2." },

                { _settings.GetOptionLabelLocaleID(nameof(CityTimelineSettings.ImportRuntimeRoadsNow)), "Importer routes runtime" },
                { _settings.GetOptionDescLocaleID(nameof(CityTimelineSettings.ImportRuntimeRoadsNow)), "Commande manuelle V1: importe les routes GeoJSON en cache si runtimeRoadImportEnabled=true." },

                { _settings.GetOptionLabelLocaleID(nameof(CityTimelineSettings.UndoRuntimeRoadImport)), "Undo import routes runtime désactivé" },
                { _settings.GetOptionDescLocaleID(nameof(CityTimelineSettings.UndoRuntimeRoadImport)), "Désactivé en V1: la suppression directe des entités réseau CS2 est non sûre." },
{ _settings.GetOptionLabelLocaleID(nameof(CityTimelineSettings.RoadColorDefault)), "Couleur routes non classées" },
                { _settings.GetOptionDescLocaleID(nameof(CityTimelineSettings.RoadColorDefault)), couleurs },

                { _settings.GetOptionLabelLocaleID(nameof(CityTimelineSettings.RoadColorMotorway)), "Couleur autoroutes" },
                { _settings.GetOptionDescLocaleID(nameof(CityTimelineSettings.RoadColorMotorway)), couleurs },

                { _settings.GetOptionLabelLocaleID(nameof(CityTimelineSettings.RoadColorPrimary)), "Couleur axes principaux" },
                { _settings.GetOptionDescLocaleID(nameof(CityTimelineSettings.RoadColorPrimary)), couleurs },

                { _settings.GetOptionLabelLocaleID(nameof(CityTimelineSettings.RoadColorSecondary)), "Couleur routes secondaires" },
                { _settings.GetOptionDescLocaleID(nameof(CityTimelineSettings.RoadColorSecondary)), couleurs },

                { _settings.GetOptionLabelLocaleID(nameof(CityTimelineSettings.RoadColorTertiary)), "Couleur routes tertiaires" },
                { _settings.GetOptionDescLocaleID(nameof(CityTimelineSettings.RoadColorTertiary)), couleurs },

                { _settings.GetOptionLabelLocaleID(nameof(CityTimelineSettings.RoadColorLink)), "Couleur bretelles / liaisons" },
                { _settings.GetOptionDescLocaleID(nameof(CityTimelineSettings.RoadColorLink)), couleurs },

                { _settings.GetOptionLabelLocaleID(nameof(CityTimelineSettings.RenderPaths)), "Afficher les chemins" },
                { _settings.GetOptionDescLocaleID(nameof(CityTimelineSettings.RenderPaths)), "Active le rendu des chemins et voies piétonnes." },

                { _settings.GetOptionLabelLocaleID(nameof(CityTimelineSettings.PathAlpha)), "Transparence chemins" },
                { _settings.GetOptionDescLocaleID(nameof(CityTimelineSettings.PathAlpha)), "Transparence globale des chemins, de 0.00 à 1.00." },

                { _settings.GetOptionLabelLocaleID(nameof(CityTimelineSettings.PathColor)), "Couleur chemins" },
                { _settings.GetOptionDescLocaleID(nameof(CityTimelineSettings.PathColor)), couleurs },

                { _settings.GetOptionLabelLocaleID(nameof(CityTimelineSettings.RenderWaterLines)), "Afficher les lignes d'eau" },
                { _settings.GetOptionDescLocaleID(nameof(CityTimelineSettings.RenderWaterLines)), "Active le rendu des rivières, cours d'eau et lignes d'eau." },

                { _settings.GetOptionLabelLocaleID(nameof(CityTimelineSettings.RenderWaterAreas)), "Afficher les surfaces d'eau" },
                { _settings.GetOptionDescLocaleID(nameof(CityTimelineSettings.RenderWaterAreas)), "Active le rendu des polygones d'eau." },

                { _settings.GetOptionLabelLocaleID(nameof(CityTimelineSettings.RenderWaterAreaOutlines)), "Afficher les contours des surfaces d'eau" },
                { _settings.GetOptionDescLocaleID(nameof(CityTimelineSettings.RenderWaterAreaOutlines)), "Active le rendu des contours des surfaces d'eau." },

                { _settings.GetOptionLabelLocaleID(nameof(CityTimelineSettings.RenderWaterAreaFillMeshes)), "Afficher le remplissage des surfaces d'eau" },
                { _settings.GetOptionDescLocaleID(nameof(CityTimelineSettings.RenderWaterAreaFillMeshes)), "Active le rendu du remplissage des surfaces d'eau." },

                { _settings.GetOptionLabelLocaleID(nameof(CityTimelineSettings.WaterLineAlpha)), "Transparence lignes d'eau" },
                { _settings.GetOptionDescLocaleID(nameof(CityTimelineSettings.WaterLineAlpha)), "Transparence des lignes d'eau, de 0.00 à 1.00." },

                { _settings.GetOptionLabelLocaleID(nameof(CityTimelineSettings.WaterAreaOutlineAlpha)), "Transparence contours d'eau" },
                { _settings.GetOptionDescLocaleID(nameof(CityTimelineSettings.WaterAreaOutlineAlpha)), "Transparence des contours d'eau, de 0.00 à 1.00." },

                { _settings.GetOptionLabelLocaleID(nameof(CityTimelineSettings.WaterAreaFillAlpha)), "Transparence remplissage eau" },
                { _settings.GetOptionDescLocaleID(nameof(CityTimelineSettings.WaterAreaFillAlpha)), "Transparence du remplissage des surfaces d'eau, de 0.00 à 1.00." },

                { _settings.GetOptionLabelLocaleID(nameof(CityTimelineSettings.WaterLineColor)), "Couleur lignes d'eau" },
                { _settings.GetOptionDescLocaleID(nameof(CityTimelineSettings.WaterLineColor)), couleurs },

                { _settings.GetOptionLabelLocaleID(nameof(CityTimelineSettings.WaterAreaOutlineColor)), "Couleur contours d'eau" },
                { _settings.GetOptionDescLocaleID(nameof(CityTimelineSettings.WaterAreaOutlineColor)), couleurs },

                { _settings.GetOptionLabelLocaleID(nameof(CityTimelineSettings.WaterAreaFillColor)), "Couleur remplissage eau" },
                { _settings.GetOptionDescLocaleID(nameof(CityTimelineSettings.WaterAreaFillColor)), couleurs },

                { _settings.GetOptionLabelLocaleID(nameof(CityTimelineSettings.RenderZoning)), "Afficher le zonage" },
                { _settings.GetOptionDescLocaleID(nameof(CityTimelineSettings.RenderZoning)), "Active le rendu du zonage." },

                { _settings.GetOptionLabelLocaleID(nameof(CityTimelineSettings.ZoningAlpha)), "Transparence zonage" },
                { _settings.GetOptionDescLocaleID(nameof(CityTimelineSettings.ZoningAlpha)), "Transparence globale du zonage, de 0.00 à 1.00." },

                { _settings.GetOptionLabelLocaleID(nameof(CityTimelineSettings.ZoningResidentialLowColor)), "Couleur résidentiel faible densité" },
                { _settings.GetOptionDescLocaleID(nameof(CityTimelineSettings.ZoningResidentialLowColor)), couleurs },

                { _settings.GetOptionLabelLocaleID(nameof(CityTimelineSettings.ZoningResidentialMediumColor)), "Couleur résidentiel moyenne densité" },
                { _settings.GetOptionDescLocaleID(nameof(CityTimelineSettings.ZoningResidentialMediumColor)), couleurs },

                { _settings.GetOptionLabelLocaleID(nameof(CityTimelineSettings.ZoningResidentialHighColor)), "Couleur résidentiel haute densité" },
                { _settings.GetOptionDescLocaleID(nameof(CityTimelineSettings.ZoningResidentialHighColor)), couleurs },

                { _settings.GetOptionLabelLocaleID(nameof(CityTimelineSettings.ZoningCommercialLowColor)), "Couleur commercial basse densité" },
                { _settings.GetOptionDescLocaleID(nameof(CityTimelineSettings.ZoningCommercialLowColor)), couleurs },

                { _settings.GetOptionLabelLocaleID(nameof(CityTimelineSettings.ZoningCommercialHighColor)), "Couleur commercial haute densité" },
                { _settings.GetOptionDescLocaleID(nameof(CityTimelineSettings.ZoningCommercialHighColor)), couleurs },

                { _settings.GetOptionLabelLocaleID(nameof(CityTimelineSettings.ZoningRetailColor)), "Couleur commerce de détail" },
                { _settings.GetOptionDescLocaleID(nameof(CityTimelineSettings.ZoningRetailColor)), couleurs },

                { _settings.GetOptionLabelLocaleID(nameof(CityTimelineSettings.ZoningIndustrialColor)), "Couleur industriel" },
                { _settings.GetOptionDescLocaleID(nameof(CityTimelineSettings.ZoningIndustrialColor)), couleurs },

                { _settings.GetOptionLabelLocaleID(nameof(CityTimelineSettings.ZoningOfficeColor)), "Couleur bureaux" },
                { _settings.GetOptionDescLocaleID(nameof(CityTimelineSettings.ZoningOfficeColor)), couleurs },

                { _settings.GetOptionLabelLocaleID(nameof(CityTimelineSettings.ZoningSurfaceColor)), "Couleur surfaces" },
                { _settings.GetOptionDescLocaleID(nameof(CityTimelineSettings.ZoningSurfaceColor)), couleurs },

                { _settings.GetOptionLabelLocaleID(nameof(CityTimelineSettings.ZoningRampColor)), "Couleur rampes" },
                { _settings.GetOptionDescLocaleID(nameof(CityTimelineSettings.ZoningRampColor)), couleurs },

                { _settings.GetOptionLabelLocaleID(nameof(CityTimelineSettings.ZoningMixedColor)), "Couleur usage mixte" },
                { _settings.GetOptionDescLocaleID(nameof(CityTimelineSettings.ZoningMixedColor)), couleurs },

                { _settings.GetOptionLabelLocaleID(nameof(CityTimelineSettings.ZoningFallbackColor)), "Couleur zonage inconnu" },
                { _settings.GetOptionDescLocaleID(nameof(CityTimelineSettings.ZoningFallbackColor)), couleurs }
            };
        }

        public void Unload()
        {
        }
    }
}



