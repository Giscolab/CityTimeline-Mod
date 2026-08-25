using System;
using CityTimelineMod.Config;
using CityTimelineMod.Importers;
using UnityEngine;

namespace CityTimelineMod.Rendering.Roads
{
    internal static class RoadRenderStyleResolver
    {
        // Single source of truth for the textured tertiary batch key. Callers
        // compare against this constant instead of repeating the literal.
        internal const string TertiaryTexturedBatchKey = "tertiary_textured";

        // Mike's Asphalt Cover (Over)
        // decal.json: colossal_MeshSize.z = 23 m.
        internal const float TertiaryTextureRepeatMeters = 23f;

        // The uploaded source contains transparent lateral margins.
        // These coordinates select the actual asphalt region instead
        // of stretching the empty margins over the road ribbon.
        internal const float TertiaryTextureUMin = 0.21484375f;
        internal const float TertiaryTextureUMax = 0.787109375f;

        internal static bool IsTexturedTertiary(
            GeoRoadLine roadLine
        )
        {
            return
                roadLine != null &&
                string.Equals(
                    roadLine.Highway,
                    "tertiary",
                    StringComparison.OrdinalIgnoreCase
                );
        }

        internal static string ResolveRoadRenderBatchKey(
            GeoRoadLine roadLine,
            GeoOverlayConfig config
        )
        {
            if (roadLine == null)
                return "fallback";

            if (roadLine.IsPath)
                return "path";

            if (config.HighlightRoundabouts && roadLine.Roundabout)
                return "roundabout";

            if (config.HighlightBridges && roadLine.Bridge)
                return "bridge";

            if (config.HighlightTunnels && roadLine.Tunnel)
                return "tunnel";

            if (
                config.HighlightOneWayRoads &&
                RoadRenderRules.IsOnewayRoad(roadLine)
            )
            {
                return "oneway";
            }

            // Deliberately target only real OSM highway=tertiary.
            // residential and living_street remain on the existing
            // solid white RoadTertiary material.
            if (IsTexturedTertiary(roadLine))
                return TertiaryTexturedBatchKey;

            return RoadRenderRules.ResolveRoadBatchKey(
                roadLine.Highway
            );
        }

        internal static Material ResolveRoadRenderBatchMaterial(
            GeoRoadLine roadLine,
            GeoOverlayConfig config,
            Material roadMotorwayMaterial,
            Material roadPrimaryMaterial,
            Material roadSecondaryMaterial,
            Material roadTertiaryMaterial,
            Material roadTertiaryTexturedMaterial,
            Material roadLinkMaterial,
            Material fallbackRoadMaterial,
            Material roadOneWayMaterial,
            Material roadBridgeMaterial,
            Material roadTunnelMaterial,
            Material roadRoundaboutMaterial
        )
        {
            var key = ResolveRoadRenderBatchKey(
                roadLine,
                config
            );

            switch (key)
            {
                case "roundabout":
                    return roadRoundaboutMaterial;

                case "bridge":
                    return roadBridgeMaterial;

                case "tunnel":
                    return roadTunnelMaterial;

                case "oneway":
                    return roadOneWayMaterial;

                case TertiaryTexturedBatchKey:
                    return roadTertiaryTexturedMaterial != null
                        ? roadTertiaryTexturedMaterial
                        : roadTertiaryMaterial;

                default:
                    return RoadRenderRules.ResolveRoadBatchMaterial(
                        roadLine != null
                            ? roadLine.Highway
                            : null,
                        roadMotorwayMaterial,
                        roadPrimaryMaterial,
                        roadSecondaryMaterial,
                        roadTertiaryMaterial,
                        roadLinkMaterial,
                        fallbackRoadMaterial
                    );
            }
        }

        internal static float ResolveRoadSegmentWidth(
            GeoRoadLine roadLine,
            bool isPath,
            GeoOverlayConfig config
        )
        {
            var baseWidth = isPath
                ? config.PathSegmentWidth
                : config.RoadSegmentWidth;

            if (
                isPath ||
                !config.UseLaneWidthScaling ||
                roadLine == null
            )
            {
                return baseWidth;
            }

            var laneCount = roadLine.TargetLaneCount.HasValue
                ? roadLine.TargetLaneCount.Value
                : (
                    roadLine.Lanes.HasValue
                        ? roadLine.Lanes.Value
                        : 1
                );

            if (laneCount <= 1)
                return baseWidth;

            var multiplier =
                1f +
                (laneCount - 1) *
                Mathf.Clamp(
                    config.LaneWidthScaleFactor,
                    0f,
                    2f
                );

            multiplier = Mathf.Clamp(
                multiplier,
                1f,
                4f
            );

            return baseWidth * multiplier;
        }
    }
}