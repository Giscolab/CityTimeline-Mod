using System;
using CityTimelineMod.Importers;
using UnityEngine;

namespace CityTimelineMod.Rendering.Roads
{
    internal static class RoadRenderRules
    {
        internal static string ResolveRoadBatchKey(string highway)
        {
            var value = string.IsNullOrWhiteSpace(highway) ? "" : highway.ToLowerInvariant();

            if (value.EndsWith("_link"))
                return "link";

            switch (value)
            {
                case "motorway":
                case "trunk":
                    return "motorway";

                case "primary":
                    return "primary";

                case "secondary":
                    return "secondary";

                case "tertiary":
                    return "tertiary";

                default:
                    return "fallback";
            }
        }

        internal static Material ResolveRoadBatchMaterial(
            string highway,
            Material roadMotorwayMaterial,
            Material roadPrimaryMaterial,
            Material roadSecondaryMaterial,
            Material roadTertiaryMaterial,
            Material roadLinkMaterial,
            Material fallbackRoadMaterial
        )
        {
            var key = ResolveRoadBatchKey(highway);

            switch (key)
            {
                case "motorway":
                    return roadMotorwayMaterial;

                case "primary":
                    return roadPrimaryMaterial;

                case "secondary":
                    return roadSecondaryMaterial;

                case "tertiary":
                    return roadTertiaryMaterial;

                case "link":
                    return roadLinkMaterial;

                default:
                    return fallbackRoadMaterial;
            }
        }

        internal static bool ShouldRenderRoadByTier(string highway, int minimumTier)
        {
            if (minimumTier <= 0)
                return true;

            return GetRoadDebugTier(highway) >= minimumTier;
        }

        internal static bool ShouldRenderHighwayByFilter(string highway, string filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
                return true;

            var normalizedFilter = filter.Trim().ToLowerInvariant();

            if (normalizedFilter == "all" || normalizedFilter == "*")
                return true;

            var highwayKey = NormalizeHighwayFilterKey(highway);
            var parts = normalizedFilter.Split(',');

            for (var i = 0; i < parts.Length; i++)
            {
                var wanted = NormalizeHighwayFilterKey(parts[i]);

                if (wanted == "all" || wanted == "*" || wanted == highwayKey)
                    return true;
            }

            return false;
        }

        private static string NormalizeHighwayFilterKey(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "(unknown)"
                : value.Trim().ToLowerInvariant();
        }

        internal static int GetRoadDebugTier(string highway)
        {
            if (string.IsNullOrWhiteSpace(highway))
                return 0;

            var value = highway.ToLowerInvariant();

            switch (value)
            {
                case "motorway":
                case "trunk":
                case "motorway_link":
                case "trunk_link":
                    return 4;

                case "primary":
                case "primary_link":
                    return 3;

                case "secondary":
                case "secondary_link":
                    return 2;

                case "tertiary":
                case "tertiary_link":
                    return 1;

                default:
                    return 0;
            }
        }

        internal static bool IsOnewayTrue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            return
                string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "1", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsOnewayReverse(string value)
        {
            return string.Equals(value, "-1", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsOnewayRoad(GeoRoadLine road)
        {
            return road != null && (IsOnewayTrue(road.Oneway) || IsOnewayReverse(road.Oneway));
        }
    }
}
