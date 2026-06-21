using System;
using HarmonyLib;

namespace CityTimelineMod.PlayableWorld
{
    internal static class PlayableWorldPatcher
    {
        private const string HarmonyId =
            "CityTimelineMod.PlayableWorld";

        private static Harmony _harmony;
        private static bool _installed;

        internal static void Install()
        {
            if (_installed)
                return;

            _harmony = new Harmony(HarmonyId);

            int patched = 0;
            int failed = 0;

            PatchClass(typeof(CapturePlayableWorldBoundsPatch), ref patched, ref failed);
            PatchClass(typeof(NetToolSystemSnapControlPointsPatch), ref patched, ref failed);
            PatchClass(typeof(NetToolSnapJobPlayableBoundsPatch), ref patched, ref failed);
            PatchClass(typeof(ObjectToolSnapJobPlayableBoundsPatch), ref patched, ref failed);
            PatchClass(typeof(GenerateNodesPlayableBoundsPatch), ref patched, ref failed);
            PatchClass(typeof(NetGeometryPlayableBoundsPatch), ref patched, ref failed);
            PatchClass(typeof(ObjectWorldBoundsValidationPatch), ref patched, ref failed);
            PatchClass(typeof(OutsideConnectionValidationPatch), ref patched, ref failed);

            _installed = patched > 0;

            Util.Log.Info(
                "[PlayableWorld] patcher finished. patched=" +
                patched +
                ", failed=" +
                failed
            );
        }

        internal static void Uninstall()
        {
            if (!_installed)
                return;

            try
            {
                if (_harmony != null)
                    _harmony.UnpatchAll(HarmonyId);

                PlayableWorldState.Reset();

                Util.Log.Info(
                    "[PlayableWorld] patches removed."
                );
            }
            catch (Exception ex)
            {
                Util.Log.Error(
                    "[PlayableWorld] removal failed: " +
                    ex
                );
            }
            finally
            {
                _installed = false;
                _harmony = null;
            }
        }

        private static void PatchClass(
            Type patchType,
            ref int patched,
            ref int failed
        )
        {
            if (patchType == null)
            {
                failed++;
                Util.Log.Error(
                    "[PlayableWorld] patch failed: null patch type"
                );
                return;
            }

            try
            {
                _harmony
                    .CreateClassProcessor(patchType)
                    .Patch();

                patched++;

                Util.Log.Info(
                    "[PlayableWorld] patch installed: " +
                    patchType.Name
                );
            }
            catch (Exception ex)
            {
                failed++;

                Util.Log.Error(
                    "[PlayableWorld] patch failed: " +
                    patchType.Name +
                    " error=" +
                    ex.GetType().Name +
                    ": " +
                    ex.Message
                );
            }
        }
    }
}
