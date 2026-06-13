using System;
using System.Collections.Generic;
using System.Globalization;
using Unity.Mathematics;

namespace CityTimelineMod.Roads
{
internal sealed class RuntimeRoadTopologyOptions
{
    public float EndpointSnapToleranceMeters = 4f;
    public float IntersectionSnapToleranceMeters = 2.5f;
    public float MinimumSegmentLengthMeters = 2f;
    public bool SplitIntersections = true;
    public int MaxIntersectionTests = 2500000;
}

    internal sealed class RuntimeRoadTopologyResult
    {
        public int InputSegments;
        public int OutputSegments;
        public int EndpointCount;
        public int EndpointClusters;
        public int EndpointSnapMoves;
        public float EndpointAverageMove;
        public float EndpointMaxMove;
        public int EndpointRawClusters;
        public int EndpointBoundedClusterSplits;
        public int EndpointToSegmentSnaps;
        public int IntersectionPairs;
        public int ExactIntersectionPairs;
        public int NearMissIntersectionPairs;
        public int IntersectionSplitPoints;
        public int SplitSegmentsCreated;
        public int SkippedShort;
        public int SkippedDuplicate;
        public int IntersectionTestCount;
        public bool IntersectionTestLimitReached;
    }

    internal sealed class RuntimeRoadTopologyBuilder
    {
        private const float Epsilon = 0.0001f;
        private const float SplitTMin = 0.01f;
        private const float SplitTMax = 0.99f;
        private const float MinimumNearMissAngleSin = 0.20f;

        private readonly RuntimeRoadTopologyOptions _options;

        internal RuntimeRoadTopologyBuilder(RuntimeRoadTopologyOptions options)
        {
            _options = options ?? new RuntimeRoadTopologyOptions();
            _options.EndpointSnapToleranceMeters = math.max(0.25f, _options.EndpointSnapToleranceMeters);
            _options.IntersectionSnapToleranceMeters = math.max(0.25f, _options.IntersectionSnapToleranceMeters);
            _options.MinimumSegmentLengthMeters = math.max(0.25f, _options.MinimumSegmentLengthMeters);
            _options.MaxIntersectionTests = Math.Max(1000, _options.MaxIntersectionTests);
        }

        internal List<RoadRequest> Build(IList<RoadRequest> input, out RuntimeRoadTopologyResult stats)
        {
            stats = new RuntimeRoadTopologyResult();

            var segments = CopyValidInput(input, stats);

            if (segments.Count == 0)
                return new List<RoadRequest>();

            CanonicalizeEndpoints(segments, stats);

            if (_options.SplitIntersections && segments.Count > 1)
            {
                SplitEndpointToSegmentTouches(segments, stats);
                SplitSegmentIntersections(segments, stats);
            }

            var output = RebuildRequests(segments, stats);
            CanonicalizeOutputEndpoints(output, stats);
            output = DeduplicateAndFilter(output, stats);

            stats.OutputSegments = output.Count;

            Util.Log.Info(
                "[RoadTopology] built. input=" + stats.InputSegments +
                ", output=" + stats.OutputSegments +
                ", endpointCount=" + stats.EndpointCount +
                ", endpointClusters=" + stats.EndpointClusters +
                ", endpointSnapMoves=" + stats.EndpointSnapMoves +
                ", endpointAvgMove=" + F(stats.EndpointAverageMove) +
                ", endpointMaxMove=" + F(stats.EndpointMaxMove) +
                ", endpointRawClusters=" + stats.EndpointRawClusters +
                ", endpointBoundedClusterSplits=" + stats.EndpointBoundedClusterSplits +
                ", endpointToSegmentSnaps=" + stats.EndpointToSegmentSnaps +
                ", intersectionPairs=" + stats.IntersectionPairs +
                ", exactIntersectionPairs=" + stats.ExactIntersectionPairs +
                ", nearMissIntersectionPairs=" + stats.NearMissIntersectionPairs +
                ", intersectionSplitPoints=" + stats.IntersectionSplitPoints +
                ", splitSegmentsCreated=" + stats.SplitSegmentsCreated +
                ", skippedShort=" + stats.SkippedShort +
                ", skippedDuplicate=" + stats.SkippedDuplicate +
                ", tests=" + stats.IntersectionTestCount +
                ", testLimitReached=" + stats.IntersectionTestLimitReached +
", endpointTolerance=" + F(_options.EndpointSnapToleranceMeters) +
", intersectionTolerance=" + F(_options.IntersectionSnapToleranceMeters)
            );

            return output;
        }

