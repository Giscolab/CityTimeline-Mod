// CTM CONTRACT GUARD: Never disable the 57 km LargeMap + PlayableWorld contract unless explicitly requested.
using System;
using System.Globalization;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;
using CityTimelineMod.Util;
using CityTimelineMod.Bundles;

namespace CityTimelineMod.Config
{
    internal sealed partial class GeoOverlayConfig
    {
        internal bool UseGeoJsonCenter = true;
        internal bool ModEnabled = true;
        internal bool LargeMapEnabled = true;
        internal bool PlayableWorldEnabled = true;
        internal bool IsReliable = true;
        internal string ReliabilityError = null;
        internal double OriginLon = -117.795;
        internal double OriginLat = 33.7025;

        internal float WorldOriginX = 0f;
        internal float WorldOriginZ = 0f;
        internal float WorldScale = 1f;
        internal float GroundMargin = 6f;
		internal float OverlayRotationDegrees = 0f;
		internal float OverlayScaleX = 1f;
		internal float OverlayScaleZ = 1f;

		internal bool FlipX = false;
		internal bool FlipZ = false;
		internal bool EnableCalibrationControls = true;
		internal float CalibrationMoveStep = 100f;
		internal float CalibrationRotationStep = 1f;
		internal float CalibrationScaleStep = 0.01f;

		internal string ConfigPath = null;
		internal string BundleManifestPath = null;
         internal string BundlesRoot = "data/exports/bundles";
         internal string ActiveBundleId = "";
         internal string ResolvedBundlesRoot = null;
         internal string ActiveBundleRoot = null;
         internal bool BundleIndexResolutionSucceeded = false;
         internal string BundleIndexResolutionError = null;
         internal string ActiveBundleDisplayName = null;
         internal string ActiveBundleCity = null;
         internal string ActiveBundleCountry = null;
         internal double ActiveBundleCenterLon = double.NaN;
         internal double ActiveBundleCenterLat = double.NaN;
        internal double RecommendedCs2WaterLevel = double.NaN;
        internal double WaterReferenceElevationMeters = double.NaN;
        internal double BelowSeaReserveMeters = double.NaN;
        internal string WaterContractSource = null;
        internal string WaterContractFormula = null;

                internal string PackPath = null;
		internal bool UseSegmentCubes = true;

                internal bool RenderWaterLines = true;
                internal bool RenderWaterAreas = true;
                internal bool RenderRoads = true;
                                internal string RoadGeometrySource = "major";
                internal bool RenderPaths = true;
                internal string RoadHighwayFilter = "all";
                internal string PathHighwayFilter = "all";
                internal string OverlayProfile = "custom";
internal bool RenderZoning = true;
                internal bool ShowOverlayHud = false;
                internal bool VerboseOverlayLogs = false;
                internal bool RenderMapBounds = true;
                internal bool RenderWorldMapBounds = true;
                internal bool RenderHeightMapBounds = true;
                internal bool RenderMapCenter = true;
                internal bool MapBoundsUseCenterAndSize = true;
                internal float WorldMapSizeKm = 57.344f;
                internal float HeightMapSizeKm = 19.115f;

                internal double WorldMapMinLon = -118.077270;
                internal double WorldMapMinLat = 33.409332;
                internal double WorldMapMaxLon = -117.458331;
                internal double WorldMapMaxLat = 33.924460;

                internal double HeightMapMinLon = -117.870959;
                internal double HeightMapMinLat = 33.581040;
                internal double HeightMapMaxLon = -117.664642;
                internal double HeightMapMaxLat = 33.752752;

                internal float MapBoundsAlpha = 0.85f;
                internal float WorldMapBoundsAlpha = 0.85f;
                internal float HeightMapBoundsAlpha = 0.85f;
                internal float MapCenterAlpha = 0.85f;

                internal string WaterLineColor = "bleuClair";
                internal string WaterAreaOutlineColor = "bleuFonce";
                internal string WaterAreaFillColor = "bleu";

                internal string RoadColorDefault = "grisClair";
                internal string RoadColorMotorway = "rouge";
                internal string RoadColorPrimary = "orange";
                internal string RoadColorSecondary = "jaune";
                internal string RoadColorTertiary = "blanc";
                internal string RoadColorLink = "magenta";
                internal string PathColor = "cyan";

