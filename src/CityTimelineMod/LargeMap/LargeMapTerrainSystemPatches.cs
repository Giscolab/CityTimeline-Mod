using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using Game.Simulation;
using HarmonyLib;
using Unity.Mathematics;

namespace CityTimelineMod.LargeMap
{
    [HarmonyPatch(typeof(TerrainSystem))]
    internal static class LargeMapTerrainSystemPatches
    {
        private static bool _finalizeLogged;

        [HarmonyPatch("FinalizeTerrainData")]
        [HarmonyPrefix]
        private static void FinalizeTerrainData_Prefix(
            ref float2 inMapCorner,
            ref float2 inMapSize,
            ref float2 inWorldCorner,
            ref float2 inWorldSize)
        {
            if (!CityTimelineLargeMapState.Enabled)
                return;

            bool recognizedMapSize =
                Approximately(inMapSize.x, CityTimelineLargeMapState.OriginalMapSizeMetersFloat) ||
                Approximately(inMapSize.x, CityTimelineLargeMapState.MapSizeMetersFloat);

            bool recognizedWorldSize =
                Approximately(inWorldSize.x, CityTimelineLargeMapState.OriginalMapSizeMetersFloat) ||
                Approximately(inWorldSize.x, CityTimelineLargeMapState.MapSizeMetersFloat) ||
                Approximately(
                    inWorldSize.x,
                    CityTimelineLargeMapState.MapSizeMetersFloat * CityTimelineLargeMapState.CoreValue
                );

            if (!recognizedMapSize && !recognizedWorldSize)
            {
                Util.Log.Error(
                    "[LargeMap] FinalizeTerrainData received unsupported dimensions. " +
                    "mapSize=" + inMapSize + ", worldSize=" + inWorldSize +
                    ". Unified 57 km conversion skipped."
                );
                return;
            }

            float2 unifiedSize = new float2(
                CityTimelineLargeMapState.MapSizeMetersFloat,
                CityTimelineLargeMapState.MapSizeMetersFloat
            );

            float2 unifiedCorner = -0.5f * unifiedSize;

            // Solution A : le terrain principal et le monde ont exactement
            // la même emprise. TerrainSystem choisit alors baseLod = 0 et
            // n'utilise plus le backdrop comme seconde source de hauteur.
            inMapSize = unifiedSize;
            inWorldSize = unifiedSize;
            inMapCorner = unifiedCorner;
            inWorldCorner = unifiedCorner;

            if (!_finalizeLogged)
            {
                _finalizeLogged = true;

                Util.Log.Info(
                    "[LargeMap] unified terrain authority enabled. " +
                    "mapSize=" + inMapSize.x +
                    ", worldSize=" + inWorldSize.x +
                    ", corner=" + inMapCorner
                );
            }
        }

        [HarmonyPatch(nameof(TerrainSystem.GetTerrainBounds))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> GetTerrainBounds_Transpiler(
            IEnumerable<CodeInstruction> instructions)
        {
            return ReplaceMapSizeConstants(
                instructions,
                "TerrainSystem.GetTerrainBounds"
            );
        }

        [HarmonyPatch(nameof(TerrainSystem.GetHeightData))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> GetHeightData_Transpiler(
            IEnumerable<CodeInstruction> instructions)
        {
            return ReplaceMapSizeConstants(
                instructions,
                "TerrainSystem.GetHeightData"
            );
        }

        private static IEnumerable<CodeInstruction> ReplaceMapSizeConstants(
            IEnumerable<CodeInstruction> instructions,
            string methodName)
        {
            int patches = 0;
            var codes = new List<CodeInstruction>(instructions);

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode != OpCodes.Ldc_R4 || !(codes[i].operand is float))
                    continue;

                float value = (float)codes[i].operand;

                if (!Approximately(value, CityTimelineLargeMapState.OriginalMapSizeMetersFloat))
                    continue;

                codes[i].operand = CityTimelineLargeMapState.MapSizeMetersFloat;
                patches++;
            }

            if (patches == 0)
            {
                throw new InvalidOperationException(
                    "[LargeMap] " + methodName +
                    " transpiler found no 14336f constant. Game.dll changed."
                );
            }

            Util.Log.Info(
                "[LargeMap] " + methodName +
                " transpiler patched constants=" + patches
            );

            return codes;
        }

        private static bool Approximately(float a, float b)
        {
            return math.abs(a - b) < 0.01f;
        }
    }
}
