using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using CityTimelineMod.Config;
using CityTimelineMod.Geometry;
using CityTimelineMod.Importers;
using CityTimelineMod.Rendering;
using CityTimelineMod.Util;

namespace CityTimelineMod
{
    internal static class GeoBundleBootstrap
    {
        private static bool _ran = false;

        internal static void RunOnce()
        {
            if (_ran)
                return;

            _ran = true;
            LoadAndInstallActiveBundle();
        }

        internal static void ReloadActiveBundle(string activeBundleId)
        {
            try
            {
                var modDir = ResolveModDirectory();
                EnsureBundledConfig(modDir);

                if (!UpdateActiveBundleIdInConfig(modDir, activeBundleId))
                    return;

                Log.Info("GeoBundleBootstrap: reload requested activeBundleId=" + activeBundleId);
                LoadAndInstallActiveBundle();
            }
            catch (Exception ex)
            {
                Log.Error("GeoBundleBootstrap: reload failed. " + ex);
            }
        }

        private static void LoadAndInstallActiveBundle()
        {
            try
            {
                var modDir = ResolveModDirectory();
                EnsureBundledConfig(modDir);

                var config = GeoOverlayConfig.Load(modDir);

                if (!config.ModEnabled)
                {
                    Log.Info("CityTimelineMod disabled by config: modEnabled=false. Overlay bootstrap skipped.");
                    return;
                }

                var legacyGeojsonRoot = Path.Combine(modDir, "data", "legacy-geojson");
                EnsureBundledLegacyGeojson(legacyGeojsonRoot);

                var geojsonRoot = legacyGeojsonRoot;

                if (!string.IsNullOrWhiteSpace(config.PackPath))
                {
                    var candidateGeojsonRoot = Path.Combine(config.PackPath, "geojson");

                    if (Directory.Exists(candidateGeojsonRoot))
                    {
                        geojsonRoot = candidateGeojsonRoot;
                    }
                    else
                    {
                        Log.Error("GeoBundleBootstrap: packPath geojson folder not found, fallback to bundled legacy geojson. candidate=" + candidateGeojsonRoot);
                    }
                }

                var lines = Path.Combine(geojsonRoot, "water_lines_clipped.geojson");
                var areas = Path.Combine(geojsonRoot, "water_areas_clipped.geojson");
                var roadsMajor = Path.Combine(geojsonRoot, "roads_major_clipped.geojson");
                var roadsDriveable = Path.Combine(geojsonRoot, "roads_driveable_clipped.geojson");
                var paths = Path.Combine(geojsonRoot, "paths.geojson");
                var zoning = Path.Combine(geojsonRoot, "zoning_polygons.geojson");

                Log.Info("GeoBundleBootstrap started.");
                Log.Info("modDir=" + modDir);
                Log.Info("packPath=" + (string.IsNullOrWhiteSpace(config.PackPath) ? "(empty)" : config.PackPath));
                Log.Info("geojsonRoot=" + geojsonRoot);
                Log.Info("lines=" + lines);
                Log.Info("areas=" + areas);
                Log.Info("roadsMajor=" + roadsMajor);
                Log.Info("roadsDriveable=" + roadsDriveable);
                Log.Info("paths=" + paths);
                Log.Info("roadGeometrySource=" + config.RoadGeometrySource + ", renderPaths=" + config.RenderPaths);
                Log.Info("zoning=" + zoning);

                if (!File.Exists(lines))
                {
                    Log.Error("GeoBundleBootstrap: required water lines file not found: " + lines);
                    return;
                }

                if (!File.Exists(areas))
                {
                    Log.Error("GeoBundleBootstrap: required water areas file not found: " + areas);
                    return;
                }

                if (!File.Exists(zoning))
                {
                    Log.Error("GeoBundleBootstrap: required zoning file not found: " + zoning);
                    return;
                }

                var lineStats = GeoJson.AnalyzeLines(lines);
                var lineGeometries = GeoJson.LoadLineGeometries(lines);
                var areaCount = GeoJson.CountFeatures(areas);
                var areaOutlines = GeoJson.LoadPolygonOutlines(areas);

                var selectedRoads = string.Equals(config.RoadGeometrySource, "driveable", StringComparison.OrdinalIgnoreCase)
                    ? roadsDriveable
                    : roadsMajor;

                if (!File.Exists(selectedRoads) && !string.Equals(selectedRoads, roadsMajor, StringComparison.OrdinalIgnoreCase) && File.Exists(roadsMajor))
                {
                    Log.Info("GeoBundleBootstrap: selected road file not found, fallback to roadsMajor. selected=" + selectedRoads + ", fallback=" + roadsMajor);
                    selectedRoads = roadsMajor;
                }

                var roadGeometries = File.Exists(selectedRoads)
                    ? GeoJson.LoadRoadGeometries(selectedRoads)
                    : new List<GeoRoadLine>();

                var pathGeometries = File.Exists(paths)
                    ? GeoJson.LoadRoadGeometries(paths)
                    : new List<GeoRoadLine>();

                MarkRoadLinesAsPath(pathGeometries);

                var zoningPolygons = File.Exists(zoning)
                    ? GeoJson.LoadZoningPolygons(zoning)
                    : new List<GeoZoningPolygon>();

                Log.Info(
                    "Loaded OK. Water line features: " + lineStats.FeatureCount +
                    ", line geometries: " + lineStats.LineGeometryCount +
                    ", points: " + lineStats.PointCount +
                    ", unsupported geometries: " + lineStats.UnsupportedGeometryCount +
                    ", water areas: " + areaCount
                );

                if (lineStats.FirstPoint != null)
                    Log.Info("First line point: lon=" + lineStats.FirstPoint.Lon + ", lat=" + lineStats.FirstPoint.Lat);

                if (lineStats.LastPoint != null)
                    Log.Info("Last line point: lon=" + lineStats.LastPoint.Lon + ", lat=" + lineStats.LastPoint.Lat);

                Log.Info("Loaded area outlines: " + areaOutlines.Count);
                Log.Info("Loaded roads geometries source=" + config.RoadGeometrySource + ": " + roadGeometries.Count);
                Log.Info("Loaded path geometries: " + pathGeometries.Count);

                var cacheBoundsSource = new List<List<GeoPoint>>();
                cacheBoundsSource.AddRange(lineGeometries);
                cacheBoundsSource.AddRange(areaOutlines);

                var cacheBounds = GeoBoundsCalculator.CalculateBounds(cacheBoundsSource);
                var cacheOriginLon = config.UseGeoJsonCenter ? cacheBounds.CenterLon : config.OriginLon;
                var cacheOriginLat = config.UseGeoJsonCenter ? cacheBounds.CenterLat : config.OriginLat;
                var runtimeImportGeometries = new List<GeoRoadLine>();
                runtimeImportGeometries.AddRange(roadGeometries);
                runtimeImportGeometries.AddRange(pathGeometries);

                Log.Info(
                    "Cached runtime import geometries: roads=" + roadGeometries.Count +
                    ", paths=" + pathGeometries.Count +
                    ", total=" + runtimeImportGeometries.Count
                );

                var roadSourceKey =
                    (config.ActiveBundleId ?? "") + "|" +
                    (config.RoadGeometrySource ?? "") + "|" +
                    Path.GetFileName(selectedRoads) + "|paths=" + Path.GetFileName(paths);

                CityTimelineMod.Roads.RuntimeRoadSpawner.SetCachedRoadLines(
                    runtimeImportGeometries,
                    config,
                    cacheOriginLon,
                    cacheOriginLat,
                    roadSourceKey
                );

                var renderWaterLineGeometries = new List<List<GeoPoint>>();
                var renderWaterAreaOutlines = new List<List<GeoPoint>>();

                if (config.RenderWaterLines)
                {
                    renderWaterLineGeometries.AddRange(lineGeometries);
                    Log.Info("Render water lines enabled: " + lineGeometries.Count);
                }
                else
                {
                    Log.Info("Render water lines disabled by config.");
                }

                if (config.RenderWaterAreas)
                {
                    renderWaterAreaOutlines.AddRange(areaOutlines);
                    Log.Info("Render water areas enabled: " + areaOutlines.Count);
                }
                else
                {
                    Log.Info("Render water areas disabled by config.");
                }

                var renderRoadGeometries = new List<GeoRoadLine>();

                if (config.RenderRoads)
                {
                    renderRoadGeometries.AddRange(roadGeometries);
                    Log.Info("Render roads enabled source=" + config.RoadGeometrySource + ": " + roadGeometries.Count);

                    if (config.RenderPaths)
                    {
                        renderRoadGeometries.AddRange(pathGeometries);
                        Log.Info("Render paths enabled: " + pathGeometries.Count);
                    }
                    else
                    {
                        Log.Info("Render paths disabled by config.");
                    }
                }
                else
                {
                    Log.Info("Render roads disabled by config.");
                }

                Log.Info("Total water line render geometries: " + renderWaterLineGeometries.Count);
                Log.Info("Total water area render geometries: " + renderWaterAreaOutlines.Count);

                // Paths must be independently renderable when renderRoads=false.
                if (config.RenderPaths && pathGeometries != null && pathGeometries.Count > 0)
                {
                    var hasPathRenderGeometries = false;

                    foreach (var line in renderRoadGeometries)
                    {
                        if (line != null && line.IsPath)
                        {
                            hasPathRenderGeometries = true;
                            break;
                        }
                    }

                    if (!hasPathRenderGeometries)
                    {
                        renderRoadGeometries.AddRange(pathGeometries);
                        Log.Info("Render paths enabled independently: " + pathGeometries.Count);
                    }
                }

                Log.Info("Total road render geometries: " + renderRoadGeometries.Count);
                Log.Info("Total zoning polygons loaded: " + zoningPolygons.Count);
                LogZoningSummary(zoningPolygons);

                GeoDebugOverlay.Install(renderWaterLineGeometries, renderWaterAreaOutlines, renderRoadGeometries, zoningPolygons, config);
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
            }
        }

