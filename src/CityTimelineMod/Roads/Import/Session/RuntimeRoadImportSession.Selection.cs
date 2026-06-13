using System;
using CityTimelineMod.Importers;
using Unity.Mathematics;

namespace CityTimelineMod.Roads
{
    internal static partial class RuntimeRoadSpawner
    {
        private sealed partial class RuntimeRoadImportSession
        {
            private bool ShouldImportRuntimeRoad(GeoRoadLine roadLine, RoadImportMetadata meta, out string skipReason)
            {
                skipReason = null;

                if (roadLine == null || meta == null)
                {
                    skipReason = "invalid";
                    return false;
                }

                if (!MatchesRuntimeSourceFilter(roadLine))
                {
                    skipReason = "source";
                    return false;
                }

                if (!MatchesRuntimeHighwayFilter(roadLine, meta))
                {
                    skipReason = "highway";
                    return false;
                }

                if (!MatchesRuntimeStageFilter(meta))
                {
                    skipReason = "stage";
                    return false;
                }

                // Paths can be cached for selection, but runtime creation still lacks a path prefab resolver.
                if (roadLine.IsPath)
                {
                    skipReason = "path";
                    return false;
                }

                if (!_config.RuntimeRoadImportIncludeBridgeTunnel && (meta.Bridge || meta.Tunnel))
                {
                    skipReason = "bridgeTunnel";
                    return false;
                }

                if (_config.RuntimeRoadImportSkipParkingAisles && IsParkingAisle(meta))
                {
                    skipReason = "parkingAisle";
                    return false;
                }

                if (_config.RuntimeRoadImportSkipClearlyUnpaved && IsClearlyUnpavedRoad(meta))
                {
                    skipReason = "unpaved";
                    return false;
                }

                return true;
            }

            private static bool IsSegmentInsideVanillaPlayable(float3 start, float3 end)
            {
                return IsSegmentInsideSquare(start, end, VanillaPlayableHalfSizeMeters);
            }

            private static bool IsSegmentInsideWorldMap57344(float3 start, float3 end)
            {
                return IsSegmentInsideSquare(start, end, WorldMapHalfSizeMeters);
            }

            private static bool IsSegmentInsideSquare(float3 start, float3 end, float halfSize)
            {
                return IsPointInsideSquare(start, halfSize) &&
                       IsPointInsideSquare(end, halfSize);
            }

            private static bool IsPointInsideSquare(float3 p, float halfSize)
            {
                return
                    p.x >= -halfSize &&
                    p.x <= halfSize &&
                    p.z >= -halfSize &&
                    p.z <= halfSize;
            }

            private void CountRuntimeRoadSelectionSkip(RuntimeRoadImportResult result, string skipReason)
            {
                if (result == null)
                    return;

                switch (skipReason)
                {
                    case "source":
                        result.SkippedSourceFilter++;
                        break;

                    case "highway":
                        result.SkippedHighwayFilter++;
                        break;

                    case "stage":
                        result.SkippedStageFilter++;
                        break;

                    case "path":
                        result.SkippedPath++;
                        break;

                    case "bridgeTunnel":
                        result.SkippedBridgeTunnel++;
                        break;

                    case "parkingAisle":
                        result.SkippedParkingAisle++;
                        break;

                    case "unpaved":
                        result.SkippedUnpaved++;
                        break;
                }
            }

            private void ResetRuntimeRoadSelectionSkipCounters()
            {
                _selectionSkippedPath = 0;
                _selectionSkippedSourceFilter = 0;
                _selectionSkippedHighwayFilter = 0;
                _selectionSkippedStageFilter = 0;
                _selectionSkippedBridgeTunnel = 0;
                _selectionSkippedParkingAisle = 0;
                _selectionSkippedUnpaved = 0;
            }

