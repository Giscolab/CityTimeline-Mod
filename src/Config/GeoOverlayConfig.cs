using System;
using System.Globalization;
using System.IO;
using Newtonsoft.Json.Linq;
using CityTimelineMod.Util;

namespace CityTimelineMod.Config
{
    internal sealed class GeoOverlayConfig
    {
        internal bool UseGeoJsonCenter = true;
        internal bool ModEnabled = true;
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
                internal bool ShowOverlayHud = true;
                internal bool VerboseOverlayLogs = false;
                internal bool RenderMapBounds = true;
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
                internal float MapBoundsYOffset = 0.75f;
                internal int MaxZoningFillMeshesDebug = 5000;
                internal float ZoningFillYOffset = 0.05f;
                internal float RoadYOffset = 0.25f;
                internal float PathYOffset = 0.28f;
                internal float WaterLineYOffset = 0.35f;
                internal float WaterAreaOutlineYOffset = 0.30f;
                internal float ZoningAlpha = 0.28f;
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
            internal int MinimumRoadDebugTier = 0;

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
config.ShowOverlayHud = GetBool(root, "showOverlayHud", config.ShowOverlayHud);
config.VerboseOverlayLogs = GetBool(root, "verboseOverlayLogs", config.VerboseOverlayLogs);
config.RenderMapBounds = GetBool(root, "renderMapBounds", config.RenderMapBounds);
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
config.MapBoundsYOffset = GetFloat(root, "mapBoundsYOffset", config.MapBoundsYOffset);
config.MaxZoningFillMeshesDebug = GetInt(root, "maxZoningFillMeshesDebug", config.MaxZoningFillMeshesDebug);
config.ZoningFillYOffset = GetFloat(root, "zoningFillYOffset", config.ZoningFillYOffset);
config.RoadYOffset = GetFloat(root, "roadYOffset", config.RoadYOffset);
config.PathYOffset = GetFloat(root, "pathYOffset", config.PathYOffset);
config.WaterLineYOffset = GetFloat(root, "waterLineYOffset", config.WaterLineYOffset);
config.WaterAreaOutlineYOffset = GetFloat(root, "waterAreaOutlineYOffset", config.WaterAreaOutlineYOffset);
config.ZoningAlpha = GetFloat(root, "zoningAlpha", config.ZoningAlpha);
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
config.MinimumRoadDebugTier = GetInt(root, "minimumRoadDebugTier", config.MinimumRoadDebugTier);

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

if (config.MapBoundsYOffset < 0f)
    config.MapBoundsYOffset = 0f;

if (config.WorldMapSizeKm <= 0f)
    config.WorldMapSizeKm = 57.344f;

if (config.HeightMapSizeKm <= 0f)
    config.HeightMapSizeKm = 19.115f;

if (config.MaxRoadSegmentsDebug < 1)
    config.MaxRoadSegmentsDebug = 1;

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

if (!config.ModEnabled)
{
    Log.Info("GeoOverlayConfig: modEnabled=false. Skipping overlay profile and bundle manifest.");
    Log.Info("GeoOverlayConfig: loaded " + path);
    return config;
}

ApplyOverlayProfile(config);

                if (!string.IsNullOrWhiteSpace(config.BundleManifestPath))
                    ApplyBundleManifest(config, modDir);

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
                root["maxWaterSegmentsDebug"] = MaxWaterSegmentsDebug;
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
                root["maxWaterSegmentsDebug"] = MaxWaterSegmentsDebug;
                root["maxWaterAreaFillMeshesDebug"] = MaxWaterAreaFillMeshesDebug;
                root["maxRenderedSegments"] = MaxRenderedSegments;
                root["pointStride"] = PointStride;

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
                MaxWaterSegmentsDebug = GetInt(root, "maxWaterSegmentsDebug", MaxWaterSegmentsDebug);
                MaxWaterAreaFillMeshesDebug = GetInt(root, "maxWaterAreaFillMeshesDebug", MaxWaterAreaFillMeshesDebug);
                MaxRenderedSegments = GetInt(root, "maxRenderedSegments", MaxRenderedSegments);
                PointStride = GetInt(root, "pointStride", PointStride);

