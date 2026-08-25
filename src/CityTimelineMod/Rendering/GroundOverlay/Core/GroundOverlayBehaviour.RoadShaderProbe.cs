using System;
using System.Collections.Generic;
using System.Text;
using CityTimelineMod.Util;
using UnityEngine;
using UnityEngine.Rendering;

namespace CityTimelineMod.Rendering
{
    internal sealed partial class GroundOverlayBehaviour
    {
        // Gardes pour les sondes (une fois suffit, même après rebuild)
        private bool _roadSurfaceShaderProbeCompleted;
        private bool _nativeNetCompositionMaterialProbeCompleted;

        // -----------------------------------------------------------------
        // Sonde n°1 : shaders HDRP/Standard pour trouver les propriétés de texture
        // -----------------------------------------------------------------
        private void ProbeRoadSurfaceShadersOnce()
        {
            if (_roadSurfaceShaderProbeCompleted)
                return;

            _roadSurfaceShaderProbeCompleted = true;

            try
            {
                var inspected = new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase
                );

                var candidateCount = 0;
                var inspectedCount = 0;

                // Candidats connus que l'on veut tester explicitement,
                // même s'ils ne sont pas encore présents dans Resources.
                var directCandidates = new[]
                {
                    "HDRP/Lit",
                    "HDRP/LayeredLit",
                    "HDRP/Decal",
                    "HDRP/Unlit",
                    "Standard",
                    "Legacy Shaders/Bumped Diffuse",
                    "Legacy Shaders/Transparent/Bumped Diffuse",
                    "Sprites/Default"
                };

                for (var i = 0; i < directCandidates.Length; i++)
                {
                    var shader = Shader.Find(directCandidates[i]);

                    if (shader == null)
                    {
                        Log.Info(
                            "[RoadShaderProbe] find=null name=" +
                            directCandidates[i]
                        );
                        continue;
                    }

                    InspectRoadSurfaceShader(
                        shader,
                        inspected,
                        ref inspectedCount,
                        ref candidateCount
                    );
                }

                // Ensuite on regarde les shaders réellement chargés par CS2.
                var loadedShaders =
                    Resources.FindObjectsOfTypeAll<Shader>();

                for (var i = 0; i < loadedShaders.Length; i++)
                {
                    InspectRoadSurfaceShader(
                        loadedShaders[i],
                        inspected,
                        ref inspectedCount,
                        ref candidateCount
                    );
                }

                Log.Info(
                    "[RoadShaderProbe] summary: inspected=" +
                    inspectedCount +
                    ", candidatesWithBaseAndNormal=" +
                    candidateCount
                );
            }
            catch (Exception ex)
            {
                Log.Error(
                    "[RoadShaderProbe] failed: " + ex
                );
            }
        }

        private static void InspectRoadSurfaceShader(
            Shader shader,
            HashSet<string> inspected,
            ref int inspectedCount,
            ref int candidateCount
        )
        {
            if (shader == null)
                return;

            var shaderName = shader.name ?? "(unnamed)";

            if (!inspected.Add(shaderName))
                return;

            inspectedCount++;

            string baseTextureProperty = null;
            string normalTextureProperty = null;
            string maskTextureProperty = null;

            var textureProperties = new StringBuilder();

            var propertyCount = shader.GetPropertyCount();

            for (var i = 0; i < propertyCount; i++)
            {
                if (
                    shader.GetPropertyType(i) !=
                    ShaderPropertyType.Texture
                )
                {
                    continue;
                }

                var propertyName =
                    shader.GetPropertyName(i);

                if (string.IsNullOrWhiteSpace(propertyName))
                    continue;

                if (textureProperties.Length > 0)
                    textureProperties.Append(',');

                textureProperties.Append(propertyName);

                if (
                    baseTextureProperty == null &&
                    IsBaseTextureProperty(propertyName)
                )
                {
                    baseTextureProperty = propertyName;
                }

                if (
                    normalTextureProperty == null &&
                    IsNormalTextureProperty(propertyName)
                )
                {
                    normalTextureProperty = propertyName;
                }

                if (
                    maskTextureProperty == null &&
                    IsMaskTextureProperty(propertyName)
                )
                {
                    maskTextureProperty = propertyName;
                }
            }

            // Pour la NormalMap, seuls les shaders possédant réellement
            // une texture de base ET une texture normale nous intéressent.
            if (
                baseTextureProperty == null ||
                normalTextureProperty == null
            )
            {
                return;
            }

            candidateCount++;

            Log.Info(
                "[RoadShaderProbe] candidate=" +
                shaderName +
                ", supported=" +
                shader.isSupported +
                ", base=" +
                baseTextureProperty +
                ", normal=" +
                normalTextureProperty +
                ", mask=" +
                (maskTextureProperty ?? "(none)") +
                ", textureProperties=" +
                textureProperties
            );
        }

