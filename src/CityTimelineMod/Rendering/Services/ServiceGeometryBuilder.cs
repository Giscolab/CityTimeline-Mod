using System;
using System.Collections.Generic;
using CityTimelineMod.Config;
using UnityEngine;

namespace CityTimelineMod.Rendering.Services
{
    internal static class ServiceGeometryBuilder
    {
        internal static List<ServiceRenderChunk> BuildChunks(
            IList<GeoServicePoint> points,
            GeoOverlayConfig config,
            double originLon,
            double originLat,
            float chunkSize,
            ServiceRenderCounters counters
        )
        {
            var chunks = new Dictionary<string, ServiceRenderChunk>(StringComparer.Ordinal);
            var ordered = new List<ServiceRenderChunk>();
            var safeChunkSize = Mathf.Max(64f, chunkSize);

            if (points == null || config == null)
                return ordered;

            if (counters != null)
                counters.SourcePoints += points.Count;

            var clipToWorldMap = config.WorldMapSizeKm > 0f &&
                !float.IsNaN(config.WorldMapSizeKm) &&
                !float.IsInfinity(config.WorldMapSizeKm);
            var worldMapHalfExtent = config.WorldMapSizeKm * 500f;

            for (var i = 0; i < points.Count; i++)
            {
                var point = points[i];
                if (point == null || point.Point == null)
                {
                    if (counters != null)
                        counters.SkippedInvalid++;
                    continue;
                }

                var world = GeoTransform.ToWorld(point.Point, config, originLon, originLat);
                if (!IsFinite(world))
                {
                    if (counters != null)
                        counters.SkippedInvalid++;
                    continue;
                }

                if (clipToWorldMap &&
                    (Mathf.Abs(world.x - config.WorldOriginX) > worldMapHalfExtent ||
                     Mathf.Abs(world.z - config.WorldOriginZ) > worldMapHalfExtent))
                {
                    if (counters != null)
                        counters.SkippedOutsideMap++;
                    continue;
                }

                var chunkX = Mathf.FloorToInt(world.x / safeChunkSize);
                var chunkZ = Mathf.FloorToInt(world.z / safeChunkSize);
                var familyKey = string.IsNullOrWhiteSpace(point.FamilyKey) ? "unknown" : point.FamilyKey;
                var key = familyKey + ":" + chunkX + ":" + chunkZ;
                ServiceRenderChunk chunk;

                if (!chunks.TryGetValue(key, out chunk))
                {
                    chunk = new ServiceRenderChunk
                    {
                        ChunkX = chunkX,
                        ChunkZ = chunkZ,
                        FamilyKey = familyKey
                    };
                    chunks[key] = chunk;
                    ordered.Add(chunk);
                }

                chunk.Add(point, world);
                if (counters != null)
                    counters.EligiblePoints++;
            }

            ordered.Sort(CompareChunks);
            if (counters != null)
                counters.CreatedChunks += ordered.Count;
            return ordered;
        }

        internal static bool AppendMarker(
            ServicePointMeshBatch batch,
            Vector3 center,
            float size,
            ServiceMarkerShape shape
        )
        {
            switch (shape)
            {
                case ServiceMarkerShape.Cross:
                    return AppendCrossMarker(batch, center, size);
                case ServiceMarkerShape.Square:
                    return AppendSquareMarker(batch, center, size);
                default:
                    return AppendDiamondMarker(batch, center, size);
            }
        }

        internal static bool AppendDiamondMarker(ServicePointMeshBatch batch, Vector3 center, float size)
        {
            if (batch == null || !IsFinite(center))
                return false;

            var radius = Mathf.Max(0.5f, size) * 0.5f;
            var baseIndex = batch.Vertices.Count;
            batch.Vertices.Add(new Vector3(center.x, center.y, center.z + radius));
            batch.Vertices.Add(new Vector3(center.x + radius, center.y, center.z));
            batch.Vertices.Add(new Vector3(center.x, center.y, center.z - radius));
            batch.Vertices.Add(new Vector3(center.x - radius, center.y, center.z));
            AddQuad(batch.Triangles, baseIndex);
            batch.PointCount++;
            return true;
        }

        internal static bool AppendSquareMarker(ServicePointMeshBatch batch, Vector3 center, float size)
        {
            if (batch == null || !IsFinite(center))
                return false;

            var half = Mathf.Max(0.5f, size) * 0.5f;
            var baseIndex = batch.Vertices.Count;
            batch.Vertices.Add(new Vector3(center.x - half, center.y, center.z + half));
            batch.Vertices.Add(new Vector3(center.x + half, center.y, center.z + half));
            batch.Vertices.Add(new Vector3(center.x + half, center.y, center.z - half));
            batch.Vertices.Add(new Vector3(center.x - half, center.y, center.z - half));
            AddQuad(batch.Triangles, baseIndex);
            batch.PointCount++;
            return true;
        }

        internal static bool AppendCrossMarker(ServicePointMeshBatch batch, Vector3 center, float size)
        {
            if (batch == null || !IsFinite(center))
                return false;

            var half = Mathf.Max(0.5f, size) * 0.5f;
            var arm = Mathf.Max(0.2f, half * 0.35f);
            AppendRectangle(batch, center, half, arm);
            AppendRectangle(batch, center, arm, half);
            batch.PointCount++;
            return true;
        }

        private static void AppendRectangle(
            ServicePointMeshBatch batch,
            Vector3 center,
            float halfX,
            float halfZ
        )
        {
            var baseIndex = batch.Vertices.Count;
            batch.Vertices.Add(new Vector3(center.x - halfX, center.y, center.z + halfZ));
            batch.Vertices.Add(new Vector3(center.x + halfX, center.y, center.z + halfZ));
            batch.Vertices.Add(new Vector3(center.x + halfX, center.y, center.z - halfZ));
            batch.Vertices.Add(new Vector3(center.x - halfX, center.y, center.z - halfZ));
            AddQuad(batch.Triangles, baseIndex);
        }

        private static void AddQuad(List<int> triangles, int baseIndex)
        {
            triangles.Add(baseIndex);
            triangles.Add(baseIndex + 1);
            triangles.Add(baseIndex + 2);
            triangles.Add(baseIndex);
            triangles.Add(baseIndex + 2);
            triangles.Add(baseIndex + 3);
        }

        private static int CompareChunks(ServiceRenderChunk left, ServiceRenderChunk right)
        {
            var family = string.Compare(left.FamilyKey, right.FamilyKey, StringComparison.Ordinal);
            if (family != 0)
                return family;

            var x = left.ChunkX.CompareTo(right.ChunkX);
            return x != 0 ? x : left.ChunkZ.CompareTo(right.ChunkZ);
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                   !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }
    }
}
