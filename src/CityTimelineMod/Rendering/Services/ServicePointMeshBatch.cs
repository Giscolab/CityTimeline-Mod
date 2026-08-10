using System.Collections.Generic;
using UnityEngine;

namespace CityTimelineMod.Rendering.Services
{
    internal sealed class ServicePointMeshBatch
    {
        internal readonly List<Vector3> Vertices = new List<Vector3>();
        internal readonly List<int> Triangles = new List<int>();
        internal Material Material;
        internal string FamilyKey = "";
        internal int ChunkIndex;
        internal int PointCount;

        internal void ClearGeometry()
        {
            Vertices.Clear();
            Triangles.Clear();
            PointCount = 0;
        }
    }
}
