using System.Collections.Generic;
using UnityEngine;

namespace CityTimelineMod.Geometry
{
    internal static class PolygonTriangulator
    {
        internal static List<int> MakeDoubleSidedTriangles(List<int> triangles)
        {
            var result = new List<int>();

            if (triangles == null || triangles.Count < 3)
                return result;

            result.AddRange(triangles);

            for (var i = 0; i + 2 < triangles.Count; i += 3)
            {
                result.Add(triangles[i]);
                result.Add(triangles[i + 2]);
                result.Add(triangles[i + 1]);
            }

            return result;
        }

        internal static List<int> TriangulatePolygonXZ(List<Vector3> vertices)
        {
            var result = new List<int>();

            if (vertices == null || vertices.Count < 3)
                return result;

            var indices = new List<int>();

            for (var i = 0; i < vertices.Count; i++)
            {
                if (indices.Count > 0 && IsSameXZ(vertices[indices[indices.Count - 1]], vertices[i]))
                    continue;

                indices.Add(i);
            }

            if (indices.Count >= 2 && IsSameXZ(vertices[indices[0]], vertices[indices[indices.Count - 1]]))
                indices.RemoveAt(indices.Count - 1);

            if (indices.Count < 3)
                return result;

            var area = SignedAreaXZ(vertices, indices);

            if (Mathf.Abs(area) < 0.0001f)
                return result;

            var ccw = area > 0f;
            var guard = 0;
            var maxGuard = indices.Count * indices.Count;

            while (indices.Count > 3 && guard < maxGuard)
            {
                var earFound = false;

                for (var i = 0; i < indices.Count; i++)
                {
                    var prevIndex = indices[(i - 1 + indices.Count) % indices.Count];
                    var currIndex = indices[i];
                    var nextIndex = indices[(i + 1) % indices.Count];

                    var prev = vertices[prevIndex];
                    var curr = vertices[currIndex];
                    var next = vertices[nextIndex];

                    if (!IsConvexXZ(prev, curr, next, ccw))
                        continue;

                    var containsPoint = false;

                    for (var j = 0; j < indices.Count; j++)
                    {
                        var testIndex = indices[j];

                        if (testIndex == prevIndex || testIndex == currIndex || testIndex == nextIndex)
                            continue;

                        // Hole bridges intentionally duplicate their two end points.
                        // A duplicate of an ear vertex must not invalidate that ear.
                        if (IsSameXZ(vertices[testIndex], prev) ||
                            IsSameXZ(vertices[testIndex], curr) ||
                            IsSameXZ(vertices[testIndex], next))
                        {
                            continue;
                        }

                        if (PointInTriangleXZ(vertices[testIndex], prev, curr, next))
                        {
                            containsPoint = true;
                            break;
                        }
                    }

                    if (containsPoint)
                        continue;

                    if (ccw)
                    {
                        result.Add(prevIndex);
                        result.Add(currIndex);
                        result.Add(nextIndex);
                    }
                    else
                    {
                        result.Add(prevIndex);
                        result.Add(nextIndex);
                        result.Add(currIndex);
                    }

                    indices.RemoveAt(i);
                    earFound = true;
                    break;
                }

                if (!earFound)
                {
                    // OSM rings frequently contain redundant collinear vertices.
                    // Removing one lets the ear clipping continue without changing
                    // the represented surface.
                    var removedCollinearVertex = false;
                    for (var i = 0; i < indices.Count; i++)
                    {
                        var previous = vertices[indices[(i - 1 + indices.Count) % indices.Count]];
                        var current = vertices[indices[i]];
                        var next = vertices[indices[(i + 1) % indices.Count]];
                        if (Mathf.Abs(CrossXZ(previous, current, next)) > 0.00001f)
                            continue;

                        indices.RemoveAt(i);
                        removedCollinearVertex = true;
                        break;
                    }

                    if (!removedCollinearVertex)
                        break;
                }

                guard++;
            }

            if (indices.Count == 3)
            {
                if (ccw)
                {
                    result.Add(indices[0]);
                    result.Add(indices[1]);
                    result.Add(indices[2]);
                }
                else
                {
                    result.Add(indices[0]);
                    result.Add(indices[2]);
                    result.Add(indices[1]);
                }
            }

            return result;
        }

