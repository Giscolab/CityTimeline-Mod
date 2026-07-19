using System.Collections.Generic;
using CityTimelineMod.Rendering.Materials;
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
        private readonly List<Material> _ownedOverlayMaterials = new List<Material>();

        private OverlayRenderMaterials CreateOverlayRenderMaterials()
        {
            var materials = new OverlayRenderMaterials();

            materials.Cyan = OverlayMaterialFactory.Create(_config.ResolveColorName(_config.WaterLineColor, _config.WaterLineAlpha));
            materials.WaterAreaBlue = OverlayMaterialFactory.Create(_config.ResolveColorName(_config.WaterAreaOutlineColor, _config.WaterAreaOutlineAlpha));
            materials.WaterAreaFillBlue = OverlayMaterialFactory.Create(_config.ResolveColorName(_config.WaterAreaFillColor, _config.WaterAreaFillAlpha));
            materials.FallbackRoad = OverlayMaterialFactory.Create(_config.ResolveColorName(_config.RoadColorDefault, _config.RoadAlpha));
            materials.RoadMotorway = OverlayMaterialFactory.Create(_config.ResolveColorName(_config.RoadColorMotorway, _config.RoadAlpha));
            materials.RoadPrimary = OverlayMaterialFactory.Create(_config.ResolveColorName(_config.RoadColorPrimary, _config.RoadAlpha));
            materials.RoadSecondary = OverlayMaterialFactory.Create(_config.ResolveColorName(_config.RoadColorSecondary, _config.RoadAlpha));
            materials.RoadTertiary = OverlayMaterialFactory.Create(_config.ResolveColorName(_config.RoadColorTertiary, _config.RoadAlpha));
            materials.RoadLink = OverlayMaterialFactory.Create(_config.ResolveColorName(_config.RoadColorLink, _config.RoadAlpha));
            materials.Path = OverlayMaterialFactory.Create(_config.ResolveColorName(_config.PathColor, _config.PathAlpha));
            materials.RoadOneWay = OverlayMaterialFactory.Create(new Color(0.25f, 0.9f, 1f, _config.RoadAlpha));
            materials.RoadBridge = OverlayMaterialFactory.Create(new Color(1f, 0.85f, 0.15f, _config.RoadAlpha));
            materials.RoadTunnel = OverlayMaterialFactory.Create(new Color(0.65f, 0.35f, 1f, _config.RoadAlpha));
            materials.RoadRoundabout = OverlayMaterialFactory.Create(new Color(1f, 0.35f, 0.15f, _config.RoadAlpha));
            materials.RoadArrow = OverlayMaterialFactory.Create(new Color(1f, 1f, 1f, _config.RoadAlpha));
            materials.RoadLabel = OverlayMaterialFactory.Create(new Color(1f, 1f, 1f, _config.RoadAlpha));

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

            materials.ZoningResidentialLow = OverlayMaterialFactory.Create(_config.ResolveColorName(_config.ZoningResidentialLowColor, _config.ZoningAlpha));
            materials.ZoningResidentialMedium = OverlayMaterialFactory.Create(_config.ResolveColorName(_config.ZoningResidentialMediumColor, _config.ZoningAlpha));
            materials.ZoningResidentialHigh = OverlayMaterialFactory.Create(_config.ResolveColorName(_config.ZoningResidentialHighColor, _config.ZoningAlpha));
            materials.ZoningCommercialLow = OverlayMaterialFactory.Create(_config.ResolveColorName(_config.ZoningCommercialLowColor, _config.ZoningAlpha));
            materials.ZoningCommercialHigh = OverlayMaterialFactory.Create(_config.ResolveColorName(_config.ZoningCommercialHighColor, _config.ZoningAlpha));
            materials.ZoningRetailDetail = OverlayMaterialFactory.Create(_config.ResolveColorName(_config.ZoningRetailColor, _config.ZoningAlpha));
            materials.ZoningIndustrial = OverlayMaterialFactory.Create(_config.ResolveColorName(_config.ZoningIndustrialColor, _config.ZoningAlpha));
            materials.ZoningOffice = OverlayMaterialFactory.Create(_config.ResolveColorName(_config.ZoningOfficeColor, _config.ZoningAlpha));
            materials.ZoningSurface = OverlayMaterialFactory.Create(_config.ResolveColorName(_config.ZoningSurfaceColor, _config.ZoningAlpha));
            materials.ZoningRamp = OverlayMaterialFactory.Create(_config.ResolveColorName(_config.ZoningRampColor, _config.ZoningAlpha));
            materials.ZoningMixed = OverlayMaterialFactory.Create(_config.ResolveColorName(_config.ZoningMixedColor, _config.ZoningAlpha));
            materials.ZoningFallback = OverlayMaterialFactory.Create(_config.ResolveColorName(_config.ZoningFallbackColor, _config.ZoningAlpha));

            materials.DebugRed = OverlayMaterialFactory.Create(_config.ResolveColorName(_config.DebugRedColor, 1f));
            materials.DebugGreen = OverlayMaterialFactory.Create(_config.ResolveColorName(_config.DebugGreenColor, 1f));
            materials.DebugYellow = OverlayMaterialFactory.Create(_config.ResolveColorName(_config.DebugYellowColor, 1f));
            materials.DebugMagenta = OverlayMaterialFactory.Create(_config.ResolveColorName(_config.DebugMagentaColor, 1f));

            materials.WorldMapBounds = OverlayMaterialFactory.Create(new Color(1f, 1f, 1f, _config.WorldMapBoundsAlpha));
            materials.HeightMapBounds = OverlayMaterialFactory.Create(new Color(1f, 0.1f, 1f, _config.HeightMapBoundsAlpha));
            materials.MapCenter = OverlayMaterialFactory.Create(new Color(1f, 0f, 0f, _config.MapCenterAlpha));

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

            _ownedOverlayMaterials.AddRange(new[]
            {
                materials.Cyan,
                materials.WaterAreaBlue,
                materials.WaterAreaFillBlue,
                materials.FallbackRoad,
                materials.RoadMotorway,
                materials.RoadPrimary,
                materials.RoadSecondary,
                materials.RoadTertiary,
                materials.RoadLink,
                materials.Path,
                materials.RoadOneWay,
                materials.RoadBridge,
                materials.RoadTunnel,
                materials.RoadRoundabout,
                materials.RoadArrow,
                materials.RoadLabel,
                materials.RailwayTrain,
                materials.RailwayTrainTunnel,
                materials.RailwayTram,
                materials.RailwayTramTunnel,
                materials.RailwayLightRail,
                materials.RailwayLightRailTunnel,
                materials.RailwaySubway,
                materials.RailwaySubwayTunnel,
                materials.RailwayService,
                materials.RailwayServiceTunnel,
                materials.ServiceWater,
                materials.ServiceElectricity,
                materials.ServiceEducation,
                materials.ServiceFire,
                materials.ServiceMedical,
                materials.ServiceParks,
                materials.ServiceWaste,
                materials.ServiceTransport,
                materials.ServiceCommunications,
                materials.ZoningResidentialLow,
                materials.ZoningResidentialMedium,
                materials.ZoningResidentialHigh,
                materials.ZoningCommercialLow,
                materials.ZoningCommercialHigh,
                materials.ZoningRetailDetail,
                materials.ZoningIndustrial,
                materials.ZoningOffice,
                materials.ZoningSurface,
                materials.ZoningRamp,
                materials.ZoningMixed,
                materials.ZoningFallback,
                materials.DebugRed,
                materials.DebugGreen,
                materials.DebugYellow,
                materials.DebugMagenta,
                materials.WorldMapBounds,
                materials.HeightMapBounds,
                materials.MapCenter
            });

            return materials;
        }


        private void ClearOverlayMaterialRegistries()
        {
            foreach (var material in _ownedOverlayMaterials)
            {
                if (material != null)
                    UnityEngine.Object.Destroy(material);
            }

            _ownedOverlayMaterials.Clear();
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

            _zoningResidentialFamilyMaterials.Clear();
            _zoningCommercialFamilyMaterials.Clear();
            _zoningIndustrialFamilyMaterials.Clear();
            _zoningOfficeFamilyMaterials.Clear();
            _zoningParkingFamilyMaterials.Clear();
            _zoningFallbackFamilyMaterials.Clear();
        }


        private void ApplyCurrentOverlayVisibilityToMaterials()
        {
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
                _waterVisible,
                _config.WaterLineAlpha,
                _config.WaterAreaOutlineAlpha,
                _config.WaterAreaFillAlpha
            );

            ApplyRailwayVisibilityToMaterials();
            ApplyServiceVisibilityToMaterials();

            OverlayVisibilityApplier.ApplyMapBoundsVisibility(
                _mapBoundsMaterials,
                _config.RenderMapBounds,
                _config.MapBoundsAlpha
            );
        }

        private Material CreateRailwayMaterial(byte red, byte green, byte blue, float opacityScale)
        {
            var alpha = Mathf.Clamp01(_config.RailwayOpacity * opacityScale);
            return OverlayMaterialFactory.Create(new Color32(red, green, blue, (byte)Mathf.RoundToInt(alpha * 255f)));
        }

        private static Material CreateServiceMaterial(byte red, byte green, byte blue, float alpha)
        {
            return OverlayMaterialFactory.Create(
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
