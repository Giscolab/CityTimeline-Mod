using System;
using System.Collections.Generic;
using CityTimelineMod.Rendering.Bounds;
using CityTimelineMod.Rendering.Core;
using CityTimelineMod.Rendering.Roads;
using CityTimelineMod.Rendering.Services;
using CityTimelineMod.Util;
using UnityEngine;

namespace CityTimelineMod.Rendering
{
    internal sealed partial class GroundOverlayBehaviour
    {
        private void CreateOverlayWithTiming(string reason)
        {
            if (_config != null && _config.EnableProgressiveOverlayRebuild)
            {
                StartProgressiveOverlayRebuild(reason);
                return;
            }

            if (_progressiveRebuild != null && _progressiveRebuild.IsActive)
                CancelProgressiveOverlayRebuild("synchronous rebuild", true);

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            PrepareRoadPrefabWidthResolutionForBuild();

            Log.Info(
                "GroundOverlay: overlay rebuild started. reason=" + SafeLogValue(reason) +
                ", renderEverything=" + (_config != null && _config.RenderEverything) +
                ", roadRenderMode=" + (_config != null ? _config.RoadRenderMode : "(null)") +
                ", pathRenderMode=" + (_config != null ? _config.PathRenderMode : "(null)") +
                ", labels=" + (_config != null && _config.RenderRoadLabels) +
                ", arrows=" + (_config != null && _config.RenderRoadDirectionArrows)
            );

            try
            {
                CreateOverlay();
            }
            finally
            {
                stopwatch.Stop();
                Log.Info(
                    "GroundOverlay: overlay rebuild finished. reason=" + SafeLogValue(reason) +
                    ", elapsedMs=" + stopwatch.ElapsedMilliseconds
                );
            }
        }

        private void RequestOverlayRebuild(string reason, bool cancelCurrent)
        {
            if (_config == null)
                return;

            var safeReason = SafeLogValue(reason);

            if (!_config.EnableProgressiveOverlayRebuild)
            {
                Log.Info(
                    "GroundOverlay: rebuild request executing synchronously. reason=" + safeReason +
                    ", cancelCurrent=" + cancelCurrent
                );
                RebuildOverlayPreservingLiveTransform();
                return;
            }

            var active = _progressiveRebuild != null && _progressiveRebuild.IsActive;
            var hadPending = !string.IsNullOrWhiteSpace(_pendingRebuildReason);

            if (hadPending)
            {
                Log.Info(
                    "GroundOverlay: rebuild request debounced. previousReason=" + SafeLogValue(_pendingRebuildReason) +
                    ", newReason=" + safeReason
                );
            }

            _pendingRebuildReason = safeReason;
            _pendingRebuildRequestTime = Time.realtimeSinceStartup;

            if (active)
            {
                _rebuildRestartPending = true;

                if (cancelCurrent)
                    _rebuildCancelRequested = true;
            }

            Log.Info(
                "GroundOverlay: rebuild request queued. reason=" + safeReason +
                ", active=" + active +
                ", restartPending=" + _rebuildRestartPending +
                ", cancelRequested=" + _rebuildCancelRequested
            );
        }

