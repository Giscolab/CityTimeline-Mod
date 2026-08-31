using System;
using System.Globalization;
using System.IO;
using CityTimelineMod.Util;
using Newtonsoft.Json.Linq;

namespace CityTimelineMod.Config
{
    internal sealed partial class GeoOverlayConfig
    {
        internal static bool ApplyBundleManifest(GeoOverlayConfig config, string modDir)
        {
            try
            {
                var manifestPath = ResolvePath(config.BundleManifestPath, modDir);
                config.BundleManifestPath = manifestPath;

                if (!File.Exists(manifestPath))
                {
                    Log.Error("GeoOverlayConfig: bundle manifest not found: " + manifestPath);
                    return false;
                }

                var manifestJson = File.ReadAllText(manifestPath);
                var manifest = JObject.Parse(manifestJson);
                var bundleRoot = Path.GetDirectoryName(manifestPath);

                var timeline = manifest["timelineMod"] as JObject;
                if (timeline != null)
                {
                    config.UseGeoJsonCenter = GetBool(timeline, "useGeoJsonCenter", config.UseGeoJsonCenter);
                    config.OriginLon = GetDouble(timeline, "originLon", config.OriginLon);
                    config.OriginLat = GetDouble(timeline, "originLat", config.OriginLat);
                    config.WorldOriginX = GetFloat(timeline, "worldOriginX", config.WorldOriginX);
                    config.WorldOriginZ = GetFloat(timeline, "worldOriginZ", config.WorldOriginZ);
                    config.WorldScale = GetFloat(timeline, "worldScale", config.WorldScale);
                    config.OverlayRotationDegrees = GetFloat(timeline, "overlayRotationDegrees", config.OverlayRotationDegrees);
                    config.OverlayScaleX = GetFloat(timeline, "overlayScaleX", config.OverlayScaleX);
                    config.OverlayScaleZ = GetFloat(timeline, "overlayScaleZ", config.OverlayScaleZ);
                    config.FlipX = GetBool(timeline, "flipX", config.FlipX);
                    config.FlipZ = GetBool(timeline, "flipZ", config.FlipZ);
                }

                var worldMap = manifest["worldMap"] as JObject;
                if (worldMap != null)
                {
                    config.WorldMapSizeKm = GetFloat(worldMap, "sizeKm", config.WorldMapSizeKm);
                    ApplyBbox(worldMap, true, config);
                }

                var heightmap = manifest["heightmap"] as JObject;

                var water = manifest["water"] as JObject;
                if (water != null)
                {
                    config.RecommendedCs2WaterLevel = GetDouble(
                        water,
                        "recommendedCs2WaterLevel",
                        config.RecommendedCs2WaterLevel
                    );

                    config.WaterReferenceElevationMeters = GetDouble(
                        water,
                        "waterReferenceElevationMeters",
                        config.WaterReferenceElevationMeters
                    );

                    config.BelowSeaReserveMeters = GetDouble(
                        water,
                        "belowSeaReserveMeters",
                        config.BelowSeaReserveMeters
                    );

                    config.WaterContractSource = GetString(
                        water,
                        "source",
                        config.WaterContractSource
                    );

                    config.WaterContractFormula = GetString(
                        water,
                        "formula",
                        config.WaterContractFormula
                    );
                }
                if (heightmap != null)
                {
                    config.HeightMapSizeKm = GetFloat(heightmap, "sizeKm", config.HeightMapSizeKm);
                    ApplyBbox(heightmap, false, config);
                }

                var derivedGeojsonPack = string.IsNullOrWhiteSpace(bundleRoot)
                    ? null
                    : Path.Combine(bundleRoot, "geojson_pack");

                if (!string.IsNullOrWhiteSpace(derivedGeojsonPack) && Directory.Exists(derivedGeojsonPack))
                {
                    config.PackPath = derivedGeojsonPack;
                }
                else
                {
                    var manifestGeojsonDir = GetManifestString(manifest, "paths.geojsonDir");

                    if (string.IsNullOrWhiteSpace(manifestGeojsonDir))
                        manifestGeojsonDir = GetManifestString(manifest, "exportBundle.geojsonDir");

                    if (!string.IsNullOrWhiteSpace(manifestGeojsonDir))
                    {
                        var candidate = ResolvePath(manifestGeojsonDir, bundleRoot);

                        if (Directory.Exists(candidate))
                            config.PackPath = candidate;
                        else
                            Log.Error("GeoOverlayConfig: manifest geojsonDir not found: " + candidate);
                    }
                }

                Log.Info("GeoOverlayConfig: bundle manifest loaded " + manifestPath);

                if (!double.IsNaN(config.RecommendedCs2WaterLevel))
                {
                    Log.Info(
                        "GeoOverlayConfig: water recommendedCs2WaterLevel=" +
                        config.RecommendedCs2WaterLevel +
                        ", waterReferenceElevationMeters=" + config.WaterReferenceElevationMeters +
                        ", belowSeaReserveMeters=" + config.BelowSeaReserveMeters
                    );
                }
                Log.Info(
                    "GeoOverlayConfig: manifest applied originLon=" + config.OriginLon +
                    ", originLat=" + config.OriginLat +
                    ", worldScale=" + config.WorldScale +
                    ", packPath=" + (string.IsNullOrWhiteSpace(config.PackPath) ? "(empty)" : config.PackPath)
                );
                return !string.IsNullOrWhiteSpace(config.PackPath);
            }
            catch (Exception ex)
            {
                Log.Error("GeoOverlayConfig: failed to apply bundle manifest. " + ex);
                return false;
            }
        }

        private static void ApplyBbox(JObject section, bool isWorldMap, GeoOverlayConfig config)
        {
            var bbox = GetString(section, "bbox", null);
            if (string.IsNullOrWhiteSpace(bbox))
                return;

            var parts = bbox.Split(',');
            if (parts.Length != 4)
            {
                Log.Error("GeoOverlayConfig: invalid bbox in manifest: " + bbox);
                return;
            }

            double south;
            double west;
            double north;
            double east;

            if (
                !double.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out south) ||
                !double.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out west) ||
                !double.TryParse(parts[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out north) ||
                !double.TryParse(parts[3].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out east)
            )
            {
                Log.Error("GeoOverlayConfig: failed to parse bbox in manifest: " + bbox);
                return;
            }

            if (isWorldMap)
            {
                config.WorldMapMinLat = south;
                config.WorldMapMinLon = west;
                config.WorldMapMaxLat = north;
                config.WorldMapMaxLon = east;
            }
            else
            {
                config.HeightMapMinLat = south;
                config.HeightMapMinLon = west;
                config.HeightMapMaxLat = north;
                config.HeightMapMaxLon = east;
            }
        }

        private static string GetManifestString(JObject root, string path)
        {
            if (root == null || string.IsNullOrWhiteSpace(path))
                return null;

            var token = root.SelectToken(path);
            if (token == null)
                return null;

            var value = token.ToString();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
    }
}