        private List<SegmentWork> CopyValidInput(IList<RoadRequest> input, RuntimeRoadTopologyResult stats)
        {
            var result = new List<SegmentWork>(input != null ? input.Count : 0);

            if (input == null)
                return result;

            for (var i = 0; i < input.Count; i++)
            {
                var request = input[i];
                var horizontalLength = Distance2(request.Start, request.End);

                if (horizontalLength < _options.MinimumSegmentLengthMeters)
                {
                    stats.SkippedShort++;
                    continue;
                }

                result.Add(new SegmentWork
                {
                    Request = request,
                    Start = request.Start,
                    End = request.End,
                    Splits = null
                });
            }

            stats.InputSegments = result.Count;
            return result;
        }

        private void CanonicalizeEndpoints(List<SegmentWork> segments, RuntimeRoadTopologyResult stats)
        {
            var endpoints = new List<EndpointRef>(segments.Count * 2);

            for (var i = 0; i < segments.Count; i++)
            {
                endpoints.Add(new EndpointRef { SegmentIndex = i, IsStart = true, Position = segments[i].Start });
                endpoints.Add(new EndpointRef { SegmentIndex = i, IsStart = false, Position = segments[i].End });
            }

            stats.EndpointCount += endpoints.Count;

            if (endpoints.Count <= 1)
                return;

            var parent = new int[endpoints.Count];
            var rank = new byte[endpoints.Count];

            for (var i = 0; i < parent.Length; i++)
                parent[i] = i;

            var tolerance = _options.EndpointSnapToleranceMeters;
            var cellSize = math.max(1f, tolerance);
            var grid = new Dictionary<long, List<int>>();

            for (var i = 0; i < endpoints.Count; i++)
            {
                var p = endpoints[i].Position;
                var cx = QuantizeFloor(p.x, cellSize);
                var cz = QuantizeFloor(p.z, cellSize);

                for (var dz = -1; dz <= 1; dz++)
                {
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        List<int> bucket;
                        if (!grid.TryGetValue(Pack(cx + dx, cz + dz), out bucket))
                            continue;

                        for (var j = 0; j < bucket.Count; j++)
                        {
                            var other = bucket[j];

                            if (Distance2(p, endpoints[other].Position) <= tolerance)
                                Union(parent, rank, i, other);
                        }
                    }
                }

                var key = Pack(cx, cz);
                List<int> own;
                if (!grid.TryGetValue(key, out own))
                {
                    own = new List<int>(4);
                    grid[key] = own;
                }
                own.Add(i);
            }

            var rawClusters = new Dictionary<int, List<int>>();

            for (var i = 0; i < endpoints.Count; i++)
            {
                var root = Find(parent, i);
                List<int> members;
                if (!rawClusters.TryGetValue(root, out members))
                {
                    members = new List<int>(4);
                    rawClusters[root] = members;
                }
                members.Add(i);
            }

            stats.EndpointRawClusters += rawClusters.Count;

            var canonicalByEndpoint = new float3[endpoints.Count];
            var boundedClusterCount = 0;
            var splitRawClusterCount = 0;

