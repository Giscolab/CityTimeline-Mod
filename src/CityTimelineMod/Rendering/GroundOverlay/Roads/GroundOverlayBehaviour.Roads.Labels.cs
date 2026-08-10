using System;
using System.Collections.Generic;
using CityTimelineMod.Importers;
using CityTimelineMod.Rendering.Batching;
using CityTimelineMod.Rendering.Core;
using CityTimelineMod.Rendering.Materials;
using CityTimelineMod.Rendering.Roads;
using CityTimelineMod.Util;
using UnityEngine;

namespace CityTimelineMod.Rendering
{
    internal sealed partial class GroundOverlayBehaviour
    {
        private int RenderRoadDirectionArrows(Material material, int stride)
        {
            if (_config == null || !_config.RenderRoadDirectionArrows || _roadLines == null || _roadLines.Count == 0)
                return 0;

            var originLon = _config.UseGeoJsonCenter ? _bounds.CenterLon : _config.OriginLon;
            var originLat = _config.UseGeoJsonCenter ? _bounds.CenterLat : _config.OriginLat;
            var safeStride = Math.Max(1, stride);
            var spacing = Mathf.Max(1f, _config.RoadArrowSpacingMeters);
            var maxArrows = Math.Max(0, _config.RoadArrowMaxCount);
            var renderAllArrows = IsRenderEverythingEnabled() || _config.RenderAllRoadArrows;
            const int maxVerticesPerMesh = 60000;

            var batch = new RoadArrowBatch();
            batch.Material = material;

            var createdArrows = 0;
            var skippedByLimit = 0;
            var eligibleLines = 0;
            var skippedByFilter = 0;

            foreach (var roadLine in _roadLines)
            {
                if (roadLine == null || roadLine.IsPath || roadLine.Points == null || roadLine.Points.Count < 2)
                    continue;

                if (!RoadRenderRules.IsOnewayRoad(roadLine))
                    continue;

                if (!ShouldRenderRoadLineForOverlay(roadLine))
                {
                    skippedByFilter++;
                    continue;
                }

                eligibleLines++;

                var road = roadLine.Points;
                var distanceSinceArrow = 0f;
                var reverse = RoadRenderRules.IsOnewayReverse(roadLine.Oneway);

                for (var i = 0; i < road.Count - safeStride; i += safeStride)
                {
                    if (!renderAllArrows && createdArrows >= maxArrows)
                    {
                        skippedByLimit++;
                        break;
                    }

                    var a = GeoTransform.ToWorld(road[i], _config, originLon, originLat);
                    var b = GeoTransform.ToWorld(road[Math.Min(i + safeStride, road.Count - 1)], _config, originLon, originLat);
                    var dx = b.x - a.x;
                    var dz = b.z - a.z;
                    var segmentLength = Mathf.Sqrt(dx * dx + dz * dz);

                    if (segmentLength < 0.1f)
                        continue;

                    distanceSinceArrow += segmentLength;

                    if (distanceSinceArrow < spacing)
                        continue;

                    distanceSinceArrow = 0f;

                    var center = new Vector3((a.x + b.x) * 0.5f, 0f, (a.z + b.z) * 0.5f);
                    center.y = ResolveY(center) + _config.GroundMargin + _config.RoadYOffset + _config.RoadArrowYOffset;

                    if (reverse)
                    {
                        var temp = a;
                        a = b;
                        b = temp;
                    }

                    if (batch.Vertices.Count + 6 >= maxVerticesPerMesh)
                        OverlayMeshFlusher.FlushRoadArrowBatch(OverlayRenderParent, batch);

                    if (RoadGeometryBuilder.AppendRoadArrow(batch, center, a, b, _config.RoadArrowSize))
                        createdArrows++;
                }

                if (!renderAllArrows && createdArrows >= maxArrows)
                    break;
            }

            OverlayMeshFlusher.FlushRoadArrowBatch(OverlayRenderParent, batch);

            Log.Info(
                "GroundOverlay: road direction arrows summary: enabled=" + _config.RenderRoadDirectionArrows +
                ", eligibleOnewayLines=" + eligibleLines +
                ", arrows=" + createdArrows +
                ", skippedByFilter=" + skippedByFilter +
                ", skippedByLimit=" + skippedByLimit +
                ", renderAllRoadArrows=" + renderAllArrows +
                ", max=" + _config.RoadArrowMaxCount
            );

            return createdArrows;
        }