        private void UpdatePendingOverlayRebuildRequests()
        {
            if (_config == null || string.IsNullOrWhiteSpace(_pendingRebuildReason))
                return;

            if (_progressiveRebuild != null && _progressiveRebuild.IsActive)
                return;

            var elapsed = Time.realtimeSinceStartup - _pendingRebuildRequestTime;

            if (elapsed < RebuildDebounceSeconds)
                return;

            var reason = _pendingRebuildReason;
            _pendingRebuildReason = null;
            _pendingRebuildRequestTime = 0f;
            _rebuildRestartPending = false;
            _rebuildCancelRequested = false;

            Log.Info("GroundOverlay: pending rebuild starting. reason=" + SafeLogValue(reason));
            RebuildOverlayPreservingLiveTransform();
        }

private void StartProgressiveOverlayRebuild(string reason)
{
    if (_config == null)
        return;

    if (_progressiveRebuild != null && _progressiveRebuild.IsActive)
    {
        _rebuildRestartPending = true;
        _pendingRebuildReason = SafeLogValue(reason);
        _pendingRebuildRequestTime = Time.realtimeSinceStartup;
        Log.Info(
            "GroundOverlay: progressive rebuild already active; restart queued. reason=" + _pendingRebuildReason +
            ", restartPending=" + _rebuildRestartPending +
            ", cancelRequested=" + _rebuildCancelRequested
        );
        return;
    }

    var startupTiming = System.Diagnostics.Stopwatch.StartNew();

    PrepareRoadPrefabWidthResolutionForBuild();

    Log.Info(
        "GroundOverlay startup timing: prefabResolutionMs=" +
        startupTiming.ElapsedMilliseconds
    );
    startupTiming.Restart();

    var state = new ProgressiveOverlayRebuildState();
    state.Reason = SafeLogValue(reason);
    state.Stopwatch = System.Diagnostics.Stopwatch.StartNew();
    state.Phase = OverlayRebuildPhase.Clearing;
    state.PreviousChildCount = CountVisibleOverlayChildren();
    state.PreviousMaterials.AddRange(_ownedOverlayMaterials);

    var stagingRoot = new GameObject("GroundOverlay_Staging");
    stagingRoot.name += "_" + stagingRoot.GetInstanceID();
    stagingRoot.transform.SetParent(transform, false);
    stagingRoot.SetActive(false);
    state.StagingRoot = stagingRoot;
    _overlayRenderParent = stagingRoot.transform;
    _progressiveRebuild = state;

    Log.Info(
        "GroundOverlay: rebuild staging started. reason=" + state.Reason +
        ", previousChildren=" + state.PreviousChildCount +
        ", previousMaterials=" + state.PreviousMaterials.Count
    );

    Log.Info(
        "GroundOverlay: progressive overlay rebuild started. reason=" + state.Reason +
        ", renderEverything=" + _config.RenderEverything +
        ", renderAllRoadSegments=" + ShouldRenderAllRoadSegments() +
        ", renderAllPathSegments=" + ShouldRenderAllPathSegments() +
        ", roadRenderMode=" + _config.RoadRenderMode +
        ", pathRenderMode=" + _config.PathRenderMode +
        ", labels=" + _config.RenderRoadLabels +
        ", arrows=" + _config.RenderRoadDirectionArrows +
        ", semanticFilter=" + _config.RoadSemanticFilterMode +
        ", spatialChunking=" + _config.EnableRoadSpatialChunking +
        ", roadChunkSizeMeters=" + _config.RoadChunkSizeMeters +
        ", roadChunksPerFrame=" + _config.RoadChunksPerFrame +
        ", pathChunksPerFrame=" + _config.PathChunksPerFrame +
        ", renderRailways=" + _config.RenderRailways +
        ", railwaySpatialChunking=" + _config.EnableRailwaySpatialChunking +
        ", railwayChunkSizeMeters=" + _config.RailwayChunkSizeMeters +
        ", railwayChunksPerFrame=" + _config.RailwayChunksPerFrame +
        ", renderServices=" + _config.RenderServices +
        ", serviceChunkSizeMeters=" + _config.ServiceChunkSizeMeters +
        ", serviceChunksPerFrame=" + _config.ServiceChunksPerFrame
    );

    if (_waterLines == null || _waterLines.Count == 0)
    {
        Log.Info("GroundOverlay: no water geometry; continuing rebuild for bounds/zoning/roads/paths.");
    }

    state.OriginLon = _config.UseGeoJsonCenter ? _bounds.CenterLon : _config.OriginLon;
    state.OriginLat = _config.UseGeoJsonCenter ? _bounds.CenterLat : _config.OriginLat;

    state.Stride = Math.Max(1, _config.PointStride);
    state.Materials = CreateOverlayRenderMaterials();

    ApplyCurrentOverlayVisibilityToMaterials();
    MapBoundsRenderer.Render(
        _config,
        OverlayRenderParent,
        state.Materials.WorldMapBounds,
        state.Materials.HeightMapBounds,
        state.Materials.MapCenter,
        state.OriginLon,
        state.OriginLat,
        ResolveY,
        LogVerboseOverlay
    );

    Log.Info(
        "GroundOverlay startup timing: materialsAndBoundsMs=" +
        startupTiming.ElapsedMilliseconds
    );
    startupTiming.Restart();

    // Le zoning est maintenant progressif : on initialise la phase sans appeler
    // RenderZoningFillMeshes ici.
    state.Phase = OverlayRebuildPhase.Zoning;

    // (Log temporel du zoning supprimé)

    state.Phase = OverlayRebuildPhase.Water;
    RenderWaterOverlayGroups(
        state.Materials.Cyan,
        state.Materials.WaterAreaBlue,
        state.Materials.WaterAreaFillBlue,
        state.Stride,
        ref state.CreatedWaterSegments,
        ref state.CreatedWaterLines,
        ref state.FirstWorld,
        ref state.LastWorld,
        ref state.HasEndpoints
    );

    Log.Info(
        "GroundOverlay startup timing: waterMs=" +
        startupTiming.ElapsedMilliseconds
    );
    startupTiming.Restart();

    CopyServiceLoadCounters(state.ServiceCounters);

    state.ServiceChunks =
        BuildServiceRenderChunks(
            state.OriginLon,
            state.OriginLat,
            state.ServiceCounters
        );

    Log.Info(
        "GroundOverlay startup timing: serviceChunkingMs=" +
        startupTiming.ElapsedMilliseconds
    );
    startupTiming.Restart();

    state.RailwayChunks =
        BuildRailwayRenderChunks(
            state.OriginLon,
            state.OriginLat
        );

    Log.Info(
        "GroundOverlay startup timing: railwayChunkingMs=" +
        startupTiming.ElapsedMilliseconds
    );
    startupTiming.Restart();

    state.RoadChunks =
        BuildRoadRenderChunks(
            false,
            state.OriginLon,
            state.OriginLat
        );

    Log.Info(
        "GroundOverlay startup timing: roadChunkingMs=" +
        startupTiming.ElapsedMilliseconds
    );
    startupTiming.Restart();

    state.PathChunks =
        BuildRoadRenderChunks(
            true,
            state.OriginLon,
            state.OriginLat
        );

    Log.Info(
        "GroundOverlay startup timing: pathChunkingMs=" +
        startupTiming.ElapsedMilliseconds
    );
    startupTiming.Restart();

    Log.Info(
        "GroundOverlay: service chunking summary: chunks=" + state.ServiceChunks.Count +
        ", sourcePoints=" + (_servicePoints != null ? _servicePoints.Count : 0) +
        ", chunkSizeMeters=" + _config.ServiceChunkSizeMeters +
        ", spatialChunking=" + _config.EnableServiceSpatialChunking
    );

    Log.Info(
        "GroundOverlay: railway chunking summary: chunks=" + state.RailwayChunks.Count +
        ", sourceLines=" + (_railwayLines != null ? _railwayLines.Count : 0) +
        ", chunkSizeMeters=" + _config.RailwayChunkSizeMeters +
        ", spatialChunking=" + _config.EnableRailwaySpatialChunking
    );

    var assignedRoadLines = CountRoadChunkLines(state.RoadChunks);
    var assignedPathLines = CountRoadChunkLines(state.PathChunks);
    var validRoadLines = CountValidRoadLines(false);
    var validPathLines = CountValidRoadLines(true);

    Log.Info(
        "GroundOverlay: road/path chunking summary: roadChunks=" + state.RoadChunks.Count +
        ", roadLinesAssigned=" + assignedRoadLines +
        ", validRoadLines=" + validRoadLines +
        ", unassignedRoadLines=" + Math.Max(0, validRoadLines - assignedRoadLines) +
        ", pathChunks=" + state.PathChunks.Count +
        ", pathLinesAssigned=" + assignedPathLines +
        ", validPathLines=" + validPathLines +
        ", unassignedPathLines=" + Math.Max(0, validPathLines - assignedPathLines) +
        ", chunkSizeMeters=" + _config.RoadChunkSizeMeters +
        ", spatialChunking=" + _config.EnableRoadSpatialChunking
    );

    if (state.RoadChunks.Count == 0 && state.PathChunks.Count == 0)
        LogRoadRenderCounters(state.RoadCounters);

    // On force la phase Zoning pour que le traitement progressif commence immédiatement.
    state.Phase = OverlayRebuildPhase.Zoning;

    LogProgressiveOverlayProgress(true);
}

private void UpdateProgressiveOverlayRebuild()
{
    var state = _progressiveRebuild;

    if (state == null || !state.IsActive)
        return;

    if (_rebuildCancelRequested)
    {
        _rebuildCancelRequested = false;
        Log.Info(
            "GroundOverlay: active progressive rebuild cancellation requested. pendingReason=" +
            SafeLogValue(_pendingRebuildReason)
        );
        CancelProgressiveOverlayRebuild("cancel requested", true);
        return;
    }

    try
    {
        if (state.Phase == OverlayRebuildPhase.Zoning)
        {
            // Budget CPU volontairement faible afin de conserver
            // une frame Unity réactive pendant les ~53k polygones.
            if (!BuildProgressiveZoningMeshes(state, 256))
                return;

            state.Phase = state.ServiceChunks.Count > 0
                ? OverlayRebuildPhase.ServiceChunks
                : (state.RailwayChunks.Count > 0
                    ? OverlayRebuildPhase.RailwayChunks
                    : (state.RoadChunks.Count > 0
                        ? OverlayRebuildPhase.RoadChunks
                        : (state.PathChunks.Count > 0
                            ? OverlayRebuildPhase.PathChunks
                            : OverlayRebuildPhase.Arrows)));

            LogProgressiveOverlayProgress(true);
            return;
        }

        if (state.Phase == OverlayRebuildPhase.ServiceChunks)
        {
            BuildProgressiveServiceChunks(state, Mathf.Clamp(_config.ServiceChunksPerFrame, 1, 64));

            if (state.ServiceChunkIndex >= state.ServiceChunks.Count)
            {
                _lastServiceRenderCounters = state.ServiceCounters.Copy();
                LogServiceRenderCounters(state.ServiceCounters, state.ServiceChunks.Count);
                state.Phase = state.RailwayChunks.Count > 0
                    ? OverlayRebuildPhase.RailwayChunks
                    : (state.RoadChunks.Count > 0
                        ? OverlayRebuildPhase.RoadChunks
                        : (state.PathChunks.Count > 0 ? OverlayRebuildPhase.PathChunks : OverlayRebuildPhase.Arrows));
                LogProgressiveOverlayProgress(true);
            }

            return;
        }

        if (state.Phase == OverlayRebuildPhase.RailwayChunks)
        {
            BuildProgressiveRailwayChunks(state, Mathf.Clamp(_config.RailwayChunksPerFrame, 1, 64));

            if (state.RailwayChunkIndex >= state.RailwayChunks.Count)
            {
                _lastRailwayRenderCounters = state.RailwayCounters.Copy();
                LogRailwayRenderCounters(state.RailwayCounters, state.RailwayChunks.Count);
                state.Phase = state.RoadChunks.Count > 0
                    ? OverlayRebuildPhase.RoadChunks
                    : (state.PathChunks.Count > 0 ? OverlayRebuildPhase.PathChunks : OverlayRebuildPhase.Arrows);
                LogProgressiveOverlayProgress(true);
            }

            return;
        }

        if (state.Phase == OverlayRebuildPhase.RoadChunks)
        {
            BuildProgressiveRoadChunks(state, false, Mathf.Clamp(_config.RoadChunksPerFrame, 1, 64));

            if (state.RoadChunkIndex >= state.RoadChunks.Count)
            {
                if (state.PathChunks.Count == 0)
                    LogRoadRenderCounters(state.RoadCounters);

                state.Phase = state.PathChunks.Count > 0 ? OverlayRebuildPhase.PathChunks : OverlayRebuildPhase.Arrows;
                LogProgressiveOverlayProgress(true);
            }

            return;
        }

        if (state.Phase == OverlayRebuildPhase.PathChunks)
        {
            BuildProgressiveRoadChunks(state, true, Mathf.Clamp(_config.PathChunksPerFrame, 1, 64));

            if (state.PathChunkIndex >= state.PathChunks.Count)
            {
                LogRoadRenderCounters(state.RoadCounters);
                state.Phase = OverlayRebuildPhase.Arrows;
                LogProgressiveOverlayProgress(true);
            }

            return;
        }

        if (state.Phase == OverlayRebuildPhase.Arrows)
        {
            state.CreatedRoadArrows = RenderRoadDirectionArrows(state.Materials.RoadArrow, state.Stride);
            state.Phase = OverlayRebuildPhase.Labels;
            LogProgressiveOverlayProgress(true);
            return;
        }

        if (state.Phase == OverlayRebuildPhase.Labels)
        {
            state.CreatedRoadLabels = RenderRoadLabels(state.Materials.RoadLabel, state.Stride);
            FinishProgressiveOverlayRebuild();
        }
    }
    catch (Exception ex)
    {
        Log.Error("GroundOverlay: progressive overlay rebuild failed. " + ex);
        var previousOverlayRetained = state.PreviousChildCount > 0;
        CancelProgressiveOverlayRebuild("error", false);

        if (previousOverlayRetained)
        {
            Log.Info(
                "GroundOverlay: failed staging discarded; previous overlay retained."
            );
            return;
        }

        _created = false;
        GeoBundleBootstrap.Reset();
        GeoDebugOverlay.Uninstall();
    }
}