            foreach (var pair in rawClusters)
            {
                var boundedClusters = BuildBoundedEndpointClusters(endpoints, pair.Value, tolerance);
                boundedClusterCount += boundedClusters.Count;

                if (boundedClusters.Count > 1)
                    splitRawClusterCount++;

                for (var c = 0; c < boundedClusters.Count; c++)
                {
                    var cluster = boundedClusters[c];
                    var snapped = cluster.Center;

                    for (var m = 0; m < cluster.Members.Count; m++)
                        canonicalByEndpoint[cluster.Members[m]] = snapped;
                }
            }

            stats.EndpointClusters += boundedClusterCount;
            stats.EndpointBoundedClusterSplits += splitRawClusterCount;

            var moveTotal = 0f;
            var moveMax = 0f;
            var moved = 0;

            for (var i = 0; i < endpoints.Count; i++)
            {
                var snapped = canonicalByEndpoint[i];
                var original = endpoints[i].Position;
                var move = Distance2(original, snapped);

                if (move > tolerance + 0.01f)
                {
                    snapped = original;
                    move = 0f;
                }

                if (move > 0.001f)
                {
                    moved++;
                    moveTotal += move;
                    if (move > moveMax)
                        moveMax = move;
                }

                var segment = segments[endpoints[i].SegmentIndex];

                if (endpoints[i].IsStart)
                    segment.Start = snapped;
                else
                    segment.End = snapped;

                segments[endpoints[i].SegmentIndex] = segment;
            }

            stats.EndpointSnapMoves += moved;
            stats.EndpointAverageMove = moved > 0 ? moveTotal / moved : 0f;
            stats.EndpointMaxMove = math.max(stats.EndpointMaxMove, moveMax);
        }

        private void SplitEndpointToSegmentTouches(List<SegmentWork> segments, RuntimeRoadTopologyResult stats)
        {
            var tolerance = _options.EndpointSnapToleranceMeters;
            var grid = BuildSegmentGrid(segments, math.max(8f, tolerance * 2f), tolerance);
            var handled = new HashSet<ulong>();

            for (var i = 0; i < segments.Count; i++)
            {
                TrySnapEndpointToSegment(segments, i, true, grid, tolerance, handled, stats);
                TrySnapEndpointToSegment(segments, i, false, grid, tolerance, handled, stats);
            }
        }

        private void TrySnapEndpointToSegment(
            List<SegmentWork> segments,
            int endpointSegmentIndex,
            bool isStart,
            Dictionary<long, List<int>> grid,
            float tolerance,
            HashSet<ulong> handled,
            RuntimeRoadTopologyResult stats)
        {
            var source = segments[endpointSegmentIndex];
            var endpoint = isStart ? source.Start : source.End;
            var cellSize = math.max(8f, tolerance * 2f);
            var minCx = QuantizeFloor(endpoint.x - tolerance, cellSize);
            var maxCx = QuantizeFloor(endpoint.x + tolerance, cellSize);
            var minCz = QuantizeFloor(endpoint.z - tolerance, cellSize);
            var maxCz = QuantizeFloor(endpoint.z + tolerance, cellSize);

            var bestSegment = -1;
            var bestT = 0f;
            var bestDistance = float.MaxValue;
            float3 bestPoint = default(float3);

            for (var cz = minCz; cz <= maxCz; cz++)
            {
                for (var cx = minCx; cx <= maxCx; cx++)
                {
                    List<int> bucket;
                    if (!grid.TryGetValue(Pack(cx, cz), out bucket))
                        continue;

                    for (var b = 0; b < bucket.Count; b++)
                    {
                        var candidateIndex = bucket[b];

                        if (candidateIndex == endpointSegmentIndex)
                            continue;

                        var candidate = segments[candidateIndex];

                        if (Distance2(endpoint, candidate.Start) <= tolerance || Distance2(endpoint, candidate.End) <= tolerance)
                            continue;

                        float t;
                        float3 projected;
                        var distance = DistancePointToSegment2(endpoint, candidate.Start, candidate.End, out t, out projected);

                        if (t <= SplitTMin || t >= SplitTMax)
                            continue;

                        if (distance > tolerance || distance >= bestDistance)
                            continue;

                        bestSegment = candidateIndex;
                        bestT = t;
                        bestDistance = distance;
                        bestPoint = new float3(projected.x, (endpoint.y + projected.y) * 0.5f, projected.z);
                    }
                }
            }

            if (bestSegment < 0)
                return;

            var handleKey = EndpointSegmentKey(endpointSegmentIndex, isStart, bestSegment);
            if (!handled.Add(handleKey))
                return;

            source = segments[endpointSegmentIndex];
            if (isStart)
                source.Start = bestPoint;
            else
                source.End = bestPoint;
            segments[endpointSegmentIndex] = source;

            AddSplit(segments, bestSegment, bestT, bestPoint);

            stats.EndpointToSegmentSnaps++;
            stats.IntersectionPairs++;
            stats.IntersectionSplitPoints += 2;
        }

