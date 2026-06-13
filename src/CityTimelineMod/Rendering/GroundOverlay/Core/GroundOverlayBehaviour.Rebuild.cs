using System;
using System.Collections.Generic;
using CityTimelineMod.Rendering.Bounds;
using CityTimelineMod.Rendering.Core;
using CityTimelineMod.Rendering.Roads;
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

            var state = new ProgressiveOverlayRebuildState();
            state.Reason = SafeLogValue(reason);
            state.Stopwatch = System.Diagnostics.Stopwatch.StartNew();
            state.Phase = OverlayRebuildPhase.Clearing;
            _progressiveRebuild = state;

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
                ", pathChunksPerFrame=" + _config.PathChunksPerFrame
            );

            ClearOverlayChildren();
            ClearOverlayMaterialRegistries();

            if (_waterLines == null || _waterLines.Count == 0)
            {
                Log.Info("GroundOverlay: no water geometry; continuing rebuild for bounds/zoning/roads/paths.");
            }

            state.OriginLon = _config.UseGeoJsonCenter ? _bounds.CenterLon : _config.OriginLon;
            state.OriginLat = _config.UseGeoJsonCenter ? _bounds.CenterLat : _config.OriginLat;

            var runtimeImportCenter = new Vector3(
                _config.WorldOriginX,
                ResolveY(new Vector3(_config.WorldOriginX, 0f, _config.WorldOriginZ)) + _config.GroundMargin,
                _config.WorldOriginZ
            );

            CityTimelineMod.Roads.GeoRoadImportPlacement.CenterX = runtimeImportCenter.x;
            CityTimelineMod.Roads.GeoRoadImportPlacement.CenterZ = runtimeImportCenter.z;
            CityTimelineMod.Roads.GeoRoadImportPlacement.GroundY = runtimeImportCenter.y;
            LogVerboseOverlay("GroundOverlay: runtime road import cache is managed by bundle load; overlay rebuild does not create CS2 roads.");

            state.Stride = Math.Max(1, _config.PointStride);
            state.Materials = CreateOverlayRenderMaterials();

            ApplyCurrentOverlayVisibilityToMaterials();
            MapBoundsRenderer.Render(
                _config,
                transform,
                state.Materials.WorldMapBounds,
                state.Materials.HeightMapBounds,
                state.Materials.MapCenter,
                state.OriginLon,
                state.OriginLat,
                ResolveY,
                LogVerboseOverlay
            );

            state.Phase = OverlayRebuildPhase.Zoning;
            state.CreatedZoningFillMeshes = RenderZoningFillMeshes(
                state.Materials.ZoningResidentialLow,
                state.Materials.ZoningResidentialMedium,
                state.Materials.ZoningResidentialHigh,
                state.Materials.ZoningIndustrial,
                state.Materials.ZoningCommercialLow,
                state.Materials.ZoningRetailDetail,
                state.Materials.ZoningOffice,
                state.Materials.ZoningSurface,
                state.Materials.ZoningRamp,
                state.Materials.ZoningFallback,
                state.Stride
            );

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

            state.RoadChunks = BuildRoadRenderChunks(false, state.OriginLon, state.OriginLat);
            state.PathChunks = BuildRoadRenderChunks(true, state.OriginLon, state.OriginLat);

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

            state.Phase = state.RoadChunks.Count > 0
                ? OverlayRebuildPhase.RoadChunks
                : (state.PathChunks.Count > 0 ? OverlayRebuildPhase.PathChunks : OverlayRebuildPhase.Arrows);

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
                CancelProgressiveOverlayRebuild("error", false);
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

        private void LogProgressiveOverlayProgress(bool force)
        {
            var state = _progressiveRebuild;

            if (state == null)
                return;

            var elapsedMs = state.Stopwatch != null ? state.Stopwatch.ElapsedMilliseconds : 0L;
            var currentIndex = state.Phase == OverlayRebuildPhase.PathChunks ? state.PathChunkIndex : state.RoadChunkIndex;
            var shouldLog = force ||
                elapsedMs - state.LastProgressLogMs >= 3000L ||
                (currentIndex > 0 && currentIndex % 25 == 0);

            if (!shouldLog)
                return;

            state.LastProgressLogMs = elapsedMs;

            Log.Info(
                "GroundOverlay: progressive rebuild progress: phase=" + state.Phase +
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

            state.Phase = OverlayRebuildPhase.Done;
            FinalizeOverlayDebugMarkers(state);

            if (state.Stopwatch != null)
                state.Stopwatch.Stop();

            LogVerboseOverlay(
                "GroundOverlay: created water lines=" + state.CreatedWaterLines +
                ", water batched segments=" + state.CreatedWaterSegments +
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

            state.Phase = OverlayRebuildPhase.None;

            if (log)
            {
                Log.Info(
                    "GroundOverlay: progressive overlay rebuild cancelled. reason=" + SafeLogValue(reason) +
                    ", roadChunks=" + state.RoadChunkIndex + "/" + state.RoadChunks.Count +
                    ", pathChunks=" + state.PathChunkIndex + "/" + state.PathChunks.Count +
                    ", roadSegments=" + state.RoadCounters.CreatedRoadSegments +
                    ", pathSegments=" + state.RoadCounters.CreatedPathSegments
                );
            }
        }

        private void FinalizeOverlayDebugMarkers(ProgressiveOverlayRebuildState state)
        {
            var center = new Vector3(
                _config.WorldOriginX,
                ResolveY(new Vector3(_config.WorldOriginX, 0f, _config.WorldOriginZ)) + _config.GroundMargin,
                _config.WorldOriginZ
            );

            CityTimelineMod.Roads.GeoRoadImportPlacement.CenterX = center.x;
            CityTimelineMod.Roads.GeoRoadImportPlacement.CenterZ = center.z;
            CityTimelineMod.Roads.GeoRoadImportPlacement.GroundY = center.y;

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

