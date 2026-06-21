using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Game.Simulation;
using HarmonyLib;

namespace CityTimelineMod.PlayableWorld
{
    internal static class PlayableWorldBoundsTranspiler
    {
        internal static IEnumerable<CodeInstruction> ReplaceTerrainBounds(
            IEnumerable<CodeInstruction> instructions,
            string ownerName,
            int expectedReplacements
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
                typeof(PlayableWorldBoundsUtility),
                nameof(PlayableWorldBoundsUtility.GetPlayableBounds)
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
                    nameof(PlayableWorldBoundsUtility.GetPlayableBounds)
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

            if (
                expectedReplacements >= 0 &&
                replacements != expectedReplacements
            )
            {
                throw new InvalidOperationException(
                    "[PlayableWorld] expected " +
                    expectedReplacements +
                    " TerrainUtils.GetBounds replacement(s) in " +
                    ownerName +
                    ", found=" +
                    replacements
                );
            }

            if (replacements == 0)
            {
                throw new InvalidOperationException(
                    "[PlayableWorld] no TerrainUtils.GetBounds call found in " +
                    ownerName
                );
            }

            Util.Log.Info(
                "[PlayableWorld] " +
                ownerName +
                " bounds transpiler installed. replacements=" +
                replacements
            );

            return result;
        }
    }
}
