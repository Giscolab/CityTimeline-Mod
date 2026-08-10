using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace CityTimelineMod.Roads
{
    internal static partial class RuntimeRoadSpawner
    {
        private static string BuildUndirectedSegmentKey(float3 a, float3 b)
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

        private static List<WorldRoadPoint> SimplifyPolyline(List<WorldRoadPoint> points)
        {
            if (points == null || points.Count <= 2)
                return points ?? new List<WorldRoadPoint>();

            var simplified = new List<WorldRoadPoint>(points.Count) { points[0] };

            for (var i = 1; i < points.Count - 1; i++)
            {
                var prevKept = simplified[simplified.Count - 1].Position;
                var currentPoint = points[i];
                var current = currentPoint.Position;
                var next = points[i + 1].Position;

                if (currentPoint.IsJunction)
                {
                    if (math.distance(new float2(prevKept.x, prevKept.z), new float2(current.x, current.z)) > 0.01f)
                        simplified.Add(currentPoint);

                    continue;
                }

                var dPrev = math.distance(new float2(prevKept.x, prevKept.z), new float2(current.x, current.z));
                var dNext = math.distance(new float2(current.x, current.z), new float2(next.x, next.z));

                if (dPrev < 1.5f || dNext < 1.5f)
                    continue;

                var v1 = math.normalizesafe(new float2(current.x - prevKept.x, current.z - prevKept.z), new float2(1f, 0f));
                var v2 = math.normalizesafe(new float2(next.x - current.x, next.z - current.z), new float2(1f, 0f));
                var dot = math.clamp(math.dot(v1, v2), -1f, 1f);
                var angleDeg = math.degrees(math.acos(dot));
                var straightDelta = math.abs(180f - angleDeg);

                if (straightDelta <= 2f && dPrev < 12f && dNext < 12f)
                    continue;

                simplified.Add(currentPoint);
            }

            simplified.Add(points[points.Count - 1]);
            return simplified;
        }

        private static float3 SnapEndpointToGrid(float3 point, Dictionary<long, List<float2>> nodeGrid, float tolerance)
        {
            var p = new float2(point.x, point.z);
            float2 snapped;

            if (TryFindNearbyNode(p, nodeGrid, tolerance, out snapped))
            {
                point.x = snapped.x;
                point.z = snapped.y;
            }

            return point;
        }

        private static void RegisterSnappedEndpoint(float3 point, Dictionary<long, List<float2>> nodeGrid, float tolerance)
        {
            var p = new float2(point.x, point.z);
            var cellSize = Math.Max(1f, tolerance);
            var cellX = Quantize(p.x, cellSize);
            var cellZ = Quantize(p.y, cellSize);
            var key = MakeCellKey(cellX, cellZ);
            List<float2> bucket;

            if (!nodeGrid.TryGetValue(key, out bucket))
            {
                bucket = new List<float2>(4);
                nodeGrid[key] = bucket;
            }

            for (var i = 0; i < bucket.Count; i++)
            {
                if (math.distance(bucket[i], p) <= 0.01f)
                    return;
            }

            bucket.Add(p);
        }

        private static bool TryFindNearbyNode(float2 point, Dictionary<long, List<float2>> nodeGrid, float tolerance, out float2 snapped)
        {
            var cellSize = Math.Max(1f, tolerance);
            var cellX = Quantize(point.x, cellSize);
            var cellZ = Quantize(point.y, cellSize);
            var bestDistance = float.MaxValue;
            var best = default(float2);
            var found = false;

            for (var dz = -1; dz <= 1; dz++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    var key = MakeCellKey(cellX + dx, cellZ + dz);
                    List<float2> bucket;

                    if (!nodeGrid.TryGetValue(key, out bucket))
                        continue;

                    for (var i = 0; i < bucket.Count; i++)
                    {
                        var distance = math.distance(bucket[i], point);

                        if (distance <= tolerance && distance < bestDistance)
                        {
                            bestDistance = distance;
                            best = bucket[i];
                            found = true;
                        }
                    }
                }
            }

            snapped = best;
            return found;
        }

        private static int Quantize(float value, float step)
        {
            return (int)math.round(value / step);
        }

        private static long MakePointKey(float3 point, float step)
        {
            return MakeCellKey(Quantize(point.x, step), Quantize(point.z, step));
        }

        private static long MakeCellKey(int x, int z)
        {
            return ((long)x << 32) ^ (uint)z;
        }
    }
}
