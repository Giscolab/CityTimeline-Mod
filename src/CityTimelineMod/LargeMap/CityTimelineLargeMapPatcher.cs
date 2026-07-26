using System;
using HarmonyLib;

namespace CityTimelineMod.LargeMap
{
    internal static class CityTimelineLargeMapPatcher
    {
        private const string HarmonyId = "CityTimelineMod.LargeMap";

        private static Harmony _harmony;

        internal static bool Install(bool authorized)
        {
            CityTimelineLargeMapState.Disable();
            var cleanupComplete = RemoveOwnedPatches("blocked install cleanup");
            _harmony = null;

            Util.Log.Error(
                "[LargeMap] install blocked for Lot 1; no LargeMap patch was installed. " +
                "authorized=" + authorized +
                ", runtimeEnabled=" + Mod.RuntimeEnabled +
                ", cleanupComplete=" + cleanupComplete + "."
            );

            return false;
        }

        internal static bool Uninstall()
        {
            CityTimelineLargeMapState.Disable();

            var rollbackComplete = RemoveOwnedPatches("uninstall");
            _harmony = null;

            if (rollbackComplete)
                Util.Log.Info("[LargeMap] uninstalled.");
            else
                Util.Log.Error("[LargeMap] uninstall incomplete: owned Harmony patches remain or could not be verified.");

            return rollbackComplete;
        }

        private static bool RemoveOwnedPatches(string operation)
        {
            try
            {
                var harmony = _harmony ?? new Harmony(HarmonyId);
                harmony.UnpatchAll(HarmonyId);
            }
            catch (Exception ex)
            {
                Util.Log.Error("[LargeMap] " + operation + " UnpatchAll failed: " + ex);
            }

            if (!TryCountOwnedPatchedMethods(out var remaining))
            {
                Util.Log.Error("[LargeMap] " + operation + " owner verification failed.");
                return false;
            }

            if (remaining != 0)
            {
                Util.Log.Error(
                    "[LargeMap] " + operation +
                    " left owned patched methods=" + remaining + "."
                );
                return false;
            }

            return true;
        }

        private static bool TryCountOwnedPatchedMethods(out int count)
        {
            count = 0;

            try
            {
                foreach (var method in Harmony.GetAllPatchedMethods())
                {
                    var patchInfo = Harmony.GetPatchInfo(method);
                    if (patchInfo == null)
                        continue;

                    foreach (var owner in patchInfo.Owners)
                    {
                        if (string.Equals(owner, HarmonyId, StringComparison.Ordinal))
                        {
                            count++;
                            break;
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Util.Log.Error("[LargeMap] Harmony owner inspection failed: " + ex);
                count = 0;
                return false;
            }
        }
    }
}