        private void SplitSegmentIntersections(List<SegmentWork> segments, RuntimeRoadTopologyResult stats)
        {
            var tolerance = _options.IntersectionSnapToleranceMeters;
            var cellSize = math.max(8f, tolerance * 2f);
            var grid = new Dictionary<long, List<int>>();
            var testedPairs = new HashSet<ulong>();

            for (var i = 0; i < segments.Count; i++)
            {
                var a = segments[i];
                var minX = math.min(a.Start.x, a.End.x) - tolerance;
                var maxX = math.max(a.Start.x, a.End.x) + tolerance;
                var minZ = math.min(a.Start.z, a.End.z) - tolerance;
                var maxZ = math.max(a.Start.z, a.End.z) + tolerance;
                var minCx = QuantizeFloor(minX, cellSize);
                var maxCx = QuantizeFloor(maxX, cellSize);
                var minCz = QuantizeFloor(minZ, cellSize);
                var maxCz = QuantizeFloor(maxZ, cellSize);

                for (var cz = minCz; cz <= maxCz; cz++)
                {
                    for (var cx = minCx; cx <= maxCx; cx++)
                    {
                        List<int> bucket;
                        if (!grid.TryGetValue(Pack(cx, cz), out bucket))
                            continue;

                        for (var bIndex = 0; bIndex < bucket.Count; bIndex++)
                        {
                            var j = bucket[bIndex];
                            var key = PairKey(i, j);

                            if (!testedPairs.Add(key))
                                continue;

                            stats.IntersectionTestCount++;
                            if (stats.IntersectionTestCount > _options.MaxIntersectionTests)
                            {
                                stats.IntersectionTestLimitReached = true;
                                return;
                            }

                            TrySplitPair(segments, i, j, stats);
                        }
                    }
                }

                for (var cz = minCz; cz <= maxCz; cz++)
                {
                    for (var cx = minCx; cx <= maxCx; cx++)
                    {
                        var key = Pack(cx, cz);
                        List<int> bucket;
                        if (!grid.TryGetValue(key, out bucket))
                        {
                            bucket = new List<int>(4);
                            grid[key] = bucket;
                        }
                        bucket.Add(i);
                    }
                }
            }
        }