        private void BuildProgressiveRoadChunks(ProgressiveOverlayRebuildState state, bool isPath, int chunksPerFrame)
        {
            var chunks = isPath ? state.PathChunks : state.RoadChunks;
            var builtThisFrame = 0;

            while (builtThisFrame < chunksPerFrame)
            {
                var index = isPath ? state.PathChunkIndex : state.RoadChunkIndex;

                if (index >= chunks.Count)
                    break;

                RenderRoadChunk(chunks[index], state.Materials, state.Stride, state.RoadCounters);

                if (isPath)
                    state.PathChunkIndex++;
                else
                    state.RoadChunkIndex++;

                builtThisFrame++;
            }

            LogProgressiveOverlayProgress(false);
        }

        private void BuildProgressiveRailwayChunks(ProgressiveOverlayRebuildState state, int chunksPerFrame)
        {
            var builtThisFrame = 0;

            while (builtThisFrame < chunksPerFrame && state.RailwayChunkIndex < state.RailwayChunks.Count)
            {
                RenderRailwayChunk(
                    state.RailwayChunks[state.RailwayChunkIndex],
                    state.Materials,
                    state.Stride,
                    state.RailwayCounters,
                    state.OriginLon,
                    state.OriginLat
                );
                state.RailwayChunkIndex++;
                builtThisFrame++;
            }

            LogProgressiveOverlayProgress(false);
        }

