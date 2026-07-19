using System;
using System.Collections.Generic;
using CityTimelineMod.Config;
using CityTimelineMod.Bundles;
using CityTimelineMod.Geometry;
using CityTimelineMod.Importers;
using CityTimelineMod.Rendering.Batching;
using CityTimelineMod.Rendering.Bounds;
using CityTimelineMod.Rendering.Core;
using CityTimelineMod.Rendering.Materials;
using CityTimelineMod.Rendering.Roads;
using CityTimelineMod.Rendering.Railways;
using CityTimelineMod.Rendering.Water;
using CityTimelineMod.Rendering.Zoning;
using CityTimelineMod.Terrain;
using CityTimelineMod.Util;
using UnityEngine;

namespace CityTimelineMod.Rendering
{
    internal static class GeoDebugOverlay
    {
        private const string RootName = "CityTimelineMod_GroundOverlay";

        internal static void Install(List<List<GeoPoint>> lines, GeoOverlayConfig config)
        {
            Install(lines, new List<List<GeoPoint>>(), new List<List<GeoPoint>>(), config);
        }

        internal static void Install(
            List<List<GeoPoint>> waterLines,
            List<List<GeoPoint>> roadLines,
            GeoOverlayConfig config
        )
        {
            Install(waterLines, new List<List<GeoPoint>>(), roadLines, config);
        }

        internal static void Install(
            List<List<GeoPoint>> waterLines,
            List<List<GeoPoint>> waterAreaOutlines,
            List<List<GeoPoint>> roadLines,
            GeoOverlayConfig config
        )
        {
            Install(waterLines, waterAreaOutlines, ConvertRoadLines(roadLines), config);
        }

        internal static void Install(
            List<List<GeoPoint>> waterLines,
            List<List<GeoPoint>> waterAreaOutlines,
            List<GeoRoadLine> roadLines,
            GeoOverlayConfig config
        )
        {
            Install(waterLines, waterAreaOutlines, roadLines, new List<GeoZoningPolygon>(), config);
        }

        internal static void Install(
            List<List<GeoPoint>> waterLines,
            List<List<GeoPoint>> waterAreaOutlines,
            List<GeoRoadLine> roadLines,
            List<GeoZoningPolygon> zoningPolygons,
            GeoOverlayConfig config
        )
        {
            Install(
                waterLines,
                waterAreaOutlines,
                roadLines,
                zoningPolygons,
                new List<GeoRailwayLine>(),
                false,
                "Aucune donnée ferroviaire disponible dans ce bundle.",
                config
            );
        }

        internal static void Install(
            List<List<GeoPoint>> waterLines,
            List<List<GeoPoint>> waterAreaOutlines,
            List<GeoRoadLine> roadLines,
            List<GeoZoningPolygon> zoningPolygons,
            List<GeoRailwayLine> railwayLines,
            bool railwayAvailable,
            string railwayStatus,
            GeoOverlayConfig config
        )
        {
            var existing = GameObject.Find(RootName);
            if (existing != null)
                UnityEngine.Object.Destroy(existing);

            var root = new GameObject(RootName);
            UnityEngine.Object.DontDestroyOnLoad(root);

            var overlay = root.AddComponent<GroundOverlayBehaviour>();
            overlay.Setup(
                waterLines,
                waterAreaOutlines,
                roadLines,
                zoningPolygons,
                railwayLines,
                railwayAvailable,
                railwayStatus,
                config
            );

            Log.Info("GroundOverlay: installed HARD visible segment overlay.");
        }

        internal static void ApplyRuntimeConfigChange(string configKey)
        {
            var existing = GameObject.Find(RootName);
            if (existing == null)
                return;

            var overlay = existing.GetComponent<GroundOverlayBehaviour>();
            if (overlay == null)
                return;

            overlay.ApplyRuntimeConfigChange(configKey);
        }

