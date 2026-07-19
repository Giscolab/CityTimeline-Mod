using System;
using CityTimelineMod.Util;
using UnityEngine;

namespace CityTimelineMod.Config
{
    internal sealed partial class GeoOverlayConfig
    {
        private void ClampRuntimeVisualSettings()
        {
            if (MaxRoadSegmentsDebug < 1)
                MaxRoadSegmentsDebug = 1;

            if (MaxPathSegmentsDebug < 1)
                MaxPathSegmentsDebug = MaxRoadSegmentsDebug;

            if (MaxZoningFillMeshesDebug < 1)
                MaxZoningFillMeshesDebug = 1;

            if (MaxWaterSegmentsDebug < 1)
                MaxWaterSegmentsDebug = 1;

            if (MaxWaterAreaFillMeshesDebug < 1)
                MaxWaterAreaFillMeshesDebug = 1;

            if (PointStride < 1)
                PointStride = 1;

            LaneWidthScaleFactor = Mathf.Clamp(LaneWidthScaleFactor, 0f, 2f);
            RoadArrowSpacingMeters = Mathf.Max(1f, RoadArrowSpacingMeters);
            RoadArrowSize = Mathf.Max(0.1f, RoadArrowSize);
            RoadArrowYOffset = Mathf.Max(0f, RoadArrowYOffset);
            RoadArrowMaxCount = Math.Max(0, RoadArrowMaxCount);
            RoadLabelMaxCount = Math.Max(0, RoadLabelMaxCount);
            RoadLabelMinTier = Mathf.Clamp(RoadLabelMinTier, 0, 4);
            RoadLabelMaxDistance = Mathf.Max(0f, RoadLabelMaxDistance);
            RoadLabelYOffset = Mathf.Max(0f, RoadLabelYOffset);
            RoadLabelFontSize = Mathf.Clamp(RoadLabelFontSize, 8, 128);
            RibbonYOffset = Mathf.Max(0f, RibbonYOffset);
            RoadChunkSizeMeters = Mathf.Clamp(RoadChunkSizeMeters, 128f, 4096f);
            RoadChunksPerFrame = Mathf.Clamp(RoadChunksPerFrame, 1, 64);
            PathChunksPerFrame = Mathf.Clamp(PathChunksPerFrame, 1, 64);
            ClampRailwaySettings();
            RoadRenderMode = NormalizeRoadRenderMode(RoadRenderMode);
            PathRenderMode = NormalizeRoadRenderMode(PathRenderMode);
            ZoningResidentialAlpha = Clamp01(ZoningResidentialAlpha);
            ZoningCommercialAlpha = Clamp01(ZoningCommercialAlpha);
            ZoningIndustrialAlpha = Clamp01(ZoningIndustrialAlpha);
            ZoningOfficeAlpha = Clamp01(ZoningOfficeAlpha);
            ParkingAlpha = Clamp01(ParkingAlpha);

            ZoningResidentialAlpha = Clamp01(ZoningResidentialAlpha);
            ZoningCommercialAlpha = Clamp01(ZoningCommercialAlpha);
            ZoningIndustrialAlpha = Clamp01(ZoningIndustrialAlpha);
            ZoningOfficeAlpha = Clamp01(ZoningOfficeAlpha);
            ParkingAlpha = Clamp01(ParkingAlpha);

            ServicesWaterAlpha = Clamp01(ServicesWaterAlpha);
            ServicesElectricityAlpha = Clamp01(ServicesElectricityAlpha);
            ServicesEducationAlpha = Clamp01(ServicesEducationAlpha);
            ServicesFireAlpha = Clamp01(ServicesFireAlpha);
            ServicesHealthAlpha = Clamp01(ServicesHealthAlpha);
            ServicesParksAlpha = Clamp01(ServicesParksAlpha);
            ServicesWasteAlpha = Clamp01(ServicesWasteAlpha);
            ServicesTransportAlpha = Clamp01(ServicesTransportAlpha);
            ServicesCommunicationAlpha = Clamp01(ServicesCommunicationAlpha);

            RoadSemanticFilterMode = NormalizeRoadSemanticFilterMode(RoadSemanticFilterMode);
            RuntimeRoadImportMaxSegments = Math.Max(1, RuntimeRoadImportMaxSegments);
            RuntimeRoadImportStride = Math.Max(1, RuntimeRoadImportStride);
            RuntimeRoadImportMinSegmentLengthMeters = Mathf.Max(0.5f, RuntimeRoadImportMinSegmentLengthMeters);
            RuntimeRoadImportSnapToleranceMeters = Mathf.Clamp(RuntimeRoadImportSnapToleranceMeters, 0.5f, 50f);
            RuntimeRoadImportYOffset = Mathf.Clamp(RuntimeRoadImportYOffset, -10f, 10f);
        }

        private static string NormalizeRoadRenderMode(string mode)
        {
            var value = string.IsNullOrWhiteSpace(mode)
                ? "ribbon"
                : mode.Trim().ToLowerInvariant();

            if (value == "ribbon")
                return "ribbon";

            if (value == "box")
            {
                Log.Info("GeoOverlayConfig: WARNING: road/path render mode=box is retired. Fallback to ribbon.");
                return "ribbon";
            }

            Log.Info("GeoOverlayConfig: WARNING: invalid road/path render mode=" + value + ". Fallback to ribbon.");
            return "ribbon";
        }

        private static string NormalizeRoadSemanticFilterMode(string mode)
        {
            var value = string.IsNullOrWhiteSpace(mode)
                ? "all"
                : mode.Trim().ToLowerInvariant();

            switch (value)
            {
                case "all":
                case "major":
                case "local":
                case "oneway":
                case "bridges":
                case "tunnels":
                case "roundabouts":
                    return value;

                default:
                    Log.Error("GeoOverlayConfig: invalid roadSemanticFilterMode=" + value + ". Fallback to all.");
                    return "all";
            }
        }

        private static float Clamp01(float value)
        {
            if (value < 0f)
                return 0f;

            if (value > 1f)
                return 1f;

            return value;
        }
    }
}
