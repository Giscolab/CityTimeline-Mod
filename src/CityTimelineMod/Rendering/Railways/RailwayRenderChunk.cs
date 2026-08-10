using System.Collections.Generic;
using UnityEngine;

namespace CityTimelineMod.Rendering.Railways
{
    internal sealed class RailwayRenderChunk
    {
        internal int ChunkX;
        internal int ChunkZ;
        internal readonly List<GeoRailwayLine> Lines = new List<GeoRailwayLine>();
        internal float BoundsMinX = float.MaxValue;
        internal float BoundsMaxX = float.MinValue;
        internal float BoundsMinZ = float.MaxValue;
        internal float BoundsMaxZ = float.MinValue;

        internal void IncludePoint(Vector3 world)
        {
            BoundsMinX = Mathf.Min(BoundsMinX, world.x);
            BoundsMaxX = Mathf.Max(BoundsMaxX, world.x);
            BoundsMinZ = Mathf.Min(BoundsMinZ, world.z);
            BoundsMaxZ = Mathf.Max(BoundsMaxZ, world.z);
        }
    }
}