        private void BuildProgressiveServiceChunks(ProgressiveOverlayRebuildState state, int chunksPerFrame)
        {
            var builtThisFrame = 0;

            while (builtThisFrame < chunksPerFrame && state.ServiceChunkIndex < state.ServiceChunks.Count)
            {
                RenderServiceChunk(
                    state.ServiceChunks[state.ServiceChunkIndex],
                    state.Materials,
                    state.ServiceCounters
                );
                state.ServiceChunkIndex++;
                builtThisFrame++;
            }

            LogProgressiveOverlayProgress(false);
        }

        private void LogProgressiveOverlayProgress(bool force)
        {
            var state = _progressiveRebuild;

            if (state == null)
                return;

            var elapsedMs = state.Stopwatch != null ? state.Stopwatch.ElapsedMilliseconds : 0L;
            var currentIndex = state.Phase == OverlayRebuildPhase.ServiceChunks
                ? state.ServiceChunkIndex
                : (state.Phase == OverlayRebuildPhase.RailwayChunks
                    ? state.RailwayChunkIndex
                    : (state.Phase == OverlayRebuildPhase.PathChunks ? state.PathChunkIndex : state.RoadChunkIndex));
            var shouldLog = force ||
                elapsedMs - state.LastProgressLogMs >= 3000L ||
                (currentIndex > 0 && currentIndex % 25 == 0);

            if (!shouldLog)
                return;

            state.LastProgressLogMs = elapsedMs;

            Log.Info(
                "GroundOverlay: progressive rebuild progress: phase=" + state.Phase +
                ", serviceChunks=" + state.ServiceChunkIndex + "/" + state.ServiceChunks.Count +
                ", servicePoints=" + state.ServiceCounters.CreatedPoints +
                ", railwayChunks=" + state.RailwayChunkIndex + "/" + state.RailwayChunks.Count +
                ", railwaySegments=" + state.RailwayCounters.CreatedSegments +
                ", roadChunks=" + state.RoadChunkIndex + "/" + state.RoadChunks.Count +
                ", pathChunks=" + state.PathChunkIndex + "/" + state.PathChunks.Count +
                ", roadSegments=" + state.RoadCounters.CreatedRoadSegments +
                ", pathSegments=" + state.RoadCounters.CreatedPathSegments +
                ", elapsedMs=" + elapsedMs
            );
        }

