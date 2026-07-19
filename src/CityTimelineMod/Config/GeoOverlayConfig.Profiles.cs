using CityTimelineMod.Util;

namespace CityTimelineMod.Config
{
    internal sealed partial class GeoOverlayConfig
    {
        private static void ApplyOverlayProfile(GeoOverlayConfig config)
        {
            if (config == null)
                return;

            var profile = string.IsNullOrWhiteSpace(config.OverlayProfile)
                ? "custom"
                : config.OverlayProfile.Trim().ToLowerInvariant();

            if (profile == "custom")
                return;

            if (profile == "major")
            {
                config.RenderWaterLines = false;
                config.RenderWaterAreas = false;
                config.RenderRoads = true;
                config.RoadGeometrySource = "major";
                config.RenderPaths = false;
                config.RenderRailways = false;
                config.RenderZoning = false;
                return;
            }

            if (profile == "driveable")
            {
                config.RenderWaterLines = false;
                config.RenderWaterAreas = false;
                config.RenderRoads = true;
                config.RoadGeometrySource = "driveable";
                config.RenderPaths = true;
                config.RenderRailways = false;
                config.RenderZoning = false;
                return;
            }

            if (profile == "zoning")
            {
                config.RenderWaterLines = false;
                config.RenderWaterAreas = false;
                config.RenderRoads = false;
                config.RenderPaths = false;
                config.RenderRailways = false;
                config.RenderZoning = true;
                return;
            }

            if (profile == "water")
            {
                config.RenderWaterLines = true;
                config.RenderWaterAreas = true;
                config.RenderRoads = false;
                config.RenderPaths = false;
                config.RenderRailways = false;
                config.RenderZoning = false;
                return;
            }

            if (profile == "full")
            {
                config.RenderWaterLines = true;
                config.RenderWaterAreas = true;
                config.RenderRoads = true;
                config.RoadGeometrySource = "driveable";
                config.RenderPaths = true;
                config.RenderZoning = true;
                config.RenderRailways = true;
                return;
            }

            if (profile == "off")
            {
                config.RenderWaterLines = false;
                config.RenderWaterAreas = false;
                config.RenderRoads = false;
                config.RenderPaths = false;
                config.RenderZoning = false;
                config.RenderRailways = false;
                return;
            }

            Log.Error("GeoOverlayConfig: invalid overlayProfile=" + profile + ". Fallback to custom.");
            config.OverlayProfile = "custom";
        }
    }
}
