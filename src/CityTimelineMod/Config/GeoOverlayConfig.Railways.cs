using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CityTimelineMod.Config
{
    internal sealed partial class GeoOverlayConfig
    {
        internal bool RenderRailways = true;
        internal float RailwayOpacity = 0.9f;
        internal float RailwayThickness = 3f;
        internal float RailwayYOffset = 0.32f;

        internal bool RailwayTrainVisible = true;
        internal bool RailwayTramVisible = true;
        internal bool RailwayLightRailVisible = true;
        internal bool RailwaySubwayVisible = true;
        internal bool RailwayServiceVisible = true;
        internal bool RailwayTunnelsVisible = true;

        internal bool EnableRailwaySpatialChunking = true;
        internal float RailwayChunkSizeMeters = 1024f;
        internal int RailwayChunksPerFrame = 16;
        internal int MaxRailwaySegmentsDebug = 800000;
        internal bool RenderAllRailwaySegments = true;

        private void LoadRailwaySettings(JObject root)
        {
            RenderRailways = GetBool(root, "renderRailways", RenderRailways);
            RailwayOpacity = GetFloat(root, "railwayOpacity", RailwayOpacity);
            RailwayThickness = GetFloat(root, "railwayThickness", RailwayThickness);
            RailwayYOffset = GetFloat(root, "railwayYOffset", RailwayYOffset);

            RailwayTrainVisible = GetBool(root, "railwayTrainVisible", RailwayTrainVisible);
            RailwayTramVisible = GetBool(root, "railwayTramVisible", RailwayTramVisible);
            RailwayLightRailVisible = GetBool(root, "railwayLightRailVisible", RailwayLightRailVisible);
            RailwaySubwayVisible = GetBool(root, "railwaySubwayVisible", RailwaySubwayVisible);
            RailwayServiceVisible = GetBool(root, "railwayServiceVisible", RailwayServiceVisible);
            RailwayTunnelsVisible = GetBool(root, "railwayTunnelsVisible", RailwayTunnelsVisible);

            EnableRailwaySpatialChunking = GetBool(root, "enableRailwaySpatialChunking", EnableRailwaySpatialChunking);
            RailwayChunkSizeMeters = GetFloat(root, "railwayChunkSizeMeters", RailwayChunkSizeMeters);
            RailwayChunksPerFrame = GetInt(root, "railwayChunksPerFrame", RailwayChunksPerFrame);
            MaxRailwaySegmentsDebug = GetInt(root, "maxRailwaySegmentsDebug", MaxRailwaySegmentsDebug);
            RenderAllRailwaySegments = GetBool(root, "renderAllRailwaySegments", RenderAllRailwaySegments);
        }

        private void WriteRailwaySettings(JObject root)
        {
            if (root == null)
                return;

            root["renderRailways"] = RenderRailways;
            root["railwayOpacity"] = RailwayOpacity;
            root["railwayThickness"] = RailwayThickness;
            root["railwayYOffset"] = RailwayYOffset;

            root["railwayTrainVisible"] = RailwayTrainVisible;
            root["railwayTramVisible"] = RailwayTramVisible;
            root["railwayLightRailVisible"] = RailwayLightRailVisible;
            root["railwaySubwayVisible"] = RailwaySubwayVisible;
            root["railwayServiceVisible"] = RailwayServiceVisible;
            root["railwayTunnelsVisible"] = RailwayTunnelsVisible;

            root["enableRailwaySpatialChunking"] = EnableRailwaySpatialChunking;
            root["railwayChunkSizeMeters"] = RailwayChunkSizeMeters;
            root["railwayChunksPerFrame"] = RailwayChunksPerFrame;
            root["maxRailwaySegmentsDebug"] = MaxRailwaySegmentsDebug;
            root["renderAllRailwaySegments"] = RenderAllRailwaySegments;
        }

        private void ClampRailwaySettings()
        {
            RailwayOpacity = Mathf.Clamp01(RailwayOpacity);
            RailwayThickness = Mathf.Clamp(RailwayThickness, 0.5f, 8f);
            RailwayYOffset = Mathf.Clamp(RailwayYOffset, 0f, 10f);
            RailwayChunkSizeMeters = Mathf.Clamp(RailwayChunkSizeMeters, 128f, 4096f);
            RailwayChunksPerFrame = Mathf.Clamp(RailwayChunksPerFrame, 1, 64);
            MaxRailwaySegmentsDebug = Mathf.Clamp(MaxRailwaySegmentsDebug, 1, 2000000);
        }
    }
}
