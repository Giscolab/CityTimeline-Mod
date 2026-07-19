using System.Collections.Generic;
using UnityEngine;
using CityTimelineMod.Rendering.Materials;
using CityTimelineMod.Rendering.Railways;
using CityTimelineMod.Rendering.Roads;

namespace CityTimelineMod.Rendering.Core
{
    internal sealed class ProgressiveOverlayRebuildState
    {
        internal OverlayRebuildPhase Phase = OverlayRebuildPhase.None;
        internal string Reason = "";
        internal System.Diagnostics.Stopwatch Stopwatch;
        internal OverlayRenderMaterials Materials;
        internal double OriginLon;
        internal double OriginLat;
        internal int Stride;
        internal readonly RoadRenderCounters RoadCounters = new RoadRenderCounters();
        internal readonly RailwayRenderCounters RailwayCounters = new RailwayRenderCounters();
        internal List<RailwayRenderChunk> RailwayChunks = new List<RailwayRenderChunk>();
        internal List<RoadRenderChunk> RoadChunks = new List<RoadRenderChunk>();
        internal List<RoadRenderChunk> PathChunks = new List<RoadRenderChunk>();
        internal int RailwayChunkIndex;
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
