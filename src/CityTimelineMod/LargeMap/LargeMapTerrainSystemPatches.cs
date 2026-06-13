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
        [HarmonyPatch("FinalizeTerrainData")]
        [HarmonyPrefix]
        private static void FinalizeTerrainData_Prefix(
            ref float2 inMapCorner,
            ref float2 inMapSize,
            ref float2 inWorldCorner,
            ref float2 inWorldSize)
        {
            if (math.abs(inMapSize.x - CityTimelineLargeMapState.OriginalMapSizeMetersFloat) >= 1f)
                return;

            inMapSize *= CityTimelineLargeMapState.CoreValue;
            inWorldSize *= CityTimelineLargeMapState.CoreValue;
            inMapCorner = -0.5f * inMapSize;
            inWorldCorner = -0.5f * inWorldSize;

            Util.Log.Info(
                "[LargeMap] TerrainSystem.FinalizeTerrainData patched. mapSize=" +
                inMapSize.x +
                ", worldSize=" +
                inWorldSize.x
            );
        }

        [HarmonyPatch(nameof(TerrainSystem.GetTerrainBounds))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> GetTerrainBounds_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return ReplaceMapSizeConstants(instructions, "TerrainSystem.GetTerrainBounds");
        }

        [HarmonyPatch(nameof(TerrainSystem.GetHeightData))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> GetHeightData_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return ReplaceMapSizeConstants(instructions, "TerrainSystem.GetHeightData");
        }

        private static IEnumerable<CodeInstruction> ReplaceMapSizeConstants(IEnumerable<CodeInstruction> instructions, string methodName)
        {
            var patches = 0;
            var codes = new List<CodeInstruction>(instructions);

            for (var i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Ldc_R4 &&
                    codes[i].operand is float &&
                    Math.Abs((float)codes[i].operand - CityTimelineLargeMapState.OriginalMapSizeMetersFloat) < 0.01f)
                {
                    codes[i].operand = CityTimelineLargeMapState.MapSizeMetersFloat;
                    patches++;
                }
            }

            if (patches == 0)
                Util.Log.Error("[LargeMap] " + methodName + " transpiler found no 14336f constants.");
            else
                Util.Log.Info("[LargeMap] " + methodName + " transpiler patched constants=" + patches);

            return codes;
        }
    }
}
