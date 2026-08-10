using CityTimelineMod.Config;
using CityTimelineMod.Importers;
using UnityEngine;

namespace CityTimelineMod.Rendering.Roads
{
    internal static class RoadRenderStyleResolver
    {
        internal static string ResolveRoadRenderBatchKey(GeoRoadLine roadLine, GeoOverlayConfig config)
        {
            if (roadLine == null)
                return "fallback";

            if (roadLine.IsPath)
                return "path";

            if (config.HighlightRoundabouts && roadLine.Roundabout)
                return "roundabout";

            if (config.HighlightBridges && roadLine.Bridge)
                return "bridge";

            if (config.HighlightTunnels && roadLine.Tunnel)
                return "tunnel";

            if (config.HighlightOneWayRoads && RoadRenderRules.IsOnewayRoad(roadLine))
                return "oneway";

            return RoadRenderRules.ResolveRoadBatchKey(roadLine.Highway);
        }

        internal static Material ResolveRoadRenderBatchMaterial(
            GeoRoadLine roadLine,
            GeoOverlayConfig config,
            Material roadMotorwayMaterial,
            Material roadPrimaryMaterial,
            Material roadSecondaryMaterial,
            Material roadTertiaryMaterial,
            Material roadLinkMaterial,
            Material fallbackRoadMaterial,
            Material roadOneWayMaterial,
            Material roadBridgeMaterial,
            Material roadTunnelMaterial,
            Material roadRoundaboutMaterial
        )
        {
            var key = ResolveRoadRenderBatchKey(roadLine, config);

            switch (key)
            {
                case "roundabout":
                    return roadRoundaboutMaterial;

                case "bridge":
                    return roadBridgeMaterial;

                case "tunnel":
                    return roadTunnelMaterial;

                case "oneway":
                    return roadOneWayMaterial;

                default:
                    return RoadRenderRules.ResolveRoadBatchMaterial(
                        roadLine != null ? roadLine.Highway : null,
                        roadMotorwayMaterial,
                        roadPrimaryMaterial,
                        roadSecondaryMaterial,
                        roadTertiaryMaterial,
                        roadLinkMaterial,
                        fallbackRoadMaterial
                    );
            }
        }

        internal static float ResolveRoadSegmentWidth(GeoRoadLine roadLine, bool isPath, GeoOverlayConfig config)
        {
            var baseWidth = isPath ? config.PathSegmentWidth : config.RoadSegmentWidth;

            if (isPath || !config.UseLaneWidthScaling || roadLine == null)
                return baseWidth;

            var laneCount = roadLine.TargetLaneCount.HasValue
                ? roadLine.TargetLaneCount.Value
                : (roadLine.Lanes.HasValue ? roadLine.Lanes.Value : 1);

            if (laneCount <= 1)
                return baseWidth;

            var multiplier = 1f + (laneCount - 1) * Mathf.Clamp(config.LaneWidthScaleFactor, 0f, 2f);
            multiplier = Mathf.Clamp(multiplier, 1f, 4f);

            return baseWidth * multiplier;
        }
    }
}
