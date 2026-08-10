using System;
using System.Collections.Generic;
using CityTimelineMod.Importers;
using CityTimelineMod.Rendering.Batching;
using CityTimelineMod.Rendering.Core;
using CityTimelineMod.Rendering.Materials;
using CityTimelineMod.Rendering.Roads;
using CityTimelineMod.Util;
using UnityEngine;

namespace CityTimelineMod.Rendering
{
    internal sealed partial class GroundOverlayBehaviour
    {
        private void LogRoadRenderCounters(RoadRenderCounters counters)
        {
            if (counters == null)
                return;

            var renderAllRoadSegments = ShouldRenderAllRoadSegments();
            var renderAllPathSegments = ShouldRenderAllPathSegments();

            Log.Info(
                "GroundOverlay: road batching summary: roadLines=" + counters.CreatedRoadLines +
                ", sourceRoads=" + counters.SourceRoadLines +
                ", eligibleRoads=" + counters.EligibleRoadLines +
                ", roadSegments=" + counters.CreatedRoadSegments +
                ", meshObjects=" + counters.CreatedRoadMeshObjects +
                ", maxRoadSegmentsDebug=" + _config.MaxRoadSegmentsDebug +
                ", renderAllRoadSegments=" + renderAllRoadSegments +
                ", roadRenderMode=" + ResolveRoadRenderMode(_config.RoadRenderMode) +
                ", skippedByFilter=" + counters.SkippedRoadByFilter +
                ", skippedByTier=" + counters.SkippedRoadByTier +
                ", skippedByLimit=" + counters.SkippedRoadByLimit +
                ", minimumRoadDebugTier=" + _config.MinimumRoadDebugTier
            );

            Log.Info(
                "GroundOverlay: path batching summary: pathLines=" + counters.CreatedPathLines +
                ", sourcePaths=" + counters.SourcePathLines +
                ", eligiblePaths=" + counters.EligiblePathLines +
                ", pathSegments=" + counters.CreatedPathSegments +
                ", meshObjects=" + counters.CreatedPathMeshObjects +
                ", maxPathSegmentsDebug=" + _config.MaxPathSegmentsDebug +
                ", renderAllPathSegments=" + renderAllPathSegments +
                ", pathRenderMode=" + ResolveRoadRenderMode(_config.PathRenderMode) +
                ", skippedByFilter=" + counters.SkippedPathByFilter +
                ", skippedByTier=" + counters.SkippedPathByTier +
                ", skippedByLimit=" + counters.SkippedPathByLimit +
                ", skippedInvalid=" + counters.SkippedInvalid
            );
        }

        private void LogRenderedAndSkippedRoadHighwaySummary()
        {
            if (_roadLines == null || _roadLines.Count == 0)
                return;

            var rendered = new Dictionary<string, int>();
            var skipped = new Dictionary<string, int>();

            foreach (var road in _roadLines)
            {
                var highway = road != null ? road.Highway : null;

                if (string.IsNullOrWhiteSpace(highway))
                    highway = "(missing)";

                var isPath = road != null && road.IsPath;
                var highwayFilter = isPath ? _config.PathHighwayFilter : _config.RoadHighwayFilter;
                var sourceEnabled = isPath ? _config.RenderPaths : _config.RenderRoads;

                var shouldRender =
                    sourceEnabled &&
                    (isPath || RoadRenderRules.ShouldRenderRoadByTier(highway, _config.MinimumRoadDebugTier)) &&
                    RoadRenderRules.ShouldRenderHighwayByFilter(highway, highwayFilter) &&
                    ShouldRenderRoadBySemanticFilter(road);
                var target = shouldRender ? rendered : skipped;

                int count;
                target.TryGetValue(highway, out count);
                target[highway] = count + 1;
            }

            LogVerboseOverlay(
                "GroundOverlay: rendered road highway summary: " +
                FormatRoadHighwayCounts(rendered)
            );

            LogVerboseOverlay(
                "GroundOverlay: skipped road highway summary: " +
                FormatRoadHighwayCounts(skipped)
            );
        }

        private static string FormatRoadHighwayCounts(Dictionary<string, int> counts)
        {
            if (counts == null || counts.Count == 0)
                return "(none)";

            var parts = new List<string>();

            foreach (var pair in counts)
                parts.Add(pair.Key + "=" + pair.Value);

            return string.Join(", ", parts.ToArray());
        }

