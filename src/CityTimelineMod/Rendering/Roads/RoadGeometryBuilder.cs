using System.Collections.Generic;
using CityTimelineMod.Rendering.Batching;
using UnityEngine;

namespace CityTimelineMod.Rendering.Roads
{
    internal static class RoadGeometryBuilder
    {
        internal static bool AppendRoadSegmentRibbon(
            RoadMeshBatch batch,
            Vector3 a,
            Vector3 b,
            float roadWidth,
            float ribbonYOffset
        )
        {
            if (batch == null)
                return false;

            var dx = b.x - a.x;
            var dz = b.z - a.z;
            var lengthSq = dx * dx + dz * dz;

            if (lengthSq < 0.01f)
                return false;

            var length = Mathf.Sqrt(lengthSq);
            var ux = dx / length;
            var uz = dz / length;
            var width = Mathf.Max(0.1f, roadWidth);

            a.y += ribbonYOffset;
            b.y += ribbonYOffset;

            var px = -uz * width * 0.5f;
            var pz = ux * width * 0.5f;
            var baseIndex = batch.Vertices.Count;

            var aLeft = new Vector3(a.x + px, a.y, a.z + pz);
            var aRight = new Vector3(a.x - px, a.y, a.z - pz);
            var bLeft = new Vector3(b.x + px, b.y, b.z + pz);
            var bRight = new Vector3(b.x - px, b.y, b.z - pz);

            batch.Vertices.Add(aLeft);
            batch.Vertices.Add(aRight);
            batch.Vertices.Add(bLeft);
            batch.Vertices.Add(bRight);

            batch.Triangles.Add(baseIndex + 0);
            batch.Triangles.Add(baseIndex + 2);
            batch.Triangles.Add(baseIndex + 3);

            batch.Triangles.Add(baseIndex + 0);
            batch.Triangles.Add(baseIndex + 3);
            batch.Triangles.Add(baseIndex + 1);

            batch.SegmentCount++;
            return true;
        }

        internal static int AppendRoadPolylineRibbon(
            RoadMeshBatch batch,
            IList<Vector3> points,
            float roadWidth,
            float ribbonYOffset
        )
        {
            float ignoredDistance;

            return AppendRoadPolylineRibbonInternal(
                batch,
                points,
                roadWidth,
                ribbonYOffset,
                false,
                1f,
                0f,
                0f,
                1f,
                out ignoredDistance
            );
        }

        internal static int AppendTexturedRoadPolylineRibbon(
            RoadMeshBatch batch,
            IList<Vector3> points,
            float roadWidth,
            float ribbonYOffset,
            float longitudinalRepeatMeters,
            float distanceOffsetMeters,
            float uMin,
            float uMax,
            out float appendedDistanceMeters
        )
        {
            return AppendRoadPolylineRibbonInternal(
                batch,
                points,
                roadWidth,
                ribbonYOffset,
                true,
                longitudinalRepeatMeters,
                distanceOffsetMeters,
                uMin,
                uMax,
                out appendedDistanceMeters
            );
        }