                internal string ZoningResidentialLowColor = "vertClair";
                internal string ZoningResidentialMediumColor = "vertMoyen";
                internal string ZoningResidentialHighColor = "vertFonce";
                internal string ZoningCommercialLowColor = "bleuCommercialClair";
                internal string ZoningCommercialHighColor = "bleuCommercial";
                internal string ZoningRetailColor = "bleuDetail";
                internal string ZoningIndustrialColor = "ambre";
                internal string ZoningOfficeColor = "violet";
                internal string ZoningSurfaceColor = "vertParkingClair";
                internal string ZoningRampColor = "vertParking";
                internal string ZoningMixedColor = "turquoise";
                internal string ZoningFallbackColor = "blanc";

                internal string WorldMapBoundsColor = "blancLimites";
                internal string HeightMapBoundsColor = "magentaHeightmap";
                internal string MapCenterColor = "rougeCentre";

                internal string DebugRedColor = "rouge";
                internal string DebugGreenColor = "vert";
                internal string DebugYellowColor = "jaune";
                internal string DebugMagentaColor = "magenta";
                internal float MapBoundsYOffset = 0.75f;
                internal int MaxZoningFillMeshesDebug = 5000;
                internal float ZoningFillYOffset = 0.05f;
                internal float RoadYOffset = 0.25f;
                internal float PathYOffset = 0.28f;
                internal float WaterLineYOffset = 0.35f;
                internal float WaterAreaOutlineYOffset = 0.30f;
                internal float ZoningAlpha = 0.28f;

                internal bool ZoningResidentialVisible = true;
                internal bool ZoningCommercialVisible = true;
                internal bool ZoningIndustrialVisible = true;
                internal bool ZoningOfficeVisible = true;
                internal bool ParkingVisible = true;

                internal float ZoningResidentialAlpha = 0.28f;
                internal float ZoningCommercialAlpha = 0.28f;
                internal float ZoningIndustrialAlpha = 0.28f;
                internal float ZoningOfficeAlpha = 0.28f;
                internal float ParkingAlpha = 0.28f;

                internal bool ServicesWaterVisible = true;
                internal bool ServicesElectricityVisible = true;
                internal bool ServicesEducationVisible = true;
                internal bool ServicesFireVisible = true;
                internal bool ServicesHealthVisible = true;
                internal bool ServicesParksVisible = true;
                internal bool ServicesWasteVisible = true;
                internal bool ServicesTransportVisible = true;
                internal bool ServicesCommunicationVisible = true;

                internal float ServicesWaterAlpha = 1f;
                internal float ServicesElectricityAlpha = 1f;
                internal float ServicesEducationAlpha = 1f;
                internal float ServicesFireAlpha = 1f;
                internal float ServicesHealthAlpha = 1f;
                internal float ServicesParksAlpha = 1f;
                internal float ServicesWasteAlpha = 1f;
                internal float ServicesTransportAlpha = 1f;
                internal float ServicesCommunicationAlpha = 1f;
                internal float WaterAreaFillAlpha = 0.35f;
                internal float RoadAlpha = 0.85f;
                internal float PathAlpha = 0.35f;
                internal float WaterLineAlpha = 0.95f;
                internal float WaterAreaOutlineAlpha = 0.75f;
                internal string ZoningDebugFilterZone = "";
                internal string ZoningDebugFilterCs2Contains = "";
                internal string ZoningDebugFilterMaterialKey = "";
		internal float SegmentWidth = 35f;
		internal float SegmentHeight = 10f;

                internal float WaterLineSegmentWidth = 2f;
                internal float WaterLineSegmentHeight = 2f;
                internal float WaterAreaSegmentWidth = 1f;
                internal float WaterAreaSegmentHeight = 2f;
                internal bool RenderWaterAreaOutlines = true;
                internal bool RenderWaterAreaFillMeshes = true;
                internal int MaxWaterAreaFillMeshesDebug = 256;
                internal float WaterAreaFillYOffset = 0.03f;
		// Paramètres séparés pour les routes debug
		internal float RoadSegmentWidth = 24f;
		internal float RoadSegmentHeight = 30f;
              internal float PathSegmentWidth = 1f;
              internal float PathSegmentHeight = 2f;
		internal int MaxRoadSegmentsDebug = 2000;
        internal int MaxPathSegmentsDebug = 2000;
            internal int MinimumRoadDebugTier = 0;

        internal bool RenderEverything = false;
        internal bool RenderAllRoadSegments = false;
        internal bool RenderAllPathSegments = false;
        internal bool RenderAllZoningPolygons = false;
        internal bool RenderAllWaterSegments = false;
        internal bool RenderAllWaterAreaFills = false;

