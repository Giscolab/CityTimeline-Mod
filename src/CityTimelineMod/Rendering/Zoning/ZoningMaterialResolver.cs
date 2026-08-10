using UnityEngine;

namespace CityTimelineMod.Rendering.Zoning
{
    internal static class ZoningMaterialResolver
    {
        internal static string ResolveMaterialKey(string zone, string cs2)
        {
            var label = string.IsNullOrWhiteSpace(cs2) ? "" : cs2.ToLowerInvariant();

            if (label.Contains("résidentiel") || label.Contains("residentiel"))
            {
                if (label.Contains("haute"))
                    return "residential_high";

                if (label.Contains("moyenne"))
                    return "residential_medium";

                return "residential_low";
            }

            if (label.Contains("commercial haute"))
                return "commercial_high";

            if (label.Contains("commercial basse"))
                return "commercial_low";

            if (label.Contains("commerce de détail") || label.Contains("commerce de detail"))
                return "retail_detail";

            if (label.Contains("mixte"))
                return "mixed";

            if (label.Contains("industrie"))
                return "industrial";

            if (label.Contains("bureau") || label.Contains("administration"))
                return "office";

            if (label.Contains("parking"))
            {
                if (label.Contains("ouvrage"))
                    return "parking_ramp";

                return "parking_surface";
            }

            var value = string.IsNullOrWhiteSpace(zone) ? "" : zone.ToLowerInvariant();

            switch (value)
            {
                case "low":
                    return "residential_low";

                case "medium":
                    return "residential_medium";

                case "high":
                    return "residential_high";

                case "industrial":
                    return "industrial";

                case "retail":
                    return "retail_detail";

                case "office":
                    return "office";

                case "surface":
                    return "parking_surface";

                case "ramp":
                    return "parking_ramp";

                case "mixed":
                    return "mixed";

                default:
                    return "fallback";
            }
        }

        internal static Material ResolveMaterial(
            string zone,
            string cs2,
            Material residentialLowMaterial,
            Material residentialMediumMaterial,
            Material residentialHighMaterial,
            Material commercialLowMaterial,
            Material commercialHighMaterial,
            Material retailDetailMaterial,
            Material industrialMaterial,
            Material officeMaterial,
            Material surfaceMaterial,
            Material rampMaterial,
            Material mixedMaterial,
            Material fallbackMaterial
        )
        {
            var label = string.IsNullOrWhiteSpace(cs2) ? "" : cs2.ToLowerInvariant();

            if (label.Contains("résidentiel") || label.Contains("residentiel"))
            {
                if (label.Contains("haute"))
                    return residentialHighMaterial;

                if (label.Contains("moyenne"))
                    return residentialMediumMaterial;

                return residentialLowMaterial;
            }

            if (label.Contains("commercial haute"))
                return commercialHighMaterial;

            if (label.Contains("commercial basse"))
                return commercialLowMaterial;

            if (label.Contains("commerce de détail") || label.Contains("commerce de detail"))
                return retailDetailMaterial;

            if (label.Contains("mixte"))
                return mixedMaterial;

            if (label.Contains("industrie"))
                return industrialMaterial;

            if (label.Contains("bureau") || label.Contains("administration"))
                return officeMaterial;

            if (label.Contains("parking"))
            {
                if (label.Contains("ouvrage"))
                    return rampMaterial;

                return surfaceMaterial;
            }

            var value = string.IsNullOrWhiteSpace(zone) ? "" : zone.ToLowerInvariant();

            switch (value)
            {
                case "low":
                    return residentialLowMaterial;

                case "medium":
                    return residentialMediumMaterial;

                case "high":
                    return residentialHighMaterial;

                case "industrial":
                    return industrialMaterial;

                case "retail":
                    return retailDetailMaterial;

                case "office":
                    return officeMaterial;

                case "surface":
                    return surfaceMaterial;

                case "ramp":
                    return rampMaterial;

                case "mixed":
                    return mixedMaterial;

                default:
                    return fallbackMaterial;
            }
        }
    }
}