            private void AddRuntimeRoadSelectionSkipsToResult(RuntimeRoadImportResult result)
            {
                if (result == null)
                    return;

                result.SkippedPath += _selectionSkippedPath;
                result.SkippedSourceFilter += _selectionSkippedSourceFilter;
                result.SkippedHighwayFilter += _selectionSkippedHighwayFilter;
                result.SkippedStageFilter += _selectionSkippedStageFilter;
                result.SkippedBridgeTunnel += _selectionSkippedBridgeTunnel;
                result.SkippedParkingAisle += _selectionSkippedParkingAisle;
                result.SkippedUnpaved += _selectionSkippedUnpaved;
            }

            private void CountRuntimeRoadSelectionSkip(string skipReason)
            {
                switch (skipReason)
                {
                    case "source":
                        _selectionSkippedSourceFilter++;
                        break;

                    case "highway":
                        _selectionSkippedHighwayFilter++;
                        break;

                    case "stage":
                        _selectionSkippedStageFilter++;
                        break;

                    case "path":
                        _selectionSkippedPath++;
                        break;

                    case "bridgeTunnel":
                        _selectionSkippedBridgeTunnel++;
                        break;

                    case "parkingAisle":
                        _selectionSkippedParkingAisle++;
                        break;

                    case "unpaved":
                        _selectionSkippedUnpaved++;
                        break;
                }
            }

            private bool MatchesRuntimeSourceFilter(GeoRoadLine roadLine)
            {
                var filter = NormalizeRuntimeFilterToken(_config.RuntimeRoadImportSourceFilter, "roads");

                if (filter == "roads-and-paths" || filter == "both" || filter == "all")
                    return true;

                if (filter == "paths")
                    return roadLine != null && roadLine.IsPath;

                return roadLine == null || !roadLine.IsPath;
            }

            private bool MatchesRuntimeHighwayFilter(GeoRoadLine roadLine, RoadImportMetadata meta)
            {
                var filter = NormalizeRuntimeFilterToken(_config.RuntimeRoadImportHighwayFilter, "all");

                if (filter == "all")
                    return true;

                var highway = NormalizeRuntimeHighwayFilterValue(roadLine, meta);
                return CsvFilterContains(filter, highway);
            }

            private bool MatchesRuntimeStageFilter(RoadImportMetadata meta)
            {
                var filter = NormalizeRuntimeFilterToken(_config.RuntimeRoadImportStageFilter, "all");

                if (filter == "all")
                    return true;

                var stage = ResolveImportStageLabel(ResolveImportStage(meta));
                return CsvFilterContains(filter, stage);
            }

            private bool HasExplicitRuntimeSelectionFilter()
            {
                var source = NormalizeRuntimeFilterToken(_config.RuntimeRoadImportSourceFilter, "roads");
                var highway = NormalizeRuntimeFilterToken(_config.RuntimeRoadImportHighwayFilter, "all");
                var stage = NormalizeRuntimeFilterToken(_config.RuntimeRoadImportStageFilter, "all");

                return source != "roads" || highway != "all" || stage != "all";
            }

            private static bool CsvFilterContains(string csv, string token)
            {
                if (string.IsNullOrWhiteSpace(csv))
                    return false;

                if (string.IsNullOrWhiteSpace(token))
                    return false;

                var parts = csv.Split(',');

                for (var i = 0; i < parts.Length; i++)
                {
                    var part = NormalizeRuntimeFilterToken(parts[i], "");

                    if (part == token)
                        return true;
                }

                return false;
            }

            private static string NormalizeRuntimeHighwayFilterValue(GeoRoadLine roadLine, RoadImportMetadata meta)
            {
                var value = roadLine != null ? roadLine.Highway : null;

                if (string.IsNullOrWhiteSpace(value) && meta != null)
                    value = meta.Highway;

                var highway = NormalizeRuntimeFilterToken(value, "residential");

                if (highway == "road")
                    return "residential";

                return highway;
            }

            private static string NormalizeRuntimeFilterToken(string value, string fallback)
            {
                if (string.IsNullOrWhiteSpace(value))
                    return fallback;

                return value.Trim().ToLowerInvariant();
            }
        }
    }
}
