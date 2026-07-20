using System;
using System.Collections.Generic;
using CityTimelineMod.Geometry;
using CityTimelineMod.Importers;
using CityTimelineMod.Rendering.Batching;
using CityTimelineMod.Rendering.Water;
using CityTimelineMod.Util;
using UnityEngine;

namespace CityTimelineMod.Rendering
{
    internal sealed partial class GroundOverlayBehaviour
    {
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
            if (_config.RenderWaterLines)
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
            }

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
            var renderAllPolygons = ShouldRenderAllWaterAreaFills();
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

                if (!renderAllPolygons && renderedPolygons >= maxPolygons)
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
                    createdMeshObjects += OverlayMeshFlusher.FlushWaterAreaFillBatch(transform, batch, LogVerboseOverlay);

                var vertexOffset = batch.Vertices.Count;
                batch.Vertices.AddRange(vertices);

                var localTriangles = PolygonTriangulator.MakeDoubleSidedTriangles(
                    PolygonTriangulator.TriangulatePolygonXZ(vertices)
                );

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

            createdMeshObjects += OverlayMeshFlusher.FlushWaterAreaFillBatch(transform, batch, LogVerboseOverlay);

            Log.Info(
                "GroundOverlay: water area fill batching summary: source=" + sourceOutlines +
                ", rendered=" + renderedPolygons +
                ", meshObjects=" + createdMeshObjects +
                ", skippedInvalid=" + skippedInvalid +
                ", skippedByLimit=" + skippedByLimit +
                ", renderAllWaterAreaFills=" + renderAllPolygons +
                ", maxWaterAreaFillMeshesDebug=" + _config.MaxWaterAreaFillMeshesDebug
            );

            LogVerboseOverlay("GroundOverlay: created water area fill meshes=" + createdMeshObjects);

            return createdMeshObjects;
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
            var renderAllSegments = ShouldRenderAllWaterSegments();
            const int maxVerticesPerMesh = 60000;

            var groupLines = 0;
            var groupSegments = 0;
            var groupMeshObjects = 0;
            var skippedByLimit = 0;

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
                    if (!renderAllSegments && createdWaterSegments >= maxSegments)
                    {
                        skippedByLimit++;
                        break;
                    }

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
                        groupMeshObjects += OverlayMeshFlusher.FlushWaterBatch(transform, namePrefix, batch, LogVerboseOverlay);

                    if (WaterGeometryBuilder.AppendWaterSegmentBox(
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

                if (!renderAllSegments && createdWaterSegments >= maxSegments)
                    break;
            }

            groupMeshObjects += OverlayMeshFlusher.FlushWaterBatch(transform, namePrefix, batch, LogVerboseOverlay);

            Log.Info(
                "GroundOverlay: water batching summary: prefix=" + namePrefix +
                ", lines=" + groupLines +
                ", segments=" + groupSegments +
                ", meshObjects=" + groupMeshObjects +
                ", skippedByLimit=" + skippedByLimit +
                ", renderAllWaterSegments=" + renderAllSegments +
                ", maxWaterSegmentsDebug=" + _config.MaxWaterSegmentsDebug
            );
        }
    }
}