        private void FinishProgressiveOverlayRebuild()
        {
            var state = _progressiveRebuild;

            if (state == null)
                return;

            FinalizeOverlayDebugMarkers(state);
            CommitStagedOverlay(state);
            state.Phase = OverlayRebuildPhase.Done;

            if (state.Stopwatch != null)
                state.Stopwatch.Stop();

            LogVerboseOverlay(
                "GroundOverlay: created water lines=" + state.CreatedWaterLines +
                ", water batched segments=" + state.CreatedWaterSegments +
                ", service points=" + state.ServiceCounters.CreatedPoints +
                ", railway lines=" + state.RailwayCounters.CreatedLines +
                ", railway batched segments=" + state.RailwayCounters.CreatedSegments +
                ", road lines=" + state.RoadCounters.CreatedRoadLines +
                ", path lines=" + state.RoadCounters.CreatedPathLines +
                ", road/path batched segments=" + (state.RoadCounters.CreatedRoadSegments + state.RoadCounters.CreatedPathSegments) +
                ", path batched segments=" + state.RoadCounters.CreatedPathSegments +
                ", one-way arrows=" + state.CreatedRoadArrows +
                ", road labels=" + state.CreatedRoadLabels
            );

            LogRoadHighwaySummary();
            LogRoadSemanticSummary();
            LogRenderedAndSkippedRoadHighwaySummary();
            LogRoadTierFilterSummary(state.RoadCounters.CreatedRoadLines + state.RoadCounters.CreatedPathLines);

            Log.Info(
                "GroundOverlay: progressive overlay rebuild finished. reason=" + state.Reason +
                ", phase=" + state.Phase +
                ", serviceChunks=" + state.ServiceChunkIndex + "/" + state.ServiceChunks.Count +
                ", servicePoints=" + state.ServiceCounters.CreatedPoints +
                ", railwayChunks=" + state.RailwayChunkIndex + "/" + state.RailwayChunks.Count +
                ", railwaySegments=" + state.RailwayCounters.CreatedSegments +
                ", roadChunks=" + state.RoadChunkIndex + "/" + state.RoadChunks.Count +
                ", pathChunks=" + state.PathChunkIndex + "/" + state.PathChunks.Count +
                ", roadSegments=" + state.RoadCounters.CreatedRoadSegments +
                ", pathSegments=" + state.RoadCounters.CreatedPathSegments +
                ", elapsedMs=" + (state.Stopwatch != null ? state.Stopwatch.ElapsedMilliseconds : 0L)
            );

            if (_rebuildRestartPending)
            {
                _rebuildRestartPending = false;

                if (string.IsNullOrWhiteSpace(_pendingRebuildReason))
                    _pendingRebuildReason = "progressive rebuild restart";

                _pendingRebuildRequestTime = Time.realtimeSinceStartup;
                Log.Info(
                    "GroundOverlay: pending restart queued after progressive rebuild completion. reason=" +
                    SafeLogValue(_pendingRebuildReason)
                );
            }
        }