        private static void EnsureBundledConfig(string modDir)
        {
            Directory.CreateDirectory(modDir);
            ExtractBundledFile("config.json", Path.Combine(modDir, "config.json"));
        }

        private static void EnsureBundledLegacyGeojson(string targetRoot)
        {
            Directory.CreateDirectory(targetRoot);

            ExtractBundledFile("water_lines_clipped.geojson", Path.Combine(targetRoot, "water_lines_clipped.geojson"));
            ExtractBundledFile("water_areas_clipped.geojson", Path.Combine(targetRoot, "water_areas_clipped.geojson"));
            ExtractBundledFile("roads_major_clipped.geojson", Path.Combine(targetRoot, "roads_major_clipped.geojson"));
            ExtractBundledFile("zoning_polygons.geojson", Path.Combine(targetRoot, "zoning_polygons.geojson"));
            ExtractBundledFile("bbox_manifest.json", Path.Combine(targetRoot, "bbox_manifest.json"));
        }

        private static void ExtractBundledFile(string fileName, string outputPath)
        {
            var existing = new FileInfo(outputPath);

            if (existing.Exists && existing.Length > 0)
                return;

            var outputDirectory = Path.GetDirectoryName(outputPath);

            if (!string.IsNullOrWhiteSpace(outputDirectory))
                Directory.CreateDirectory(outputDirectory);

            var asm = typeof(Mod).Assembly;
            var resourceName = "";

            foreach (var name in asm.GetManifestResourceNames())
            {
                if (name.EndsWith("." + fileName, StringComparison.OrdinalIgnoreCase))
                {
                    resourceName = name;
                    break;
                }
            }

            if (string.IsNullOrWhiteSpace(resourceName))
            {
                Log.Error("GeoBundleBootstrap: embedded resource not found for file=" + fileName);
                Log.Error("GeoBundleBootstrap: available embedded resources=" + string.Join(", ", asm.GetManifestResourceNames()));
                throw new FileNotFoundException("Embedded resource not found for file: " + fileName);
            }

            using (var input = asm.GetManifestResourceStream(resourceName))
            {
                if (input == null)
                    throw new FileNotFoundException("Embedded resource stream is null: " + resourceName);

                using (var output = File.Create(outputPath))
                {
                    input.CopyTo(output);
                }
            }

            Log.Info("GeoBundleBootstrap: extracted bundled file: " + outputPath);
        }

