using UnityEngine;

namespace CityTimelineMod.Rendering.Railways
{
    internal static class RailwayGeometryBuilder
    {
        internal static bool AppendRibbonSegment(
            RailwayMeshBatch batch,
            Vector3 a,
            Vector3 b,
            float width
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
            var halfWidth = Mathf.Max(0.5f, width) * 0.5f;
            var px = -dz / length * halfWidth;
            var pz = dx / length * halfWidth;
            var baseIndex = batch.Vertices.Count;

            batch.Vertices.Add(new Vector3(a.x + px, a.y, a.z + pz));
            batch.Vertices.Add(new Vector3(a.x - px, a.y, a.z - pz));
            batch.Vertices.Add(new Vector3(b.x + px, b.y, b.z + pz));
            batch.Vertices.Add(new Vector3(b.x - px, b.y, b.z - pz));

            batch.Triangles.Add(baseIndex);
            batch.Triangles.Add(baseIndex + 2);
            batch.Triangles.Add(baseIndex + 3);
            batch.Triangles.Add(baseIndex);
            batch.Triangles.Add(baseIndex + 3);
            batch.Triangles.Add(baseIndex + 1);
            batch.PrimitiveCount++;
            return true;
        }

        internal static int AppendDashedRibbonSegment(
            RailwayMeshBatch batch,
            Vector3 a,
            Vector3 b,
            float width,
            float dashLength,
            float gapLength
        )
        {
            var dx = b.x - a.x;
            var dz = b.z - a.z;
            var horizontalLength = Mathf.Sqrt(dx * dx + dz * dz);
            if (horizontalLength < 0.1f)
                return 0;

            var safeDash = Mathf.Max(1f, dashLength);
            var cycle = safeDash + Mathf.Max(1f, gapLength);
            var appended = 0;

            for (var start = 0f; start < horizontalLength; start += cycle)
            {
                var end = Mathf.Min(horizontalLength, start + safeDash);
                if (end - start < 0.1f)
                    continue;

                var dashA = Vector3.Lerp(a, b, start / horizontalLength);
                var dashB = Vector3.Lerp(a, b, end / horizontalLength);
                if (AppendRibbonSegment(batch, dashA, dashB, width))
                    appended++;
            }

            return appended;
        }
    }
}
