using System;
using System.Collections.Generic;
using CityTimelineMod.Rendering.Batching;
using CityTimelineMod.Rendering.Materials;
using CityTimelineMod.Rendering.Services;
using CityTimelineMod.Util;
using UnityEngine;

namespace CityTimelineMod.Rendering
{
    internal sealed partial class GroundOverlayBehaviour
    {
        private const int ServiceMaxVerticesPerMesh = 60000;
        private List<GeoServicePoint> _servicePoints = new List<GeoServicePoint>();
        private ServiceGeoJsonLoadResult _serviceLoadResult = new ServiceGeoJsonLoadResult();
        private ServiceRenderCounters _lastServiceRenderCounters = new ServiceRenderCounters();

        private void InitializeServiceOverlay(
            List<GeoServicePoint> points,
            ServiceGeoJsonLoadResult loadResult
        )
        {
            _servicePoints = points ?? new List<GeoServicePoint>();
            _serviceLoadResult = loadResult ?? new ServiceGeoJsonLoadResult();
            _lastServiceRenderCounters = new ServiceRenderCounters();

            Log.Info(
                "GroundOverlay: service semantic summary: points=" + _servicePoints.Count +
                ", loadedFamilies=" + _serviceLoadResult.LoadedFamilyCount +
                ", missingFamilies=" + _serviceLoadResult.MissingFamilyCount +
                ", failedFamilies=" + _serviceLoadResult.FailedFamilyCount
            );
        }

        internal bool SetServiceBoolean(string familyKey, bool value)
        {
            if (_config == null)
                return false;

            var masterWasDisabled = !_config.RenderServices;

            switch (NormalizeServiceFamilyKey(familyKey))
            {
                case "water": _config.ServicesWaterVisible = value; break;
                case "electricity": _config.ServicesElectricityVisible = value; break;
                case "education": _config.ServicesEducationVisible = value; break;
                case "fire": _config.ServicesFireVisible = value; break;
                case "medical": _config.ServicesHealthVisible = value; break;
                case "parks": _config.ServicesParksVisible = value; break;
                case "waste": _config.ServicesWasteVisible = value; break;
                case "transport": _config.ServicesTransportVisible = value; break;
                case "communications": _config.ServicesCommunicationVisible = value; break;
                default: return false;
            }

            if (value)
                _config.RenderServices = true;

            InvalidateBundleHudSnapshotJson();
            SyncModernLayerStateFromConfig();
            ApplyCurrentOverlayVisibilityToMaterials();
            ScheduleRailwaySettingsSave();

            if (value && masterWasDisabled)
                RequestOverlayRebuild("service overlay enabled", true);

            return true;
        }

        internal bool SetServiceFloat(string familyKey, float value)
        {
            if (_config == null)
                return false;

            var alpha = Mathf.Clamp01(value);
            switch (NormalizeServiceFamilyKey(familyKey))
            {
                case "water": _config.ServicesWaterAlpha = alpha; break;
                case "electricity": _config.ServicesElectricityAlpha = alpha; break;
                case "education": _config.ServicesEducationAlpha = alpha; break;
                case "fire": _config.ServicesFireAlpha = alpha; break;
                case "medical": _config.ServicesHealthAlpha = alpha; break;
                case "parks": _config.ServicesParksAlpha = alpha; break;
                case "waste": _config.ServicesWasteAlpha = alpha; break;
                case "transport": _config.ServicesTransportAlpha = alpha; break;
                case "communications": _config.ServicesCommunicationAlpha = alpha; break;
                default: return false;
            }

            InvalidateBundleHudSnapshotJson();
            SyncModernLayerStateFromConfig();
            ApplyCurrentOverlayVisibilityToMaterials();
            ScheduleRailwaySettingsSave();
            return true;
        }

        private bool GetServiceFamilyVisible(string familyKey)
        {
            if (_config == null || !_config.RenderServices)
                return false;

            switch (NormalizeServiceFamilyKey(familyKey))
            {
                case "water": return _config.ServicesWaterVisible;
                case "electricity": return _config.ServicesElectricityVisible;
                case "education": return _config.ServicesEducationVisible;
                case "fire": return _config.ServicesFireVisible;
                case "medical": return _config.ServicesHealthVisible;
                case "parks": return _config.ServicesParksVisible;
                case "waste": return _config.ServicesWasteVisible;
                case "transport": return _config.ServicesTransportVisible;
                case "communications": return _config.ServicesCommunicationVisible;
                default: return false;
            }
        }