        private static bool UpdateActiveBundleIdInConfig(string modDir, string activeBundleId)
        {
            if (string.IsNullOrWhiteSpace(activeBundleId))
            {
                Log.Error("GeoBundleBootstrap: cannot reload empty activeBundleId.");
                return false;
            }

            var configPath = Path.Combine(modDir, "config.json");

            if (!File.Exists(configPath))
            {
                Log.Error("GeoBundleBootstrap: config.json not found: " + configPath);
                return false;
            }

            try
            {
                var root = JObject.Parse(File.ReadAllText(configPath));
                root["useBundleIndex"] = true;
                root["activeBundleId"] = activeBundleId;

                File.WriteAllText(configPath, root.ToString(Newtonsoft.Json.Formatting.Indented));
                Log.Info("GeoBundleBootstrap: config activeBundleId saved=" + activeBundleId);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("GeoBundleBootstrap: failed to update activeBundleId in config. " + ex);
                return false;
            }
        }

        private static void MarkRoadLinesAsPath(List<GeoRoadLine> lines)
        {
            if (lines == null)
                return;

            foreach (var line in lines)
            {
                if (line != null)
                    line.IsPath = true;
            }
        }

        private static string ResolveModDirectory()
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            var runtimeDir = Path.GetFullPath(
                Path.Combine(
                    localAppData,
                    "..",
                    "LocalLow",
                    "Colossal Order",
                    "Cities Skylines II",
                    "Mods",
                    "CityTimelineMod"
                )
            );

