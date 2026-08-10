using Unity.Mathematics;

namespace CityTimelineMod.Roads
{
    internal static partial class RuntimeRoadSpawner
    {
        private sealed class RuntimeRoadImportResult
        {
            public int ProcessedRoads;
            public int QueuedSegments;
            public int QueuedInside14336;
            public int QueuedOutside14336Inside57344;
            public int SkippedOutside57344;
            public int RequestedMaxSegments;
            public int EffectiveMaxSegments;
            public bool CappedByVanillaSingleApplyLimit;
            public int BaselineEdgeRoadBeforeImport;
            public int VanillaSafeSessionEdgeRoadBudget;
            public bool CappedByVanillaSessionBudget;
            public int SkippedPath;
            public int SkippedSourceFilter;
            public int SkippedHighwayFilter;
            public int SkippedStageFilter;
            public int SkippedBridgeTunnel;
            public int SkippedShort;
            public int SkippedDuplicate;
            public int SkippedNoPrefab;
            public int SkippedParkingAisle;
            public int SkippedUnpaved;
            public int TerrainHits;
            public int TerrainMisses;
        }

        private struct WorldRoadPoint
        {
            public float3 Position;
            public bool IsJunction;
        }
    }
}
