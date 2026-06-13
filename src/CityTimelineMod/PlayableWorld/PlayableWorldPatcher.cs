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

            try
            {
                _harmony = new Harmony(HarmonyId);

                _harmony
                    .CreateClassProcessor(
                        typeof(CapturePlayableWorldBoundsPatch)
                    )
                    .Patch();

                _harmony
                    .CreateClassProcessor(
                        typeof(NetToolSystemSnapControlPointsPatch)
                    )
                    .Patch();

                _harmony
                    .CreateClassProcessor(
                        typeof(GenerateNodesPlayableBoundsPatch)
                    )
                    .Patch();

                _installed = true;

                Util.Log.Info(
                    "[PlayableWorld] bounds, net-tool and " +
                    "node-generation patches installed."
                );
            }
            catch (Exception ex)
            {
                Util.Log.Error(
                    "[PlayableWorld] installation failed: " +
                    ex
                );

                throw;
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

                PlayableWorldState.Reset();

                Util.Log.Info(
                    "[PlayableWorld] bounds, net-tool and " +
                    "node-generation patches removed."
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
    }
}
