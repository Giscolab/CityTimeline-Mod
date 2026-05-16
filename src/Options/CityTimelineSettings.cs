using System;
using System.IO;
using System.Text;
using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CityTimelineMod.Options
{
    [FileLocation("CityTimelineMod")]
    public sealed class CityTimelineSettings : ModSetting
    {
        public const string SectionGeneral = "general";
        public const string GroupMain = "main";

        private const string ModFolderName = "CityTimelineMod";

        private bool _suppressConfigWrite;

        private bool _modEnabled = true;
        private bool _showOverlayHud = true;
        private bool _renderRoads = true;
        private bool _renderPaths = true;
        private bool _renderWaterLines = true;
        private bool _renderWaterAreas = true;
        private bool _renderZoning = true;

        public CityTimelineSettings(IMod mod) : base(mod)
        {
            LoadFromRuntimeConfig();
        }

        [SettingsUISection(SectionGeneral, GroupMain)]
        public bool ModEnabled
        {
            get => _modEnabled;
            set => SetBool(ref _modEnabled, value, "modEnabled");
        }

        [SettingsUISection(SectionGeneral, GroupMain)]
        public bool ShowOverlayHud
        {
            get => _showOverlayHud;
            set => SetBool(ref _showOverlayHud, value, "showOverlayHud");
        }

        [SettingsUISection(SectionGeneral, GroupMain)]
        public bool RenderRoads
        {
            get => _renderRoads;
            set => SetBool(ref _renderRoads, value, "renderRoads");
        }

        [SettingsUISection(SectionGeneral, GroupMain)]
        public bool RenderPaths
        {
            get => _renderPaths;
            set => SetBool(ref _renderPaths, value, "renderPaths");
        }

        [SettingsUISection(SectionGeneral, GroupMain)]
        public bool RenderWaterLines
        {
            get => _renderWaterLines;
            set => SetBool(ref _renderWaterLines, value, "renderWaterLines");
        }

        [SettingsUISection(SectionGeneral, GroupMain)]
        public bool RenderWaterAreas
        {
            get => _renderWaterAreas;
            set => SetBool(ref _renderWaterAreas, value, "renderWaterAreas");
        }

        [SettingsUISection(SectionGeneral, GroupMain)]
        public bool RenderZoning
        {
            get => _renderZoning;
            set => SetBool(ref _renderZoning, value, "renderZoning");
        }

        public override void SetDefaults()
        {
            _suppressConfigWrite = true;

            _modEnabled = true;
            _showOverlayHud = true;
            _renderRoads = true;
            _renderPaths = true;
            _renderWaterLines = true;
            _renderWaterAreas = true;
            _renderZoning = true;

            LoadFromRuntimeConfig();

            _suppressConfigWrite = false;
        }

        public void LoadFromRuntimeConfig()
        {
            _suppressConfigWrite = true;

            try
            {
                var path = ResolveRuntimeConfigPath();

                if (File.Exists(path))
                {
                    var root = JObject.Parse(File.ReadAllText(path, Encoding.UTF8));

                    _modEnabled = GetBool(root, "modEnabled", _modEnabled);
                    _showOverlayHud = GetBool(root, "showOverlayHud", _showOverlayHud);

                    _renderRoads = GetBool(root, "renderRoads", _renderRoads);
                    _renderPaths = GetBool(root, "renderPaths", _renderPaths);
                    _renderWaterLines = GetBool(root, "renderWaterLines", _renderWaterLines);
                    _renderWaterAreas = GetBool(root, "renderWaterAreas", _renderWaterAreas);
                    _renderZoning = GetBool(root, "renderZoning", _renderZoning);
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.Log("[CityTimelineMod] Failed to load runtime options from config.json: " + ex);
            }
            finally
            {
                _suppressConfigWrite = false;
            }
        }

        private void SetBool(ref bool field, bool value, string configKey)
        {
            if (field == value)
                return;

            field = value;

            if (!_suppressConfigWrite)
                SaveBoolToRuntimeConfig(configKey, value);
        }

        private static bool GetBool(JObject root, string key, bool fallback)
        {
            if (root != null && root.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out var token))
            {
                try { return token.Value<bool>(); }
                catch { return fallback; }
            }

            return fallback;
        }

        private static void SaveBoolToRuntimeConfig(string key, bool value)
        {
            try
            {
                var path = ResolveRuntimeConfigPath();
                var dir = Path.GetDirectoryName(path);

                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                JObject root;

                if (File.Exists(path))
                    root = JObject.Parse(File.ReadAllText(path, Encoding.UTF8));
                else
                    root = new JObject();

                root[key] = value;

                File.WriteAllText(path, root.ToString(Formatting.Indented), new UTF8Encoding(false));

                UnityEngine.Debug.Log("[CityTimelineMod] Option saved to config.json: " + key + "=" + value);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.Log("[CityTimelineMod] Failed to save option to config.json: " + key + " => " + ex);
            }
        }

        private static string ResolveRuntimeConfigPath()
        {
            var localModsPath = Environment.GetEnvironmentVariable("CSII_LOCALMODSPATH", EnvironmentVariableTarget.User);

            if (!string.IsNullOrWhiteSpace(localModsPath))
                return Path.Combine(localModsPath, ModFolderName, "config.json");

            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            return Path.Combine(
                userProfile,
                "AppData",
                "LocalLow",
                "Colossal Order",
                "Cities Skylines II",
                "Mods",
                ModFolderName,
                "config.json"
            );
        }
    }
}
