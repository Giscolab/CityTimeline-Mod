using System.Collections.Generic;
using UnityEngine;

namespace CityTimelineMod.Rendering.Materials
{
    internal sealed class RoadSurfaceMaterialBundle
    {
        internal Material Material { get; }
        internal List<Texture2D> Textures { get; }
        internal List<string> BoundTextureProperties { get; }

        internal RoadSurfaceMaterialBundle(
            Material material,
            List<Texture2D> textures,
            List<string> boundTextureProperties
        )
        {
            Material = material;
            Textures = textures ?? new List<Texture2D>();
            BoundTextureProperties =
                boundTextureProperties ?? new List<string>();
        }
    }
}