        private void TrySplitPair(List<SegmentWork> segments, int aIndex, int bIndex, RuntimeRoadTopologyResult stats)
        {
            if (aIndex == bIndex)
                return;

            var a = segments[aIndex];
            var b = segments[bIndex];

            if (ShareEndpoint(a, b, _options.EndpointSnapToleranceMeters))
                return;

            if (!HasCrossingAngle(a, b, MinimumNearMissAngleSin))
                return;

            float ta;
            float tb;
            float2 p;

            if (TryLineIntersection2D(a.Start, a.End, b.Start, b.End, out ta, out tb, out p))
            {
                if (IsInteriorT(ta) && IsInteriorT(tb))
                {
                    var pa = math.lerp(a.Start, a.End, ta);
                    var pb = math.lerp(b.Start, b.End, tb);
                    var y = (pa.y + pb.y) * 0.5f;
                    var point = new float3(p.x, y, p.y);

                    AddSplit(segments, aIndex, ta, point);
                    AddSplit(segments, bIndex, tb, point);

                    stats.IntersectionPairs++;
                    stats.ExactIntersectionPairs++;
                    stats.IntersectionSplitPoints += 2;
                }

                return;
            }

            float nearTa;
            float nearTb;
            float3 nearA;
            float3 nearB;
            var distance = ClosestSegmentDistance2D(a.Start, a.End, b.Start, b.End, out nearTa, out nearTb, out nearA, out nearB);

            if (distance > _options.IntersectionSnapToleranceMeters)
                return;

            if (!IsInteriorT(nearTa) || !IsInteriorT(nearTb))
                return;

            var shared = new float3(
                (nearA.x + nearB.x) * 0.5f,
                (nearA.y + nearB.y) * 0.5f,
                (nearA.z + nearB.z) * 0.5f
            );

            AddSplit(segments, aIndex, nearTa, shared);
            AddSplit(segments, bIndex, nearTb, shared);

            stats.IntersectionPairs++;
            stats.NearMissIntersectionPairs++;
            stats.IntersectionSplitPoints += 2;
        }

        private static bool TryLineIntersection2D(float3 a0, float3 a1, float3 b0, float3 b1, out float ta, out float tb, out float2 p)
        {
            var ax = a1.x - a0.x;
            var ay = a1.z - a0.z;
            var bx = b1.x - b0.x;
            var by = b1.z - b0.z;
            var cx = b0.x - a0.x;
            var cy = b0.z - a0.z;
            var denom = ax * by - ay * bx;

            if (math.abs(denom) < 0.000001f)
            {
                ta = 0f;
                tb = 0f;
                p = default(float2);
                return false;
            }

            ta = (cx * by - cy * bx) / denom;
            tb = (cx * ay - cy * ax) / denom;

            if (ta < -Epsilon || ta > 1f + Epsilon || tb < -Epsilon || tb > 1f + Epsilon)
            {
                p = default(float2);
                return false;
            }

            p = new float2(a0.x + ax * ta, a0.z + ay * ta);
            return true;
        }

        private static float ClosestSegmentDistance2D(
            float3 a0,
            float3 a1,
            float3 b0,
            float3 b1,
            out float ta,
            out float tb,
            out float3 pa,
            out float3 pb)
        {
            var d1 = new float2(a1.x - a0.x, a1.z - a0.z);
            var d2 = new float2(b1.x - b0.x, b1.z - b0.z);
            var r = new float2(a0.x - b0.x, a0.z - b0.z);
            var a = math.dot(d1, d1);
            var e = math.dot(d2, d2);
            var f = math.dot(d2, r);

            if (a <= Epsilon && e <= Epsilon)
            {
                ta = 0f;
                tb = 0f;
                pa = a0;
                pb = b0;
                return Distance2(a0, b0);
            }

            if (a <= Epsilon)
            {
                ta = 0f;
                tb = math.clamp(f / e, 0f, 1f);
            }
            else
            {
                var c = math.dot(d1, r);

                if (e <= Epsilon)
                {
                    tb = 0f;
                    ta = math.clamp(-c / a, 0f, 1f);
                }
                else
                {
                    var b = math.dot(d1, d2);
                    var denom = a * e - b * b;

                    if (math.abs(denom) > Epsilon)
                        ta = math.clamp((b * f - c * e) / denom, 0f, 1f);
                    else
                        ta = 0f;

                    var tnom = b * ta + f;

                    if (tnom < 0f)
                    {
                        tb = 0f;
                        ta = math.clamp(-c / a, 0f, 1f);
                    }
                    else if (tnom > e)
                    {
                        tb = 1f;
                        ta = math.clamp((b - c) / a, 0f, 1f);
                    }
                    else
                    {
                        tb = tnom / e;
                    }
                }
            }

            pa = math.lerp(a0, a1, ta);
            pb = math.lerp(b0, b1, tb);
            return Distance2(pa, pb);
        }