        private void LogRoadTierFilterSummary(int renderedRoadLines)
        {
            var sourceRoadLines = _roadLines != null ? _roadLines.Count : 0;
            var skippedRoadLines = Math.Max(0, sourceRoadLines - renderedRoadLines);

            LogVerboseOverlay(
                "GroundOverlay: road tier filter summary: source=" + sourceRoadLines +
                ", rendered=" + renderedRoadLines +
                ", skipped=" + skippedRoadLines +
                ", minimumTier=" + _config.MinimumRoadDebugTier
            );
        }

        private void LogRoadHighwaySummary()
        {
            if (_roadLines == null || _roadLines.Count == 0)
                return;

            var counts = new Dictionary<string, int>();

            foreach (var road in _roadLines)
            {
                var highway = road != null ? road.Highway : null;

                if (string.IsNullOrWhiteSpace(highway))
                    highway = "(missing)";

                int count;
                counts.TryGetValue(highway, out count);
                counts[highway] = count + 1;
            }

            var parts = new List<string>();

            foreach (var pair in counts)
                parts.Add(pair.Key + "=" + pair.Value);

            LogVerboseOverlay("GroundOverlay: road highway summary: " + string.Join(", ", parts.ToArray()));
        }

        private void RecomputeRoadSemanticStats()
        {
            var stats = new RoadSemanticStats();

            if (_roadLines != null)
            {
                foreach (var road in _roadLines)
                {
                    if (road == null)
                        continue;

                    stats.Total++;

                    if (road.IsPath)
                        stats.Paths++;
                    else
                        stats.Roads++;

                    if (!string.IsNullOrWhiteSpace(road.Name))
                        stats.WithName++;

                    if (!string.IsNullOrWhiteSpace(road.Oneway))
                    {
                        stats.WithOneway++;

                        if (RoadRenderRules.IsOnewayTrue(road.Oneway))
                            stats.OnewayTrue++;

                        if (RoadRenderRules.IsOnewayReverse(road.Oneway))
                            stats.OnewayReverse++;
                    }

                    if (road.Lanes.HasValue)
                        stats.WithLanes++;

                    if (road.TargetLaneCount.HasValue)
                        stats.WithTargetLaneCount++;

                    if (!string.IsNullOrWhiteSpace(road.MaxSpeed))
                        stats.WithMaxSpeed++;

                    if (road.Bridge)
                        stats.Bridges++;

                    if (road.Tunnel)
                        stats.Tunnels++;

                    if (road.Roundabout)
                        stats.Roundabouts++;
                }
            }

            _roadSemanticStats = stats;
        }

        private void LogRoadSemanticSummary()
        {
            var stats = _roadSemanticStats ?? new RoadSemanticStats();

            Log.Info(
                "GroundOverlay: road semantic summary: " +
                "total=" + stats.Total +
                ", roads=" + stats.Roads +
                ", paths=" + stats.Paths +
                ", withName=" + stats.WithName +
                ", withOneway=" + stats.WithOneway +
                ", onewayTrue=" + stats.OnewayTrue +
                ", onewayReverse=" + stats.OnewayReverse +
                ", withLanes=" + stats.WithLanes +
                ", withTargetLaneCount=" + stats.WithTargetLaneCount +
                ", withMaxSpeed=" + stats.WithMaxSpeed +
                ", bridges=" + stats.Bridges +
                ", tunnels=" + stats.Tunnels +
                ", roundabouts=" + stats.Roundabouts
            );
        }

private static string FormatRoadSemanticSample(GeoRoadLine road)
{
    if (road == null)
        return "(null)";

    return
        "name=" + SafeRoadSemanticValue(road.Name) +
        ", highway=" + SafeRoadSemanticValue(road.Highway) +
        ", oneway=" + SafeRoadSemanticValue(road.Oneway) +
        ", lanes=" + (road.Lanes.HasValue ? road.Lanes.Value.ToString() : "(none)") +
        ", targetLaneCount=" + (road.TargetLaneCount.HasValue ? road.TargetLaneCount.Value.ToString() : "(none)") +
        ", maxspeed=" + SafeRoadSemanticValue(road.MaxSpeed) +
        ", bridge=" + road.Bridge +
        ", tunnel=" + road.Tunnel +
        ", roundabout=" + road.Roundabout;
}

private static string SafeRoadSemanticValue(string value)
{
    return string.IsNullOrWhiteSpace(value) ? "(none)" : value;
}
    }
}
