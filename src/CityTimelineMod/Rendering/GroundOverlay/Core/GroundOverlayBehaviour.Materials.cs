using System.Collections.Generic;
using CityTimelineMod.Rendering.Materials;
using CityTimelineMod.Util;
using UnityEngine;

namespace CityTimelineMod.Rendering
{
    internal sealed partial class GroundOverlayBehaviour
    {
        private readonly List<Material> _zoningResidentialFamilyMaterials = new List<Material>();
        private readonly List<Material> _zoningCommercialFamilyMaterials = new List<Material>();
        private readonly List<Material> _zoningIndustrialFamilyMaterials = new List<Material>();
        private readonly List<Material> _zoningOfficeFamilyMaterials = new List<Material>();
        private readonly List<Material> _zoningParkingFamilyMaterials = new List<Material>();
        private readonly List<Material> _zoningFallbackFamilyMaterials = new List<Material>();
        private readonly Dictionary<string, List<Material>> _railwayMaterialGroups =
            new Dictionary<string, List<Material>>(System.StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<Material>> _serviceMaterialGroups =
            new Dictionary<string, List<Material>>(System.StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<Material>> _overlaySublayerMaterialGroups =
            new Dictionary<string, List<Material>>(System.StringComparer.OrdinalIgnoreCase);
        private readonly List<Material> _ownedOverlayMaterials = new List<Material>();
        private bool _overlayTeardownRequested;
        private bool _overlayTeardownInProgress;
        private bool _overlayTeardownComplete;

        internal bool TeardownOverlayResources()
        {
            if (_overlayTeardownComplete)
                return true;

            if (_overlayTeardownInProgress)
                return false;

            _overlayTeardownRequested = true;
            _overlayTeardownInProgress = true;
            var completed = true;

            // Railway changes are debounced during normal play.  Flush them
            // while the authoritative config snapshot is still available.
            if (_railwaySettingsSavePending)
            {
                try
                {
                    if (_config != null && _config.SaveVisualSettingsToConfig())
                        _railwaySettingsSavePending = false;
                    else
                        completed = false;
                }
                catch (System.Exception ex)
                {
                    completed = false;
                    Log.Error("GroundOverlay: deferred railway save failed during teardown. " + ex);
                }
            }

            // Do not discard the authoritative config or destroy the retry
            // container while a required deferred save is still pending.
            if (!completed || _railwaySettingsSavePending)
            {
                _created = false;
                _overlayTeardownInProgress = false;
                Log.Error("GroundOverlay: teardown paused until the deferred railway save can be retried.");
                return false;
            }

            _rebuildCancelRequested = true;

            try
            {
                CancelProgressiveOverlayRebuild("overlay teardown", false);
                _progressiveRebuild = null;
            }
            catch (System.Exception ex)
            {
                completed = false;
                Log.Error("GroundOverlay: progressive rebuild cleanup failed. " + ex);
            }

            _rebuildRestartPending = false;
            _controlPanelRebuildPending = false;
            _pendingBundleReloadId = null;
            _railwayRebuildPending = false;
            _railwayRebuildReason = null;
            _config = null;

            try
            {
                ClearOverlayChildren();
            }
            catch (System.Exception ex)
            {
                completed = false;
                Log.Error("GroundOverlay: child/mesh cleanup failed. " + ex);
            }

            try
            {
                ClearOverlayMaterialRegistries();
            }
            catch (System.Exception ex)
            {
                completed = false;
                Log.Error("GroundOverlay: material cleanup failed. " + ex);
            }

            try
            {
                DestroyHudResources();
            }
            catch (System.Exception ex)
            {
                completed = false;
                Log.Error("GroundOverlay: HUD cleanup failed. " + ex);
            }

            ResetRoadPrefabWidthResolution();

            _created = false;
            _overlayTeardownInProgress = false;

            if (completed)
                _overlayTeardownComplete = true;

            return _overlayTeardownComplete;
        }

        private Material CreateOwnedOverlayMaterial(Color color)
        {
            return OverlayMaterialFactory.Create(
                color,
                material => _ownedOverlayMaterials.Add(material)
            );
        }

        private OverlayRenderMaterials CreateOverlayRenderMaterials()
        {
            var materials = new OverlayRenderMaterials();

            materials.Cyan = CreateOwnedOverlayMaterial(_config.ResolveColorName(_config.WaterLineColor, _config.WaterLineAlpha));
            materials.WaterAreaBlue = CreateOwnedOverlayMaterial(_config.ResolveColorName(_config.WaterAreaOutlineColor, _config.WaterAreaOutlineAlpha));
            materials.WaterAreaFillBlue = CreateOwnedOverlayMaterial(_config.ResolveColorName(_config.WaterAreaFillColor, _config.WaterAreaFillAlpha));
            materials.FallbackRoad = CreateOwnedOverlayMaterial(_config.ResolveColorName(_config.RoadColorDefault, _config.RoadAlpha));
            materials.RoadMotorway = CreateOwnedOverlayMaterial(_config.ResolveColorName(_config.RoadColorMotorway, _config.RoadAlpha));
            materials.RoadPrimary = CreateOwnedOverlayMaterial(_config.ResolveColorName(_config.RoadColorPrimary, _config.RoadAlpha));
            materials.RoadSecondary = CreateOwnedOverlayMaterial(_config.ResolveColorName(_config.RoadColorSecondary, _config.RoadAlpha));
            materials.RoadTertiary = CreateOwnedOverlayMaterial(_config.ResolveColorName(_config.RoadColorTertiary, _config.RoadAlpha));
            materials.RoadLink = CreateOwnedOverlayMaterial(_config.ResolveColorName(_config.RoadColorLink, _config.RoadAlpha));
            materials.Path = CreateOwnedOverlayMaterial(_config.ResolveColorName(_config.PathColor, _config.PathAlpha));
            materials.RoadOneWay = CreateOwnedOverlayMaterial(new Color(0.25f, 0.9f, 1f, _config.RoadAlpha));
            materials.RoadBridge = CreateOwnedOverlayMaterial(new Color(1f, 0.85f, 0.15f, _config.RoadAlpha));
            materials.RoadTunnel = CreateOwnedOverlayMaterial(new Color(0.65f, 0.35f, 1f, _config.RoadAlpha));
            materials.RoadRoundabout = CreateOwnedOverlayMaterial(new Color(1f, 0.35f, 0.15f, _config.RoadAlpha));
            materials.RoadArrow = CreateOwnedOverlayMaterial(new Color(1f, 1f, 1f, _config.RoadAlpha));
            materials.RoadLabel = CreateOwnedOverlayMaterial(new Color(1f, 1f, 1f, _config.RoadAlpha));

            // Palette shared with cs2-realmap-generator/visualizer/js/railway-controller.js.
            materials.RailwayTrain = CreateRailwayMaterial(0xf8, 0xfa, 0xfc, 1f);
            materials.RailwayTrainTunnel = CreateRailwayMaterial(0xf8, 0xfa, 0xfc, 0.48f);
            materials.RailwayTram = CreateRailwayMaterial(0xf5, 0x9e, 0x0b, 1f);
            materials.RailwayTramTunnel = CreateRailwayMaterial(0xf5, 0x9e, 0x0b, 0.48f);
            materials.RailwayLightRail = CreateRailwayMaterial(0x22, 0xd3, 0xee, 1f);
            materials.RailwayLightRailTunnel = CreateRailwayMaterial(0x22, 0xd3, 0xee, 0.48f);
            materials.RailwaySubway = CreateRailwayMaterial(0xa7, 0x8b, 0xfa, 1f);
            materials.RailwaySubwayTunnel = CreateRailwayMaterial(0xa7, 0x8b, 0xfa, 0.48f);
            materials.RailwayService = CreateRailwayMaterial(0x94, 0xa3, 0xb8, 1f);
            materials.RailwayServiceTunnel = CreateRailwayMaterial(0x94, 0xa3, 0xb8, 0.48f);

            // Service markers use stable family colors shared by both HUDs.
            materials.ServiceWater = CreateServiceMaterial(0x38, 0xc8, 0xff, _config.ServicesWaterAlpha);
            materials.ServiceElectricity = CreateServiceMaterial(0xff, 0xd8, 0x4d, _config.ServicesElectricityAlpha);
            materials.ServiceEducation = CreateServiceMaterial(0x4d, 0xa3, 0xff, _config.ServicesEducationAlpha);
            materials.ServiceFire = CreateServiceMaterial(0xff, 0x5a, 0x52, _config.ServicesFireAlpha);
            materials.ServiceMedical = CreateServiceMaterial(0xff, 0x73, 0xa8, _config.ServicesHealthAlpha);
            materials.ServiceParks = CreateServiceMaterial(0x65, 0xd6, 0x6e, _config.ServicesParksAlpha);
            materials.ServiceWaste = CreateServiceMaterial(0xad, 0x96, 0x7a, _config.ServicesWasteAlpha);
            materials.ServiceTransport = CreateServiceMaterial(0xb8, 0x87, 0xff, _config.ServicesTransportAlpha);
            materials.ServiceCommunications = CreateServiceMaterial(0xff, 0x9f, 0x43, _config.ServicesCommunicationAlpha);

            materials.ZoningResidentialLow = CreateOwnedOverlayMaterial(_config.ResolveColorName(_config.ZoningResidentialLowColor, _config.ZoningAlpha));
            materials.ZoningResidentialMedium = CreateOwnedOverlayMaterial(_config.ResolveColorName(_config.ZoningResidentialMediumColor, _config.ZoningAlpha));
            materials.ZoningResidentialHigh = CreateOwnedOverlayMaterial(_config.ResolveColorName(_config.ZoningResidentialHighColor, _config.ZoningAlpha));
            materials.ZoningCommercialLow = CreateOwnedOverlayMaterial(_config.ResolveColorName(_config.ZoningCommercialLowColor, _config.ZoningAlpha));
            materials.ZoningCommercialHigh = CreateOwnedOverlayMaterial(_config.ResolveColorName(_config.ZoningCommercialHighColor, _config.ZoningAlpha));
            materials.ZoningRetailDetail = CreateOwnedOverlayMaterial(_config.ResolveColorName(_config.ZoningRetailColor, _config.ZoningAlpha));
            materials.ZoningIndustrial = CreateOwnedOverlayMaterial(_config.ResolveColorName(_config.ZoningIndustrialColor, _config.ZoningAlpha));
            materials.ZoningOffice = CreateOwnedOverlayMaterial(_config.ResolveColorName(_config.ZoningOfficeColor, _config.ZoningAlpha));
            materials.ZoningSurface = CreateOwnedOverlayMaterial(_config.ResolveColorName(_config.ZoningSurfaceColor, _config.ZoningAlpha));
            materials.ZoningRamp = CreateOwnedOverlayMaterial(_config.ResolveColorName(_config.ZoningRampColor, _config.ZoningAlpha));
            materials.ZoningMixed = CreateOwnedOverlayMaterial(_config.ResolveColorName(_config.ZoningMixedColor, _config.ZoningAlpha));
            materials.ZoningFallback = CreateOwnedOverlayMaterial(_config.ResolveColorName(_config.ZoningFallbackColor, _config.ZoningAlpha));

            materials.DebugRed = CreateOwnedOverlayMaterial(_config.ResolveColorName(_config.DebugRedColor, 1f));
            materials.DebugGreen = CreateOwnedOverlayMaterial(_config.ResolveColorName(_config.DebugGreenColor, 1f));
            materials.DebugYellow = CreateOwnedOverlayMaterial(_config.ResolveColorName(_config.DebugYellowColor, 1f));
            materials.DebugMagenta = CreateOwnedOverlayMaterial(_config.ResolveColorName(_config.DebugMagentaColor, 1f));

            materials.WorldMapBounds = CreateOwnedOverlayMaterial(new Color(1f, 1f, 1f, _config.WorldMapBoundsAlpha));
            materials.HeightMapBounds = CreateOwnedOverlayMaterial(new Color(1f, 0.1f, 1f, _config.HeightMapBoundsAlpha));
            materials.MapCenter = CreateOwnedOverlayMaterial(new Color(1f, 0f, 0f, _config.MapCenterAlpha));

            _waterLineMaterials.Add(materials.Cyan);
            _waterAreaOutlineMaterials.Add(materials.WaterAreaBlue);
            _waterAreaFillMaterials.Add(materials.WaterAreaFillBlue);
            _mapBoundsMaterials.Add(materials.WorldMapBounds);
            _mapBoundsMaterials.Add(materials.HeightMapBounds);
            _mapBoundsMaterials.Add(materials.MapCenter);

            _roadMaterials.Add(materials.FallbackRoad);
            _roadMaterials.Add(materials.RoadMotorway);
            _roadMaterials.Add(materials.RoadPrimary);
            _roadMaterials.Add(materials.RoadSecondary);
            _roadMaterials.Add(materials.RoadTertiary);
            _roadMaterials.Add(materials.RoadLink);
            _roadMaterials.Add(materials.RoadOneWay);
            _roadMaterials.Add(materials.RoadBridge);
            _roadMaterials.Add(materials.RoadTunnel);
            _roadMaterials.Add(materials.RoadRoundabout);
            _roadMaterials.Add(materials.RoadArrow);
            _roadMaterials.Add(materials.RoadLabel);

            _pathMaterials.Add(materials.Path);

            RegisterOverlaySublayerMaterial("roads.highway", materials.RoadMotorway);
            RegisterOverlaySublayerMaterial("roads.large_road", materials.RoadPrimary);
            RegisterOverlaySublayerMaterial("roads.medium_road", materials.RoadSecondary);
            RegisterOverlaySublayerMaterial("roads.small_road", materials.RoadTertiary);
            RegisterOverlaySublayerMaterial("roads.ramp", materials.RoadLink);
            RegisterOverlaySublayerMaterial("roads.gravel_road", materials.FallbackRoad);
            RegisterOverlaySublayerMaterial("roads.pathway", materials.Path);

            // Water sublayers directly toggle RenderWaterLines/RenderWaterAreas.
            // Keep their distinct line/outline/fill alpha values in the central
            // water applier instead of flattening both area materials to one alpha.

            RegisterOverlaySublayerMaterial("zoning.residential_low", materials.ZoningResidentialLow);
            RegisterOverlaySublayerMaterial("zoning.residential_medium", materials.ZoningResidentialMedium);
            RegisterOverlaySublayerMaterial("zoning.residential_high", materials.ZoningResidentialHigh);
            RegisterOverlaySublayerMaterial("zoning.commercial_low", materials.ZoningCommercialLow);
            RegisterOverlaySublayerMaterial("zoning.commercial_high", materials.ZoningCommercialHigh);
            RegisterOverlaySublayerMaterial("zoning.retail", materials.ZoningRetailDetail);
            RegisterOverlaySublayerMaterial("zoning.industrial", materials.ZoningIndustrial);
            RegisterOverlaySublayerMaterial("zoning.office", materials.ZoningOffice);
            RegisterOverlaySublayerMaterial("zoning.parking_surface", materials.ZoningSurface);
            RegisterOverlaySublayerMaterial("zoning.parking_structure", materials.ZoningRamp);
            RegisterOverlaySublayerMaterial("zoning.mixed", materials.ZoningMixed);

            RegisterRailwayMaterial("train.surface", materials.RailwayTrain);
            RegisterRailwayMaterial("train.tunnel", materials.RailwayTrainTunnel);
            RegisterRailwayMaterial("tram.surface", materials.RailwayTram);
            RegisterRailwayMaterial("tram.tunnel", materials.RailwayTramTunnel);
            RegisterRailwayMaterial("light_rail.surface", materials.RailwayLightRail);
            RegisterRailwayMaterial("light_rail.tunnel", materials.RailwayLightRailTunnel);
            RegisterRailwayMaterial("subway.surface", materials.RailwaySubway);
            RegisterRailwayMaterial("subway.tunnel", materials.RailwaySubwayTunnel);
            RegisterRailwayMaterial("service.surface", materials.RailwayService);
            RegisterRailwayMaterial("service.tunnel", materials.RailwayServiceTunnel);

            RegisterServiceMaterial("water", materials.ServiceWater);
            RegisterServiceMaterial("electricity", materials.ServiceElectricity);
            RegisterServiceMaterial("education", materials.ServiceEducation);
            RegisterServiceMaterial("fire", materials.ServiceFire);
            RegisterServiceMaterial("medical", materials.ServiceMedical);
            RegisterServiceMaterial("parks", materials.ServiceParks);
            RegisterServiceMaterial("waste", materials.ServiceWaste);
            RegisterServiceMaterial("transport", materials.ServiceTransport);
            RegisterServiceMaterial("communications", materials.ServiceCommunications);

            _zoningResidentialFamilyMaterials.Add(materials.ZoningResidentialLow);
            _zoningResidentialFamilyMaterials.Add(materials.ZoningResidentialMedium);
            _zoningResidentialFamilyMaterials.Add(materials.ZoningResidentialHigh);

            _zoningCommercialFamilyMaterials.Add(materials.ZoningCommercialLow);
            _zoningCommercialFamilyMaterials.Add(materials.ZoningCommercialHigh);
            _zoningCommercialFamilyMaterials.Add(materials.ZoningRetailDetail);
            _zoningCommercialFamilyMaterials.Add(materials.ZoningMixed);

            _zoningIndustrialFamilyMaterials.Add(materials.ZoningIndustrial);
            _zoningOfficeFamilyMaterials.Add(materials.ZoningOffice);

            _zoningParkingFamilyMaterials.Add(materials.ZoningSurface);
            _zoningParkingFamilyMaterials.Add(materials.ZoningRamp);

            _zoningFallbackFamilyMaterials.Add(materials.ZoningFallback);

            _zoningMaterials.Add(materials.ZoningResidentialLow);
            _zoningMaterials.Add(materials.ZoningResidentialMedium);
            _zoningMaterials.Add(materials.ZoningResidentialHigh);
            _zoningMaterials.Add(materials.ZoningCommercialLow);
            _zoningMaterials.Add(materials.ZoningCommercialHigh);
            _zoningMaterials.Add(materials.ZoningRetailDetail);
            _zoningMaterials.Add(materials.ZoningMixed);
            _zoningMaterials.Add(materials.ZoningIndustrial);
            _zoningMaterials.Add(materials.ZoningOffice);
            _zoningMaterials.Add(materials.ZoningSurface);
            _zoningMaterials.Add(materials.ZoningRamp);
            _zoningMaterials.Add(materials.ZoningFallback);

            return materials;
        }


        private void ClearOverlayMaterialRegistries()
        {
            var failed = false;

            for (var i = _ownedOverlayMaterials.Count - 1; i >= 0; i--)
            {
                var material = _ownedOverlayMaterials[i];

                try
                {
                    if (material != null)
                        UnityEngine.Object.Destroy(material);

                    _ownedOverlayMaterials.RemoveAt(i);
                }
                catch (System.Exception ex)
                {
                    failed = true;
                    Log.Error("GroundOverlay: failed to destroy owned material. " + ex);
                }
            }

            if (failed)
                throw new System.InvalidOperationException("One or more owned overlay materials could not be destroyed.");

            _zoningMaterials.Clear();
            _roadMaterials.Clear();
            _pathMaterials.Clear();
            _waterLineMaterials.Clear();
            _waterAreaOutlineMaterials.Clear();
            _waterAreaFillMaterials.Clear();
            _mapBoundsMaterials.Clear();
            _roadLabelMeshes.Clear();
            _railwayMaterialGroups.Clear();
            _serviceMaterialGroups.Clear();
            _overlaySublayerMaterialGroups.Clear();

            _zoningResidentialFamilyMaterials.Clear();
            _zoningCommercialFamilyMaterials.Clear();
            _zoningIndustrialFamilyMaterials.Clear();
            _zoningOfficeFamilyMaterials.Clear();
            _zoningParkingFamilyMaterials.Clear();
            _zoningFallbackFamilyMaterials.Clear();

        }

        private void ReleaseOverlayMaterials(IList<Material> materials)
        {
            if (materials == null || materials.Count == 0)
                return;

            var failed = false;
            var releasedMaterialIds = new HashSet<int>();
            for (var i = 0; i < materials.Count; i++)
            {
                var material = materials[i];
                if (material == null)
                    continue;

                try
                {
                    var materialId = material.GetInstanceID();
                    UnityEngine.Object.Destroy(material);
                    releasedMaterialIds.Add(materialId);
                }
                catch (System.Exception ex)
                {
                    failed = true;
                    Log.Error("GroundOverlay: failed to release staged overlay material. " + ex);
                }
            }

            RemoveMaterialReferences(_ownedOverlayMaterials, releasedMaterialIds);
            RemoveMaterialReferences(_zoningMaterials, releasedMaterialIds);
            RemoveMaterialReferences(_roadMaterials, releasedMaterialIds);
            RemoveMaterialReferences(_pathMaterials, releasedMaterialIds);
            RemoveMaterialReferences(_waterLineMaterials, releasedMaterialIds);
            RemoveMaterialReferences(_waterAreaOutlineMaterials, releasedMaterialIds);
            RemoveMaterialReferences(_waterAreaFillMaterials, releasedMaterialIds);
            RemoveMaterialReferences(_mapBoundsMaterials, releasedMaterialIds);
            RemoveMaterialReferences(_zoningResidentialFamilyMaterials, releasedMaterialIds);
            RemoveMaterialReferences(_zoningCommercialFamilyMaterials, releasedMaterialIds);
            RemoveMaterialReferences(_zoningIndustrialFamilyMaterials, releasedMaterialIds);
            RemoveMaterialReferences(_zoningOfficeFamilyMaterials, releasedMaterialIds);
            RemoveMaterialReferences(_zoningParkingFamilyMaterials, releasedMaterialIds);
            RemoveMaterialReferences(_zoningFallbackFamilyMaterials, releasedMaterialIds);
            RemoveMaterialReferences(_railwayMaterialGroups, releasedMaterialIds);
            RemoveMaterialReferences(_serviceMaterialGroups, releasedMaterialIds);
            RemoveMaterialReferences(_overlaySublayerMaterialGroups, releasedMaterialIds);

            if (failed)
            {
                throw new System.InvalidOperationException(
                    "One or more staged overlay materials could not be released."
                );
            }
        }

        private static void RemoveMaterialReferences(
            List<Material> materials,
            HashSet<int> materialIds
        )
        {
            if (materials == null)
                return;

            for (var i = materials.Count - 1; i >= 0; i--)
            {
                var material = materials[i];
                if (material == null || materialIds.Contains(material.GetInstanceID()))
                    materials.RemoveAt(i);
            }
        }

        private static void RemoveMaterialReferences(
            Dictionary<string, List<Material>> groups,
            HashSet<int> materialIds
        )
        {
            if (groups == null)
                return;

            foreach (var materials in groups.Values)
                RemoveMaterialReferences(materials, materialIds);
        }


        private void ApplyCurrentOverlayVisibilityToMaterials()
        {
            InvalidateBundleHudSnapshotJson();
            ApplyZoningFamilyVisibilityToMaterials();

            OverlayVisibilityApplier.ApplyRoadVisibility(
                _roadMaterials,
                _pathMaterials,
                _config.RenderRoads,
                _config.RenderPaths,
                _config.RoadAlpha,
                _config.PathAlpha
            );

            OverlayVisibilityApplier.ApplyWaterVisibility(
                _waterLineMaterials,
                _waterAreaOutlineMaterials,
                _waterAreaFillMaterials,
                _config.RenderWaterLines,
                _config.RenderWaterAreas,
                _config.WaterLineAlpha,
                _config.WaterAreaOutlineAlpha,
                _config.WaterAreaFillAlpha
            );

            ApplyRailwayVisibilityToMaterials();
            ApplyServiceVisibilityToMaterials();
            ApplyOverlaySublayerVisibilityToMaterials();

            OverlayVisibilityApplier.ApplyMapBoundsVisibility(
                _mapBoundsMaterials,
                _config.RenderMapBounds,
                _config.MapBoundsAlpha
            );
        }

        private Material CreateRailwayMaterial(byte red, byte green, byte blue, float opacityScale)
        {
            var alpha = Mathf.Clamp01(_config.RailwayOpacity * opacityScale);
            return CreateOwnedOverlayMaterial(new Color32(red, green, blue, (byte)Mathf.RoundToInt(alpha * 255f)));
        }

        private Material CreateServiceMaterial(byte red, byte green, byte blue, float alpha)
        {
            return CreateOwnedOverlayMaterial(
                new Color32(red, green, blue, (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255f))
            );
        }

        private void RegisterServiceMaterial(string family, Material material)
        {
            if (material == null || string.IsNullOrWhiteSpace(family))
                return;

            List<Material> group;
            if (!_serviceMaterialGroups.TryGetValue(family, out group))
            {
                group = new List<Material>();
                _serviceMaterialGroups[family] = group;
            }

            group.Add(material);
        }

        private void RegisterOverlaySublayerMaterial(string key, Material material)
        {
            if (material == null || string.IsNullOrWhiteSpace(key))
                return;

            List<Material> group;
            if (!_overlaySublayerMaterialGroups.TryGetValue(key, out group))
            {
                group = new List<Material>();
                _overlaySublayerMaterialGroups[key] = group;
            }

            group.Add(material);
        }

        private void ApplyOverlaySublayerVisibilityToMaterials()
        {
            if (_config == null)
                return;

            foreach (var pair in _overlaySublayerMaterialGroups)
            {
                var key = pair.Key;
                var visible = ShouldRenderOverlaySublayer(key);
                var alpha = 1f;

                if (key.StartsWith("roads.", System.StringComparison.OrdinalIgnoreCase))
                {
                    var isPath = string.Equals(key, "roads.pathway", System.StringComparison.OrdinalIgnoreCase);
                    visible = visible && (isPath ? _config.RenderPaths : _config.RenderRoads);
                    alpha = isPath ? _config.PathAlpha : _config.RoadAlpha;
                }
                else if (string.Equals(key, "water.lines", System.StringComparison.OrdinalIgnoreCase))
                {
                    visible = visible && _config.RenderWaterLines;
                    alpha = _config.WaterLineAlpha;
                }
                else if (string.Equals(key, "water.areas", System.StringComparison.OrdinalIgnoreCase))
                {
                    visible = visible && _config.RenderWaterAreas;
                    alpha = _config.WaterAreaFillAlpha;
                }
                else if (key.StartsWith("zoning.", System.StringComparison.OrdinalIgnoreCase))
                {
                    visible = visible && _config.RenderZoning && _zoningVisible &&
                        ResolveZoningSublayerFamilyVisible(key);
                    alpha = ResolveZoningSublayerAlpha(key);
                }

                ApplyMaterialGroupAlpha(pair.Value, visible, alpha);
            }
        }

        private bool ResolveZoningSublayerFamilyVisible(string key)
        {
            if (key.StartsWith("zoning.residential_", System.StringComparison.OrdinalIgnoreCase))
                return ResolveModernLayerVisibleForMaterials("zoning.residential", _config.ZoningResidentialVisible);
            if (key.StartsWith("zoning.commercial_", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "zoning.retail", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "zoning.mixed", System.StringComparison.OrdinalIgnoreCase))
            {
                return ResolveModernLayerVisibleForMaterials("zoning.commercial", _config.ZoningCommercialVisible);
            }
            if (string.Equals(key, "zoning.industrial", System.StringComparison.OrdinalIgnoreCase))
                return ResolveModernLayerVisibleForMaterials("zoning.industrial", _config.ZoningIndustrialVisible);
            if (string.Equals(key, "zoning.office", System.StringComparison.OrdinalIgnoreCase))
                return ResolveModernLayerVisibleForMaterials("zoning.office", _config.ZoningOfficeVisible);
            if (key.StartsWith("zoning.parking_", System.StringComparison.OrdinalIgnoreCase))
                return ResolveModernLayerVisibleForMaterials("parking", _config.ParkingVisible);
            return true;
        }

        private float ResolveZoningSublayerAlpha(string key)
        {
            if (key.StartsWith("zoning.residential_", System.StringComparison.OrdinalIgnoreCase))
                return ResolveModernLayerAlphaForMaterials("zoning.residential", _config.ZoningResidentialAlpha);
            if (key.StartsWith("zoning.commercial_", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "zoning.retail", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "zoning.mixed", System.StringComparison.OrdinalIgnoreCase))
            {
                return ResolveModernLayerAlphaForMaterials("zoning.commercial", _config.ZoningCommercialAlpha);
            }
            if (string.Equals(key, "zoning.industrial", System.StringComparison.OrdinalIgnoreCase))
                return ResolveModernLayerAlphaForMaterials("zoning.industrial", _config.ZoningIndustrialAlpha);
            if (string.Equals(key, "zoning.office", System.StringComparison.OrdinalIgnoreCase))
                return ResolveModernLayerAlphaForMaterials("zoning.office", _config.ZoningOfficeAlpha);
            if (key.StartsWith("zoning.parking_", System.StringComparison.OrdinalIgnoreCase))
                return ResolveModernLayerAlphaForMaterials("parking", _config.ParkingAlpha);
            return _config.ZoningAlpha;
        }

        private void ApplyServiceVisibilityToMaterials()
        {
            if (_config == null)
                return;

            ApplyServiceFamilyVisibility("water", _config.ServicesWaterVisible, _config.ServicesWaterAlpha);
            ApplyServiceFamilyVisibility("electricity", _config.ServicesElectricityVisible, _config.ServicesElectricityAlpha);
            ApplyServiceFamilyVisibility("education", _config.ServicesEducationVisible, _config.ServicesEducationAlpha);
            ApplyServiceFamilyVisibility("fire", _config.ServicesFireVisible, _config.ServicesFireAlpha);
            ApplyServiceFamilyVisibility("medical", _config.ServicesHealthVisible, _config.ServicesHealthAlpha);
            ApplyServiceFamilyVisibility("parks", _config.ServicesParksVisible, _config.ServicesParksAlpha);
            ApplyServiceFamilyVisibility("waste", _config.ServicesWasteVisible, _config.ServicesWasteAlpha);
            ApplyServiceFamilyVisibility("transport", _config.ServicesTransportVisible, _config.ServicesTransportAlpha);
            ApplyServiceFamilyVisibility("communications", _config.ServicesCommunicationVisible, _config.ServicesCommunicationAlpha);
        }

        private void ApplyServiceFamilyVisibility(string family, bool visible, float alpha)
        {
            List<Material> materials;
            if (_serviceMaterialGroups.TryGetValue(family, out materials))
                ApplyMaterialGroupAlpha(materials, _config.RenderServices && visible, alpha);
        }

        private void RegisterRailwayMaterial(string key, Material material)
        {
            if (material == null || string.IsNullOrWhiteSpace(key))
                return;

            List<Material> group;
            if (!_railwayMaterialGroups.TryGetValue(key, out group))
            {
                group = new List<Material>();
                _railwayMaterialGroups[key] = group;
            }

            group.Add(material);
        }

        private void ApplyRailwayVisibilityToMaterials()
        {
            if (_config == null)
                return;

            ApplyRailwayCategoryVisibility("train", _config.RailwayTrainVisible);
            ApplyRailwayCategoryVisibility("tram", _config.RailwayTramVisible);
            ApplyRailwayCategoryVisibility("light_rail", _config.RailwayLightRailVisible);
            ApplyRailwayCategoryVisibility("subway", _config.RailwaySubwayVisible);
            ApplyRailwayCategoryVisibility("service", _config.RailwayServiceVisible);
        }

        private void ApplyRailwayCategoryVisibility(string category, bool categoryVisible)
        {
            var surfaceVisible = _config.RenderRailways && categoryVisible;
            var tunnelVisible = surfaceVisible && _config.RailwayTunnelsVisible;
            List<Material> surface;
            List<Material> tunnel;

            if (_railwayMaterialGroups.TryGetValue(category + ".surface", out surface))
            {
                OverlayVisibilityApplier.SetMaterialsAlpha(
                    surface,
                    surfaceVisible ? _config.RailwayOpacity : 0f
                );
            }

            if (_railwayMaterialGroups.TryGetValue(category + ".tunnel", out tunnel))
            {
                OverlayVisibilityApplier.SetMaterialsAlpha(
                    tunnel,
                    tunnelVisible ? _config.RailwayOpacity * 0.48f : 0f
                );
            }
        }

        private void ApplyZoningFamilyVisibilityToMaterials()
        {
            if (_config == null)
                return;

            var zoningMasterVisible = _config.RenderZoning && _zoningVisible;

            ApplyMaterialGroupAlpha(
                _zoningResidentialFamilyMaterials,
                zoningMasterVisible && ResolveModernLayerVisibleForMaterials("zoning.residential", _config.ZoningResidentialVisible),
                ResolveModernLayerAlphaForMaterials("zoning.residential", _config.ZoningResidentialAlpha)
            );

            ApplyMaterialGroupAlpha(
                _zoningCommercialFamilyMaterials,
                zoningMasterVisible && ResolveModernLayerVisibleForMaterials("zoning.commercial", _config.ZoningCommercialVisible),
                ResolveModernLayerAlphaForMaterials("zoning.commercial", _config.ZoningCommercialAlpha)
            );

            ApplyMaterialGroupAlpha(
                _zoningIndustrialFamilyMaterials,
                zoningMasterVisible && ResolveModernLayerVisibleForMaterials("zoning.industrial", _config.ZoningIndustrialVisible),
                ResolveModernLayerAlphaForMaterials("zoning.industrial", _config.ZoningIndustrialAlpha)
            );

            ApplyMaterialGroupAlpha(
                _zoningOfficeFamilyMaterials,
                zoningMasterVisible && ResolveModernLayerVisibleForMaterials("zoning.office", _config.ZoningOfficeVisible),
                ResolveModernLayerAlphaForMaterials("zoning.office", _config.ZoningOfficeAlpha)
            );

            ApplyMaterialGroupAlpha(
                _zoningParkingFamilyMaterials,
                zoningMasterVisible && ResolveModernLayerVisibleForMaterials("parking", _config.ParkingVisible),
                ResolveModernLayerAlphaForMaterials("parking", _config.ParkingAlpha)
            );

            ApplyMaterialGroupAlpha(
                _zoningFallbackFamilyMaterials,
                zoningMasterVisible,
                _config.ZoningAlpha
            );
        }


        private bool ResolveModernLayerVisibleForMaterials(string layerId, bool fallback)
        {
            bool value;

            if (_modernHudLayerVisible != null && _modernHudLayerVisible.TryGetValue(layerId, out value))
                return value;

            return fallback;
        }

        private float ResolveModernLayerAlphaForMaterials(string layerId, float fallback)
        {
            float value;

            if (_modernHudLayerOpacity != null && _modernHudLayerOpacity.TryGetValue(layerId, out value))
                return Mathf.Clamp01(value);

            return Mathf.Clamp01(fallback);
        }

        private static void ApplyMaterialGroupAlpha(List<Material> materials, bool visible, float alpha)
        {
            if (materials == null)
                return;

            var targetAlpha = visible ? Mathf.Clamp01(alpha) : 0f;

            for (var i = 0; i < materials.Count; i++)
            {
                var material = materials[i];

                if (material == null)
                    continue;

                var color = material.color;

                if (Mathf.Abs(color.a - targetAlpha) < 0.0001f)
                    continue;

                color.a = targetAlpha;
                material.color = color;
            }
        }

    }
}