        private int RenderRoadLabels(Material material, int stride)
        {
            if (_config == null || !_config.RenderRoadLabels || _roadLines == null || _roadLines.Count == 0)
                return 0;

            var originLon = _config.UseGeoJsonCenter ? _bounds.CenterLon : _config.OriginLon;
            var originLat = _config.UseGeoJsonCenter ? _bounds.CenterLat : _config.OriginLat;
            var safeStride = Math.Max(1, stride);
            var renderAllLabels = IsRenderEverythingEnabled() || _config.RenderAllRoadLabels;
            var maxLabels = Math.Max(0, _config.RoadLabelMaxCount);
            var camera = Camera.main;
            var createdLabels = 0;
            var skippedByFilter = 0;
            var skippedByLimit = 0;
            var skippedDuplicate = 0;
            var seenLabels = new HashSet<string>();

            foreach (var roadLine in _roadLines)
            {
                if (roadLine == null || roadLine.Points == null || roadLine.Points.Count < 2 || string.IsNullOrWhiteSpace(roadLine.Name))
                    continue;

                if (!ShouldRenderRoadLineForOverlay(roadLine))
                {
                    skippedByFilter++;
                    continue;
                }

                if (!renderAllLabels)
                {
                    if (roadLine.IsPath || RoadRenderRules.GetRoadDebugTier(roadLine.Highway) < _config.RoadLabelMinTier)
                    {
                        skippedByFilter++;
                        continue;
                    }

                    if (createdLabels >= maxLabels)
                    {
                        skippedByLimit++;
                        continue;
                    }
                }

                var road = roadLine.Points;
                var anchorIndex = Math.Min(road.Count - 2, Math.Max(0, road.Count / 2));
                var nextIndex = Math.Min(road.Count - 1, anchorIndex + safeStride);
                var a = GeoTransform.ToWorld(road[anchorIndex], _config, originLon, originLat);
                var b = GeoTransform.ToWorld(road[nextIndex], _config, originLon, originLat);
                var world = new Vector3((a.x + b.x) * 0.5f, 0f, (a.z + b.z) * 0.5f);
                world.y = ResolveY(world) + _config.GroundMargin + _config.RoadYOffset + _config.RoadLabelYOffset;

                if (!renderAllLabels && camera != null && _config.RoadLabelMaxDistance > 0f)
                {
                    var delta = world - camera.transform.position;
                    if (delta.sqrMagnitude > _config.RoadLabelMaxDistance * _config.RoadLabelMaxDistance)
                    {
                        skippedByFilter++;
                        continue;
                    }
                }

                if (_config.DeduplicateRoadLabels && !renderAllLabels)
                {
                    var bucketX = Mathf.RoundToInt(world.x / 500f);
                    var bucketZ = Mathf.RoundToInt(world.z / 500f);
                    var dedupeKey = roadLine.Name.Trim().ToLowerInvariant() + "@" + bucketX + ":" + bucketZ;

                    if (seenLabels.Contains(dedupeKey))
                    {
                        skippedDuplicate++;
                        continue;
                    }

                    seenLabels.Add(dedupeKey);
                }

                CreateRoadLabel(roadLine.Name, world, a, b, material);
                createdLabels++;
            }

            Log.Info(
                "GroundOverlay: road labels summary: enabled=" + _config.RenderRoadLabels +
                ", labels=" + createdLabels +
                ", skippedByFilter=" + skippedByFilter +
                ", skippedDuplicate=" + skippedDuplicate +
                ", skippedByLimit=" + skippedByLimit +
                ", renderAllRoadLabels=" + renderAllLabels +
                ", max=" + _config.RoadLabelMaxCount
            );

            return createdLabels;
        }

        private void CreateRoadLabel(string label, Vector3 world, Vector3 a, Vector3 b, Material material)
        {
            var obj = new GameObject("road_label_" + OverlayObjectNameUtil.SanitizeObjectName(label));
            obj.transform.SetParent(OverlayRenderParent, true);
            obj.transform.position = world;

            var text = obj.AddComponent<TextMesh>();
            text.text = label;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = Mathf.Clamp(_config.RoadLabelFontSize, 8, 128);
            text.characterSize = Mathf.Max(1f, _config.RoadSegmentWidth * 0.75f);
            text.color = new Color(1f, 1f, 1f, Mathf.Clamp01(_config.RoadAlpha));

            var renderer = obj.GetComponent<MeshRenderer>();
            if (renderer == null)
                renderer = obj.AddComponent<MeshRenderer>();

            if (renderer != null && material != null)
                renderer.material = material;

            OrientRoadLabel(obj.transform, a, b);
            _roadLabelMeshes.Add(text);
        }

        private void OrientRoadLabel(Transform labelTransform, Vector3 a, Vector3 b)
        {
            var camera = Camera.main;

            if (camera != null)
            {
                var directionToCamera = camera.transform.position - labelTransform.position;
                directionToCamera.y = 0f;

                if (directionToCamera.sqrMagnitude > 0.01f)
                {
                    labelTransform.rotation = Quaternion.LookRotation(directionToCamera.normalized, Vector3.up);
                    return;
                }
            }

            var direction = b - a;
            direction.y = 0f;

            if (direction.sqrMagnitude > 0.01f)
                labelTransform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        private void UpdateRoadLabelBillboards()
        {
            if (_roadLabelMeshes == null || _roadLabelMeshes.Count == 0)
                return;

            var camera = Camera.main;
            if (camera == null)
                return;

            for (var i = 0; i < _roadLabelMeshes.Count; i++)
            {
                var label = _roadLabelMeshes[i];
                if (label == null)
                    continue;

                var directionToCamera = camera.transform.position - label.transform.position;
                directionToCamera.y = 0f;

                if (directionToCamera.sqrMagnitude > 0.01f)
                    label.transform.rotation = Quaternion.LookRotation(directionToCamera.normalized, Vector3.up);
            }
        }
    }
}
