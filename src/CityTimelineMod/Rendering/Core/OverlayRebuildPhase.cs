namespace CityTimelineMod.Rendering.Core
{
    internal enum OverlayRebuildPhase
    {
        None,
        Clearing,
        Zoning,
        Water,
        RailwayChunks,
        RoadChunks,
        PathChunks,
        Arrows,
        Labels,
        Done
    }
}
