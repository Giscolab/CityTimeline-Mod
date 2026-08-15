using System;
using System.Collections.Generic;
using CityTimelineMod.Geometry;
using CityTimelineMod.Rendering.Batching;
using CityTimelineMod.Rendering.Core;
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
            Material commercialHighMaterial,
            Material retailDetailMaterial,
            Material industrialMaterial,
            Material officeMaterial,
            Material surfaceMaterial,
            Material rampMaterial,
            Material mixedMaterial,
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
            var sourceHoleRings = 0;
            var renderedHoleRings = 0;
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

                var materialKey = ZoningMaterialResolver.ResolveMaterialKey(polygon.Zone, polygon.Cs2);
                var sublayerKey = ResolveZoningOverlaySublayerKey(materialKey);
                if (!string.IsNullOrWhiteSpace(sublayerKey) && !ShouldRenderOverlaySublayer(sublayerKey))
                {
                    skippedByFilter++;
                    continue;
                }

                if (!ZoningFilterRules.ShouldRenderByFilter(
                    polygon.Zone,
                    polygon.Cs2,
                    materialKey,
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

                sourceHoleRings += Math.Max(0, polygon.Rings.Count - 1);
                var worldRings = BuildSampledZoningRings(
                    polygon.Rings,
                    safeStride,
                    maxVerticesPerMesh,
                    originLon,
                    originLat
                );
                if (worldRings.Count == 0 || worldRings[0].Count < 3)
                    continue;

                List<Vector3> vertices;
                var triangles = PolygonTriangulator.MakeDoubleSidedTriangles(
                    PolygonTriangulator.TriangulatePolygonWithHolesXZ(worldRings, out vertices)
                );

                if (triangles.Count < 3)
                    continue;

                renderedHoleRings += Math.Max(0, worldRings.Count - 1);

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
                        commercialHighMaterial,
                        retailDetailMaterial,
                        industrialMaterial,
                        officeMaterial,
                        surfaceMaterial,
                        rampMaterial,
                        mixedMaterial,
                        fallbackMaterial
                    );

                    batches[materialKey] = batch;
                }

                if (batch.Vertices.Count + vertices.Count >= maxVerticesPerMesh)
                    createdMeshObjects += OverlayMeshFlusher.FlushZoningBatch(OverlayRenderParent, materialKey, batch, LogVerboseOverlay);

                var vertexOffset = batch.Vertices.Count;

                batch.Vertices.AddRange(vertices);

                for (var i = 0; i < triangles.Count; i++)
                    batch.Triangles.Add(vertexOffset + triangles[i]);

                batch.PolygonCount++;
                renderedPolygons++;
            }

            foreach (var pair in batches)
                createdMeshObjects += OverlayMeshFlusher.FlushZoningBatch(OverlayRenderParent, pair.Key, pair.Value, LogVerboseOverlay);

            Log.Info(
                "GroundOverlay: zoning render summary: source=" + sourceMeshes +
                ", eligible=" + eligibleMeshes +
                ", rendered=" + renderedPolygons +
                ", meshObjects=" + createdMeshObjects +
                ", skippedByFilter=" + skippedByFilter +
                ", skippedByLimit=" + skippedByLimit +
                ", sourceHoleRings=" + sourceHoleRings +
                ", renderedHoleRings=" + renderedHoleRings +
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

        /// <summary>
        /// Traite progressivement les polygones de zoning, en construisant les batches
        /// par petits groupes afin de répartir la charge sur plusieurs frames.
        /// Retourne <c>true</c> lorsque tous les polygones ont été traités et les batches flushés.
        /// </summary>
        private bool BuildProgressiveZoningMeshes(
            ProgressiveOverlayRebuildState state,
            int maxPolygonsThisFrame)
        {
            if (state == null)
                return true;

            // Sécurité : si le zoning n'est pas activé ou aucune donnée, on considère terminé.
            if (!_config.RenderZoning || _zoningPolygons == null || _zoningPolygons.Count == 0)
                return true;

            var originLon = _config.UseGeoJsonCenter ? _bounds.CenterLon : _config.OriginLon;
            var originLat = _config.UseGeoJsonCenter ? _bounds.CenterLat : _config.OriginLat;

            var sourceMeshes = _zoningPolygons.Count;
            var maxPolygons = Math.Max(1, _config.MaxZoningFillMeshesDebug);
            var renderAllPolygons = ShouldRenderAllZoningPolygons();
            var safeStride = Math.Max(1, state.Stride);
            const int maxVerticesPerMesh = 60000;

            var processedThisFrame = 0;

            while (state.ZoningPolygonIndex < sourceMeshes && processedThisFrame < maxPolygonsThisFrame)
            {
                var polygon = _zoningPolygons[state.ZoningPolygonIndex];
                state.ZoningPolygonIndex++;
                processedThisFrame++;

                if (polygon == null || polygon.Rings == null || polygon.Rings.Count == 0)
                    continue;

                var materialKey = ZoningMaterialResolver.ResolveMaterialKey(polygon.Zone, polygon.Cs2);
                var sublayerKey = ResolveZoningOverlaySublayerKey(materialKey);
                if (!string.IsNullOrWhiteSpace(sublayerKey) && !ShouldRenderOverlaySublayer(sublayerKey))
                {
                    state.ZoningSkippedByFilter++;
                    continue;
                }

                if (!ZoningFilterRules.ShouldRenderByFilter(
                    polygon.Zone,
                    polygon.Cs2,
                    materialKey,
                    _config.ZoningDebugFilterZone,
                    _config.ZoningDebugFilterCs2Contains,
                    _config.ZoningDebugFilterMaterialKey))
                {
                    state.ZoningSkippedByFilter++;
                    continue;
                }

                state.ZoningEligibleMeshes++;

                var cs2Key = string.IsNullOrWhiteSpace(polygon.Cs2) ? "(missing)" : polygon.Cs2;
                int cs2Count;
                state.ZoningEligibleCs2Counts.TryGetValue(cs2Key, out cs2Count);
                state.ZoningEligibleCs2Counts[cs2Key] = cs2Count + 1;

                if (!renderAllPolygons && state.ZoningRenderedPolygons >= maxPolygons)
                {
                    state.ZoningSkippedByLimit++;
                    continue;
                }

                state.ZoningSourceHoleRings += Math.Max(0, polygon.Rings.Count - 1);
                var worldRings = BuildSampledZoningRings(
                    polygon.Rings,
                    safeStride,
                    maxVerticesPerMesh,
                    originLon,
                    originLat
                );
                if (worldRings.Count == 0 || worldRings[0].Count < 3)
                    continue;

                List<Vector3> vertices;
                var triangles = PolygonTriangulator.MakeDoubleSidedTriangles(
                    PolygonTriangulator.TriangulatePolygonWithHolesXZ(worldRings, out vertices)
                );

                if (triangles.Count < 3)
                    continue;

                state.ZoningRenderedHoleRings += Math.Max(0, worldRings.Count - 1);

                ZoningMeshBatch batch;
                if (!state.ZoningBatches.TryGetValue(materialKey, out batch))
                {
                    batch = new ZoningMeshBatch();
                    batch.Material = ZoningMaterialResolver.ResolveMaterial(
                        polygon.Zone,
                        polygon.Cs2,
                        state.Materials.ZoningResidentialLow,
                        state.Materials.ZoningResidentialMedium,
                        state.Materials.ZoningResidentialHigh,
                        state.Materials.ZoningCommercialLow,
                        state.Materials.ZoningCommercialHigh,
                        state.Materials.ZoningRetailDetail,
                        state.Materials.ZoningIndustrial,
                        state.Materials.ZoningOffice,
                        state.Materials.ZoningSurface,
                        state.Materials.ZoningRamp,
                        state.Materials.ZoningMixed,
                        state.Materials.ZoningFallback
                    );
                    state.ZoningBatches[materialKey] = batch;
                }

                if (batch.Vertices.Count + vertices.Count >= maxVerticesPerMesh)
                {
                    state.CreatedZoningFillMeshes +=
                        OverlayMeshFlusher.FlushZoningBatch(
                            OverlayRenderParent,
                            materialKey,
                            batch,
                            LogVerboseOverlay
                        );
                }

                var vertexOffset = batch.Vertices.Count;
                batch.Vertices.AddRange(vertices);

                for (var i = 0; i < triangles.Count; i++)
                    batch.Triangles.Add(vertexOffset + triangles[i]);

                batch.PolygonCount++;
                state.ZoningRenderedPolygons++;
            }

            // S'il reste des polygones, on rend la main à Unity.
            if (state.ZoningPolygonIndex < sourceMeshes)
                return false;

            // Tous les polygones ont été traités : on flush les batches restants et on finalise.
            var flushTiming = System.Diagnostics.Stopwatch.StartNew();

            foreach (var pair in state.ZoningBatches)
            {
                state.CreatedZoningFillMeshes +=
                    OverlayMeshFlusher.FlushZoningBatch(
                        OverlayRenderParent,
                        pair.Key,
                        pair.Value,
                        LogVerboseOverlay
                    );
            }

            flushTiming.Stop();

            Log.Info(
                "GroundOverlay: progressive zoning finished. source=" + sourceMeshes +
                ", eligible=" + state.ZoningEligibleMeshes +
                ", rendered=" + state.ZoningRenderedPolygons +
                ", meshObjects=" + state.CreatedZoningFillMeshes +
                ", skippedByFilter=" + state.ZoningSkippedByFilter +
                ", skippedByLimit=" + state.ZoningSkippedByLimit +
                ", sourceHoleRings=" + state.ZoningSourceHoleRings +
                ", renderedHoleRings=" + state.ZoningRenderedHoleRings +
                ", flushMs=" + flushTiming.ElapsedMilliseconds
            );

            LogVerboseOverlay(
                "GroundOverlay: eligible zoning cs2 summary: " +
                FormatRoadHighwayCounts(state.ZoningEligibleCs2Counts)
            );

            return true;
        }

        private List<List<Vector3>> BuildSampledZoningRings(
            List<List<Importers.GeoPoint>> sourceRings,
            int stride,
            int maxVertices,
            double originLon,
            double originLat)
        {
            var result = new List<List<Vector3>>();
            if (sourceRings == null || sourceRings.Count == 0)
                return result;

            var outer = BuildSampledZoningRing(
                sourceRings[0],
                stride,
                maxVertices,
                originLon,
                originLat
            );
            if (outer.Count < 3)
                return result;

            result.Add(outer);
            var usedVertices = outer.Count;

            for (var i = 1; i < sourceRings.Count; i++)
            {
                // Two duplicate vertices are added by every zero-width bridge.
                var remaining = maxVertices - usedVertices - 2;
                if (remaining < 3)
                    break;

                var hole = BuildSampledZoningRing(
                    sourceRings[i],
                    stride,
                    remaining,
                    originLon,
                    originLat
                );
                if (hole.Count < 3)
                    continue;

                result.Add(hole);
                usedVertices += hole.Count + 2;
            }

            return result;
        }

        private List<Vector3> BuildSampledZoningRing(
            List<Importers.GeoPoint> sourceRing,
            int stride,
            int maxVertices,
            double originLon,
            double originLat)
        {
            var result = new List<Vector3>();
            if (sourceRing == null || sourceRing.Count < 3)
                return result;

            for (var i = 0; i < sourceRing.Count && result.Count < maxVertices; i += Math.Max(1, stride))
            {
                var world = GeoTransform.ToWorld(sourceRing[i], _config, originLon, originLat);
                world.y = ResolveY(world) + _config.GroundMargin + _config.ZoningFillYOffset;
                result.Add(world);
            }

            return result;
        }

        private void LogZoningMaterialLegend()
        {
            LogVerboseOverlay(
                "GroundOverlay: zoning material legend: " +
                "residential_low=vertClair#7ab64d, " +
                "residential_medium=vertMoyen#2d9d54, " +
                "residential_high=vertFonce#0b6f3a, " +
                "commercial_low=bleuCommercialClair#4aa3ff, " +
                "commercial_high=bleuCommercial#1f57d6, " +
                "retail_detail=bleuDetail#74c5ff, " +
                "industrial=ambre#d6ad32, " +
                "office=violet#a46bd5, " +
                "parking_surface=vertParkingClair#b9ed70, " +
                "parking_ramp=vertParking#5fe86e, " +
                "mixed=turquoise#2ed6e5, " +
                "fallback=white"
            );
        }
    }
}