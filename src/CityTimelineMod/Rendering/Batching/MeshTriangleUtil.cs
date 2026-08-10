using System.Collections.Generic;

namespace CityTimelineMod.Rendering.Batching
{
    internal static class MeshTriangleUtil
    {
        internal static void AddQuad(List<int> triangles, int a, int b, int c, int d)
        {
            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);

            triangles.Add(a);
            triangles.Add(c);
            triangles.Add(d);
        }
    }
}
