using System;
using System.Collections.Generic;
using System.Globalization;
using CityTimelineMod.Importers;
using CityTimelineMod.Rendering;
using Unity.Mathematics;

namespace CityTimelineMod.Roads
{
    internal static partial class RuntimeRoadSpawner
    {
        private sealed partial class RuntimeRoadImportSession
        {
            private struct RoadCenterCandidate
            {
                public int Index;
                public float DistanceSq;
                public int DistanceBucket;
                public int Priority;
                public long CellKey;
                public int CellX;
                public int CellZ;
                public int Stage;
            }

            private List<int> BuildRoadOrder(IList<GeoRoadLine> roadLines, string selectionMode)
            {
                if (string.Equals(selectionMode, "file-order", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(selectionMode, "sequential", StringComparison.OrdinalIgnoreCase))
                {
                    return BuildSequentialRoadOrder(roadLines);
                }

                if (string.Equals(selectionMode, "staged-cell", StringComparison.OrdinalIgnoreCase))
                {
                    return BuildStagedCellRoadOrder(roadLines, false);
                }

                if (string.Equals(selectionMode, "staged-cell-priority", StringComparison.OrdinalIgnoreCase))
                {
                    return BuildStagedCellRoadOrder(roadLines, true);
                }

                if (string.Equals(selectionMode, "whole-map", StringComparison.OrdinalIgnoreCase))
                {
                    return BuildWholeMapRoadOrder(roadLines, false);
                }

                if (string.Equals(selectionMode, "whole-map-priority", StringComparison.OrdinalIgnoreCase))
                {
                    return BuildWholeMapRoadOrder(roadLines, true);
                }

                if (string.Equals(selectionMode, "near-center", StringComparison.OrdinalIgnoreCase))
                {
                    return BuildNearCenterRoadOrder(roadLines, false);
                }

                return BuildNearCenterRoadOrder(roadLines, true);
            }

            private List<int> BuildSequentialRoadOrder(IList<GeoRoadLine> roadLines)
            {
                var result = new List<int>();

                if (roadLines == null)
                    return result;

                for (var i = 0; i < roadLines.Count; i++)
                    result.Add(i);

                return result;
            }

            private List<int> BuildStagedCellRoadOrder(IList<GeoRoadLine> roadLines, bool usePriority)
            {
                var result = new List<int>();

                if (roadLines == null || roadLines.Count == 0)
                    return result;

                var cellSize = Math.Max(250.0, _config.RuntimeRoadImportDistanceBucketMeters);
                var stages = new Dictionary<int, Dictionary<long, List<RoadCenterCandidate>>>();

                for (var i = 0; i < roadLines.Count; i++)
                {
                    var line = roadLines[i];

                    if (line == null || line.Points == null || line.Points.Count < 2)
                        continue;

                    var meta = RoadImportMetadata.FromRoadLine(line);

                    if (meta == null)
                        continue;

                    string skipReason;

                    if (!ShouldImportRuntimeRoad(line, meta, out skipReason))
                    {
                        CountRuntimeRoadSelectionSkip(skipReason);
                        continue;
                    }

                    var center = CalculateApproxRoadCenter(line);
                    var cellX = (int)Math.Floor(center.x / cellSize);
                    var cellZ = (int)Math.Floor(center.y / cellSize);
                    var cellKey = PackCellKey(cellX, cellZ);
                    var stage = ResolveImportStage(meta);
                    var priority = usePriority ? CalculateRoadImportPriority(meta) : 0;
                    var distanceSq = center.x * center.x + center.y * center.y;

                    Dictionary<long, List<RoadCenterCandidate>> cells;

                    if (!stages.TryGetValue(stage, out cells))
                    {
                        cells = new Dictionary<long, List<RoadCenterCandidate>>();
                        stages[stage] = cells;
                    }

                    List<RoadCenterCandidate> group;

                    if (!cells.TryGetValue(cellKey, out group))
                    {
                        group = new List<RoadCenterCandidate>();
                        cells[cellKey] = group;
                    }

                    group.Add(new RoadCenterCandidate
                    {
                        Index = i,
                        DistanceSq = distanceSq,
                        DistanceBucket = 0,
                        Priority = priority,
                        CellKey = cellKey,
                        CellX = cellX,
                        CellZ = cellZ,
                        Stage = stage
                    });
                }

                var stageIds = new List<int>(stages.Keys);
                stageIds.Sort();

                for (var s = 0; s < stageIds.Count; s++)
                {
                    var stage = stageIds[s];
                    var cells = stages[stage];
                    var orderedCells = new List<long>(cells.Keys);

                    orderedCells.Sort((a, b) =>
                    {
                        var aGroup = cells[a];
                        var bGroup = cells[b];

                        var aFirst = aGroup.Count > 0 ? aGroup[0] : default(RoadCenterCandidate);
                        var bFirst = bGroup.Count > 0 ? bGroup[0] : default(RoadCenterCandidate);

                        var aCellDistanceSq = CalculateCellDistanceSq(aFirst.CellX, aFirst.CellZ, cellSize);
                        var bCellDistanceSq = CalculateCellDistanceSq(bFirst.CellX, bFirst.CellZ, cellSize);

                        var compare = aCellDistanceSq.CompareTo(bCellDistanceSq);

                        if (compare != 0)
                            return compare;

                        return a.CompareTo(b);
                    });

                    for (var c = 0; c < orderedCells.Count; c++)
                    {
                        var group = cells[orderedCells[c]];

                        group.Sort((a, b) =>
                        {
                            var compare = b.Priority.CompareTo(a.Priority);

                            if (compare != 0)
                                return compare;

                            compare = a.DistanceSq.CompareTo(b.DistanceSq);

                            if (compare != 0)
                                return compare;

                            return a.Index.CompareTo(b.Index);
                        });

                        for (var i = 0; i < group.Count; i++)
                            result.Add(group[i].Index);
                    }

                    Util.Log.Info(
                        "[RoadImport] staged-cell stage=" + stage +
                        " label=" + ResolveImportStageLabel(stage) +
                        " cells=" + cells.Count +
                        " accumulated=" + result.Count
                    );
                }

                Util.Log.Info(
                    "[RoadImport] staged-cell selection enabled. priority=" + usePriority +
                    ", stages=" + stageIds.Count +
                    ", candidates=" + result.Count +
                    ", cellSize=" + cellSize.ToString("0.###", CultureInfo.InvariantCulture)
                );

                return result.Count > 0 || HasExplicitRuntimeSelectionFilter() ? result : BuildSequentialRoadOrder(roadLines);
            }

            private List<int> BuildWholeMapRoadOrder(IList<GeoRoadLine> roadLines, bool usePriority)
            {
                var result = new List<int>();

                if (roadLines == null || roadLines.Count == 0)
                    return result;

                var cellSize = Math.Max(500.0, _config.RuntimeRoadImportDistanceBucketMeters);
                var groups = new Dictionary<long, List<RoadCenterCandidate>>();

                for (var i = 0; i < roadLines.Count; i++)
                {
                    var line = roadLines[i];

                    if (line == null || line.Points == null || line.Points.Count < 2)
                        continue;

                    var meta = RoadImportMetadata.FromRoadLine(line);

                    if (meta == null)
                        continue;

                    string skipReason;

                    if (!ShouldImportRuntimeRoad(line, meta, out skipReason))
                    {
                        CountRuntimeRoadSelectionSkip(skipReason);
                        continue;
                    }

                    var center = CalculateApproxRoadCenter(line);
                    var cellX = (int)Math.Floor(center.x / cellSize);
                    var cellZ = (int)Math.Floor(center.y / cellSize);
                    var cellKey = PackCellKey(cellX, cellZ);

                    List<RoadCenterCandidate> group;

                    if (!groups.TryGetValue(cellKey, out group))
                    {
                        group = new List<RoadCenterCandidate>();
                        groups[cellKey] = group;
                    }

                    group.Add(new RoadCenterCandidate
                    {
                        Index = i,
                        DistanceSq = center.x * center.x + center.y * center.y,
                        DistanceBucket = 0,
                        Priority = usePriority ? CalculateRoadImportPriority(meta) : 0,
                        CellKey = cellKey
                    });
                }

                var orderedCells = new List<long>(groups.Keys);

                for (var i = 0; i < orderedCells.Count; i++)
                {
                    var group = groups[orderedCells[i]];

                    group.Sort((a, b) =>
                    {
                        var compare = b.Priority.CompareTo(a.Priority);

                        if (compare != 0)
                            return compare;

                        return a.DistanceSq.CompareTo(b.DistanceSq);
                    });
                }

                orderedCells.Sort((a, b) =>
                {
                    var aGroup = groups[a];
                    var bGroup = groups[b];

                    var aPriority = aGroup.Count > 0 ? aGroup[0].Priority : 0;
                    var bPriority = bGroup.Count > 0 ? bGroup[0].Priority : 0;

                    var compare = bPriority.CompareTo(aPriority);

                    if (compare != 0)
                        return compare;

                    return a.CompareTo(b);
                });

                var cursor = 0;
                var added = true;

                while (added)
                {
                    added = false;

                    for (var i = 0; i < orderedCells.Count; i++)
                    {
                        var group = groups[orderedCells[i]];

                        if (cursor >= group.Count)
                            continue;

                        result.Add(group[cursor].Index);
                        added = true;
                    }

                    cursor++;
                }

                Util.Log.Info(
                    "[RoadImport] whole-map selection enabled. priority=" + usePriority +
                    ", cells=" + groups.Count +
                    ", candidates=" + result.Count +
                    ", cellSize=" + cellSize.ToString("0.###", CultureInfo.InvariantCulture)
                );

                return result.Count > 0 || HasExplicitRuntimeSelectionFilter() ? result : BuildSequentialRoadOrder(roadLines);
            }

            private List<int> BuildNearCenterRoadOrder(IList<GeoRoadLine> roadLines, bool usePriority)
            {
                var result = new List<int>();

                if (roadLines == null || roadLines.Count == 0)
                    return result;

                var target = new float2(
                    GeoRoadImportPlacement.CenterX,
                    GeoRoadImportPlacement.CenterZ
                );

                var candidates = new List<RoadCenterCandidate>(roadLines.Count);

                for (var i = 0; i < roadLines.Count; i++)
                {
                    var line = roadLines[i];

                    if (line == null || line.Points == null || line.Points.Count < 2)
                        continue;

                    var meta = RoadImportMetadata.FromRoadLine(line);

                    if (meta == null)
                        continue;

                    string skipReason;

                    if (!ShouldImportRuntimeRoad(line, meta, out skipReason))
                    {
                        CountRuntimeRoadSelectionSkip(skipReason);
                        continue;
                    }

                    var distanceSq = CalculateNearestDistanceSqToImportCenter(line, target);

                    if (float.IsNaN(distanceSq) || float.IsInfinity(distanceSq) || distanceSq == float.MaxValue)
                        continue;

                    candidates.Add(new RoadCenterCandidate
                    {
                        Index = i,
                        DistanceSq = distanceSq,
                        DistanceBucket = CalculateRoadDistanceBucket(distanceSq),
                        Priority = usePriority ? CalculateRoadImportPriority(meta) : 0
                    });
                }

                candidates.Sort((a, b) =>
                {
                    var compare = a.DistanceBucket.CompareTo(b.DistanceBucket);

                    if (compare != 0)
                        return compare;

                    compare = b.Priority.CompareTo(a.Priority);

                    if (compare != 0)
                        return compare;

                    return a.DistanceSq.CompareTo(b.DistanceSq);
                });

                for (var i = 0; i < candidates.Count; i++)
                    result.Add(candidates[i].Index);

                if (candidates.Count > 0)
                {
                    var nearest = candidates[0];

                    Util.Log.Info(
                        "[RoadImport] near-center selection enabled. priority=" + usePriority + ", target=(" +
                        target.x.ToString("0.###", CultureInfo.InvariantCulture) + "," +
                        target.y.ToString("0.###", CultureInfo.InvariantCulture) +
                        "), firstIndex=" + nearest.Index +
                        ", firstDistance=" + Math.Sqrt(nearest.DistanceSq).ToString("0.###", CultureInfo.InvariantCulture) +
                        ", firstBucket=" + nearest.DistanceBucket +
                        ", firstPriority=" + nearest.Priority +
                        ", candidates=" + candidates.Count
                    );
                }
                else
                {
                    if (HasExplicitRuntimeSelectionFilter())
                    {
                        Util.Log.Info("[RoadImport] near-center prioritized selection found no candidates for active filters.");
                        return result;
                    }

                    Util.Log.Info("[RoadImport] near-center prioritized selection found no candidates; falling back to file order.");
                    return BuildSequentialRoadOrder(roadLines);
                }

                return result;
            }

            private float CalculateNearestDistanceSqToImportCenter(GeoRoadLine roadLine, float2 target)
            {
                if (roadLine == null || roadLine.Points == null || roadLine.Points.Count == 0)
                    return float.MaxValue;

                var best = float.MaxValue;

                for (var i = 0; i < roadLine.Points.Count; i++)
                {
                    var world = GeoTransform.ToWorld(roadLine.Points[i], _config, _originLon, _originLat);
                    var dx = world.x - target.x;
                    var dz = world.z - target.y;
                    var distanceSq = dx * dx + dz * dz;

                    if (distanceSq < best)
                        best = distanceSq;
                }

                return best;
            }

            private float2 CalculateApproxRoadCenter(GeoRoadLine roadLine)
            {
                if (roadLine == null || roadLine.Points == null || roadLine.Points.Count == 0)
                    return new float2(0f, 0f);

                double sumX = 0.0;
                double sumZ = 0.0;
                var count = 0;

                for (var i = 0; i < roadLine.Points.Count; i++)
                {
                    var world = GeoTransform.ToWorld(roadLine.Points[i], _config, _originLon, _originLat);
                    sumX += world.x;
                    sumZ += world.z;
                    count++;
                }

                if (count <= 0)
                    return new float2(0f, 0f);

                return new float2(
                    (float)(sumX / count),
                    (float)(sumZ / count)
                );
            }

            private static double CalculateCellDistanceSq(int cellX, int cellZ, double cellSize)
            {
                var centerX = (cellX + 0.5) * cellSize;
                var centerZ = (cellZ + 0.5) * cellSize;

                return centerX * centerX + centerZ * centerZ;
            }

            private static long PackCellKey(int cellX, int cellZ)
            {
                return ((long)cellX << 32) ^ (uint)cellZ;
            }
        }
    }
}
