using System;
using System.Collections.Generic;
using System.Globalization;
using CityTimelineMod.Compatibility;
using CityTimelineMod.Config;
using CityTimelineMod.Importers;
using Game.Prefabs;
using Unity.Entities;
using Newtonsoft.Json.Linq;

namespace CityTimelineMod.Roads
{
    internal static partial class RuntimeRoadSpawner
    {
        private static int _seen;
        private static int _logged;
        private static bool _suppressedLogged;
        private static RoadPrefabResolver _resolver;
        private static string _lastImportKey;
        private static IList<GeoRoadLine> _cachedRoadLines;
        private static GeoOverlayConfig _cachedConfig;
        private static double _cachedOriginLon;
        private static double _cachedOriginLat;
        private static string _cachedSourceKey;
        private const int VanillaSafeSingleApplyMaxSegments = 25000;
        private const int VanillaSafeSessionEdgeRoadBudget = 45000;

        internal static void AnalyzeRoadFeature(JToken feature)
        {
            var meta = RoadImportMetadata.FromFeature(feature as JObject);

            if (meta == null)
                return;

            _seen++;
            var prefabHint = RoadPrefabResolver.ResolvePrefabHint(meta);

            if (_logged < 50)
            {
                _logged++;

                Util.Log.Info(
                    "[RoadImport] highway=" + meta.Highway +
                    " lanes=" + meta.TargetLaneCount +
                    " oneway=" + meta.Oneway +
                    " prefabHint=" + prefabHint
                );

                return;
            }

            if (!_suppressedLogged)
            {
                _suppressedLogged = true;
                Util.Log.Info("[RoadImport] log limité à 50 routes. Analyse continue silencieusement.");
            }
        }

        internal static void SetCachedRoadLines(
            IList<GeoRoadLine> roadLines,
            GeoOverlayConfig config,
            double originLon,
            double originLat,
            string sourceKey)
        {
            _cachedRoadLines = roadLines ?? new List<GeoRoadLine>();
            _cachedConfig = config;
            _cachedOriginLon = originLon;
            _cachedOriginLat = originLat;
            _cachedSourceKey = sourceKey ?? "";

            Util.Log.Info(
                "[RoadImport] cached road lines. sourceKey=" + Safe(_cachedSourceKey) +
                ", config=" + (config != null) +
                ", enabled=" + (config != null && config.RuntimeRoadImportEnabled) +
                ", roadLines=" + _cachedRoadLines.Count +
                ", maxSegments=" + (config != null ? config.RuntimeRoadImportMaxSegments : 0) +
                ", runOnce=" + (config != null && config.RuntimeRoadImportRunOnce) +
                ", originLon=" + originLon.ToString("R", CultureInfo.InvariantCulture) +
                ", originLat=" + originLat.ToString("R", CultureInfo.InvariantCulture)
            );
        }

        internal static void QueueRoadLines(
            IList<GeoRoadLine> roadLines,
            GeoOverlayConfig config,
            double originLon,
            double originLat,
            string reason)
        {
            SetCachedRoadLines(roadLines, config, originLon, originLat, reason);
            Util.Log.Info("[RoadImport] QueueRoadLines compatibility path used: cache updated only, no CS2 roads created.");
        }

        internal static string GetCacheStatusText()
        {
            var count = _cachedRoadLines != null ? _cachedRoadLines.Count : 0;
            var source = Safe(_cachedSourceKey);
            var enabled = _cachedConfig != null && _cachedConfig.RuntimeRoadImportEnabled;

            return
                "Cache import routes: " + count +
                " ligne(s), source=" + source +
                ", enabled=" + enabled +
                ", origin=" + _cachedOriginLon.ToString("0.######", CultureInfo.InvariantCulture) +
                "/" + _cachedOriginLat.ToString("0.######", CultureInfo.InvariantCulture);
        }

