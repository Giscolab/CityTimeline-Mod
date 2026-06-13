using CityTimelineMod.Rendering.Materials;
using UnityEngine;

namespace CityTimelineMod.Rendering
{
    internal sealed partial class GroundOverlayBehaviour
    {
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

            materials.ZoningResidentialLow = OverlayMaterialFactory.Create(_config.ResolveColorName(_config.ZoningResidentialLowColor, _config.ZoningAlpha));
            materials.ZoningResidentialMedium = OverlayMaterialFactory.Create(_config.ResolveColorName(_config.ZoningResidentialMediumColor, _config.ZoningAlpha));
            materials.ZoningResidentialHigh = OverlayMaterialFactory.Create(_config.ResolveColorName(_config.ZoningResidentialHighColor, _config.ZoningAlpha));
            materials.ZoningCommercialLow = OverlayMaterialFactory.Create(_config.ResolveColorName(_config.ZoningCommercialLowColor, _config.ZoningAlpha));
            materials.ZoningRetailDetail = OverlayMaterialFactory.Create(_config.ResolveColorName(_config.ZoningRetailColor, _config.ZoningAlpha));
            materials.ZoningIndustrial = OverlayMaterialFactory.Create(_config.ResolveColorName(_config.ZoningIndustrialColor, _config.ZoningAlpha));
            materials.ZoningOffice = OverlayMaterialFactory.Create(_config.ResolveColorName(_config.ZoningOfficeColor, _config.ZoningAlpha));
            materials.ZoningSurface = OverlayMaterialFactory.Create(_config.ResolveColorName(_config.ZoningSurfaceColor, _config.ZoningAlpha));
            materials.ZoningRamp = OverlayMaterialFactory.Create(_config.ResolveColorName(_config.ZoningRampColor, _config.ZoningAlpha));
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

            _zoningMaterials.Add(materials.ZoningResidentialLow);
            _zoningMaterials.Add(materials.ZoningResidentialMedium);
            _zoningMaterials.Add(materials.ZoningResidentialHigh);
            _zoningMaterials.Add(materials.ZoningCommercialLow);
            _zoningMaterials.Add(materials.ZoningRetailDetail);
            _zoningMaterials.Add(materials.ZoningIndustrial);
            _zoningMaterials.Add(materials.ZoningOffice);
            _zoningMaterials.Add(materials.ZoningSurface);
            _zoningMaterials.Add(materials.ZoningRamp);
            _zoningMaterials.Add(materials.ZoningFallback);

            return materials;
        }

        private void ClearOverlayMaterialRegistries()
        {
            _zoningMaterials.Clear();
            _roadMaterials.Clear();
            _pathMaterials.Clear();
            _waterLineMaterials.Clear();
            _waterAreaOutlineMaterials.Clear();
            _waterAreaFillMaterials.Clear();
            _mapBoundsMaterials.Clear();
            _roadLabelMeshes.Clear();
        }

        private void ApplyCurrentOverlayVisibilityToMaterials()
        {
            OverlayVisibilityApplier.ApplyZoningVisibility(_zoningMaterials, _zoningVisible, _config.ZoningAlpha);
            OverlayVisibilityApplier.ApplyRoadVisibility(_roadMaterials, _pathMaterials, _config.RenderRoads, _config.RenderPaths, _config.RoadAlpha, _config.PathAlpha);
            OverlayVisibilityApplier.ApplyWaterVisibility(_waterLineMaterials, _waterAreaOutlineMaterials, _waterAreaFillMaterials, _waterVisible, _config.WaterLineAlpha, _config.WaterAreaOutlineAlpha, _config.WaterAreaFillAlpha);
            OverlayVisibilityApplier.ApplyMapBoundsVisibility(_mapBoundsMaterials, _config.RenderMapBounds, _config.MapBoundsAlpha);
        }
    }
}