        private static int AppendRoadPolylineRibbonInternal(
            RoadMeshBatch batch,
            IList<Vector3> points,
            float roadWidth,
            float ribbonYOffset,
            bool generateUv,
            float longitudinalRepeatMeters,
            float distanceOffsetMeters,
            float uMin,
            float uMax,
            out float appendedDistanceMeters
        )
        {
            appendedDistanceMeters = 0f;

            if (batch == null || points == null || points.Count < 2)
                return 0;

            // A RoadMeshBatch must never mix textured and non-textured geometry.
            if (generateUv)
            {
                if (batch.UV0.Count != batch.Vertices.Count)
                    return 0;
            }
            else if (batch.UV0.Count != 0)
            {
                return 0;
            }

            var cleaned = new List<Vector3>(points.Count);

            for (var i = 0; i < points.Count; i++)
            {
                var p = points[i];
                p.y += ribbonYOffset;

                if (
                    cleaned.Count == 0 ||
                    HasMeaningfulHorizontalDistance(
                        cleaned[cleaned.Count - 1],
                        p
                    )
                )
                {
                    cleaned.Add(p);
                }
            }

            if (cleaned.Count < 2)
                return 0;

            var segmentNormals = new List<Vector2>(cleaned.Count - 1);
            var segmentLengths = new List<float>(cleaned.Count - 1);

            for (var i = 0; i < cleaned.Count - 1; i++)
            {
                var a = cleaned[i];
                var b = cleaned[i + 1];

                var dx = b.x - a.x;
                var dz = b.z - a.z;
                var lengthSq = dx * dx + dz * dz;

                if (lengthSq < 0.01f)
                {
                    segmentNormals.Add(Vector2.zero);
                    segmentLengths.Add(0f);
                    continue;
                }

                var length = Mathf.Sqrt(lengthSq);
                var ux = dx / length;
                var uz = dz / length;

                segmentNormals.Add(new Vector2(-uz, ux));
                segmentLengths.Add(length);
            }

            var halfWidth = Mathf.Max(0.1f, roadWidth) * 0.5f;
            var baseIndex = batch.Vertices.Count;

            var safeRepeatMeters = Mathf.Max(
                0.01f,
                longitudinalRepeatMeters
            );

            var safeUMin = Mathf.Clamp01(uMin);
            var safeUMax = Mathf.Clamp01(uMax);

            if (safeUMax <= safeUMin)
            {
                safeUMin = 0f;
                safeUMax = 1f;
            }

            var cumulativeDistance = Mathf.Max(
                0f,
                distanceOffsetMeters
            );

            for (var i = 0; i < cleaned.Count; i++)
            {
                if (i > 0)
                    cumulativeDistance += segmentLengths[i - 1];

                var offset = ResolvePolylineOffset(
                    segmentNormals,
                    i,
                    halfWidth
                );

                var p = cleaned[i];

                batch.Vertices.Add(
                    new Vector3(
                        p.x + offset.x,
                        p.y,
                        p.z + offset.y
                    )
                );

                batch.Vertices.Add(
                    new Vector3(
                        p.x - offset.x,
                        p.y,
                        p.z - offset.y
                    )
                );

                if (generateUv)
                {
                    var v = cumulativeDistance / safeRepeatMeters;

                    batch.UV0.Add(new Vector2(safeUMin, v));
                    batch.UV0.Add(new Vector2(safeUMax, v));
                }
            }

            appendedDistanceMeters =
                cumulativeDistance -
                Mathf.Max(0f, distanceOffsetMeters);

            var appendedSegments = 0;

            for (var i = 0; i < cleaned.Count - 1; i++)
            {
                var segmentNormal = segmentNormals[i];

                if (segmentNormal.sqrMagnitude < 0.0001f)
                    continue;

                var leftA = baseIndex + i * 2;
                var rightA = leftA + 1;
                var leftB = baseIndex + (i + 1) * 2;
                var rightB = leftB + 1;

                batch.Triangles.Add(leftA);
                batch.Triangles.Add(leftB);
                batch.Triangles.Add(rightB);

                batch.Triangles.Add(leftA);
                batch.Triangles.Add(rightB);
                batch.Triangles.Add(rightA);

                appendedSegments++;
            }

            batch.SegmentCount += appendedSegments;

            return appendedSegments;
        }

