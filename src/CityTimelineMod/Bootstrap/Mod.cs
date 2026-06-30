using System;
using Colossal.Logging;
using Game;                // UpdateSystem
using Game.Modding;        // IMod
using Game.SceneFlow;
using UnityEngine;
using CityTimelineMod.Options;
using Colossal.Localization;
using CityTimelineMod.LargeMap;
using CityTimelineMod.PlayableWorld;
using CityTimelineMod.Systems;

namespace CityTimelineMod
{
    public sealed class Mod : IMod
    {
        private CityTimelineSettings _settings;

        internal static CityTimelineSettings Settings { get; private set; }

        public static ILog Log = LogManager.GetLogger(nameof(CityTimelineMod)).SetShowsErrorsInUI(true);

        public void OnLoad(UpdateSystem updateSystem)
        {
            _settings = new CityTimelineSettings(this);
            Settings = _settings;

            _settings.RegisterInOptionsUI();
            _settings.RegisterKeyBindings();
            GameManager.instance.localizationManager.AddSource("fr-FR", new CityTimelineSettingsLocale(_settings));
            GameManager.instance.localizationManager.AddSource("en-US", new CityTimelineSettingsLocale(_settings));
            _settings.SetDefaults();
            Debug.Log("[CityTimelineMod] It loaded! (UnityEngine)");
            Log.Info("[CityTimelineMod] It loaded! (Colossal)");

            try
            {
                CityTimelineLargeMapPatcher.Install();
            }
            catch (Exception ex)
            {
                Debug.LogError("[CityTimelineMod] LargeMap patcher error: " + ex);
                Log.Error(ex);
            }

            try
            {
                PlayableWorldPatcher.Install();
            }
            catch (Exception ex)
            {
                Debug.LogError("[CityTimelineMod] PlayableWorld patcher error: " + ex);
                Log.Error(ex);
            }

            try
            {
                updateSystem.UpdateAt<CityTimelineMod.PlayableWorld.PlayableWorldErrorPrefabSystem>(SystemUpdatePhase.ToolUpdate);
            updateSystem.UpdateAt<CityTimelineMod.Diagnostics.LargeMapTerrainDiagnosticSystem>(SystemUpdatePhase.Modification1);
                updateSystem.UpdateAt<CityTimelineMod.PlayableWorld.PlayableWorldRoadProfileSystem>(SystemUpdatePhase.Modification1);
                updateSystem.UpdateAt<CityTimelineMod.Roads.RuntimeRoadToolSystem>(SystemUpdatePhase.ToolUpdate);
                updateSystem.UpdateAt<CityTimelineMod.Systems.RuntimeRoadToolDriverSystem>(SystemUpdatePhase.ToolUpdate);
                updateSystem.UpdateAt<CityTimelineMod.Roads.VanillaNetCourseProbeSystem>(SystemUpdatePhase.ToolUpdate);
                updateSystem.UpdateAt<CityTimelineMod.UI.CityTimelineUISystem>(SystemUpdatePhase.UIUpdate);
                Log.Info("[CityTimelineMod] Runtime road and CoHTML UI systems registered.");

                foreach (SystemUpdatePhase phase in Enum.GetValues(typeof(SystemUpdatePhase)))
                {
                    Log.Info("[CityTimelineMod] SystemUpdatePhase " + phase + " = " + (int)phase);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[CityTimelineMod] RuntimeRoadToolSystem registration error: " + ex);
                Log.Error(ex);
            }

            try { CityTimelineRuntimeController.Install(); }
            catch (Exception ex)
            {
                Debug.LogError("[CityTimelineMod] Runtime controller error: " + ex);
                Log.Error(ex);
            }

            try { GeoBundleBootstrap.RunOnce(); }
            catch (Exception ex)
            {
                Debug.LogError("[CityTimelineMod] Bootstrap error: " + ex);
                Log.Error(ex);
            }
        }

        public void OnDispose()
        {
            _settings?.UnregisterInOptionsUI();
            _settings = null;
            Settings = null;
            try { PlayableWorldPatcher.Uninstall(); }
            catch (Exception ex)
            {
                Debug.LogError("[CityTimelineMod] PlayableWorld dispose error: " + ex);
                Log.Error(ex);
            }
            try { CityTimelineRuntimeController.Uninstall(); }
            catch (Exception ex)
            {
                Debug.LogError("[CityTimelineMod] Runtime controller dispose error: " + ex);
                Log.Error(ex);
            }

            try
            {
                CityTimelineLargeMapPatcher.Uninstall();
            }
            catch (Exception ex)
            {
                Debug.LogError("[CityTimelineMod] LargeMap patcher dispose error: " + ex);
                Log.Error(ex);
            }

            Log.Info("[CityTimelineMod] Disposed.");
        }
    }
}




