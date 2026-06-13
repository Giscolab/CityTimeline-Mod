using System;
using System.Collections.Generic;
using CityTimelineMod.Geometry;
using CityTimelineMod.Rendering.Batching;
using CityTimelineMod.Rendering.Zoning;
using CityTimelineMod.Util;
using UnityEngine;

namespace CityTimelineMod.Rendering
{
    internal sealed partial class GroundOverlayBehaviour
    {
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
            var renderAllPolygons = ShouldRenderAllZoningPolygons();
            var safeStride = Math.Max(1, stride);
            const int maxVerticesPerMesh = 60000;

            foreach (var polygon in _zoningPolygons)
            {
                if (polygon == null || polygon.Rings == null || polygon.Rings.Count == 0)
                    continue;

                if (!ZoningFilterRules.ShouldRenderByFilter(
                    polygon.Zone,
                    polygon.Cs2,
                    ZoningMaterialResolver.ResolveMaterialKey(polygon.Zone, polygon.Cs2),
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

                if (!renderAllPolygons && renderedPolygons >= maxPolygons)
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

                var triangles = PolygonTriangulator.MakeDoubleSidedTriangles(
                    PolygonTriangulator.TriangulatePolygonXZ(vertices)
                );

                if (triangles.Count < 3)
                    continue;

                var materialKey = ZoningMaterialResolver.ResolveMaterialKey(polygon.Zone, polygon.Cs2);

                ZoningMeshBatch batch;

                if (!batches.TryGetValue(materialKey, out batch))
                {
                    batch = new ZoningMeshBatch();
                    batch.Material = ZoningMaterialResolver.ResolveMaterial(
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
                    createdMeshObjects += OverlayMeshFlusher.FlushZoningBatch(transform, materialKey, batch, LogVerboseOverlay);

                var vertexOffset = batch.Vertices.Count;

                batch.Vertices.AddRange(vertices);

                for (var i = 0; i < triangles.Count; i++)
                    batch.Triangles.Add(vertexOffset + triangles[i]);

                batch.PolygonCount++;
                renderedPolygons++;
            }

            foreach (var pair in batches)
                createdMeshObjects += OverlayMeshFlusher.FlushZoningBatch(transform, pair.Key, pair.Value, LogVerboseOverlay);

            Log.Info(
                "GroundOverlay: zoning render summary: source=" + sourceMeshes +
                ", eligible=" + eligibleMeshes +
                ", rendered=" + renderedPolygons +
                ", meshObjects=" + createdMeshObjects +
                ", skippedByFilter=" + skippedByFilter +
                ", skippedByLimit=" + skippedByLimit +
                ", renderAllZoningPolygons=" + renderAllPolygons +
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
    }
}