        private static Vector2 ResolvePolylineOffset(
            IList<Vector2> segmentNormals,
            int pointIndex,
            float halfWidth
        )
        {
            if (segmentNormals == null || segmentNormals.Count == 0)
                return new Vector2(halfWidth, 0f);

            if (pointIndex <= 0)
                return SafeScaledNormal(segmentNormals[0], halfWidth);

            if (pointIndex >= segmentNormals.Count)
            {
                return SafeScaledNormal(
                    segmentNormals[segmentNormals.Count - 1],
                    halfWidth
                );
            }

            var previous = segmentNormals[pointIndex - 1];
            var next = segmentNormals[pointIndex];

            if (previous.sqrMagnitude < 0.0001f)
                return SafeScaledNormal(next, halfWidth);

            if (next.sqrMagnitude < 0.0001f)
                return SafeScaledNormal(previous, halfWidth);

            var miter = previous + next;

            if (miter.sqrMagnitude < 0.0001f)
                return SafeScaledNormal(next, halfWidth);

            miter.Normalize();

            var denominator = Mathf.Abs(
                Vector2.Dot(miter, next)
            );

            if (denominator < 0.2f)
                return SafeScaledNormal(next, halfWidth);

            var miterLength = halfWidth / denominator;
            var maxMiterLength = halfWidth * 2.5f;

            if (miterLength > maxMiterLength)
                miterLength = maxMiterLength;

            return miter * miterLength;
        }

        private static Vector2 SafeScaledNormal(
            Vector2 normal,
            float halfWidth
        )
        {
            if (normal.sqrMagnitude < 0.0001f)
                return new Vector2(halfWidth, 0f);

            normal.Normalize();
            return normal * halfWidth;
        }

        private static bool HasMeaningfulHorizontalDistance(
            Vector3 a,
            Vector3 b
        )
        {
            var dx = b.x - a.x;
            var dz = b.z - a.z;

            return dx * dx + dz * dz >= 0.01f;
        }

        internal static int AppendTexturedOffsetPolylineStrip(
            RoadMeshBatch batch,
            IList<Vector3> points,
            float stripWidth,
            float centerOffset,
            float ribbonYOffset,
            float uMin,
            float uMax,
            float longitudinalRepeatMeters,
            float distanceOffset,
            bool mirrorU,
            out float distanceEnd
        )
        {
            distanceEnd = distanceOffset;

            if (batch == null || points == null || points.Count < 2)
                return 0;

            var cleaned = new List<Vector3>(points.Count);

            for (var i = 0; i < points.Count; i++)
            {
                var p = points[i];
                p.y += ribbonYOffset;

                if (
                    cleaned.Count == 0 ||
                    HasMeaningfulHorizontalDistance(
                        cleaned[cleaned.Count - 1],
                        p
                    )
                )
                {
                    cleaned.Add(p);
                }
            }

            if (cleaned.Count < 2)
                return 0;

            var segmentNormals =
                new List<Vector2>(cleaned.Count - 1);

            var segmentLengths =
                new List<float>(cleaned.Count - 1);

            for (var i = 0; i < cleaned.Count - 1; i++)
            {
                var a = cleaned[i];
                var b = cleaned[i + 1];

                var dx = b.x - a.x;
                var dz = b.z - a.z;
                var lengthSq = dx * dx + dz * dz;

                if (lengthSq < 0.01f)
                {
                    segmentNormals.Add(Vector2.zero);
                    segmentLengths.Add(0f);
                    continue;
                }

                var length = Mathf.Sqrt(lengthSq);

                segmentNormals.Add(
                    new Vector2(
                        -dz / length,
                        dx / length
                    )
                );

                segmentLengths.Add(length);
            }

            var safeStripWidth =
                Mathf.Max(0.05f, stripWidth);

            var halfStripWidth =
                safeStripWidth * 0.5f;

            var edgeA =
                centerOffset + halfStripWidth;

            var edgeB =
                centerOffset - halfStripWidth;

            var repeat =
                Mathf.Max(
                    0.01f,
                    longitudinalRepeatMeters
                );

            var firstU =
                mirrorU ? uMax : uMin;

            var secondU =
                mirrorU ? uMin : uMax;

            var baseIndex =
                batch.Vertices.Count;

            var cumulativeDistance =
                distanceOffset;

            for (var i = 0; i < cleaned.Count; i++)
            {
                if (i > 0)
                {
                    cumulativeDistance +=
                        segmentLengths[i - 1];
                }

                var offsetA =
                    ResolveSignedPolylineOffset(
                        segmentNormals,
                        i,
                        edgeA
                    );

                var offsetB =
                    ResolveSignedPolylineOffset(
                        segmentNormals,
                        i,
                        edgeB
                    );

                var p = cleaned[i];

                batch.Vertices.Add(
                    new Vector3(
                        p.x + offsetA.x,
                        p.y,
                        p.z + offsetA.y
                    )
                );

                batch.Vertices.Add(
                    new Vector3(
                        p.x + offsetB.x,
                        p.y,
                        p.z + offsetB.y
                    )
                );

                var v =
                    cumulativeDistance / repeat;

                batch.UV0.Add(
                    new Vector2(firstU, v)
                );

                batch.UV0.Add(
                    new Vector2(secondU, v)
                );
            }

            var appendedSegments = 0;

            for (var i = 0; i < cleaned.Count - 1; i++)
            {
                if (
                    segmentNormals[i].sqrMagnitude <
                    0.0001f
                )
                {
                    continue;
                }

                var a0 =
                    baseIndex + i * 2;

                var a1 =
                    a0 + 1;

                var b0 =
                    baseIndex + (i + 1) * 2;

                var b1 =
                    b0 + 1;

                batch.Triangles.Add(a0);
                batch.Triangles.Add(b0);
                batch.Triangles.Add(b1);

                batch.Triangles.Add(a0);
                batch.Triangles.Add(b1);
                batch.Triangles.Add(a1);

                appendedSegments++;
            }

            batch.SegmentCount +=
                appendedSegments;

            distanceEnd =
                cumulativeDistance;

            return appendedSegments;
        }

