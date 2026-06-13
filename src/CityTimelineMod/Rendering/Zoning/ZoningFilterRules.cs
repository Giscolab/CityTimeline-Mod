using System;

namespace CityTimelineMod.Rendering.Zoning
{
    internal static class ZoningFilterRules
    {
        internal static bool ShouldRenderByFilter(
            string zone,
            string cs2,
            string materialKey,
            string filterZone,
            string filterCs2Contains,
            string filterMaterialKey)
        {
            if (!MatchesZoningExactFilter(zone, filterZone))
                return false;

            if (!MatchesZoningContainsFilter(cs2, filterCs2Contains))
                return false;

            if (!MatchesZoningExactFilter(materialKey, filterMaterialKey))
                return false;

            return true;
        }

        private static bool MatchesZoningExactFilter(string value, string filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
                return true;

            var normalizedFilter = filter.Trim().ToLowerInvariant();

            if (normalizedFilter == "all" || normalizedFilter == "*")
                return true;

            var normalizedValue = string.IsNullOrWhiteSpace(value)
                ? "(unknown)"
                : value.Trim().ToLowerInvariant();

            var parts = normalizedFilter.Split(',');

            for (var i = 0; i < parts.Length; i++)
            {
                var wanted = string.IsNullOrWhiteSpace(parts[i])
                    ? "(unknown)"
                    : parts[i].Trim().ToLowerInvariant();

                if (wanted == "all" || wanted == "*" || wanted == normalizedValue)
                    return true;
            }

            return false;
        }

        private static bool MatchesZoningContainsFilter(string value, string filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
                return true;

            var normalizedFilter = filter.Trim().ToLowerInvariant();

            if (normalizedFilter == "all" || normalizedFilter == "*")
                return true;

            var normalizedValue = string.IsNullOrWhiteSpace(value)
                ? ""
                : value.Trim().ToLowerInvariant();

            var parts = normalizedFilter.Split(',');

            for (var i = 0; i < parts.Length; i++)
            {
                var wanted = string.IsNullOrWhiteSpace(parts[i])
                    ? ""
                    : parts[i].Trim().ToLowerInvariant();

                if (wanted == "all" || wanted == "*")
                    return true;

                if (wanted.Length > 0 && normalizedValue.IndexOf(wanted, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }
    }
}