        private static List<EndpointClusterWork> BuildBoundedEndpointClusters(List<EndpointRef> endpoints, List<int> members, float tolerance)
        {
            var clusters = new List<EndpointClusterWork>();

            for (var i = 0; i < members.Count; i++)
            {
                var endpointIndex = members[i];
                var point = endpoints[endpointIndex].Position;
                var bestCluster = -1;
                var bestDistance = float.MaxValue;
                float3 bestCenter = default(float3);

                for (var c = 0; c < clusters.Count; c++)
                {
                    float3 candidateCenter;
                    if (!CanAddToBoundedEndpointCluster(endpoints, clusters[c], point, tolerance, out candidateCenter))
                        continue;

                    var distance = Distance2(point, clusters[c].Center);
                    if (distance >= bestDistance)
                        continue;

                    bestCluster = c;
                    bestDistance = distance;
                    bestCenter = candidateCenter;
                }

                if (bestCluster < 0)
                {
                    clusters.Add(new EndpointClusterWork
                    {
                        Members = new List<int> { endpointIndex },
                        Count = 1,
                        SumX = point.x,
                        SumY = point.y,
                        SumZ = point.z,
                        Center = point
                    });
                    continue;
                }

                var cluster = clusters[bestCluster];
                cluster.Members.Add(endpointIndex);
                cluster.Count++;
                cluster.SumX += point.x;
                cluster.SumY += point.y;
                cluster.SumZ += point.z;
                cluster.Center = bestCenter;
                clusters[bestCluster] = cluster;
            }

            return clusters;
        }

        private static bool CanAddToBoundedEndpointCluster(
            List<EndpointRef> endpoints,
            EndpointClusterWork cluster,
            float3 point,
            float tolerance,
            out float3 candidateCenter)
        {
            var newCount = cluster.Count + 1;
            candidateCenter = new float3(
                (float)((cluster.SumX + point.x) / newCount),
                (float)((cluster.SumY + point.y) / newCount),
                (float)((cluster.SumZ + point.z) / newCount)
            );

            if (Distance2(point, candidateCenter) > tolerance)
                return false;

            for (var i = 0; i < cluster.Members.Count; i++)
            {
                if (Distance2(endpoints[cluster.Members[i]].Position, candidateCenter) > tolerance)
                    return false;
            }

            return true;
        }

        private static float DistancePointToSegment2(float3 point, float3 a, float3 b, out float t, out float3 projected)
        {
            var abx = b.x - a.x;
            var abz = b.z - a.z;
            var lenSq = abx * abx + abz * abz;

            if (lenSq <= Epsilon)
            {
                t = 0f;
                projected = a;
                return Distance2(point, a);
            }

            t = ((point.x - a.x) * abx + (point.z - a.z) * abz) / lenSq;
            t = math.clamp(t, 0f, 1f);
            projected = math.lerp(a, b, t);
            return Distance2(point, projected);
        }

        private static void AddSplit(List<SegmentWork> segments, int index, float t, float3 point)
        {
            var segment = segments[index];

            if (segment.Splits == null)
                segment.Splits = new List<SplitPoint>(2);

            for (var i = 0; i < segment.Splits.Count; i++)
            {
                if (math.abs(segment.Splits[i].T - t) < 0.001f)
                    return;

                if (Distance2(segment.Splits[i].Position, point) < 0.25f)
                    return;
            }

            segment.Splits.Add(new SplitPoint { T = math.clamp(t, 0f, 1f), Position = point });
            segments[index] = segment;
        }

