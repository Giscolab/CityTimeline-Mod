using System;
using System.Collections.Generic;
using CityTimelineMod.Importers;
using CityTimelineMod.Rendering.Batching;
using CityTimelineMod.Rendering.Core;
using CityTimelineMod.Rendering.Materials;
using CityTimelineMod.Rendering.Roads;
using CityTimelineMod.Roads;
using CityTimelineMod.Util;
using Game.Prefabs;
using Unity.Entities;
using UnityEngine;

namespace CityTimelineMod.Rendering
{
    internal sealed partial class GroundOverlayBehaviour
    {
        private readonly Dictionary<string, float> _roadPrefabWidthsByProfile =
            new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly HashSet<int> _loggedRoadPrefabWidthEntities = new HashSet<int>();
        private World _roadPrefabResolverWorld;
        private RoadPrefabResolver _roadPrefabResolver;
        private bool _roadPrefabWidthsAvailableForCurrentBuild;
        private bool _roadPrefabWidthFallbackLogged;

        private List<RoadRenderChunk> BuildRoadRenderChunks(bool isPath, double originLon, double originLat)
        {
            var chunks = new Dictionary<string, RoadRenderChunk>();
            var chunkSize = Mathf.Clamp(_config.RoadChunkSizeMeters, 128f, 4096f);

            if (_roadLines == null || _roadLines.Count == 0)
                return new List<RoadRenderChunk>();

            if ((isPath && !_config.RenderPaths) || (!isPath && !_config.RenderRoads))
                return new List<RoadRenderChunk>();

            foreach (var roadLine in _roadLines)
            {
                if (roadLine == null || roadLine.IsPath != isPath || roadLine.Points == null || roadLine.Points.Count < 2)
                    continue;

                // Filtering before spatial partitioning prevents thousands of
                // empty chunks from being visited over many frames when a layer
                // or semantic filter hides every line.
                if (!ShouldRenderRoadLineForOverlay(roadLine))
                    continue;

                var midpoint = ResolveRoadLineMidpointWorld(roadLine, originLon, originLat);
                var chunkX = _config.EnableRoadSpatialChunking ? Mathf.FloorToInt(midpoint.x / chunkSize) : 0;
                var chunkZ = _config.EnableRoadSpatialChunking ? Mathf.FloorToInt(midpoint.z / chunkSize) : 0;
                var key = chunkX + ":" + chunkZ;

                RoadRenderChunk chunk;
                if (!chunks.TryGetValue(key, out chunk))
                {
                    chunk = new RoadRenderChunk();
                    chunk.ChunkX = chunkX;
                    chunk.ChunkZ = chunkZ;
                    chunk.IsPath = isPath;
                    chunks[key] = chunk;
                }

                chunk.Lines.Add(roadLine);
                chunk.IncludePoint(midpoint);
                chunk.IncludePoint(GeoTransform.ToWorld(roadLine.Points[0], _config, originLon, originLat));
                chunk.IncludePoint(GeoTransform.ToWorld(roadLine.Points[roadLine.Points.Count - 1], _config, originLon, originLat));
            }

            var result = new List<RoadRenderChunk>(chunks.Values);
            result.Sort(CompareRoadRenderChunks);
            return result;
        }

        private static int CompareRoadRenderChunks(RoadRenderChunk a, RoadRenderChunk b)
        {
            if (a == null && b == null)
                return 0;

            if (a == null)
                return -1;

            if (b == null)
                return 1;

            var compareX = a.ChunkX.CompareTo(b.ChunkX);
            return compareX != 0 ? compareX : a.ChunkZ.CompareTo(b.ChunkZ);
        }

        private Vector3 ResolveRoadLineMidpointWorld(GeoRoadLine roadLine, double originLon, double originLat)
        {
            var points = roadLine.Points;
            var anchorIndex = Math.Min(points.Count - 2, Math.Max(0, points.Count / 2 - 1));
            var nextIndex = Math.Min(points.Count - 1, anchorIndex + 1);
            var a = GeoTransform.ToWorld(points[anchorIndex], _config, originLon, originLat);
            var b = GeoTransform.ToWorld(points[nextIndex], _config, originLon, originLat);
            return new Vector3((a.x + b.x) * 0.5f, 0f, (a.z + b.z) * 0.5f);
        }

