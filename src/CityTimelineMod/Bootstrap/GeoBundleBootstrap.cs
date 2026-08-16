using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using CityTimelineMod.Bundles;
using CityTimelineMod.Config;
using CityTimelineMod.Geometry;
using CityTimelineMod.Importers;
using CityTimelineMod.Rendering;
using CityTimelineMod.Rendering.Railways;
using CityTimelineMod.Rendering.Services;
using CityTimelineMod.Rendering.Statistics;
using CityTimelineMod.Util;

namespace CityTimelineMod
{
    internal static class GeoBundleBootstrap
    {
        private static bool _ran = false;
        private static readonly object RuntimeBaselineSync = new object();
        private static GeoOverlayConfig _bundleReloadBaseline;

        internal static GeoOverlayConfig LoadRuntimeConfigSnapshot()
        {
            var modDir = ResolveModDirectory();
            EnsureBundledConfig(modDir);
            var snapshot = GeoOverlayConfig.Load(modDir);

            lock (RuntimeBaselineSync)
            {
                _bundleReloadBaseline = snapshot != null && snapshot.IsReliable
                    ? snapshot.CreateBundleReloadBaseline()
                    : null;
            }

            return snapshot;
        }

        internal static PreparedVisualRuntimeActivation PrepareVisualRuntimeActivation(GeoOverlayConfig config)
        {
            if (config == null || !Mod.RuntimeEnabled)
                return null;

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                BundleOverlayPayload payload;
                if (!TryLoadActiveBundle(config, out payload, true))
                {
                    Log.Error("GeoBundleBootstrap: prepare visual runtime activation failed to load bundle.");
                    return null;
                }

                var prepared = new PreparedVisualRuntimeActivation(config, payload);
                stopwatch.Stop();
                Log.Info($"GeoBundleBootstrap: visual runtime preparation completed in {stopwatch.ElapsedMilliseconds}ms.");
                return prepared;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Log.Error($"GeoBundleBootstrap: async visual runtime preparation failed after {stopwatch.ElapsedMilliseconds}ms. {ex}");
                return null;
            }
        }