        internal static string GetRuntimeImportHudText()
        {
            var config = _cachedConfig;
            var count = _cachedRoadLines != null ? _cachedRoadLines.Count : 0;

            if (config == null)
            {
                return
                    "Import runtime CS2 : config absente\n" +
                    "Progression : indisponible";
            }

            var progress = "Progression : tool indisponible";

            try
            {
                var world = World.DefaultGameObjectInjectionWorld;

                if (world != null)
                {
                    var tool = world.GetOrCreateSystemManaged<RuntimeRoadToolSystem>();

                    if (tool != null)
                        progress = tool.GetRuntimeImportProgressText();
                }
            }
            catch (Exception ex)
            {
                progress = "Progression : erreur HUD " + ex.GetType().Name;
            }

            return
                "Mode : " + Safe(config.RuntimeRoadImportSelectionMode) + "\n" +
                "Pipeline : " + Safe(config.RuntimeRoadImportPipelineMode) + "\n" +
                "Source : " + Safe(config.RuntimeRoadImportSourceFilter) + "\n" +
                "Highway : " + Safe(config.RuntimeRoadImportHighwayFilter) + "\n" +
                "Stage : " + Safe(config.RuntimeRoadImportStageFilter) + "\n" +
                "Max demandé : " + (config.RuntimeRoadImportMaxSegments <= 0 ? "illimite" : config.RuntimeRoadImportMaxSegments.ToString(CultureInfo.InvariantCulture)) + "\n" +
                "Cap vanilla transaction : " + VanillaSafeSingleApplyMaxSegments.ToString(CultureInfo.InvariantCulture) + "\n" +
                "Batch : " + config.RuntimeRoadImportBatchSize.ToString(CultureInfo.InvariantCulture) + "\n" +
                "Cache : " + count.ToString(CultureInfo.InvariantCulture) + " lignes\n" +
                progress;
        }

