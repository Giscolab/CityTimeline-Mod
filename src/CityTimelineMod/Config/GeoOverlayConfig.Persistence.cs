using System;
using System.IO;
using CityTimelineMod.Bundles;
using CityTimelineMod.Util;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CityTimelineMod.Config
{
    internal sealed partial class GeoOverlayConfig
    {
        internal static GeoOverlayConfig Load(string modDir)
        {
            var config = new GeoOverlayConfig();
            var path = Path.Combine(modDir, "config.json");
            config.ConfigPath = path;

            if (!File.Exists(path))
            {
                Log.Info("GeoOverlayConfig: config.json not found. Using defaults.");
                return config;
            }

            try
            {
                var json = File.ReadAllText(path);
                var root = JObject.Parse(json);

                config.UseGeoJsonCenter = GetBool(root, "useGeoJsonCenter", config.UseGeoJsonCenter);
                config.ModEnabled = GetBool(root, "modEnabled", config.ModEnabled);
                config.OriginLon = GetDouble(root, "originLon", config.OriginLon);
                config.OriginLat = GetDouble(root, "originLat", config.OriginLat);
                config.PackPath = GetString(root, "packPath", config.PackPath);
                config.BundleManifestPath = GetString(root, "bundleManifestPath", config.BundleManifestPath);
                config.UseBundleIndex = GetBool(root, "useBundleIndex", config.UseBundleIndex);
                config.BundlesRoot = GetString(root, "bundlesRoot", config.BundlesRoot);
                config.ActiveBundleId = GetString(root, "activeBundleId", config.ActiveBundleId);

                config.WorldOriginX = GetFloat(root, "worldOriginX", config.WorldOriginX);
                config.WorldOriginZ = GetFloat(root, "worldOriginZ", config.WorldOriginZ);
                config.WorldScale = GetFloat(root, "worldScale", config.WorldScale);
                config.GroundMargin = GetFloat(root, "groundMargin", config.GroundMargin);
config.OverlayRotationDegrees = GetFloat(root, "overlayRotationDegrees", config.OverlayRotationDegrees);
config.OverlayScaleX = GetFloat(root, "overlayScaleX", config.OverlayScaleX);
config.OverlayScaleZ = GetFloat(root, "overlayScaleZ", config.OverlayScaleZ);
config.FlipX = GetBool(root, "flipX", config.FlipX);
config.FlipZ = GetBool(root, "flipZ", config.FlipZ);
config.EnableCalibrationControls = GetBool(root, "enableCalibrationControls", config.EnableCalibrationControls);
config.CalibrationMoveStep = GetFloat(root, "calibrationMoveStep", config.CalibrationMoveStep);
config.CalibrationRotationStep = GetFloat(root, "calibrationRotationStep", config.CalibrationRotationStep);
config.CalibrationScaleStep = GetFloat(root, "calibrationScaleStep", config.CalibrationScaleStep);
config.UseSegmentCubes = GetBool(root, "useSegmentCubes", config.UseSegmentCubes);
config.RenderWaterLines = GetBool(root, "renderWaterLines", config.RenderWaterLines);
config.RenderWaterAreas = GetBool(root, "renderWaterAreas", config.RenderWaterAreas);
config.RenderRoads = GetBool(root, "renderRoads", config.RenderRoads);
config.RoadGeometrySource = GetString(root, "roadGeometrySource", config.RoadGeometrySource);
config.RenderPaths = GetBool(root, "renderPaths", config.RenderPaths);
config.RoadHighwayFilter = GetString(root, "roadHighwayFilter", config.RoadHighwayFilter);
config.PathHighwayFilter = GetString(root, "pathHighwayFilter", config.PathHighwayFilter);
config.OverlayProfile = GetString(root, "overlayProfile", config.OverlayProfile);
config.RenderZoning = GetBool(root, "renderZoning", config.RenderZoning);
config.ZoningResidentialVisible = GetBool(root, "zoningResidentialVisible", config.ZoningResidentialVisible);
config.ZoningCommercialVisible = GetBool(root, "zoningCommercialVisible", config.ZoningCommercialVisible);
config.ZoningIndustrialVisible = GetBool(root, "zoningIndustrialVisible", config.ZoningIndustrialVisible);
config.ZoningOfficeVisible = GetBool(root, "zoningOfficeVisible", config.ZoningOfficeVisible);
config.ParkingVisible = GetBool(root, "parkingVisible", config.ParkingVisible);
config.ServicesWaterVisible = GetBool(root, "servicesWaterVisible", config.ServicesWaterVisible);
config.ServicesElectricityVisible = GetBool(root, "servicesElectricityVisible", config.ServicesElectricityVisible);
config.ServicesEducationVisible = GetBool(root, "servicesEducationVisible", config.ServicesEducationVisible);
config.ServicesFireVisible = GetBool(root, "servicesFireVisible", config.ServicesFireVisible);
config.ServicesHealthVisible = GetBool(root, "servicesHealthVisible", config.ServicesHealthVisible);
config.ServicesParksVisible = GetBool(root, "servicesParksVisible", config.ServicesParksVisible);
config.ServicesWasteVisible = GetBool(root, "servicesWasteVisible", config.ServicesWasteVisible);
config.ServicesTransportVisible = GetBool(root, "servicesTransportVisible", config.ServicesTransportVisible);
config.ServicesCommunicationVisible = GetBool(root, "servicesCommunicationVisible", config.ServicesCommunicationVisible);
config.ShowOverlayHud = GetBool(root, "showOverlayHud", config.ShowOverlayHud);
config.VerboseOverlayLogs = GetBool(root, "verboseOverlayLogs", config.VerboseOverlayLogs);
config.RenderMapBounds = GetBool(root, "renderMapBounds", config.RenderMapBounds);
                    config.RenderWorldMapBounds = GetBool(root, "renderWorldMapBounds", config.RenderWorldMapBounds);
                    config.RenderHeightMapBounds = GetBool(root, "renderHeightMapBounds", config.RenderHeightMapBounds);
                    config.RenderMapCenter = GetBool(root, "renderMapCenter", config.RenderMapCenter);
config.MapBoundsUseCenterAndSize = GetBool(root, "mapBoundsUseCenterAndSize", config.MapBoundsUseCenterAndSize);
config.WorldMapSizeKm = GetFloat(root, "worldMapSizeKm", config.WorldMapSizeKm);
config.HeightMapSizeKm = GetFloat(root, "heightMapSizeKm", config.HeightMapSizeKm);

config.WorldMapMinLon = GetDouble(root, "worldMapMinLon", config.WorldMapMinLon);
config.WorldMapMinLat = GetDouble(root, "worldMapMinLat", config.WorldMapMinLat);
config.WorldMapMaxLon = GetDouble(root, "worldMapMaxLon", config.WorldMapMaxLon);
config.WorldMapMaxLat = GetDouble(root, "worldMapMaxLat", config.WorldMapMaxLat);

config.HeightMapMinLon = GetDouble(root, "heightMapMinLon", config.HeightMapMinLon);
config.HeightMapMinLat = GetDouble(root, "heightMapMinLat", config.HeightMapMinLat);
config.HeightMapMaxLon = GetDouble(root, "heightMapMaxLon", config.HeightMapMaxLon);
config.HeightMapMaxLat = GetDouble(root, "heightMapMaxLat", config.HeightMapMaxLat);

config.MapBoundsAlpha = GetFloat(root, "mapBoundsAlpha", config.MapBoundsAlpha);
                    config.WorldMapBoundsAlpha = GetFloat(root, "worldMapBoundsAlpha", config.WorldMapBoundsAlpha);
                    config.HeightMapBoundsAlpha = GetFloat(root, "heightMapBoundsAlpha", config.HeightMapBoundsAlpha);
                    config.MapCenterAlpha = GetFloat(root, "mapCenterAlpha", config.MapCenterAlpha);

                    config.WaterLineColor = GetString(root, "waterLineColor", config.WaterLineColor);
                    config.WaterAreaOutlineColor = GetString(root, "waterAreaOutlineColor", config.WaterAreaOutlineColor);
                    config.WaterAreaFillColor = GetString(root, "waterAreaFillColor", config.WaterAreaFillColor);

                    config.RoadColorDefault = GetString(root, "roadColorDefault", config.RoadColorDefault);
                    config.RoadColorMotorway = GetString(root, "roadColorMotorway", config.RoadColorMotorway);
                    config.RoadColorPrimary = GetString(root, "roadColorPrimary", config.RoadColorPrimary);
                    config.RoadColorSecondary = GetString(root, "roadColorSecondary", config.RoadColorSecondary);
                    config.RoadColorTertiary = GetString(root, "roadColorTertiary", config.RoadColorTertiary);
                    config.RoadColorLink = GetString(root, "roadColorLink", config.RoadColorLink);
                    config.PathColor = GetString(root, "pathColor", config.PathColor);

                    config.ZoningResidentialLowColor = GetString(root, "zoningResidentialLowColor", config.ZoningResidentialLowColor);
                    config.ZoningResidentialMediumColor = GetString(root, "zoningResidentialMediumColor", config.ZoningResidentialMediumColor);
                    config.ZoningResidentialHighColor = GetString(root, "zoningResidentialHighColor", config.ZoningResidentialHighColor);
                    config.ZoningCommercialLowColor = GetString(root, "zoningCommercialLowColor", config.ZoningCommercialLowColor);
                    config.ZoningCommercialHighColor = GetString(root, "zoningCommercialHighColor", config.ZoningCommercialHighColor);
                    config.ZoningRetailColor = GetString(root, "zoningRetailColor", config.ZoningRetailColor);
                    config.ZoningIndustrialColor = GetString(root, "zoningIndustrialColor", config.ZoningIndustrialColor);
                    config.ZoningOfficeColor = GetString(root, "zoningOfficeColor", config.ZoningOfficeColor);
                    config.ZoningSurfaceColor = GetString(root, "zoningSurfaceColor", config.ZoningSurfaceColor);
                    config.ZoningRampColor = GetString(root, "zoningRampColor", config.ZoningRampColor);
                    config.ZoningMixedColor = GetString(root, "zoningMixedColor", config.ZoningMixedColor);
                    config.ZoningFallbackColor = GetString(root, "zoningFallbackColor", config.ZoningFallbackColor);

                    config.WorldMapBoundsColor = GetString(root, "worldMapBoundsColor", config.WorldMapBoundsColor);
                    config.HeightMapBoundsColor = GetString(root, "heightMapBoundsColor", config.HeightMapBoundsColor);
                    config.MapCenterColor = GetString(root, "mapCenterColor", config.MapCenterColor);
config.MapBoundsYOffset = GetFloat(root, "mapBoundsYOffset", config.MapBoundsYOffset);
config.MaxZoningFillMeshesDebug = GetInt(root, "maxZoningFillMeshesDebug", config.MaxZoningFillMeshesDebug);
config.ZoningFillYOffset = GetFloat(root, "zoningFillYOffset", config.ZoningFillYOffset);
config.RoadYOffset = GetFloat(root, "roadYOffset", config.RoadYOffset);
config.PathYOffset = GetFloat(root, "pathYOffset", config.PathYOffset);
config.WaterLineYOffset = GetFloat(root, "waterLineYOffset", config.WaterLineYOffset);
config.WaterAreaOutlineYOffset = GetFloat(root, "waterAreaOutlineYOffset", config.WaterAreaOutlineYOffset);
config.ZoningAlpha = GetFloat(root, "zoningAlpha", config.ZoningAlpha);
config.ZoningResidentialAlpha = GetFloat(root, "zoningResidentialAlpha", config.ZoningResidentialAlpha);
config.ZoningCommercialAlpha = GetFloat(root, "zoningCommercialAlpha", config.ZoningCommercialAlpha);
config.ZoningIndustrialAlpha = GetFloat(root, "zoningIndustrialAlpha", config.ZoningIndustrialAlpha);
config.ZoningOfficeAlpha = GetFloat(root, "zoningOfficeAlpha", config.ZoningOfficeAlpha);
config.ParkingAlpha = GetFloat(root, "parkingAlpha", config.ParkingAlpha);
config.ServicesWaterAlpha = GetFloat(root, "servicesWaterAlpha", config.ServicesWaterAlpha);
config.ServicesElectricityAlpha = GetFloat(root, "servicesElectricityAlpha", config.ServicesElectricityAlpha);
config.ServicesEducationAlpha = GetFloat(root, "servicesEducationAlpha", config.ServicesEducationAlpha);
config.ServicesFireAlpha = GetFloat(root, "servicesFireAlpha", config.ServicesFireAlpha);
config.ServicesHealthAlpha = GetFloat(root, "servicesHealthAlpha", config.ServicesHealthAlpha);
config.ServicesParksAlpha = GetFloat(root, "servicesParksAlpha", config.ServicesParksAlpha);
config.ServicesWasteAlpha = GetFloat(root, "servicesWasteAlpha", config.ServicesWasteAlpha);
config.ServicesTransportAlpha = GetFloat(root, "servicesTransportAlpha", config.ServicesTransportAlpha);
config.ServicesCommunicationAlpha = GetFloat(root, "servicesCommunicationAlpha", config.ServicesCommunicationAlpha);
config.WaterAreaFillAlpha = GetFloat(root, "waterAreaFillAlpha", config.WaterAreaFillAlpha);
config.RoadAlpha = GetFloat(root, "roadAlpha", config.RoadAlpha);
config.PathAlpha = GetFloat(root, "pathAlpha", config.PathAlpha);
config.WaterLineAlpha = GetFloat(root, "waterLineAlpha", config.WaterLineAlpha);
config.WaterAreaOutlineAlpha = GetFloat(root, "waterAreaOutlineAlpha", config.WaterAreaOutlineAlpha);
config.ZoningDebugFilterZone = GetString(root, "zoningDebugFilterZone", config.ZoningDebugFilterZone);
config.ZoningDebugFilterCs2Contains = GetString(root, "zoningDebugFilterCs2Contains", config.ZoningDebugFilterCs2Contains);
config.ZoningDebugFilterMaterialKey = GetString(root, "zoningDebugFilterMaterialKey", config.ZoningDebugFilterMaterialKey);
config.SegmentWidth = GetFloat(root, "segmentWidth", config.SegmentWidth);
config.SegmentHeight = GetFloat(root, "segmentHeight", config.SegmentHeight);
config.WaterLineSegmentWidth = GetFloat(root, "waterLineSegmentWidth", config.SegmentWidth);
config.WaterLineSegmentHeight = GetFloat(root, "waterLineSegmentHeight", config.SegmentHeight);
config.WaterAreaSegmentWidth = GetFloat(root, "waterAreaSegmentWidth", config.SegmentWidth);
config.WaterAreaSegmentHeight = GetFloat(root, "waterAreaSegmentHeight", config.SegmentHeight);
config.RenderWaterAreaOutlines = GetBool(root, "renderWaterAreaOutlines", config.RenderWaterAreaOutlines);
config.RenderWaterAreaFillMeshes = GetBool(root, "renderWaterAreaFillMeshes", config.RenderWaterAreaFillMeshes);
config.MaxWaterAreaFillMeshesDebug = GetInt(root, "maxWaterAreaFillMeshesDebug", config.MaxWaterAreaFillMeshesDebug);
config.WaterAreaFillYOffset = GetFloat(root, "waterAreaFillYOffset", config.WaterAreaFillYOffset);

config.RoadSegmentWidth = GetFloat(root, "roadSegmentWidth", config.RoadSegmentWidth);
config.RoadSegmentHeight = GetFloat(root, "roadSegmentHeight", config.RoadSegmentHeight);
config.PathSegmentWidth = GetFloat(root, "pathSegmentWidth", config.PathSegmentWidth);
config.PathSegmentHeight = GetFloat(root, "pathSegmentHeight", config.PathSegmentHeight);
config.MaxRoadSegmentsDebug = GetInt(root, "maxRoadSegmentsDebug", config.MaxRoadSegmentsDebug);
config.MaxPathSegmentsDebug = root["maxPathSegmentsDebug"] != null
    ? GetInt(root, "maxPathSegmentsDebug", config.MaxPathSegmentsDebug)
    : config.MaxRoadSegmentsDebug;
config.MinimumRoadDebugTier = GetInt(root, "minimumRoadDebugTier", config.MinimumRoadDebugTier);

config.RenderEverything = GetBool(root, "renderEverything", config.RenderEverything);
config.RenderAllRoadSegments = GetBool(root, "renderAllRoadSegments", config.RenderAllRoadSegments);
config.RenderAllPathSegments = GetBool(root, "renderAllPathSegments", config.RenderAllPathSegments);
config.RenderAllZoningPolygons = GetBool(root, "renderAllZoningPolygons", config.RenderAllZoningPolygons);
config.RenderAllWaterSegments = GetBool(root, "renderAllWaterSegments", config.RenderAllWaterSegments);
config.RenderAllWaterAreaFills = GetBool(root, "renderAllWaterAreaFills", config.RenderAllWaterAreaFills);

config.UseLaneWidthScaling = GetBool(root, "useLaneWidthScaling", config.UseLaneWidthScaling);
config.LaneWidthScaleFactor = GetFloat(root, "laneWidthScaleFactor", config.LaneWidthScaleFactor);
config.HighlightOneWayRoads = GetBool(root, "highlightOneWayRoads", config.HighlightOneWayRoads);
config.HighlightBridges = GetBool(root, "highlightBridges", config.HighlightBridges);
config.HighlightTunnels = GetBool(root, "highlightTunnels", config.HighlightTunnels);
config.HighlightRoundabouts = GetBool(root, "highlightRoundabouts", config.HighlightRoundabouts);
config.RoadRenderMode = NormalizeRoadRenderMode(GetString(root, "roadRenderMode", config.RoadRenderMode));
config.PathRenderMode = NormalizeRoadRenderMode(GetString(root, "pathRenderMode", config.PathRenderMode));
config.RibbonYOffset = GetFloat(root, "ribbonYOffset", config.RibbonYOffset);
config.EnableRoadSpatialChunking = GetBool(root, "enableRoadSpatialChunking", config.EnableRoadSpatialChunking);
config.RoadChunkSizeMeters = GetFloat(root, "roadChunkSizeMeters", config.RoadChunkSizeMeters);
config.EnableProgressiveOverlayRebuild = GetBool(root, "enableProgressiveOverlayRebuild", config.EnableProgressiveOverlayRebuild);
config.RoadChunksPerFrame = GetInt(root, "roadChunksPerFrame", config.RoadChunksPerFrame);
config.PathChunksPerFrame = GetInt(root, "pathChunksPerFrame", config.PathChunksPerFrame);

config.RenderRoadDirectionArrows = GetBool(root, "renderRoadDirectionArrows", config.RenderRoadDirectionArrows);
config.RenderAllRoadArrows = GetBool(root, "renderAllRoadArrows", config.RenderAllRoadArrows);
config.RoadArrowSpacingMeters = GetFloat(root, "roadArrowSpacingMeters", config.RoadArrowSpacingMeters);
config.RoadArrowSize = GetFloat(root, "roadArrowSize", config.RoadArrowSize);
config.RoadArrowYOffset = GetFloat(root, "roadArrowYOffset", config.RoadArrowYOffset);
config.RoadArrowMaxCount = GetInt(root, "roadArrowMaxCount", config.RoadArrowMaxCount);

config.RenderRoadLabels = GetBool(root, "renderRoadLabels", config.RenderRoadLabels);
config.RenderAllRoadLabels = GetBool(root, "renderAllRoadLabels", config.RenderAllRoadLabels);
config.RoadLabelMaxCount = GetInt(root, "roadLabelMaxCount", config.RoadLabelMaxCount);
config.RoadLabelMinTier = GetInt(root, "roadLabelMinTier", config.RoadLabelMinTier);
config.RoadLabelMaxDistance = GetFloat(root, "roadLabelMaxDistance", config.RoadLabelMaxDistance);
config.RoadLabelYOffset = GetFloat(root, "roadLabelYOffset", config.RoadLabelYOffset);
config.RoadLabelFontSize = GetInt(root, "roadLabelFontSize", config.RoadLabelFontSize);
config.DeduplicateRoadLabels = GetBool(root, "deduplicateRoadLabels", config.DeduplicateRoadLabels);
config.RoadSemanticFilterMode = GetString(root, "roadSemanticFilterMode", config.RoadSemanticFilterMode);
config.RuntimeRoadImportEnabled = GetBool(root, "runtimeRoadImportEnabled", config.RuntimeRoadImportEnabled);
config.RuntimeRoadImportRunOnce = GetBool(root, "runtimeRoadImportRunOnce", config.RuntimeRoadImportRunOnce);
config.RuntimeRoadImportMaxSegments = GetInt(root, "runtimeRoadImportMaxSegments", config.RuntimeRoadImportMaxSegments);
config.RuntimeRoadImportStride = GetInt(root, "runtimeRoadImportStride", config.RuntimeRoadImportStride);
config.RuntimeRoadImportMinSegmentLengthMeters = GetFloat(root, "runtimeRoadImportMinSegmentLengthMeters", config.RuntimeRoadImportMinSegmentLengthMeters);
config.RuntimeRoadImportSnapToleranceMeters = GetFloat(root, "runtimeRoadImportSnapToleranceMeters", config.RuntimeRoadImportSnapToleranceMeters);
config.RuntimeRoadImportYOffset = GetFloat(root, "runtimeRoadImportYOffset", config.RuntimeRoadImportYOffset);
config.RuntimeRoadImportIncludeBridgeTunnel = GetBool(root, "runtimeRoadImportIncludeBridgeTunnel", config.RuntimeRoadImportIncludeBridgeTunnel);
config.RuntimeRoadImportSourceFilter = GetString(root, "runtimeRoadImportSourceFilter", config.RuntimeRoadImportSourceFilter);
config.RuntimeRoadImportHighwayFilter = GetString(root, "runtimeRoadImportHighwayFilter", config.RuntimeRoadImportHighwayFilter);
config.RuntimeRoadImportStageFilter = GetString(root, "runtimeRoadImportStageFilter", config.RuntimeRoadImportStageFilter);
config.RuntimeRoadImportShowProgressInHud = GetBool(root, "runtimeRoadImportShowProgressInHud", config.RuntimeRoadImportShowProgressInHud);
config.RuntimeRoadImportVerboseSelectionLogs = GetBool(root, "runtimeRoadImportVerboseSelectionLogs", config.RuntimeRoadImportVerboseSelectionLogs);
config.RuntimeRoadImportSelectionMode = GetString(root, "runtimeRoadImportSelectionMode", config.RuntimeRoadImportSelectionMode);
config.RuntimeRoadImportDistanceBucketMeters = GetFloat(root, "runtimeRoadImportDistanceBucketMeters", config.RuntimeRoadImportDistanceBucketMeters);
config.RuntimeRoadImportPriorityWeight = GetFloat(root, "runtimeRoadImportPriorityWeight", config.RuntimeRoadImportPriorityWeight);
config.RuntimeRoadImportSkipParkingAisles = GetBool(root, "runtimeRoadImportSkipParkingAisles", config.RuntimeRoadImportSkipParkingAisles);
config.RuntimeRoadImportSkipClearlyUnpaved = GetBool(root, "runtimeRoadImportSkipClearlyUnpaved", config.RuntimeRoadImportSkipClearlyUnpaved);
config.RuntimeRoadImportBatchSize = GetInt(root, "runtimeRoadImportBatchSize", config.RuntimeRoadImportBatchSize);
config.RuntimeRoadImportPipelineMode = GetString(root, "runtimeRoadImportPipelineMode", config.RuntimeRoadImportPipelineMode);

ClampRuntimeRoadImportSettings(config);

                config.UseLineRenderer = GetBool(root, "useLineRenderer", config.UseLineRenderer);
                config.LineWidth = GetFloat(root, "lineWidth", config.LineWidth);

                config.DebugBeacons = GetBool(root, "debugBeacons", config.DebugBeacons);
                config.BeaconHeight = GetFloat(root, "beaconHeight", config.BeaconHeight);
                config.BeaconWidth = GetFloat(root, "beaconWidth", config.BeaconWidth);

                config.DebugTower = GetBool(root, "debugTower", config.DebugTower);
                config.TowerHeight = GetFloat(root, "towerHeight", config.TowerHeight);
                config.TowerWidth = GetFloat(root, "towerWidth", config.TowerWidth);

                // --- Nouveaux paramètres ---
                config.MaxRenderedSegments = GetInt(root, "maxRenderedSegments", config.MaxRenderedSegments);
                config.MaxWaterSegmentsDebug = GetInt(root, "maxWaterSegmentsDebug", config.MaxWaterSegmentsDebug);
                config.PointStride = GetInt(root, "pointStride", config.PointStride);

if (config.PointStride < 1)
    config.PointStride = 1;

if (config.MaxRenderedSegments < 1)
    config.MaxRenderedSegments = 1;

if (config.MaxWaterSegmentsDebug < 1)
    config.MaxWaterSegmentsDebug = 1;

if (config.WaterLineSegmentWidth < 0.1f)
    config.WaterLineSegmentWidth = 0.1f;

if (config.WaterLineSegmentHeight < 0.1f)
    config.WaterLineSegmentHeight = 0.1f;

if (config.WaterAreaSegmentWidth < 0.1f)
    config.WaterAreaSegmentWidth = 0.1f;

if (config.WaterAreaSegmentHeight < 0.1f)
    config.WaterAreaSegmentHeight = 0.1f;

if (config.MaxWaterAreaFillMeshesDebug < 1)
    config.MaxWaterAreaFillMeshesDebug = 1;

if (config.WaterAreaFillYOffset < 0f)
    config.WaterAreaFillYOffset = 0f;

if (config.RoadSegmentWidth < 1f)
    config.RoadSegmentWidth = 1f;

if (config.RoadSegmentHeight < 1f)
    config.RoadSegmentHeight = 1f;
if (config.PathSegmentWidth < 0.1f)
    config.PathSegmentWidth = 0.1f;

if (config.PathSegmentHeight < 0.1f)
    config.PathSegmentHeight = 0.1f;

if (config.MinimumRoadDebugTier < 0)
    config.MinimumRoadDebugTier = 0;

if (config.MinimumRoadDebugTier > 4)
    config.MinimumRoadDebugTier = 4;

if (config.MaxZoningFillMeshesDebug < 1)
    config.MaxZoningFillMeshesDebug = 1;

if (config.ZoningFillYOffset < 0f)
    config.ZoningFillYOffset = 0f;

if (config.RoadYOffset < 0f)
    config.RoadYOffset = 0f;
if (config.PathYOffset < 0f)
    config.PathYOffset = 0f;

if (config.WaterLineYOffset < 0f)
    config.WaterLineYOffset = 0f;

if (config.WaterAreaOutlineYOffset < 0f)
    config.WaterAreaOutlineYOffset = 0f;

config.ZoningAlpha = Clamp01(config.ZoningAlpha);
config.WaterAreaFillAlpha = Clamp01(config.WaterAreaFillAlpha);
config.RoadAlpha = Clamp01(config.RoadAlpha);
config.PathAlpha = Clamp01(config.PathAlpha);
config.WaterLineAlpha = Clamp01(config.WaterLineAlpha);
config.WaterAreaOutlineAlpha = Clamp01(config.WaterAreaOutlineAlpha);
config.MapBoundsAlpha = Clamp01(config.MapBoundsAlpha);
config.WorldMapBoundsAlpha = Clamp01(config.WorldMapBoundsAlpha);
config.HeightMapBoundsAlpha = Clamp01(config.HeightMapBoundsAlpha);
config.MapCenterAlpha = Clamp01(config.MapCenterAlpha);

if (config.MapBoundsYOffset < 0f)
    config.MapBoundsYOffset = 0f;

if (config.WorldMapSizeKm <= 0f)
    config.WorldMapSizeKm = 57.344f;

if (config.HeightMapSizeKm <= 0f)
    config.HeightMapSizeKm = 19.115f;

if (config.MaxRoadSegmentsDebug < 1)
    config.MaxRoadSegmentsDebug = 1;

if (config.MaxPathSegmentsDebug < 1)
    config.MaxPathSegmentsDebug = 1;

if (config.LaneWidthScaleFactor < 0f)
    config.LaneWidthScaleFactor = 0f;

if (config.LaneWidthScaleFactor > 2f)
    config.LaneWidthScaleFactor = 2f;

if (config.RoadArrowSpacingMeters < 1f)
    config.RoadArrowSpacingMeters = 1f;

if (config.RoadArrowSize < 0.1f)
    config.RoadArrowSize = 0.1f;

if (config.RoadArrowYOffset < 0f)
    config.RoadArrowYOffset = 0f;

if (config.RoadArrowMaxCount < 0)
    config.RoadArrowMaxCount = 0;

if (config.RoadLabelMaxCount < 0)
    config.RoadLabelMaxCount = 0;

if (config.RoadLabelMinTier < 0)
    config.RoadLabelMinTier = 0;

if (config.RoadLabelMinTier > 4)
    config.RoadLabelMinTier = 4;

if (config.RoadLabelMaxDistance < 0f)
    config.RoadLabelMaxDistance = 0f;

if (config.RoadLabelYOffset < 0f)
    config.RoadLabelYOffset = 0f;

if (config.RoadLabelFontSize < 8)
    config.RoadLabelFontSize = 8;

if (config.RoadLabelFontSize > 128)
    config.RoadLabelFontSize = 128;

if (config.RibbonYOffset < 0f)
    config.RibbonYOffset = 0f;

config.RoadChunkSizeMeters = Mathf.Clamp(config.RoadChunkSizeMeters, 128f, 4096f);
config.RoadChunksPerFrame = Mathf.Clamp(config.RoadChunksPerFrame, 1, 64);
config.PathChunksPerFrame = Mathf.Clamp(config.PathChunksPerFrame, 1, 64);

config.RoadRenderMode = NormalizeRoadRenderMode(config.RoadRenderMode);
config.PathRenderMode = NormalizeRoadRenderMode(config.PathRenderMode);

if (config.OverlayScaleX <= 0f)
    config.OverlayScaleX = 1f;

if (config.OverlayScaleZ <= 0f)
    config.OverlayScaleZ = 1f;

config.RoadGeometrySource = string.IsNullOrWhiteSpace(config.RoadGeometrySource)
    ? "major"
    : config.RoadGeometrySource.Trim().ToLowerInvariant();

if (config.RoadGeometrySource != "major" && config.RoadGeometrySource != "driveable")
{
    Log.Error("GeoOverlayConfig: invalid roadGeometrySource=" + config.RoadGeometrySource + ". Fallback to major.");
    config.RoadGeometrySource = "major";
}

config.RoadHighwayFilter = string.IsNullOrWhiteSpace(config.RoadHighwayFilter)
    ? "all"
    : config.RoadHighwayFilter.Trim().ToLowerInvariant();

config.PathHighwayFilter = string.IsNullOrWhiteSpace(config.PathHighwayFilter)
    ? "all"
    : config.PathHighwayFilter.Trim().ToLowerInvariant();

config.OverlayProfile = string.IsNullOrWhiteSpace(config.OverlayProfile)
    ? "custom"
    : config.OverlayProfile.Trim().ToLowerInvariant();

config.RoadSemanticFilterMode = NormalizeRoadSemanticFilterMode(config.RoadSemanticFilterMode);

if (!config.ModEnabled)
{
    Log.Info("GeoOverlayConfig: modEnabled=false. Skipping overlay profile and bundle manifest.");
    Log.Info("GeoOverlayConfig: loaded " + path);
    return config;
}

if (string.Equals(config.OverlayProfile, "safe", StringComparison.OrdinalIgnoreCase))
{
    ApplySafeOverlayProfile(config);
}
else
{
    ApplyOverlayProfile(config);
}

                var resolvedBundleManifestPath = BundleResolver.ResolveManifestPath(config, modDir);

                if (!string.IsNullOrWhiteSpace(resolvedBundleManifestPath))
                {
                    config.BundleManifestPath = resolvedBundleManifestPath;
                    ApplyBundleManifest(config, modDir);
                }

                Log.Info("GeoOverlayConfig: loaded " + path);
                Log.Info("GeoOverlayConfig: packPath=" + (string.IsNullOrWhiteSpace(config.PackPath) ? "(empty)" : config.PackPath));
Log.Info(
    "GeoOverlayConfig: renderWaterLines=" + config.RenderWaterLines +
    ", renderWaterAreas=" + config.RenderWaterAreas +
    ", renderRoads=" + config.RenderRoads +
    ", roadGeometrySource=" + config.RoadGeometrySource +
    ", renderPaths=" + config.RenderPaths +
    ", roadHighwayFilter=" + config.RoadHighwayFilter +
    ", pathHighwayFilter=" + config.PathHighwayFilter +
    ", overlayProfile=" + config.OverlayProfile
);

Log.Info(
    "GeoOverlayConfig: waterLineSegmentWidth=" + config.WaterLineSegmentWidth +
    ", waterLineSegmentHeight=" + config.WaterLineSegmentHeight +
    ", waterAreaSegmentWidth=" + config.WaterAreaSegmentWidth +
    ", waterAreaSegmentHeight=" + config.WaterAreaSegmentHeight
);

Log.Info(
    "GeoOverlayConfig: renderWaterAreaOutlines=" + config.RenderWaterAreaOutlines +
    ", renderWaterAreaFillMeshes=" + config.RenderWaterAreaFillMeshes +
    ", maxWaterAreaFillMeshesDebug=" + config.MaxWaterAreaFillMeshesDebug +
    ", waterAreaFillYOffset=" + config.WaterAreaFillYOffset
);

Log.Info(
    "GeoOverlayConfig: roadSegmentWidth=" + config.RoadSegmentWidth +
    ", roadSegmentHeight=" + config.RoadSegmentHeight +
    ", pathSegmentWidth=" + config.PathSegmentWidth +
    ", pathSegmentHeight=" + config.PathSegmentHeight +
    ", pathYOffset=" + config.PathYOffset +
    ", pathAlpha=" + config.PathAlpha +
    ", maxRoadSegmentsDebug=" + config.MaxRoadSegmentsDebug +
    ", maxPathSegmentsDebug=" + config.MaxPathSegmentsDebug +
    " | maxWaterSegmentsDebug=" + config.MaxWaterSegmentsDebug +
    " | overlayRotationDegrees=" + config.OverlayRotationDegrees +
    ", overlayScaleX=" + config.OverlayScaleX +
    ", overlayScaleZ=" + config.OverlayScaleZ +
    ", flipX=" + config.FlipX +
    ", flipZ=" + config.FlipZ
);

Log.Info(
    "GeoOverlayConfig: minimumRoadDebugTier=" + config.MinimumRoadDebugTier
);

Log.Info(
    "GeoOverlayConfig: runtimeRoadImport enabled=" + config.RuntimeRoadImportEnabled +
    ", runOnce=" + config.RuntimeRoadImportRunOnce +
    ", maxSegments=" + config.RuntimeRoadImportMaxSegments +
    ", stride=" + config.RuntimeRoadImportStride +
    ", minSegmentLength=" + config.RuntimeRoadImportMinSegmentLengthMeters +
    ", snapTolerance=" + config.RuntimeRoadImportSnapToleranceMeters +
    ", yOffset=" + config.RuntimeRoadImportYOffset +
    ", includeBridgeTunnel=" + config.RuntimeRoadImportIncludeBridgeTunnel +
    ", sourceFilter=" + config.RuntimeRoadImportSourceFilter +
    ", highwayFilter=" + config.RuntimeRoadImportHighwayFilter +
    ", stageFilter=" + config.RuntimeRoadImportStageFilter +
    ", showProgressInHud=" + config.RuntimeRoadImportShowProgressInHud +
    ", verboseSelectionLogs=" + config.RuntimeRoadImportVerboseSelectionLogs +
    ", selectionMode=" + config.RuntimeRoadImportSelectionMode +
    ", distanceBucketMeters=" + config.RuntimeRoadImportDistanceBucketMeters +
    ", priorityWeight=" + config.RuntimeRoadImportPriorityWeight +
    ", skipParkingAisles=" + config.RuntimeRoadImportSkipParkingAisles +
    ", skipClearlyUnpaved=" + config.RuntimeRoadImportSkipClearlyUnpaved +
    ", batchSize=" + config.RuntimeRoadImportBatchSize +
    ", pipelineMode=" + config.RuntimeRoadImportPipelineMode
);

Log.Info(
    "GeoOverlayConfig: renderEverything=" + config.RenderEverything +
    ", renderAllRoadSegments=" + config.RenderAllRoadSegments +
    ", renderAllPathSegments=" + config.RenderAllPathSegments +
    ", renderAllZoningPolygons=" + config.RenderAllZoningPolygons +
    ", renderAllWaterSegments=" + config.RenderAllWaterSegments +
    ", renderAllWaterAreaFills=" + config.RenderAllWaterAreaFills
);

Log.Info(
    "GeoOverlayConfig: road enriched rendering laneWidthScaling=" + config.UseLaneWidthScaling +
    ", laneWidthScaleFactor=" + config.LaneWidthScaleFactor +
    ", highlightOneWayRoads=" + config.HighlightOneWayRoads +
    ", highlightBridges=" + config.HighlightBridges +
    ", highlightTunnels=" + config.HighlightTunnels +
    ", highlightRoundabouts=" + config.HighlightRoundabouts +
    ", roadRenderMode=" + config.RoadRenderMode +
    ", pathRenderMode=" + config.PathRenderMode +
    ", ribbonYOffset=" + config.RibbonYOffset +
    ", enableRoadSpatialChunking=" + config.EnableRoadSpatialChunking +
    ", roadChunkSizeMeters=" + config.RoadChunkSizeMeters +
    ", enableProgressiveOverlayRebuild=" + config.EnableProgressiveOverlayRebuild +
    ", roadChunksPerFrame=" + config.RoadChunksPerFrame +
    ", pathChunksPerFrame=" + config.PathChunksPerFrame +
    ", semanticFilter=" + config.RoadSemanticFilterMode
);

Log.Info(
    "GeoOverlayConfig: renderZoning=" + config.RenderZoning +
    ", maxZoningFillMeshesDebug=" + config.MaxZoningFillMeshesDebug +
    ", zoningFillYOffset=" + config.ZoningFillYOffset
);

Log.Info(
    "GeoOverlayConfig: layerYOffsets road=" + config.RoadYOffset +
    ", waterLine=" + config.WaterLineYOffset +
    ", waterAreaOutline=" + config.WaterAreaOutlineYOffset +
    ", zoningFill=" + config.ZoningFillYOffset +
    ", waterAreaFill=" + config.WaterAreaFillYOffset
);

Log.Info(
    "GeoOverlayConfig: layerAlphas zoning=" + config.ZoningAlpha +
    ", waterAreaFill=" + config.WaterAreaFillAlpha +
    ", road=" + config.RoadAlpha +
    ", waterLine=" + config.WaterLineAlpha +
    ", waterAreaOutline=" + config.WaterAreaOutlineAlpha
);

Log.Info(
    "GeoOverlayConfig: zoningDebugFilterZone=" +
    (string.IsNullOrWhiteSpace(config.ZoningDebugFilterZone) ? "(empty)" : config.ZoningDebugFilterZone)
);

Log.Info(
    "GeoOverlayConfig: zoningDebugFilterCs2Contains=" +
    (string.IsNullOrWhiteSpace(config.ZoningDebugFilterCs2Contains) ? "(empty)" : config.ZoningDebugFilterCs2Contains)
);

Log.Info(
    "GeoOverlayConfig: zoningDebugFilterMaterialKey=" +
    (string.IsNullOrWhiteSpace(config.ZoningDebugFilterMaterialKey) ? "(empty)" : config.ZoningDebugFilterMaterialKey)
);

            }
            catch (Exception ex)
            {
                Log.Error("GeoOverlayConfig: failed to load config.json. Using defaults. " + ex);
            }

            return config;
        }

        private static void ClampRuntimeRoadImportSettings(GeoOverlayConfig config)
        {
            if (config == null)
                return;

            // 0 = illimité.
            if (config.RuntimeRoadImportMaxSegments < 0)
                config.RuntimeRoadImportMaxSegments = 0;

            // Garde-fou très haut pour éviter une valeur JSON absurde.
            if (config.RuntimeRoadImportMaxSegments > 1000000)
                config.RuntimeRoadImportMaxSegments = 1000000;

            if (config.RuntimeRoadImportStride < 1)
                config.RuntimeRoadImportStride = 1;

            if (config.RuntimeRoadImportStride > 100)
                config.RuntimeRoadImportStride = 100;

            if (config.RuntimeRoadImportMinSegmentLengthMeters < 0.5f)
                config.RuntimeRoadImportMinSegmentLengthMeters = 0.5f;

            if (config.RuntimeRoadImportSnapToleranceMeters < 0.1f)
                config.RuntimeRoadImportSnapToleranceMeters = 0.1f;

            if (config.RuntimeRoadImportSnapToleranceMeters > 50f)
                config.RuntimeRoadImportSnapToleranceMeters = 50f;

            if (config.RuntimeRoadImportYOffset < 0f)
                config.RuntimeRoadImportYOffset = 0f;

            if (config.RuntimeRoadImportYOffset > 10f)
                config.RuntimeRoadImportYOffset = 10f;

            config.RuntimeRoadImportSourceFilter = NormalizeRuntimeRoadImportSourceFilter(config.RuntimeRoadImportSourceFilter);
            config.RuntimeRoadImportHighwayFilter = NormalizeRuntimeCsvFilter(
                config.RuntimeRoadImportHighwayFilter,
                "all",
                IsValidRuntimeRoadImportHighwayFilterToken,
                "runtimeRoadImportHighwayFilter");
            config.RuntimeRoadImportStageFilter = NormalizeRuntimeCsvFilter(
                config.RuntimeRoadImportStageFilter,
                "all",
                IsValidRuntimeRoadImportStageFilterToken,
                "runtimeRoadImportStageFilter");

            if (string.IsNullOrWhiteSpace(config.RuntimeRoadImportSelectionMode))
                config.RuntimeRoadImportSelectionMode = "staged-cell-priority";

            config.RuntimeRoadImportSelectionMode = config.RuntimeRoadImportSelectionMode.Trim().ToLowerInvariant();

            if (config.RuntimeRoadImportSelectionMode != "staged-cell-priority" &&
                config.RuntimeRoadImportSelectionMode != "staged-cell" &&
                config.RuntimeRoadImportSelectionMode != "whole-map-priority" &&
                config.RuntimeRoadImportSelectionMode != "whole-map" &&
                config.RuntimeRoadImportSelectionMode != "near-center-priority" &&
                config.RuntimeRoadImportSelectionMode != "near-center" &&
                config.RuntimeRoadImportSelectionMode != "file-order" &&
                config.RuntimeRoadImportSelectionMode != "sequential")
            {
                Log.Error("GeoOverlayConfig: invalid runtimeRoadImportSelectionMode=" + config.RuntimeRoadImportSelectionMode + ". Fallback to staged-cell-priority.");
                config.RuntimeRoadImportSelectionMode = "staged-cell-priority";
            }

            if (config.RuntimeRoadImportDistanceBucketMeters < 50f)
                config.RuntimeRoadImportDistanceBucketMeters = 50f;

            if (config.RuntimeRoadImportDistanceBucketMeters > 5000f)
                config.RuntimeRoadImportDistanceBucketMeters = 5000f;

            if (config.RuntimeRoadImportPriorityWeight < 0f)
                config.RuntimeRoadImportPriorityWeight = 0f;

            if (config.RuntimeRoadImportPriorityWeight > 3f)
                config.RuntimeRoadImportPriorityWeight = 3f;

            if (config.RuntimeRoadImportBatchSize < 1)
                config.RuntimeRoadImportBatchSize = 1;

            if (config.RuntimeRoadImportBatchSize > 4096)
                config.RuntimeRoadImportBatchSize = 4096;

            config.RuntimeRoadImportPipelineMode = NormalizeRuntimeRoadImportPipelineMode(config.RuntimeRoadImportPipelineMode);
        }

        private static string NormalizeRuntimeRoadImportPipelineMode(string value)
        {
            var normalized = string.IsNullOrWhiteSpace(value)
                ? "confirmed-fast-flush"
                : value.Trim().ToLowerInvariant();

            normalized = normalized.Replace("_", "-");

            if (normalized == "fast" ||
                normalized == "flush" ||
                normalized == "fast-flush" ||
                normalized == "earth2cities" ||
                normalized == "confirmed" ||
                normalized == "confirmed-chunks" ||
                normalized == "confirmed-fast-flush")
            {
                return "confirmed-fast-flush";
            }

            if (normalized == "legacy" ||
                normalized == "legacy-fast" ||
                normalized == "legacy-fast-flush" ||
                normalized == "unbounded-fast-flush")
            {
                return "legacy-fast-flush";
            }

            if (normalized == "batch" ||
                normalized == "safe" ||
                normalized == "batch-safe")
            {
                return "batch-safe";
            }

            Log.Error("GeoOverlayConfig: invalid runtimeRoadImportPipelineMode=" + normalized + ". Fallback to confirmed-fast-flush.");
            return "confirmed-fast-flush";
        }

        private static string NormalizeRuntimeRoadImportSourceFilter(string value)
        {
            var normalized = string.IsNullOrWhiteSpace(value)
                ? "roads"
                : value.Trim().ToLowerInvariant();

            normalized = normalized.Replace("_", "-");

            if (normalized == "both" || normalized == "all" || normalized == "roads,paths" || normalized == "paths,roads")
                return "roads-and-paths";

            if (normalized == "road")
                return "roads";

            if (normalized == "path")
                return "paths";

            if (normalized == "roads" || normalized == "paths" || normalized == "roads-and-paths")
                return normalized;

            Log.Error("GeoOverlayConfig: invalid runtimeRoadImportSourceFilter=" + normalized + ". Fallback to roads.");
            return "roads";
        }

        private static string NormalizeRuntimeCsvFilter(
            string value,
            string fallback,
            Func<string, bool> isValidToken,
            string configKey)
        {
            if (string.IsNullOrWhiteSpace(value))
                return fallback;

            var raw = value.Trim().ToLowerInvariant();

            if (raw == "all" || raw == "*")
                return "all";

            var parts = raw.Split(',');
            var normalized = "";

            for (var i = 0; i < parts.Length; i++)
            {
                var token = parts[i] == null ? "" : parts[i].Trim().ToLowerInvariant();

                if (string.IsNullOrWhiteSpace(token))
                    continue;

                if (!isValidToken(token))
                {
                    Log.Error("GeoOverlayConfig: invalid " + configKey + " token=" + token + ". Ignored.");
                    continue;
                }

                if (ContainsCsvToken(normalized, token))
                    continue;

                normalized = string.IsNullOrWhiteSpace(normalized)
                    ? token
                    : normalized + "," + token;
            }

            if (string.IsNullOrWhiteSpace(normalized))
            {
                Log.Error("GeoOverlayConfig: invalid " + configKey + "=" + raw + ". Fallback to " + fallback + ".");
                return fallback;
            }

            return normalized;
        }

        private static bool ContainsCsvToken(string csv, string token)
        {
            if (string.IsNullOrWhiteSpace(csv) || string.IsNullOrWhiteSpace(token))
                return false;

            var parts = csv.Split(',');

            for (var i = 0; i < parts.Length; i++)
            {
                if (string.Equals(parts[i].Trim(), token, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static bool IsValidRuntimeRoadImportHighwayFilterToken(string token)
        {
            switch (token)
            {
                case "motorway":
                case "trunk":
                case "primary":
                case "secondary":
                case "tertiary":
                case "residential":
                case "unclassified":
                case "living_street":
                case "service":
                case "track":
                case "motorway_link":
                case "trunk_link":
                case "primary_link":
                case "secondary_link":
                case "tertiary_link":
                case "path":
                case "footway":
                case "cycleway":
                    return true;

                default:
                    return false;
            }
        }

        private static bool IsValidRuntimeRoadImportStageFilterToken(string token)
        {
            switch (token)
            {
                case "backbone":
                case "urban":
                case "local":
                case "service":
                case "optional":
                    return true;

                default:
                    return false;
            }
        }

        private static void ApplySafeOverlayProfile(GeoOverlayConfig config)
        {
            if (config == null)
                return;

            config.RenderEverything = false;
            config.RenderAllRoadSegments = false;
            config.RenderAllPathSegments = false;
            config.RenderAllZoningPolygons = false;
            config.RenderAllWaterSegments = false;
            config.RenderAllWaterAreaFills = false;

            config.RenderRoads = true;
            config.RenderPaths = false;
            config.RenderWaterLines = false;
            config.RenderWaterAreas = false;
            config.RenderWaterAreaOutlines = false;
            config.RenderWaterAreaFillMeshes = false;
            config.RenderZoning = false;

            config.MaxRoadSegmentsDebug = Math.Min(Math.Max(config.MaxRoadSegmentsDebug, 1), 50000);
            config.MaxPathSegmentsDebug = 0;
            config.MaxWaterSegmentsDebug = 0;
            config.MaxWaterAreaFillMeshesDebug = 0;
            config.MaxZoningFillMeshesDebug = 0;

            config.RenderRoadDirectionArrows = false;
            config.RenderRoadLabels = false;
            config.RoadArrowMaxCount = Math.Min(Math.Max(config.RoadArrowMaxCount, 0), 5000);
            config.RoadLabelMaxCount = Math.Min(Math.Max(config.RoadLabelMaxCount, 0), 300);

            config.OverlayProfile = "safe";
            Log.Info("GeoOverlayConfig: overlayProfile=safe applied.");
        }
        internal void SaveCalibrationToConfig()
        {
            if (string.IsNullOrWhiteSpace(ConfigPath))
            {
                Log.Error("GeoOverlayConfig: cannot save calibration. ConfigPath is empty.");
                return;
            }

            try
            {
                JObject root;

                if (File.Exists(ConfigPath))
                {
                    var json = File.ReadAllText(ConfigPath);
                    root = JObject.Parse(json);
                }
                else
                {
                    root = new JObject();
                }

                root["modEnabled"] = ModEnabled;
                root["worldOriginX"] = WorldOriginX;
                root["worldOriginZ"] = WorldOriginZ;
                root["worldScale"] = WorldScale;

                root["overlayRotationDegrees"] = OverlayRotationDegrees;
                root["overlayScaleX"] = OverlayScaleX;
                root["overlayScaleZ"] = OverlayScaleZ;
                root["flipX"] = FlipX;
                root["flipZ"] = FlipZ;

                root["renderWaterLines"] = RenderWaterLines;
                root["renderWaterAreas"] = RenderWaterAreas;
                root["renderRoads"] = RenderRoads;
                root["roadGeometrySource"] = RoadGeometrySource;
                root["renderPaths"] = RenderPaths;
                root["roadHighwayFilter"] = RoadHighwayFilter;
                root["pathHighwayFilter"] = PathHighwayFilter;
                root["overlayProfile"] = OverlayProfile;
                root["renderZoning"] = RenderZoning;
                root["showOverlayHud"] = ShowOverlayHud;
                root["renderMapBounds"] = RenderMapBounds;
                root["renderWorldMapBounds"] = RenderWorldMapBounds;
                root["renderHeightMapBounds"] = RenderHeightMapBounds;
                root["renderMapCenter"] = RenderMapCenter;
                root["mapBoundsUseCenterAndSize"] = MapBoundsUseCenterAndSize;
                root["worldMapSizeKm"] = WorldMapSizeKm;
                root["heightMapSizeKm"] = HeightMapSizeKm;

                root["worldMapMinLon"] = WorldMapMinLon;
                root["worldMapMinLat"] = WorldMapMinLat;
                root["worldMapMaxLon"] = WorldMapMaxLon;
                root["worldMapMaxLat"] = WorldMapMaxLat;

                root["heightMapMinLon"] = HeightMapMinLon;
                root["heightMapMinLat"] = HeightMapMinLat;
                root["heightMapMaxLon"] = HeightMapMaxLon;
                root["heightMapMaxLat"] = HeightMapMaxLat;

                root["mapBoundsAlpha"] = MapBoundsAlpha;
                root["worldMapBoundsAlpha"] = WorldMapBoundsAlpha;
                root["heightMapBoundsAlpha"] = HeightMapBoundsAlpha;
                root["mapCenterAlpha"] = MapCenterAlpha;
                root["mapBoundsYOffset"] = MapBoundsYOffset;
                root["maxZoningFillMeshesDebug"] = MaxZoningFillMeshesDebug;
                root["zoningFillYOffset"] = ZoningFillYOffset;
                root["roadYOffset"] = RoadYOffset;
                root["pathYOffset"] = PathYOffset;
                root["waterLineYOffset"] = WaterLineYOffset;
                root["waterAreaOutlineYOffset"] = WaterAreaOutlineYOffset;
                root["zoningAlpha"] = ZoningAlpha;
                root["waterAreaFillAlpha"] = WaterAreaFillAlpha;
                root["roadAlpha"] = RoadAlpha;
                root["pathAlpha"] = PathAlpha;
                root["pathSegmentWidth"] = PathSegmentWidth;
                root["pathSegmentHeight"] = PathSegmentHeight;
                root["waterLineAlpha"] = WaterLineAlpha;
                root["waterAreaOutlineAlpha"] = WaterAreaOutlineAlpha;
                root["zoningDebugFilterZone"] = ZoningDebugFilterZone;
                root["zoningDebugFilterCs2Contains"] = ZoningDebugFilterCs2Contains;
                root["zoningDebugFilterMaterialKey"] = ZoningDebugFilterMaterialKey;
                root["minimumRoadDebugTier"] = MinimumRoadDebugTier;
                root["maxRoadSegmentsDebug"] = MaxRoadSegmentsDebug;
                root["maxPathSegmentsDebug"] = MaxPathSegmentsDebug;
                root["maxWaterSegmentsDebug"] = MaxWaterSegmentsDebug;
                root["renderEverything"] = RenderEverything;
                root["renderAllRoadSegments"] = RenderAllRoadSegments;
                root["renderAllPathSegments"] = RenderAllPathSegments;
                root["renderAllZoningPolygons"] = RenderAllZoningPolygons;
                root["renderAllWaterSegments"] = RenderAllWaterSegments;
                root["renderAllWaterAreaFills"] = RenderAllWaterAreaFills;
                root["useLaneWidthScaling"] = UseLaneWidthScaling;
                root["laneWidthScaleFactor"] = LaneWidthScaleFactor;
                root["highlightOneWayRoads"] = HighlightOneWayRoads;
                root["highlightBridges"] = HighlightBridges;
                root["highlightTunnels"] = HighlightTunnels;
                root["highlightRoundabouts"] = HighlightRoundabouts;
                root["roadRenderMode"] = "ribbon";
                root["pathRenderMode"] = "ribbon";
                root["ribbonYOffset"] = RibbonYOffset;
                root["enableRoadSpatialChunking"] = EnableRoadSpatialChunking;
                root["roadChunkSizeMeters"] = RoadChunkSizeMeters;
                root["enableProgressiveOverlayRebuild"] = EnableProgressiveOverlayRebuild;
                root["roadChunksPerFrame"] = RoadChunksPerFrame;
                root["pathChunksPerFrame"] = PathChunksPerFrame;
                root["renderRoadDirectionArrows"] = RenderRoadDirectionArrows;
                root["renderAllRoadArrows"] = RenderAllRoadArrows;
                root["roadArrowSpacingMeters"] = RoadArrowSpacingMeters;
                root["roadArrowSize"] = RoadArrowSize;
                root["roadArrowYOffset"] = RoadArrowYOffset;
                root["roadArrowMaxCount"] = RoadArrowMaxCount;
                root["renderRoadLabels"] = RenderRoadLabels;
                root["renderAllRoadLabels"] = RenderAllRoadLabels;
                root["roadLabelMaxCount"] = RoadLabelMaxCount;
                root["roadLabelMinTier"] = RoadLabelMinTier;
                root["roadLabelMaxDistance"] = RoadLabelMaxDistance;
                root["roadLabelYOffset"] = RoadLabelYOffset;
                root["roadLabelFontSize"] = RoadLabelFontSize;
                root["deduplicateRoadLabels"] = DeduplicateRoadLabels;
                root["roadSemanticFilterMode"] = RoadSemanticFilterMode;
                root["runtimeRoadImportEnabled"] = RuntimeRoadImportEnabled;
                root["runtimeRoadImportRunOnce"] = RuntimeRoadImportRunOnce;
                root["runtimeRoadImportMaxSegments"] = RuntimeRoadImportMaxSegments;
                root["runtimeRoadImportStride"] = RuntimeRoadImportStride;
                root["runtimeRoadImportMinSegmentLengthMeters"] = RuntimeRoadImportMinSegmentLengthMeters;
                root["runtimeRoadImportSnapToleranceMeters"] = RuntimeRoadImportSnapToleranceMeters;
                root["runtimeRoadImportYOffset"] = RuntimeRoadImportYOffset;
                root["runtimeRoadImportIncludeBridgeTunnel"] = RuntimeRoadImportIncludeBridgeTunnel;
                root["runtimeRoadImportSourceFilter"] = RuntimeRoadImportSourceFilter;
                root["runtimeRoadImportHighwayFilter"] = RuntimeRoadImportHighwayFilter;
                root["runtimeRoadImportStageFilter"] = RuntimeRoadImportStageFilter;
                root["runtimeRoadImportShowProgressInHud"] = RuntimeRoadImportShowProgressInHud;
                root["runtimeRoadImportVerboseSelectionLogs"] = RuntimeRoadImportVerboseSelectionLogs;
                root["runtimeRoadImportSelectionMode"] = RuntimeRoadImportSelectionMode;
                root["runtimeRoadImportDistanceBucketMeters"] = RuntimeRoadImportDistanceBucketMeters;
                root["runtimeRoadImportPriorityWeight"] = RuntimeRoadImportPriorityWeight;
                root["runtimeRoadImportSkipParkingAisles"] = RuntimeRoadImportSkipParkingAisles;
                root["runtimeRoadImportSkipClearlyUnpaved"] = RuntimeRoadImportSkipClearlyUnpaved;
                root["runtimeRoadImportBatchSize"] = RuntimeRoadImportBatchSize;
                root["runtimeRoadImportPipelineMode"] = RuntimeRoadImportPipelineMode;
                root["maxRenderedSegments"] = MaxRenderedSegments;
                root["pointStride"] = PointStride;

                root["waterLineSegmentWidth"] = WaterLineSegmentWidth;
                root["waterLineSegmentHeight"] = WaterLineSegmentHeight;
                root["waterAreaSegmentWidth"] = WaterAreaSegmentWidth;
                root["waterAreaSegmentHeight"] = WaterAreaSegmentHeight;
                root["renderWaterAreaOutlines"] = RenderWaterAreaOutlines;
                root["renderWaterAreaFillMeshes"] = RenderWaterAreaFillMeshes;
                root["maxWaterAreaFillMeshesDebug"] = MaxWaterAreaFillMeshesDebug;
                root["waterAreaFillYOffset"] = WaterAreaFillYOffset;

                if (!string.IsNullOrWhiteSpace(BundleManifestPath))
                    root["bundleManifestPath"] = BundleManifestPath;

                if (!string.IsNullOrWhiteSpace(PackPath))
                    root["packPath"] = PackPath;

                File.WriteAllText(ConfigPath, root.ToString(), System.Text.Encoding.UTF8);

                Log.Info(
                    "GeoOverlayConfig: calibration saved. " +
                    "worldOriginX=" + WorldOriginX +
                    ", worldOriginZ=" + WorldOriginZ +
                    ", worldScale=" + WorldScale +
                    ", overlayRotationDegrees=" + OverlayRotationDegrees +
                    ", overlayScaleX=" + OverlayScaleX +
                    ", overlayScaleZ=" + OverlayScaleZ +
                    ", flipX=" + FlipX +
                    ", flipZ=" + FlipZ
                );
            }
            catch (Exception ex)
            {
                Log.Error("GeoOverlayConfig: failed to save calibration. " + ex);
            }
        }

        internal void SaveVisualSettingsToConfig()
        {
            if (string.IsNullOrWhiteSpace(ConfigPath))
            {
                Log.Error("GeoOverlayConfig: cannot save visual settings. ConfigPath is empty.");
                return;
            }

            try
            {
                JObject root;

                if (File.Exists(ConfigPath))
                {
                    var json = File.ReadAllText(ConfigPath);
                    root = JObject.Parse(json);
                }
                else
                {
                    root = new JObject();
                }

                root["renderWaterLines"] = RenderWaterLines;
                root["renderWaterAreas"] = RenderWaterAreas;
                root["renderWaterAreaOutlines"] = RenderWaterAreaOutlines;
                root["renderWaterAreaFillMeshes"] = RenderWaterAreaFillMeshes;

                root["renderRoads"] = RenderRoads;
                root["renderPaths"] = RenderPaths;
                root["renderZoning"] = RenderZoning;
                root["renderMapBounds"] = RenderMapBounds;
                root["renderWorldMapBounds"] = RenderWorldMapBounds;
                root["renderHeightMapBounds"] = RenderHeightMapBounds;
                root["renderMapCenter"] = RenderMapCenter;
                root["verboseOverlayLogs"] = VerboseOverlayLogs;

                root["roadGeometrySource"] = RoadGeometrySource;
                root["roadHighwayFilter"] = RoadHighwayFilter;
                root["pathHighwayFilter"] = PathHighwayFilter;
                root["minimumRoadDebugTier"] = MinimumRoadDebugTier;

                root["zoningDebugFilterZone"] = ZoningDebugFilterZone;
                root["zoningDebugFilterCs2Contains"] = ZoningDebugFilterCs2Contains;
                root["zoningDebugFilterMaterialKey"] = ZoningDebugFilterMaterialKey;

                root["zoningAlpha"] = ZoningAlpha;
                root["roadAlpha"] = RoadAlpha;
                root["pathAlpha"] = PathAlpha;
                root["waterLineAlpha"] = WaterLineAlpha;
                root["waterAreaOutlineAlpha"] = WaterAreaOutlineAlpha;
                root["waterAreaFillAlpha"] = WaterAreaFillAlpha;
                root["mapBoundsAlpha"] = MapBoundsAlpha;
                root["worldMapBoundsAlpha"] = WorldMapBoundsAlpha;
                root["heightMapBoundsAlpha"] = HeightMapBoundsAlpha;
                root["mapCenterAlpha"] = MapCenterAlpha;

                root["groundMargin"] = GroundMargin;
                root["zoningFillYOffset"] = ZoningFillYOffset;
                root["roadYOffset"] = RoadYOffset;
                root["pathYOffset"] = PathYOffset;
                root["waterLineYOffset"] = WaterLineYOffset;
                root["waterAreaOutlineYOffset"] = WaterAreaOutlineYOffset;
                root["waterAreaFillYOffset"] = WaterAreaFillYOffset;
                root["mapBoundsYOffset"] = MapBoundsYOffset;

                root["maxZoningFillMeshesDebug"] = MaxZoningFillMeshesDebug;
                root["maxRoadSegmentsDebug"] = MaxRoadSegmentsDebug;
                root["maxPathSegmentsDebug"] = MaxPathSegmentsDebug;
                root["maxWaterSegmentsDebug"] = MaxWaterSegmentsDebug;
                root["maxWaterAreaFillMeshesDebug"] = MaxWaterAreaFillMeshesDebug;
                root["maxRenderedSegments"] = MaxRenderedSegments;
                root["pointStride"] = PointStride;

                root["renderEverything"] = RenderEverything;
                root["renderAllRoadSegments"] = RenderAllRoadSegments;
                root["renderAllPathSegments"] = RenderAllPathSegments;
                root["renderAllZoningPolygons"] = RenderAllZoningPolygons;
                root["renderAllWaterSegments"] = RenderAllWaterSegments;
                root["renderAllWaterAreaFills"] = RenderAllWaterAreaFills;

                root["useLaneWidthScaling"] = UseLaneWidthScaling;
                root["laneWidthScaleFactor"] = LaneWidthScaleFactor;
                root["highlightOneWayRoads"] = HighlightOneWayRoads;
                root["highlightBridges"] = HighlightBridges;
                root["highlightTunnels"] = HighlightTunnels;
                root["highlightRoundabouts"] = HighlightRoundabouts;
                root["roadRenderMode"] = "ribbon";
                root["pathRenderMode"] = "ribbon";
                root["ribbonYOffset"] = RibbonYOffset;
                root["enableRoadSpatialChunking"] = EnableRoadSpatialChunking;
                root["roadChunkSizeMeters"] = RoadChunkSizeMeters;
                root["enableProgressiveOverlayRebuild"] = EnableProgressiveOverlayRebuild;
                root["roadChunksPerFrame"] = RoadChunksPerFrame;
                root["pathChunksPerFrame"] = PathChunksPerFrame;

                root["renderRoadDirectionArrows"] = RenderRoadDirectionArrows;
                root["renderAllRoadArrows"] = RenderAllRoadArrows;
                root["roadArrowSpacingMeters"] = RoadArrowSpacingMeters;
                root["roadArrowSize"] = RoadArrowSize;
                root["roadArrowYOffset"] = RoadArrowYOffset;
                root["roadArrowMaxCount"] = RoadArrowMaxCount;

                root["renderRoadLabels"] = RenderRoadLabels;
                root["renderAllRoadLabels"] = RenderAllRoadLabels;
                root["roadLabelMaxCount"] = RoadLabelMaxCount;
                root["roadLabelMinTier"] = RoadLabelMinTier;
                root["roadLabelMaxDistance"] = RoadLabelMaxDistance;
                root["roadLabelYOffset"] = RoadLabelYOffset;
                root["roadLabelFontSize"] = RoadLabelFontSize;
                root["deduplicateRoadLabels"] = DeduplicateRoadLabels;
                root["roadSemanticFilterMode"] = RoadSemanticFilterMode;
                root["runtimeRoadImportEnabled"] = RuntimeRoadImportEnabled;
                root["runtimeRoadImportRunOnce"] = RuntimeRoadImportRunOnce;
                root["runtimeRoadImportMaxSegments"] = RuntimeRoadImportMaxSegments;
                root["runtimeRoadImportStride"] = RuntimeRoadImportStride;
                root["runtimeRoadImportMinSegmentLengthMeters"] = RuntimeRoadImportMinSegmentLengthMeters;
                root["runtimeRoadImportSnapToleranceMeters"] = RuntimeRoadImportSnapToleranceMeters;
                root["runtimeRoadImportYOffset"] = RuntimeRoadImportYOffset;
                root["runtimeRoadImportIncludeBridgeTunnel"] = RuntimeRoadImportIncludeBridgeTunnel;
                root["runtimeRoadImportSourceFilter"] = RuntimeRoadImportSourceFilter;
                root["runtimeRoadImportHighwayFilter"] = RuntimeRoadImportHighwayFilter;
                root["runtimeRoadImportStageFilter"] = RuntimeRoadImportStageFilter;
                root["runtimeRoadImportShowProgressInHud"] = RuntimeRoadImportShowProgressInHud;
                root["runtimeRoadImportVerboseSelectionLogs"] = RuntimeRoadImportVerboseSelectionLogs;
                root["runtimeRoadImportSelectionMode"] = RuntimeRoadImportSelectionMode;
                root["runtimeRoadImportDistanceBucketMeters"] = RuntimeRoadImportDistanceBucketMeters;
                root["runtimeRoadImportPriorityWeight"] = RuntimeRoadImportPriorityWeight;
                root["runtimeRoadImportSkipParkingAisles"] = RuntimeRoadImportSkipParkingAisles;
                root["runtimeRoadImportSkipClearlyUnpaved"] = RuntimeRoadImportSkipClearlyUnpaved;
                root["runtimeRoadImportBatchSize"] = RuntimeRoadImportBatchSize;
                root["runtimeRoadImportPipelineMode"] = RuntimeRoadImportPipelineMode;

                File.WriteAllText(
                    ConfigPath,
                    root.ToString(),
                    new System.Text.UTF8Encoding(false)
                );

                Log.Info("GeoOverlayConfig: visual settings saved.");
            }
            catch (Exception ex)
            {
                Log.Error("GeoOverlayConfig: failed to save visual settings. " + ex);
            }
        }

        internal void LoadVisualSettingsFromConfig()
        {
            if (string.IsNullOrWhiteSpace(ConfigPath))
            {
                Log.Error("GeoOverlayConfig: cannot load visual settings. ConfigPath is empty.");
                return;
            }

            if (!File.Exists(ConfigPath))
            {
                Log.Error("GeoOverlayConfig: cannot load visual settings. File not found: " + ConfigPath);
                return;
            }

            try
            {
                var json = File.ReadAllText(ConfigPath);
                var root = JObject.Parse(json);

                RenderWaterLines = GetBool(root, "renderWaterLines", RenderWaterLines);
                RenderWaterAreas = GetBool(root, "renderWaterAreas", RenderWaterAreas);
                RenderWaterAreaOutlines = GetBool(root, "renderWaterAreaOutlines", RenderWaterAreaOutlines);
                RenderWaterAreaFillMeshes = GetBool(root, "renderWaterAreaFillMeshes", RenderWaterAreaFillMeshes);

                RenderRoads = GetBool(root, "renderRoads", RenderRoads);
                RenderPaths = GetBool(root, "renderPaths", RenderPaths);
                RenderZoning = GetBool(root, "renderZoning", RenderZoning);
                RenderMapBounds = GetBool(root, "renderMapBounds", RenderMapBounds);
                VerboseOverlayLogs = GetBool(root, "verboseOverlayLogs", VerboseOverlayLogs);

                RoadGeometrySource = GetString(root, "roadGeometrySource", RoadGeometrySource);
                RoadHighwayFilter = GetString(root, "roadHighwayFilter", RoadHighwayFilter);
                PathHighwayFilter = GetString(root, "pathHighwayFilter", PathHighwayFilter);
                MinimumRoadDebugTier = GetInt(root, "minimumRoadDebugTier", MinimumRoadDebugTier);

                ZoningDebugFilterZone = GetString(root, "zoningDebugFilterZone", ZoningDebugFilterZone);
                ZoningDebugFilterCs2Contains = GetString(root, "zoningDebugFilterCs2Contains", ZoningDebugFilterCs2Contains);
                ZoningDebugFilterMaterialKey = GetString(root, "zoningDebugFilterMaterialKey", ZoningDebugFilterMaterialKey);

                ZoningAlpha = GetFloat(root, "zoningAlpha", ZoningAlpha);
                RoadAlpha = GetFloat(root, "roadAlpha", RoadAlpha);
                PathAlpha = GetFloat(root, "pathAlpha", PathAlpha);
                WaterLineAlpha = GetFloat(root, "waterLineAlpha", WaterLineAlpha);
                WaterAreaOutlineAlpha = GetFloat(root, "waterAreaOutlineAlpha", WaterAreaOutlineAlpha);
                WaterAreaFillAlpha = GetFloat(root, "waterAreaFillAlpha", WaterAreaFillAlpha);
                MapBoundsAlpha = GetFloat(root, "mapBoundsAlpha", MapBoundsAlpha);

                GroundMargin = GetFloat(root, "groundMargin", GroundMargin);
                ZoningFillYOffset = GetFloat(root, "zoningFillYOffset", ZoningFillYOffset);
                RoadYOffset = GetFloat(root, "roadYOffset", RoadYOffset);
                PathYOffset = GetFloat(root, "pathYOffset", PathYOffset);
                WaterLineYOffset = GetFloat(root, "waterLineYOffset", WaterLineYOffset);
                WaterAreaOutlineYOffset = GetFloat(root, "waterAreaOutlineYOffset", WaterAreaOutlineYOffset);
                WaterAreaFillYOffset = GetFloat(root, "waterAreaFillYOffset", WaterAreaFillYOffset);
                MapBoundsYOffset = GetFloat(root, "mapBoundsYOffset", MapBoundsYOffset);

                MaxZoningFillMeshesDebug = GetInt(root, "maxZoningFillMeshesDebug", MaxZoningFillMeshesDebug);
                MaxRoadSegmentsDebug = GetInt(root, "maxRoadSegmentsDebug", MaxRoadSegmentsDebug);
                MaxPathSegmentsDebug = root["maxPathSegmentsDebug"] != null
                    ? GetInt(root, "maxPathSegmentsDebug", MaxPathSegmentsDebug)
                    : MaxRoadSegmentsDebug;
                MaxWaterSegmentsDebug = GetInt(root, "maxWaterSegmentsDebug", MaxWaterSegmentsDebug);
                MaxWaterAreaFillMeshesDebug = GetInt(root, "maxWaterAreaFillMeshesDebug", MaxWaterAreaFillMeshesDebug);
                MaxRenderedSegments = GetInt(root, "maxRenderedSegments", MaxRenderedSegments);
                PointStride = GetInt(root, "pointStride", PointStride);

                RenderEverything = GetBool(root, "renderEverything", RenderEverything);
                RenderAllRoadSegments = GetBool(root, "renderAllRoadSegments", RenderAllRoadSegments);
                RenderAllPathSegments = GetBool(root, "renderAllPathSegments", RenderAllPathSegments);
                RenderAllZoningPolygons = GetBool(root, "renderAllZoningPolygons", RenderAllZoningPolygons);
                RenderAllWaterSegments = GetBool(root, "renderAllWaterSegments", RenderAllWaterSegments);
                RenderAllWaterAreaFills = GetBool(root, "renderAllWaterAreaFills", RenderAllWaterAreaFills);

                UseLaneWidthScaling = GetBool(root, "useLaneWidthScaling", UseLaneWidthScaling);
                LaneWidthScaleFactor = GetFloat(root, "laneWidthScaleFactor", LaneWidthScaleFactor);
                HighlightOneWayRoads = GetBool(root, "highlightOneWayRoads", HighlightOneWayRoads);
                HighlightBridges = GetBool(root, "highlightBridges", HighlightBridges);
                HighlightTunnels = GetBool(root, "highlightTunnels", HighlightTunnels);
                HighlightRoundabouts = GetBool(root, "highlightRoundabouts", HighlightRoundabouts);
                RoadRenderMode = NormalizeRoadRenderMode(GetString(root, "roadRenderMode", RoadRenderMode));
                PathRenderMode = NormalizeRoadRenderMode(GetString(root, "pathRenderMode", PathRenderMode));
                RibbonYOffset = GetFloat(root, "ribbonYOffset", RibbonYOffset);
                EnableRoadSpatialChunking = GetBool(root, "enableRoadSpatialChunking", EnableRoadSpatialChunking);
                RoadChunkSizeMeters = GetFloat(root, "roadChunkSizeMeters", RoadChunkSizeMeters);
                EnableProgressiveOverlayRebuild = GetBool(root, "enableProgressiveOverlayRebuild", EnableProgressiveOverlayRebuild);
                RoadChunksPerFrame = GetInt(root, "roadChunksPerFrame", RoadChunksPerFrame);
                PathChunksPerFrame = GetInt(root, "pathChunksPerFrame", PathChunksPerFrame);

                RenderRoadDirectionArrows = GetBool(root, "renderRoadDirectionArrows", RenderRoadDirectionArrows);
                RenderAllRoadArrows = GetBool(root, "renderAllRoadArrows", RenderAllRoadArrows);
                RoadArrowSpacingMeters = GetFloat(root, "roadArrowSpacingMeters", RoadArrowSpacingMeters);
                RoadArrowSize = GetFloat(root, "roadArrowSize", RoadArrowSize);
                RoadArrowYOffset = GetFloat(root, "roadArrowYOffset", RoadArrowYOffset);
                RoadArrowMaxCount = GetInt(root, "roadArrowMaxCount", RoadArrowMaxCount);

                RenderRoadLabels = GetBool(root, "renderRoadLabels", RenderRoadLabels);
                RenderAllRoadLabels = GetBool(root, "renderAllRoadLabels", RenderAllRoadLabels);
                RoadLabelMaxCount = GetInt(root, "roadLabelMaxCount", RoadLabelMaxCount);
                RoadLabelMinTier = GetInt(root, "roadLabelMinTier", RoadLabelMinTier);
                RoadLabelMaxDistance = GetFloat(root, "roadLabelMaxDistance", RoadLabelMaxDistance);
                RoadLabelYOffset = GetFloat(root, "roadLabelYOffset", RoadLabelYOffset);
                RoadLabelFontSize = GetInt(root, "roadLabelFontSize", RoadLabelFontSize);
                DeduplicateRoadLabels = GetBool(root, "deduplicateRoadLabels", DeduplicateRoadLabels);
                RoadSemanticFilterMode = NormalizeRoadSemanticFilterMode(GetString(root, "roadSemanticFilterMode", RoadSemanticFilterMode));
                RuntimeRoadImportEnabled = GetBool(root, "runtimeRoadImportEnabled", RuntimeRoadImportEnabled);
                RuntimeRoadImportRunOnce = GetBool(root, "runtimeRoadImportRunOnce", RuntimeRoadImportRunOnce);
                RuntimeRoadImportMaxSegments = GetInt(root, "runtimeRoadImportMaxSegments", RuntimeRoadImportMaxSegments);
                RuntimeRoadImportStride = GetInt(root, "runtimeRoadImportStride", RuntimeRoadImportStride);
                RuntimeRoadImportMinSegmentLengthMeters = GetFloat(root, "runtimeRoadImportMinSegmentLengthMeters", RuntimeRoadImportMinSegmentLengthMeters);
                RuntimeRoadImportSnapToleranceMeters = GetFloat(root, "runtimeRoadImportSnapToleranceMeters", RuntimeRoadImportSnapToleranceMeters);
                RuntimeRoadImportYOffset = GetFloat(root, "runtimeRoadImportYOffset", RuntimeRoadImportYOffset);
                RuntimeRoadImportIncludeBridgeTunnel = GetBool(root, "runtimeRoadImportIncludeBridgeTunnel", RuntimeRoadImportIncludeBridgeTunnel);
                RuntimeRoadImportSourceFilter = GetString(root, "runtimeRoadImportSourceFilter", RuntimeRoadImportSourceFilter);
                RuntimeRoadImportHighwayFilter = GetString(root, "runtimeRoadImportHighwayFilter", RuntimeRoadImportHighwayFilter);
                RuntimeRoadImportStageFilter = GetString(root, "runtimeRoadImportStageFilter", RuntimeRoadImportStageFilter);
                RuntimeRoadImportShowProgressInHud = GetBool(root, "runtimeRoadImportShowProgressInHud", RuntimeRoadImportShowProgressInHud);
                RuntimeRoadImportVerboseSelectionLogs = GetBool(root, "runtimeRoadImportVerboseSelectionLogs", RuntimeRoadImportVerboseSelectionLogs);
                RuntimeRoadImportSelectionMode = GetString(root, "runtimeRoadImportSelectionMode", RuntimeRoadImportSelectionMode);
                RuntimeRoadImportDistanceBucketMeters = GetFloat(root, "runtimeRoadImportDistanceBucketMeters", RuntimeRoadImportDistanceBucketMeters);
                RuntimeRoadImportPriorityWeight = GetFloat(root, "runtimeRoadImportPriorityWeight", RuntimeRoadImportPriorityWeight);
                RuntimeRoadImportSkipParkingAisles = GetBool(root, "runtimeRoadImportSkipParkingAisles", RuntimeRoadImportSkipParkingAisles);
                RuntimeRoadImportSkipClearlyUnpaved = GetBool(root, "runtimeRoadImportSkipClearlyUnpaved", RuntimeRoadImportSkipClearlyUnpaved);
                RuntimeRoadImportBatchSize = GetInt(root, "runtimeRoadImportBatchSize", RuntimeRoadImportBatchSize);
                RuntimeRoadImportPipelineMode = GetString(root, "runtimeRoadImportPipelineMode", RuntimeRoadImportPipelineMode);

                ClampRuntimeRoadImportSettings(this);
                ClampRuntimeVisualSettings();

                Log.Info("GeoOverlayConfig: visual settings loaded.");
            }
            catch (Exception ex)
            {
                Log.Error("GeoOverlayConfig: failed to load visual settings. " + ex);
            }
        }
    }
}
