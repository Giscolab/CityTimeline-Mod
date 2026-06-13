namespace CityTimelineMod.Rendering.Core
{
    internal enum OverlayRebuildPhase
    {
        None,
        Clearing,
        Zoning,
        Water,
        RoadChunks,
        PathChunks,
        Arrows,
        Labels,
        Done
    }
}