        internal static RailwayHudSnapshot GetRailwayHudSnapshot()
        {
            var existing = GameObject.Find(RootName);
            if (existing == null)
                return RailwayHudSnapshot.Unavailable("Overlay ferroviaire non initialisé.");

            var overlay = existing.GetComponent<GroundOverlayBehaviour>();
            return overlay != null
                ? overlay.GetRailwayHudSnapshot()
                : RailwayHudSnapshot.Unavailable("Overlay ferroviaire non initialisé.");
        }

        internal static bool SetRailwayBoolean(string key, bool value)
        {
            var existing = GameObject.Find(RootName);
            var overlay = existing != null ? existing.GetComponent<GroundOverlayBehaviour>() : null;
            return overlay != null && overlay.SetRailwayBoolean(key, value);
        }

        internal static bool SetRailwayFloat(string key, float value)
        {
            var existing = GameObject.Find(RootName);
            var overlay = existing != null ? existing.GetComponent<GroundOverlayBehaviour>() : null;
            return overlay != null && overlay.SetRailwayFloat(key, value);
        }
        internal static List<GeoRoadLine> ConvertRoadLines(List<List<GeoPoint>> lines)
        {
            var result = new List<GeoRoadLine>();

            if (lines == null)
                return result;

            foreach (var line in lines)
            {
                if (line != null && line.Count >= 2)
                    result.Add(new GeoRoadLine(line, null, null));
            }

            return result;
        }
    }

    internal sealed partial class GroundOverlayBehaviour : MonoBehaviour
    {
        private enum CalibrationMode
        {
            Global,
            Water,
            Roads,
            Render
        }

        private List<List<GeoPoint>> _waterLines;
        private List<List<GeoPoint>> _waterAreaOutlines;
        private List<GeoRoadLine> _roadLines;
        private List<GeoZoningPolygon> _zoningPolygons;
        private GeoOverlayConfig _config;

        private bool VerboseOverlayLogs
        {
            get { return _config != null && _config.VerboseOverlayLogs; }
        }

        private void LogVerboseOverlay(string message)
        {
            if (VerboseOverlayLogs)
                Log.Info(message);
        }

        private GeoBounds _bounds;
        private bool _created;
        private CalibrationMode _activeCalibrationMode = CalibrationMode.Global;

        private readonly List<Material> _zoningMaterials = new List<Material>();
        private readonly List<Material> _roadMaterials = new List<Material>();
        private readonly List<Material> _pathMaterials = new List<Material>();
        private readonly List<Material> _waterLineMaterials = new List<Material>();
        private readonly List<Material> _waterAreaOutlineMaterials = new List<Material>();
        private readonly List<Material> _waterAreaFillMaterials = new List<Material>();
        private readonly List<Material> _mapBoundsMaterials = new List<Material>();
        private readonly List<TextMesh> _roadLabelMeshes = new List<TextMesh>();
        private RoadSemanticStats _roadSemanticStats = new RoadSemanticStats();

        private bool _zoningVisible = true;
        private bool _roadsVisible = true;
        private bool _waterVisible = true;

        private Rect _controlPanelRect = new Rect(24f, 24f, 620f, 760f);
        private Vector2 _controlPanelScroll = Vector2.zero;
        private bool _controlPanelLogOnce;
        private bool _controlPanelCalibrationUnlocked;
        private bool _controlPanelRebuildPending;
        private string _visualSettingsStatusMessage = "Réglages live.";
        private bool _visualSettingsDirty;
        private List<BundleCatalogEntry> _bundleCatalogEntries = new List<BundleCatalogEntry>();
        private int _selectedBundleIndex = -1;
        private string _bundleSelectorStatusMessage = "Catalogue bundles non chargé.";
        private string _pendingBundleReloadId = null;

