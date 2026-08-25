using System.Collections.Generic;
using UnityEngine;

namespace CityTimelineMod.Rendering.Batching
{
    internal sealed class RoadMeshBatch
    {
        internal readonly List<Vector3> Vertices = new List<Vector3>();
        internal readonly List<int> Triangles = new List<int>();

        // Empty for the legacy solid-color ribbons.
        // Populated only by explicitly textured road batches.
        internal readonly List<Vector2> UV0 = new List<Vector2>();

        internal Material Material;
        internal int ChunkIndex;
        internal int SegmentCount;
    }
}