        private void CancelProgressiveOverlayRebuild(string reason, bool log)
        {
            var state = _progressiveRebuild;

            if (state == null || !state.IsActive)
                return;

            if (state.Stopwatch != null)
                state.Stopwatch.Stop();

            _overlayRenderParent = null;

            try
            {
                var previousMaterialIds = new HashSet<int>();
                for (var i = 0; i < state.PreviousMaterials.Count; i++)
                {
                    var material = state.PreviousMaterials[i];
                    if (material != null)
                        previousMaterialIds.Add(material.GetInstanceID());
                }

                var stagedMaterials = new List<Material>();
                for (var i = 0; i < _ownedOverlayMaterials.Count; i++)
                {
                    var material = _ownedOverlayMaterials[i];
                    if (material != null && !previousMaterialIds.Contains(material.GetInstanceID()))
                        stagedMaterials.Add(material);
                }

                ReleaseOverlayMaterials(stagedMaterials);
            }
            catch (Exception ex)
            {
                Log.Error("GroundOverlay: staged material cancellation cleanup failed. " + ex);
            }

            try
            {
                if (state.StagingRoot != null)
                {
                    state.StagingRoot.SetActive(false);
                    UnityEngine.Object.Destroy(state.StagingRoot);
                    state.StagingRoot = null;
                }
            }
            catch (Exception ex)
            {
                Log.Error("GroundOverlay: staged child cancellation cleanup failed. " + ex);
            }

            PruneDestroyedRoadLabelMeshes();

            state.Phase = OverlayRebuildPhase.None;

            if (log)
            {
                Log.Info(
                    "GroundOverlay: progressive overlay rebuild cancelled. reason=" + SafeLogValue(reason) +
                    ", serviceChunks=" + state.ServiceChunkIndex + "/" + state.ServiceChunks.Count +
                    ", servicePoints=" + state.ServiceCounters.CreatedPoints +
                    ", railwayChunks=" + state.RailwayChunkIndex + "/" + state.RailwayChunks.Count +
                    ", railwaySegments=" + state.RailwayCounters.CreatedSegments +
                    ", roadChunks=" + state.RoadChunkIndex + "/" + state.RoadChunks.Count +
                    ", pathChunks=" + state.PathChunkIndex + "/" + state.PathChunks.Count +
                    ", roadSegments=" + state.RoadCounters.CreatedRoadSegments +
                    ", pathSegments=" + state.RoadCounters.CreatedPathSegments
                );
            }
        }

