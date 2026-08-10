using UnityEngine;

namespace CityTimelineMod.Rendering.Core
{
    internal static class OverlayPrimitiveUtil
    {
        internal static void RemoveCollider(GameObject obj)
        {
            var collider = obj.GetComponent<Collider>();
            if (collider != null)
                UnityEngine.Object.Destroy(collider);
        }

        internal static void ApplyMaterial(GameObject obj, Material material)
        {
            var renderer = obj.GetComponent<Renderer>();
            if (renderer != null && material != null)
                renderer.sharedMaterial = material;
        }
    }
}
