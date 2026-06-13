using UnityEngine;

namespace CityTimelineMod.Rendering.Materials
{
    internal static class OverlayMaterialFactory
    {
        internal static Material Create(Color color)
        {
            Shader shader = Shader.Find("Unlit/Transparent");

            if (shader == null)
                shader = Shader.Find("Sprites/Default");

            if (shader == null)
                shader = Shader.Find("Legacy Shaders/Transparent/Diffuse");

            if (shader == null)
                shader = Shader.Find("Legacy Shaders/Transparent/VertexLit");

            if (shader == null)
                shader = Shader.Find("Hidden/Internal-Colored");

            if (shader == null)
                shader = Shader.Find("Standard");

            if (shader == null)
                shader = Shader.Find("HDRP/Unlit");

            if (shader == null)
                return null;

            var material = new Material(shader);
            ApplyTransparentSettings(material, color);
            return material;
        }

        internal static void ApplyTransparentSettings(Material material, Color color)
        {
            if (material == null)
                return;

            material.color = color;

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);

            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);

            if (material.HasProperty("_SurfaceType"))
                material.SetFloat("_SurfaceType", 1f);

            if (material.HasProperty("_BlendMode"))
                material.SetFloat("_BlendMode", 0f);

            if (material.HasProperty("_Surface"))
                material.SetFloat("_Surface", 1f);

            if (material.HasProperty("_Blend"))
                material.SetFloat("_Blend", 0f);

            if (material.HasProperty("_Mode"))
                material.SetFloat("_Mode", 3f);

            if (material.HasProperty("_SrcBlend"))
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);

            if (material.HasProperty("_DstBlend"))
                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);

            if (material.HasProperty("_ZWrite"))
                material.SetFloat("_ZWrite", 0f);

            if (material.HasProperty("_AlphaCutoffEnable"))
                material.SetFloat("_AlphaCutoffEnable", 0f);

            material.SetOverrideTag("RenderType", "Transparent");

            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
    }
}