        private List<RoadRequest> RebuildRequests(List<SegmentWork> segments, RuntimeRoadTopologyResult stats)
        {
            var result = new List<RoadRequest>(segments.Count);

            for (var i = 0; i < segments.Count; i++)
            {
                var segment = segments[i];

                if (segment.Splits == null || segment.Splits.Count == 0)
                {
                    AddBuiltSegment(result, segment.Request, segment.Start, segment.End, stats);
                    continue;
                }

                var points = new List<SplitPoint>(segment.Splits.Count + 2);
                points.Add(new SplitPoint { T = 0f, Position = segment.Start });
                points.AddRange(segment.Splits);
                points.Add(new SplitPoint { T = 1f, Position = segment.End });
                points.Sort((a, b) => a.T.CompareTo(b.T));

                for (var p = 0; p < points.Count - 1; p++)
                {
                    if (math.abs(points[p + 1].T - points[p].T) < 0.001f)
                        continue;

                    AddBuiltSegment(result, segment.Request, points[p].Position, points[p + 1].Position, stats);
                    stats.SplitSegmentsCreated++;
                }
            }

            return result;
        }

        private void AddBuiltSegment(List<RoadRequest> result, RoadRequest template, float3 start, float3 end, RuntimeRoadTopologyResult stats)
        {
            if (Distance2(start, end) < _options.MinimumSegmentLengthMeters)
            {
                stats.SkippedShort++;
                return;
            }

            template.Start = start;
            template.End = end;
            result.Add(template);
        }

        private void CanonicalizeOutputEndpoints(List<RoadRequest> requests, RuntimeRoadTopologyResult stats)
        {
            if (requests == null || requests.Count <= 1)
                return;

            var segments = new List<SegmentWork>(requests.Count);
            for (var i = 0; i < requests.Count; i++)
            {
                segments.Add(new SegmentWork
                {
                    Request = requests[i],
                    Start = requests[i].Start,
                    End = requests[i].End
                });
            }

            CanonicalizeEndpoints(segments, stats);

            for (var i = 0; i < requests.Count; i++)
            {
                var request = requests[i];
                request.Start = segments[i].Start;
                request.End = segments[i].End;
                requests[i] = request;
            }
        }

        private List<RoadRequest> DeduplicateAndFilter(List<RoadRequest> input, RuntimeRoadTopologyResult stats)
        {
            var output = new List<RoadRequest>(input.Count);
            var keys = new HashSet<string>();

            for (var i = 0; i < input.Count; i++)
            {
                var request = input[i];

                if (Distance2(request.Start, request.End) < _options.MinimumSegmentLengthMeters)
                {
                    stats.SkippedShort++;
                    continue;
                }

                var key = BuildUndirectedKey(request.Start, request.End);
                if (!keys.Add(key))
                {
                    stats.SkippedDuplicate++;
                    continue;
                }

                output.Add(request);
            }

            return output;
        }

        private Dictionary<long, List<int>> BuildSegmentGrid(List<SegmentWork> segments, float cellSize, float tolerance)
        {
            var grid = new Dictionary<long, List<int>>();

            for (var i = 0; i < segments.Count; i++)
            {
                var s = segments[i];
                var minX = math.min(s.Start.x, s.End.x) - tolerance;
                var maxX = math.max(s.Start.x, s.End.x) + tolerance;
                var minZ = math.min(s.Start.z, s.End.z) - tolerance;
                var maxZ = math.max(s.Start.z, s.End.z) + tolerance;
                var minCx = QuantizeFloor(minX, cellSize);
                var maxCx = QuantizeFloor(maxX, cellSize);
                var minCz = QuantizeFloor(minZ, cellSize);
                var maxCz = QuantizeFloor(maxZ, cellSize);

                for (var cz = minCz; cz <= maxCz; cz++)
                {
                    for (var cx = minCx; cx <= maxCx; cx++)
                    {
                        var key = Pack(cx, cz);
                        List<int> bucket;
                        if (!grid.TryGetValue(key, out bucket))
                        {
                            bucket = new List<int>(4);
                            grid[key] = bucket;
                        }
                        bucket.Add(i);
                    }
                }
            }

            return grid;
        }

