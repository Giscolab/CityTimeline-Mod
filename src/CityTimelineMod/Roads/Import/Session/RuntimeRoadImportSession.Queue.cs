using System;
using System.Collections.Generic;
using System.Globalization;
using CityTimelineMod.Importers;
using Game.Net;
using Unity.Entities;
using Unity.Mathematics;

namespace CityTimelineMod.Roads
{
    internal static partial class RuntimeRoadSpawner
    {
        private sealed partial class RuntimeRoadImportSession
        {
            internal RuntimeRoadImportResult Queue(IList<GeoRoadLine> roadLines)
            {
                var result = new RuntimeRoadImportResult();
                var requestedMaxSegments = _config.RuntimeRoadImportMaxSegments <= 0
                    ? int.MaxValue
                    : _config.RuntimeRoadImportMaxSegments;
                var remainingSessionBudget = Math.Max(
                    0,
                    VanillaSafeSessionEdgeRoadBudget - _baselineEdgeRoadCount
                );
                var maxSegments = Math.Min(
                    requestedMaxSegments,
                    Math.Min(VanillaSafeSingleApplyMaxSegments, remainingSessionBudget)
                );

                result.RequestedMaxSegments = _config.RuntimeRoadImportMaxSegments;
                result.EffectiveMaxSegments = maxSegments;
                result.CappedByVanillaSingleApplyLimit = requestedMaxSegments > VanillaSafeSingleApplyMaxSegments;
                result.BaselineEdgeRoadBeforeImport = _baselineEdgeRoadCount;
                result.VanillaSafeSessionEdgeRoadBudget = VanillaSafeSessionEdgeRoadBudget;
                result.CappedByVanillaSessionBudget =
                    remainingSessionBudget < VanillaSafeSingleApplyMaxSegments ||
                    _baselineEdgeRoadCount > 0;

                var stride = Math.Max(1, _config.RuntimeRoadImportStride);
                var minLength = Math.Max(0.5f, _config.RuntimeRoadImportMinSegmentLengthMeters);
                var junctionQuantizeStep = Math.Max(0.5f, Math.Min(2f, _config.RuntimeRoadImportSnapToleranceMeters));
                var junctionPositions = BuildJunctionPositions(roadLines, junctionQuantizeStep);
                var junctionKeys = new HashSet<long>(junctionPositions.Keys);

                Util.Log.Info(
                    "[RoadImport] runtime filters: source=" + Safe(_config.RuntimeRoadImportSourceFilter) +
                    ", highway=" + Safe(_config.RuntimeRoadImportHighwayFilter) +
                    ", stage=" + Safe(_config.RuntimeRoadImportStageFilter) +
                    ", pathsRuntimeImportSupported=false"
                );

                Util.Log.Info(
                    "[RoadImport] spatial scope: worldMap57344=true" +
                    ", central14336DiagnosticOnly=true" +
                    ", worldHalfSizeMeters=" + WorldMapHalfSizeMeters.ToString("0", CultureInfo.InvariantCulture)
                );

                Util.Log.Info(
                    "[RoadImport] junction analysis: sharedWorldPoints=" + junctionKeys.Count +
                    ", canonicalJunctions=" + junctionPositions.Count +
                    ", quantizeStepMeters=" + junctionQuantizeStep.ToString("0.###", CultureInfo.InvariantCulture)
                );

                var selectionMode = SelectionMode;
                ResetRuntimeRoadSelectionSkipCounters();
                var orderedRoadIndices = BuildRoadOrder(roadLines, selectionMode);
                AddRuntimeRoadSelectionSkipsToResult(result);

                Util.Log.Info(
                    "[RoadImport] road import order prepared. mode=" +
                    selectionMode +
                    ", candidates=" + orderedRoadIndices.Count +
                    ", targetCenter=(" +
                    GeoRoadImportPlacement.CenterX.ToString("0.###", CultureInfo.InvariantCulture) + "," +
                    GeoRoadImportPlacement.CenterZ.ToString("0.###", CultureInfo.InvariantCulture) + ")"
                );

                var plannedRequests = new List<RoadRequest>(Math.Min(maxSegments, 32768));

                for (var orderIndex = 0; orderIndex < orderedRoadIndices.Count; orderIndex++)
                {
                    if (plannedRequests.Count >= maxSegments)
                        break;

                    var r = orderedRoadIndices[orderIndex];
                    var roadLine = roadLines[r];

                    if (roadLine == null || roadLine.Points == null || roadLine.Points.Count < 2)
                        continue;

                    result.ProcessedRoads++;

                    var meta = RoadImportMetadata.FromRoadLine(roadLine);

                    if (meta == null)
                        continue;

                    string skipReason;

                    if (!ShouldImportRuntimeRoad(roadLine, meta, out skipReason))
                    {
                        CountRuntimeRoadSelectionSkip(result, skipReason);
                        continue;
                    }

                    var prefab = _resolver.ResolveForRoad(meta);

                    if (prefab == Entity.Null)
                    {
                        result.SkippedNoPrefab++;
                        continue;
                    }

                    var prefabBase = _resolver.GetPrefabBase(prefab);

                    if (_config.RuntimeRoadImportVerboseSelectionLogs && _selectionLogs < 25)
                    {
                        _selectionLogs++;
                        Util.Log.Info(
                            "[RoadSelect] name=" + (string.IsNullOrWhiteSpace(meta.Name) ? "<unnamed>" : meta.Name) +
                            " highway=" + meta.Highway +
                            " lanes=" + meta.TargetLaneCount +
                            " oneway=" + meta.Oneway +
                            " -> prefab=" + _resolver.GetPrefabName(prefab) +
                            " entity=" + prefab.Index
                        );
                    }

                    var points = BuildWorldPoints(roadLine.Points, stride, junctionKeys, junctionPositions, junctionQuantizeStep);
                    var simplified = SimplifyPolyline(points);

                    for (var i = 0; i < simplified.Count - 1; i++)
                    {
                        if (plannedRequests.Count >= maxSegments)
                            break;

                        var start = simplified[i].Position;
                        var end = simplified[i + 1].Position;
                        var horizontalLength = math.distance(new float2(start.x, start.z), new float2(end.x, end.z));

                        if (horizontalLength < minLength)
                        {
                            result.SkippedShort++;
                            continue;
                        }

                        var key = BuildUndirectedSegmentKey(start, end);

                        if (!_segmentKeys.Add(key))
                        {
                            result.SkippedDuplicate++;
                            continue;
                        }

                        if (ApplyTerrainY(ref start))
                            result.TerrainHits++;
                        else
                            result.TerrainMisses++;

                        if (ApplyTerrainY(ref end))
                            result.TerrainHits++;
                        else
                            result.TerrainMisses++;

                        start.y += _config.RuntimeRoadImportYOffset;
                        end.y += _config.RuntimeRoadImportYOffset;

                        if (!IsSegmentInsideWorldMap57344(start, end))
                        {
                            result.SkippedOutside57344++;
                            continue;
                        }

                        plannedRequests.Add(new RoadRequest
                        {
                            Prefab = prefab,
                            PrefabBase = prefabBase,
                            Start = start,
                            End = end,
                            RoadName = meta.Name ?? ""
                        });
                    }
                }

                var topologyOptions = new RuntimeRoadTopologyOptions
                {
                    EndpointSnapToleranceMeters = _config.RuntimeRoadImportSnapToleranceMeters,
                    IntersectionSnapToleranceMeters = Math.Max(
                        2.5f,
                        Math.Min(
                            _config.RuntimeRoadImportSnapToleranceMeters,
                            _config.RuntimeRoadImportSnapToleranceMeters * 0.75f
                        )
                    ),
                    MinimumSegmentLengthMeters = minLength,
                    SplitIntersections = true,
                    MaxIntersectionTests = 5000000
                };

                RuntimeRoadTopologyResult topologyStats;
                var topologyBuilder = new RuntimeRoadTopologyBuilder(topologyOptions);
                var finalRequests = topologyBuilder.Build(plannedRequests, out topologyStats);

                result.SkippedShort += topologyStats.SkippedShort;
                result.SkippedDuplicate += topologyStats.SkippedDuplicate;

                var finalCount = Math.Min(maxSegments, finalRequests.Count);

                for (var i = 0; i < finalCount; i++)
                {
                    var request = finalRequests[i];

                    if (!IsSegmentInsideWorldMap57344(request.Start, request.End))
                    {
                        result.SkippedOutside57344++;
                        continue;
                    }

                    _tool.QueueRoadEdge(request.Prefab, request.PrefabBase, request.Start, request.End, request.RoadName);

                    if (IsSegmentInsideVanillaPlayable(request.Start, request.End))
                        result.QueuedInside14336++;
                    else
                        result.QueuedOutside14336Inside57344++;

                    result.QueuedSegments++;

                    if (_segmentLogs < 5)
                    {
                        _segmentLogs++;
                        Util.Log.Info(
                            "[RoadImport] final topology segment #" + result.QueuedSegments +
                            " start=(" + request.Start.x.ToString("F1") + "," + request.Start.y.ToString("F1") + "," + request.Start.z.ToString("F1") + ")" +
                            " end=(" + request.End.x.ToString("F1") + "," + request.End.y.ToString("F1") + "," + request.End.z.ToString("F1") + ")"
                        );
                    }
                }

                if (finalRequests.Count > finalCount)
                {
                    Util.Log.Info(
                        "[RoadTopology] final output capped by maxSegments. output=" +
                        finalRequests.Count +
                        ", queued=" +
                        finalCount +
                        ", maxSegments=" +
                        maxSegments
                    );
                }

                return result;
            }
        }
    }
}
