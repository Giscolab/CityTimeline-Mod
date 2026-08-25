// CTM CONTRACT GUARD: Never disable the 57 km LargeMap + PlayableWorld contract unless explicitly requested.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
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
            string path = null;

            try
            {
                path = CityTimelineConfigStorage.ResolveWritableConfigPath(modDir);
                config.ConfigPath = path;

                JObject root;
                bool exists;
                string readError;
                if (!TryReadRuntimeConfigRoot(path, out root, out exists, out readError))
                    throw new IOException("Failed to read runtime config: " + readError);

                if (!exists)
                {
                    Log.Info("GeoOverlayConfig: config.json not found. Using defaults.");
                    return config;
                }

                config.UseGeoJsonCenter = GetBool(root, "useGeoJsonCenter", config.UseGeoJsonCenter);
                config.ModEnabled = GetBool(root, "modEnabled", config.ModEnabled);
                config.LargeMapEnabled = GetBool(root, "largeMapEnabled", config.LargeMapEnabled);
                config.PlayableWorldEnabled = GetBool(root, "playableWorldEnabled", config.PlayableWorldEnabled);
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
config.LoadRailwaySettings(root);
config.LoadServiceSettings(root);
config.LoadOverlaySublayerSettings(root);
config.RoadHighwayFilter = GetString(root, "roadHighwayFilter", config.RoadHighwayFilter);
config.PathHighwayFilter = GetString(root, "pathHighwayFilter", config.PathHighwayFilter);
config.OverlayProfile = GetString(root, "overlayProfile", config.OverlayProfile);
config.RenderZoning = GetBool(root, "renderZoning", config.RenderZoning);
config.ZoningResidentialVisible = GetBool(root, "zoningResidentialVisible", config.ZoningResidentialVisible);
config.ZoningCommercialVisible = GetBool(root, "zoningCommercialVisible", config.ZoningCommercialVisible);
config.ZoningIndustrialVisible = GetBool(root, "zoningIndustrialVisible", config.ZoningIndustrialVisible);
config.ZoningOfficeVisible = GetBool(root, "zoningOfficeVisible", config.ZoningOfficeVisible);
config.ParkingVisible = GetBool(root, "parkingVisible", config.ParkingVisible);
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
config.MaxPathSegmentsDebug = GetToken(root, "maxPathSegmentsDebug") != null
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
config.ClampRailwaySettings();
config.SanitizeServiceSettings();

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
    Log.Info("GeoOverlayConfig: modEnabled=false. Skipping overlay profile.");
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
                config = new GeoOverlayConfig
                {
                    ConfigPath = path,
                    ModEnabled = false,
                    LargeMapEnabled = false,
                    PlayableWorldEnabled = false,
                    IsReliable = false,
                    ReliabilityError = ex.ToString()
                };
                Log.Error("GeoOverlayConfig: failed to load config.json. Runtime overlay startup is blocked. " + ex);
            }

            return config;
        }

        internal bool PrepareBundle(string modDir)
        {
            if (!IsReliable || !ModEnabled)
            {
                Log.Info("GeoOverlayConfig: bundle preparation skipped because runtime config is disabled or unreliable.");
                return false;
            }

            try
            {
                var resolvedBundleManifestPath = BundleResolver.ResolveManifestPath(this, modDir);

                if (!string.IsNullOrWhiteSpace(resolvedBundleManifestPath))
                {
                    BundleManifestPath = resolvedBundleManifestPath;
                    // Never reuse a persisted packPath from another bundle. The
                    // validated manifest is the only authority for indexed bundles.
                    PackPath = null;
                    var applied = ApplyBundleManifest(this, modDir);
                    if (!applied)
                    {
                        BundleIndexResolutionSucceeded = false;
                        BundleIndexResolutionError = "Bundle manifest could not be applied.";
                    }

                    return applied;
                }

                if (UseBundleIndex)
                {
                    BundleManifestPath = null;
                    PackPath = null;
                    ActiveBundleRoot = null;
                    return false;
                }

                // Legacy mode may intentionally rely on packPath alone.
                return true;
            }
            catch (Exception ex)
            {
                BundleIndexResolutionSucceeded = false;
                BundleIndexResolutionError = ex.Message;
                Log.Error("GeoOverlayConfig: failed to prepare bundle. " + ex);
                return false;
            }
        }

        internal GeoOverlayConfig CloneForBundleReload()
        {
            return (GeoOverlayConfig)MemberwiseClone();
        }

        internal GeoOverlayConfig CreateBundleReloadBaseline()
        {
            if (!IsReliable)
            {
                throw new InvalidOperationException(
                    "A reliable runtime snapshot is required for the bundle fallback baseline."
                );
            }

            // Keep this baseline independent from the prepared active-bundle
            // state. Only values that may be replaced by index/manifest
            // metadata are captured. Lifecycle flags are deliberately absent:
            // candidates inherit them exclusively from Mod.RuntimeConfig.
            return new GeoOverlayConfig
            {
                IsReliable = true,
                ReliabilityError = null,
                UseGeoJsonCenter = UseGeoJsonCenter,
                OriginLon = OriginLon,
                OriginLat = OriginLat,
                WorldOriginX = WorldOriginX,
                WorldOriginZ = WorldOriginZ,
                WorldScale = WorldScale,
                OverlayRotationDegrees = OverlayRotationDegrees,
                OverlayScaleX = OverlayScaleX,
                OverlayScaleZ = OverlayScaleZ,
                FlipX = FlipX,
                FlipZ = FlipZ,
                WorldMapSizeKm = WorldMapSizeKm,
                HeightMapSizeKm = HeightMapSizeKm,
                WorldMapMinLon = WorldMapMinLon,
                WorldMapMinLat = WorldMapMinLat,
                WorldMapMaxLon = WorldMapMaxLon,
                WorldMapMaxLat = WorldMapMaxLat,
                HeightMapMinLon = HeightMapMinLon,
                HeightMapMinLat = HeightMapMinLat,
                HeightMapMaxLon = HeightMapMaxLon,
                HeightMapMaxLat = HeightMapMaxLat
            };
        }

        internal GeoOverlayConfig CloneForBundleReloadCandidate(GeoOverlayConfig strictFallbacks)
        {
            if (strictFallbacks == null || !strictFallbacks.IsReliable)
                throw new ArgumentException("A reliable strict config snapshot is required.", nameof(strictFallbacks));

            var candidate = (GeoOverlayConfig)MemberwiseClone();

            // Manifest values from the active bundle must never become fallbacks
            // for the next bundle. Restore only the config/default values that
            // the normal startup path would expose before applying an index and
            // manifest, while retaining unrelated live visual settings.
            candidate.UseGeoJsonCenter = strictFallbacks.UseGeoJsonCenter;
            candidate.OriginLon = strictFallbacks.OriginLon;
            candidate.OriginLat = strictFallbacks.OriginLat;
            candidate.WorldOriginX = strictFallbacks.WorldOriginX;
            candidate.WorldOriginZ = strictFallbacks.WorldOriginZ;
            candidate.WorldScale = strictFallbacks.WorldScale;
            candidate.OverlayRotationDegrees = strictFallbacks.OverlayRotationDegrees;
            candidate.OverlayScaleX = strictFallbacks.OverlayScaleX;
            candidate.OverlayScaleZ = strictFallbacks.OverlayScaleZ;
            candidate.FlipX = strictFallbacks.FlipX;
            candidate.FlipZ = strictFallbacks.FlipZ;

            candidate.BundleManifestPath = null;
            candidate.ResolvedBundlesRoot = null;
            candidate.ActiveBundleRoot = null;
            candidate.BundleIndexResolutionSucceeded = false;
            candidate.BundleIndexResolutionError = null;
            candidate.ActiveBundleId = "";
            candidate.ActiveBundleDisplayName = null;
            candidate.ActiveBundleCity = null;
            candidate.ActiveBundleCountry = null;
            candidate.ActiveBundleCenterLon = double.NaN;
            candidate.ActiveBundleCenterLat = double.NaN;
            candidate.RecommendedCs2WaterLevel = double.NaN;
            candidate.WaterReferenceElevationMeters = double.NaN;
            candidate.BelowSeaReserveMeters = double.NaN;
            candidate.WaterContractSource = null;
            candidate.WaterContractFormula = null;
            candidate.PackPath = null;

            candidate.WorldMapSizeKm = strictFallbacks.WorldMapSizeKm;
            candidate.HeightMapSizeKm = strictFallbacks.HeightMapSizeKm;
            candidate.WorldMapMinLon = strictFallbacks.WorldMapMinLon;
            candidate.WorldMapMinLat = strictFallbacks.WorldMapMinLat;
            candidate.WorldMapMaxLon = strictFallbacks.WorldMapMaxLon;
            candidate.WorldMapMaxLat = strictFallbacks.WorldMapMaxLat;
            candidate.HeightMapMinLon = strictFallbacks.HeightMapMinLon;
            candidate.HeightMapMinLat = strictFallbacks.HeightMapMinLat;
            candidate.HeightMapMaxLon = strictFallbacks.HeightMapMaxLon;
            candidate.HeightMapMaxLat = strictFallbacks.HeightMapMaxLat;

            return candidate;
        }

        internal void ApplyPreparedBundleStateFrom(GeoOverlayConfig source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            UseGeoJsonCenter = source.UseGeoJsonCenter;
            OriginLon = source.OriginLon;
            OriginLat = source.OriginLat;
            WorldOriginX = source.WorldOriginX;
            WorldOriginZ = source.WorldOriginZ;
            WorldScale = source.WorldScale;
            OverlayRotationDegrees = source.OverlayRotationDegrees;
            OverlayScaleX = source.OverlayScaleX;
            OverlayScaleZ = source.OverlayScaleZ;
            FlipX = source.FlipX;
            FlipZ = source.FlipZ;

            BundleManifestPath = source.BundleManifestPath;
            UseBundleIndex = source.UseBundleIndex;
            ActiveBundleId = source.ActiveBundleId;
            ResolvedBundlesRoot = source.ResolvedBundlesRoot;
            ActiveBundleRoot = source.ActiveBundleRoot;
            BundleIndexResolutionSucceeded = source.BundleIndexResolutionSucceeded;
            BundleIndexResolutionError = source.BundleIndexResolutionError;
            ActiveBundleDisplayName = source.ActiveBundleDisplayName;
            ActiveBundleCity = source.ActiveBundleCity;
            ActiveBundleCountry = source.ActiveBundleCountry;
            ActiveBundleCenterLon = source.ActiveBundleCenterLon;
            ActiveBundleCenterLat = source.ActiveBundleCenterLat;
            RecommendedCs2WaterLevel = source.RecommendedCs2WaterLevel;
            WaterReferenceElevationMeters = source.WaterReferenceElevationMeters;
            BelowSeaReserveMeters = source.BelowSeaReserveMeters;
            WaterContractSource = source.WaterContractSource;
            WaterContractFormula = source.WaterContractFormula;
            PackPath = source.PackPath;

            WorldMapSizeKm = source.WorldMapSizeKm;
            HeightMapSizeKm = source.HeightMapSizeKm;
            WorldMapMinLon = source.WorldMapMinLon;
            WorldMapMinLat = source.WorldMapMinLat;
            WorldMapMaxLon = source.WorldMapMaxLon;
            WorldMapMaxLat = source.WorldMapMaxLat;
            HeightMapMinLon = source.HeightMapMinLon;
            HeightMapMinLat = source.HeightMapMinLat;
            HeightMapMaxLon = source.HeightMapMaxLon;
            HeightMapMaxLat = source.HeightMapMaxLat;
        }

        internal bool TryReloadVisualSettingsFromConfig(
            out string error,
            out GeoOverlayConfig refreshedBundleBaseline)
        {
            error = null;
            refreshedBundleBaseline = null;

            if (string.IsNullOrWhiteSpace(ConfigPath))
            {
                error = "ConfigPath is empty.";
                return false;
            }

            var loaded = Load(Path.GetDirectoryName(ConfigPath));
            if (loaded == null || !loaded.IsReliable)
            {
                error = loaded == null
                    ? "The strict config loader returned no snapshot."
                    : loaded.ReliabilityError;
                return false;
            }

            if (loaded.ModEnabled != ModEnabled ||
                loaded.LargeMapEnabled != LargeMapEnabled ||
                loaded.PlayableWorldEnabled != PlayableWorldEnabled)
            {
                Log.Info(
                    "GeoOverlayConfig: lifecycle option changes found during visual reload; " +
                    "modEnabled, largeMapEnabled and playableWorldEnabled remain restart-scoped."
                );
            }

            CopyReloadableVisualFields(loaded);
            refreshedBundleBaseline = loaded.CreateBundleReloadBaseline();
            Log.Info("GeoOverlayConfig: visual settings reloaded through the strict runtime loader.");
            return true;
        }

        private void CopyReloadableVisualFields(GeoOverlayConfig source)
        {
            var blocked = new HashSet<string>(StringComparer.Ordinal)
            {
                nameof(ModEnabled),
                nameof(LargeMapEnabled),
                nameof(PlayableWorldEnabled),
                nameof(IsReliable),
                nameof(ReliabilityError),
                nameof(ConfigPath),
                nameof(BundleManifestPath),
                nameof(UseBundleIndex),
                nameof(BundlesRoot),
                nameof(ActiveBundleId),
                nameof(ResolvedBundlesRoot),
                nameof(ActiveBundleRoot),
                nameof(BundleIndexResolutionSucceeded),
                nameof(BundleIndexResolutionError),
                nameof(ActiveBundleDisplayName),
                nameof(ActiveBundleCity),
                nameof(ActiveBundleCountry),
                nameof(ActiveBundleCenterLon),
                nameof(ActiveBundleCenterLat),
                nameof(RecommendedCs2WaterLevel),
                nameof(WaterReferenceElevationMeters),
                nameof(BelowSeaReserveMeters),
                nameof(WaterContractSource),
                nameof(WaterContractFormula),
                nameof(PackPath),
                nameof(ShowOverlayHud),
                nameof(OverlaySublayerVisibility)
            };

            var fields = typeof(GeoOverlayConfig).GetFields(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            );

            foreach (var field in fields)
            {
                if (field.IsInitOnly || blocked.Contains(field.Name) ||
                    field.Name.StartsWith("RuntimeRoadImport", StringComparison.Ordinal))
                {
                    continue;
                }

                field.SetValue(this, field.GetValue(source));
            }

            OverlaySublayerVisibility.Clear();
            foreach (var pair in source.OverlaySublayerVisibility)
                OverlaySublayerVisibility[pair.Key] = pair.Value;

            ClampRuntimeVisualSettings();
            ClampRailwaySettings();
            SanitizeServiceSettings();
        }

        private static void ClampRuntimeRoadImportSettings(GeoOverlayConfig config)
        {
            if (config == null)
                return;

            // GeoJSON is a visual/informational overlay only. Persisted legacy
            // settings must never reactivate the former network importer.
            config.RuntimeRoadImportEnabled = false;
            config.RuntimeRoadImportRunOnce = false;

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
            config.RenderRailways = false;
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
            config.RenderAllRailwaySegments = false;
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

            JObject ignoredRoot;
            string updateError;
            if (!TryUpdateRuntimeConfigFile(
                ConfigPath,
                root =>
                {
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

                },
                out ignoredRoot,
                out updateError
            ))
            {
                Log.Error("GeoOverlayConfig: failed to save calibration. " + updateError);
                return;
            }

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

        internal bool SaveVisualSettingsToConfig()
        {
            if (string.IsNullOrWhiteSpace(ConfigPath))
            {
                Log.Error("GeoOverlayConfig: cannot save visual settings. ConfigPath is empty.");
                return false;
            }

            JObject ignoredRoot;
            string updateError;
            if (!TryUpdateRuntimeConfigFile(
                ConfigPath,
                root =>
                {
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
                root["renderWaterAreaOutlines"] = RenderWaterAreaOutlines;
                root["renderWaterAreaFillMeshes"] = RenderWaterAreaFillMeshes;

                root["renderRoads"] = RenderRoads;
                root["renderPaths"] = RenderPaths;
                WriteRailwaySettings(root);
                SanitizeServiceSettings();
                WriteServiceSettings(root);
                WriteOverlaySublayerSettings(root);
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

                },
                out ignoredRoot,
                out updateError
            ))
            {
                Log.Error("GeoOverlayConfig: failed to save visual settings. " + updateError);
                return false;
            }

            Log.Info("GeoOverlayConfig: visual settings saved.");
            return true;
        }

    }
}