        private int _debugWaterSegmentLogs;
        private int _debugRoadSegmentLogs;
        private Vector3 _liveCalibrationOffset = Vector3.zero;
        private float _liveCalibrationRotationDegrees = 0f;
        private float _liveCalibrationScaleX = 1f;
        private float _liveCalibrationScaleZ = 1f;
        private ProgressiveOverlayRebuildState _progressiveRebuild;
        private bool _rebuildRestartPending;
        private bool _rebuildCancelRequested;
        private string _pendingRebuildReason;
        private float _pendingRebuildRequestTime;
        private const float RebuildDebounceSeconds = 0.4f;

        internal void Setup(
            List<List<GeoPoint>> waterLines,
            List<List<GeoPoint>> roadLines,
            GeoOverlayConfig config
        )
        {
            Setup(waterLines, new List<List<GeoPoint>>(), GeoDebugOverlay.ConvertRoadLines(roadLines), config);
        }

        internal void Setup(
            List<List<GeoPoint>> waterLines,
            List<List<GeoPoint>> waterAreaOutlines,
            List<List<GeoPoint>> roadLines,
            GeoOverlayConfig config
        )
        {
            Setup(waterLines, waterAreaOutlines, GeoDebugOverlay.ConvertRoadLines(roadLines), config);
        }

        internal void Setup(
            List<List<GeoPoint>> waterLines,
            List<List<GeoPoint>> waterAreaOutlines,
            List<GeoRoadLine> roadLines,
            GeoOverlayConfig config
        )
        {
            Setup(waterLines, waterAreaOutlines, roadLines, new List<GeoZoningPolygon>(), config);
        }

        internal void Setup(
            List<List<GeoPoint>> waterLines,
            List<List<GeoPoint>> waterAreaOutlines,
            List<GeoRoadLine> roadLines,
            List<GeoZoningPolygon> zoningPolygons,
            GeoOverlayConfig config
        )
        {
            Setup(
                waterLines,
                waterAreaOutlines,
                roadLines,
                zoningPolygons,
                new List<GeoRailwayLine>(),
                false,
                "Aucune donnée ferroviaire disponible dans ce bundle.",
                config
            );
        }

        internal void Setup(
            List<List<GeoPoint>> waterLines,
            List<List<GeoPoint>> waterAreaOutlines,
            List<GeoRoadLine> roadLines,
            List<GeoZoningPolygon> zoningPolygons,
            List<GeoRailwayLine> railwayLines,
            bool railwayAvailable,
            string railwayStatus,
            GeoOverlayConfig config
        )
        {
            _waterLines = waterLines ?? new List<List<GeoPoint>>();
            _waterAreaOutlines = waterAreaOutlines ?? new List<List<GeoPoint>>();
            _roadLines = roadLines ?? new List<GeoRoadLine>();
            _zoningPolygons = zoningPolygons ?? new List<GeoZoningPolygon>();
            _config = config;
            InitializeRailwayOverlay(railwayLines, railwayAvailable, railwayStatus);

            // Important :
            // on garde les bounds de l'eau comme référence,
            // sinon les routes déplaceraient le centre/origine validé.
            var boundsSource = new List<List<GeoPoint>>();
            boundsSource.AddRange(_waterLines);
            boundsSource.AddRange(_waterAreaOutlines);

            // Preserve water as the calibrated reference whenever it exists.
            // A dry bundle still needs a valid center for its independent railway layer.
            if (boundsSource.Count == 0 && _railwayLines != null)
            {
                for (var i = 0; i < _railwayLines.Count; i++)
                {
                    var railwayLine = _railwayLines[i];

                    if (railwayLine != null && railwayLine.Points != null && railwayLine.Points.Count > 0)
                        boundsSource.Add(railwayLine.Points);
                }
            }

            _bounds = GeoBoundsCalculator.CalculateBounds(boundsSource);
            RecomputeRoadSemanticStats();
        }

