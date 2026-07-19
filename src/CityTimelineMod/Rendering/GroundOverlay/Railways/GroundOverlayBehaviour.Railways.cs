using System;
using System.Collections.Generic;
using CityTimelineMod.Importers;
using CityTimelineMod.Rendering.Batching;
using CityTimelineMod.Rendering.Materials;
using CityTimelineMod.Rendering.Railways;
using CityTimelineMod.Util;
using UnityEngine;

namespace CityTimelineMod.Rendering
{
    internal sealed partial class GroundOverlayBehaviour
    {
        private const string NoRailwayDataMessage = "Aucune donnée ferroviaire disponible dans ce bundle.";
        private const int RailwayMaxVerticesPerMesh = 60000;
        private const float RailwayTunnelDashMeters = 12f;
        private const float RailwayTunnelGapMeters = 10f;

        private List<GeoRailwayLine> _railwayLines = new List<GeoRailwayLine>();
        private RailwaySemanticStats _railwayStats = new RailwaySemanticStats();
        private bool _railwayAvailable;
        private string _railwayStatus = NoRailwayDataMessage;
        private bool _railwaySettingsSavePending;
        private float _railwaySettingsSaveDeadline;

        private void InitializeRailwayOverlay(
            List<GeoRailwayLine> lines,
            bool available,
            string status
        )
        {
            _railwayLines = lines ?? new List<GeoRailwayLine>();
            _railwayStats = RailwaySemanticStats.FromLines(_railwayLines);
            _railwayAvailable = available && _railwayStats.Total > 0;
            _railwayStatus = string.IsNullOrWhiteSpace(status)
                ? (_railwayAvailable ? _railwayStats.Total + " voie(s) ferroviaire(s)." : NoRailwayDataMessage)
                : status;

            Log.Info(
                "GroundOverlay: railway semantic summary: train=" + _railwayStats.Train +
                ", tram=" + _railwayStats.Tram +
                ", lightRail=" + _railwayStats.LightRail +
                ", subway=" + _railwayStats.Subway +
                ", service=" + _railwayStats.Service +
                ", tunnels=" + _railwayStats.Tunnels +
                ", total=" + _railwayStats.Total +
                ", available=" + _railwayAvailable
            );
        }

        internal RailwayHudSnapshot GetRailwayHudSnapshot()
        {
            var stats = _railwayStats ?? new RailwaySemanticStats();
            var status = _railwayStatus;

            if (_railwayAvailable && _progressiveRebuild != null && _progressiveRebuild.IsActive)
            {
                status = "Affichage " + _progressiveRebuild.RailwayChunkIndex +
                    " / " + _progressiveRebuild.RailwayChunks.Count + " chunks";
            }

            return new RailwayHudSnapshot
            {
                Available = _railwayAvailable,
                Visible = _config != null && _config.RenderRailways,
                Opacity = _config != null ? _config.RailwayOpacity : 0.9f,
                Thickness = _config != null ? _config.RailwayThickness : 3f,
                TrainVisible = _config == null || _config.RailwayTrainVisible,
                TramVisible = _config == null || _config.RailwayTramVisible,
                LightRailVisible = _config == null || _config.RailwayLightRailVisible,
                SubwayVisible = _config == null || _config.RailwaySubwayVisible,
                ServiceVisible = _config == null || _config.RailwayServiceVisible,
                TunnelsVisible = _config == null || _config.RailwayTunnelsVisible,
                TrainCount = stats.Train,
                TramCount = stats.Tram,
                LightRailCount = stats.LightRail,
                SubwayCount = stats.Subway,
                ServiceCount = stats.Service,
                TunnelCount = stats.Tunnels,
                TotalCount = stats.Total,
                Status = status ?? NoRailwayDataMessage
            };
        }

