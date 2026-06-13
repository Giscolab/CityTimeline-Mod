using CityTimelineMod.Rendering.Batching;
using UnityEngine;

namespace CityTimelineMod.Rendering.Water
{
    internal static class WaterGeometryBuilder
    {
        internal static bool AppendWaterSegmentBox(
            WaterMeshBatch batch,
            Vector3 a,
            Vector3 b,
            float segmentWidth,
            float segmentHeight
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

            var width = Mathf.Max(0.1f, segmentWidth);
            var height = Mathf.Max(0.1f, segmentHeight);

            var overlap = width * 0.35f;
            var halfOverlap = overlap * 0.5f;

            var ax = a.x - ux * halfOverlap;
            var az = a.z - uz * halfOverlap;
            var bx = b.x + ux * halfOverlap;
            var bz = b.z + uz * halfOverlap;

            var px = -uz * width * 0.5f;
            var pz = ux * width * 0.5f;

            var baseIndex = batch.Vertices.Count;

            var aLeftBottom = new Vector3(ax + px, a.y, az + pz);
            var aRightBottom = new Vector3(ax - px, a.y, az - pz);
            var bLeftBottom = new Vector3(bx + px, b.y, bz + pz);
            var bRightBottom = new Vector3(bx - px, b.y, bz - pz);

            var aLeftTop = new Vector3(aLeftBottom.x, aLeftBottom.y + height, aLeftBottom.z);
            var aRightTop = new Vector3(aRightBottom.x, aRightBottom.y + height, aRightBottom.z);
            var bLeftTop = new Vector3(bLeftBottom.x, bLeftBottom.y + height, bLeftBottom.z);
            var bRightTop = new Vector3(bRightBottom.x, bRightBottom.y + height, bRightBottom.z);

            batch.Vertices.Add(aLeftBottom);
            batch.Vertices.Add(aRightBottom);
            batch.Vertices.Add(bLeftBottom);
            batch.Vertices.Add(bRightBottom);
            batch.Vertices.Add(aLeftTop);
            batch.Vertices.Add(aRightTop);
            batch.Vertices.Add(bLeftTop);
            batch.Vertices.Add(bRightTop);

            MeshTriangleUtil.AddQuad(batch.Triangles, baseIndex + 4, baseIndex + 6, baseIndex + 7, baseIndex + 5); // top
            MeshTriangleUtil.AddQuad(batch.Triangles, baseIndex + 0, baseIndex + 1, baseIndex + 3, baseIndex + 2); // bottom
            MeshTriangleUtil.AddQuad(batch.Triangles, baseIndex + 0, baseIndex + 2, baseIndex + 6, baseIndex + 4); // left
            MeshTriangleUtil.AddQuad(batch.Triangles, baseIndex + 1, baseIndex + 5, baseIndex + 7, baseIndex + 3); // right
            MeshTriangleUtil.AddQuad(batch.Triangles, baseIndex + 0, baseIndex + 4, baseIndex + 5, baseIndex + 1); // start
            MeshTriangleUtil.AddQuad(batch.Triangles, baseIndex + 2, baseIndex + 3, baseIndex + 7, baseIndex + 6); // end

            batch.SegmentCount++;

            return true;
        }
    }
}