        internal bool UseLaneWidthScaling = false;
        internal float LaneWidthScaleFactor = 0.18f;
        internal bool HighlightOneWayRoads = false;
        internal bool HighlightBridges = false;
        internal bool HighlightTunnels = false;
        internal bool HighlightRoundabouts = false;
        internal string RoadRenderMode = "ribbon";
        internal string PathRenderMode = "ribbon";
        internal float RibbonYOffset = 0.05f;
        internal bool EnableRoadSpatialChunking = true;
        internal float RoadChunkSizeMeters = 1024f;
        internal bool EnableProgressiveOverlayRebuild = true;
        internal int RoadChunksPerFrame = 16;
        internal int PathChunksPerFrame = 16;

        internal bool RenderRoadDirectionArrows = false;
        internal bool RenderAllRoadArrows = false;
        internal float RoadArrowSpacingMeters = 300f;
        internal float RoadArrowSize = 18f;
        internal float RoadArrowYOffset = 2f;
        internal int RoadArrowMaxCount = 5000;

        internal bool RenderRoadLabels = false;
        internal bool RenderAllRoadLabels = false;
        internal int RoadLabelMaxCount = 300;
        internal int RoadLabelMinTier = 2;
        internal float RoadLabelMaxDistance = 2500f;
        internal float RoadLabelYOffset = 8f;
        internal int RoadLabelFontSize = 32;
        internal bool DeduplicateRoadLabels = true;

        internal string RoadSemanticFilterMode = "all";

        internal bool RuntimeRoadImportEnabled = false;
        internal bool RuntimeRoadImportRunOnce = false;

        // Nombre maximum de segments réellement créés dans le réseau CS2.
        // Ce n’est pas l’overlay visuel. C’est l’import runtime.
        // 0 = illimité : importer tous les segments éligibles du bundle.
        internal int RuntimeRoadImportMaxSegments = 0;

        internal int RuntimeRoadImportStride = 1;
        internal float RuntimeRoadImportMinSegmentLengthMeters = 2f;
        internal float RuntimeRoadImportSnapToleranceMeters = 6f;
        internal float RuntimeRoadImportYOffset = 0.15f;
        internal bool RuntimeRoadImportIncludeBridgeTunnel = false;

        // Filtres runtime explicites. Ils pilotent la sélection avant mise en queue CS2.
        // Source : roads | paths | roads-and-paths.
        internal string RuntimeRoadImportSourceFilter = "roads";

        // Highway : all ou CSV, ex. primary,secondary,tertiary.
        internal string RuntimeRoadImportHighwayFilter = "all";

        // Stage : all ou CSV, ex. backbone,urban.
        internal string RuntimeRoadImportStageFilter = "all";

        // HUD/logs runtime import.
        internal bool RuntimeRoadImportShowProgressInHud = true;
        internal bool RuntimeRoadImportVerboseSelectionLogs = false;

        // Sélection des routes à importer depuis le bundle.
        // "staged-cell-priority" = familles de routes + cellules spatiales complètes.
        internal string RuntimeRoadImportSelectionMode = "staged-cell-priority";

        // Taille des anneaux autour du centre de carte.
        // Dans chaque anneau, les routes importantes passent avant les voies locales.
        internal float RuntimeRoadImportDistanceBucketMeters = 500f;

        // Pondération de l’importance OSM dans le tri.
        // 1.0 = normal. 0 = tri purement par distance.
        internal float RuntimeRoadImportPriorityWeight = 1f;

        // Sécurité : éviter d’importer les parkings internes comme réseau principal.
        internal bool RuntimeRoadImportSkipParkingAisles = true;

        // Sécurité : éviter gravel/track sauf demande explicite.
        internal bool RuntimeRoadImportSkipClearlyUnpaved = true;

        // Nombre de routes émises avant chaque finalisation CS2.
        // Plus petit = meilleur snapping progressif, mais plus lent.
        // 64 = qualité, 128/256 = compromis, 512+ = rapide.
        internal int RuntimeRoadImportBatchSize = 256;
        internal string RuntimeRoadImportPipelineMode = "confirmed-fast-flush";
        // confirmed-fast-flush = chunks rapides bornés, appliqués et confirmés avant le chunk suivant.
// batch-safe = petits commits stricts, plus lents mais utiles pour isoler les erreurs.

        internal bool UseLineRenderer = false;
        internal float LineWidth = 60f;

        internal bool DebugBeacons = true;
        internal float BeaconHeight = 1000f;
        internal float BeaconWidth = 80f;

        internal bool DebugTower = true;
        internal float TowerHeight = 3000f;
        internal float TowerWidth = 180f;

        // --- Nouveaux champs ---
        internal int MaxRenderedSegments = 500;
        internal int MaxWaterSegmentsDebug = 500;
        internal int PointStride = 1;

    }
}

