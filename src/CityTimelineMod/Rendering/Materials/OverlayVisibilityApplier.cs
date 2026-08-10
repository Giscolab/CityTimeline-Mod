using System.Collections.Generic;
using UnityEngine;

namespace CityTimelineMod.Rendering.Materials
{
    internal static class OverlayVisibilityApplier
    {
        internal static void ApplyZoningVisibility(List<Material> zoningMaterials, bool visible, float zoningAlpha)
        {
            SetMaterialsAlpha(zoningMaterials, visible ? zoningAlpha : 0f);
        }

        internal static void ApplyRoadVisibility(List<Material> roadMaterials, List<Material> pathMaterials, bool renderRoads, bool renderPaths, float roadAlpha, float pathAlpha)
        {
            SetMaterialsAlpha(roadMaterials, renderRoads ? roadAlpha : 0f);
            SetMaterialsAlpha(pathMaterials, renderPaths ? pathAlpha : 0f);
        }

        internal static void ApplyWaterVisibility(List<Material> waterLineMaterials, List<Material> waterAreaOutlineMaterials, List<Material> waterAreaFillMaterials, bool renderLines, bool renderAreas, float waterLineAlpha, float waterAreaOutlineAlpha, float waterAreaFillAlpha)
        {
            SetMaterialsAlpha(waterLineMaterials, renderLines ? waterLineAlpha : 0f);
            SetMaterialsAlpha(waterAreaOutlineMaterials, renderAreas ? waterAreaOutlineAlpha : 0f);
            SetMaterialsAlpha(waterAreaFillMaterials, renderAreas ? waterAreaFillAlpha : 0f);
        }

        internal static void ApplyMapBoundsVisibility(List<Material> mapBoundsMaterials, bool renderMapBounds, float mapBoundsAlpha)
        {
            SetMaterialsAlpha(mapBoundsMaterials, renderMapBounds ? mapBoundsAlpha : 0f);
        }

        internal static void SetMaterialsAlpha(List<Material> materials, float alpha)
        {
            if (materials == null)
                return;

            var safeAlpha = Mathf.Clamp01(alpha);

            for (var i = 0; i < materials.Count; i++)
            {
                var material = materials[i];
                if (material == null)
                    continue;

                var color = material.color;
                color.a = safeAlpha;
                OverlayMaterialFactory.ApplyTransparentSettings(material, color);
            }
        }
    }
}