        private static bool ShareEndpoint(SegmentWork a, SegmentWork b, float tolerance)
        {
            return
                Distance2(a.Start, b.Start) <= tolerance ||
                Distance2(a.Start, b.End) <= tolerance ||
                Distance2(a.End, b.Start) <= tolerance ||
                Distance2(a.End, b.End) <= tolerance;
        }

        private static bool HasCrossingAngle(SegmentWork a, SegmentWork b, float minimumSin)
        {
            var ax = a.End.x - a.Start.x;
            var az = a.End.z - a.Start.z;
            var bx = b.End.x - b.Start.x;
            var bz = b.End.z - b.Start.z;
            var la = math.sqrt(ax * ax + az * az);
            var lb = math.sqrt(bx * bx + bz * bz);

            if (la <= Epsilon || lb <= Epsilon)
                return false;

            var sin = math.abs(ax * bz - az * bx) / (la * lb);
            return sin >= minimumSin;
        }

        private static bool IsInteriorT(float t)
        {
            return t > SplitTMin && t < SplitTMax;
        }

        private static float Distance2(float3 a, float3 b)
        {
            var dx = a.x - b.x;
            var dz = a.z - b.z;
            return math.sqrt(dx * dx + dz * dz);
        }

        private static int QuantizeFloor(float value, float step)
        {
            return (int)math.floor(value / step);
        }

        private static int Quantize(float value, float step)
        {
            return (int)math.round(value / step);
        }

        private static long Pack(int x, int z)
        {
            return ((long)x << 32) ^ (uint)z;
        }

        private static ulong PairKey(int a, int b)
        {
            var lo = (uint)Math.Min(a, b);
            var hi = (uint)Math.Max(a, b);
            return ((ulong)lo << 32) | hi;
        }

        private static ulong EndpointSegmentKey(int endpointSegmentIndex, bool isStart, int targetSegmentIndex)
        {
            var endpointKey = ((ulong)(uint)endpointSegmentIndex << 1) | (isStart ? 1UL : 0UL);
            return (endpointKey << 32) | (uint)targetSegmentIndex;
        }

        private static string BuildUndirectedKey(float3 a, float3 b)
        {
            var ax = Quantize(a.x, 0.1f);
            var az = Quantize(a.z, 0.1f);
            var bx = Quantize(b.x, 0.1f);
            var bz = Quantize(b.z, 0.1f);

            if (ax > bx || (ax == bx && az > bz))
            {
                var tx = ax;
                var tz = az;
                ax = bx;
                az = bz;
                bx = tx;
                bz = tz;
            }

            return ax + "," + az + "|" + bx + "," + bz;
        }

        private static int Find(int[] parent, int x)
        {
            while (parent[x] != x)
            {
                parent[x] = parent[parent[x]];
                x = parent[x];
            }
            return x;
        }

        private static void Union(int[] parent, byte[] rank, int a, int b)
        {
            var ra = Find(parent, a);
            var rb = Find(parent, b);

            if (ra == rb)
                return;

            if (rank[ra] < rank[rb])
            {
                parent[ra] = rb;
            }
            else if (rank[ra] > rank[rb])
            {
                parent[rb] = ra;
            }
            else
            {
                parent[rb] = ra;
                rank[ra]++;
            }
        }

        private static string F(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private struct SegmentWork
        {
            public RoadRequest Request;
            public float3 Start;
            public float3 End;
            public List<SplitPoint> Splits;
        }

        private struct EndpointRef
        {
            public int SegmentIndex;
            public bool IsStart;
            public float3 Position;
        }

        private struct SplitPoint
        {
            public float T;
            public float3 Position;
        }

        private struct EndpointClusterWork
        {
            public List<int> Members;
            public int Count;
            public double SumX;
            public double SumY;
            public double SumZ;
            public float3 Center;
        }
    }
}
