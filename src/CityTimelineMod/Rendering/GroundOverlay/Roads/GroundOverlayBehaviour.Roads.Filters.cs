using System;
using System.Collections.Generic;
using CityTimelineMod.Importers;
using CityTimelineMod.Rendering.Batching;
using CityTimelineMod.Rendering.Core;
using CityTimelineMod.Rendering.Materials;
using CityTimelineMod.Rendering.Roads;
using CityTimelineMod.Util;
using UnityEngine;

namespace CityTimelineMod.Rendering
{
    internal sealed partial class GroundOverlayBehaviour
    {
        private bool ShouldRenderRoadBySemanticFilter(GeoRoadLine road)
        {
            if (road == null)
                return false;

            var mode = _config == null || string.IsNullOrWhiteSpace(_config.RoadSemanticFilterMode)
                ? "all"
                : _config.RoadSemanticFilterMode.Trim().ToLowerInvariant();

            switch (mode)
            {
                case "all":
                    return true;

                case "major":
                    return !road.IsPath && RoadRenderRules.GetRoadDebugTier(road.Highway) >= 2;

                case "local":
                    return !road.IsPath && RoadRenderRules.GetRoadDebugTier(road.Highway) < 2;

                case "oneway":
                    return RoadRenderRules.IsOnewayRoad(road);

                case "bridges":
                    return road.Bridge;

                case "tunnels":
                    return road.Tunnel;

                case "roundabouts":
                    return road.Roundabout;

                default:
                    return true;
            }
        }

        private bool ShouldRenderRoadLineForOverlay(GeoRoadLine roadLine)
        {
            if (roadLine == null || roadLine.Points == null || roadLine.Points.Count < 2)
                return false;

            var isPath = roadLine.IsPath;

            if (isPath)
            {
                if (!_config.RenderPaths)
                    return false;
            }
            else if (!_config.RenderRoads)
            {
                return false;
            }

            if (!ShouldRenderOverlaySublayer(ResolveRoadOverlaySublayerKey(roadLine)))
                return false;

            if (!isPath && !RoadRenderRules.ShouldRenderRoadByTier(roadLine.Highway, _config.MinimumRoadDebugTier))
                return false;

            var highwayFilter = isPath ? _config.PathHighwayFilter : _config.RoadHighwayFilter;

            return RoadRenderRules.ShouldRenderHighwayByFilter(roadLine.Highway, highwayFilter) &&
                   ShouldRenderRoadBySemanticFilter(roadLine);
        }
    }
}
