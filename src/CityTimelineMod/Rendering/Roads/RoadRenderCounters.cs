namespace CityTimelineMod.Rendering.Roads
{
    internal sealed class RoadRenderCounters
    {
        internal int CreatedRoadSegments;
        internal int CreatedPathSegments;
        internal int CreatedRoadMeshObjects;
        internal int CreatedPathMeshObjects;
        internal int SourceRoadLines;
        internal int SourcePathLines;
        internal int EligibleRoadLines;
        internal int EligiblePathLines;
        internal int SkippedRoadByFilter;
        internal int SkippedPathByFilter;
        internal int SkippedRoadByTier;
        internal int SkippedPathByTier = 0;
        internal int SkippedRoadByLimit;
        internal int SkippedPathByLimit;
        internal int SkippedInvalid;
        internal int CreatedRoadLines;
        internal int CreatedPathLines;
    }
}