        internal static bool InstallPreparedVisualRuntimeActivation(
            PreparedVisualRuntimeActivation prepared,
            GeoOverlayConfig runtimeConfig)
        {
            if (prepared == null || runtimeConfig == null || !Mod.RuntimeEnabled)
                return false;

            var previousBundleState = runtimeConfig.CloneForBundleReload();
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                runtimeConfig.ApplyPreparedBundleStateFrom(prepared.Config);

                var installed =
                    InstallLoadedBundle(prepared.Payload, runtimeConfig) &&
                    GeoDebugOverlay.IsInstalled;

                stopwatch.Stop();
                _ran = installed;

                Log.Info($"GeoBundleBootstrap: prepared visual runtime install completed. installed={installed}, elapsedMs={stopwatch.ElapsedMilliseconds}");

                if (!installed)
                    runtimeConfig.ApplyPreparedBundleStateFrom(previousBundleState);

                return installed;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                runtimeConfig.ApplyPreparedBundleStateFrom(previousBundleState);
                _ran = GeoDebugOverlay.IsInstalled;

                Log.Error($"GeoBundleBootstrap: prepared visual runtime install failed after {stopwatch.ElapsedMilliseconds}ms. {ex}");
                return false;
            }
        }

        internal static bool RunOnce(GeoOverlayConfig config)
        {
            if (!Mod.RuntimeEnabled)
            {
                Log.Info("GeoBundleBootstrap: runtime gate is closed; bootstrap skipped.");
                return false;
            }

            if (_ran && GeoDebugOverlay.IsInstalled)
                return true;

            _ran = false;
            var installed = LoadAndInstallActiveBundle(config);
            _ran = installed && GeoDebugOverlay.IsInstalled;
            return _ran;
        }

        internal static void Reset(bool releaseRuntimeBaseline = false)
        {
            _ran = false;

            if (!releaseRuntimeBaseline)
                return;

            lock (RuntimeBaselineSync)
                _bundleReloadBaseline = null;
        }

        internal static bool UpdateBundleReloadBaselineAfterStrictVisualReload(
            GeoOverlayConfig strictVisualSnapshot)
        {
            if (strictVisualSnapshot == null || !strictVisualSnapshot.IsReliable)
            {
                Log.Error(
                    "GeoBundleBootstrap: strict visual reload did not provide a reliable bundle baseline."
                );
                return false;
            }

            var refreshedBaseline = strictVisualSnapshot.CreateBundleReloadBaseline();
            lock (RuntimeBaselineSync)
                _bundleReloadBaseline = refreshedBaseline;

            return true;
        }

        internal static void ReloadActiveBundle(string activeBundleId)
        {
            if (!Mod.RuntimeEnabled)
            {
                Log.Info("GeoBundleBootstrap: runtime gate is closed; reload skipped.");
                return;
            }

            GeoOverlayConfig config = null;
            GeoOverlayConfig previousBundleState = null;
            BundleSelectionFileState persistedSelection = null;
            var overlaySwapStarted = false;

            try
            {
                var modDir = ResolveModDirectory();
                EnsureBundledConfig(modDir);

                config = Mod.RuntimeConfig;
                if (config == null || !config.IsReliable)
                {
                    Log.Error("GeoBundleBootstrap: reload blocked because the runtime snapshot is unavailable.");
                    return;
                }

                GeoOverlayConfig candidateConfig;
                BundleOverlayPayload candidatePayload;
                string canonicalId;
                string bundleIndexPath;
                if (!TryPrepareBundleReloadCandidate(
                    modDir,
                    config,
                    activeBundleId,
                    out candidateConfig,
                    out canonicalId,
                    out bundleIndexPath
                ))
                    return;

                // Parsing every required/optional source happens while the old
                // overlay is still alive.  Only the final Install call may tear
                // it down.
                if (!TryLoadActiveBundle(candidateConfig, out candidatePayload, false))
                {
                    Log.Error("GeoBundleBootstrap: candidate bundle rejected; current overlay kept active.");
                    return;
                }

                if (!TryPersistActiveBundleSelection(
                    modDir,
                    config.BundlesRoot,
                    canonicalId,
                    bundleIndexPath,
                    out persistedSelection
                ))
                {
                    Log.Error("GeoBundleBootstrap: candidate persistence failed; current overlay kept active.");
                    return;
                }

                previousBundleState = config.CloneForBundleReload();
                config.ApplyPreparedBundleStateFrom(candidateConfig);

                Log.Info("GeoBundleBootstrap: reload requested activeBundleId=" + activeBundleId);
                overlaySwapStarted = true;
                if (InstallLoadedBundle(candidatePayload, config) && GeoDebugOverlay.IsInstalled)
                {
                    _ran = true;
                    Log.Info("GeoBundleBootstrap: bundle reload committed activeBundleId=" + canonicalId);
                    return;
                }

                throw new InvalidOperationException("The candidate overlay was not operational after installation.");
            }
            catch (Exception ex)
            {
                Log.Error("GeoBundleBootstrap: reload failed. " + ex);

                if (persistedSelection != null && !RestoreActiveBundleSelection(persistedSelection))
                {
                    Log.Error(
                        "GeoBundleBootstrap: failed reload left bundle selection rollback unverified; " +
                        "the next startup must revalidate config.json and bundle_index.json."
                    );
                }

                if (config != null && previousBundleState != null)
                    config.ApplyPreparedBundleStateFrom(previousBundleState);

                if (overlaySwapStarted && config != null)
                {
                    BundleOverlayPayload previousPayload;
                    var restored = TryLoadActiveBundle(config, out previousPayload, false) &&
                        InstallLoadedBundle(previousPayload, config) &&
                        GeoDebugOverlay.IsInstalled;
                    _ran = restored;
                    Log.Info("GeoBundleBootstrap: previous overlay restore after failed reload=" + restored);
                }
                else
                {
                    _ran = GeoDebugOverlay.IsInstalled;
                }
            }
        }

        private static bool LoadAndInstallActiveBundle(GeoOverlayConfig config)
        {
            BundleOverlayPayload payload;
            return TryLoadActiveBundle(config, out payload, true) && InstallLoadedBundle(payload, config);
        }

        private static bool TryLoadActiveBundle(
            GeoOverlayConfig config,
            out BundleOverlayPayload payload,
            bool prepareBundle
        )
        {
            payload = null;

            if (!Mod.RuntimeEnabled)
                return false;

            try
            {
                var modDir = ResolveModDirectory();

                if (config == null || !config.IsReliable)
                {
                    Log.Error("GeoBundleBootstrap: unreliable config; overlay bootstrap blocked.");
                    return false;
                }

                if (!config.ModEnabled)
                {
                    Log.Info("CityTimelineMod disabled by config: modEnabled=false. Overlay bootstrap skipped.");
                    return false;
                }

                if (prepareBundle && !config.PrepareBundle(modDir))
                {
                    Log.Error(
                        "GeoBundleBootstrap: active bundle preparation failed; " +
                        "the runtime configuration remains reliable but no overlay will be installed."
                    );
                    return false;
                }

                string geojsonRoot;

                if (config.UseBundleIndex)
                {
                    if (!config.BundleIndexResolutionSucceeded)
                    {
                        Log.Error(
                            "GeoBundleBootstrap: active indexed bundle was not installed: " +
                            (string.IsNullOrWhiteSpace(config.BundleIndexResolutionError)
                                ? "unknown bundle resolution error."
                                : config.BundleIndexResolutionError)
                        );
                        return false;
                    }

                    if (string.IsNullOrWhiteSpace(config.PackPath))
                    {
                        Log.Error("GeoBundleBootstrap: validated indexed bundle has no GeoJSON packPath.");
                        return false;
                    }

                    var candidateGeojsonRoot = Path.Combine(config.PackPath, "geojson");
                    if (!Directory.Exists(candidateGeojsonRoot))
                    {
                        Log.Error("GeoBundleBootstrap: validated indexed GeoJSON folder disappeared: " + candidateGeojsonRoot);
                        return false;
                    }

                    geojsonRoot = candidateGeojsonRoot;
                }
                else
                {
                    var legacyGeojsonRoot = Path.Combine(modDir, "data", "legacy-geojson");
                    EnsureBundledLegacyGeojson(legacyGeojsonRoot);
                    geojsonRoot = legacyGeojsonRoot;

                    if (!string.IsNullOrWhiteSpace(config.PackPath))
                    {
                        var candidateGeojsonRoot = Path.Combine(config.PackPath, "geojson");
                        if (Directory.Exists(candidateGeojsonRoot))
                            geojsonRoot = candidateGeojsonRoot;
                        else
                            Log.Error("GeoBundleBootstrap: legacy packPath not found; using bundled legacy GeoJSON. candidate=" + candidateGeojsonRoot);
                    }
                }

                var lines = Path.Combine(geojsonRoot, "water_lines_clipped.geojson");
                var areas = Path.Combine(geojsonRoot, "water_areas_clipped.geojson");
                var roadsMajor = Path.Combine(geojsonRoot, "roads_major_clipped.geojson");
                var roadsDriveable = Path.Combine(geojsonRoot, "roads_driveable_clipped.geojson");
                var paths = Path.Combine(geojsonRoot, "paths.geojson");
                var railways = Path.Combine(geojsonRoot, "railways.geojson");
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
                Log.Info("railways=" + railways);
                Log.Info("roadGeometrySource=" + config.RoadGeometrySource + ", renderPaths=" + config.RenderPaths);
                Log.Info("zoning=" + zoning);

                if (!File.Exists(lines))
                {
                    Log.Error("GeoBundleBootstrap: required water lines file not found: " + lines);
                    return false;
                }

                if (!File.Exists(areas))
                {
                    Log.Error("GeoBundleBootstrap: required water areas file not found: " + areas);
                    return false;
                }

                if (!File.Exists(zoning))
                {
                    Log.Error("GeoBundleBootstrap: required zoning file not found: " + zoning);
                    return false;
                }

                var loadTiming =
                    System.Diagnostics.Stopwatch.StartNew();

                var waterLineLoad =
                    GeoJson.LoadLineGeometriesWithAnalysis(lines);

                var lineStats = waterLineLoad.Analysis;
                var lineGeometries = waterLineLoad.Geometries;

                Log.Info(
                    "GeoBundle load timing: waterLinesCombinedMs=" +
                    loadTiming.ElapsedMilliseconds +
                    ", features=" +
                    lineStats.FeatureCount +
                    ", geometries=" +
                    lineGeometries.Count
                );

                loadTiming.Restart();

                var waterAreaLoad =
                    GeoJson.LoadPolygonOutlinesWithCount(areas);

                var areaCount = waterAreaLoad.FeatureCount;
                var areaOutlines = waterAreaLoad.Outlines;

                Log.Info(
                    "GeoBundle load timing: waterAreasCombinedMs=" +
                    loadTiming.ElapsedMilliseconds +
                    ", features=" +
                    areaCount +
                    ", outlines=" +
                    areaOutlines.Count
                );

                loadTiming.Restart();

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

                Log.Info(
                    "GeoBundle load timing: roadsMs=" +
                    loadTiming.ElapsedMilliseconds +
                    ", count=" +
                    roadGeometries.Count
                );
                loadTiming.Restart();

                var pathGeometries = File.Exists(paths)
                    ? GeoJson.LoadRoadGeometries(paths)
                    : new List<GeoRoadLine>();

                Log.Info(
                    "GeoBundle load timing: pathsMs=" +
                    loadTiming.ElapsedMilliseconds +
                    ", count=" +
                    pathGeometries.Count
                );
                loadTiming.Restart();

                MarkRoadLinesAsPath(pathGeometries);

                var railwayGeometries = new List<GeoRailwayLine>();
                var railwayAvailable = false;
                var railwayStatus = "Aucune donnée ferroviaire disponible dans ce bundle.";

                if (File.Exists(railways))
                {
                    try
                    {
                        railwayGeometries = RailwayGeoJsonLoader.Load(railways);
                        railwayAvailable = railwayGeometries.Count > 0;
                        railwayStatus = railwayAvailable
                            ? railwayGeometries.Count + " voie(s) ferroviaire(s) chargée(s)."
                            : "Aucune donnée ferroviaire disponible dans ce bundle.";
                    }
                    catch (Exception ex)
                    {
                        railwayGeometries.Clear();
                        railwayStatus = "Données ferroviaires indisponibles ou illisibles. Le bundle reste utilisable.";
                        Log.Error("GeoBundleBootstrap: optional railway overlay failed to load. " + ex);
                    }
                }
                else
                {
                    Log.Info("GeoBundleBootstrap: optional railways.geojson not found; bundle continues without railway overlay.");
                }

                Log.Info(
                    "GeoBundle load timing: railwaysMs=" +
                    loadTiming.ElapsedMilliseconds +
                    ", count=" +
                    railwayGeometries.Count
                );
                loadTiming.Restart();

                var zoningHudFeatureCount = 0;
                var zoningHudCounts =
                    new Dictionary<string, int>(
                        StringComparer.OrdinalIgnoreCase
                    );

                var zoningPolygons = File.Exists(zoning)
                    ? GeoJson.LoadZoningPolygons(
                        zoning,
                        delegate(string cs2, string zone)
                        {
                            zoningHudFeatureCount++;

                            var key =
                                BundleHudStatsLoader.ClassifyZoning(
                                    cs2,
                                    zone
                                );

                            int count;
                            zoningHudCounts.TryGetValue(
                                key,
                                out count
                            );

                            zoningHudCounts[key] = count + 1;
                        })
                    : new List<GeoZoningPolygon>();

                Log.Info(
                    "GeoBundle load timing: zoningMs=" +
                    loadTiming.ElapsedMilliseconds +
                    ", count=" +
                    zoningPolygons.Count
                );
                loadTiming.Restart();

                var geoJsonPackRoot = BundleHudStatsLoader.ResolveGeoJsonPackRoot(geojsonRoot);
                var serviceLoadResult = ServiceGeoJsonLoader.LoadPack(geoJsonPackRoot);

                Log.Info(
                    "GeoBundle load timing: servicesMs=" +
                    loadTiming.ElapsedMilliseconds +
                    ", count=" +
                    serviceLoadResult.Points.Count
                );
                loadTiming.Restart();

                var bundleHudSnapshot =
                    BundleHudStatsLoader.Load(
                        geoJsonPackRoot,
                        zoningHudFeatureCount,
                        zoningHudCounts
                    );

                Log.Info(
                    "GeoBundle load timing: hudStatsMs=" +
                    loadTiming.ElapsedMilliseconds
                );

                Log.Info(
                    "Bundle completeness: " + bundleHudSnapshot.PresentSourceFileCount +
                    "/" + bundleHudSnapshot.ExpectedSourceFileCount +
                    " expected files, complete=" + bundleHudSnapshot.CoverageComplete +
                    (bundleHudSnapshot.MissingSourceFiles.Count == 0
                        ? ""
                        : ", missing=" + string.Join(", ", bundleHudSnapshot.MissingSourceFiles))
                );

                Log.Info(
                    "Loaded service points: " + serviceLoadResult.Points.Count +
                    ", loaded families=" + serviceLoadResult.LoadedFamilyCount +
                    ", missing families=" + serviceLoadResult.MissingFamilyCount +
                    ", failed families=" + serviceLoadResult.FailedFamilyCount
                );

                foreach (var serviceFamily in serviceLoadResult.Families)
                {
                    if (serviceFamily == null || serviceFamily.Definition == null)
                        continue;

                    Log.Info(
                        "Service family " + serviceFamily.Definition.Key +
                        ": available=" + serviceFamily.Available +
                        ", points=" + serviceFamily.Points.Count +
                        ", sourceFeatures=" + serviceFamily.SourceFeatureCount +
                        ", skipped=" + serviceFamily.SkippedFeatureCount +
                        (string.IsNullOrWhiteSpace(serviceFamily.Error) ? "" : ", status=" + serviceFamily.Error)
                    );
                }

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
                Log.Info("Loaded railway geometries: " + railwayGeometries.Count + ", available=" + railwayAvailable);

                var renderWaterLineGeometries = new List<List<GeoPoint>>(lineGeometries);
                var renderWaterAreaOutlines = new List<List<GeoPoint>>(areaOutlines);
                var renderRoadGeometries = new List<GeoRoadLine>(roadGeometries);
                renderRoadGeometries.AddRange(pathGeometries);

                Log.Info("Water line source geometries retained: " + lineGeometries.Count +
                    ", initially visible=" + config.RenderWaterLines);
                Log.Info("Water area source geometries retained: " + areaOutlines.Count +
                    ", initially visible=" + config.RenderWaterAreas);
                Log.Info("Road source geometries retained source=" + config.RoadGeometrySource +
                    ": " + roadGeometries.Count + ", initially visible=" + config.RenderRoads);
                Log.Info("Path source geometries retained: " + pathGeometries.Count +
                    ", initially visible=" + config.RenderPaths);

                Log.Info("Total water line render geometries: " + renderWaterLineGeometries.Count);
                Log.Info("Total water area render geometries: " + renderWaterAreaOutlines.Count);

                Log.Info("Total road render geometries: " + renderRoadGeometries.Count);
                Log.Info("Total zoning polygons loaded: " + zoningPolygons.Count);
                LogZoningSummary(zoningPolygons);

                payload = new BundleOverlayPayload(
                    renderWaterLineGeometries,
                    renderWaterAreaOutlines,
                    renderRoadGeometries,
                    zoningPolygons,
                    serviceLoadResult.Points,
                    serviceLoadResult,
                    bundleHudSnapshot,
                    railwayGeometries,
                    railwayAvailable,
                    railwayStatus
                );
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
                return false;
            }
        }

        private static bool InstallLoadedBundle(BundleOverlayPayload payload, GeoOverlayConfig config)
        {
            if (payload == null || config == null || !Mod.RuntimeEnabled)
                return false;

            return GeoDebugOverlay.Install(
                payload.WaterLines,
                payload.WaterAreaOutlines,
                payload.RoadLines,
                payload.ZoningPolygons,
                payload.ServicePoints,
                payload.ServiceLoadResult,
                payload.BundleHudSnapshot,
                payload.RailwayLines,
                payload.RailwayAvailable,
                payload.RailwayStatus,
                config
            );
        }

        private static void EnsureBundledConfig(string modDir)
        {
            Directory.CreateDirectory(modDir);
            var configPath = Path.Combine(modDir, "config.json");

            try
            {
                var attributes = File.GetAttributes(configPath);
                if ((attributes & FileAttributes.Directory) != 0)
                    throw new IOException("Expected config.json but found a directory: " + configPath);

                return;
            }
            catch (FileNotFoundException)
            {
            }
            catch (DirectoryNotFoundException)
            {
            }

            ExtractBundledFile("config.json", configPath);
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

        private static bool TryPrepareBundleReloadCandidate(
            string modDir,
            GeoOverlayConfig runtimeConfig,
            string requestedId,
            out GeoOverlayConfig candidate,
            out string canonicalId,
            out string indexPath
        )
        {
            candidate = null;
            canonicalId = null;
            indexPath = null;

            if (runtimeConfig == null || string.IsNullOrWhiteSpace(requestedId))
            {
                Log.Error("GeoBundleBootstrap: cannot prepare an empty bundle selection.");
                return false;
            }

            try
            {
                var configuredRoot = string.IsNullOrWhiteSpace(runtimeConfig.BundlesRoot)
                    ? "data/exports/bundles"
                    : runtimeConfig.BundlesRoot;
                var bundlesRoot = BundleResolver.ResolveBundlesRootPath(configuredRoot, modDir);
                indexPath = Path.Combine(bundlesRoot, "bundle_index.json");
                var indexRoot = JObject.Parse(File.ReadAllText(indexPath));
                var bundles = indexRoot["bundles"] as JArray;
                if (bundles == null)
                {
                    Log.Error("GeoBundleBootstrap: bundle_index.json has no bundles array.");
                    return false;
                }

                JObject selected = null;
                foreach (var token in bundles)
                {
                    var entry = token as JObject;
                    if (entry != null && string.Equals(
                        ReadIndexString(entry, "id"),
                        requestedId.Trim(),
                        StringComparison.OrdinalIgnoreCase
                    ))
                    {
                        selected = entry;
                        break;
                    }
                }

                if (selected == null)
                {
                    Log.Error("GeoBundleBootstrap: bundle not found in index: " + requestedId);
                    return false;
                }

                string manifestPath;
                string bundleRoot;
                string validationError;
                if (!BundleResolver.TryValidateEntry(
                    bundlesRoot,
                    selected,
                    out manifestPath,
                    out bundleRoot,
                    out validationError
                ))
                {
                    Log.Error("GeoBundleBootstrap: candidate bundle is invalid. " + validationError);
                    return false;
                }

                canonicalId = ReadIndexString(selected, "id").Trim();

                GeoOverlayConfig fallbackBaseline;
                lock (RuntimeBaselineSync)
                    fallbackBaseline = _bundleReloadBaseline;

                if (fallbackBaseline == null || !fallbackBaseline.IsReliable)
                {
                    Log.Error(
                        "GeoBundleBootstrap: candidate preparation blocked because the strict config " +
                        "fallback snapshot is unavailable or invalid."
                    );
                    return false;
                }

                candidate = runtimeConfig.CloneForBundleReloadCandidate(fallbackBaseline);
                candidate.UseBundleIndex = false;
                candidate.BundleManifestPath = manifestPath;
                candidate.PackPath = null;
                BundleResolver.ApplyIndexMetadata(candidate, selected);

                if (!candidate.PrepareBundle(modDir))
                {
                    candidate = null;
                    return false;
                }

                candidate.UseBundleIndex = true;
                candidate.ResolvedBundlesRoot = bundlesRoot;
                candidate.ActiveBundleRoot = bundleRoot;
                candidate.ActiveBundleId = canonicalId;
                candidate.BundleIndexResolutionSucceeded = true;
                candidate.BundleIndexResolutionError = null;
                return true;
            }
            catch (Exception ex)
            {
                candidate = null;
                Log.Error("GeoBundleBootstrap: candidate preparation failed. " + ex);
                return false;
            }
        }

        private static bool TryPersistActiveBundleSelection(
            string modDir,
            string configuredBundlesRoot,
            string canonicalId,
            string indexPath,
            out BundleSelectionFileState state
        )
        {
            state = null;
            var configPath = CityTimelineConfigStorage.ResolveWritableConfigPath(modDir);
            BundleSelectionFileState captured;
            if (!TryCaptureActiveBundleSelection(configPath, indexPath, out captured))
                return false;

            JObject ignoredRoot;
            string updateError;
            if (!GeoOverlayConfig.TryUpdateRuntimeConfigFile(
                configPath,
                root =>
                {
                    root["useBundleIndex"] = true;
                    RemoveLegacyActiveBundleId(root);
                },
                out ignoredRoot,
                out updateError
            ))
            {
                Log.Error("GeoBundleBootstrap: failed to persist bundle-index mode in config.json. " + updateError);
                return false;
            }

            string configVerificationError;
            if (!VerifyPersistedBundleIndexMode(
                configPath,
                out configVerificationError
            ))
            {
                Log.Error(
                    "GeoBundleBootstrap: config.json bundle-index mode verification failed. " +
                    configVerificationError
                );
                if (!RestoreActiveBundleSelection(captured))
                {
                    Log.Error(
                        "GeoBundleBootstrap: config verification rollback could not be verified; " +
                        "config.json and bundle_index.json require startup revalidation."
                    );
                }
                return false;
            }

            string persistedCanonicalId;
            string pointerError;
            var bundlesRoot = string.IsNullOrWhiteSpace(configuredBundlesRoot)
                ? "data/exports/bundles"
                : configuredBundlesRoot;
            if (!BundleResolver.TryUpdateActiveBundlePointer(
                bundlesRoot,
                modDir,
                canonicalId,
                out persistedCanonicalId,
                out pointerError
            ) || !string.Equals(persistedCanonicalId, canonicalId, StringComparison.Ordinal))
            {
                Log.Error("GeoBundleBootstrap: failed to persist bundle_index.json. " + pointerError);
                if (!RestoreActiveBundleSelection(captured))
                {
                    Log.Error(
                        "GeoBundleBootstrap: persistence failure rollback could not be verified; " +
                        "config.json and bundle_index.json require startup revalidation."
                    );
                }
                return false;
            }

            state = captured;
            Log.Info(
                "GeoBundleBootstrap: bundle selection persisted in bundle_index.json authority. " +
                "activeBundleId=" + canonicalId
            );
            return true;
        }

        private static void RemoveLegacyActiveBundleId(JObject root)
        {
            if (root == null)
                return;

            JProperty legacyProperty = null;
            foreach (var property in root.Properties())
            {
                if (string.Equals(
                    property.Name,
                    "activeBundleId",
                    StringComparison.OrdinalIgnoreCase
                ))
                {
                    legacyProperty = property;
                    break;
                }
            }

            if (legacyProperty != null)
                legacyProperty.Remove();
        }

        private static bool TryCaptureActiveBundleSelection(
            string configPath,
            string indexPath,
            out BundleSelectionFileState state
        )
        {
            state = null;

            try
            {
                state = new BundleSelectionFileState(
                    configPath,
                    File.Exists(configPath),
                    File.Exists(configPath) ? File.ReadAllBytes(configPath) : null,
                    indexPath,
                    File.Exists(indexPath),
                    File.Exists(indexPath) ? File.ReadAllBytes(indexPath) : null
                );
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("GeoBundleBootstrap: failed to snapshot bundle selection files. " + ex);
                return false;
            }
        }

        private static bool RestoreActiveBundleSelection(BundleSelectionFileState state)
        {
            if (state == null)
                return true;

            var indexRestored = RestoreSelectionFile(
                state.IndexPath,
                state.IndexExisted,
                state.IndexContents
            );
            var configRestored = RestoreSelectionFile(
                state.ConfigPath,
                state.ConfigExisted,
                state.ConfigContents
            );
            var restored = indexRestored && configRestored;
            Log.Info("GeoBundleBootstrap: bundle selection rollback complete=" + restored);
            return restored;
        }

        private static bool RestoreSelectionFile(string path, bool existed, byte[] contents)
        {
            try
            {
                if (existed)
                {
                    string restoreError;
                    if (!GeoOverlayConfig.TryWriteBytesAtomically(path, contents, out restoreError))
                    {
                        Log.Error(
                            "GeoBundleBootstrap: atomic selection rollback failed for " + path + ". " +
                            restoreError
                        );
                        return false;
                    }
                }
                else if (File.Exists(path))
                {
                    File.Delete(path);
                }

                if (File.Exists(path) != existed)
                    throw new IOException("Selection rollback existence verification failed for " + path);

                if (existed && !GeoOverlayConfig.ByteArraysEqual(contents, File.ReadAllBytes(path)))
                    throw new IOException("Selection rollback content verification failed for " + path);

                return true;
            }
            catch (Exception ex)
            {
                Log.Error("GeoBundleBootstrap: failed to restore selection file " + path + ". " + ex);
                return false;
            }
        }

        private static bool VerifyPersistedBundleIndexMode(
            string configPath,
            out string error
        )
        {
            error = null;

            try
            {
                var root = JObject.Parse(File.ReadAllText(configPath));
                JToken useBundleIndexToken;
                if (!root.TryGetValue(
                    "useBundleIndex",
                    StringComparison.OrdinalIgnoreCase,
                    out useBundleIndexToken
                ) || useBundleIndexToken.Type != JTokenType.Boolean ||
                    !useBundleIndexToken.Value<bool>())
                {
                    error = "useBundleIndex=true was not persisted.";
                    return false;
                }

                JToken legacyActiveBundleToken;
                if (root.TryGetValue(
                    "activeBundleId",
                    StringComparison.OrdinalIgnoreCase,
                    out legacyActiveBundleToken
                ))
                {
                    error =
                        "activeBundleId must not be duplicated in config.json; " +
                        "bundle_index.json is the active-bundle authority.";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.ToString();
                return false;
            }
        }

        private static string ReadIndexString(JObject root, string name)
        {
            JToken token;
            return root != null && root.TryGetValue(name, StringComparison.OrdinalIgnoreCase, out token)
                ? (token == null ? "" : token.ToString())
                : "";
        }

        internal sealed class PreparedVisualRuntimeActivation
        {
            internal readonly GeoOverlayConfig Config;
            internal readonly BundleOverlayPayload Payload;

            internal PreparedVisualRuntimeActivation(GeoOverlayConfig config, BundleOverlayPayload payload)
            {
                Config = config;
                Payload = payload;
            }
        }

        private sealed class BundleSelectionFileState
        {
            internal readonly string ConfigPath;
            internal readonly bool ConfigExisted;
            internal readonly byte[] ConfigContents;
            internal readonly string IndexPath;
            internal readonly bool IndexExisted;
            internal readonly byte[] IndexContents;

            internal BundleSelectionFileState(
                string configPath,
                bool configExisted,
                byte[] configContents,
                string indexPath,
                bool indexExisted,
                byte[] indexContents
            )
            {
                ConfigPath = configPath;
                ConfigExisted = configExisted;
                ConfigContents = configContents;
                IndexPath = indexPath;
                IndexExisted = indexExisted;
                IndexContents = indexContents;
            }
        }

        internal sealed class BundleOverlayPayload
        {
            internal readonly List<List<GeoPoint>> WaterLines;
            internal readonly List<List<GeoPoint>> WaterAreaOutlines;
            internal readonly List<GeoRoadLine> RoadLines;
            internal readonly List<GeoZoningPolygon> ZoningPolygons;
            internal readonly List<GeoServicePoint> ServicePoints;
            internal readonly ServiceGeoJsonLoadResult ServiceLoadResult;
            internal readonly BundleHudSnapshot BundleHudSnapshot;
            internal readonly List<GeoRailwayLine> RailwayLines;
            internal readonly bool RailwayAvailable;
            internal readonly string RailwayStatus;

            internal BundleOverlayPayload(
                List<List<GeoPoint>> waterLines,
                List<List<GeoPoint>> waterAreaOutlines,
                List<GeoRoadLine> roadLines,
                List<GeoZoningPolygon> zoningPolygons,
                List<GeoServicePoint> servicePoints,
                ServiceGeoJsonLoadResult serviceLoadResult,
                BundleHudSnapshot bundleHudSnapshot,
                List<GeoRailwayLine> railwayLines,
                bool railwayAvailable,
                string railwayStatus
            )
            {
                WaterLines = waterLines;
                WaterAreaOutlines = waterAreaOutlines;
                RoadLines = roadLines;
                ZoningPolygons = zoningPolygons;
                ServicePoints = servicePoints;
                ServiceLoadResult = serviceLoadResult;
                BundleHudSnapshot = bundleHudSnapshot;
                RailwayLines = railwayLines;
                RailwayAvailable = railwayAvailable;
                RailwayStatus = railwayStatus;
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