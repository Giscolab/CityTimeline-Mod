using System;
using System.Collections.Generic;
using CityTimelineMod.Importers;
using CityTimelineMod.Rendering.Batching;
using CityTimelineMod.Rendering.Core;
using CityTimelineMod.Rendering.Materials;
using CityTimelineMod.Rendering.Roads;
using CityTimelineMod.Util;
using UnityEngine;

namespace CityTimelineMod.Rendering
{
    internal sealed partial class GroundOverlayBehaviour
    {
        private bool AppendRoadSegmentBox(
            RoadMeshBatch batch,
            Vector3 a,
            Vector3 b,
            float roadWidth,
            float roadHeight
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
            var height = Mathf.Max(0.1f, roadHeight);

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

            batch.Vertices.Add(aLeftBottom);   // 0
            batch.Vertices.Add(aRightBottom);  // 1
            batch.Vertices.Add(bLeftBottom);   // 2
            batch.Vertices.Add(bRightBottom);  // 3
            batch.Vertices.Add(aLeftTop);      // 4
            batch.Vertices.Add(aRightTop);     // 5
            batch.Vertices.Add(bLeftTop);      // 6
            batch.Vertices.Add(bRightTop);     // 7

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
