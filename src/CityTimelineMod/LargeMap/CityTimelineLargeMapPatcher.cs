using System;
using HarmonyLib;

namespace CityTimelineMod.LargeMap
{
    internal static class CityTimelineLargeMapPatcher
    {
        private const string HarmonyId = "CityTimelineMod.LargeMap";
        private static Harmony _harmony;
        private static bool _installed;

        internal static void Install()
        {
            if (_installed)
                return;

            if (!CityTimelineLargeMapState.Enabled)
            {
                Util.Log.Info("[LargeMap] disabled.");
                return;
            }

            try
            {
                _harmony = new Harmony(HarmonyId);

                _harmony
                    .CreateClassProcessor(typeof(LargeMapTerrainSystemPatches))
                    .Patch();

                LargeMapCellMapSystemPatches.Apply(_harmony);
                LargeMapUnifiedWorldPatches.Apply(_harmony);

                _installed = true;

                Util.Log.Info(
                    "[LargeMap] unified 57 km solution installed. " +
                    "coreValue=" + CityTimelineLargeMapState.CoreValue +
                    ", mapSizeMeters=" +
                    CityTimelineLargeMapState.MapSizeMeters
                );
            }
            catch (Exception ex)
            {
                try
                {
                    if (_harmony != null)
                        _harmony.UnpatchAll(HarmonyId);
                }
                catch
                {
                }

                _harmony = null;
                _installed = false;

                Util.Log.Error(
                    "[LargeMap] install failed; all LargeMap patches rolled back: " +
                    ex
                );
            }
        }

        internal static void Uninstall()
        {
            if (!_installed)
                return;

            try
            {
                if (_harmony != null)
                    _harmony.UnpatchAll(HarmonyId);

                Util.Log.Info("[LargeMap] uninstalled.");
            }
            catch (Exception ex)
            {
                Util.Log.Error("[LargeMap] uninstall failed: " + ex);
            }
            finally
            {
                _installed = false;
                _harmony = null;
            }
        }
    }
}
