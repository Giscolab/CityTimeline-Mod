using CityTimelineMod.Terrain;
using CityTimelineMod.Util;
using UnityEngine;

namespace CityTimelineMod.Rendering
{
    internal sealed partial class GroundOverlayBehaviour
    {
        private static bool RequiresOverlayRebuildForConfigKey(string configKey)
        {
            if (string.IsNullOrWhiteSpace(configKey))
                return true;

            switch (configKey)
            {
                case "roadAlpha":
                case "pathAlpha":
                case "waterLineAlpha":
                case "waterAreaOutlineAlpha":
                case "waterAreaFillAlpha":
                case "zoningAlpha":
                case "mapBoundsAlpha":
                case "renderRailways":
                case "railwayOpacity":
                case "verboseOverlayLogs":
                case "showOverlayHud":
                case "modEnabled":
                    return false;

                default:
                    return true;
            }
        }

        private bool IsRenderEverythingEnabled()
        {
            return _config != null && _config.RenderEverything;
        }

        private bool ShouldRenderAllRoadSegments()
        {
            return IsRenderEverythingEnabled() || (_config != null && _config.RenderAllRoadSegments);
        }

        private bool ShouldRenderAllPathSegments()
        {
            return IsRenderEverythingEnabled() || (_config != null && _config.RenderAllPathSegments);
        }

        private bool ShouldRenderAllZoningPolygons()
        {
            return IsRenderEverythingEnabled() || (_config != null && _config.RenderAllZoningPolygons);
        }

        private bool ShouldRenderAllWaterSegments()
        {
            return IsRenderEverythingEnabled() || (_config != null && _config.RenderAllWaterSegments);
        }

        private bool ShouldRenderAllWaterAreaFills()
        {
            return IsRenderEverythingEnabled() || (_config != null && _config.RenderAllWaterAreaFills);
        }

        private static string ResolveRoadRenderMode(string value)
        {
            return "ribbon";
        }

        private void SyncVisibilityStateFromConfig()
        {
            _zoningVisible = _config.RenderZoning;
            _roadsVisible = _config.RenderRoads || _config.RenderPaths;
            _waterVisible = _config.RenderWaterLines || _config.RenderWaterAreas;
            SyncModernLayerStateFromConfig();
        }

private void HandleOverlayVisibilityInput()
{
    // Alt+H is owned by CityTimelineUISystem.
}

        private static bool IsSaveOverlayVisualsModifierDown()
        {
            return Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        }

        private static float ResolveY(Vector3 world)
        {
            float y;
            if (TerrainHeightSampler.TrySampleHeight(new Vector3(world.x, 0f, world.z), out y))
                return y;

            return 0f;
        }
    }
}