        private void CommitStagedOverlay(ProgressiveOverlayRebuildState state)
        {
            if (state == null || state.StagingRoot == null)
                throw new InvalidOperationException("The progressive overlay staging root is unavailable.");

            var stagingRoot = state.StagingRoot;
            var stagingTransform = stagingRoot.transform;
            var previousChildrenReleased = true;
            var previousMaterialsReleased = true;

            _overlayRenderParent = null;

            // Activating the completed staging container before disabling the
            // previous content prevents a deliberately empty rendered frame.
            stagingRoot.SetActive(true);

            try
            {
                ClearOverlayChildrenExcept(stagingRoot);
            }
            catch (Exception ex)
            {
                previousChildrenReleased = false;
                Log.Error(
                    "GroundOverlay: previous overlay child release was incomplete after staged commit. " + ex
                );
            }

            try
            {
                ReleaseOverlayMaterials(state.PreviousMaterials);
            }
            catch (Exception ex)
            {
                previousMaterialsReleased = false;
                Log.Error(
                    "GroundOverlay: previous overlay material release was incomplete after staged commit. " + ex
                );
            }

            PruneDestroyedRoadLabelMeshes();
            state.StagingRoot = null;

            Log.Info(
                "GroundOverlay: staged chunk swap complete. serviceChunks=" +
                state.ServiceChunkIndex + "/" + state.ServiceChunks.Count +
                ", railwayChunks=" + state.RailwayChunkIndex + "/" + state.RailwayChunks.Count +
                ", roadChunks=" + state.RoadChunkIndex + "/" + state.RoadChunks.Count +
                ", pathChunks=" + state.PathChunkIndex + "/" + state.PathChunks.Count
            );
            Log.Info(
                "GroundOverlay: staged overlay committed. reason=" + state.Reason +
                ", stagedChildren=" + stagingTransform.childCount
            );

            if (previousChildrenReleased && previousMaterialsReleased)
            {
                Log.Info(
                    "GroundOverlay: previous overlay released. children=" +
                    state.PreviousChildCount +
                    ", materials=" + state.PreviousMaterials.Count
                );
            }
        }

