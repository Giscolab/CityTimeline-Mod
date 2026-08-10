using CityTimelineMod.Config;
using CityTimelineMod.Rendering.Materials;
using UnityEngine;

namespace CityTimelineMod.Rendering.Railways
{
    internal static class RailwayRenderRules
    {
        internal static string CategoryKey(RailwayCategory category)
        {
            switch (category)
            {
                case RailwayCategory.Tram: return "tram";
                case RailwayCategory.LightRail: return "light_rail";
                case RailwayCategory.Subway: return "subway";
                case RailwayCategory.Service: return "service";
                default: return "train";
            }
        }

        internal static bool IsCategoryVisible(RailwayCategory category, GeoOverlayConfig config)
        {
            if (config == null)
                return false;

            switch (category)
            {
                case RailwayCategory.Tram: return config.RailwayTramVisible;
                case RailwayCategory.LightRail: return config.RailwayLightRailVisible;
                case RailwayCategory.Subway: return config.RailwaySubwayVisible;
                case RailwayCategory.Service: return config.RailwayServiceVisible;
                default: return config.RailwayTrainVisible;
            }
        }

        internal static float WidthScale(RailwayCategory category)
        {
            switch (category)
            {
                case RailwayCategory.Tram: return 0.82f;
                case RailwayCategory.LightRail: return 0.92f;
                case RailwayCategory.Subway: return 0.92f;
                case RailwayCategory.Service: return 0.62f;
                default: return 1f;
            }
        }

        internal static Material ResolveMaterial(GeoRailwayLine line, OverlayRenderMaterials materials)
        {
            if (line == null || materials == null)
                return null;

            switch (line.Category)
            {
                case RailwayCategory.Tram:
                    return line.Tunnel ? materials.RailwayTramTunnel : materials.RailwayTram;
                case RailwayCategory.LightRail:
                    return line.Tunnel ? materials.RailwayLightRailTunnel : materials.RailwayLightRail;
                case RailwayCategory.Subway:
                    return line.Tunnel ? materials.RailwaySubwayTunnel : materials.RailwaySubway;
                case RailwayCategory.Service:
                    return line.Tunnel ? materials.RailwayServiceTunnel : materials.RailwayService;
                default:
                    return line.Tunnel ? materials.RailwayTrainTunnel : materials.RailwayTrain;
            }
        }
    }
}
