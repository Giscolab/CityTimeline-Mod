// CTM CONTRACT GUARD: Never disable the 57 km LargeMap + PlayableWorld contract unless explicitly requested.
using Colossal.Mathematics;
using CityTimelineMod.LargeMap;
using Unity.Mathematics;

namespace CityTimelineMod.PlayableWorld
{
    internal static class PlayableWorldState
    {
        internal static bool Installed { get; private set; } = false;

        internal static bool HasActiveWorld { get; private set; }

        internal static ulong ActiveWorldSequence { get; private set; }

        internal static bool Enabled
        {
            // "Enabled" means ready for mutation. Harmony ownership may be
            // installed while current terrain bounds are still unavailable.
            get
            {
                return Mod.RuntimeEnabled &&
                    CityTimelineLargeMapState.Enabled &&
                    Installed &&
                    Initialized &&
                    HasActiveWorld;
            }
        }

        internal static bool Initialized { get; private set; }

        internal static float2 Min { get; private set; }

        internal static float2 Max { get; private set; }

        internal static void Update(
            ulong worldSequence,
            float2 worldOffset,
            float2 worldSize)
        {
            TryUpdate(worldSequence, worldOffset, worldSize);
        }

        internal static bool TryUpdate(
            ulong worldSequence,
            float2 worldOffset,
            float2 worldSize)
        {
            if (!Mod.RuntimeEnabled ||
                !Installed ||
                !HasActiveWorld ||
                worldSequence != ActiveWorldSequence ||
                !CityTimelineLargeMapState.Enabled)
            {
                return false;
            }

            float2 max = worldOffset + worldSize;

            if (!IsFinite(worldOffset.x) ||
                !IsFinite(worldOffset.y) ||
                !IsFinite(worldSize.x) ||
                !IsFinite(worldSize.y) ||
                !IsFinite(max.x) ||
                !IsFinite(max.y) ||
                worldSize.x <= 0f ||
                worldSize.y <= 0f)
            {
                ResetBounds();
                Util.Log.Error(
                    "[PlayableWorld] rejected invalid world bounds: offset=" +
                    worldOffset + ", size=" + worldSize + "."
                );
                return false;
            }

            var expectedHalf = CityTimelineLargeMapState.HalfMapSizeMetersFloat;
            var expectedSize = CityTimelineLargeMapState.MapSizeMetersFloat;
            if (math.abs(worldOffset.x + expectedHalf) > 1f ||
                math.abs(worldOffset.y + expectedHalf) > 1f ||
                math.abs(worldSize.x - expectedSize) > 1f ||
                math.abs(worldSize.y - expectedSize) > 1f)
            {
                ResetBounds();
                Util.Log.Error(
                    "[PlayableWorld] rejected non-LargeMap bounds: offset=" +
                    worldOffset + ", size=" + worldSize + "."
                );
                return false;
            }

            Min = worldOffset;
            Max = max;
            Initialized = true;

            Util.Log.Info(
                "[PlayableWorld] world bounds captured: min=" +
                Min +
                ", max=" +
                Max +
                ", size=" +
                worldSize
            );

            return true;
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

        internal static void Enable(ulong worldSequence)
        {
            Installed = true;
            HasActiveWorld = true;
            ActiveWorldSequence = worldSequence;
            ResetBounds();
        }

        internal static void Disable()
        {
            Installed = false;
            HasActiveWorld = false;
            ActiveWorldSequence = 0;
            ResetBounds();
        }

        internal static void MarkUninitialized()
        {
            ResetBounds();
        }

        private static void ResetBounds()
        {
            Initialized = false;
            Min = float2.zero;
            Max = float2.zero;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
