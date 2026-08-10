namespace CityTimelineMod.Rendering.Core
{
    internal enum OverlayRebuildPhase
    {
        None,
        Clearing,
        Zoning,
        Water,
        ServiceChunks,
        RailwayChunks,
        RoadChunks,
        PathChunks,
        Arrows,
        Labels,
        Done
    }
}