        private int CountVisibleOverlayChildren()
        {
            var count = 0;

            for (var i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (child != null && child.gameObject.activeSelf)
                    count++;
            }

            return count;
        }

        private void PruneDestroyedRoadLabelMeshes()
        {
            for (var i = _roadLabelMeshes.Count - 1; i >= 0; i--)
            {
                if (_roadLabelMeshes[i] == null)
                    _roadLabelMeshes.RemoveAt(i);
            }
        }

        private void FinalizeOverlayDebugMarkers(ProgressiveOverlayRebuildState state)
        {
            var center = new Vector3(
                _config.WorldOriginX,
                ResolveY(new Vector3(_config.WorldOriginX, 0f, _config.WorldOriginZ)) + _config.GroundMargin,
                _config.WorldOriginZ
            );

            if (_config.DebugBeacons)
            {
                CreateBeacon("ground_red_center_beacon", center, state.Materials.DebugRed);

                if (state.HasEndpoints)
                {
                    CreateBeacon("ground_green_first_beacon", state.FirstWorld, state.Materials.DebugGreen);
                    CreateBeacon("ground_yellow_last_beacon", state.LastWorld, state.Materials.DebugYellow);
                }
            }

            if (_config.DebugTower)
                CreateTower("ground_magenta_debug_tower", center, state.Materials.DebugMagenta);

            LogVerboseOverlay("GroundOverlay: first world point=" + state.FirstWorld);
            LogVerboseOverlay("GroundOverlay: last world point=" + state.LastWorld);
            LogVerboseOverlay("GroundOverlay: center world point=" + center);
        }

        private void RebuildOverlayPreservingLiveTransform()
        {
            var livePosition = transform.position;
            var liveRotation = transform.rotation;
            var liveScale = transform.localScale;

            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one;

            if (_config == null || !_config.EnableProgressiveOverlayRebuild)
                ClearOverlayChildren();

            CreateOverlayWithTiming("rebuild-preserve-transform");

            transform.position = livePosition;
            transform.rotation = liveRotation;
            transform.localScale = liveScale;

            Log.Info("GroundOverlay calibration: overlay rebuilt. mode=" + GetCalibrationModeLabel(_activeCalibrationMode));
        }
    }
}