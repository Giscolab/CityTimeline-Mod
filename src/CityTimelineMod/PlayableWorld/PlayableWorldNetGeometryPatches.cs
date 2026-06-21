using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace CityTimelineMod.PlayableWorld
{
    /*
     * Game.Net.GeometrySystem.OnUpdate transmet m_TerrainBounds au job
     * CalculateEdgeGeometryJob. Avec les bounds vanilla, la géométrie
     * réseau reste analysée autour du noyau 14,336 km.
     */
    [HarmonyPatch]
    internal static class NetGeometryPlayableBoundsPatch
    {
        private static MethodBase TargetMethod()
        {
            Type type = AccessTools.TypeByName(
                "Game.Net.GeometrySystem"
            );

            if (type == null)
            {
                throw new MissingMemberException(
                    "Game.Net.GeometrySystem"
                );
            }

            MethodInfo method = AccessTools.Method(
                type,
                "OnUpdate"
            );

            if (method == null)
            {
                throw new MissingMethodException(
                    "Game.Net.GeometrySystem.OnUpdate"
                );
            }

            return method;
        }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(
            IEnumerable<CodeInstruction> instructions
        )
        {
            return PlayableWorldBoundsTranspiler.ReplaceTerrainBounds(
                instructions,
                "Game.Net.GeometrySystem.OnUpdate",
                1
            );
        }
    }
}