        internal bool SetRailwayBoolean(string key, bool value)
        {
            if (_config == null)
                return false;

            var normalized = NormalizeRailwaySettingKey(key);
            var requiresRebuild = false;

            switch (normalized)
            {
                case "visible":
                    _config.RenderRailways = value;
                    requiresRebuild = true;
                    break;
                case "trainvisible":
                    _config.RailwayTrainVisible = value;
                    requiresRebuild = true;
                    break;
                case "tramvisible":
                    _config.RailwayTramVisible = value;
                    requiresRebuild = true;
                    break;
                case "lightrailvisible":
                    _config.RailwayLightRailVisible = value;
                    requiresRebuild = true;
                    break;
                case "subwayvisible":
                    _config.RailwaySubwayVisible = value;
                    requiresRebuild = true;
                    break;
                case "servicevisible":
                    _config.RailwayServiceVisible = value;
                    requiresRebuild = true;
                    break;
                case "tunnelsvisible":
                    _config.RailwayTunnelsVisible = value;
                    requiresRebuild = true;
                    break;
                default:
                    return false;
            }

            _railwaySettingsSavePending = false;
            _config.SaveVisualSettingsToConfig();
            ApplyRailwayVisibilityToMaterials();

            if (requiresRebuild && _railwayAvailable)
                RequestOverlayRebuild("railway HUD filter: " + normalized, true);

            return true;
        }

        internal bool SetRailwayFloat(string key, float value)
        {
            if (_config == null)
                return false;

            var normalized = NormalizeRailwaySettingKey(key);

            if (normalized == "opacity")
            {
                _config.RailwayOpacity = Mathf.Clamp01(value);
                ScheduleRailwaySettingsSave();
                ApplyRailwayVisibilityToMaterials();
                return true;
            }

            if (normalized == "thickness")
            {
                _config.RailwayThickness = Mathf.Clamp(value, 0.5f, 8f);
                ScheduleRailwaySettingsSave();

                if (_railwayAvailable)
                    RequestOverlayRebuild("railway HUD thickness", true);

                return true;
            }

            return false;
        }

        private void ScheduleRailwaySettingsSave()
        {
            _railwaySettingsSavePending = true;
            _railwaySettingsSaveDeadline = Time.realtimeSinceStartup + 0.35f;
        }

        private void UpdatePendingRailwaySettingsSave()
        {
            if (!_railwaySettingsSavePending || Time.realtimeSinceStartup < _railwaySettingsSaveDeadline)
                return;

            _railwaySettingsSavePending = false;

            if (_config != null)
                _config.SaveVisualSettingsToConfig();
        }

        private void OnDestroy()
        {
            if (_railwaySettingsSavePending && _config != null)
            {
                _railwaySettingsSavePending = false;
                _config.SaveVisualSettingsToConfig();
            }
        }

        private static string NormalizeRailwaySettingKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return "";

            var normalized = key.Trim().ToLowerInvariant()
                .Replace("_", "")
                .Replace("-", "")
                .Replace(" ", "");

            return normalized.StartsWith("railway", StringComparison.Ordinal)
                ? normalized.Substring("railway".Length)
                : normalized;
        }

        private List<RailwayRenderChunk> BuildRailwayRenderChunks(double originLon, double originLat)
        {
            var chunks = new Dictionary<string, RailwayRenderChunk>();
            var chunkSize = Mathf.Clamp(_config.RailwayChunkSizeMeters, 128f, 4096f);

            if (!_config.RenderRailways || _railwayLines == null)
                return new List<RailwayRenderChunk>();

            for (var i = 0; i < _railwayLines.Count; i++)
            {
                var line = _railwayLines[i];
                if (line == null || line.Points == null || line.Points.Count < 2)
                    continue;

                var midpoint = ResolveRailwayLineMidpointWorld(line, originLon, originLat);
                var chunkX = _config.EnableRailwaySpatialChunking ? Mathf.FloorToInt(midpoint.x / chunkSize) : 0;
                var chunkZ = _config.EnableRailwaySpatialChunking ? Mathf.FloorToInt(midpoint.z / chunkSize) : 0;
                var key = chunkX + ":" + chunkZ;
                RailwayRenderChunk chunk;

                if (!chunks.TryGetValue(key, out chunk))
                {
                    chunk = new RailwayRenderChunk { ChunkX = chunkX, ChunkZ = chunkZ };
                    chunks[key] = chunk;
                }

                chunk.Lines.Add(line);
                chunk.IncludePoint(midpoint);
                chunk.IncludePoint(GeoTransform.ToWorld(line.Points[0], _config, originLon, originLat));
                chunk.IncludePoint(GeoTransform.ToWorld(line.Points[line.Points.Count - 1], _config, originLon, originLat));
            }

            var result = new List<RailwayRenderChunk>(chunks.Values);
            result.Sort(CompareRailwayChunks);
            return result;
        }

