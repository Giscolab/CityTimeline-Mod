using System.Collections.Generic;
using UnityEngine;

namespace CityTimelineMod.Rendering.Batching
{
    internal sealed class ZoningMeshBatch
    {
        internal readonly List<Vector3> Vertices = new List<Vector3>();
        internal readonly List<int> Triangles = new List<int>();
        internal Material Material;
        internal int ChunkIndex;
        internal int PolygonCount;
    }
}
