using System.Collections.Generic;
using UnityEngine;
using CityTimelineMod.Rendering.Materials;
using CityTimelineMod.Rendering.Railways;
using CityTimelineMod.Rendering.Roads;
using CityTimelineMod.Rendering.Services;
using CityTimelineMod.Rendering.Batching;

namespace CityTimelineMod.Rendering.Core
{
    internal sealed class ProgressiveOverlayRebuildState
    {
        internal OverlayRebuildPhase Phase = OverlayRebuildPhase.None;
        internal string Reason = "";
        internal System.Diagnostics.Stopwatch Stopwatch;
        internal OverlayRenderMaterials Materials;
        internal GameObject StagingRoot;
        internal readonly List<Material> PreviousMaterials = new List<Material>();
        internal int PreviousChildCount;
        internal double OriginLon;
        internal double OriginLat;
        internal int Stride;

        // --- Nouveaux champs pour le zoning progressif ---
        internal readonly Dictionary<string, ZoningMeshBatch> ZoningBatches =
            new Dictionary<string, ZoningMeshBatch>();
        internal readonly Dictionary<string, int> ZoningEligibleCs2Counts =
            new Dictionary<string, int>();
        internal int ZoningPolygonIndex;
        internal int ZoningEligibleMeshes;
        internal int ZoningRenderedPolygons;
        internal int ZoningSkippedByFilter;
        internal int ZoningSkippedByLimit;
        internal int ZoningSourceHoleRings;
        internal int ZoningRenderedHoleRings;
        // --- Fin des nouveaux champs ---

        internal readonly RoadRenderCounters RoadCounters = new RoadRenderCounters();
        internal readonly RailwayRenderCounters RailwayCounters = new RailwayRenderCounters();
        internal readonly ServiceRenderCounters ServiceCounters = new ServiceRenderCounters();
        internal List<ServiceRenderChunk> ServiceChunks = new List<ServiceRenderChunk>();
        internal List<RailwayRenderChunk> RailwayChunks = new List<RailwayRenderChunk>();
        internal List<RoadRenderChunk> RoadChunks = new List<RoadRenderChunk>();
        internal List<RoadRenderChunk> PathChunks = new List<RoadRenderChunk>();
        internal int RailwayChunkIndex;
        internal int ServiceChunkIndex;
        internal int RoadChunkIndex;
        internal int PathChunkIndex;
        internal int CreatedWaterSegments;
        internal int CreatedWaterLines;
        internal int CreatedZoningFillMeshes;
        internal int CreatedRoadArrows;
        internal int CreatedRoadLabels;
        internal Vector3 FirstWorld = Vector3.zero;
        internal Vector3 LastWorld = Vector3.zero;
        internal bool HasEndpoints;
        internal long LastProgressLogMs;

        internal bool IsActive
        {
            get { return Phase != OverlayRebuildPhase.None && Phase != OverlayRebuildPhase.Done; }
        }
    }
}