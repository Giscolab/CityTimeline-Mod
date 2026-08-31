using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using CityTimelineMod.Util;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace CityTimelineMod.Rendering.Materials
{
    internal static class RoadSurfaceMaterialFactory
    {
        private static readonly string[] ShaderCandidates =
        {
            "HDRP/Lit",
            "BH/SG_BaseShader",
            "BH/SG_DefaultShader"
        };

        private sealed class EmbeddedTextureMap
        {
            internal string ResourceName;
            internal string PropertyName;
        }

        internal static RoadSurfaceMaterialBundle Create(
            Assembly assembly,
            string embeddedFolder,
            Color tint
        )
        {
            if (assembly == null)
                throw new ArgumentNullException(nameof(assembly));

            if (string.IsNullOrWhiteSpace(embeddedFolder))
                throw new ArgumentException(
                    "Embedded texture folder is required.",
                    nameof(embeddedFolder)
                );

            var maps = DiscoverTextureMaps(
                assembly,
                embeddedFolder
            );

            if (maps.Count == 0)
            {
                throw new InvalidOperationException(
                    "No embedded material maps were found under " +
                    embeddedFolder
                );
            }

            var shader = FindCompatibleShader(maps);

            if (shader == null)
            {
                var properties = new List<string>();

                for (var i = 0; i < maps.Count; i++)
                    properties.Add(maps[i].PropertyName);

                throw new InvalidOperationException(
                    "No supported road-surface shader can consume every " +
                    "texture map. Required properties=" +
                    string.Join(",", properties)
                );
            }

            Material material = null;
            var textures = new List<Texture2D>();
            var boundProperties = new List<string>();

            try
            {
                material = new Material(shader)
                {
                    name =
                        "CityTimelineMod_RoadSurface_" +
                        shader.name.Replace('/', '_'),
                    hideFlags = HideFlags.HideAndDontSave
                };

                ConfigureTransparentRoadSurface(
                    material,
                    tint
                );

                var metadataProperties =
                    ApplyEmbeddedMetadata(
                        assembly,
                        embeddedFolder,
                        material
                    );

                for (var i = 0; i < maps.Count; i++)
                {
                    var map = maps[i];

                    var texture = LoadTexture(
                        assembly,
                        map.ResourceName,
                        map.PropertyName
                    );

                    material.SetTexture(
                        map.PropertyName,
                        texture
                    );

                    textures.Add(texture);
                    boundProperties.Add(map.PropertyName);
                }

                ApplyTextureDependentDefaults(
                    material,
                    boundProperties,
                    metadataProperties
                );

                HDMaterial.ValidateMaterial(material);

                // ValidateMaterial établit les passes/keywords HDRP.
                // Le ribbon CTM reste volontairement dans la file transparente.
                material.renderQueue =
                    (int)RenderQueue.Transparent;

                material.SetOverrideTag(
                    "RenderType",
                    "Transparent"
                );

                for (var i = 0; i < maps.Count; i++)
                {
                    if (
                        material.GetTexture(
                            maps[i].PropertyName
                        ) == null
                    )
                    {
                        throw new InvalidOperationException(
                            "Texture binding lost after material validation: " +
                            maps[i].PropertyName
                        );
                    }
                }

                Log.Info(
                    "[RoadSurfaceMaterial] created: shader=" +
                    shader.name +
                    ", maps=" +
                    maps.Count +
                    ", properties=" +
                    string.Join(",", boundProperties) +
                    ", queue=" +
                    material.renderQueue
                );

                return new RoadSurfaceMaterialBundle(
                    material,
                    textures,
                    boundProperties
                );
            }
            catch
            {
                if (material != null)
                    UnityEngine.Object.Destroy(material);

                for (var i = 0; i < textures.Count; i++)
                {
                    if (textures[i] != null)
                        UnityEngine.Object.Destroy(textures[i]);
                }

                throw;
            }
        }

        private static List<EmbeddedTextureMap>
            DiscoverTextureMaps(
                Assembly assembly,
                string embeddedFolder
            )
        {
            var result =
                new List<EmbeddedTextureMap>();

            var normalized =
                NormalizeResourceFolder(
                    embeddedFolder
                );

            var marker =
                "." + normalized + ".";

            var resources =
                assembly.GetManifestResourceNames();

            for (var i = 0; i < resources.Length; i++)
            {
                var resourceName = resources[i];

                var markerIndex =
                    resourceName.IndexOf(
                        marker,
                        StringComparison.OrdinalIgnoreCase
                    );

                if (markerIndex < 0)
                    continue;

                var tail = resourceName.Substring(
                    markerIndex + marker.Length
                );

                if (
                    !tail.EndsWith(
                        ".png",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    continue;
                }

                var propertyName =
                    tail.Substring(
                        0,
                        tail.Length - 4
                    );

                // Les maps matériau CTM suivent directement le nom
                // de propriété shader : _BaseColorMap.png,
                // _NormalMap.png, _MaskMap.png, etc.
                if (
                    !propertyName.StartsWith(
                        "_",
                        StringComparison.Ordinal
                    )
                )
                {
                    continue;
                }

                // Un sous-dossier supplémentaire ne doit jamais être
                // confondu avec un nom de propriété.
                if (propertyName.IndexOf('.') >= 0)
                    continue;

                result.Add(
                    new EmbeddedTextureMap
                    {
                        ResourceName = resourceName,
                        PropertyName = propertyName
                    }
                );
            }

            result.Sort(
                (a, b) =>
                    string.Compare(
                        a.PropertyName,
                        b.PropertyName,
                        StringComparison.OrdinalIgnoreCase
                    )
            );

            return result;
        }

        private static Shader FindCompatibleShader(
            List<EmbeddedTextureMap> maps
        )
        {
            for (
                var shaderIndex = 0;
                shaderIndex < ShaderCandidates.Length;
                shaderIndex++
            )
            {
                var shaderName =
                    ShaderCandidates[shaderIndex];

                var shader =
                    Shader.Find(shaderName);

                if (
                    shader == null ||
                    !shader.isSupported
                )
                {
                    continue;
                }

                var supportsEveryMap = true;

                for (
                    var mapIndex = 0;
                    mapIndex < maps.Count;
                    mapIndex++
                )
                {
                    if (
                        !ShaderHasProperty(
                            shader,
                            maps[mapIndex].PropertyName
                        )
                    )
                    {
                        supportsEveryMap = false;
                        break;
                    }
                }

                if (supportsEveryMap)
                    return shader;
            }

            return null;
        }

        private static bool ShaderHasProperty(
            Shader shader,
            string propertyName
        )
        {
            if (
                shader == null ||
                string.IsNullOrWhiteSpace(propertyName)
            )
            {
                return false;
            }

            var count =
                shader.GetPropertyCount();

            for (var i = 0; i < count; i++)
            {
                if (
                    string.Equals(
                        shader.GetPropertyName(i),
                        propertyName,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    return true;
                }
            }

            return false;
        }

        private static Texture2D LoadTexture(
            Assembly assembly,
            string resourceName,
            string propertyName
        )
        {
            byte[] bytes;

            using (
                var stream =
                    assembly.GetManifestResourceStream(
                        resourceName
                    )
            )
            {
                if (stream == null)
                {
                    throw new InvalidOperationException(
                        "Embedded texture stream not found: " +
                        resourceName
                    );
                }

                using (
                    var memory =
                        new MemoryStream()
                )
                {
                    stream.CopyTo(memory);
                    bytes = memory.ToArray();
                }
            }

            var linear =
                !IsColorTextureProperty(
                    propertyName
                );

            var texture =
                new Texture2D(
                    2,
                    2,
                    TextureFormat.RGBA32,
                    true,
                    linear
                )
                {
                    name =
                        "CityTimelineMod_" +
                        propertyName,
                    wrapMode =
                        TextureWrapMode.Repeat,
                    filterMode =
                        FilterMode.Trilinear,
                    anisoLevel = 8,
                    hideFlags =
                        HideFlags.HideAndDontSave
                };

            if (
                !ImageConversion.LoadImage(
                    texture,
                    bytes,
                    false
                )
            )
            {
                UnityEngine.Object.Destroy(texture);

                throw new InvalidOperationException(
                    "Failed to decode embedded texture: " +
                    resourceName
                );
            }

            Log.Info(
                "[RoadSurfaceTexture] loaded: property=" +
                propertyName +
                ", resource=" +
                resourceName +
                ", size=" +
                texture.width +
                "x" +
                texture.height +
                ", linear=" +
                linear +
                ", wrap=" +
                texture.wrapMode
            );

            return texture;
        }

        private static bool IsColorTextureProperty(
            string propertyName
        )
        {
            if (string.IsNullOrWhiteSpace(propertyName))
                return false;

            return
                propertyName.IndexOf(
                    "BaseColor",
                    StringComparison.OrdinalIgnoreCase
                ) >= 0 ||
                propertyName.IndexOf(
                    "EmissiveColor",
                    StringComparison.OrdinalIgnoreCase
                ) >= 0 ||
                propertyName.IndexOf(
                    "SpecularColor",
                    StringComparison.OrdinalIgnoreCase
                ) >= 0 ||
                propertyName.IndexOf(
                    "TransmittanceColor",
                    StringComparison.OrdinalIgnoreCase
                ) >= 0;
        }

        private static void
            ConfigureTransparentRoadSurface(
                Material material,
                Color tint
            )
        {
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor(
                    "_BaseColor",
                    tint
                );
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor(
                    "_Color",
                    tint
                );
            }

            SetFloatIfPresent(
                material,
                "_SurfaceType",
                1f
            );

            SetFloatIfPresent(
                material,
                "_BlendMode",
                0f
            );

            SetFloatIfPresent(
                material,
                "_Surface",
                1f
            );

            SetFloatIfPresent(
                material,
                "_Blend",
                0f
            );

            SetFloatIfPresent(
                material,
                "_SrcBlend",
                (float)BlendMode.SrcAlpha
            );

            SetFloatIfPresent(
                material,
                "_DstBlend",
                (float)BlendMode.OneMinusSrcAlpha
            );

            SetFloatIfPresent(
                material,
                "_ZWrite",
                0f
            );

            SetFloatIfPresent(
                material,
                "_TransparentZWrite",
                0f
            );

            SetFloatIfPresent(
                material,
                "_AlphaCutoffEnable",
                0f
            );

            // Le ribbon doit rester visible quel que soit le winding
            // de la portion de polyline.
            SetFloatIfPresent(
                material,
                "_DoubleSidedEnable",
                1f
            );

            SetFloatIfPresent(
                material,
                "_CullMode",
                (float)CullMode.Off
            );

            SetFloatIfPresent(
                material,
                "_CullModeForward",
                (float)CullMode.Off
            );

            SetFloatIfPresent(
                material,
                "_TransparentCullMode",
                (float)CullMode.Off
            );

            SetFloatIfPresent(
                material,
                "_TransparentSortPriority",
                0f
            );

            material.SetOverrideTag(
                "RenderType",
                "Transparent"
            );

            material.renderQueue =
                (int)RenderQueue.Transparent;

            material.enableInstancing = true;
        }

        private static HashSet<string>
            ApplyEmbeddedMetadata(
                Assembly assembly,
                string embeddedFolder,
                Material material
            )
        {
            var applied =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase
                );

            var normalized =
                NormalizeResourceFolder(
                    embeddedFolder
                );

            var marker =
                "." + normalized + ".";

            var resources =
                assembly.GetManifestResourceNames();

            for (var i = 0; i < resources.Length; i++)
            {
                var resourceName =
                    resources[i];

                var markerIndex =
                    resourceName.IndexOf(
                        marker,
                        StringComparison.OrdinalIgnoreCase
                    );

                if (markerIndex < 0)
                    continue;

                var tail =
                    resourceName.Substring(
                        markerIndex + marker.Length
                    );

                if (
                    !tail.EndsWith(
                        ".json",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    continue;
                }

                string json;

                using (
                    var stream =
                        assembly.GetManifestResourceStream(
                            resourceName
                        )
                )
                {
                    if (stream == null)
                    {
                        throw new InvalidOperationException(
                            "Embedded metadata stream not found: " +
                            resourceName
                        );
                    }

                    using (
                        var reader =
                            new StreamReader(stream)
                    )
                    {
                        json =
                            reader.ReadToEnd();
                    }
                }

                var root =
                    JObject.Parse(json);

                ApplyFloatSection(
                    root["Float"] as JObject,
                    material,
                    applied
                );

                ApplyIntSection(
                    root["Int"] as JObject,
                    material,
                    applied
                );

                ApplyVectorSection(
                    root["Vector"] as JObject,
                    material,
                    applied
                );

                ApplyColorSection(
                    root["Color"] as JObject,
                    material,
                    applied
                );
            }

            return applied;
        }

        private static void ApplyFloatSection(
            JObject section,
            Material material,
            HashSet<string> applied
        )
        {
            if (section == null)
                return;

            foreach (
                var property in section.Properties()
            )
            {
                var value =
                    property.Value.Value<float>();

                if (
                    TrySetFloatWithAliases(
                        material,
                        property.Name,
                        value,
                        applied
                    )
                )
                {
                    continue;
                }
            }
        }

        private static bool TrySetFloatWithAliases(
            Material material,
            string propertyName,
            float value,
            HashSet<string> applied
        )
        {
            if (material.HasProperty(propertyName))
            {
                material.SetFloat(
                    propertyName,
                    value
                );

                applied.Add(propertyName);
                return true;
            }

            // Mike/CS2 decal metadata calls this opacity.
            // HDRP/Lit exposes the equivalent tangent normal scale.
            if (
                string.Equals(
                    propertyName,
                    "_NormalOpacity",
                    StringComparison.OrdinalIgnoreCase
                ) &&
                material.HasProperty("_NormalScale")
            )
            {
                material.SetFloat(
                    "_NormalScale",
                    value
                );

                applied.Add("_NormalScale");
                return true;
            }

            return false;
        }

        private static void ApplyIntSection(
            JObject section,
            Material material,
            HashSet<string> applied
        )
        {
            if (section == null)
                return;

            foreach (
                var property in section.Properties()
            )
            {
                if (
                    !material.HasProperty(
                        property.Name
                    )
                )
                {
                    continue;
                }

                material.SetInt(
                    property.Name,
                    property.Value.Value<int>()
                );

                applied.Add(property.Name);
            }
        }

        private static void ApplyVectorSection(
            JObject section,
            Material material,
            HashSet<string> applied
        )
        {
            if (section == null)
                return;

            foreach (
                var property in section.Properties()
            )
            {
                if (
                    !material.HasProperty(
                        property.Name
                    )
                )
                {
                    continue;
                }

                Vector4 vector;

                if (
                    !TryReadVector4(
                        property.Value,
                        out vector
                    )
                )
                {
                    continue;
                }

                material.SetVector(
                    property.Name,
                    vector
                );

                applied.Add(property.Name);
            }
        }

        private static void ApplyColorSection(
            JObject section,
            Material material,
            HashSet<string> applied
        )
        {
            if (section == null)
                return;

            foreach (
                var property in section.Properties()
            )
            {
                if (
                    !material.HasProperty(
                        property.Name
                    )
                )
                {
                    continue;
                }

                Vector4 vector;

                if (
                    !TryReadVector4(
                        property.Value,
                        out vector
                    )
                )
                {
                    continue;
                }

                material.SetColor(
                    property.Name,
                    new Color(
                        vector.x,
                        vector.y,
                        vector.z,
                        vector.w
                    )
                );

                applied.Add(property.Name);
            }
        }

        private static bool TryReadVector4(
            JToken token,
            out Vector4 value
        )
        {
            var obj = token as JObject;

            if (obj != null)
            {
                value =
                    new Vector4(
                        ReadFloat(
                            obj["x"] ?? obj["r"],
                            0f
                        ),
                        ReadFloat(
                            obj["y"] ?? obj["g"],
                            0f
                        ),
                        ReadFloat(
                            obj["z"] ?? obj["b"],
                            0f
                        ),
                        ReadFloat(
                            obj["w"] ?? obj["a"],
                            1f
                        )
                    );

                return true;
            }

            var array = token as JArray;

            if (
                array != null &&
                array.Count >= 3
            )
            {
                value =
                    new Vector4(
                        ReadFloat(array[0], 0f),
                        ReadFloat(array[1], 0f),
                        ReadFloat(array[2], 0f),
                        array.Count >= 4
                            ? ReadFloat(array[3], 1f)
                            : 1f
                    );

                return true;
            }

            value = default(Vector4);
            return false;
        }

        private static float ReadFloat(
            JToken token,
            float fallback
        )
        {
            if (token == null)
                return fallback;

            return token.Value<float>();
        }

        private static void
            ApplyTextureDependentDefaults(
                Material material,
                List<string> boundProperties,
                HashSet<string> metadataProperties
            )
        {
            var hasNormal = false;
            var hasEmissive = false;

            for (
                var i = 0;
                i < boundProperties.Count;
                i++
            )
            {
                var property =
                    boundProperties[i];

                if (
                    property.IndexOf(
                        "Normal",
                        StringComparison.OrdinalIgnoreCase
                    ) >= 0
                )
                {
                    hasNormal = true;
                }

                if (
                    property.IndexOf(
                        "EmissiveColorMap",
                        StringComparison.OrdinalIgnoreCase
                    ) >= 0
                )
                {
                    hasEmissive = true;
                }
            }

            if (
                hasNormal &&
                material.HasProperty("_NormalScale") &&
                !metadataProperties.Contains("_NormalScale")
            )
            {
                material.SetFloat(
                    "_NormalScale",
                    1f
                );
            }

            if (
                hasNormal &&
                material.HasProperty("_NormalMapSpace")
            )
            {
                // 0 = tangent space dans HDRP/Lit.
                material.SetFloat(
                    "_NormalMapSpace",
                    0f
                );
            }

            if (
                hasEmissive &&
                material.HasProperty("_EmissiveColor")
            )
            {
                material.SetColor(
                    "_EmissiveColor",
                    Color.white
                );
            }

            if (
                hasEmissive &&
                material.HasProperty("_EmissiveIntensity")
            )
            {
                material.SetFloat(
                    "_EmissiveIntensity",
                    1f
                );
            }
        }

        private static void SetFloatIfPresent(
            Material material,
            string propertyName,
            float value
        )
        {
            if (
                material.HasProperty(propertyName)
            )
            {
                material.SetFloat(
                    propertyName,
                    value
                );
            }
        }

        private static string
            NormalizeResourceFolder(
                string embeddedFolder
            )
        {
            return embeddedFolder
                .Trim()
                .Trim('.')
                .Replace('\\', '.')
                .Replace('/', '.');
        }
    }
}