                Log.Info("GeoOverlayConfig: visual settings loaded.");
            }
            catch (Exception ex)
            {
                Log.Error("GeoOverlayConfig: failed to load visual settings. " + ex);
            }
        }

        private static void ApplyBundleManifest(GeoOverlayConfig config, string modDir)
        {
            try
            {
                var manifestPath = ResolvePath(config.BundleManifestPath, modDir);
                config.BundleManifestPath = manifestPath;

                if (!File.Exists(manifestPath))
                {
                    Log.Error("GeoOverlayConfig: bundle manifest not found: " + manifestPath);
                    return;
                }

                var manifestJson = File.ReadAllText(manifestPath);
                var manifest = JObject.Parse(manifestJson);
                var bundleRoot = Path.GetDirectoryName(manifestPath);

                var timeline = manifest["timelineMod"] as JObject;
                if (timeline != null)
                {
                    config.UseGeoJsonCenter = GetBool(timeline, "useGeoJsonCenter", config.UseGeoJsonCenter);
                    config.OriginLon = GetDouble(timeline, "originLon", config.OriginLon);
                    config.OriginLat = GetDouble(timeline, "originLat", config.OriginLat);
                    config.WorldOriginX = GetFloat(timeline, "worldOriginX", config.WorldOriginX);
                    config.WorldOriginZ = GetFloat(timeline, "worldOriginZ", config.WorldOriginZ);
                    config.WorldScale = GetFloat(timeline, "worldScale", config.WorldScale);
                    config.OverlayRotationDegrees = GetFloat(timeline, "overlayRotationDegrees", config.OverlayRotationDegrees);
                    config.OverlayScaleX = GetFloat(timeline, "overlayScaleX", config.OverlayScaleX);
                    config.OverlayScaleZ = GetFloat(timeline, "overlayScaleZ", config.OverlayScaleZ);
                    config.FlipX = GetBool(timeline, "flipX", config.FlipX);
                    config.FlipZ = GetBool(timeline, "flipZ", config.FlipZ);
                }

                var worldMap = manifest["worldMap"] as JObject;
                if (worldMap != null)
                {
                    config.WorldMapSizeKm = GetFloat(worldMap, "sizeKm", config.WorldMapSizeKm);
                    ApplyBbox(worldMap, true, config);
                }

                var heightmap = manifest["heightmap"] as JObject;
                if (heightmap != null)
                {
                    config.HeightMapSizeKm = GetFloat(heightmap, "sizeKm", config.HeightMapSizeKm);
                    ApplyBbox(heightmap, false, config);
                }

                var derivedGeojsonPack = string.IsNullOrWhiteSpace(bundleRoot)
                    ? null
                    : Path.Combine(bundleRoot, "geojson_pack");

                if (!string.IsNullOrWhiteSpace(derivedGeojsonPack) && Directory.Exists(derivedGeojsonPack))
                {
                    config.PackPath = derivedGeojsonPack;
                }
                else
                {
                    var manifestGeojsonDir = GetManifestString(manifest, "paths.geojsonDir");

                    if (string.IsNullOrWhiteSpace(manifestGeojsonDir))
                        manifestGeojsonDir = GetManifestString(manifest, "exportBundle.geojsonDir");

                    if (!string.IsNullOrWhiteSpace(manifestGeojsonDir))
                    {
                        var candidate = ResolvePath(manifestGeojsonDir, bundleRoot);

                        if (Directory.Exists(candidate))
                            config.PackPath = candidate;
                        else
                            Log.Error("GeoOverlayConfig: manifest geojsonDir not found: " + candidate);
                    }
                }

                Log.Info("GeoOverlayConfig: bundle manifest loaded " + manifestPath);
                Log.Info(
                    "GeoOverlayConfig: manifest applied originLon=" + config.OriginLon +
                    ", originLat=" + config.OriginLat +
                    ", worldScale=" + config.WorldScale +
                    ", packPath=" + (string.IsNullOrWhiteSpace(config.PackPath) ? "(empty)" : config.PackPath)
                );
            }
            catch (Exception ex)
            {
                Log.Error("GeoOverlayConfig: failed to apply bundle manifest. " + ex);
            }
        }

        private static void ApplyBbox(JObject section, bool isWorldMap, GeoOverlayConfig config)
        {
            var bbox = GetString(section, "bbox", null);
            if (string.IsNullOrWhiteSpace(bbox))
                return;

            var parts = bbox.Split(',');
            if (parts.Length != 4)
            {
                Log.Error("GeoOverlayConfig: invalid bbox in manifest: " + bbox);
                return;
            }

            double south;
            double west;
            double north;
            double east;

            if (
                !double.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out south) ||
                !double.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out west) ||
                !double.TryParse(parts[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out north) ||
                !double.TryParse(parts[3].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out east)
            )
            {
                Log.Error("GeoOverlayConfig: failed to parse bbox in manifest: " + bbox);
                return;
            }

            if (isWorldMap)
            {
                config.WorldMapMinLat = south;
                config.WorldMapMinLon = west;
                config.WorldMapMaxLat = north;
                config.WorldMapMaxLon = east;
            }
            else
            {
                config.HeightMapMinLat = south;
                config.HeightMapMinLon = west;
                config.HeightMapMaxLat = north;
                config.HeightMapMaxLon = east;
            }
        }

        private static string GetManifestString(JObject root, string path)
        {
            if (root == null || string.IsNullOrWhiteSpace(path))
                return null;

            var token = root.SelectToken(path);
            if (token == null)
                return null;

            var value = token.ToString();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        private static string ResolvePath(string path, string baseDir)
        {
            if (string.IsNullOrWhiteSpace(path))
                return path;

            var expanded = Environment.ExpandEnvironmentVariables(path);

            if (Path.IsPathRooted(expanded))
                return Path.GetFullPath(expanded);

            if (!string.IsNullOrWhiteSpace(baseDir))
                return Path.GetFullPath(Path.Combine(baseDir, expanded));

            return Path.GetFullPath(expanded);
        }



        private static void ApplyOverlayProfile(GeoOverlayConfig config)
        {
            if (config == null)
                return;

            var profile = string.IsNullOrWhiteSpace(config.OverlayProfile)
                ? "custom"
                : config.OverlayProfile.Trim().ToLowerInvariant();

            if (profile == "custom")
                return;

            if (profile == "major")
            {
                config.RenderWaterLines = false;
                config.RenderWaterAreas = false;
                config.RenderRoads = true;
                config.RoadGeometrySource = "major";
                config.RenderPaths = false;
                config.RenderZoning = false;
                return;
            }

            if (profile == "driveable")
            {
                config.RenderWaterLines = false;
                config.RenderWaterAreas = false;
                config.RenderRoads = true;
                config.RoadGeometrySource = "driveable";
                config.RenderPaths = true;
                config.RenderZoning = false;
                return;
            }

            if (profile == "zoning")
            {
                config.RenderWaterLines = false;
                config.RenderWaterAreas = false;
                config.RenderRoads = false;
                config.RenderPaths = false;
                config.RenderZoning = true;
                return;
            }

            if (profile == "water")
            {
                config.RenderWaterLines = true;
                config.RenderWaterAreas = true;
                config.RenderRoads = false;
                config.RenderPaths = false;
                config.RenderZoning = false;
                return;
            }

            if (profile == "full")
            {
                config.RenderWaterLines = true;
                config.RenderWaterAreas = true;
                config.RenderRoads = true;
                config.RoadGeometrySource = "driveable";
                config.RenderPaths = true;
                config.RenderZoning = true;
                return;
            }

            if (profile == "off")
            {
                config.RenderWaterLines = false;
                config.RenderWaterAreas = false;
                config.RenderRoads = false;
                config.RenderPaths = false;
                config.RenderZoning = false;
                return;
            }

            Log.Error("GeoOverlayConfig: invalid overlayProfile=" + profile + ". Fallback to custom.");
            config.OverlayProfile = "custom";
        }

        private static float Clamp01(float value)
        {
            if (value < 0f)
                return 0f;

            if (value > 1f)
                return 1f;

            return value;
        }

        private static string GetString(JObject root, string name, string fallback)
        {
            var token = root[name];
            if (token == null) return fallback;

            var value = token.ToString();
            if (string.IsNullOrWhiteSpace(value))
                return fallback;

            return value;
        }

        private static bool GetBool(JObject root, string name, bool fallback)
        {
            var token = root[name];
            if (token == null) return fallback;

            bool value;
            if (bool.TryParse(token.ToString(), out value))
                return value;

            return fallback;
        }

        private static float GetFloat(JObject root, string name, float fallback)
        {
            var token = root[name];
            if (token == null) return fallback;

            float value;
            if (float.TryParse(token.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                return value;

            return fallback;
        }

        private static double GetDouble(JObject root, string name, double fallback)
        {
            var token = root[name];
            if (token == null) return fallback;

            double value;
            if (double.TryParse(token.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                return value;

            return fallback;
        }

        // --- Nouveau helper ---
        private static int GetInt(JObject root, string name, int fallback)
        {
            var token = root[name];
            if (token == null) return fallback;

            int value;
            if (int.TryParse(token.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                return value;

            return fallback;
        }
    }
}