        private static int CountRoadChunkLines(List<RoadRenderChunk> chunks)
        {
            if (chunks == null)
                return 0;

            var count = 0;

            for (var i = 0; i < chunks.Count; i++)
            {
                if (chunks[i] != null && chunks[i].Lines != null)
                    count += chunks[i].Lines.Count;
            }

            return count;
        }

        private int CountValidRoadLines(bool isPath)
        {
            if (_roadLines == null)
                return 0;

            var count = 0;

            for (var i = 0; i < _roadLines.Count; i++)
            {
                var roadLine = _roadLines[i];

                if (roadLine != null && roadLine.IsPath == isPath && ShouldRenderRoadLineForOverlay(roadLine))
                    count++;
            }

            return count;
        }

        private List<Vector3> BuildRoadRibbonWorldPoints(
            List<GeoPoint> road,
            bool isPath,
            int safeStride,
            double originLon,
            double originLat,
            bool renderAllSegments,
            int remainingSegments,
            out bool limitHit
        )
        {
            limitHit = false;
            var points = new List<Vector3>();

            if (road == null || road.Count < 2)
                return points;

            var maxSegments = renderAllSegments ? int.MaxValue : Math.Max(0, remainingSegments);
            var stride = Math.Max(1, safeStride);
            var index = 0;
            var appendedSegments = 0;

            points.Add(ResolveRoadRibbonWorldPoint(road[index], isPath, originLon, originLat));

            while (index < road.Count - 1)
            {
                if (appendedSegments >= maxSegments)
                {
                    limitHit = true;
                    break;
                }

                var nextIndex = Math.Min(index + stride, road.Count - 1);

                if (nextIndex <= index)
                    break;

                points.Add(ResolveRoadRibbonWorldPoint(road[nextIndex], isPath, originLon, originLat));
                index = nextIndex;
                appendedSegments++;
            }

            return points;
        }

        private Vector3 ResolveRoadRibbonWorldPoint(GeoPoint point, bool isPath, double originLon, double originLat)
        {
            var world = GeoTransform.ToWorld(point, _config, originLon, originLat);
            var yOffset = isPath ? _config.PathYOffset : _config.RoadYOffset;
            world.y = ResolveY(world) + _config.GroundMargin + yOffset;
            return world;
        }

        private float ResolvePrefabRoadWidth(GeoRoadLine roadLine, bool isPath)
        {
            var fallbackWidth = RoadRenderStyleResolver.ResolveRoadSegmentWidth(roadLine, isPath, _config);

            // Paths do not map to the driveable-road prefab resolver contract.
            if (isPath || roadLine == null)
                return fallbackWidth;

            var metadata = RoadImportMetadata.FromRoadLine(roadLine);

            if (metadata == null)
                return fallbackWidth;

            if (!_roadPrefabWidthsAvailableForCurrentBuild ||
                _roadPrefabResolverWorld == null ||
                !_roadPrefabResolverWorld.IsCreated ||
                _roadPrefabResolver == null)
            {
                return fallbackWidth;
            }

            var world = _roadPrefabResolverWorld;
            var resolver = _roadPrefabResolver;

            var profileKey = BuildRoadPrefabWidthProfileKey(metadata);
            float cachedWidth;

            if (_roadPrefabWidthsByProfile.TryGetValue(profileKey, out cachedWidth))
                return cachedWidth;

            try
            {
                var prefabEntity = resolver.ResolveForRoad(metadata);
                var entityManager = world.EntityManager;

                if (prefabEntity == Entity.Null ||
                    !entityManager.Exists(prefabEntity) ||
                    !entityManager.HasComponent<NetGeometryData>(prefabEntity))
                {
                    LogRoadPrefabWidthFallbackOnce("resolved prefab has no NetGeometryData");
                    return fallbackWidth;
                }

                var geometry = entityManager.GetComponentData<NetGeometryData>(prefabEntity);
                var prefabWidth = geometry.m_DefaultWidth;

                if (float.IsNaN(prefabWidth) || float.IsInfinity(prefabWidth) || prefabWidth <= 0f)
                {
                    LogRoadPrefabWidthFallbackOnce("resolved prefab returned an invalid m_DefaultWidth");
                    return fallbackWidth;
                }

                _roadPrefabWidthsByProfile[profileKey] = prefabWidth;

                if (_loggedRoadPrefabWidthEntities.Add(prefabEntity.Index))
                {
                    Log.Info(
                        "[RoadOverlayWidth] prefab=" + resolver.GetPrefabName(prefabEntity) +
                        ", entity=" + prefabEntity.Index +
                        ", defaultWidth=" + prefabWidth.ToString(
                            "0.###",
                            System.Globalization.CultureInfo.InvariantCulture
                        ) +
                        ", source=NetGeometryData.m_DefaultWidth"
                    );
                }

                return prefabWidth;
            }
            catch (Exception ex)
            {
                LogRoadPrefabWidthFallbackOnce("prefab width lookup failed: " + ex.Message);
                return fallbackWidth;
            }
        }

