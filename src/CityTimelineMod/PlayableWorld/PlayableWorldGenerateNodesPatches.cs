using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Colossal.Mathematics;
using Game.Simulation;
using Game.Tools;
using HarmonyLib;

namespace CityTimelineMod.PlayableWorld
{
    /*
     * GenerateNodesSystem transmet les limites de la heightmap détaillée
     * à CreateNodesJob.
     *
     * CreateNodesJob marque ensuite comme OutsideConnection tout nœud
     * placé hors de ces limites.
     *
     * On conserve les limites verticales vanilla, mais on remplace
     * uniquement l'emprise XZ par celle de la worldmap finale.
     */
    [HarmonyPatch(typeof(GenerateNodesSystem), "OnUpdate")]
    internal static class GenerateNodesPlayableBoundsPatch
    {
        private static bool _logged;

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(
            IEnumerable<CodeInstruction> instructions
        )
        {
            MethodInfo original = AccessTools.Method(
                typeof(TerrainUtils),
                nameof(TerrainUtils.GetBounds),
                new[]
                {
                    typeof(TerrainHeightData).MakeByRefType()
                }
            );

            MethodInfo replacement = AccessTools.Method(
                typeof(GenerateNodesPlayableBoundsPatch),
                nameof(GetPlayableNodeBounds)
            );

            if (original == null)
            {
                throw new MissingMethodException(
                    "TerrainUtils.GetBounds(ref TerrainHeightData)"
                );
            }

            if (replacement == null)
            {
                throw new MissingMethodException(
                    nameof(GetPlayableNodeBounds)
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
                    "[PlayableWorld] expected exactly one " +
                    "TerrainUtils.GetBounds call in " +
                    "GenerateNodesSystem.OnUpdate, found=" +
                    replacements
                );
            }

            return result;
        }

        private static Bounds3 GetPlayableNodeBounds(
            ref TerrainHeightData terrainData
        )
        {
            Bounds3 bounds =
                TerrainUtils.GetBounds(ref terrainData);

            if (!PlayableWorldState.Enabled)
                return bounds;

            if (!PlayableWorldState.Initialized)
                return bounds;

            float playableWidth =
                PlayableWorldState.Max.x -
                PlayableWorldState.Min.x;

            float vanillaWidth =
                bounds.max.x -
                bounds.min.x;

            if (playableWidth <= vanillaWidth + 1f)
                return bounds;

            bounds.min.x = PlayableWorldState.Min.x;
            bounds.min.z = PlayableWorldState.Min.y;

            bounds.max.x = PlayableWorldState.Max.x;
            bounds.max.z = PlayableWorldState.Max.y;

            if (!_logged)
            {
                _logged = true;

                Util.Log.Info(
                    "[PlayableWorld] GenerateNodes bounds expanded: " +
                    "min=" +
                    bounds.min.xz +
                    ", max=" +
                    bounds.max.xz
                );
            }

            return bounds;
        }
    }
}
