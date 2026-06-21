using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace CityTimelineMod.PlayableWorld
{
    /*
     * ValidationHelpers.ValidateWorldBounds est appelé par les objets
     * et par les edges réseau. Sans ce patch, la validation produit
     * une erreur hors-monde dès qu'un prefab/edge sort des ±7168 m.
     */
    [HarmonyPatch]
    internal static class ObjectWorldBoundsValidationPatch
    {
        private static MethodBase TargetMethod()
        {
            return GetValidationMethod("ValidateWorldBounds");
        }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(
            IEnumerable<CodeInstruction> instructions
        )
        {
            return PlayableWorldBoundsTranspiler.ReplaceTerrainBounds(
                instructions,
                "Game.Objects.ValidationHelpers.ValidateWorldBounds",
                1
            );
        }

        internal static MethodInfo GetValidationMethod(
            string name
        )
        {
            Type type = AccessTools.TypeByName(
                "Game.Objects.ValidationHelpers"
            );

            if (type == null)
            {
                throw new MissingMemberException(
                    "Game.Objects.ValidationHelpers"
                );
            }

            MethodInfo method = AccessTools.Method(type, name);

            if (method == null)
            {
                throw new MissingMethodException(
                    "Game.Objects.ValidationHelpers." +
                    name
                );
            }

            return method;
        }
    }

    /*
     * OutsideConnection doit être évalué contre la vraie bordure
     * jouable/worldmap, pas contre l'ancienne bordure 14 km.
     */
    [HarmonyPatch]
    internal static class OutsideConnectionValidationPatch
    {
        private static MethodBase TargetMethod()
        {
            return ObjectWorldBoundsValidationPatch
                .GetValidationMethod("ValidateOutsideConnection");
        }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(
            IEnumerable<CodeInstruction> instructions
        )
        {
            return PlayableWorldBoundsTranspiler.ReplaceTerrainBounds(
                instructions,
                "Game.Objects.ValidationHelpers.ValidateOutsideConnection",
                1
            );
        }
    }
}