        private void PrepareRoadPrefabWidthResolutionForBuild()
        {
            World world;
            RoadPrefabResolver resolver;
            _roadPrefabWidthsAvailableForCurrentBuild =
                TryGetRoadPrefabResolver(out world, out resolver);
        }

        private bool TryGetRoadPrefabResolver(out World world, out RoadPrefabResolver resolver)
        {
            world = World.DefaultGameObjectInjectionWorld;
            resolver = null;

            if (world == null || !world.IsCreated || !Mod.IsActiveRuntimeWorld(world))
            {
                LogRoadPrefabWidthFallbackOnce("active gameplay ECS world is unavailable");
                return false;
            }

            if (!ReferenceEquals(world, _roadPrefabResolverWorld))
            {
                _roadPrefabResolverWorld = world;
                _roadPrefabResolver = null;
                _roadPrefabWidthsByProfile.Clear();
                _loggedRoadPrefabWidthEntities.Clear();
                _roadPrefabWidthFallbackLogged = false;
            }

            if (_roadPrefabResolver != null && _roadPrefabResolver.CandidateCount > 0)
            {
                resolver = _roadPrefabResolver;
                return true;
            }

            _roadPrefabResolver = null;

            try
            {
                var prefabSystem = world.GetExistingSystemManaged<PrefabSystem>();

                if (prefabSystem == null)
                {
                    LogRoadPrefabWidthFallbackOnce("PrefabSystem is unavailable");
                    return false;
                }

                _roadPrefabResolver = new RoadPrefabResolver(world.EntityManager, prefabSystem);

                if (!_roadPrefabResolver.TryInitialize())
                {
                    _roadPrefabResolver = null;
                    LogRoadPrefabWidthFallbackOnce("RoadPrefabResolver initialization returned no candidates");
                    return false;
                }

                resolver = _roadPrefabResolver;
                return true;
            }
            catch (Exception ex)
            {
                _roadPrefabResolver = null;
                LogRoadPrefabWidthFallbackOnce("RoadPrefabResolver initialization failed: " + ex.Message);
                return false;
            }
        }

        private void ResetRoadPrefabWidthResolution()
        {
            _roadPrefabResolver = null;
            _roadPrefabResolverWorld = null;
            _roadPrefabWidthsByProfile.Clear();
            _loggedRoadPrefabWidthEntities.Clear();
            _roadPrefabWidthsAvailableForCurrentBuild = false;
            _roadPrefabWidthFallbackLogged = false;
        }

        private static string BuildRoadPrefabWidthProfileKey(RoadImportMetadata metadata)
        {
            return metadata.Highway + "|" +
                metadata.TargetLaneCount + "|" +
                metadata.Oneway + "|" +
                metadata.Surface + "|" +
                metadata.Maxspeed + "|" +
                (metadata.Bridge || metadata.Tunnel);
        }

        private void LogRoadPrefabWidthFallbackOnce(string reason)
        {
            if (_roadPrefabWidthFallbackLogged)
                return;

            _roadPrefabWidthFallbackLogged = true;
            Log.Info(
                "[RoadOverlayWidth] using configured road width fallback; " + reason +
                ". The next overlay rebuild will retry prefab resolution."
            );
        }

