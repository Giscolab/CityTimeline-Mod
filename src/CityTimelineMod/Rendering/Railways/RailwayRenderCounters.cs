namespace CityTimelineMod.Rendering.Railways
{
    internal sealed class RailwayRenderCounters
    {
        internal int SourceLines;
        internal int EligibleLines;
        internal int CreatedLines;
        internal int CreatedSegments;
        internal int CreatedMeshObjects;
        internal int SkippedByFilter;
        internal int SkippedByLimit;
        internal int SkippedInvalid;

        internal RailwayRenderCounters Copy()
        {
            return new RailwayRenderCounters
            {
                SourceLines = SourceLines,
                EligibleLines = EligibleLines,
                CreatedLines = CreatedLines,
                CreatedSegments = CreatedSegments,
                CreatedMeshObjects = CreatedMeshObjects,
                SkippedByFilter = SkippedByFilter,
                SkippedByLimit = SkippedByLimit,
                SkippedInvalid = SkippedInvalid
            };
        }
    }
}
