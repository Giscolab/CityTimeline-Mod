using System.Collections.Generic;
using CityTimelineMod.Rendering.Materials;
using UnityEngine;

namespace CityTimelineMod.Rendering
{
    internal sealed partial class GroundOverlayBehaviour
    {
        private Material _roadTertiaryCurbMaterial;
        private readonly Dictionary<int, List<Texture2D>>
            _ownedGenericRoadSurfaceTexturesByMaterialId =
                new Dictionary<int, List<Texture2D>>();

        private Material CreateOwnedRoadSurfaceMaterial(
            string embeddedFolder,
            Color tint
        )
        {
            var bundle =
                RoadSurfaceMaterialFactory.Create(
                    typeof(GroundOverlayBehaviour).Assembly,
                    embeddedFolder,
                    tint
                );

            if (
                bundle == null ||
                bundle.Material == null
            )
            {
                throw new System.InvalidOperationException(
                    "Road surface material factory returned no material."
                );
            }

            _ownedOverlayMaterials.Add(
                bundle.Material
            );

            _ownedGenericRoadSurfaceTexturesByMaterialId[
                bundle.Material.GetInstanceID()
            ] = bundle.Textures;

            return bundle.Material;
        }

        private void ReleaseOwnedRoadSurfaceTextures(
            int materialId
        )
        {
            List<Texture2D> textures;

            if (
                !_ownedGenericRoadSurfaceTexturesByMaterialId
                    .TryGetValue(
                        materialId,
                        out textures
                    )
            )
            {
                return;
            }

            if (textures != null)
            {
                for (
                    var i = textures.Count - 1;
                    i >= 0;
                    i--
                )
                {
                    var texture = textures[i];

                    if (texture != null)
                    {
                        UnityEngine.Object.Destroy(
                            texture
                        );
                    }
                }
            }

            _ownedGenericRoadSurfaceTexturesByMaterialId
                .Remove(materialId);
        }

        private void ReleaseAllOwnedRoadSurfaceTextures()
        {
            var materialIds =
                new List<int>(
                    _ownedGenericRoadSurfaceTexturesByMaterialId.Keys
                );

            for (
                var i = 0;
                i < materialIds.Count;
                i++
            )
            {
                ReleaseOwnedRoadSurfaceTextures(
                    materialIds[i]
                );
            }
        }
    }
}

