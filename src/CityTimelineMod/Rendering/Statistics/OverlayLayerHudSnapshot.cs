namespace CityTimelineMod.Rendering.Statistics
{
    /// <summary>
    /// Read-only state exposed to the CoHTML layer controls. These values only
    /// affect the visual overlay; no game entity, prefab or network is touched.
    /// </summary>
    internal sealed class OverlayLayerHudSnapshot
    {
        internal bool Available;

        internal bool ZoningVisible;
        internal bool RoadsVisible;
        internal bool PathsVisible;
        internal bool WaterVisible;
        internal bool BoundsVisible;

        internal float ZoningOpacity = 1f;
        internal float RoadsOpacity = 1f;
        internal float PathsOpacity = 1f;
        internal float WaterOpacity = 1f;
        internal float BoundsOpacity = 1f;

        internal static OverlayLayerHudSnapshot Unavailable()
        {
            return new OverlayLayerHudSnapshot { Available = false };
        }
    }
}