        internal static void ImportCachedRoadLinesNow()
        {
            var config = _cachedConfig;
            var roadLines = _cachedRoadLines;

            Util.Log.Info(
                "[RoadImport] manual import requested. sourceKey=" + Safe(_cachedSourceKey) +
                ", config=" + (config != null) +
                ", enabled=" + (config != null && config.RuntimeRoadImportEnabled) +
                ", roadLines=" + (roadLines != null ? roadLines.Count : 0)
            );

            if (config == null || !config.RuntimeRoadImportEnabled)
            {
                Util.Log.Info("[RoadImport] manual import blocked: runtimeRoadImportEnabled=false.");
                return;
            }

            if (roadLines == null || roadLines.Count == 0)
            {
                Util.Log.Info("[RoadImport] manual import blocked: no cached road lines available.");
                return;
            }

            int mapExtCoreValue;
            int mapExtMapSizeMeters;
            string mapExtMode;
            string mapExtReason;

            var mapExtDetected = MapExtInterop.TryGetMapExtState(
                out mapExtCoreValue,
                out mapExtMapSizeMeters,
                out mapExtMode,
                out mapExtReason
            );

            Util.Log.Info(
                "[RoadImport] MapExt state: detected=" + mapExtDetected +
                ", coreValue=" + mapExtCoreValue +
                ", mapSizeMeters=" + mapExtMapSizeMeters +
                ", mode=" + Safe(mapExtMode) +
                ", reason=" + Safe(mapExtReason)
            );

            if (!mapExtDetected || mapExtCoreValue < 4 || mapExtMapSizeMeters < 57344)
            {
                Util.Log.Info(
                    "[RoadImport] External MapExt not detected. Internal LargeMap is disabled/safe. Runtime import uses current game validation bounds."
                );
            }

            var importKey = BuildImportKey(roadLines, config, _cachedOriginLon, _cachedOriginLat, _cachedSourceKey);

            if (config.RuntimeRoadImportRunOnce && string.Equals(importKey, _lastImportKey, StringComparison.Ordinal))
            {
                Util.Log.Info("[RoadImport] already imported, use undo first. sourceKey=" + Safe(_cachedSourceKey));
                return;
            }

            var world = World.DefaultGameObjectInjectionWorld;

            if (world == null)
            {
                Util.Log.Info("[RoadImport] World null, impossible de créer les routes runtime.");
                return;
            }

            try
            {
                var entityManager = world.EntityManager;
                var prefabSystem = world.GetOrCreateSystemManaged<PrefabSystem>();
                var resolver = EnsureResolver(entityManager, prefabSystem);

                if (resolver == null || !resolver.TryInitialize())
                {
                    Util.Log.Error("[RoadImport] runtime import aborted: no usable CS2 road prefab found.");
                    return;
                }

                var tool = world.GetOrCreateSystemManaged<RuntimeRoadToolSystem>();
                tool.SetMaxRequestsPerFinalizeBatch(config.RuntimeRoadImportBatchSize);
                tool.SetRuntimeRoadImportPipelineMode(config.RuntimeRoadImportPipelineMode);
                tool.SetRuntimeRoadSnapTolerance(config.RuntimeRoadImportSnapToleranceMeters);

                var baseline = CaptureRoadEdgeEntityIndices(entityManager);

                if (baseline.Count >= VanillaSafeSessionEdgeRoadBudget)
                {
                    Util.Log.Info(
                        "[RoadImport] runtime import blocked: vanilla-safe session Edge/Road budget reached. " +
                        "baselineEdgeRoad=" + baseline.Count +
                        ", budget=" + VanillaSafeSessionEdgeRoadBudget +
                        ". Restart/reload a clean save or reduce imported layers."
                    );

                    return;
                }

                tool.PrepareTaggedImportBatch(baseline, importKey);

                var importer = new RuntimeRoadImportSession(
                    config,
                    resolver,
                    tool,
                    _cachedOriginLon,
                    _cachedOriginLat,
                    baseline.Count
                );
                var result = importer.Queue(roadLines);

                if (result.QueuedSegments > 0)
                {
                    _lastImportKey = importKey;
                }
                else
                {
                    tool.ClearPreparedTaggedImportBatch(importKey);
                }

                Util.Log.Info(
                    "[RoadImport] runtime import summary: sourceKey=" + Safe(_cachedSourceKey) +
                    ", roads=" + result.ProcessedRoads +
                    ", queuedSegments=" + result.QueuedSegments +
                    ", queuedInside14336=" + result.QueuedInside14336 +
                    ", deferredOutside14336Inside57344=" + result.DeferredOutside14336Inside57344 +
                    ", skippedOutside57344=" + result.SkippedOutside57344 +
                    ", requestedMaxSegments=" + result.RequestedMaxSegments +
                    ", effectiveMaxSegments=" + result.EffectiveMaxSegments +
                    ", cappedByVanillaSingleApplyLimit=" + result.CappedByVanillaSingleApplyLimit +
                    ", baselineEdgeRoadBeforeImport=" + result.BaselineEdgeRoadBeforeImport +
                    ", vanillaSafeSessionEdgeRoadBudget=" + result.VanillaSafeSessionEdgeRoadBudget +
                    ", cappedByVanillaSessionBudget=" + result.CappedByVanillaSessionBudget +
                    ", skippedPath=" + result.SkippedPath +
                    ", skippedSourceFilter=" + result.SkippedSourceFilter +
                    ", skippedHighwayFilter=" + result.SkippedHighwayFilter +
                    ", skippedStageFilter=" + result.SkippedStageFilter +
                    ", skippedBridgeTunnel=" + result.SkippedBridgeTunnel +
                    ", skippedShort=" + result.SkippedShort +
                    ", skippedDuplicate=" + result.SkippedDuplicate +
                    ", skippedNoPrefab=" + result.SkippedNoPrefab +
                    ", skippedParkingAisle=" + result.SkippedParkingAisle +
                    ", skippedUnpaved=" + result.SkippedUnpaved +
                    ", terrainHits=" + result.TerrainHits +
                    ", terrainMisses=" + result.TerrainMisses +
                    ", maxSegments=" + config.RuntimeRoadImportMaxSegments +
                    ", batchSize=" + config.RuntimeRoadImportBatchSize +
                    ", pipelineMode=" + Safe(config.RuntimeRoadImportPipelineMode) +
                    ", baselineEdgeRoad=" + baseline.Count
                );
            }
            catch (Exception ex)
            {
                Util.Log.Error("[RoadImport] runtime import failed: " + ex);
            }
        }