            Directory.CreateDirectory(runtimeDir);

            Log.Info("GeoBundleBootstrap: using runtime modDir=" + runtimeDir);
            Log.Info("GeoBundleBootstrap: Assembly.Location=" + SafeString(typeof(Mod).Assembly.Location));
            Log.Info("GeoBundleBootstrap: Assembly.CodeBase=" + SafeString(typeof(Mod).Assembly.CodeBase));

            return runtimeDir;
        }

        private static string SafeString(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "(empty)" : value;
        }

        private static void LogZoningSummary(List<GeoZoningPolygon> zoningPolygons)
        {
            if (zoningPolygons == null || zoningPolygons.Count == 0)
            {
                Log.Info("Zoning summary: none");
                return;
            }

            var zones = new Dictionary<string, int>();
            var cs2Values = new Dictionary<string, int>();

            foreach (var polygon in zoningPolygons)
            {
                var zone = polygon != null ? polygon.Zone : null;
                var cs2 = polygon != null ? polygon.Cs2 : null;

                if (string.IsNullOrWhiteSpace(zone))
                    zone = "(missing)";

                if (string.IsNullOrWhiteSpace(cs2))
                    cs2 = "(missing)";

                int zoneCount;
                zones.TryGetValue(zone, out zoneCount);
                zones[zone] = zoneCount + 1;

                int cs2Count;
                cs2Values.TryGetValue(cs2, out cs2Count);
                cs2Values[cs2] = cs2Count + 1;
            }

            Log.Info("Zoning zone summary: " + FormatCounts(zones));
            Log.Info("Zoning cs2 summary: " + FormatCounts(cs2Values));
        }

        private static string FormatCounts(Dictionary<string, int> counts)
        {
            if (counts == null || counts.Count == 0)
                return "(none)";

            var parts = new List<string>();

            foreach (var pair in counts)
                parts.Add(pair.Key + "=" + pair.Value);

            return string.Join(", ", parts.ToArray());
        }
    }
}