        private int AppendRoadRibbonPolylineToBatch(
            RoadMeshBatch batch,
            string batchKey,
            List<Vector3> points,
            float segmentWidth,
            int maxVerticesPerMesh,
            bool texturedTertiary,
            out int flushedObjects
        )
        {
            flushedObjects = 0;

            if (batch == null || points == null || points.Count < 2)
                return 0;

            var appendedSegments = 0;
            var startIndex = 0;
            var safeMaxVertices = Math.Max(16, maxVerticesPerMesh);
            var ribbonYOffset = _config != null ? Mathf.Max(0f, _config.RibbonYOffset) : 0.05f;

            // Carried across slices so a polyline split over several meshes
            // keeps a continuous V and does not restart the asphalt pattern
            // at every batch boundary.
            var textureDistanceOffsetMeters = 0f;

            while (startIndex < points.Count - 1)
            {
                if (batch.Vertices.Count + 4 >= safeMaxVertices)
                    flushedObjects += OverlayMeshFlusher.FlushRoadBatch(OverlayRenderParent, batchKey, batch, LogVerboseOverlay);

                var availableVertices = Math.Max(4, safeMaxVertices - batch.Vertices.Count);
                var maxPointsThisBatch = Math.Max(2, availableVertices / 2);
                var endExclusive = Math.Min(points.Count, startIndex + maxPointsThisBatch);

                if (endExclusive - startIndex < 2)
                {
                    flushedObjects += OverlayMeshFlusher.FlushRoadBatch(OverlayRenderParent, batchKey, batch, LogVerboseOverlay);
                    continue;
                }

                var slice = startIndex == 0 && endExclusive == points.Count
                    ? points
                    : points.GetRange(startIndex, endExclusive - startIndex);

                if (texturedTertiary)
                {
                    float appendedDistanceMeters;

                    appendedSegments += RoadGeometryBuilder.AppendTexturedRoadPolylineRibbon(
                        batch,
                        slice,
                        segmentWidth,
                        ribbonYOffset,
                        RoadRenderStyleResolver.TertiaryTextureRepeatMeters,
                        textureDistanceOffsetMeters,
                        RoadRenderStyleResolver.TertiaryTextureUMin,
                        RoadRenderStyleResolver.TertiaryTextureUMax,
                        out appendedDistanceMeters
                    );

                    textureDistanceOffsetMeters += appendedDistanceMeters;
                }
                else
                {
                    appendedSegments += RoadGeometryBuilder.AppendRoadPolylineRibbon(
                        batch,
                        slice,
                        segmentWidth,
                        ribbonYOffset
                    );
                }

                startIndex = endExclusive - 1;
            }

            return appendedSegments;
        }