        private static string BuildImportKey(IList<GeoRoadLine> roadLines, GeoOverlayConfig config, double originLon, double originLat, string sourceKey)
        {
            return
                (sourceKey ?? "") + "|" +
                (config.ActiveBundleId ?? "") + "|" +
                (config.RoadGeometrySource ?? "") + "|" +
                roadLines.Count + "|" +
                originLon.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "|" +
                originLat.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "|" +
                config.WorldOriginX.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "|" +
                config.WorldOriginZ.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "|" +
                config.WorldScale.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "|" +
                config.OverlayRotationDegrees.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "|" +
                config.OverlayScaleX.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "|" +
                config.OverlayScaleZ.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "|" +
                config.FlipX + "|" +
                config.FlipZ + "|" +
                config.RuntimeRoadImportMaxSegments + "|" +
                config.RuntimeRoadImportStride + "|" +
                config.RuntimeRoadImportMinSegmentLengthMeters.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "|" +
                config.RuntimeRoadImportSnapToleranceMeters.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "|" +
                config.RuntimeRoadImportIncludeBridgeTunnel + "|" +
                (config.RuntimeRoadImportSelectionMode ?? "") + "|" +
                config.RuntimeRoadImportDistanceBucketMeters.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "|" +
                config.RuntimeRoadImportPriorityWeight.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "|" +
                config.RuntimeRoadImportSkipParkingAisles + "|" +
                config.RuntimeRoadImportSkipClearlyUnpaved + "|" +
                (config.RuntimeRoadImportSourceFilter ?? "") + "|" +
                (config.RuntimeRoadImportHighwayFilter ?? "") + "|" +
                (config.RuntimeRoadImportStageFilter ?? "") + "|" +
                config.RuntimeRoadImportBatchSize + "|" +
                (config.RuntimeRoadImportPipelineMode ?? "");
        }

        private static string Safe(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "(empty)" : value.Replace("\r", " ").Replace("\n", " ");
        }

        private sealed partial class RuntimeRoadImportSession
        {
            private readonly GeoOverlayConfig _config;
            private readonly RoadPrefabResolver _resolver;
            private readonly RuntimeRoadToolSystem _tool;
            private readonly double _originLon;
            private readonly double _originLat;
            private readonly int _baselineEdgeRoadCount;
            private readonly HashSet<string> _segmentKeys = new HashSet<string>();
            private const float VanillaPlayableHalfSizeMeters = 7168f;
            private const float WorldMapHalfSizeMeters = 28672f;
            private int _selectionLogs;
            private int _segmentLogs;
            private int _selectionSkippedPath;
            private int _selectionSkippedSourceFilter;
            private int _selectionSkippedHighwayFilter;
            private int _selectionSkippedStageFilter;
            private int _selectionSkippedBridgeTunnel;
            private int _selectionSkippedParkingAisle;
            private int _selectionSkippedUnpaved;

            private string SelectionMode
            {
                get
                {
                    return string.IsNullOrWhiteSpace(_config.RuntimeRoadImportSelectionMode)
                        ? "near-center-priority"
                        : _config.RuntimeRoadImportSelectionMode.Trim().ToLowerInvariant();
                }
            }

            internal RuntimeRoadImportSession(
                GeoOverlayConfig config,
                RoadPrefabResolver resolver,
                RuntimeRoadToolSystem tool,
                double originLon,
                double originLat,
                int baselineEdgeRoadCount)
            {
                _config = config;
                _resolver = resolver;
                _tool = tool;
                _originLon = originLon;
                _originLat = originLat;
                _baselineEdgeRoadCount = Math.Max(0, baselineEdgeRoadCount);
            }

        }

    }
}