        private static int CompareRailwayChunks(RailwayRenderChunk a, RailwayRenderChunk b)
        {
            if (ReferenceEquals(a, b))
                return 0;
            if (a == null)
                return -1;
            if (b == null)
                return 1;

            var x = a.ChunkX.CompareTo(b.ChunkX);
            return x != 0 ? x : a.ChunkZ.CompareTo(b.ChunkZ);
        }

        private Vector3 ResolveRailwayLineMidpointWorld(GeoRailwayLine line, double originLon, double originLat)
        {
            var anchor = Math.Min(line.Points.Count - 2, Math.Max(0, line.Points.Count / 2 - 1));
            var a = GeoTransform.ToWorld(line.Points[anchor], _config, originLon, originLat);
            var b = GeoTransform.ToWorld(line.Points[anchor + 1], _config, originLon, originLat);
            return new Vector3((a.x + b.x) * 0.5f, 0f, (a.z + b.z) * 0.5f);
        }

        private List<Vector3> BuildRailwayWorldPoints(
            IList<GeoPoint> geoPoints,
            int stride,
            double originLon,
            double originLat,
            int remainingSegments,
            out bool limitHit
        )
        {
            limitHit = false;
            var result = new List<Vector3>();
            if (geoPoints == null || geoPoints.Count < 2 || remainingSegments <= 0)
            {
                limitHit = geoPoints != null && geoPoints.Count >= 2;
                return result;
            }

            var index = 0;
            var appended = 0;
            var safeStride = Math.Max(1, stride);
            result.Add(ResolveRailwayWorldPoint(geoPoints[0], originLon, originLat));

            while (index < geoPoints.Count - 1)
            {
                if (appended >= remainingSegments)
                {
                    limitHit = true;
                    break;
                }

                var next = Math.Min(geoPoints.Count - 1, index + safeStride);
                if (next <= index)
                    break;

                result.Add(ResolveRailwayWorldPoint(geoPoints[next], originLon, originLat));
                index = next;
                appended++;
            }

            return result;
        }

        private Vector3 ResolveRailwayWorldPoint(GeoPoint point, double originLon, double originLat)
        {
            var world = GeoTransform.ToWorld(point, _config, originLon, originLat);
            world.y = ResolveY(world) + _config.GroundMargin + _config.RailwayYOffset;
            return world;
        }

