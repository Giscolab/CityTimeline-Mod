using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace CityTimelineMod.PlayableWorld
{
    /*
     * NetToolSystem.SnapJob.HandleWorldSize et
     * ObjectToolSystem.SnapJob.HandleWorldSize utilisent les bounds
     * de la heightmap détaillée pour ramener routes et objets vers
     * la bordure ±7168 m.
     *
     * Ces patches gardent le TerrainHeightData original, mais donnent
     * aux jobs des bounds XZ correspondant à la worldmap capturée.
     */
    [HarmonyPatch]
    internal static class NetToolSnapJobPlayableBoundsPatch
    {
        private static MethodBase TargetMethod()
        {
            Type netToolType = AccessTools.TypeByName(
                "Game.Tools.NetToolSystem"
            );

            if (netToolType == null)
            {
                throw new MissingMemberException(
                    "Game.Tools.NetToolSystem"
                );
            }

            Type snapJobType = AccessTools.Inner(
                netToolType,
                "SnapJob"
            );

            if (snapJobType == null)
            {
                throw new MissingMemberException(
                    "NetToolSystem.SnapJob"
                );
            }

            MethodInfo method = AccessTools.Method(
                snapJobType,
                "HandleWorldSize"
            );

            if (method == null)
            {
                throw new MissingMethodException(
                    "NetToolSystem.SnapJob.HandleWorldSize"
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
                "NetToolSystem.SnapJob.HandleWorldSize",
                1
            );
        }
    }

    [HarmonyPatch]
    internal static class ObjectToolSnapJobPlayableBoundsPatch
    {
        private static MethodBase TargetMethod()
        {
            Type objectToolType = AccessTools.TypeByName(
                "Game.Tools.ObjectToolSystem"
            );

            if (objectToolType == null)
            {
                throw new MissingMemberException(
                    "Game.Tools.ObjectToolSystem"
                );
            }

            Type snapJobType = AccessTools.Inner(
                objectToolType,
                "SnapJob"
            );

            if (snapJobType == null)
            {
                throw new MissingMemberException(
                    "ObjectToolSystem.SnapJob"
                );
            }

            MethodInfo method = AccessTools.Method(
                snapJobType,
                "HandleWorldSize"
            );

            if (method == null)
            {
                throw new MissingMethodException(
                    "ObjectToolSystem.SnapJob.HandleWorldSize"
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
                "ObjectToolSystem.SnapJob.HandleWorldSize",
                1
            );
        }
    }
}
