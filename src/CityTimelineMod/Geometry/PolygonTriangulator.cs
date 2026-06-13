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
                    break;

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

            return !(hasNegative && hasPositive);
        }
    }
}