        private int RenderRailwayChunk(
            RailwayRenderChunk chunk,
            OverlayRenderMaterials materials,
            int stride,
            RailwayRenderCounters counters,
            double originLon,
            double originLat
        )
        {
            if (chunk == null || chunk.Lines == null || materials == null || counters == null)
                return 0;

            var batches = new Dictionary<string, RailwayMeshBatch>();
            var createdBefore = counters.CreatedSegments;
            var maxSegments = _config.RenderEverything || _config.RenderAllRailwaySegments
                ? int.MaxValue
                : Math.Max(1, _config.MaxRailwaySegmentsDebug);

            for (var i = 0; i < chunk.Lines.Count; i++)
            {
                var line = chunk.Lines[i];
                counters.SourceLines++;

                if (line == null || line.Points == null || line.Points.Count < 2)
                {
                    counters.SkippedInvalid++;
                    continue;
                }

                if (!RailwayRenderRules.IsCategoryVisible(line.Category, _config) ||
                    (line.Tunnel && !_config.RailwayTunnelsVisible))
                {
                    counters.SkippedByFilter++;
                    continue;
                }

                counters.EligibleLines++;
                var remaining = maxSegments == int.MaxValue
                    ? int.MaxValue
                    : Math.Max(0, maxSegments - counters.CreatedSegments);
                bool limitHit;
                var points = BuildRailwayWorldPoints(
                    line.Points,
                    stride,
                    originLon,
                    originLat,
                    remaining,
                    out limitHit
                );

                if (points.Count < 2)
                {
                    if (limitHit)
                        counters.SkippedByLimit++;
                    continue;
                }

                var categoryKey = RailwayRenderRules.CategoryKey(line.Category);
                var batchKey = categoryKey + (line.Tunnel ? "_tunnel" : "_surface") +
                    "_cx" + chunk.ChunkX + "_cz" + chunk.ChunkZ;
                RailwayMeshBatch batch;

                if (!batches.TryGetValue(batchKey, out batch))
                {
                    batch = new RailwayMeshBatch
                    {
                        Material = RailwayRenderRules.ResolveMaterial(line, materials)
                    };
                    batches[batchKey] = batch;
                }

                var width = _config.RailwayThickness * RailwayRenderRules.WidthScale(line.Category);
                var appendedSourceSegments = 0;

                for (var pointIndex = 0; pointIndex < points.Count - 1; pointIndex++)
                {
                    if (batch.Vertices.Count >= RailwayMaxVerticesPerMesh - 4096)
                    {
                        counters.CreatedMeshObjects += OverlayMeshFlusher.FlushRailwayBatch(
                            transform,
                            batchKey,
                            batch,
                            LogVerboseOverlay
                        );
                    }

                    var appended = line.Tunnel
                        ? RailwayGeometryBuilder.AppendDashedRibbonSegment(
                            batch,
                            points[pointIndex],
                            points[pointIndex + 1],
                            width,
                            RailwayTunnelDashMeters,
                            RailwayTunnelGapMeters
                        )
                        : (RailwayGeometryBuilder.AppendRibbonSegment(
                            batch,
                            points[pointIndex],
                            points[pointIndex + 1],
                            width
                        ) ? 1 : 0);

                    if (appended > 0)
                        appendedSourceSegments++;
                }

                if (appendedSourceSegments > 0)
                {
                    counters.CreatedLines++;
                    counters.CreatedSegments += appendedSourceSegments;
                }

                if (limitHit)
                    counters.SkippedByLimit++;
            }

            foreach (var pair in batches)
            {
                counters.CreatedMeshObjects += OverlayMeshFlusher.FlushRailwayBatch(
                    transform,
                    pair.Key,
                    pair.Value,
                    LogVerboseOverlay
                );
            }

            return counters.CreatedSegments - createdBefore;
        }

        private RailwayRenderCounters RenderRailwayGroup(
            OverlayRenderMaterials materials,
            int stride,
            double originLon,
            double originLat
        )
        {
            var counters = new RailwayRenderCounters();
            var chunks = BuildRailwayRenderChunks(originLon, originLat);

            for (var i = 0; i < chunks.Count; i++)
                RenderRailwayChunk(chunks[i], materials, stride, counters, originLon, originLat);

            LogRailwayRenderCounters(counters, chunks.Count);
            return counters;
        }

        private void LogRailwayRenderCounters(RailwayRenderCounters counters, int chunkCount)
        {
            if (counters == null)
                return;

            Log.Info(
                "GroundOverlay: railway batching summary: sourceLines=" + counters.SourceLines +
                ", eligibleLines=" + counters.EligibleLines +
                ", renderedLines=" + counters.CreatedLines +
                ", segments=" + counters.CreatedSegments +
                ", meshObjects=" + counters.CreatedMeshObjects +
                ", chunks=" + chunkCount +
                ", skippedByFilter=" + counters.SkippedByFilter +
                ", skippedByLimit=" + counters.SkippedByLimit +
                ", skippedInvalid=" + counters.SkippedInvalid
            );
        }
    }
}
