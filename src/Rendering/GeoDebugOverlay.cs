using System;
using System.Collections.Generic;
using CityTimelineMod.Config;
using CityTimelineMod.Importers;
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
            var existing = GameObject.Find(RootName);
            if (existing != null)
                UnityEngine.Object.Destroy(existing);

            var root = new GameObject(RootName);
            UnityEngine.Object.DontDestroyOnLoad(root);

            var overlay = root.AddComponent<GroundOverlayBehaviour>();
            overlay.Setup(waterLines, waterAreaOutlines, roadLines, zoningPolygons, config);

            Log.Info("GroundOverlay: installed HARD visible segment overlay.");
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

    internal sealed class GroundOverlayBehaviour : MonoBehaviour
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

        private bool _zoningVisible = true;
        private bool _roadsVisible = true;
        private bool _waterVisible = true;

        private Rect _controlPanelRect = new Rect(24f, 24f, 620f, 760f);
        private Vector2 _controlPanelScroll = Vector2.zero;
        private bool _controlPanelLogOnce;
        private bool _controlPanelCalibrationUnlocked;
        private bool _controlPanelRebuildPending;
        private string _currentDisplayPresetLabel = "Custom";
        private string _visualSettingsStatusMessage = "Réglages live.";
        private bool _visualSettingsDirty;

        private int _debugWaterSegmentLogs;
        private int _debugRoadSegmentLogs;
        private Vector3 _liveCalibrationOffset = Vector3.zero;
        private float _liveCalibrationRotationDegrees = 0f;
        private float _liveCalibrationScaleX = 1f;
        private float _liveCalibrationScaleZ = 1f;

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
            _waterLines = waterLines ?? new List<List<GeoPoint>>();
            _waterAreaOutlines = waterAreaOutlines ?? new List<List<GeoPoint>>();
            _roadLines = roadLines ?? new List<GeoRoadLine>();
            _zoningPolygons = zoningPolygons ?? new List<GeoZoningPolygon>();
            _config = config;

            // Important :
            // on garde les bounds de l'eau comme référence,
            // sinon les routes déplaceraient le centre/origine validé.
            var boundsSource = new List<List<GeoPoint>>();
            boundsSource.AddRange(_waterLines);
            boundsSource.AddRange(_waterAreaOutlines);
            _bounds = CalculateBounds(boundsSource);
        }

        private void Update()
        {
            if (!_created)
            {
                _created = true;
                CreateOverlay();
                return;
            }

            HandleCalibrationInput();
            HandleOverlayVisibilityInput();
            UpdateOverlayHud();
        }

        private void CreateOverlay()
        {
            ClearOverlayMaterialRegistries();
            if (_waterLines == null || _waterLines.Count == 0)
            {
                Log.Info("GroundOverlay: no water geometry.");
                return;
            }

            var originLon = _config.UseGeoJsonCenter ? _bounds.CenterLon : _config.OriginLon;
            var originLat = _config.UseGeoJsonCenter ? _bounds.CenterLat : _config.OriginLat;

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
            LogVerboseOverlay("GroundOverlay: maxRenderedSegments=" + _config.MaxRenderedSegments + ", maxWaterSegmentsDebug=" + _config.MaxWaterSegmentsDebug + ", pointStride=" + _config.PointStride);
            LogVerboseOverlay(
                "GroundOverlay: roadSegmentWidth=" + _config.RoadSegmentWidth +
                ", roadSegmentHeight=" + _config.RoadSegmentHeight +
                ", pathSegmentWidth=" + _config.PathSegmentWidth +
                ", pathSegmentHeight=" + _config.PathSegmentHeight +
                ", pathYOffset=" + _config.PathYOffset +
                ", pathAlpha=" + _config.PathAlpha +
                ", maxRoadSegmentsDebug=" + _config.MaxRoadSegmentsDebug
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

            var cyan = CreateMaterial(new Color(0f, 1f, 1f, _config.WaterLineAlpha));
            var waterAreaBlue = CreateMaterial(new Color(0.05f, 0.45f, 1f, _config.WaterAreaOutlineAlpha));
            var waterAreaFillBlue = CreateMaterial(new Color(0.02f, 0.18f, 0.55f, _config.WaterAreaFillAlpha));
            var orange = CreateMaterial(new Color(1f, 0.45f, 0f, _config.RoadAlpha));
            var roadMotorwayMaterial = CreateMaterial(new Color(1f, 0.18f, 0f, _config.RoadAlpha));
            var roadPrimaryMaterial = CreateMaterial(new Color(1f, 0.38f, 0f, _config.RoadAlpha));
            var roadSecondaryMaterial = CreateMaterial(new Color(1f, 0.62f, 0.05f, _config.RoadAlpha));
            var roadTertiaryMaterial = CreateMaterial(new Color(1f, 0.82f, 0.15f, _config.RoadAlpha));
            var roadLinkMaterial = CreateMaterial(new Color(1f, 0.28f, 0f, _config.RoadAlpha));
            var pathMaterial = CreateMaterial(new Color(0.95f, 0.95f, 0.95f, _config.PathAlpha));

            var zoningResidentialLowMaterial = CreateMaterial(new Color(0.15f, 0.85f, 0.25f, _config.ZoningAlpha));
            var zoningResidentialMediumMaterial = CreateMaterial(new Color(0.65f, 0.9f, 0.25f, _config.ZoningAlpha));
            var zoningResidentialHighMaterial = CreateMaterial(new Color(1f, 0.9f, 0.2f, _config.ZoningAlpha));
            var zoningCommercialLowMaterial = CreateMaterial(new Color(0.35f, 0.25f, 1f, _config.ZoningAlpha));
            var zoningRetailDetailMaterial = CreateMaterial(new Color(0.75f, 0.2f, 1f, _config.ZoningAlpha));
            var zoningIndustrialMaterial = CreateMaterial(new Color(0.9f, 0.45f, 0.12f, _config.ZoningAlpha));
            var zoningOfficeMaterial = CreateMaterial(new Color(0.2f, 0.55f, 1f, _config.ZoningAlpha));
            var zoningSurfaceMaterial = CreateMaterial(new Color(0.45f, 0.45f, 0.45f, _config.ZoningAlpha));
            var zoningRampMaterial = CreateMaterial(new Color(0.22f, 0.22f, 0.22f, _config.ZoningAlpha));
            var zoningFallbackMaterial = CreateMaterial(new Color(1f, 1f, 1f, _config.ZoningAlpha));

            _waterLineMaterials.Add(cyan);
            _waterAreaOutlineMaterials.Add(waterAreaBlue);
            _waterAreaFillMaterials.Add(waterAreaFillBlue);

            _roadMaterials.Add(orange);
            _roadMaterials.Add(roadMotorwayMaterial);
            _roadMaterials.Add(roadPrimaryMaterial);
            _roadMaterials.Add(roadSecondaryMaterial);
            _roadMaterials.Add(roadTertiaryMaterial);
            _roadMaterials.Add(roadLinkMaterial);

            _pathMaterials.Add(pathMaterial);

            _zoningMaterials.Add(zoningResidentialLowMaterial);
            _zoningMaterials.Add(zoningResidentialMediumMaterial);
            _zoningMaterials.Add(zoningResidentialHighMaterial);
            _zoningMaterials.Add(zoningCommercialLowMaterial);
            _zoningMaterials.Add(zoningRetailDetailMaterial);
            _zoningMaterials.Add(zoningIndustrialMaterial);
            _zoningMaterials.Add(zoningOfficeMaterial);
            _zoningMaterials.Add(zoningSurfaceMaterial);
            _zoningMaterials.Add(zoningRampMaterial);
            _zoningMaterials.Add(zoningFallbackMaterial);

            ApplyCurrentOverlayVisibilityToMaterials();

            var red = CreateMaterial(Color.red);
            var green = CreateMaterial(Color.green);
            var yellow = CreateMaterial(Color.yellow);
            var magenta = CreateMaterial(Color.magenta);

            var worldMapBoundsMaterial = CreateMaterial(new Color(1f, 1f, 1f, _config.MapBoundsAlpha));
            var heightMapBoundsMaterial = CreateMaterial(new Color(1f, 0.1f, 1f, _config.MapBoundsAlpha));
            var mapCenterMaterial = CreateMaterial(new Color(1f, 0f, 0f, _config.MapBoundsAlpha));


            var stride = Math.Max(1, _config.PointStride);
            RenderMapBoundsOverlay(worldMapBoundsMaterial, heightMapBoundsMaterial, mapCenterMaterial, originLon, originLat);

            var createdWaterSegments = 0;
            var createdWaterLines = 0;

            Vector3 firstWorld = Vector3.zero;
            Vector3 lastWorld = Vector3.zero;
            var hasEndpoints = false;

            // ==========================================================
            // EAU : lignes et contours de surfaces rendus séparément
            // ==========================================================
            var createdZoningFillMeshes = RenderZoningFillMeshes(
                zoningResidentialLowMaterial,
                zoningResidentialMediumMaterial,
                zoningResidentialHighMaterial,
                zoningIndustrialMaterial,
                zoningCommercialLowMaterial,
                zoningRetailDetailMaterial,
                zoningOfficeMaterial,
                zoningSurfaceMaterial,
                zoningRampMaterial,
                zoningFallbackMaterial,
                stride
            );

            if (_config.RenderZoning)
                LogVerboseOverlay("GroundOverlay: created zoning fill meshes=" + createdZoningFillMeshes);

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

            var createdRoadLines = 0;
            var createdPathLines = 0;
            var createdPathSegments = 0;
            var createdRoadAndPathSegments = RenderRoadGroup(
                roadMotorwayMaterial,
                roadPrimaryMaterial,
                roadSecondaryMaterial,
                roadTertiaryMaterial,
                roadLinkMaterial,
                pathMaterial,
                orange,
                stride,
                ref createdRoadLines,
                ref createdPathLines,
                ref createdPathSegments
            );

            var center = new Vector3(
                _config.WorldOriginX,
                ResolveY(new Vector3(_config.WorldOriginX, 0f, _config.WorldOriginZ)) + _config.GroundMargin,
                _config.WorldOriginZ
            );

            if (_config.DebugBeacons)
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
                ", road lines=" + createdRoadLines +
                ", path lines=" + createdPathLines +
                ", road/path batched segments=" + createdRoadAndPathSegments +
                ", path batched segments=" + createdPathSegments
            );

            LogRoadHighwaySummary();
            LogRenderedAndSkippedRoadHighwaySummary();
            LogRoadTierFilterSummary(createdRoadLines + createdPathLines);
        }



        private sealed class RoadMeshBatch
        {
            internal readonly List<Vector3> Vertices = new List<Vector3>();
            internal readonly List<int> Triangles = new List<int>();
            internal Material Material;
            internal int ChunkIndex;
            internal int SegmentCount;
        }

        private int RenderRoadGroup(
            Material roadMotorwayMaterial,
            Material roadPrimaryMaterial,
            Material roadSecondaryMaterial,
            Material roadTertiaryMaterial,
            Material roadLinkMaterial,
            Material pathMaterial,
            Material fallbackRoadMaterial,
            int stride,
            ref int createdRoadLines,
            ref int createdPathLines,
            ref int createdPathSegments
        )
        {
            var createdRoadSegments = 0;
            var createdRoadMeshObjects = 0;
            var createdPathMeshObjects = 0;

            if ((!_config.RenderRoads && !_config.RenderPaths) || _roadLines == null || _roadLines.Count == 0)
                return 0;

            var originLon = _config.UseGeoJsonCenter ? _bounds.CenterLon : _config.OriginLon;
            var originLat = _config.UseGeoJsonCenter ? _bounds.CenterLat : _config.OriginLat;

            var safeStride = Math.Max(1, stride);
            var maxSegments = Math.Max(1, _config.MaxRoadSegmentsDebug);
            const int maxVerticesPerMesh = 60000;

            var batches = new Dictionary<string, RoadMeshBatch>();

            foreach (var roadLine in _roadLines)
            {
                var road = roadLine != null ? roadLine.Points : null;
                var highway = roadLine != null ? roadLine.Highway : null;

                if (road == null || road.Count < 2)
                    continue;

                var isPath = roadLine != null && roadLine.IsPath;

                if (isPath)
                {
                    if (!_config.RenderPaths)
                        continue;
                }
                else if (!_config.RenderRoads)
                {
                    continue;
                }

                if (!isPath && !ShouldRenderRoadByTier(highway, _config.MinimumRoadDebugTier))
                    continue;

                var highwayFilter = isPath ? _config.PathHighwayFilter : _config.RoadHighwayFilter;

                if (!ShouldRenderHighwayByFilter(highway, highwayFilter))
                    continue;

                if (isPath)
                    createdPathLines++;
                else
                    createdRoadLines++;

                for (var i = 0; i < road.Count - safeStride; i += safeStride)
                {
                    if (createdRoadSegments + createdPathSegments >= maxSegments)
                        break;

                    var a = GeoTransform.ToWorld(road[i], _config, originLon, originLat);
                    var b = GeoTransform.ToWorld(road[Math.Min(i + safeStride, road.Count - 1)], _config, originLon, originLat);

                    var yOffset = isPath ? _config.PathYOffset : _config.RoadYOffset;

                    a.y = ResolveY(a) + _config.GroundMargin + yOffset;
                    b.y = ResolveY(b) + _config.GroundMargin + yOffset;

                    var batchKey = isPath ? "path" : ResolveRoadBatchKey(highway);

                    RoadMeshBatch batch;

                    if (!batches.TryGetValue(batchKey, out batch))
                    {
                        batch = new RoadMeshBatch();
                        batch.Material = isPath
                            ? pathMaterial
                            : ResolveRoadBatchMaterial(
                                highway,
                                roadMotorwayMaterial,
                                roadPrimaryMaterial,
                                roadSecondaryMaterial,
                                roadTertiaryMaterial,
                                roadLinkMaterial,
                                fallbackRoadMaterial
                            );

                        batches[batchKey] = batch;
                    }

                    if (batch.Vertices.Count + 8 >= maxVerticesPerMesh)
                    {
                        var flushedObjects = FlushRoadBatch(batchKey, batch);

                        if (isPath)
                            createdPathMeshObjects += flushedObjects;
                        else
                            createdRoadMeshObjects += flushedObjects;
                    }

                    var segmentWidth = isPath ? _config.PathSegmentWidth : _config.RoadSegmentWidth;
                    var segmentHeight = isPath ? _config.PathSegmentHeight : _config.RoadSegmentHeight;

                    if (AppendRoadSegmentBox(
                        batch,
                        a,
                        b,
                        segmentWidth,
                        segmentHeight
                    ))
                    {
                        if (isPath)
                            createdPathSegments++;
                        else
                            createdRoadSegments++;
                    }
                }

                if (createdRoadSegments + createdPathSegments >= maxSegments)
                    break;
            }

            foreach (var pair in batches)
            {
                var flushedObjects = FlushRoadBatch(pair.Key, pair.Value);

                if (pair.Key == "path")
                    createdPathMeshObjects += flushedObjects;
                else
                    createdRoadMeshObjects += flushedObjects;
            }

            LogVerboseOverlay(
                "GroundOverlay: road batching summary: roadLines=" + createdRoadLines +
                ", roadSegments=" + createdRoadSegments +
                ", meshObjects=" + createdRoadMeshObjects +
                ", maxRoadSegmentsDebug=" + _config.MaxRoadSegmentsDebug +
                ", minimumRoadDebugTier=" + _config.MinimumRoadDebugTier
            );

            LogVerboseOverlay(
                "GroundOverlay: path batching summary: pathLines=" + createdPathLines +
                ", pathSegments=" + createdPathSegments +
                ", meshObjects=" + createdPathMeshObjects +
                ", maxRoadSegmentsDebug=" + _config.MaxRoadSegmentsDebug
            );

            return createdRoadSegments + createdPathSegments;
        }

        private bool AppendRoadSegmentBox(
            RoadMeshBatch batch,
            Vector3 a,
            Vector3 b,
            float roadWidth,
            float roadHeight
        )
        {
            if (batch == null)
                return false;

            var dx = b.x - a.x;
            var dz = b.z - a.z;
            var lengthSq = dx * dx + dz * dz;

            if (lengthSq < 0.01f)
                return false;

            var length = Mathf.Sqrt(lengthSq);
            var ux = dx / length;
            var uz = dz / length;

            var width = Mathf.Max(0.1f, roadWidth);
            var height = Mathf.Max(0.1f, roadHeight);

            var overlap = width * 0.35f;
            var halfOverlap = overlap * 0.5f;

            var ax = a.x - ux * halfOverlap;
            var az = a.z - uz * halfOverlap;
            var bx = b.x + ux * halfOverlap;
            var bz = b.z + uz * halfOverlap;

            var px = -uz * width * 0.5f;
            var pz = ux * width * 0.5f;

            var baseIndex = batch.Vertices.Count;

            var aLeftBottom = new Vector3(ax + px, a.y, az + pz);
            var aRightBottom = new Vector3(ax - px, a.y, az - pz);
            var bLeftBottom = new Vector3(bx + px, b.y, bz + pz);
            var bRightBottom = new Vector3(bx - px, b.y, bz - pz);

            var aLeftTop = new Vector3(aLeftBottom.x, aLeftBottom.y + height, aLeftBottom.z);
            var aRightTop = new Vector3(aRightBottom.x, aRightBottom.y + height, aRightBottom.z);
            var bLeftTop = new Vector3(bLeftBottom.x, bLeftBottom.y + height, bLeftBottom.z);
            var bRightTop = new Vector3(bRightBottom.x, bRightBottom.y + height, bRightBottom.z);

            batch.Vertices.Add(aLeftBottom);   // 0
            batch.Vertices.Add(aRightBottom);  // 1
            batch.Vertices.Add(bLeftBottom);   // 2
            batch.Vertices.Add(bRightBottom);  // 3
            batch.Vertices.Add(aLeftTop);      // 4
            batch.Vertices.Add(aRightTop);     // 5
            batch.Vertices.Add(bLeftTop);      // 6
            batch.Vertices.Add(bRightTop);     // 7

            AddQuad(batch.Triangles, baseIndex + 4, baseIndex + 6, baseIndex + 7, baseIndex + 5); // top
            AddQuad(batch.Triangles, baseIndex + 0, baseIndex + 1, baseIndex + 3, baseIndex + 2); // bottom
            AddQuad(batch.Triangles, baseIndex + 0, baseIndex + 2, baseIndex + 6, baseIndex + 4); // left
            AddQuad(batch.Triangles, baseIndex + 1, baseIndex + 5, baseIndex + 7, baseIndex + 3); // right
            AddQuad(batch.Triangles, baseIndex + 0, baseIndex + 4, baseIndex + 5, baseIndex + 1); // start
            AddQuad(batch.Triangles, baseIndex + 2, baseIndex + 3, baseIndex + 7, baseIndex + 6); // end

            batch.SegmentCount++;

            return true;
        }

        private static void AddQuad(List<int> triangles, int a, int b, int c, int d)
        {
            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);

            triangles.Add(a);
            triangles.Add(c);
            triangles.Add(d);
        }

        private int FlushRoadBatch(string batchKey, RoadMeshBatch batch)
        {
            if (batch == null || batch.Vertices.Count < 3 || batch.Triangles.Count < 3)
                return 0;

            var name = "road_batch_" + SanitizeObjectName(batchKey) + "_" + batch.ChunkIndex;

            var obj = new GameObject(name);
            obj.transform.SetParent(transform, true);

            var mesh = new Mesh();
            mesh.name = name + "_mesh";
            mesh.vertices = batch.Vertices.ToArray();
            mesh.triangles = batch.Triangles.ToArray();
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();

            var filter = obj.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            var renderer = obj.AddComponent<MeshRenderer>();
            renderer.material = batch.Material;

            LogVerboseOverlay(
                "GroundOverlay: road batch created: " + name +
                ", segments=" + batch.SegmentCount +
                ", vertices=" + batch.Vertices.Count +
                ", triangles=" + (batch.Triangles.Count / 3)
            );

            batch.ChunkIndex++;
            batch.SegmentCount = 0;
            batch.Vertices.Clear();
            batch.Triangles.Clear();

            return 1;
        }

        private static string ResolveRoadBatchKey(string highway)
        {
            var value = string.IsNullOrWhiteSpace(highway) ? "" : highway.ToLowerInvariant();

            if (value.EndsWith("_link"))
                return "link";

            switch (value)
            {
                case "motorway":
                case "trunk":
                    return "motorway";

                case "primary":
                    return "primary";

                case "secondary":
                    return "secondary";

                case "tertiary":
                    return "tertiary";

                default:
                    return "fallback";
            }
        }

        private static Material ResolveRoadBatchMaterial(
            string highway,
            Material roadMotorwayMaterial,
            Material roadPrimaryMaterial,
            Material roadSecondaryMaterial,
            Material roadTertiaryMaterial,
            Material roadLinkMaterial,
            Material fallbackRoadMaterial
        )
        {
            var key = ResolveRoadBatchKey(highway);

            switch (key)
            {
                case "motorway":
                    return roadMotorwayMaterial;

                case "primary":
                    return roadPrimaryMaterial;

                case "secondary":
                    return roadSecondaryMaterial;

                case "tertiary":
                    return roadTertiaryMaterial;

                case "link":
                    return roadLinkMaterial;

                default:
                    return fallbackRoadMaterial;
            }
        }


        private void LogRenderedAndSkippedRoadHighwaySummary()
        {
            if (_roadLines == null || _roadLines.Count == 0)
                return;

            var rendered = new Dictionary<string, int>();
            var skipped = new Dictionary<string, int>();

            foreach (var road in _roadLines)
            {
                var highway = road != null ? road.Highway : null;

                if (string.IsNullOrWhiteSpace(highway))
                    highway = "(missing)";

                var isPath = road != null && road.IsPath;
                var highwayFilter = isPath ? _config.PathHighwayFilter : _config.RoadHighwayFilter;
                var sourceEnabled = isPath ? _config.RenderPaths : _config.RenderRoads;

                var shouldRender =
                    sourceEnabled &&
                    (isPath || ShouldRenderRoadByTier(highway, _config.MinimumRoadDebugTier)) &&
                    ShouldRenderHighwayByFilter(highway, highwayFilter);
                var target = shouldRender ? rendered : skipped;

                int count;
                target.TryGetValue(highway, out count);
                target[highway] = count + 1;
            }

            LogVerboseOverlay(
                "GroundOverlay: rendered road highway summary: " +
                FormatRoadHighwayCounts(rendered)
            );

            LogVerboseOverlay(
                "GroundOverlay: skipped road highway summary: " +
                FormatRoadHighwayCounts(skipped)
            );
        }

        private static string FormatRoadHighwayCounts(Dictionary<string, int> counts)
        {
            if (counts == null || counts.Count == 0)
                return "(none)";

            var parts = new List<string>();

            foreach (var pair in counts)
                parts.Add(pair.Key + "=" + pair.Value);

            return string.Join(", ", parts.ToArray());
        }


        private void LogRoadTierFilterSummary(int renderedRoadLines)
        {
            var sourceRoadLines = _roadLines != null ? _roadLines.Count : 0;
            var skippedRoadLines = Math.Max(0, sourceRoadLines - renderedRoadLines);

            LogVerboseOverlay(
                "GroundOverlay: road tier filter summary: source=" + sourceRoadLines +
                ", rendered=" + renderedRoadLines +
                ", skipped=" + skippedRoadLines +
                ", minimumTier=" + _config.MinimumRoadDebugTier
            );
        }



        private static bool ShouldRenderRoadByTier(string highway, int minimumTier)
        {
            if (minimumTier <= 0)
                return true;

            return GetRoadDebugTier(highway) >= minimumTier;
        }

        private static bool ShouldRenderHighwayByFilter(string highway, string filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
                return true;

            var normalizedFilter = filter.Trim().ToLowerInvariant();

            if (normalizedFilter == "all" || normalizedFilter == "*")
                return true;

            var highwayKey = NormalizeHighwayFilterKey(highway);
            var parts = normalizedFilter.Split(',');

            for (var i = 0; i < parts.Length; i++)
            {
                var wanted = NormalizeHighwayFilterKey(parts[i]);

                if (wanted == "all" || wanted == "*" || wanted == highwayKey)
                    return true;
            }

            return false;
        }

        private static string NormalizeHighwayFilterKey(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "(unknown)"
                : value.Trim().ToLowerInvariant();
        }

        private static int GetRoadDebugTier(string highway)
        {
            if (string.IsNullOrWhiteSpace(highway))
                return 0;

            var value = highway.ToLowerInvariant();

            switch (value)
            {
                case "motorway":
                case "trunk":
                case "motorway_link":
                case "trunk_link":
                    return 4;

                case "primary":
                case "primary_link":
                    return 3;

                case "secondary":
                case "secondary_link":
                    return 2;

                case "tertiary":
                case "tertiary_link":
                    return 1;

                default:
                    return 0;
            }
        }


        private void LogRoadHighwaySummary()
        {
            if (_roadLines == null || _roadLines.Count == 0)
                return;

            var counts = new Dictionary<string, int>();

            foreach (var road in _roadLines)
            {
                var highway = road != null ? road.Highway : null;

                if (string.IsNullOrWhiteSpace(highway))
                    highway = "(missing)";

                int count;
                counts.TryGetValue(highway, out count);
                counts[highway] = count + 1;
            }

            var parts = new List<string>();

            foreach (var pair in counts)
                parts.Add(pair.Key + "=" + pair.Value);

            LogVerboseOverlay("GroundOverlay: road highway summary: " + string.Join(", ", parts.ToArray()));
        }



        private sealed class ZoningMeshBatch
        {
            internal readonly List<Vector3> Vertices = new List<Vector3>();
            internal readonly List<int> Triangles = new List<int>();
            internal Material Material;
            internal int ChunkIndex;
            internal int PolygonCount;
        }

        private int RenderZoningFillMeshes(
            Material residentialLowMaterial,
            Material residentialMediumMaterial,
            Material residentialHighMaterial,
            Material commercialLowMaterial,
            Material retailDetailMaterial,
            Material industrialMaterial,
            Material officeMaterial,
            Material surfaceMaterial,
            Material rampMaterial,
            Material fallbackMaterial,
            int stride
        )
        {
            if (!_config.RenderZoning || _zoningPolygons == null || _zoningPolygons.Count == 0)
                return 0;

            var originLon = _config.UseGeoJsonCenter ? _bounds.CenterLon : _config.OriginLon;
            var originLat = _config.UseGeoJsonCenter ? _bounds.CenterLat : _config.OriginLat;

            var sourceMeshes = _zoningPolygons.Count;
            var eligibleMeshes = 0;
            var renderedPolygons = 0;
            var createdMeshObjects = 0;
            var skippedByFilter = 0;
            var skippedByLimit = 0;
            var eligibleCs2Counts = new Dictionary<string, int>();

            var batches = new Dictionary<string, ZoningMeshBatch>();

            var maxPolygons = Math.Max(1, _config.MaxZoningFillMeshesDebug);
            var safeStride = Math.Max(1, stride);
            const int maxVerticesPerMesh = 60000;

            foreach (var polygon in _zoningPolygons)
            {
                if (polygon == null || polygon.Rings == null || polygon.Rings.Count == 0)
                    continue;

                if (!ShouldRenderZoningByFilter(
                    polygon.Zone,
                    polygon.Cs2,
                    ResolveZoningMaterialKey(polygon.Zone, polygon.Cs2),
                    _config.ZoningDebugFilterZone,
                    _config.ZoningDebugFilterCs2Contains,
                    _config.ZoningDebugFilterMaterialKey))
                {
                    skippedByFilter++;
                    continue;
                }

                eligibleMeshes++;

                var cs2Key = string.IsNullOrWhiteSpace(polygon.Cs2) ? "(missing)" : polygon.Cs2;
                int cs2Count;
                eligibleCs2Counts.TryGetValue(cs2Key, out cs2Count);
                eligibleCs2Counts[cs2Key] = cs2Count + 1;

                if (renderedPolygons >= maxPolygons)
                {
                    skippedByLimit++;
                    continue;
                }

                var outerRing = polygon.Rings[0];

                if (outerRing == null || outerRing.Count < 3)
                    continue;

                var vertices = new List<Vector3>();

                for (var i = 0; i < outerRing.Count; i += safeStride)
                {
                    var world = GeoTransform.ToWorld(outerRing[i], _config, originLon, originLat);
                    world.y = ResolveY(world) + _config.GroundMargin + _config.ZoningFillYOffset;
                    vertices.Add(world);

                    if (vertices.Count >= maxVerticesPerMesh)
                        break;
                }

                if (vertices.Count < 3)
                    continue;

                var triangles = MakeDoubleSidedTriangles(TriangulatePolygonXZ(vertices));

                if (triangles.Count < 3)
                    continue;

                var materialKey = ResolveZoningMaterialKey(polygon.Zone, polygon.Cs2);

                ZoningMeshBatch batch;

                if (!batches.TryGetValue(materialKey, out batch))
                {
                    batch = new ZoningMeshBatch();
                    batch.Material = ResolveZoningMaterial(
                        polygon.Zone,
                        polygon.Cs2,
                        residentialLowMaterial,
                        residentialMediumMaterial,
                        residentialHighMaterial,
                        commercialLowMaterial,
                        retailDetailMaterial,
                        industrialMaterial,
                        officeMaterial,
                        surfaceMaterial,
                        rampMaterial,
                        fallbackMaterial
                    );

                    batches[materialKey] = batch;
                }

                if (batch.Vertices.Count + vertices.Count >= maxVerticesPerMesh)
                    createdMeshObjects += FlushZoningBatch(materialKey, batch);

                var vertexOffset = batch.Vertices.Count;

                batch.Vertices.AddRange(vertices);

                for (var i = 0; i < triangles.Count; i++)
                    batch.Triangles.Add(vertexOffset + triangles[i]);

                batch.PolygonCount++;
                renderedPolygons++;
            }

            foreach (var pair in batches)
                createdMeshObjects += FlushZoningBatch(pair.Key, pair.Value);

            LogVerboseOverlay(
                "GroundOverlay: zoning render summary: source=" + sourceMeshes +
                ", eligible=" + eligibleMeshes +
                ", rendered=" + renderedPolygons +
                ", meshObjects=" + createdMeshObjects +
                ", skippedByFilter=" + skippedByFilter +
                ", skippedByLimit=" + skippedByLimit +
                ", filterZone=" + (string.IsNullOrWhiteSpace(_config.ZoningDebugFilterZone) ? "(empty)" : _config.ZoningDebugFilterZone) +
                ", filterCs2Contains=" + (string.IsNullOrWhiteSpace(_config.ZoningDebugFilterCs2Contains) ? "(empty)" : _config.ZoningDebugFilterCs2Contains) +
                ", filterMaterialKey=" +
                (string.IsNullOrWhiteSpace(_config.ZoningDebugFilterMaterialKey) ? "(empty)" : _config.ZoningDebugFilterMaterialKey)
            );

            LogVerboseOverlay(
                "GroundOverlay: eligible zoning cs2 summary: " +
                FormatRoadHighwayCounts(eligibleCs2Counts)
            );

            return createdMeshObjects;
        }

        private int FlushZoningBatch(string materialKey, ZoningMeshBatch batch)
        {
            if (batch == null || batch.Vertices.Count < 3 || batch.Triangles.Count < 3)
                return 0;

            var name = "zoning_batch_" + SanitizeObjectName(materialKey) + "_" + batch.ChunkIndex;

            var obj = new GameObject(name);
            obj.transform.SetParent(transform, true);

            var mesh = new Mesh();
            mesh.name = name + "_mesh";
            mesh.vertices = batch.Vertices.ToArray();
            mesh.triangles = batch.Triangles.ToArray();
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();

            var filter = obj.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            var renderer = obj.AddComponent<MeshRenderer>();
            renderer.material = batch.Material;

            LogVerboseOverlay(
                "GroundOverlay: zoning batch created: " + name +
                ", polygons=" + batch.PolygonCount +
                ", vertices=" + batch.Vertices.Count +
                ", triangles=" + (batch.Triangles.Count / 3)
            );

            batch.ChunkIndex++;
            batch.PolygonCount = 0;
            batch.Vertices.Clear();
            batch.Triangles.Clear();

            return 1;
        }

        private static string ResolveZoningMaterialKey(string zone, string cs2)
        {
            var label = string.IsNullOrWhiteSpace(cs2) ? "" : cs2.ToLowerInvariant();

            if (label.Contains("résidentiel") || label.Contains("residentiel"))
            {
                if (label.Contains("haute"))
                    return "residential_high";

                if (label.Contains("moyenne"))
                    return "residential_medium";

                return "residential_low";
            }

            if (label.Contains("commercial basse"))
                return "commercial_low";

            if (label.Contains("commerce de détail") || label.Contains("commerce de detail"))
                return "retail_detail";

            if (label.Contains("industrie"))
                return "industrial";

            if (label.Contains("bureau") || label.Contains("administration"))
                return "office";

            if (label.Contains("parking"))
            {
                if (label.Contains("ouvrage"))
                    return "parking_ramp";

                return "parking_surface";
            }

            var value = string.IsNullOrWhiteSpace(zone) ? "" : zone.ToLowerInvariant();

            switch (value)
            {
                case "low":
                    return "residential_low";

                case "medium":
                    return "residential_medium";

                case "high":
                    return "residential_high";

                case "industrial":
                    return "industrial";

                case "retail":
                    return "retail_detail";

                case "office":
                    return "office";

                case "surface":
                    return "parking_surface";

                case "ramp":
                    return "parking_ramp";

                default:
                    return "fallback";
            }
        }




        private static List<int> MakeDoubleSidedTriangles(List<int> triangles)
        {
            var result = new List<int>();

            if (triangles == null || triangles.Count < 3)
                return result;

            result.AddRange(triangles);

            for (var i = 0; i + 2 < triangles.Count; i += 3)
            {
                result.Add(triangles[i]);
                result.Add(triangles[i + 2]);
                result.Add(triangles[i + 1]);
            }

            return result;
        }


        private static List<int> TriangulatePolygonXZ(List<Vector3> vertices)
        {
            var result = new List<int>();

            if (vertices == null || vertices.Count < 3)
                return result;

            var indices = new List<int>();

            for (var i = 0; i < vertices.Count; i++)
            {
                if (indices.Count > 0 && IsSameXZ(vertices[indices[indices.Count - 1]], vertices[i]))
                    continue;

                indices.Add(i);
            }

            if (indices.Count >= 2 && IsSameXZ(vertices[indices[0]], vertices[indices[indices.Count - 1]]))
                indices.RemoveAt(indices.Count - 1);

            if (indices.Count < 3)
                return result;

            var area = SignedAreaXZ(vertices, indices);

            if (Mathf.Abs(area) < 0.0001f)
                return result;

            var ccw = area > 0f;
            var guard = 0;
            var maxGuard = indices.Count * indices.Count;

            while (indices.Count > 3 && guard < maxGuard)
            {
                var earFound = false;

                for (var i = 0; i < indices.Count; i++)
                {
                    var prevIndex = indices[(i - 1 + indices.Count) % indices.Count];
                    var currIndex = indices[i];
                    var nextIndex = indices[(i + 1) % indices.Count];

                    var prev = vertices[prevIndex];
                    var curr = vertices[currIndex];
                    var next = vertices[nextIndex];

                    if (!IsConvexXZ(prev, curr, next, ccw))
                        continue;

                    var containsPoint = false;

                    for (var j = 0; j < indices.Count; j++)
                    {
                        var testIndex = indices[j];

                        if (testIndex == prevIndex || testIndex == currIndex || testIndex == nextIndex)
                            continue;

                        if (PointInTriangleXZ(vertices[testIndex], prev, curr, next))
                        {
                            containsPoint = true;
                            break;
                        }
                    }

                    if (containsPoint)
                        continue;

                    if (ccw)
                    {
                        result.Add(prevIndex);
                        result.Add(currIndex);
                        result.Add(nextIndex);
                    }
                    else
                    {
                        result.Add(prevIndex);
                        result.Add(nextIndex);
                        result.Add(currIndex);
                    }

                    indices.RemoveAt(i);
                    earFound = true;
                    break;
                }

                if (!earFound)
                    break;

                guard++;
            }

            if (indices.Count == 3)
            {
                if (ccw)
                {
                    result.Add(indices[0]);
                    result.Add(indices[1]);
                    result.Add(indices[2]);
                }
                else
                {
                    result.Add(indices[0]);
                    result.Add(indices[2]);
                    result.Add(indices[1]);
                }
            }

            return result;
        }

        private static bool IsSameXZ(Vector3 a, Vector3 b)
        {
            var dx = a.x - b.x;
            var dz = a.z - b.z;

            return dx * dx + dz * dz < 0.0001f;
        }

        private static float SignedAreaXZ(List<Vector3> vertices, List<int> indices)
        {
            var area = 0f;

            for (var i = 0; i < indices.Count; i++)
            {
                var a = vertices[indices[i]];
                var b = vertices[indices[(i + 1) % indices.Count]];

                area += a.x * b.z - b.x * a.z;
            }

            return area * 0.5f;
        }

        private static bool IsConvexXZ(Vector3 a, Vector3 b, Vector3 c, bool ccw)
        {
            var cross = CrossXZ(a, b, c);

            return ccw ? cross > 0.00001f : cross < -0.00001f;
        }

        private static float CrossXZ(Vector3 a, Vector3 b, Vector3 c)
        {
            return (b.x - a.x) * (c.z - a.z) - (b.z - a.z) * (c.x - a.x);
        }

        private static bool PointInTriangleXZ(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
        {
            const float epsilon = 0.00001f;

            var c1 = CrossXZ(a, b, p);
            var c2 = CrossXZ(b, c, p);
            var c3 = CrossXZ(c, a, p);

            var hasNegative = c1 < -epsilon || c2 < -epsilon || c3 < -epsilon;
            var hasPositive = c1 > epsilon || c2 > epsilon || c3 > epsilon;

            return !(hasNegative && hasPositive);
        }


        private static bool ShouldRenderZoningByFilter(
            string zone,
            string cs2,
            string materialKey,
            string filterZone,
            string filterCs2Contains,
            string filterMaterialKey)
        {
            if (!MatchesZoningExactFilter(zone, filterZone))
                return false;

            if (!MatchesZoningContainsFilter(cs2, filterCs2Contains))
                return false;

            if (!MatchesZoningExactFilter(materialKey, filterMaterialKey))
                return false;

            return true;
        }

        private static bool MatchesZoningExactFilter(string value, string filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
                return true;

            var normalizedFilter = filter.Trim().ToLowerInvariant();

            if (normalizedFilter == "all" || normalizedFilter == "*")
                return true;

            var normalizedValue = string.IsNullOrWhiteSpace(value)
                ? "(unknown)"
                : value.Trim().ToLowerInvariant();

            var parts = normalizedFilter.Split(',');

            for (var i = 0; i < parts.Length; i++)
            {
                var wanted = string.IsNullOrWhiteSpace(parts[i])
                    ? "(unknown)"
                    : parts[i].Trim().ToLowerInvariant();

                if (wanted == "all" || wanted == "*" || wanted == normalizedValue)
                    return true;
            }

            return false;
        }

        private static bool MatchesZoningContainsFilter(string value, string filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
                return true;

            var normalizedFilter = filter.Trim().ToLowerInvariant();

            if (normalizedFilter == "all" || normalizedFilter == "*")
                return true;

            var normalizedValue = string.IsNullOrWhiteSpace(value)
                ? ""
                : value.Trim().ToLowerInvariant();

            var parts = normalizedFilter.Split(',');

            for (var i = 0; i < parts.Length; i++)
            {
                var wanted = string.IsNullOrWhiteSpace(parts[i])
                    ? ""
                    : parts[i].Trim().ToLowerInvariant();

                if (wanted == "all" || wanted == "*")
                    return true;

                if (wanted.Length > 0 && normalizedValue.IndexOf(wanted, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }



        private void LogZoningMaterialLegend()
        {
            LogVerboseOverlay(
                "GroundOverlay: zoning material legend: " +
                "residential_low=green, " +
                "residential_medium=yellow_green, " +
                "residential_high=yellow, " +
                "commercial_low=blue_violet, " +
                "retail_detail=magenta, " +
                "industrial=orange, " +
                "office=blue, " +
                "parking_surface=gray, " +
                "parking_ramp=dark_gray, " +
                "fallback=white"
            );
        }


        private static Material ResolveZoningMaterial(
            string zone,
            string cs2,
            Material residentialLowMaterial,
            Material residentialMediumMaterial,
            Material residentialHighMaterial,
            Material commercialLowMaterial,
            Material retailDetailMaterial,
            Material industrialMaterial,
            Material officeMaterial,
            Material surfaceMaterial,
            Material rampMaterial,
            Material fallbackMaterial
        )
        {
            var label = string.IsNullOrWhiteSpace(cs2) ? "" : cs2.ToLowerInvariant();

            if (label.Contains("résidentiel") || label.Contains("residentiel"))
            {
                if (label.Contains("haute"))
                    return residentialHighMaterial;

                if (label.Contains("moyenne"))
                    return residentialMediumMaterial;

                return residentialLowMaterial;
            }

            if (label.Contains("commercial basse"))
                return commercialLowMaterial;

            if (label.Contains("commerce de détail") || label.Contains("commerce de detail"))
                return retailDetailMaterial;

            if (label.Contains("industrie"))
                return industrialMaterial;

            if (label.Contains("bureau") || label.Contains("administration"))
                return officeMaterial;

            if (label.Contains("parking"))
            {
                if (label.Contains("ouvrage"))
                    return rampMaterial;

                return surfaceMaterial;
            }

            var value = string.IsNullOrWhiteSpace(zone) ? "" : zone.ToLowerInvariant();

            switch (value)
            {
                case "low":
                    return residentialLowMaterial;

                case "medium":
                    return residentialMediumMaterial;

                case "high":
                    return residentialHighMaterial;

                case "industrial":
                    return industrialMaterial;

                case "retail":
                    return retailDetailMaterial;

                case "office":
                    return officeMaterial;

                case "surface":
                    return surfaceMaterial;

                case "ramp":
                    return rampMaterial;

                default:
                    return fallbackMaterial;
            }
        }


        private static string SanitizeObjectName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "unknown";

            return value
                .ToLowerInvariant()
                .Replace(" ", "_")
                .Replace("-", "_")
                .Replace("/", "_")
                .Replace("\\", "_");
        }


        private void RenderWaterOverlayGroups(
            Material waterLineMaterial,
            Material waterAreaOutlineMaterial,
            Material waterAreaFillMaterial,
            int stride,
            ref int createdWaterSegments,
            ref int createdWaterLines,
            ref Vector3 firstWorld,
            ref Vector3 lastWorld,
            ref bool hasEndpoints
        )
        {
            RenderWaterGroup(
                _waterLines,
                "cyan_water_line_segment_",
                waterLineMaterial,
                _config.WaterLineSegmentWidth,
                _config.WaterLineSegmentHeight,
                _config.WaterLineYOffset,
                stride,
                ref createdWaterSegments,
                ref createdWaterLines,
                ref firstWorld,
                ref lastWorld,
                ref hasEndpoints
            );

            if (_config.RenderWaterAreas && _config.RenderWaterAreaFillMeshes)
                RenderWaterAreaFillMeshes(_waterAreaOutlines, waterAreaFillMaterial, stride);

            if (_config.RenderWaterAreas && _config.RenderWaterAreaOutlines)
            {
                RenderWaterGroup(
                    _waterAreaOutlines,
                    "blue_water_area_segment_",
                    waterAreaOutlineMaterial,
                    _config.WaterAreaSegmentWidth,
                    _config.WaterAreaSegmentHeight,
                    _config.WaterAreaOutlineYOffset,
                    stride,
                    ref createdWaterSegments,
                    ref createdWaterLines,
                    ref firstWorld,
                    ref lastWorld,
                    ref hasEndpoints
                );
            }
        }



        private sealed class WaterAreaFillMeshBatch
        {
            internal readonly List<Vector3> Vertices = new List<Vector3>();
            internal readonly List<int> Triangles = new List<int>();
            internal Material Material;
            internal int ChunkIndex;
            internal int PolygonCount;
        }

        private int RenderWaterAreaFillMeshes(
            List<List<GeoPoint>> outlines,
            Material material,
            int stride
        )
        {
            if (outlines == null || outlines.Count == 0)
                return 0;

            var originLon = _config.UseGeoJsonCenter ? _bounds.CenterLon : _config.OriginLon;
            var originLat = _config.UseGeoJsonCenter ? _bounds.CenterLat : _config.OriginLat;

            var sourceOutlines = outlines.Count;
            var renderedPolygons = 0;
            var skippedInvalid = 0;
            var skippedByLimit = 0;
            var createdMeshObjects = 0;

            var maxPolygons = Math.Max(1, _config.MaxWaterAreaFillMeshesDebug);
            var safeStride = Math.Max(1, stride);
            const int maxVerticesPerMesh = 60000;

            var batch = new WaterAreaFillMeshBatch();
            batch.Material = material;

            foreach (var outline in outlines)
            {
                if (outline == null || outline.Count < 3)
                {
                    skippedInvalid++;
                    continue;
                }

                if (renderedPolygons >= maxPolygons)
                {
                    skippedByLimit++;
                    continue;
                }

                var vertices = new List<Vector3>();

                for (var i = 0; i < outline.Count; i += safeStride)
                {
                    var world = GeoTransform.ToWorld(outline[i], _config, originLon, originLat);
                    world.y = ResolveY(world) + _config.GroundMargin + _config.WaterAreaFillYOffset;
                    vertices.Add(world);

                    if (vertices.Count >= maxVerticesPerMesh)
                        break;
                }

                if (vertices.Count < 3)
                {
                    skippedInvalid++;
                    continue;
                }

                if (batch.Vertices.Count + vertices.Count >= maxVerticesPerMesh)
                    createdMeshObjects += FlushWaterAreaFillBatch(batch);

                var vertexOffset = batch.Vertices.Count;
                batch.Vertices.AddRange(vertices);

                var localTriangles = MakeDoubleSidedTriangles(TriangulatePolygonXZ(vertices));

                if (localTriangles.Count < 3)
                {
                    skippedInvalid++;
                    continue;
                }

                for (var i = 0; i < localTriangles.Count; i++)
                    batch.Triangles.Add(vertexOffset + localTriangles[i]);

                batch.PolygonCount++;
                renderedPolygons++;
            }

            createdMeshObjects += FlushWaterAreaFillBatch(batch);

            LogVerboseOverlay(
                "GroundOverlay: water area fill batching summary: source=" + sourceOutlines +
                ", rendered=" + renderedPolygons +
                ", meshObjects=" + createdMeshObjects +
                ", skippedInvalid=" + skippedInvalid +
                ", skippedByLimit=" + skippedByLimit +
                ", maxWaterAreaFillMeshesDebug=" + _config.MaxWaterAreaFillMeshesDebug
            );

            LogVerboseOverlay("GroundOverlay: created water area fill meshes=" + createdMeshObjects);

            return createdMeshObjects;
        }

        private int FlushWaterAreaFillBatch(WaterAreaFillMeshBatch batch)
        {
            if (batch == null || batch.Vertices.Count < 3 || batch.Triangles.Count < 3)
                return 0;

            var name = "water_area_fill_batch_" + batch.ChunkIndex;

            var obj = new GameObject(name);
            obj.transform.SetParent(transform, true);

            var mesh = new Mesh();
            mesh.name = name + "_mesh";
            mesh.vertices = batch.Vertices.ToArray();
            mesh.triangles = batch.Triangles.ToArray();
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();

            var filter = obj.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            var renderer = obj.AddComponent<MeshRenderer>();
            renderer.material = batch.Material;

            LogVerboseOverlay(
                "GroundOverlay: water area fill batch created: " + name +
                ", polygons=" + batch.PolygonCount +
                ", vertices=" + batch.Vertices.Count +
                ", triangles=" + (batch.Triangles.Count / 3)
            );

            batch.ChunkIndex++;
            batch.PolygonCount = 0;
            batch.Vertices.Clear();
            batch.Triangles.Clear();

            return 1;
        }


        private sealed class WaterMeshBatch
        {
            internal readonly List<Vector3> Vertices = new List<Vector3>();
            internal readonly List<int> Triangles = new List<int>();
            internal Material Material;
            internal int ChunkIndex;
            internal int SegmentCount;
        }

        private void RenderWaterGroup(
            List<List<GeoPoint>> lines,
            string namePrefix,
            Material material,
            float segmentWidth,
            float segmentHeight,
            float yOffset,
            int stride,
            ref int createdWaterSegments,
            ref int createdWaterLines,
            ref Vector3 firstWorld,
            ref Vector3 lastWorld,
            ref bool hasEndpoints
        )
        {
            if (lines == null || lines.Count == 0)
                return;

            var originLon = _config.UseGeoJsonCenter ? _bounds.CenterLon : _config.OriginLon;
            var originLat = _config.UseGeoJsonCenter ? _bounds.CenterLat : _config.OriginLat;

            var safeStride = Math.Max(1, stride);
            var maxSegments = Math.Max(1, _config.MaxWaterSegmentsDebug);
            const int maxVerticesPerMesh = 60000;

            var groupLines = 0;
            var groupSegments = 0;
            var groupMeshObjects = 0;

            var batch = new WaterMeshBatch();
            batch.Material = material;

            foreach (var line in lines)
            {
                if (line == null || line.Count < 2)
                    continue;

                createdWaterLines++;
                groupLines++;

                for (var i = 0; i < line.Count - safeStride; i += safeStride)
                {
                    if (createdWaterSegments >= maxSegments)
                        break;

                    var a = GeoTransform.ToWorld(line[i], _config, originLon, originLat);
                    var b = GeoTransform.ToWorld(line[Math.Min(i + safeStride, line.Count - 1)], _config, originLon, originLat);

                    a.y = ResolveY(a) + _config.GroundMargin + yOffset;
                    b.y = ResolveY(b) + _config.GroundMargin + yOffset;

                    if (!hasEndpoints)
                    {
                        firstWorld = a;
                        hasEndpoints = true;
                    }

                    lastWorld = b;

                    if (batch.Vertices.Count + 8 >= maxVerticesPerMesh)
                        groupMeshObjects += FlushWaterBatch(namePrefix, batch);

                    if (AppendWaterSegmentBox(
                        batch,
                        a,
                        b,
                        segmentWidth,
                        segmentHeight
                    ))
                    {
                        createdWaterSegments++;
                        groupSegments++;
                    }
                }

                if (createdWaterSegments >= maxSegments)
                    break;
            }

            groupMeshObjects += FlushWaterBatch(namePrefix, batch);

            LogVerboseOverlay(
                "GroundOverlay: water batching summary: prefix=" + namePrefix +
                ", lines=" + groupLines +
                ", segments=" + groupSegments +
                ", meshObjects=" + groupMeshObjects +
                ", maxWaterSegmentsDebug=" + _config.MaxWaterSegmentsDebug
            );
        }

        private bool AppendWaterSegmentBox(
            WaterMeshBatch batch,
            Vector3 a,
            Vector3 b,
            float segmentWidth,
            float segmentHeight
        )
        {
            if (batch == null)
                return false;

            var dx = b.x - a.x;
            var dz = b.z - a.z;
            var lengthSq = dx * dx + dz * dz;

            if (lengthSq < 0.01f)
                return false;

            var length = Mathf.Sqrt(lengthSq);
            var ux = dx / length;
            var uz = dz / length;

            var width = Mathf.Max(0.1f, segmentWidth);
            var height = Mathf.Max(0.1f, segmentHeight);

            var overlap = width * 0.35f;
            var halfOverlap = overlap * 0.5f;

            var ax = a.x - ux * halfOverlap;
            var az = a.z - uz * halfOverlap;
            var bx = b.x + ux * halfOverlap;
            var bz = b.z + uz * halfOverlap;

            var px = -uz * width * 0.5f;
            var pz = ux * width * 0.5f;

            var baseIndex = batch.Vertices.Count;

            var aLeftBottom = new Vector3(ax + px, a.y, az + pz);
            var aRightBottom = new Vector3(ax - px, a.y, az - pz);
            var bLeftBottom = new Vector3(bx + px, b.y, bz + pz);
            var bRightBottom = new Vector3(bx - px, b.y, bz - pz);

            var aLeftTop = new Vector3(aLeftBottom.x, aLeftBottom.y + height, aLeftBottom.z);
            var aRightTop = new Vector3(aRightBottom.x, aRightBottom.y + height, aRightBottom.z);
            var bLeftTop = new Vector3(bLeftBottom.x, bLeftBottom.y + height, bLeftBottom.z);
            var bRightTop = new Vector3(bRightBottom.x, bRightBottom.y + height, bRightBottom.z);

            batch.Vertices.Add(aLeftBottom);
            batch.Vertices.Add(aRightBottom);
            batch.Vertices.Add(bLeftBottom);
            batch.Vertices.Add(bRightBottom);
            batch.Vertices.Add(aLeftTop);
            batch.Vertices.Add(aRightTop);
            batch.Vertices.Add(bLeftTop);
            batch.Vertices.Add(bRightTop);

            AddQuad(batch.Triangles, baseIndex + 4, baseIndex + 6, baseIndex + 7, baseIndex + 5); // top
            AddQuad(batch.Triangles, baseIndex + 0, baseIndex + 1, baseIndex + 3, baseIndex + 2); // bottom
            AddQuad(batch.Triangles, baseIndex + 0, baseIndex + 2, baseIndex + 6, baseIndex + 4); // left
            AddQuad(batch.Triangles, baseIndex + 1, baseIndex + 5, baseIndex + 7, baseIndex + 3); // right
            AddQuad(batch.Triangles, baseIndex + 0, baseIndex + 4, baseIndex + 5, baseIndex + 1); // start
            AddQuad(batch.Triangles, baseIndex + 2, baseIndex + 3, baseIndex + 7, baseIndex + 6); // end

            batch.SegmentCount++;

            return true;
        }

        private int FlushWaterBatch(string namePrefix, WaterMeshBatch batch)
        {
            if (batch == null || batch.Vertices.Count < 3 || batch.Triangles.Count < 3)
                return 0;

            var name = SanitizeObjectName(namePrefix) + "batch_" + batch.ChunkIndex;

            var obj = new GameObject(name);
            obj.transform.SetParent(transform, true);

            var mesh = new Mesh();
            mesh.name = name + "_mesh";
            mesh.vertices = batch.Vertices.ToArray();
            mesh.triangles = batch.Triangles.ToArray();
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();

            var filter = obj.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            var renderer = obj.AddComponent<MeshRenderer>();
            renderer.material = batch.Material;

            LogVerboseOverlay(
                "GroundOverlay: water batch created: " + name +
                ", segments=" + batch.SegmentCount +
                ", vertices=" + batch.Vertices.Count +
                ", triangles=" + (batch.Triangles.Count / 3)
            );

            batch.ChunkIndex++;
            batch.SegmentCount = 0;
            batch.Vertices.Clear();
            batch.Triangles.Clear();

            return 1;
        }



        private void UpdateOverlayHud()
        {
            // Ancien HUD TextMesh supprimé.
            // Le panneau interactif est rendu par OnGUI().
        }


        private static void ApplyControlPanelGuiSkin()
        {
            if (GUI.skin == null)
                return;

            GUI.skin.window.fontSize = 16;
            GUI.skin.label.fontSize = 16;
            GUI.skin.toggle.fontSize = 16;
            GUI.skin.button.fontSize = 16;

            GUI.skin.horizontalSlider.fixedHeight = 18f;
            GUI.skin.horizontalSliderThumb.fixedWidth = 18f;
            GUI.skin.horizontalSliderThumb.fixedHeight = 18f;
        }

        private void NormalizeControlPanelRect()
        {
            var hudWidth = Mathf.Min(680f, Mathf.Max(360f, Screen.width - 16f));
            var hudHeight = Mathf.Min(900f, Mathf.Max(420f, Screen.height - 16f));

            if (_controlPanelRect.width >= hudWidth && _controlPanelRect.height >= hudHeight)
                return;

            var x = Mathf.Clamp(_controlPanelRect.x, 4f, Mathf.Max(4f, Screen.width - hudWidth - 4f));
            var y = Mathf.Clamp(_controlPanelRect.y, 4f, Mathf.Max(4f, Screen.height - hudHeight - 4f));

            _controlPanelRect = new Rect(x, y, hudWidth, hudHeight);
        }

        private void OnGUI()
        {
            if (_config == null || !_created || !_config.ShowOverlayHud)
                return;

            if (!_controlPanelLogOnce)
            {
                _controlPanelLogOnce = true;
                Log.Info("GroundOverlay HUD panel: opened.");
            }

            ApplyControlPanelGuiSkin();
            NormalizeControlPanelRect();

            _controlPanelRect = GUI.Window(
                93014,
                _controlPanelRect,
                DrawControlPanelWindow,
                "CityTimelineMod — Panneau HUD"
            );
        }

        private void DrawControlPanelWindow(int windowId)
        {
            var visibilityChanged = false;
            var alphaChanged = false;

            _controlPanelScroll = GUILayout.BeginScrollView(
                _controlPanelScroll,
                GUILayout.Width(_controlPanelRect.width - 18f),
                GUILayout.Height(_controlPanelRect.height - 38f)
            );

            DrawDisplayPresetButtons();
            DrawVisualSettingsStatus();

            GUILayout.Space(12f);
            GUILayout.Label("Couches visibles");

            visibilityChanged |= DrawLayerToggle("Zonage", ref _config.RenderZoning);
            visibilityChanged |= DrawLayerToggle("Routes", ref _config.RenderRoads);
            visibilityChanged |= DrawLayerToggle("Chemins", ref _config.RenderPaths);

            var waterVisible = _config.RenderWaterLines && _config.RenderWaterAreas;
            var newWaterVisible = GUILayout.Toggle(waterVisible, "Eau");
            if (newWaterVisible != waterVisible)
            {
                _config.RenderWaterLines = newWaterVisible;
                _config.RenderWaterAreas = newWaterVisible;
                visibilityChanged = true;
            }

            visibilityChanged |= DrawLayerToggle("Limites worldmap / heightmap", ref _config.RenderMapBounds);

            GUILayout.Space(12f);
            GUILayout.Label("Transparence");

            alphaChanged |= DrawFloatSlider("Alpha zonage", ref _config.ZoningAlpha, 0f, 1f, "0.00");
            alphaChanged |= DrawFloatSlider("Alpha routes", ref _config.RoadAlpha, 0f, 1f, "0.00");
            alphaChanged |= DrawFloatSlider("Alpha chemins", ref _config.PathAlpha, 0f, 1f, "0.00");

            var waterMainAlpha = _config.WaterLineAlpha;
            if (DrawFloatSlider("Alpha eau lignes/contours", ref waterMainAlpha, 0f, 1f, "0.00"))
            {
                _config.WaterLineAlpha = waterMainAlpha;
                _config.WaterAreaOutlineAlpha = waterMainAlpha;
                alphaChanged = true;
            }

            alphaChanged |= DrawFloatSlider("Alpha eau remplissage", ref _config.WaterAreaFillAlpha, 0f, 1f, "0.00");

            if (DrawFloatSlider("Alpha limites", ref _config.MapBoundsAlpha, 0f, 1f, "0.00"))
            {
                alphaChanged = true;
                _controlPanelRebuildPending = true;
            }

            GUILayout.Space(12f);
            GUILayout.Label("Hauteur");

            if (DrawFloatSlider("Ground margin", ref _config.GroundMargin, 0f, 600f, "0"))
            {
                _controlPanelRebuildPending = true;
            }

            GUILayout.Space(12f);
            GUILayout.Label("Performance");

            if (DrawIntSlider("Budget zonage", ref _config.MaxZoningFillMeshesDebug, 0, 20000))
            {
                _controlPanelRebuildPending = true;
            }

            if (DrawIntSlider("Budget routes/chemins", ref _config.MaxRoadSegmentsDebug, 1000, 800000))
            {
                _controlPanelRebuildPending = true;
            }

            if (DrawIntSlider("Budget eau segments", ref _config.MaxWaterSegmentsDebug, 1000, 400000))
            {
                _controlPanelRebuildPending = true;
            }

            if (DrawIntSlider("Budget eau surfaces", ref _config.MaxWaterAreaFillMeshesDebug, 0, 2000))
            {
                _controlPanelRebuildPending = true;
            }

            if (DrawIntSlider("Niveau routes", ref _config.MinimumRoadDebugTier, 0, 4))
            {
                _controlPanelRebuildPending = true;
            }

            if (DrawIntSlider("Pas / pointStride", ref _config.PointStride, 1, 20))
            {
                _controlPanelRebuildPending = true;
            }

            GUILayout.Space(12f);
            GUILayout.Label("Calage avancé");

            _controlPanelCalibrationUnlocked = GUILayout.Toggle(
                _controlPanelCalibrationUnlocked,
                "Déverrouiller le calage"
            );

            if (_controlPanelCalibrationUnlocked)
            {
                if (DrawFloatSlider("Offset X", ref _config.WorldOriginX, -5000f, 5000f, "0.0"))
                {
                    _controlPanelRebuildPending = true;
                }

                if (DrawFloatSlider("Offset Z", ref _config.WorldOriginZ, -5000f, 5000f, "0.0"))
                {
                    _controlPanelRebuildPending = true;
                }

                if (DrawFloatSlider("Rotation degrés", ref _config.OverlayRotationDegrees, -30f, 30f, "0.00"))
                {
                    _controlPanelRebuildPending = true;
                }

                if (DrawFloatSlider("Scale X", ref _config.OverlayScaleX, 0.5f, 1.5f, "0.000"))
                {
                    _controlPanelRebuildPending = true;
                }

                if (DrawFloatSlider("Scale Z", ref _config.OverlayScaleZ, 0.5f, 1.5f, "0.000"))
                {
                    _controlPanelRebuildPending = true;
                }

                GUILayout.Label(
                    "worldScale : " +
                    _config.WorldScale.ToString("0.000000", System.Globalization.CultureInfo.InvariantCulture) +
                    " / lecture seule"
                );
            }
            else
            {
                GUILayout.Label("Calage verrouillé.");
            }

            GUILayout.Space(12f);

            GUILayout.BeginHorizontal();

            if (GUILayout.Button(_controlPanelRebuildPending ? "Appliquer / reconstruire *" : "Appliquer / reconstruire"))
            {
                var hadPendingChanges = _controlPanelRebuildPending;
                _controlPanelRebuildPending = false;

                if (hadPendingChanges)
                    MarkVisualSettingsDirty("Réglages reconstruits — sauvegarde optionnelle.");

                Log.Info("GroundOverlay HUD panel: rebuild requested.");
                RebuildOverlayPreservingLiveTransform();
            }

            if (GUILayout.Button("Fermer"))
            {
                _config.ShowOverlayHud = false;
                _controlPanelLogOnce = false;
                Log.Info("GroundOverlay HUD panel: closed.");
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(4f);

            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Sauvegarder visuels"))
            {
                _config.SaveVisualSettingsToConfig();
                _visualSettingsDirty = false;
                _visualSettingsStatusMessage = "Visuels sauvegardés dans config.json.";
                Log.Info("GroundOverlay HUD panel: visual settings saved from HUD.");
            }

            if (GUILayout.Button("Recharger visuels"))
            {
                _config.LoadVisualSettingsFromConfig();
                SyncVisibilityStateFromConfig();
                _currentDisplayPresetLabel = "Config";
                _visualSettingsDirty = false;
                _visualSettingsStatusMessage = "Visuels rechargés depuis config.json.";
                _controlPanelRebuildPending = false;
                Log.Info("GroundOverlay HUD panel: visual settings reloaded from config.");
                RebuildOverlayPreservingLiveTransform();
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(4f);
            if (_controlPanelRebuildPending)
                GUILayout.Label("Changement en attente : cliquer Appliquer / reconstruire.");
            GUILayout.Label("Live HUD — sauvegarde visuelle optionnelle.");

            if (visibilityChanged)
            {
                SyncVisibilityStateFromConfig();
                MarkVisualSettingsDirty("Couches modifiées — non sauvegardées.");
                Log.Info(
                    "GroundOverlay HUD panel: visibility changed " +
                    "zoning=" + _config.RenderZoning +
                    ", roads=" + _config.RenderRoads +
                    ", paths=" + _config.RenderPaths +
                    ", waterLines=" + _config.RenderWaterLines +
                    ", waterAreas=" + _config.RenderWaterAreas +
                    ", bounds=" + _config.RenderMapBounds
                );
                RebuildOverlayPreservingLiveTransform();
            }
            else if (alphaChanged)
            {
                MarkVisualSettingsDirty("Transparence modifiée — non sauvegardée.");
                ApplyCurrentOverlayVisibilityToMaterials();
            }

            GUILayout.EndScrollView();

            GUI.DragWindow(new Rect(0f, 0f, 10000f, 24f));
        }
        private void DrawVisualSettingsStatus()
        {
            GUILayout.Space(6f);
            GUILayout.Label("Preset live : " + _currentDisplayPresetLabel);

            var status = _visualSettingsDirty
                ? "Modifié non sauvegardé"
                : _visualSettingsStatusMessage;

            GUILayout.Label("État live : " + status);
        }

        private void DrawDisplayPresetButtons()
        {
            GUILayout.Label("Presets affichage");

            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Debug complet"))
                ApplyDisplayPreset("debug");

            if (GUILayout.Button("Lisible"))
                ApplyDisplayPreset("readable");

            if (GUILayout.Button("Routes"))
                ApplyDisplayPreset("roads");

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Eau"))
                ApplyDisplayPreset("water");

            if (GUILayout.Button("Zoning"))
                ApplyDisplayPreset("zoning");

            if (GUILayout.Button("Bounds"))
                ApplyDisplayPreset("bounds");

            GUILayout.EndHorizontal();
        }

        private void ApplyDisplayPreset(string preset)
        {
            if (_config == null)
                return;

            var normalized = string.IsNullOrWhiteSpace(preset)
                ? string.Empty
                : preset.Trim().ToLowerInvariant();

            switch (normalized)
            {
                case "debug":
                    _config.RenderZoning = true;
                    _config.RenderRoads = true;
                    _config.RenderPaths = true;
                    SetWaterRenderFlags(true);
                    _config.RenderMapBounds = true;

                    _config.ZoningAlpha = 0.25f;
                    _config.RoadAlpha = 0.75f;
                    _config.PathAlpha = 0.55f;
                    _config.WaterLineAlpha = 0.75f;
                    _config.WaterAreaOutlineAlpha = 0.75f;
                    _config.WaterAreaFillAlpha = 0.25f;
                    _config.MapBoundsAlpha = 0.95f;
                    break;

                case "readable":
                    _config.RenderZoning = true;
                    _config.RenderRoads = true;
                    _config.RenderPaths = true;
                    SetWaterRenderFlags(true);
                    _config.RenderMapBounds = true;

                    _config.ZoningAlpha = 0.05f;
                    _config.RoadAlpha = 0.20f;
                    _config.PathAlpha = 0.35f;
                    _config.WaterLineAlpha = 0.20f;
                    _config.WaterAreaOutlineAlpha = 0.20f;
                    _config.WaterAreaFillAlpha = 0.05f;
                    _config.MapBoundsAlpha = 0.85f;
                    break;

                case "roads":
                    _config.RenderZoning = false;
                    _config.RenderRoads = true;
                    _config.RenderPaths = true;
                    SetWaterRenderFlags(false);
                    _config.RenderMapBounds = true;

                    _config.RoadAlpha = 0.75f;
                    _config.PathAlpha = 0.55f;
                    _config.MapBoundsAlpha = 0.60f;
                    break;

                case "water":
                    _config.RenderZoning = false;
                    _config.RenderRoads = false;
                    _config.RenderPaths = false;
                    SetWaterRenderFlags(true);
                    _config.RenderMapBounds = true;

                    _config.WaterLineAlpha = 0.80f;
                    _config.WaterAreaOutlineAlpha = 0.80f;
                    _config.WaterAreaFillAlpha = 0.22f;
                    _config.MapBoundsAlpha = 0.60f;
                    break;

                case "zoning":
                    _config.RenderZoning = true;
                    _config.RenderRoads = false;
                    _config.RenderPaths = false;
                    SetWaterRenderFlags(false);
                    _config.RenderMapBounds = true;

                    _config.ZoningAlpha = 0.18f;
                    _config.MapBoundsAlpha = 0.60f;
                    break;

                case "bounds":
                    _config.RenderZoning = false;
                    _config.RenderRoads = false;
                    _config.RenderPaths = false;
                    SetWaterRenderFlags(false);
                    _config.RenderMapBounds = true;

                    _config.MapBoundsAlpha = 0.95f;
                    break;

                default:
                    Log.Info("GroundOverlay HUD panel: unknown display preset=" + preset);
                    return;
            }

            SyncVisibilityStateFromConfig();
            _controlPanelRebuildPending = false;
            _currentDisplayPresetLabel = ResolveDisplayPresetLabel(normalized);
            _visualSettingsDirty = true;
            _visualSettingsStatusMessage = "Preset appliqué — non sauvegardé.";

            Log.Info(
                "GroundOverlay HUD panel: display preset applied=" + normalized +
                ", zoning=" + _config.RenderZoning +
                ", roads=" + _config.RenderRoads +
                ", paths=" + _config.RenderPaths +
                ", waterLines=" + _config.RenderWaterLines +
                ", waterAreas=" + _config.RenderWaterAreas +
                ", bounds=" + _config.RenderMapBounds
            );

            RebuildOverlayPreservingLiveTransform();
        }

        private static string ResolveDisplayPresetLabel(string preset)
        {
            switch (preset)
            {
                case "debug":
                    return "Debug complet";
                case "readable":
                    return "Lisible";
                case "roads":
                    return "Routes";
                case "water":
                    return "Eau";
                case "zoning":
                    return "Zoning";
                case "bounds":
                    return "Bounds";
                default:
                    return "Custom";
            }
        }

        private void MarkVisualSettingsDirty(string message)
        {
            _currentDisplayPresetLabel = "Custom";
            _visualSettingsDirty = true;
            _visualSettingsStatusMessage = string.IsNullOrWhiteSpace(message)
                ? "Modifié non sauvegardé"
                : message;
        }

        private static bool DrawLayerToggle(string label, ref bool value)
        {
            var next = GUILayout.Toggle(value, label);
            if (next == value)
                return false;

            value = next;
            return true;
        }

        private static bool DrawFloatSlider(string label, ref float value, float min, float max, string format)
        {
            var safeValue = Mathf.Clamp(value, min, max);

            GUILayout.Label(
                label + " : " +
                safeValue.ToString(format, System.Globalization.CultureInfo.InvariantCulture)
            );

            var next = GUILayout.HorizontalSlider(safeValue, min, max, GUILayout.Height(22f));
            GUILayout.Space(4f);

            if (System.Math.Abs(next - value) < 0.0001f)
                return false;

            value = next;
            return true;
        }

        private static bool DrawIntSlider(string label, ref int value, int min, int max)
        {
            var safeValue = Mathf.Clamp(value, min, max);

            GUILayout.Label(label + " : " + safeValue);

            var next = Mathf.RoundToInt(GUILayout.HorizontalSlider(safeValue, min, max, GUILayout.Height(22f)));
            GUILayout.Space(4f);

            if (next == value)
                return false;

            value = next;
            return true;
        }
        private void SyncVisibilityStateFromConfig()
        {
            _zoningVisible = _config.RenderZoning;
            _roadsVisible = _config.RenderRoads;
            _waterVisible = _config.RenderWaterLines || _config.RenderWaterAreas;
        }

        private void RenderMapBoundsOverlay(Material worldMapMaterial, Material heightMapMaterial, Material centerMaterial, double originLon, double originLat)
        {
            if (_config == null || !_config.RenderMapBounds)
                return;

            double worldMinLon = _config.WorldMapMinLon;
            double worldMinLat = _config.WorldMapMinLat;
            double worldMaxLon = _config.WorldMapMaxLon;
            double worldMaxLat = _config.WorldMapMaxLat;

            double heightMinLon = _config.HeightMapMinLon;
            double heightMinLat = _config.HeightMapMinLat;
            double heightMaxLon = _config.HeightMapMaxLon;
            double heightMaxLat = _config.HeightMapMaxLat;

            var mapBoundsCenterLon = originLon;
            var mapBoundsCenterLat = originLat;

            if (_config.MapBoundsUseCenterAndSize)
            {
                ComputeGeoSquareBounds(
                    mapBoundsCenterLon,
                    mapBoundsCenterLat,
                    _config.WorldMapSizeKm,
                    out worldMinLon,
                    out worldMinLat,
                    out worldMaxLon,
                    out worldMaxLat
                );

                ComputeGeoSquareBounds(
                    mapBoundsCenterLon,
                    mapBoundsCenterLat,
                    _config.HeightMapSizeKm,
                    out heightMinLon,
                    out heightMinLat,
                    out heightMaxLon,
                    out heightMaxLat
                );
            }

            RenderGeoBoundsRectangle(
                "worldmap_bounds",
                worldMinLon,
                worldMinLat,
                worldMaxLon,
                worldMaxLat,
                worldMapMaterial,
                originLon,
                originLat,
                9f
            );

            RenderGeoBoundsRectangle(
                "heightmap_bounds",
                heightMinLon,
                heightMinLat,
                heightMaxLon,
                heightMaxLat,
                heightMapMaterial,
                originLon,
                originLat,
                7f
            );

            var centerLon = _config.MapBoundsUseCenterAndSize ? mapBoundsCenterLon : (heightMinLon + heightMaxLon) * 0.5;
            var centerLat = _config.MapBoundsUseCenterAndSize ? mapBoundsCenterLat : (heightMinLat + heightMaxLat) * 0.5;
            var center = GeoToBoundsWorld(centerLon, centerLat, originLon, originLat, 0.2f);

            CreateBoundsMarker("map_bounds_center_marker", center, centerMaterial);

            LogVerboseOverlay(
                "GroundOverlay bounds: mode=" + (_config.MapBoundsUseCenterAndSize ? "center+size:origin" : "bbox") +
                ", originLon=" + originLon +
                ", originLat=" + originLat +
                ", boundsCenterLon=" + mapBoundsCenterLon +
                ", boundsCenterLat=" + mapBoundsCenterLat +
                ", worldMapSizeKm=" + _config.WorldMapSizeKm +
                ", heightMapSizeKm=" + _config.HeightMapSizeKm +
                ", worldMap=[" + worldMinLat + "," + worldMinLon + "," + worldMaxLat + "," + worldMaxLon + "]" +
                ", heightMap=[" + heightMinLat + "," + heightMinLon + "," + heightMaxLat + "," + heightMaxLon + "]"
            );
        }

        private static void ComputeGeoSquareBounds(double centerLon, double centerLat, float sizeKm, out double minLon, out double minLat, out double maxLon, out double maxLat)
        {
            var safeSizeKm = Math.Max(0.001, sizeKm);
            var halfKm = safeSizeKm * 0.5;

            const double kmPerDegreeLat = 111.32;
            var latRadians = centerLat * Math.PI / 180.0;
            var cosLat = Math.Cos(latRadians);

            if (Math.Abs(cosLat) < 0.000001)
                cosLat = 0.000001;

            var kmPerDegreeLon = kmPerDegreeLat * cosLat;

            var halfLatDegrees = halfKm / kmPerDegreeLat;
            var halfLonDegrees = halfKm / kmPerDegreeLon;

            minLon = centerLon - halfLonDegrees;
            maxLon = centerLon + halfLonDegrees;
            minLat = centerLat - halfLatDegrees;
            maxLat = centerLat + halfLatDegrees;
        }

        private void RenderGeoBoundsRectangle(string name, double minLon, double minLat, double maxLon, double maxLat, Material material, double originLon, double originLat, float width)
        {
            var southWest = GeoToBoundsWorld(minLon, minLat, originLon, originLat, 0f);
            var southEast = GeoToBoundsWorld(maxLon, minLat, originLon, originLat, 0f);
            var northEast = GeoToBoundsWorld(maxLon, maxLat, originLon, originLat, 0f);
            var northWest = GeoToBoundsWorld(minLon, maxLat, originLon, originLat, 0f);

            CreateBoundsSegment(name + "_south", southWest, southEast, material, width);
            CreateBoundsSegment(name + "_east", southEast, northEast, material, width);
            CreateBoundsSegment(name + "_north", northEast, northWest, material, width);
            CreateBoundsSegment(name + "_west", northWest, southWest, material, width);
        }

        private Vector3 GeoToBoundsWorld(double lon, double lat, double originLon, double originLat, float extraYOffset)
        {
            var point = new GeoPoint(lon, lat);
            var world = GeoTransform.ToWorld(point, _config, originLon, originLat);
            world.y = ResolveY(world) + _config.GroundMargin + _config.MapBoundsYOffset + extraYOffset;
            return world;
        }

        private void CreateBoundsSegment(string name, Vector3 a, Vector3 b, Material material, float width)
        {
            if (material == null)
                return;

            var mid = (a + b) * 0.5f;
            var delta = b - a;

            var sizeX = Mathf.Max(Mathf.Abs(delta.x), width);
            var sizeZ = Mathf.Max(Mathf.Abs(delta.z), width);

            if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.z))
                sizeZ = width;
            else
                sizeX = width;

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(transform, true);
            cube.transform.position = mid;
            cube.transform.localScale = new Vector3(sizeX, 4f, sizeZ);

            RemoveCollider(cube);
            ApplyMaterial(cube, material);
        }

        private void CreateBoundsMarker(string name, Vector3 center, Material material)
        {
            if (material == null)
                return;

            var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = name;
            marker.transform.SetParent(transform, true);
            marker.transform.position = center + Vector3.up * 40f;
            marker.transform.localScale = new Vector3(40f, 80f, 40f);

            RemoveCollider(marker);
            ApplyMaterial(marker, material);
        }

        private void ClearOverlayMaterialRegistries()
        {
            _zoningMaterials.Clear();
            _roadMaterials.Clear();
            _waterLineMaterials.Clear();
            _waterAreaOutlineMaterials.Clear();
            _waterAreaFillMaterials.Clear();
        }

        private void HandleOverlayVisibilityInput()
        {
            // Nouveau comportement :
            // Alt+H ouvre/ferme uniquement le panneau HUD interactif.
            // Les anciens raccourcis F10/F11/F12/Alt+B/Alt+D/etc. sont volontairement neutralisés.
            if (Input.GetKeyDown(KeyCode.H) && IsSaveOverlayVisualsModifierDown())
            {
                _config.ShowOverlayHud = !_config.ShowOverlayHud;
                _controlPanelLogOnce = false;
                Log.Info("GroundOverlay HUD panel: visible=" + _config.ShowOverlayHud);
            }
        }
        private static bool IsSaveOverlayVisualsModifierDown()
        {
            return Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        }

        private void SetWaterRenderFlags(bool visible)
        {
            if (_config == null)
                return;

            _config.RenderWaterLines = visible;
            _config.RenderWaterAreas = visible;
            _config.RenderWaterAreaOutlines = visible;
            _config.RenderWaterAreaFillMeshes = visible;
        }
        private void ApplyCurrentOverlayVisibilityToMaterials()
        {
            ApplyZoningVisibilityToMaterials();
            ApplyRoadVisibilityToMaterials();
            ApplyWaterVisibilityToMaterials();
        }

        private void ApplyZoningVisibilityToMaterials()
        {
            SetMaterialsAlpha(_zoningMaterials, _zoningVisible ? _config.ZoningAlpha : 0f);
        }

        private void ApplyRoadVisibilityToMaterials()
        {
            SetMaterialsAlpha(_roadMaterials, _roadsVisible ? _config.RoadAlpha : 0f);
            SetMaterialsAlpha(_pathMaterials, _roadsVisible ? _config.PathAlpha : 0f);
        }

        private void ApplyWaterVisibilityToMaterials()
        {
            SetMaterialsAlpha(_waterLineMaterials, _waterVisible ? _config.WaterLineAlpha : 0f);
            SetMaterialsAlpha(_waterAreaOutlineMaterials, _waterVisible ? _config.WaterAreaOutlineAlpha : 0f);
            SetMaterialsAlpha(_waterAreaFillMaterials, _waterVisible ? _config.WaterAreaFillAlpha : 0f);
        }

        private static void SetMaterialsAlpha(List<Material> materials, float alpha)
        {
            if (materials == null)
                return;

            var safeAlpha = Mathf.Clamp01(alpha);

            for (var i = 0; i < materials.Count; i++)
            {
                var material = materials[i];
                if (material == null)
                    continue;

                var color = material.color;
                color.a = safeAlpha;
                ApplyTransparentMaterialSettings(material, color);
            }
        }

        private void HandleCalibrationInput()
        {
            // Les raccourcis clavier de calibration sont désactivés.
            // Le calage doit passer par le panneau HUD interactif, avec déverrouillage explicite.
            return;
        }
        private void CycleCalibrationMode()
        {
            if (_activeCalibrationMode == CalibrationMode.Render)
                _activeCalibrationMode = CalibrationMode.Global;
            else
                _activeCalibrationMode = (CalibrationMode)((int)_activeCalibrationMode + 1);

            Log.Info("GroundOverlay calibration: active mode=" + GetCalibrationModeLabel(_activeCalibrationMode));
        }

        private void RebuildOverlayPreservingLiveTransform()
        {
            var livePosition = transform.position;
            var liveRotation = transform.rotation;
            var liveScale = transform.localScale;

            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one;

            ClearOverlayChildren();
            CreateOverlay();

            transform.position = livePosition;
            transform.rotation = liveRotation;
            transform.localScale = liveScale;

            Log.Info("GroundOverlay calibration: overlay rebuilt. mode=" + GetCalibrationModeLabel(_activeCalibrationMode));
        }

        private void ApplyLiveCalibrationTransform()
        {
            transform.position = new Vector3(
                _liveCalibrationOffset.x,
                0f,
                _liveCalibrationOffset.z
            );

            transform.rotation = Quaternion.Euler(
                0f,
                _liveCalibrationRotationDegrees,
                0f
            );

            transform.localScale = new Vector3(
                _liveCalibrationScaleX,
                1f,
                _liveCalibrationScaleZ
            );
        }

        private void LogLiveCalibrationState()
        {
            Log.Info(
                "GroundOverlay calibration LIVE: " +
                "mode=" + GetCalibrationModeLabel(_activeCalibrationMode) +
                ", offsetX=" + _liveCalibrationOffset.x +
                ", offsetZ=" + _liveCalibrationOffset.z +
                ", rotationDelta=" + _liveCalibrationRotationDegrees +
                ", scaleXDelta=" + _liveCalibrationScaleX +
                ", scaleZDelta=" + _liveCalibrationScaleZ
            );
        }

        private void LogCalibrationState()
        {
            Log.Info(
                "GroundOverlay calibration STATE: " +
                "mode=" + GetCalibrationModeLabel(_activeCalibrationMode) +
                ", offsetX=" + _liveCalibrationOffset.x +
                ", offsetZ=" + _liveCalibrationOffset.z +
                ", rotationDelta=" + _liveCalibrationRotationDegrees +
                ", scaleXDelta=" + _liveCalibrationScaleX +
                ", scaleZDelta=" + _liveCalibrationScaleZ +
                ", worldOriginX=" + _config.WorldOriginX +
                ", worldOriginZ=" + _config.WorldOriginZ +
                ", worldScale=" + _config.WorldScale +
                ", overlayRotationDegrees=" + _config.OverlayRotationDegrees +
                ", overlayScaleX=" + _config.OverlayScaleX +
                ", overlayScaleZ=" + _config.OverlayScaleZ +
                ", pointStride=" + _config.PointStride +
                ", waterWidth=" + _config.SegmentWidth +
                ", waterHeight=" + _config.SegmentHeight +
                ", roadWidth=" + _config.RoadSegmentWidth +
                ", roadHeight=" + _config.RoadSegmentHeight +
                ", waterLines=" + (_waterLines != null ? _waterLines.Count : 0) +
                ", roadLines=" + (_roadLines != null ? _roadLines.Count : 0)
            );
        }

        private static string GetCalibrationModeLabel(CalibrationMode mode)
        {
            switch (mode)
            {
                case CalibrationMode.Global:
                    return "Global";
                case CalibrationMode.Water:
                    return "Eau";
                case CalibrationMode.Roads:
                    return "Routes";
                case CalibrationMode.Render:
                    return "Rendu";
                default:
                    return mode.ToString();
            }
        }

        private void SaveAndBakeLiveCalibration()
        {
            if (_config == null)
                return;

            _config.WorldOriginX += _liveCalibrationOffset.x;
            _config.WorldOriginZ += _liveCalibrationOffset.z;

            _config.OverlayRotationDegrees += _liveCalibrationRotationDegrees;
            _config.OverlayScaleX *= _liveCalibrationScaleX;
            _config.OverlayScaleZ *= _liveCalibrationScaleZ;

            _config.SaveCalibrationToConfig();

            Log.Info(
                "GroundOverlay calibration SAVED: " +
                "worldOriginX=" + _config.WorldOriginX +
                ", worldOriginZ=" + _config.WorldOriginZ +
                ", overlayRotationDegrees=" + _config.OverlayRotationDegrees +
                ", overlayScaleX=" + _config.OverlayScaleX +
                ", overlayScaleZ=" + _config.OverlayScaleZ
            );

            _liveCalibrationOffset = Vector3.zero;
            _liveCalibrationRotationDegrees = 0f;
            _liveCalibrationScaleX = 1f;
            _liveCalibrationScaleZ = 1f;

            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one;

            ClearOverlayChildren();

            CreateOverlay();
        }

        private void ClearOverlayChildren()
        {
            var children = new List<GameObject>();

            for (var i = 0; i < transform.childCount; i++)
                children.Add(transform.GetChild(i).gameObject);

            foreach (var child in children)
                UnityEngine.Object.Destroy(child);

            Log.Info("GroundOverlay calibration: cleared children=" + children.Count);
        }

        private bool CreateOrientedWaterCube(
            string name,
            Vector3 a,
            Vector3 b,
            Material material,
            float waterWidth,
            float waterHeight
        )
        {
            var mid = (a + b) * 0.5f;

            var delta = b - a;
            delta.y = 0f;

            var length = delta.magnitude;

            if (length < 0.1f)
                return false;

            var width = Mathf.Max(0.1f, waterWidth);
            var height = Mathf.Max(0.1f, waterHeight);

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(transform, true);

            cube.transform.position = new Vector3(
                mid.x,
                mid.y + height * 0.5f,
                mid.z
            );

            cube.transform.rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);

            cube.transform.localScale = new Vector3(
                width,
                height,
                length + width * 0.35f
            );

            RemoveCollider(cube);
            ApplyMaterial(cube, material);

            if (_debugWaterSegmentLogs < 10)
            {
                _debugWaterSegmentLogs++;
                Log.Info(
                    "GroundOverlay: CREATED oriented water cube " +
                    name +
                    " pos=" + cube.transform.position +
                    " scale=" + cube.transform.localScale +
                    " length=" + length
                );
            }

            return true;
        }

        private bool CreateWaterDebugCube(
            string name,
            Vector3 a,
            Vector3 b,
            Material material
        )
        {
            var mid = (a + b) * 0.5f;
            var delta = b - a;

            var sizeX = Mathf.Abs(delta.x) + _config.SegmentWidth;
            var sizeZ = Mathf.Abs(delta.z) + _config.SegmentWidth;

            sizeX = Mathf.Max(sizeX, _config.SegmentWidth);
            sizeZ = Mathf.Max(sizeZ, _config.SegmentWidth);

            var height = _config.SegmentHeight;

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(transform, true);

            cube.transform.position = new Vector3(
                mid.x,
                mid.y + height * 0.5f,
                mid.z
            );

            cube.transform.rotation = Quaternion.identity;
            cube.transform.localScale = new Vector3(sizeX, height, sizeZ);

            RemoveCollider(cube);
            ApplyMaterial(cube, material);

            if (_debugWaterSegmentLogs < 5)
            {
                _debugWaterSegmentLogs++;
                Log.Info(
                    "GroundOverlay: CREATED water debug cube " +
                    name +
                    " pos=" + cube.transform.position +
                    " scale=" + cube.transform.localScale
                );
            }

            return true;
        }

        private bool CreateOrientedRoadCube(
            string name,
            Vector3 a,
            Vector3 b,
            Material material,
            float roadWidth,
            float roadHeight
        )
        {
            var mid = (a + b) * 0.5f;

            var delta = b - a;
            delta.y = 0f;

            var length = delta.magnitude;

            if (length < 0.1f)
                return false;

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(transform, true);

            cube.transform.position = new Vector3(
                mid.x,
                mid.y + roadHeight * 0.5f,
                mid.z
            );

            cube.transform.rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);

            // Local X = largeur de route en mètres CS2
            // Local Y = hauteur debug
            // Local Z = longueur du segment
            //
            // On ajoute un léger recouvrement pour éviter les micro-coupures
            // entre deux segments successifs.
            cube.transform.localScale = new Vector3(
                roadWidth,
                roadHeight,
                length + roadWidth * 0.35f
            );

            RemoveCollider(cube);
            ApplyMaterial(cube, material);

            if (_debugRoadSegmentLogs < 10)
            {
                _debugRoadSegmentLogs++;
                Log.Info(
                    "GroundOverlay: CREATED oriented road cube " +
                    name +
                    " pos=" + cube.transform.position +
                    " scale=" + cube.transform.localScale +
                    " length=" + length
                );
            }

            return true;
        }

        private void CreateBeacon(string name, Vector3 basePosition, Material material)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(transform, true);
            cube.transform.position = basePosition + Vector3.up * (_config.BeaconHeight * 0.5f);
            cube.transform.localScale = new Vector3(_config.BeaconWidth, _config.BeaconHeight, _config.BeaconWidth);

            RemoveCollider(cube);
            ApplyMaterial(cube, material);

            Log.Info("GroundOverlay: beacon created: " + name + " at " + cube.transform.position);
        }

        private void CreateTower(string name, Vector3 basePosition, Material material)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(transform, true);
            cube.transform.position = basePosition + Vector3.up * (_config.TowerHeight * 0.5f);
            cube.transform.localScale = new Vector3(_config.TowerWidth, _config.TowerHeight, _config.TowerWidth);

            RemoveCollider(cube);
            ApplyMaterial(cube, material);

            Log.Info("GroundOverlay: tower created: " + name + " at " + cube.transform.position);
        }

        private static float ResolveY(Vector3 world)
        {
            float y;
            if (TerrainHeightSampler.TrySampleHeight(new Vector3(world.x, 0f, world.z), out y))
                return y;

            return 0f;
        }

        private static void RemoveCollider(GameObject obj)
        {
            var collider = obj.GetComponent<Collider>();
            if (collider != null)
                UnityEngine.Object.Destroy(collider);
        }

        private static void ApplyMaterial(GameObject obj, Material material)
        {
            var renderer = obj.GetComponent<Renderer>();
            if (renderer != null && material != null)
                renderer.sharedMaterial = material;
        }

        private static Material CreateMaterial(Color color)
        {
            Shader shader = Shader.Find("Unlit/Transparent");

            if (shader == null)
                shader = Shader.Find("Sprites/Default");

            if (shader == null)
                shader = Shader.Find("Legacy Shaders/Transparent/Diffuse");

            if (shader == null)
                shader = Shader.Find("Legacy Shaders/Transparent/VertexLit");

            if (shader == null)
                shader = Shader.Find("Hidden/Internal-Colored");

            if (shader == null)
                shader = Shader.Find("Standard");

            if (shader == null)
                shader = Shader.Find("HDRP/Unlit");

            if (shader == null)
                return null;

            var material = new Material(shader);
            ApplyTransparentMaterialSettings(material, color);
            return material;
        }

        private static void ApplyTransparentMaterialSettings(Material material, Color color)
        {
            if (material == null)
                return;

            material.color = color;

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);

            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);

            if (material.HasProperty("_SurfaceType"))
                material.SetFloat("_SurfaceType", 1f);

            if (material.HasProperty("_BlendMode"))
                material.SetFloat("_BlendMode", 0f);

            if (material.HasProperty("_Surface"))
                material.SetFloat("_Surface", 1f);

            if (material.HasProperty("_Blend"))
                material.SetFloat("_Blend", 0f);

            if (material.HasProperty("_Mode"))
                material.SetFloat("_Mode", 3f);

            if (material.HasProperty("_SrcBlend"))
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);

            if (material.HasProperty("_DstBlend"))
                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);

            if (material.HasProperty("_ZWrite"))
                material.SetFloat("_ZWrite", 0f);

            if (material.HasProperty("_AlphaCutoffEnable"))
                material.SetFloat("_AlphaCutoffEnable", 0f);

            material.SetOverrideTag("RenderType", "Transparent");

            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        private static GeoBounds CalculateBounds(List<List<GeoPoint>> lines)
        {
            var bounds = new GeoBounds();
            var initialized = false;

            if (lines != null)
            {
                foreach (var line in lines)
                {
                    if (line == null)
                        continue;

                    foreach (var point in line)
                    {
                        if (!initialized)
                        {
                            bounds.MinLon = point.Lon;
                            bounds.MaxLon = point.Lon;
                            bounds.MinLat = point.Lat;
                            bounds.MaxLat = point.Lat;
                            initialized = true;
                        }
                        else
                        {
                            if (point.Lon < bounds.MinLon) bounds.MinLon = point.Lon;
                            if (point.Lon > bounds.MaxLon) bounds.MaxLon = point.Lon;
                            if (point.Lat < bounds.MinLat) bounds.MinLat = point.Lat;
                            if (point.Lat > bounds.MaxLat) bounds.MaxLat = point.Lat;
                        }
                    }
                }
            }

            if (!initialized)
            {
                bounds.MinLon = 0.0;
                bounds.MaxLon = 0.0;
                bounds.MinLat = 0.0;
                bounds.MaxLat = 0.0;
                bounds.CenterLon = 0.0;
                bounds.CenterLat = 0.0;
                return bounds;
            }

            bounds.CenterLon = (bounds.MinLon + bounds.MaxLon) / 2.0;
            bounds.CenterLat = (bounds.MinLat + bounds.MaxLat) / 2.0;

            return bounds;
        }

        private sealed class GeoBounds
        {
            internal double MinLon;
            internal double MaxLon;
            internal double MinLat;
            internal double MaxLat;
            internal double CenterLon;
            internal double CenterLat;
        }
    }
}











