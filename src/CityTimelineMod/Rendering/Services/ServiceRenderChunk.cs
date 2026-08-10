using System.Collections.Generic;
using UnityEngine;

namespace CityTimelineMod.Rendering.Services
{
    internal sealed class ServiceRenderPoint
    {
        internal readonly GeoServicePoint Source;
        internal readonly Vector3 World;

        internal ServiceRenderPoint(GeoServicePoint source, Vector3 world)
        {
            Source = source;
            World = world;
        }
    }

    internal sealed class ServiceRenderChunk
    {
        internal int ChunkX;
        internal int ChunkZ;
        internal string FamilyKey = "";
        internal readonly List<ServiceRenderPoint> Points = new List<ServiceRenderPoint>();
        internal float BoundsMinX = float.MaxValue;
        internal float BoundsMaxX = float.MinValue;
        internal float BoundsMinZ = float.MaxValue;
        internal float BoundsMaxZ = float.MinValue;

        internal string Key
        {
            get { return FamilyKey + ":" + ChunkX + ":" + ChunkZ; }
        }

        internal bool HasBounds
        {
            get { return Points.Count > 0 && BoundsMinX <= BoundsMaxX && BoundsMinZ <= BoundsMaxZ; }
        }

        internal void Add(GeoServicePoint source, Vector3 world)
        {
            Points.Add(new ServiceRenderPoint(source, world));
            IncludePoint(world);
        }

        internal void IncludePoint(Vector3 world)
        {
            BoundsMinX = Mathf.Min(BoundsMinX, world.x);
            BoundsMaxX = Mathf.Max(BoundsMaxX, world.x);
            BoundsMinZ = Mathf.Min(BoundsMinZ, world.z);
            BoundsMaxZ = Mathf.Max(BoundsMaxZ, world.z);
        }
    }
}
