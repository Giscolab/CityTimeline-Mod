using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Colossal.Mathematics;
using Game.Simulation;
using Game.Tools;
using HarmonyLib;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace CityTimelineMod.PlayableWorld
{
    /*
     * NetToolSystem.SnapJob.HandleWorldSize utilise
     * TerrainUtils.GetBounds(m_TerrainHeightData).
     *
     * Avec les données vanilla détaillées, ces limites valent
     * environ -7168 / +7168 et le job ramène automatiquement
     * le point routier sur cette bordure.
     *
     * Lorsque le dernier point brut est hors de la heightmap
     * détaillée mais reste dans la worldmap, on fournit au job
     * une vue locale du backdrop comme heightmap principale.
     *
     * Aucun champ du TerrainSystem n'est modifié.
     */
    [HarmonyPatch]
    internal static class NetToolSystemSnapControlPointsPatch
    {
        [ThreadStatic]
        private static bool s_UseBackdropAsPrimary;

        private static bool s_BackdropLogged;

        private static readonly AccessTools.FieldRef<
            NetToolSystem,
            NativeList<ControlPoint>
        > ControlPointsField =
            AccessTools.FieldRefAccess<
                NetToolSystem,
                NativeList<ControlPoint>
            >("m_ControlPoints");

        private static readonly AccessTools.FieldRef<
            NetToolSystem,
            TerrainSystem
        > TerrainSystemField =
            AccessTools.FieldRefAccess<
                NetToolSystem,
                TerrainSystem
            >("m_TerrainSystem");

        private static MethodBase TargetMethod()
        {
            MethodInfo method = AccessTools.Method(
                typeof(NetToolSystem),
                "SnapControlPoints",
                new[]
                {
                    typeof(JobHandle),
                    typeof(bool)
                }
            );

            if (method == null)
            {
                throw new MissingMethodException(
                    "NetToolSystem.SnapControlPoints(JobHandle, bool)"
                );
            }

            return method;
        }

        [HarmonyPrefix]
        private static void Prefix(
            NetToolSystem __instance,
            out bool __state
        )
        {
            __state = ShouldUseBackdrop(__instance);
            s_UseBackdropAsPrimary = __state;
        }

        [HarmonyPostfix]
        private static void Postfix(
            NetToolSystem __instance,
            JobHandle __result,
            bool __state
        )
        {
            try
            {
                if (__state)
                {
                    TerrainSystem terrainSystem =
                        TerrainSystemField(__instance);

                    if (terrainSystem != null)
                    {
                        terrainSystem.AddCPUDownsampleHeightReader(
                            __result
                        );
                    }
                }
            }
            finally
            {
                s_UseBackdropAsPrimary = false;
            }
        }

        [HarmonyFinalizer]
        private static Exception Finalizer(
            Exception __exception
        )
        {
            s_UseBackdropAsPrimary = false;
            return __exception;
        }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(
            IEnumerable<CodeInstruction> instructions
        )
        {
            MethodInfo original = AccessTools.Method(
                typeof(TerrainSystem),
                nameof(TerrainSystem.GetHeightData),
                new[]
                {
                    typeof(bool)
                }
            );

            MethodInfo replacement = AccessTools.Method(
                typeof(NetToolSystemSnapControlPointsPatch),
                nameof(GetHeightDataForNetSnap)
            );

            if (original == null)
            {
                throw new MissingMethodException(
                    "TerrainSystem.GetHeightData(bool)"
                );
            }

            if (replacement == null)
            {
                throw new MissingMethodException(
                    nameof(GetHeightDataForNetSnap)
                );
            }

            List<CodeInstruction> result =
                new List<CodeInstruction>(instructions);

            int replacements = 0;

            foreach (CodeInstruction instruction in result)
            {
                if (!instruction.Calls(original))
                    continue;

                instruction.opcode = OpCodes.Call;
                instruction.operand = replacement;
                replacements++;
            }

            if (replacements != 1)
            {
                throw new InvalidOperationException(
                    "[PlayableWorld] expected one " +
                    "TerrainSystem.GetHeightData call in " +
                    "NetToolSystem.SnapControlPoints, found=" +
                    replacements
                );
            }

            return result;
        }

        /*
         * Signature volontairement identique à la méthode vanilla :
         *
         * instance TerrainSystem + bool -> TerrainHeightData
         *
         * Le transpiler peut donc remplacer directement le callvirt.
         */
        private static TerrainHeightData GetHeightDataForNetSnap(
            TerrainSystem terrainSystem,
            bool forceUpdate
        )
        {
            TerrainHeightData detailedData =
                terrainSystem.GetHeightData(forceUpdate);

            if (!s_UseBackdropAsPrimary)
                return detailedData;

            if (
                !detailedData.isCreated ||
                !detailedData.hasBackdrop ||
                !detailedData.downscaledHeights.IsCreated ||
                detailedData.downscaledHeights.Length == 0
            )
            {
                return detailedData;
            }

            int3 backdropResolution =
                detailedData.downScaledResolution;

            if (
                backdropResolution.x <= 1 ||
                backdropResolution.z <= 1
            )
            {
                return detailedData;
            }

            float3 backdropWorldScale =
                TerrainUtils.BackDropWorldSizeScale;

            float3 downscaleFactor = new float3(
                (float)detailedData.resolution.x /
                    backdropResolution.x,
                1f,
                (float)detailedData.resolution.z /
                    backdropResolution.z
            );

            float3 backdropScale =
                detailedData.scale /
                backdropWorldScale /
                downscaleFactor;

            float3 backdropOffset =
                detailedData.offset *
                backdropWorldScale;

            TerrainHeightData backdropData =
                new TerrainHeightData(
                    detailedData.downscaledHeights,
                    default(NativeArray<ushort>),
                    backdropResolution,
                    backdropScale,
                    backdropOffset,
                    false
                );

            if (!s_BackdropLogged)
            {
                s_BackdropLogged = true;

                Bounds3 bounds =
                    TerrainUtils.GetBounds(ref backdropData);

                Util.Log.Info(
                    "[PlayableWorld] road snap switched to " +
                    "backdrop terrain data. bounds min=" +
                    bounds.min.xz +
                    ", max=" +
                    bounds.max.xz
                );
            }

            return backdropData;
        }

        private static bool ShouldUseBackdrop(
            NetToolSystem netToolSystem
        )
        {
            if (!PlayableWorldState.Enabled)
                return false;

            if (!PlayableWorldState.Initialized)
                return false;

            TerrainSystem terrainSystem =
                TerrainSystemField(netToolSystem);

            if (terrainSystem == null)
                return false;

            NativeList<ControlPoint> controlPoints =
                ControlPointsField(netToolSystem);

            if (
                !controlPoints.IsCreated ||
                controlPoints.Length == 0
            )
            {
                return false;
            }

            TerrainHeightData detailedData =
                terrainSystem.GetHeightData(false);

            if (
                !detailedData.isCreated ||
                !detailedData.hasBackdrop ||
                !detailedData.downscaledHeights.IsCreated
            )
            {
                return false;
            }

            Bounds3 detailedBounds =
                TerrainUtils.GetBounds(ref detailedData);

            float3 position =
                controlPoints[
                    controlPoints.Length - 1
                ].m_HitPosition;

            bool outsideDetailed =
                position.x < detailedBounds.min.x ||
                position.x > detailedBounds.max.x ||
                position.z < detailedBounds.min.z ||
                position.z > detailedBounds.max.z;

            if (!outsideDetailed)
                return false;

            bool insidePlayableWorld =
                position.x >= PlayableWorldState.Min.x &&
                position.x <= PlayableWorldState.Max.x &&
                position.z >= PlayableWorldState.Min.y &&
                position.z <= PlayableWorldState.Max.y;

            return insidePlayableWorld;
        }
    }
}
