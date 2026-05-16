using System;
using Colossal.Logging;
using Game;                // UpdateSystem
using Game.Modding;        // IMod
using Game.SceneFlow;
using UnityEngine;
using CityTimelineMod.Options;
using Colossal.Localization;

namespace CityTimelineMod
{
    public sealed class Mod : IMod
    {
        private CityTimelineSettings _settings;
        public static ILog Log = LogManager.GetLogger(nameof(CityTimelineMod)).SetShowsErrorsInUI(true);

        public void OnLoad(UpdateSystem updateSystem)
        {
            _settings = new CityTimelineSettings(this);
            _settings.RegisterInOptionsUI();
            GameManager.instance.localizationManager.AddSource("fr-FR", new CityTimelineSettingsLocale(_settings));
            GameManager.instance.localizationManager.AddSource("en-US", new CityTimelineSettingsLocale(_settings));
            _settings.SetDefaults();
            Debug.Log("[CityTimelineMod] It loaded! (UnityEngine)");
            Log.Info("[CityTimelineMod] It loaded! (Colossal)");

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
            try { CityTimelineRuntimeController.Uninstall(); }
            catch (Exception ex)
            {
                Debug.LogError("[CityTimelineMod] Runtime controller dispose error: " + ex);
                Log.Error(ex);
            }

            Log.Info("[CityTimelineMod] Disposed.");
        }
    }
}

