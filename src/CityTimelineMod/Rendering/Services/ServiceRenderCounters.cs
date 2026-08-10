namespace CityTimelineMod.Rendering.Services
{
    internal sealed class ServiceRenderCounters
    {
        internal int SourcePoints;
        internal int EligiblePoints;
        internal int CreatedPoints;
        internal int CreatedMeshObjects;
        internal int CreatedChunks;
        internal int LoadedFamilies;
        internal int MissingFamilies;
        internal int FailedFamilies;
        internal int SkippedByFilter;
        internal int SkippedByLimit;
        internal int SkippedOutsideMap;
        internal int SkippedInvalid;

        internal void Reset()
        {
            SourcePoints = 0;
            EligiblePoints = 0;
            CreatedPoints = 0;
            CreatedMeshObjects = 0;
            CreatedChunks = 0;
            LoadedFamilies = 0;
            MissingFamilies = 0;
            FailedFamilies = 0;
            SkippedByFilter = 0;
            SkippedByLimit = 0;
            SkippedOutsideMap = 0;
            SkippedInvalid = 0;
        }

        internal ServiceRenderCounters Copy()
        {
            return new ServiceRenderCounters
            {
                SourcePoints = SourcePoints,
                EligiblePoints = EligiblePoints,
                CreatedPoints = CreatedPoints,
                CreatedMeshObjects = CreatedMeshObjects,
                CreatedChunks = CreatedChunks,
                LoadedFamilies = LoadedFamilies,
                MissingFamilies = MissingFamilies,
                FailedFamilies = FailedFamilies,
                SkippedByFilter = SkippedByFilter,
                SkippedByLimit = SkippedByLimit,
                SkippedOutsideMap = SkippedOutsideMap,
                SkippedInvalid = SkippedInvalid
            };
        }
    }
}