        private int RenderRoadChunk(
            RoadRenderChunk chunk,
            OverlayRenderMaterials materials,
            int stride,
            RoadRenderCounters counters
        )
        {
            if (chunk == null || chunk.Lines == null || chunk.Lines.Count == 0 || materials == null || counters == null)
                return 0;

            var isPath = chunk.IsPath;
            var safeStride = Math.Max(1, stride);
            var maxRoadSegments = Math.Max(1, _config.MaxRoadSegmentsDebug);
            var maxPathSegments = Math.Max(1, _config.MaxPathSegmentsDebug);
            var renderAllRoadSegments = ShouldRenderAllRoadSegments();
            var renderAllPathSegments = ShouldRenderAllPathSegments();
            const int maxVerticesPerMesh = 60000;
            var batches = new Dictionary<string, RoadMeshBatch>();
            var createdSegmentsBefore = counters.CreatedRoadSegments + counters.CreatedPathSegments;

            foreach (var roadLine in chunk.Lines)
            {
                var road = roadLine != null ? roadLine.Points : null;
                var highway = roadLine != null ? roadLine.Highway : null;

                if (isPath)
                    counters.SourcePathLines++;
                else
                    counters.SourceRoadLines++;

                if (road == null || road.Count < 2)
                {
                    counters.SkippedInvalid++;
                    continue;
                }

                if (isPath)
                {
                    if (!_config.RenderPaths)
                    {
                        counters.SkippedPathByFilter++;
                        continue;
                    }
                }
                else if (!_config.RenderRoads)
                {
                    counters.SkippedRoadByFilter++;
                    continue;
                }

                if (!isPath && !RoadRenderRules.ShouldRenderRoadByTier(highway, _config.MinimumRoadDebugTier))
                {
                    counters.SkippedRoadByTier++;
                    continue;
                }

                var highwayFilter = isPath ? _config.PathHighwayFilter : _config.RoadHighwayFilter;

                if (!RoadRenderRules.ShouldRenderHighwayByFilter(highway, highwayFilter))
                {
                    if (isPath)
                        counters.SkippedPathByFilter++;
                    else
                        counters.SkippedRoadByFilter++;

                    continue;
                }

                if (!ShouldRenderRoadBySemanticFilter(roadLine))
                {
                    if (isPath)
                        counters.SkippedPathByFilter++;
                    else
                        counters.SkippedRoadByFilter++;

                    continue;
                }

                if (isPath)
                    counters.EligiblePathLines++;
                else
                    counters.EligibleRoadLines++;

                var renderedSegmentsForLine = 0;
                var remainingSegments = isPath
                    ? maxPathSegments - counters.CreatedPathSegments
                    : maxRoadSegments - counters.CreatedRoadSegments;
                var renderAllSegments = isPath ? renderAllPathSegments : renderAllRoadSegments;
                bool limitHitForLine;
                var ribbonPoints = BuildRoadRibbonWorldPoints(
                    road,
                    isPath,
                    safeStride,
                    _progressiveRebuild.OriginLon,
                    _progressiveRebuild.OriginLat,
                    renderAllSegments,
                    remainingSegments,
                    out limitHitForLine
                );

                if (ribbonPoints.Count >= 2)
                {
                    var baseBatchKey = RoadRenderStyleResolver.ResolveRoadRenderBatchKey(roadLine, _config);

                    var texturedTertiary = !isPath && string.Equals(
                        baseBatchKey,
                        RoadRenderStyleResolver.TertiaryTexturedBatchKey,
                        StringComparison.Ordinal
                    );

                    var batchKey = baseBatchKey +
                        "_cx" + chunk.ChunkX +
                        "_cz" + chunk.ChunkZ;

                    RoadMeshBatch batch;

                    if (!batches.TryGetValue(batchKey, out batch))
                    {
                        batch = new RoadMeshBatch();
                        batch.Material = isPath
                            ? materials.Path
                            : RoadRenderStyleResolver.ResolveRoadRenderBatchMaterial(
                                roadLine,
                                _config,
                                materials.RoadMotorway,
                                materials.RoadPrimary,
                                materials.RoadSecondary,
                                materials.RoadTertiary,
                                materials.RoadTertiaryTextured,
                                materials.RoadLink,
                                materials.FallbackRoad,
                                materials.RoadOneWay,
                                materials.RoadBridge,
                                materials.RoadTunnel,
                                materials.RoadRoundabout
                            );

                        batches[batchKey] = batch;
                    }

                    var segmentWidth = ResolvePrefabRoadWidth(roadLine, isPath);
                    int flushedObjects;
                    var appendedSegments = AppendRoadRibbonPolylineToBatch(
                        batch,
                        batchKey,
                        ribbonPoints,
                        segmentWidth,
                        maxVerticesPerMesh,
                        texturedTertiary,
                        out flushedObjects
                    );

                    if (isPath)
                        counters.CreatedPathMeshObjects += flushedObjects;
                    else
                        counters.CreatedRoadMeshObjects += flushedObjects;

                    if (appendedSegments > 0)
                    {
                        if (isPath)
                            counters.CreatedPathSegments += appendedSegments;
                        else
                            counters.CreatedRoadSegments += appendedSegments;

                        renderedSegmentsForLine += appendedSegments;
                    }
                }

                if (renderedSegmentsForLine > 0)
                {
                    if (isPath)
                        counters.CreatedPathLines++;
                    else
                        counters.CreatedRoadLines++;
                }

                if (limitHitForLine)
                {
                    if (isPath)
                        counters.SkippedPathByLimit++;
                    else
                        counters.SkippedRoadByLimit++;
                }
            }

            foreach (var pair in batches)
            {
                var flushedObjects = OverlayMeshFlusher.FlushRoadBatch(OverlayRenderParent, pair.Key, pair.Value, LogVerboseOverlay);

                if (isPath)
                    counters.CreatedPathMeshObjects += flushedObjects;
                else
                    counters.CreatedRoadMeshObjects += flushedObjects;
            }

            return counters.CreatedRoadSegments + counters.CreatedPathSegments - createdSegmentsBefore;
        }

