using System;
using CityTimelineMod.Rendering.Statistics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CityTimelineMod.Rendering
{
    internal sealed partial class GroundOverlayBehaviour
    {
        private static readonly string[] OverlaySublayerKeys =
        {
            "zoning.residential_high",
            "zoning.residential_medium",
            "zoning.residential_low",
            "zoning.commercial_high",
            "zoning.commercial_low",
            "zoning.retail",
            "zoning.industrial",
            "zoning.parking_structure",
            "zoning.parking_surface",
            "zoning.office",
            "zoning.mixed",
            "roads.highway",
            "roads.large_road",
            "roads.medium_road",
            "roads.small_road",
            "roads.ramp",
            "roads.gravel_road",
            "roads.pathway",
            "water.lines",
            "water.areas"
        };

        internal OverlayLayerHudSnapshot GetOverlayLayerHudSnapshot()
        {
            if (_config == null)
                return OverlayLayerHudSnapshot.Unavailable();

            return new OverlayLayerHudSnapshot
            {
                Available = true,
                ZoningVisible = _config.RenderZoning,
                RoadsVisible = _config.RenderRoads,
                PathsVisible = _config.RenderPaths,
                WaterVisible = _config.RenderWaterLines || _config.RenderWaterAreas,
                BoundsVisible = _config.RenderMapBounds,
                ZoningOpacity = Mathf.Clamp01(_config.ZoningAlpha),
                RoadsOpacity = Mathf.Clamp01(_config.RoadAlpha),
                PathsOpacity = Mathf.Clamp01(_config.PathAlpha),
                WaterOpacity = Mathf.Clamp01(_config.WaterLineAlpha),
                BoundsOpacity = Mathf.Clamp01(_config.MapBoundsAlpha)
            };
        }

        internal bool SetOverlayLayerBoolean(string key, bool value)
        {
            if (_config == null || string.IsNullOrWhiteSpace(key))
                return false;

            var normalized = key.Trim().ToLowerInvariant();
            switch (normalized)
            {
                case "zoning":
                    _config.RenderZoning = value;
                    break;
                case "roads":
                    _config.RenderRoads = value;
                    break;
                case "paths":
                    _config.RenderPaths = value;
                    break;
                case "water":
                    _config.RenderWaterLines = value;
                    _config.RenderWaterAreas = value;
                    break;
                case "bounds":
                    _config.RenderMapBounds = value;
                    break;
                default:
                    return false;
            }

            SyncVisibilityStateFromConfig();
            InvalidateBundleHudSnapshotJson();
            ApplyCurrentOverlayVisibilityToMaterials();
            ScheduleRailwaySettingsSave();

            // Hidden layers may not have produced meshes during the initial build.
            // Enabling one performs one deliberate progressive rebuild; disabling
            // remains an immediate material-only operation.
            if (value)
                RequestOverlayRebuild("CoHTML layer enabled: " + normalized, true);

            return true;
        }

        internal bool SetOverlayLayerFloat(string key, float value)
        {
            if (_config == null || string.IsNullOrWhiteSpace(key))
                return false;

            var normalized = key.Trim().ToLowerInvariant();
            var opacity = Mathf.Clamp01(value);
            switch (normalized)
            {
                case "zoning":
                    _config.ZoningAlpha = opacity;
                    _config.ZoningResidentialAlpha = opacity;
                    _config.ZoningCommercialAlpha = opacity;
                    _config.ZoningIndustrialAlpha = opacity;
                    _config.ZoningOfficeAlpha = opacity;
                    _config.ParkingAlpha = opacity;
                    break;
                case "roads":
                    _config.RoadAlpha = opacity;
                    break;
                case "paths":
                    _config.PathAlpha = opacity;
                    break;
                case "water":
                    _config.WaterLineAlpha = opacity;
                    _config.WaterAreaOutlineAlpha = opacity;
                    _config.WaterAreaFillAlpha = opacity;
                    break;
                case "bounds":
                    _config.MapBoundsAlpha = opacity;
                    _config.WorldMapBoundsAlpha = opacity;
                    _config.HeightMapBoundsAlpha = opacity;
                    _config.MapCenterAlpha = opacity;
                    break;
                default:
                    return false;
            }

            SyncModernLayerStateFromConfig();
            InvalidateBundleHudSnapshotJson();
            ApplyCurrentOverlayVisibilityToMaterials();
            ScheduleRailwaySettingsSave();
            return true;
        }

        internal string GetOverlaySublayersJson()
        {
            var layers = new JObject();
            for (var i = 0; i < OverlaySublayerKeys.Length; i++)
            {
                var key = OverlaySublayerKeys[i];
                layers[key] = GetOverlaySublayerVisible(key);
            }

            return new JObject
            {
                ["available"] = _config != null,
                ["layers"] = layers
            }.ToString(Formatting.None);
        }

        internal bool SetOverlaySublayerVisible(string key, bool value)
        {
            var normalized = NormalizeOverlaySublayerKey(key);
            if (_config == null || Array.IndexOf(OverlaySublayerKeys, normalized) < 0)
                return false;

            if (normalized == "water.lines")
            {
                _config.RenderWaterLines = value;
                SyncVisibilityStateFromConfig();
            }
            else if (normalized == "water.areas")
            {
                _config.RenderWaterAreas = value;
                SyncVisibilityStateFromConfig();
            }
            else
            {
                _config.SetOverlaySublayerVisible(normalized, value);
            }

            InvalidateBundleHudSnapshotJson();
            ApplyCurrentOverlayVisibilityToMaterials();
            ScheduleRailwaySettingsSave();

            // Zoning/water categories have dedicated materials. Road semantic
            // styles (bridge, tunnel, one-way...) deliberately share materials
            // across road classes, so hiding one road class also needs one
            // deliberate filtered rebuild for exact results.
            if (value || normalized.StartsWith("roads.", StringComparison.Ordinal))
                RequestOverlayRebuild("CoHTML sublayer filter: " + normalized, true);
            return true;
        }

        private bool GetOverlaySublayerVisible(string key)
        {
            var normalized = NormalizeOverlaySublayerKey(key);
            if (_config != null)
            {
                if (normalized == "water.lines")
                    return _config.RenderWaterLines;
                if (normalized == "water.areas")
                    return _config.RenderWaterAreas;
            }

            return _config == null || _config.GetOverlaySublayerVisible(normalized);
        }

        private bool ShouldRenderOverlaySublayer(string key)
        {
            return GetOverlaySublayerVisible(key);
        }

        private static string ResolveZoningOverlaySublayerKey(string materialKey)
        {
            switch ((materialKey ?? "").Trim().ToLowerInvariant())
            {
                case "retail_detail": return "zoning.retail";
                case "parking_ramp": return "zoning.parking_structure";
                case "parking_surface": return "zoning.parking_surface";
                case "residential_high": return "zoning.residential_high";
                case "residential_medium": return "zoning.residential_medium";
                case "residential_low": return "zoning.residential_low";
                case "commercial_high": return "zoning.commercial_high";
                case "commercial_low": return "zoning.commercial_low";
                case "industrial": return "zoning.industrial";
                case "office": return "zoning.office";
                case "mixed": return "zoning.mixed";
                default: return "";
            }
        }

        private static string ResolveRoadOverlaySublayerKey(Importers.GeoRoadLine road)
        {
            return "roads." + ResolveRoadStatisticsCategory(road);
        }

        private static string NormalizeOverlaySublayerKey(string key)
        {
            return string.IsNullOrWhiteSpace(key) ? "" : key.Trim().ToLowerInvariant();
        }
    }
}