        /// <summary>
        /// Triangulates one GeoJSON polygon while preserving its inner rings.
        /// Ring zero is the shell; subsequent rings are holes. Each hole is
        /// connected to the shell with a zero-area bridge before ear clipping.
        /// </summary>
        internal static List<int> TriangulatePolygonWithHolesXZ(
            List<List<Vector3>> rings,
            out List<Vector3> vertices)
        {
            vertices = new List<Vector3>();
            var empty = new List<int>();
            if (rings == null || rings.Count == 0)
                return empty;

            var outer = CleanRing(rings[0]);
            if (outer.Count < 3)
                return empty;

            EnsureOrientation(outer, true);
            var holes = new List<List<Vector3>>();
            for (var i = 1; i < rings.Count; i++)
            {
                var hole = CleanRing(rings[i]);
                if (hole.Count < 3)
                    continue;

                EnsureOrientation(hole, false);
                holes.Add(hole);
            }

            vertices.AddRange(outer);
            for (var i = 0; i < holes.Count; i++)
            {
                var hole = holes[i];
                var holeIndex = FindRightmostVertex(hole);
                var shellIndex = FindVisibleBridgeVertex(vertices, hole, holeIndex, outer, holes);
                if (shellIndex < 0)
                    continue;

                vertices = SpliceHole(vertices, shellIndex, hole, holeIndex);
            }

            return TriangulatePolygonXZ(vertices);
        }

        private static List<Vector3> CleanRing(List<Vector3> source)
        {
            var result = new List<Vector3>();
            if (source == null)
                return result;

            for (var i = 0; i < source.Count; i++)
            {
                var point = source[i];
                if (result.Count > 0 && IsSameXZ(result[result.Count - 1], point))
                    continue;

                result.Add(point);
            }

            if (result.Count >= 2 && IsSameXZ(result[0], result[result.Count - 1]))
                result.RemoveAt(result.Count - 1);

            return result;
        }

        private static void EnsureOrientation(List<Vector3> ring, bool counterClockwise)
        {
            var indices = new List<int>();
            for (var i = 0; i < ring.Count; i++)
                indices.Add(i);

            var isCounterClockwise = SignedAreaXZ(ring, indices) > 0f;
            if (isCounterClockwise != counterClockwise)
                ring.Reverse();
        }

        private static int FindRightmostVertex(List<Vector3> ring)
        {
            var result = 0;
            for (var i = 1; i < ring.Count; i++)
            {
                if (ring[i].x > ring[result].x ||
                    (Mathf.Approximately(ring[i].x, ring[result].x) && ring[i].z < ring[result].z))
                {
                    result = i;
                }
            }

            return result;
        }

        private static int FindVisibleBridgeVertex(
            List<Vector3> shell,
            List<Vector3> hole,
            int holeIndex,
            List<Vector3> originalOuter,
            List<List<Vector3>> allHoles)
        {
            var origin = hole[holeIndex];
            var bestIndex = -1;
            var bestDistance = float.MaxValue;

            for (var i = 0; i < shell.Count; i++)
            {
                var candidate = shell[i];
                var dx = candidate.x - origin.x;
                var dz = candidate.z - origin.z;
                var distance = dx * dx + dz * dz;
                if (distance < 0.0001f || distance >= bestDistance)
                    continue;

                if (BridgeIntersectsRing(origin, candidate, shell, i, -1))
                    continue;
                if (BridgeIntersectsRing(origin, candidate, hole, -1, holeIndex))
                    continue;

                var intersectsAnotherHole = false;
                for (var h = 0; h < allHoles.Count; h++)
                {
                    if (ReferenceEquals(allHoles[h], hole))
                        continue;
                    if (BridgeIntersectsRing(origin, candidate, allHoles[h], -1, -1))
                    {
                        intersectsAnotherHole = true;
                        break;
                    }
                }

                if (intersectsAnotherHole)
                    continue;

                var midpoint = new Vector3(
                    (origin.x + candidate.x) * 0.5f,
                    (origin.y + candidate.y) * 0.5f,
                    (origin.z + candidate.z) * 0.5f
                );
                if (!PointInRingXZ(midpoint, originalOuter))
                    continue;

                var midpointInsideHole = false;
                for (var h = 0; h < allHoles.Count; h++)
                {
                    if (PointInRingXZ(midpoint, allHoles[h]))
                    {
                        midpointInsideHole = true;
                        break;
                    }
                }

                if (midpointInsideHole)
                    continue;

                bestIndex = i;
                bestDistance = distance;
            }

            if (bestIndex >= 0)
                return bestIndex;

            // Defensive fallback for highly degenerate OSM polygons: connect to
            // the closest shell point. The hole remains represented instead of
            // silently being filled.
            for (var i = 0; i < shell.Count; i++)
            {
                var dx = shell[i].x - origin.x;
                var dz = shell[i].z - origin.z;
                var distance = dx * dx + dz * dz;
                if (distance >= bestDistance)
                    continue;

                bestIndex = i;
                bestDistance = distance;
            }

            return bestIndex;
        }