        private int RenderRoadGroup(
            Material roadMotorwayMaterial,
            Material roadPrimaryMaterial,
            Material roadSecondaryMaterial,
            Material roadTertiaryMaterial,
            Material roadTertiaryTexturedMaterial,
            Material roadLinkMaterial,
            Material pathMaterial,
            Material fallbackRoadMaterial,
            Material roadOneWayMaterial,
            Material roadBridgeMaterial,
            Material roadTunnelMaterial,
            Material roadRoundaboutMaterial,
            int stride,
            ref int createdRoadLines,
            ref int createdPathLines,
            ref int createdPathSegments
        )
        {
            var createdRoadSegments = 0;
            var createdRoadMeshObjects = 0;
            var createdPathMeshObjects = 0;
            var sourceRoadLines = 0;
            var sourcePathLines = 0;
            var eligibleRoadLines = 0;
            var eligiblePathLines = 0;
            var skippedRoadByFilter = 0;
            var skippedPathByFilter = 0;
            var skippedRoadByTier = 0;
            var skippedPathByTier = 0;
            var skippedRoadByLimit = 0;
            var skippedPathByLimit = 0;
            var skippedInvalid = 0;

            if ((!_config.RenderRoads && !_config.RenderPaths) || _roadLines == null || _roadLines.Count == 0)
                return 0;

            var originLon = _config.UseGeoJsonCenter ? _bounds.CenterLon : _config.OriginLon;
            var originLat = _config.UseGeoJsonCenter ? _bounds.CenterLat : _config.OriginLat;

            var safeStride = Math.Max(1, stride);
            var maxRoadSegments = Math.Max(1, _config.MaxRoadSegmentsDebug);
            var maxPathSegments = Math.Max(1, _config.MaxPathSegmentsDebug);
            var renderAllRoadSegments = ShouldRenderAllRoadSegments();
            var renderAllPathSegments = ShouldRenderAllPathSegments();
            const int maxVerticesPerMesh = 60000;

            var batches = new Dictionary<string, RoadMeshBatch>();

            foreach (var roadLine in _roadLines)
            {
                var road = roadLine != null ? roadLine.Points : null;
                var highway = roadLine != null ? roadLine.Highway : null;
                var isPath = roadLine != null && roadLine.IsPath;

                if (isPath)
                    sourcePathLines++;
                else
                    sourceRoadLines++;

                if (road == null || road.Count < 2)
                {
                    skippedInvalid++;
                    continue;
                }

                if (isPath)
                {
                    if (!_config.RenderPaths)
                    {
                        skippedPathByFilter++;
                        continue;
                    }
                }
                else if (!_config.RenderRoads)
                {
                    skippedRoadByFilter++;
                    continue;
                }

                if (!isPath && !RoadRenderRules.ShouldRenderRoadByTier(highway, _config.MinimumRoadDebugTier))
                {
                    skippedRoadByTier++;
                    continue;
                }

                var highwayFilter = isPath ? _config.PathHighwayFilter : _config.RoadHighwayFilter;

                if (!RoadRenderRules.ShouldRenderHighwayByFilter(highway, highwayFilter))
                {
                    if (isPath)
                        skippedPathByFilter++;
                    else
                        skippedRoadByFilter++;

                    continue;
                }

                if (!ShouldRenderRoadBySemanticFilter(roadLine))
                {
                    if (isPath)
                        skippedPathByFilter++;
                    else
                        skippedRoadByFilter++;

                    continue;
                }

                if (isPath)
                    eligiblePathLines++;
                else
                    eligibleRoadLines++;

                var renderedSegmentsForLine = 0;
                var remainingSegments = isPath
                    ? maxPathSegments - createdPathSegments
                    : maxRoadSegments - createdRoadSegments;
                var renderAllSegments = isPath ? renderAllPathSegments : renderAllRoadSegments;
                bool limitHitForLine;
                var ribbonPoints = BuildRoadRibbonWorldPoints(
                    road,
                    isPath,
                    safeStride,
                    originLon,
                    originLat,
                    renderAllSegments,
                    remainingSegments,
                    out limitHitForLine
                );

                if (ribbonPoints.Count >= 2)
                {
                    var baseBatchKey = RoadRenderStyleResolver.ResolveRoadRenderBatchKey(roadLine, _config);

                    var texturedTertiary = !isPath && string.Equals(
                        baseBatchKey,
                        RoadRenderStyleResolver.TertiaryTexturedBatchKey,
                        StringComparison.Ordinal
                    );

                    var batchKey = baseBatchKey;

                    RoadMeshBatch batch;

                    if (!batches.TryGetValue(batchKey, out batch))
                    {
                        batch = new RoadMeshBatch();
                        batch.Material = isPath
                            ? pathMaterial
                            : RoadRenderStyleResolver.ResolveRoadRenderBatchMaterial(
                                roadLine,
                                _config,
                                roadMotorwayMaterial,
                                roadPrimaryMaterial,
                                roadSecondaryMaterial,
                                roadTertiaryMaterial,
                                roadTertiaryTexturedMaterial,
                                roadLinkMaterial,
                                fallbackRoadMaterial,
                                roadOneWayMaterial,
                                roadBridgeMaterial,
                                roadTunnelMaterial,
                                roadRoundaboutMaterial
                            );

                        batches[batchKey] = batch;
                    }

                    var segmentWidth = ResolvePrefabRoadWidth(roadLine, isPath);
                    int flushedObjects;
                    var appendedSegments = AppendRoadRibbonPolylineToBatch(
                        batch,
                        batchKey,
                        ribbonPoints,
                        segmentWidth,
                        maxVerticesPerMesh,
                        texturedTertiary,
                        out flushedObjects
                    );

                    if (isPath)
                        createdPathMeshObjects += flushedObjects;
                    else
                        createdRoadMeshObjects += flushedObjects;

                    if (appendedSegments > 0)
                    {
                        if (isPath)
                            createdPathSegments += appendedSegments;
                        else
                            createdRoadSegments += appendedSegments;

                        renderedSegmentsForLine += appendedSegments;
                    }
                }

                if (renderedSegmentsForLine > 0)
                {
                    if (isPath)
                        createdPathLines++;
                    else
                        createdRoadLines++;
                }

                if (limitHitForLine)
                {
                    if (isPath)
                        skippedPathByLimit++;
                    else
                        skippedRoadByLimit++;
                }
            }

            foreach (var pair in batches)
            {
                var flushedObjects = OverlayMeshFlusher.FlushRoadBatch(OverlayRenderParent, pair.Key, pair.Value, LogVerboseOverlay);

                if (pair.Key == "path")
                    createdPathMeshObjects += flushedObjects;
                else
                    createdRoadMeshObjects += flushedObjects;
            }

            Log.Info(
                "GroundOverlay: road batching summary: roadLines=" + createdRoadLines +
                ", sourceRoads=" + sourceRoadLines +
                ", eligibleRoads=" + eligibleRoadLines +
                ", roadSegments=" + createdRoadSegments +
                ", meshObjects=" + createdRoadMeshObjects +
                ", maxRoadSegmentsDebug=" + _config.MaxRoadSegmentsDebug +
                ", renderAllRoadSegments=" + renderAllRoadSegments +
                ", roadRenderMode=" + ResolveRoadRenderMode(_config.RoadRenderMode) +
                ", skippedByFilter=" + skippedRoadByFilter +
                ", skippedByTier=" + skippedRoadByTier +
                ", skippedByLimit=" + skippedRoadByLimit +
                ", minimumRoadDebugTier=" + _config.MinimumRoadDebugTier
            );

            Log.Info(
                "GroundOverlay: path batching summary: pathLines=" + createdPathLines +
                ", sourcePaths=" + sourcePathLines +
                ", eligiblePaths=" + eligiblePathLines +
                ", pathSegments=" + createdPathSegments +
                ", meshObjects=" + createdPathMeshObjects +
                ", maxPathSegmentsDebug=" + _config.MaxPathSegmentsDebug +
                ", renderAllPathSegments=" + renderAllPathSegments +
                ", pathRenderMode=" + ResolveRoadRenderMode(_config.PathRenderMode) +
                ", skippedByFilter=" + skippedPathByFilter +
                ", skippedByTier=" + skippedPathByTier +
                ", skippedByLimit=" + skippedPathByLimit +
                ", skippedInvalid=" + skippedInvalid
            );

            return createdRoadSegments + createdPathSegments;
        }
    }
}
