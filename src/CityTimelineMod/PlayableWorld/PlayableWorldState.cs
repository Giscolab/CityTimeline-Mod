using Colossal.Mathematics;
using Unity.Mathematics;

namespace CityTimelineMod.PlayableWorld
{
    internal static class PlayableWorldState
    {
        internal static bool Enabled = true;

        internal static bool Initialized { get; private set; }

        internal static float2 Min { get; private set; }

        internal static float2 Max { get; private set; }

        internal static void Update(float2 worldOffset, float2 worldSize)
        {
            Min = worldOffset;
            Max = worldOffset + worldSize;
            Initialized = true;

            Util.Log.Info(
                "[PlayableWorld] world bounds captured: min=" +
                Min +
                ", max=" +
                Max +
                ", size=" +
                worldSize
            );
        }

        internal static bool Contains(Bounds3 bounds)
        {
            if (!Enabled || !Initialized)
                return false;

            return
                bounds.min.x >= Min.x &&
                bounds.min.z >= Min.y &&
                bounds.max.x <= Max.x &&
                bounds.max.z <= Max.y;
        }

        internal static void Reset()
        {
            Initialized = false;
            Min = float2.zero;
            Max = float2.zero;
        }
    }
}
