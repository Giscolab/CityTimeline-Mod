using System;
using System.Collections.Generic;
using System.IO;
using CityTimelineMod.Config;
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

            try
            {
                var modDir = ResolveModDirectory();
                var config = GeoOverlayConfig.Load(modDir);
                if (!config.ModEnabled)
                {
                    Log.Info("CityTimelineMod disabled by config: modEnabled=false. Overlay bootstrap skipped.");
                    return;
                }

                var legacyGeojsonRoot = Path.Combine(modDir, "data", "legacy-geojson");
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

                var lineStats = GeoJson.AnalyzeLines(lines);
                var lineGeometries = GeoJson.LoadLineGeometries(lines);
                var areaCount = GeoJson.CountFeatures(areas);
                var areaOutlines = GeoJson.LoadPolygonOutlines(areas);

                var selectedRoads = string.Equals(config.RoadGeometrySource, "driveable", StringComparison.OrdinalIgnoreCase)
                    ? roadsDriveable
                    : roadsMajor;

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
            try
            {
                var location = typeof(Mod).Assembly.Location;

                if (!string.IsNullOrWhiteSpace(location) && File.Exists(location))
                {
                    var dir = Path.GetDirectoryName(location);

                    if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                        return dir;
                }
            }
            catch
            {
            }

            try
            {
                var codeBase = typeof(Mod).Assembly.CodeBase;

                if (!string.IsNullOrWhiteSpace(codeBase))
                {
                    var uri = new Uri(codeBase);
                    var localPath = uri.LocalPath;

                    if (!string.IsNullOrWhiteSpace(localPath) && File.Exists(localPath))
                    {
                        var dir = Path.GetDirectoryName(localPath);

                        if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                            return dir;
                    }
                }
            }
            catch
            {
            }

            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            var fallback = Path.GetFullPath(
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

            if (!Directory.Exists(fallback))
                throw new DirectoryNotFoundException("Could not resolve mod directory. Fallback does not exist: " + fallback);

            return fallback;
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