        private float GetServiceFamilyOpacity(string familyKey)
        {
            if (_config == null)
                return 0f;

            switch (NormalizeServiceFamilyKey(familyKey))
            {
                case "water": return Mathf.Clamp01(_config.ServicesWaterAlpha);
                case "electricity": return Mathf.Clamp01(_config.ServicesElectricityAlpha);
                case "education": return Mathf.Clamp01(_config.ServicesEducationAlpha);
                case "fire": return Mathf.Clamp01(_config.ServicesFireAlpha);
                case "medical": return Mathf.Clamp01(_config.ServicesHealthAlpha);
                case "parks": return Mathf.Clamp01(_config.ServicesParksAlpha);
                case "waste": return Mathf.Clamp01(_config.ServicesWasteAlpha);
                case "transport": return Mathf.Clamp01(_config.ServicesTransportAlpha);
                case "communications": return Mathf.Clamp01(_config.ServicesCommunicationAlpha);
                default: return 0f;
            }
        }

        private bool IsServiceFamilyAvailable(string familyKey)
        {
            var family = _serviceLoadResult != null
                ? _serviceLoadResult.GetFamily(NormalizeServiceFamilyKey(familyKey))
                : null;
            return family != null && family.Available;
        }

        private static string NormalizeServiceFamilyKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return "";

            var normalized = key.Trim().ToLowerInvariant();
            if (normalized.StartsWith("services.", StringComparison.Ordinal))
                normalized = normalized.Substring("services.".Length);

            if (normalized == "health")
                return "medical";
            if (normalized == "communication")
                return "communications";
            return normalized;
        }

        private List<ServiceRenderChunk> BuildServiceRenderChunks(
            double originLon,
            double originLat,
            ServiceRenderCounters counters
        )
        {
            if (_servicePoints == null || _servicePoints.Count == 0 || _config == null)
                return new List<ServiceRenderChunk>();

            if (!_config.RenderServices)
            {
                if (counters != null)
                {
                    counters.SourcePoints += _servicePoints.Count;
                    counters.SkippedByFilter += _servicePoints.Count;
                }
                return new List<ServiceRenderChunk>();
            }

            var chunkSize = _config.EnableServiceSpatialChunking
                ? _config.ServiceChunkSizeMeters
                : 100000000f;

            return ServiceGeometryBuilder.BuildChunks(
                _servicePoints,
                _config,
                originLon,
                originLat,
                chunkSize,
                counters
            );
        }

        private int RenderServiceChunk(
            ServiceRenderChunk chunk,
            OverlayRenderMaterials materials,
            ServiceRenderCounters counters
        )
        {
            if (chunk == null || chunk.Points == null || materials == null || counters == null)
                return 0;

            if (_config == null || !_config.RenderServices)
            {
                counters.SkippedByFilter += chunk.Points.Count;
                return 0;
            }

            var definition = ServiceCatalog.GetFamily(chunk.FamilyKey);
            var material = ResolveServiceMaterial(chunk.FamilyKey, materials);
            if (definition == null || material == null)
            {
                counters.SkippedInvalid += chunk.Points.Count;
                return 0;
            }

            var batch = new ServicePointMeshBatch
            {
                FamilyKey = chunk.FamilyKey,
                Material = material
            };
            var batchKey = chunk.FamilyKey + "_cx" + chunk.ChunkX + "_cz" + chunk.ChunkZ;
            var createdBefore = counters.CreatedPoints;

            for (var i = 0; i < chunk.Points.Count; i++)
            {
                var renderPoint = chunk.Points[i];
                if (renderPoint == null || renderPoint.Source == null)
                {
                    counters.SkippedInvalid++;
                    continue;
                }

                if (batch.Vertices.Count >= ServiceMaxVerticesPerMesh - 16)
                {
                    counters.CreatedMeshObjects += OverlayMeshFlusher.FlushServicePointBatch(
                        transform,
                        batchKey,
                        batch,
                        LogVerboseOverlay
                    );
                }

                var world = renderPoint.World;
                world.y = ResolveY(world) + _config.GroundMargin + _config.ServiceYOffset +
                    ResolveServiceFamilyYOffset(chunk.FamilyKey);
                var markerSize = ResolveServiceMarkerSize(renderPoint.Source);

                if (ServiceGeometryBuilder.AppendMarker(batch, world, markerSize, definition.MarkerShape))
                    counters.CreatedPoints++;
                else
                    counters.SkippedInvalid++;
            }

            counters.CreatedMeshObjects += OverlayMeshFlusher.FlushServicePointBatch(
                transform,
                batchKey,
                batch,
                LogVerboseOverlay
            );
            return counters.CreatedPoints - createdBefore;
        }

