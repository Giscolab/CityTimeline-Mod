using System.Collections.Generic;
using UnityEngine;

namespace CityTimelineMod.Rendering.Railways
{
    internal sealed class RailwayMeshBatch
    {
        internal readonly List<Vector3> Vertices = new List<Vector3>();
        internal readonly List<int> Triangles = new List<int>();
        internal Material Material;
        internal int ChunkIndex;
        internal int PrimitiveCount;
    }
}