        internal void ApplyRuntimeConfigChange(string configKey)
        {
            if (_config == null)
                return;

            try
            {
                _config.LoadVisualSettingsFromConfig();
                SyncVisibilityStateFromConfig();

                _visualSettingsDirty = false;
                _visualSettingsStatusMessage = "Options CS2 appliquées depuis config.json.";
                _controlPanelRebuildPending = false;

                if (RequiresOverlayRebuildForConfigKey(configKey))
                {
                    Log.Info("GroundOverlay: applying Options UI change with rebuild: " + configKey);
                    RequestOverlayRebuild("Options UI change: " + configKey, true);
                }
                else
                {
                    Log.Info("GroundOverlay: applying Options UI change live: " + configKey);
                    ApplyCurrentOverlayVisibilityToMaterials();
                }
            }
            catch (Exception ex)
            {
                Log.Info("GroundOverlay: failed to apply Options UI change " + configKey + " => " + ex);
            }
        }

        private void Update()
        {
            if (!_created)
            {
                _created = true;
                CreateOverlayWithTiming("initial");
                return;
            }

            HandleCalibrationInput();
            HandleOverlayVisibilityInput();
            HandlePendingBundleReload();
            UpdateProgressiveOverlayRebuild();
            UpdatePendingOverlayRebuildRequests();
            UpdatePendingRailwaySettingsSave();
            UpdateRoadLabelBillboards();
            UpdateOverlayHud();
        }



















