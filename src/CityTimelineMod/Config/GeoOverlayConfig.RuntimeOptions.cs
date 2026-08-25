// CTM CONTRACT GUARD: Never disable the 57 km LargeMap + PlayableWorld contract unless explicitly requested.
using System;
using CityTimelineMod.Util;
using Newtonsoft.Json.Linq;

namespace CityTimelineMod.Config
{
    internal sealed partial class GeoOverlayConfig
    {
        internal bool ApplyRuntimeOption(string key, object value)
        {
            if (string.IsNullOrWhiteSpace(key))
                return false;

            if (IsRestartScopedLifecycleOption(key))
                return false;

            try
            {
                var option = new JObject
                {
                    [key] = value == null ? JValue.CreateNull() : JToken.FromObject(value)
                };

                switch (key.Trim().ToLowerInvariant())
                {
                    case "showoverlayhud": ShowOverlayHud = GetBool(option, key, ShowOverlayHud); break;

                    case "renderroads": RenderRoads = GetBool(option, key, RenderRoads); break;
                    case "renderpaths": RenderPaths = GetBool(option, key, RenderPaths); break;
                    case "renderwaterlines": RenderWaterLines = GetBool(option, key, RenderWaterLines); break;
                    case "renderwaterareas": RenderWaterAreas = GetBool(option, key, RenderWaterAreas); break;
                    case "renderwaterareaoutlines": RenderWaterAreaOutlines = GetBool(option, key, RenderWaterAreaOutlines); break;
                    case "renderwaterareafillmeshes": RenderWaterAreaFillMeshes = GetBool(option, key, RenderWaterAreaFillMeshes); break;
                    case "renderzoning": RenderZoning = GetBool(option, key, RenderZoning); break;
                    case "rendermapbounds": RenderMapBounds = GetBool(option, key, RenderMapBounds); break;
                    case "renderworldmapbounds": RenderWorldMapBounds = GetBool(option, key, RenderWorldMapBounds); break;
                    case "renderheightmapbounds": RenderHeightMapBounds = GetBool(option, key, RenderHeightMapBounds); break;
                    case "rendermapcenter": RenderMapCenter = GetBool(option, key, RenderMapCenter); break;
                    case "verboseoverlaylogs": VerboseOverlayLogs = GetBool(option, key, VerboseOverlayLogs); break;

                    case "roadalpha": RoadAlpha = GetFloat(option, key, RoadAlpha); break;
                    case "pathalpha": PathAlpha = GetFloat(option, key, PathAlpha); break;
                    case "waterlinealpha": WaterLineAlpha = GetFloat(option, key, WaterLineAlpha); break;
                    case "waterareaoutlinealpha": WaterAreaOutlineAlpha = GetFloat(option, key, WaterAreaOutlineAlpha); break;
                    case "waterareafillalpha": WaterAreaFillAlpha = GetFloat(option, key, WaterAreaFillAlpha); break;
                    case "zoningalpha": ZoningAlpha = GetFloat(option, key, ZoningAlpha); break;
                    case "mapboundsalpha": MapBoundsAlpha = GetFloat(option, key, MapBoundsAlpha); break;
                    case "worldmapboundsalpha": WorldMapBoundsAlpha = GetFloat(option, key, WorldMapBoundsAlpha); break;
                    case "heightmapboundsalpha": HeightMapBoundsAlpha = GetFloat(option, key, HeightMapBoundsAlpha); break;
                    case "mapcenteralpha": MapCenterAlpha = GetFloat(option, key, MapCenterAlpha); break;

                    case "roadyoffset": RoadYOffset = GetFloat(option, key, RoadYOffset); break;
                    case "pathyoffset": PathYOffset = GetFloat(option, key, PathYOffset); break;
                    case "waterlineyoffset": WaterLineYOffset = GetFloat(option, key, WaterLineYOffset); break;
                    case "waterareaoutlineyoffset": WaterAreaOutlineYOffset = GetFloat(option, key, WaterAreaOutlineYOffset); break;
                    case "waterareafillyoffset": WaterAreaFillYOffset = GetFloat(option, key, WaterAreaFillYOffset); break;
                    case "zoningfillyoffset": ZoningFillYOffset = GetFloat(option, key, ZoningFillYOffset); break;
                    case "mapboundsyoffset": MapBoundsYOffset = GetFloat(option, key, MapBoundsYOffset); break;

                    case "roadgeometrysource": RoadGeometrySource = GetString(option, key, RoadGeometrySource); break;
                    case "roadhighwayfilter":
                        RoadHighwayFilter = GetString(option, key, RoadHighwayFilter);
                        break;
                    case "pathhighwayfilter":
                        PathHighwayFilter = GetString(option, key, PathHighwayFilter);
                        break;
                    case "minimumroaddebugtier": MinimumRoadDebugTier = GetInt(option, key, MinimumRoadDebugTier); break;
                    case "runtimeroadimportenabled": RuntimeRoadImportEnabled = GetBool(option, key, RuntimeRoadImportEnabled); break;
                    case "runtimeroadimportrunonce": RuntimeRoadImportRunOnce = GetBool(option, key, RuntimeRoadImportRunOnce); break;
                    case "runtimeroadimportmaxsegments": RuntimeRoadImportMaxSegments = GetInt(option, key, RuntimeRoadImportMaxSegments); break;
                    case "runtimeroadimportbatchsize": RuntimeRoadImportBatchSize = GetInt(option, key, RuntimeRoadImportBatchSize); break;
                    case "runtimeroadimportselectionmode": RuntimeRoadImportSelectionMode = GetString(option, key, RuntimeRoadImportSelectionMode); break;
                    case "runtimeroadimportdistancebucketmeters": RuntimeRoadImportDistanceBucketMeters = GetFloat(option, key, RuntimeRoadImportDistanceBucketMeters); break;
                    case "runtimeroadimportpriorityweight": RuntimeRoadImportPriorityWeight = GetFloat(option, key, RuntimeRoadImportPriorityWeight); break;
                    case "runtimeroadimportskipparkingaisles": RuntimeRoadImportSkipParkingAisles = GetBool(option, key, RuntimeRoadImportSkipParkingAisles); break;
                    case "runtimeroadimportskipclearlyunpaved": RuntimeRoadImportSkipClearlyUnpaved = GetBool(option, key, RuntimeRoadImportSkipClearlyUnpaved); break;

                    case "roadcolordefault": RoadColorDefault = GetString(option, key, RoadColorDefault); break;
                    case "roadcolormotorway": RoadColorMotorway = GetString(option, key, RoadColorMotorway); break;
                    case "roadcolorprimary": RoadColorPrimary = GetString(option, key, RoadColorPrimary); break;
                    case "roadcolorsecondary": RoadColorSecondary = GetString(option, key, RoadColorSecondary); break;
                    case "roadcolortertiary": RoadColorTertiary = GetString(option, key, RoadColorTertiary); break;
                    case "roadcolorlink": RoadColorLink = GetString(option, key, RoadColorLink); break;
                    case "pathcolor": PathColor = GetString(option, key, PathColor); break;

                    case "waterlinecolor": WaterLineColor = GetString(option, key, WaterLineColor); break;
                    case "waterareaoutlinecolor": WaterAreaOutlineColor = GetString(option, key, WaterAreaOutlineColor); break;
                    case "waterareafillcolor": WaterAreaFillColor = GetString(option, key, WaterAreaFillColor); break;

                    case "zoningresidentiallowcolor": ZoningResidentialLowColor = GetString(option, key, ZoningResidentialLowColor); break;
                    case "zoningresidentialmediumcolor": ZoningResidentialMediumColor = GetString(option, key, ZoningResidentialMediumColor); break;
                    case "zoningresidentialhighcolor": ZoningResidentialHighColor = GetString(option, key, ZoningResidentialHighColor); break;
                    case "zoningcommerciallowcolor": ZoningCommercialLowColor = GetString(option, key, ZoningCommercialLowColor); break;
                    case "zoningcommercialhighcolor": ZoningCommercialHighColor = GetString(option, key, ZoningCommercialHighColor); break;
                    case "zoningretailcolor": ZoningRetailColor = GetString(option, key, ZoningRetailColor); break;
                    case "zoningindustrialcolor": ZoningIndustrialColor = GetString(option, key, ZoningIndustrialColor); break;
                    case "zoningofficecolor": ZoningOfficeColor = GetString(option, key, ZoningOfficeColor); break;
                    case "zoningsurfacecolor": ZoningSurfaceColor = GetString(option, key, ZoningSurfaceColor); break;
                    case "zoningrampcolor": ZoningRampColor = GetString(option, key, ZoningRampColor); break;
                    case "zoningmixedcolor": ZoningMixedColor = GetString(option, key, ZoningMixedColor); break;
                    case "zoningfallbackcolor": ZoningFallbackColor = GetString(option, key, ZoningFallbackColor); break;

                    case "worldmapboundscolor": WorldMapBoundsColor = GetString(option, key, WorldMapBoundsColor); break;
                    case "heightmapboundscolor": HeightMapBoundsColor = GetString(option, key, HeightMapBoundsColor); break;
                    case "mapcentercolor": MapCenterColor = GetString(option, key, MapCenterColor); break;

                    default:
                        return false;
                }

                RoadHighwayFilter = string.IsNullOrWhiteSpace(RoadHighwayFilter)
                    ? "all"
                    : RoadHighwayFilter.Trim().ToLowerInvariant();

                PathHighwayFilter = string.IsNullOrWhiteSpace(PathHighwayFilter)
                    ? "all"
                    : PathHighwayFilter.Trim().ToLowerInvariant();

                ClampRuntimeRoadImportSettings(this);
                ClampRuntimeVisualSettings();
                RoadAlpha = Clamp01(RoadAlpha);
                PathAlpha = Clamp01(PathAlpha);
                WaterLineAlpha = Clamp01(WaterLineAlpha);
                WaterAreaOutlineAlpha = Clamp01(WaterAreaOutlineAlpha);
                WaterAreaFillAlpha = Clamp01(WaterAreaFillAlpha);
                ZoningAlpha = Clamp01(ZoningAlpha);
                MapBoundsAlpha = Clamp01(MapBoundsAlpha);
                WorldMapBoundsAlpha = Clamp01(WorldMapBoundsAlpha);
                HeightMapBoundsAlpha = Clamp01(HeightMapBoundsAlpha);
                MapCenterAlpha = Clamp01(MapCenterAlpha);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("GeoOverlayConfig: failed to apply runtime option '" + key + "'. " + ex);
                return false;
            }
        }

        internal static bool IsRestartScopedLifecycleOption(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return false;

            switch (key.Trim().ToLowerInvariant())
            {
                case "modenabled":
                case "largemapenabled":
                case "playableworldenabled":
                    return true;
                default:
                    return false;
            }
        }
    }
}
