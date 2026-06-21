using Colossal.Mathematics;
using Game.Simulation;

namespace CityTimelineMod.PlayableWorld
{
    internal static class PlayableWorldBoundsUtility
    {
        private static bool _logged;

        internal static Bounds3 GetPlayableBounds(
            ref TerrainHeightData terrainData
        )
        {
            Bounds3 bounds =
                TerrainUtils.GetBounds(ref terrainData);

            return ExpandToPlayableWorld(bounds);
        }

        internal static Bounds3 ExpandToPlayableWorld(
            Bounds3 bounds
        )
        {
            if (!PlayableWorldState.Enabled)
                return bounds;

            if (!PlayableWorldState.Initialized)
                return bounds;

            float playableWidth =
                PlayableWorldState.Max.x -
                PlayableWorldState.Min.x;

            float detailedWidth =
                bounds.max.x -
                bounds.min.x;

            if (playableWidth <= detailedWidth + 1f)
                return bounds;

            bounds.min.x = PlayableWorldState.Min.x;
            bounds.min.z = PlayableWorldState.Min.y;
            bounds.max.x = PlayableWorldState.Max.x;
            bounds.max.z = PlayableWorldState.Max.y;

            if (!_logged)
            {
                _logged = true;

                Util.Log.Info(
                    "[PlayableWorld] terrain bounds expanded to " +
                    "captured world bounds. min=" +
                    bounds.min.xz +
                    ", max=" +
                    bounds.max.xz
                );
            }

            return bounds;
        }
    }
}