        private float ResolveServiceMarkerSize(GeoServicePoint point)
        {
            var size = Mathf.Max(1f, _config.ServiceMarkerSize);
            var subcategory = point != null ? point.SubcategoryKey : "";

            switch (subcategory)
            {
                case "grid":
                case "radio":
                case "bus":
                case "sewage":
                    return size * 0.55f;

                case "large_station":
                case "hospital":
                case "large_park":
                case "university":
                case "air":
                    return size * 1.25f;

                default:
                    return size;
            }
        }

        private static float ResolveServiceFamilyYOffset(string familyKey)
        {
            switch (NormalizeServiceFamilyKey(familyKey))
            {
                case "education": return 0.00f;
                case "fire": return 0.08f;
                case "medical": return 0.16f;
                case "parks": return 0.24f;
                case "electricity": return 0.32f;
                case "waste": return 0.40f;
                case "transport": return 0.48f;
                case "water": return 0.56f;
                case "communications": return 0.64f;
                default: return 0f;
            }
        }

        private static Material ResolveServiceMaterial(string familyKey, OverlayRenderMaterials materials)
        {
            switch (NormalizeServiceFamilyKey(familyKey))
            {
                case "water": return materials.ServiceWater;
                case "electricity": return materials.ServiceElectricity;
                case "education": return materials.ServiceEducation;
                case "fire": return materials.ServiceFire;
                case "medical": return materials.ServiceMedical;
                case "parks": return materials.ServiceParks;
                case "waste": return materials.ServiceWaste;
                case "transport": return materials.ServiceTransport;
                case "communications": return materials.ServiceCommunications;
                default: return null;
            }
        }

        private ServiceRenderCounters RenderServiceGroup(
            OverlayRenderMaterials materials,
            double originLon,
            double originLat
        )
        {
            var counters = new ServiceRenderCounters();
            CopyServiceLoadCounters(counters);
            var chunks = BuildServiceRenderChunks(originLon, originLat, counters);

            for (var i = 0; i < chunks.Count; i++)
                RenderServiceChunk(chunks[i], materials, counters);

            _lastServiceRenderCounters = counters.Copy();
            LogServiceRenderCounters(counters, chunks.Count);
            return counters;
        }

        private void CopyServiceLoadCounters(ServiceRenderCounters counters)
        {
            if (counters == null || _serviceLoadResult == null)
                return;

            counters.LoadedFamilies = _serviceLoadResult.LoadedFamilyCount;
            counters.MissingFamilies = _serviceLoadResult.MissingFamilyCount;
            counters.FailedFamilies = _serviceLoadResult.FailedFamilyCount;
        }

        private void LogServiceRenderCounters(ServiceRenderCounters counters, int chunkCount)
        {
            if (counters == null)
                return;

            Log.Info(
                "GroundOverlay: service batching summary: sourcePoints=" + counters.SourcePoints +
                ", eligiblePoints=" + counters.EligiblePoints +
                ", renderedPoints=" + counters.CreatedPoints +
                ", meshObjects=" + counters.CreatedMeshObjects +
                ", chunks=" + chunkCount +
                ", loadedFamilies=" + counters.LoadedFamilies +
                ", missingFamilies=" + counters.MissingFamilies +
                ", failedFamilies=" + counters.FailedFamilies +
                ", skippedOutsideMap=" + counters.SkippedOutsideMap +
                ", skippedByFilter=" + counters.SkippedByFilter +
                ", skippedInvalid=" + counters.SkippedInvalid
            );
        }
    }
}