        private static bool IsBaseTextureProperty(
            string propertyName
        )
        {
            return
                propertyName.Equals(
                    "_BaseColorMap",
                    StringComparison.OrdinalIgnoreCase
                ) ||
                propertyName.Equals(
                    "_BaseMap",
                    StringComparison.OrdinalIgnoreCase
                ) ||
                propertyName.Equals(
                    "_MainTex",
                    StringComparison.OrdinalIgnoreCase
                ) ||
                propertyName.Equals(
                    "_UnlitColorMap",
                    StringComparison.OrdinalIgnoreCase
                ) ||
                propertyName.IndexOf(
                    "BaseColorMap",
                    StringComparison.OrdinalIgnoreCase
                ) >= 0 ||
                propertyName.IndexOf(
                    "BaseMap",
                    StringComparison.OrdinalIgnoreCase
                ) >= 0;
        }

        private static bool IsNormalTextureProperty(
            string propertyName
        )
        {
            return
                propertyName.IndexOf(
                    "Normal",
                    StringComparison.OrdinalIgnoreCase
                ) >= 0 &&
                propertyName.IndexOf(
                    "Scale",
                    StringComparison.OrdinalIgnoreCase
                ) < 0;
        }

        private static bool IsMaskTextureProperty(
            string propertyName
        )
        {
            return
                propertyName.IndexOf(
                    "Mask",
                    StringComparison.OrdinalIgnoreCase
                ) >= 0;
        }

        // -----------------------------------------------------------------
        // Sonde n°2 : matériaux BH/NetCompositionMeshLitShader déjà chargés
        // -----------------------------------------------------------------
        private void ProbeNativeNetCompositionMaterials()
        {
            if (_nativeNetCompositionMaterialProbeCompleted)
                return;

            _nativeNetCompositionMaterialProbeCompleted = true;

            const string targetShaderName =
                "BH/NetCompositionMeshLitShader";

            try
            {
                var materials =
                    Resources.FindObjectsOfTypeAll<Material>();

                var found = 0;

                for (var i = 0; i < materials.Length; i++)
                {
                    var material = materials[i];

                    if (
                        material == null ||
                        material.shader == null ||
                        !string.Equals(
                            material.shader.name,
                            targetShaderName,
                            StringComparison.Ordinal
                        )
                    )
                    {
                        continue;
                    }

                    found++;

                    var keywords = material.shaderKeywords != null
                        ? string.Join(",", material.shaderKeywords)
                        : "(none)";

                    Log.Info(
                        "[RoadNativeMaterialProbe] " +
                        "name=" + material.name +
                        ", shader=" + material.shader.name +
                        ", queue=" + material.renderQueue +
                        ", keywords=" + keywords +
                        ", base=" +
                        DescribeTexture(material, "_BaseColorMap") +
                        ", normal=" +
                        DescribeTexture(material, "_NormalMap") +
                        ", mask=" +
                        DescribeTexture(material, "_MaskMap") +
                        ", worldAlbedo=" +
                        DescribeTexture(material, "_WorldspaceAlbedo") +
                        ", worldNormal=" +
                        DescribeTexture(material, "_WorldspaceNormalMap")
                    );

                    // Quelques exemplaires suffisent.
                    if (found >= 8)
                        break;
                }

                Log.Info(
                    "[RoadNativeMaterialProbe] summary: found=" +
                    found
                );
            }
            catch (Exception ex)
            {
                Log.Error(
                    "[RoadNativeMaterialProbe] failed: " + ex
                );
            }
        }

        private static string DescribeTexture(
            Material material,
            string propertyName
        )
        {
            if (
                material == null ||
                !material.HasProperty(propertyName)
            )
            {
                return "(property-absent)";
            }

            var texture = material.GetTexture(propertyName);

            if (texture == null)
                return "(null)";

            return
                texture.name +
                "[" +
                texture.width +
                "x" +
                texture.height +
                "]";
        }
    }
}