        private static Vector2 ResolveSignedPolylineOffset(
            IList<Vector2> segmentNormals,
            int pointIndex,
            float signedDistance
        )
        {
            if (Mathf.Abs(signedDistance) < 0.0001f)
                return Vector2.zero;

            var offset =
                ResolvePolylineOffset(
                    segmentNormals,
                    pointIndex,
                    Mathf.Abs(signedDistance)
                );

            return signedDistance < 0f
                ? -offset
                : offset;
        }
        internal static bool AppendRoadArrow(
            RoadArrowBatch batch,
            Vector3 center,
            Vector3 a,
            Vector3 b,
            float size
        )
        {
            if (batch == null)
                return false;

            var dx = b.x - a.x;
            var dz = b.z - a.z;
            var lengthSq = dx * dx + dz * dz;

            if (lengthSq < 0.01f)
                return false;

            var length = Mathf.Sqrt(lengthSq);
            var ux = dx / length;
            var uz = dz / length;
            var safeSize = Mathf.Max(0.1f, size);
            var halfWidth = safeSize * 0.35f;
            var halfLength = safeSize * 0.5f;

            var px = -uz * halfWidth;
            var pz = ux * halfWidth;

            var tip = new Vector3(
                center.x + ux * halfLength,
                center.y,
                center.z + uz * halfLength
            );

            var tailCenter = new Vector3(
                center.x - ux * halfLength,
                center.y,
                center.z - uz * halfLength
            );

            var left = new Vector3(
                tailCenter.x + px,
                center.y,
                tailCenter.z + pz
            );

            var right = new Vector3(
                tailCenter.x - px,
                center.y,
                tailCenter.z - pz
            );

            var baseIndex = batch.Vertices.Count;

            batch.Vertices.Add(tip);
            batch.Vertices.Add(left);
            batch.Vertices.Add(right);

            batch.Triangles.Add(baseIndex);
            batch.Triangles.Add(baseIndex + 1);
            batch.Triangles.Add(baseIndex + 2);

            batch.Triangles.Add(baseIndex);
            batch.Triangles.Add(baseIndex + 2);
            batch.Triangles.Add(baseIndex + 1);

            batch.ArrowCount++;

            return true;
        }
    }
}