        private void CreateOverlay()
        {
            ClearOverlayMaterialRegistries();
            if (_waterLines == null || _waterLines.Count == 0)
            {
                Log.Info("GroundOverlay: no water line geometry; continuing with independent overlay layers.");
            }

            var originLon = _config.UseGeoJsonCenter ? _bounds.CenterLon : _config.OriginLon;
            var originLat = _config.UseGeoJsonCenter ? _bounds.CenterLat : _config.OriginLat;

            var runtimeImportCenter = new Vector3(
                _config.WorldOriginX,
                ResolveY(new Vector3(_config.WorldOriginX, 0f, _config.WorldOriginZ)) + _config.GroundMargin,
                _config.WorldOriginZ
            );

            CityTimelineMod.Roads.GeoRoadImportPlacement.CenterX = runtimeImportCenter.x;
            CityTimelineMod.Roads.GeoRoadImportPlacement.CenterZ = runtimeImportCenter.z;
            CityTimelineMod.Roads.GeoRoadImportPlacement.GroundY = runtimeImportCenter.y;
            LogVerboseOverlay("GroundOverlay: runtime road import cache is managed by bundle load; overlay rebuild does not create CS2 roads.");

            LogVerboseOverlay("GroundOverlay: creating HARD visible segment overlay.");
            LogVerboseOverlay("GroundOverlay: bounds lon=[" + _bounds.MinLon + ", " + _bounds.MaxLon + "], lat=[" + _bounds.MinLat + ", " + _bounds.MaxLat + "]");
            LogVerboseOverlay("GroundOverlay: center lon=" + _bounds.CenterLon + ", lat=" + _bounds.CenterLat);
            LogVerboseOverlay("GroundOverlay: origin lon=" + originLon + ", lat=" + originLat);
            LogVerboseOverlay("GroundOverlay: world origin x=" + _config.WorldOriginX + ", z=" + _config.WorldOriginZ);
            LogVerboseOverlay("GroundOverlay: worldScale=" + _config.WorldScale + ", groundMargin=" + _config.GroundMargin);

            LogVerboseOverlay(
                "GroundOverlay: overlayRotationDegrees=" + _config.OverlayRotationDegrees +
                ", overlayScaleX=" + _config.OverlayScaleX +
                ", overlayScaleZ=" + _config.OverlayScaleZ +
                ", flipX=" + _config.FlipX +
                ", flipZ=" + _config.FlipZ
            );

            LogVerboseOverlay("GroundOverlay: water segmentWidth=" + _config.SegmentWidth + ", segmentHeight=" + _config.SegmentHeight);
            LogVerboseOverlay("GroundOverlay: waterLineSegmentWidth=" + _config.WaterLineSegmentWidth + ", waterLineSegmentHeight=" + _config.WaterLineSegmentHeight);
            LogVerboseOverlay("GroundOverlay: waterAreaSegmentWidth=" + _config.WaterAreaSegmentWidth + ", waterAreaSegmentHeight=" + _config.WaterAreaSegmentHeight);
            LogVerboseOverlay(
                "GroundOverlay: renderWaterAreaOutlines=" + _config.RenderWaterAreaOutlines +
                ", renderWaterAreaFillMeshes=" + _config.RenderWaterAreaFillMeshes +
                ", maxWaterAreaFillMeshesDebug=" + _config.MaxWaterAreaFillMeshesDebug +
                ", waterAreaFillYOffset=" + _config.WaterAreaFillYOffset
            );
            Log.Info(
                "GroundOverlay: render mode: exhaustive=" + IsRenderEverythingEnabled() +
                ", renderAllRoadSegments=" + ShouldRenderAllRoadSegments() +
                ", renderAllPathSegments=" + ShouldRenderAllPathSegments() +
                ", renderAllZoningPolygons=" + ShouldRenderAllZoningPolygons() +
                ", renderAllWaterSegments=" + ShouldRenderAllWaterSegments() +
                ", renderAllWaterAreaFills=" + ShouldRenderAllWaterAreaFills() +
                ", roadRenderMode=" + _config.RoadRenderMode +
                ", pathRenderMode=" + _config.PathRenderMode +
                ", semanticFilter=" + _config.RoadSemanticFilterMode
            );
            LogVerboseOverlay("GroundOverlay: maxRenderedSegments=" + _config.MaxRenderedSegments + ", maxWaterSegmentsDebug=" + _config.MaxWaterSegmentsDebug + ", pointStride=" + _config.PointStride);
            LogVerboseOverlay(
                "GroundOverlay: roadSegmentWidth=" + _config.RoadSegmentWidth +
                ", roadSegmentHeight=" + _config.RoadSegmentHeight +
                ", pathSegmentWidth=" + _config.PathSegmentWidth +
                ", pathSegmentHeight=" + _config.PathSegmentHeight +
                ", pathYOffset=" + _config.PathYOffset +
                ", pathAlpha=" + _config.PathAlpha +
                ", maxRoadSegmentsDebug=" + _config.MaxRoadSegmentsDebug +
                ", maxPathSegmentsDebug=" + _config.MaxPathSegmentsDebug
            );
            LogVerboseOverlay("GroundOverlay: road hierarchy style=highway");
            LogVerboseOverlay("GroundOverlay: minimumRoadDebugTier=" + _config.MinimumRoadDebugTier);
            LogVerboseOverlay(
                "GroundOverlay: layerYOffsets road=" + _config.RoadYOffset +
                ", waterLine=" + _config.WaterLineYOffset +
                ", waterAreaOutline=" + _config.WaterAreaOutlineYOffset +
                ", zoningFill=" + _config.ZoningFillYOffset +
                ", waterAreaFill=" + _config.WaterAreaFillYOffset
            );
            LogVerboseOverlay(
                "GroundOverlay: layerAlphas zoning=" + _config.ZoningAlpha +
                ", waterAreaFill=" + _config.WaterAreaFillAlpha +
                ", road=" + _config.RoadAlpha +
                ", waterLine=" + _config.WaterLineAlpha +
                ", waterAreaOutline=" + _config.WaterAreaOutlineAlpha
            );
            LogVerboseOverlay(
                "GroundOverlay: zoning polygons available=" +
                (_zoningPolygons != null ? _zoningPolygons.Count : 0) +
                ", renderZoning=" + _config.RenderZoning
            );
            LogVerboseOverlay(
                "GroundOverlay: zoningDebugFilterZone=" +
                (string.IsNullOrWhiteSpace(_config.ZoningDebugFilterZone) ? "(empty)" : _config.ZoningDebugFilterZone)
            );
            LogVerboseOverlay(
                "GroundOverlay: zoningDebugFilterCs2Contains=" +
                (string.IsNullOrWhiteSpace(_config.ZoningDebugFilterCs2Contains) ? "(empty)" : _config.ZoningDebugFilterCs2Contains)
            );

            LogVerboseOverlay(
                "GroundOverlay: zoningDebugFilterMaterialKey=" +
                (string.IsNullOrWhiteSpace(_config.ZoningDebugFilterMaterialKey) ? "(empty)" : _config.ZoningDebugFilterMaterialKey)
            );
            LogVerboseOverlay("GroundOverlay: zoning material style=cs2-first");
            LogZoningMaterialLegend();

            var materials = CreateOverlayRenderMaterials();
            var cyan = materials.Cyan;
            var waterAreaBlue = materials.WaterAreaBlue;
            var waterAreaFillBlue = materials.WaterAreaFillBlue;
            var orange = materials.FallbackRoad;
            var roadMotorwayMaterial = materials.RoadMotorway;
            var roadPrimaryMaterial = materials.RoadPrimary;
            var roadSecondaryMaterial = materials.RoadSecondary;
            var roadTertiaryMaterial = materials.RoadTertiary;
            var roadLinkMaterial = materials.RoadLink;
            var pathMaterial = materials.Path;
            var roadOneWayMaterial = materials.RoadOneWay;
            var roadBridgeMaterial = materials.RoadBridge;
            var roadTunnelMaterial = materials.RoadTunnel;
            var roadRoundaboutMaterial = materials.RoadRoundabout;
            var roadArrowMaterial = materials.RoadArrow;
            var roadLabelMaterial = materials.RoadLabel;
            var zoningResidentialLowMaterial = materials.ZoningResidentialLow;
            var zoningResidentialMediumMaterial = materials.ZoningResidentialMedium;
            var zoningResidentialHighMaterial = materials.ZoningResidentialHigh;
            var zoningCommercialLowMaterial = materials.ZoningCommercialLow;
            var zoningCommercialHighMaterial = materials.ZoningCommercialHigh;
            var zoningRetailDetailMaterial = materials.ZoningRetailDetail;
            var zoningIndustrialMaterial = materials.ZoningIndustrial;
            var zoningOfficeMaterial = materials.ZoningOffice;
            var zoningSurfaceMaterial = materials.ZoningSurface;
            var zoningRampMaterial = materials.ZoningRamp;
            var zoningMixedMaterial = materials.ZoningMixed;
            var zoningFallbackMaterial = materials.ZoningFallback;

            ApplyCurrentOverlayVisibilityToMaterials();

            var red = materials.DebugRed;
            var green = materials.DebugGreen;
            var yellow = materials.DebugYellow;
            var magenta = materials.DebugMagenta;

            var worldMapBoundsMaterial = materials.WorldMapBounds;
            var heightMapBoundsMaterial = materials.HeightMapBounds;
            var mapCenterMaterial = materials.MapCenter;


            var stride = Math.Max(1, _config.PointStride);
            MapBoundsRenderer.Render(
                _config,
                transform,
                worldMapBoundsMaterial,
                heightMapBoundsMaterial,
                mapCenterMaterial,
                originLon,
                originLat,
                ResolveY,
                LogVerboseOverlay
            );

            var createdWaterSegments = 0;
            var createdWaterLines = 0;

            Vector3 firstWorld = Vector3.zero;
            Vector3 lastWorld = Vector3.zero;
            var hasEndpoints = false;

            // TODO(progressive rebuild): split zoning into its own coroutine phase.
            var createdZoningFillMeshes = RenderZoningFillMeshes(
                zoningResidentialLowMaterial,
                zoningResidentialMediumMaterial,
                zoningResidentialHighMaterial,
                zoningCommercialLowMaterial,
                zoningCommercialHighMaterial,
                zoningRetailDetailMaterial,
                zoningIndustrialMaterial,
                zoningOfficeMaterial,
                zoningSurfaceMaterial,
                zoningRampMaterial,
                zoningMixedMaterial,
                zoningFallbackMaterial,
                stride
            );

            if (_config.RenderZoning)
                LogVerboseOverlay("GroundOverlay: created zoning fill meshes=" + createdZoningFillMeshes);

            // TODO(progressive rebuild): split water lines/areas into their own coroutine phase.
            RenderWaterOverlayGroups(
                cyan,
                waterAreaBlue,
                waterAreaFillBlue,
                stride,
                ref createdWaterSegments,
                ref createdWaterLines,
                ref firstWorld,
                ref lastWorld,
                ref hasEndpoints
            );

            var railwayCounters = RenderRailwayGroup(materials, stride, originLon, originLat);

            var createdRoadLines = 0;
            var createdPathLines = 0;
            var createdPathSegments = 0;
            // TODO(progressive rebuild): split road/path mesh batching into resumable chunks.
            var createdRoadAndPathSegments = RenderRoadGroup(
                roadMotorwayMaterial,
                roadPrimaryMaterial,
                roadSecondaryMaterial,
                roadTertiaryMaterial,
                roadLinkMaterial,
                pathMaterial,
                orange,
                roadOneWayMaterial,
                roadBridgeMaterial,
                roadTunnelMaterial,
                roadRoundaboutMaterial,
                stride,
                ref createdRoadLines,
                ref createdPathLines,
                ref createdPathSegments
            );

            // TODO(progressive rebuild): keep arrows and labels as separate optional phases.
            var createdRoadArrows = RenderRoadDirectionArrows(roadArrowMaterial, stride);
            var createdRoadLabels = RenderRoadLabels(roadLabelMaterial, stride);

            var center = new Vector3(
                _config.WorldOriginX,
                ResolveY(new Vector3(_config.WorldOriginX, 0f, _config.WorldOriginZ)) + _config.GroundMargin,
                _config.WorldOriginZ
            );

            
            CityTimelineMod.Roads.GeoRoadImportPlacement.CenterX = center.x;
            CityTimelineMod.Roads.GeoRoadImportPlacement.CenterZ = center.z;
            CityTimelineMod.Roads.GeoRoadImportPlacement.GroundY = center.y;if (_config.DebugBeacons)
            {
                CreateBeacon("ground_red_center_beacon", center, red);

                if (hasEndpoints)
                {
                    CreateBeacon("ground_green_first_beacon", firstWorld, green);
                    CreateBeacon("ground_yellow_last_beacon", lastWorld, yellow);
                }
            }

            if (_config.DebugTower)
                CreateTower("ground_magenta_debug_tower", center, magenta);

            LogVerboseOverlay("GroundOverlay: first world point=" + firstWorld);
            LogVerboseOverlay("GroundOverlay: last world point=" + lastWorld);
            LogVerboseOverlay("GroundOverlay: center world point=" + center);

            LogVerboseOverlay(
                "GroundOverlay: created water lines=" + createdWaterLines +
                ", water batched segments=" + createdWaterSegments +
                ", railway lines=" + railwayCounters.CreatedLines +
                ", railway batched segments=" + railwayCounters.CreatedSegments +
                ", road lines=" + createdRoadLines +
                ", path lines=" + createdPathLines +
                ", road/path batched segments=" + createdRoadAndPathSegments +
                ", path batched segments=" + createdPathSegments +
                ", one-way arrows=" + createdRoadArrows +
                ", road labels=" + createdRoadLabels
            );

LogRoadHighwaySummary();
LogRoadSemanticSummary();
LogRenderedAndSkippedRoadHighwaySummary();
LogRoadTierFilterSummary(createdRoadLines + createdPathLines);
        }




















































































    }
}
