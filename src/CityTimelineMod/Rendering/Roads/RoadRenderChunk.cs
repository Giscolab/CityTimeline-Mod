using System.Collections.Generic;
using CityTimelineMod.Importers;
using UnityEngine;

namespace CityTimelineMod.Rendering.Roads
{
    internal sealed class RoadRenderChunk
    {
        internal int ChunkX;
        internal int ChunkZ;
        internal bool IsPath;
        internal readonly List<GeoRoadLine> Lines = new List<GeoRoadLine>();
        internal float BoundsMinX = float.MaxValue;
        internal float BoundsMaxX = float.MinValue;
        internal float BoundsMinZ = float.MaxValue;
        internal float BoundsMaxZ = float.MinValue;

        internal void IncludePoint(Vector3 world)
        {
            if (world.x < BoundsMinX)
                BoundsMinX = world.x;

            if (world.x > BoundsMaxX)
                BoundsMaxX = world.x;

            if (world.z < BoundsMinZ)
                BoundsMinZ = world.z;

            if (world.z > BoundsMaxZ)
                BoundsMaxZ = world.z;
        }
    }
}