        private static bool BridgeIntersectsRing(
            Vector3 start,
            Vector3 end,
            List<Vector3> ring,
            int allowedEndVertex,
            int allowedStartVertex)
        {
            for (var i = 0; i < ring.Count; i++)
            {
                var next = (i + 1) % ring.Count;
                if (i == allowedEndVertex || next == allowedEndVertex ||
                    i == allowedStartVertex || next == allowedStartVertex)
                {
                    continue;
                }

                if (SegmentsIntersectXZ(start, end, ring[i], ring[next]))
                    return true;
            }

            return false;
        }

        private static bool SegmentsIntersectXZ(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            const float epsilon = 0.00001f;
            var abC = CrossXZ(a, b, c);
            var abD = CrossXZ(a, b, d);
            var cdA = CrossXZ(c, d, a);
            var cdB = CrossXZ(c, d, b);

            if (((abC > epsilon && abD < -epsilon) || (abC < -epsilon && abD > epsilon)) &&
                ((cdA > epsilon && cdB < -epsilon) || (cdA < -epsilon && cdB > epsilon)))
            {
                return true;
            }

            if (Mathf.Abs(abC) <= epsilon && PointOnSegmentXZ(c, a, b)) return true;
            if (Mathf.Abs(abD) <= epsilon && PointOnSegmentXZ(d, a, b)) return true;
            if (Mathf.Abs(cdA) <= epsilon && PointOnSegmentXZ(a, c, d)) return true;
            if (Mathf.Abs(cdB) <= epsilon && PointOnSegmentXZ(b, c, d)) return true;
            return false;
        }

        private static bool PointOnSegmentXZ(Vector3 point, Vector3 start, Vector3 end)
        {
            const float epsilon = 0.00001f;
            return point.x >= Mathf.Min(start.x, end.x) - epsilon &&
                   point.x <= Mathf.Max(start.x, end.x) + epsilon &&
                   point.z >= Mathf.Min(start.z, end.z) - epsilon &&
                   point.z <= Mathf.Max(start.z, end.z) + epsilon;
        }

        private static bool PointInRingXZ(Vector3 point, List<Vector3> ring)
        {
            var inside = false;
            for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
            {
                var a = ring[i];
                var b = ring[j];
                var crosses = (a.z > point.z) != (b.z > point.z) &&
                    point.x < (b.x - a.x) * (point.z - a.z) /
                    ((b.z - a.z) + 0.0000001f) + a.x;
                if (crosses)
                    inside = !inside;
            }

            return inside;
        }

        private static List<Vector3> SpliceHole(
            List<Vector3> shell,
            int shellIndex,
            List<Vector3> hole,
            int holeIndex)
        {
            var result = new List<Vector3>(shell.Count + hole.Count + 2);
            for (var i = 0; i <= shellIndex; i++)
                result.Add(shell[i]);

            for (var i = 0; i < hole.Count; i++)
                result.Add(hole[(holeIndex + i) % hole.Count]);

            result.Add(hole[holeIndex]);
            result.Add(shell[shellIndex]);

            for (var i = shellIndex + 1; i < shell.Count; i++)
                result.Add(shell[i]);

            return result;
        }

        private static bool IsSameXZ(Vector3 a, Vector3 b)
        {
            var dx = a.x - b.x;
            var dz = a.z - b.z;

            return dx * dx + dz * dz < 0.0001f;
        }

        private static float SignedAreaXZ(List<Vector3> vertices, List<int> indices)
        {
            var area = 0f;

            for (var i = 0; i < indices.Count; i++)
            {
                var a = vertices[indices[i]];
                var b = vertices[indices[(i + 1) % indices.Count]];

                area += a.x * b.z - b.x * a.z;
            }

            return area * 0.5f;
        }

        private static bool IsConvexXZ(Vector3 a, Vector3 b, Vector3 c, bool ccw)
        {
            var cross = CrossXZ(a, b, c);

            return ccw ? cross > 0.00001f : cross < -0.00001f;
        }

        private static float CrossXZ(Vector3 a, Vector3 b, Vector3 c)
        {
            return (b.x - a.x) * (c.z - a.z) - (b.z - a.z) * (c.x - a.x);
        }

        private static bool PointInTriangleXZ(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
        {
            const float epsilon = 0.00001f;

            var c1 = CrossXZ(a, b, p);
            var c2 = CrossXZ(b, c, p);
            var c3 = CrossXZ(c, a, p);

            var hasNegative = c1 < -epsilon || c2 < -epsilon || c3 < -epsilon;
            var hasPositive = c1 > epsilon || c2 > epsilon || c3 > epsilon;

            if (hasNegative && hasPositive)
                return false;

            // Boundary points do not invalidate an ear. This is essential for
            // weakly-simple polygons created by zero-width hole bridges.
            return Mathf.Abs(c1) > epsilon && Mathf.Abs(c2) > epsilon && Mathf.Abs(c3) > epsilon;
        }
    }
}
