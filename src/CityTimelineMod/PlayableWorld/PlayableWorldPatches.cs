using Game.Simulation;
using HarmonyLib;

namespace CityTimelineMod.PlayableWorld
{
    [HarmonyPatch(typeof(TerrainSystem), "FinalizeTerrainData")]
    internal static class CapturePlayableWorldBoundsPatch
    {
        [HarmonyPostfix]
        private static void Postfix(TerrainSystem __instance)
        {
            PlayableWorldState.Update(
                __instance.worldOffset,
                __instance.worldSize
            );
        }
    }
}
