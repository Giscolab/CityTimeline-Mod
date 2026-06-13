using System;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;
using CityTimelineMod.Util;

namespace CityTimelineMod
{
    internal sealed class CityTimelineRuntimeController : MonoBehaviour
    {
        private const string RootName = "CityTimelineMod_RuntimeController";

        private Rect _windowRect;
        private Rect _miniRect;
        private bool _rectInitialized;

        private string _configPath;
        private bool _modEnabled = true;
        private bool _showOverlayHud = true;
        private bool _expanded = false;

        private string _statusMessage = "État runtime chargé.";

        internal static void Install()
        {
            var existing = GameObject.Find(RootName);
            if (existing != null)
                return;

            var root = new GameObject(RootName);
            UnityEngine.Object.DontDestroyOnLoad(root);
            root.AddComponent<CityTimelineRuntimeController>();

            Log.Info("CityTimelineMod runtime controller installed.");
        }

        internal static void Uninstall()
        {
            var existing = GameObject.Find(RootName);
            if (existing != null)
                UnityEngine.Object.Destroy(existing);
        }

        private void Awake()
        {
            _configPath = Path.Combine(ResolveModDirectory(), "config.json");
            LoadState();
        }

        private void OnGUI()
        {
            HandleToggleShortcut();
            EnsureRects();
            ApplyGuiSkin();

            if (!_expanded)
            {
                var label = _modEnabled ? "CTM : ON" : "CTM : OFF";

                if (GUI.Button(_miniRect, label))
                    _expanded = true;

                return;
            }

            _windowRect = GUI.Window(
                732197,
                _windowRect,
                DrawWindow,
                "CityTimelineMod"
            );
        }

        private void DrawWindow(int id)
        {
            GUILayout.Label("Contrôleur global local");

            GUILayout.Space(6f);

            GUILayout.Label("Mod complet : " + (_modEnabled ? "ACTIF" : "INACTIF"));
            GUILayout.Label("HUD debug config : " + (_showOverlayHud ? "VISIBLE" : "MASQUÉ"));

            GUILayout.Space(8f);

            if (_modEnabled)
            {
                if (GUILayout.Button("Désactiver CityTimelineMod"))
                    SaveBoolSetting("modEnabled", false, "Désactivation enregistrée.");
            }
            else
            {
                if (GUILayout.Button("Activer CityTimelineMod"))
                    SaveBoolSetting("modEnabled", true, "Activation enregistrée.");
            }

            GUILayout.Space(4f);

            if (_showOverlayHud)
            {
                if (GUILayout.Button("Masquer HUD debug au prochain chargement"))
                    SaveBoolSetting("showOverlayHud", false, "HUD debug masqué dans config.json.");
            }
            else
            {
                if (GUILayout.Button("Afficher HUD debug au prochain chargement"))
                    SaveBoolSetting("showOverlayHud", true, "HUD debug visible dans config.json.");
            }

            GUILayout.Space(8f);

            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Relire config.json"))
                LoadState();

            if (GUILayout.Button("Réduire"))
                _expanded = false;

            GUILayout.EndHorizontal();

            GUILayout.Space(8f);

            GUILayout.Label(_statusMessage);
            GUILayout.Label("Note : modEnabled s'applique proprement au prochain chargement du mod.");
            GUILayout.Label("Raccourci : Alt+T");

            GUI.DragWindow();
        }

        private void HandleToggleShortcut()
        {
            var evt = Event.current;

            if (evt == null || evt.type != EventType.KeyDown)
                return;

            if (!evt.alt || evt.keyCode != KeyCode.T)
                return;

            _expanded = !_expanded;
            evt.Use();

            Log.Info("CityTimelineMod runtime controller: expanded=" + _expanded);
        }

        private void LoadState()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_configPath))
                    _configPath = Path.Combine(ResolveModDirectory(), "config.json");

                if (!File.Exists(_configPath))
                {
                    _modEnabled = true;
                    _showOverlayHud = true;
                    _statusMessage = "config.json absent : défaut ACTIF.";
                    Log.Info("CityTimelineMod runtime controller: config.json not found, default enabled.");
                    return;
                }

                var json = File.ReadAllText(_configPath);
                var root = JObject.Parse(json);

                _modEnabled = GetBool(root, "modEnabled", true);
                _showOverlayHud = GetBool(root, "showOverlayHud", true);

                _statusMessage = "config.json relu.";
                Log.Info(
                    "CityTimelineMod runtime controller: modEnabled=" + _modEnabled +
                    ", showOverlayHud=" + _showOverlayHud
                );
            }
            catch (Exception ex)
            {
                _statusMessage = "Erreur lecture config.";
                Log.Error("CityTimelineMod runtime controller: failed to load config. " + ex);
            }
        }

        private void SaveBoolSetting(string key, bool value, string successMessage)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_configPath))
                    _configPath = Path.Combine(ResolveModDirectory(), "config.json");

                JObject root;

                if (File.Exists(_configPath))
                {
                    var json = File.ReadAllText(_configPath);
                    root = JObject.Parse(json);
                }
                else
                {
                    root = new JObject();
                }

                root[key] = value;

                File.WriteAllText(
                    _configPath,
                    root.ToString(),
                    new UTF8Encoding(false)
                );

                LoadState();
                _statusMessage = successMessage;

                Log.Info("CityTimelineMod runtime controller: saved " + key + "=" + value);
            }
            catch (Exception ex)
            {
                _statusMessage = "Erreur sauvegarde config.";
                Log.Error("CityTimelineMod runtime controller: failed to save " + key + ". " + ex);
            }
        }

        private static bool GetBool(JObject root, string key, bool fallback)
        {
            if (root == null)
                return fallback;

            var token = root[key];

            if (token == null)
                return fallback;

            try
            {
                return token.Value<bool>();
            }
            catch
            {
                return fallback;
            }
        }

        private void EnsureRects()
        {
            if (_rectInitialized)
                return;

            var miniWidth = 115f;
            var miniHeight = 34f;

            _miniRect = new Rect(
                Mathf.Max(8f, Screen.width - miniWidth - 12f),
                8f,
                miniWidth,
                miniHeight
            );

            var width = 430f;
            var height = 245f;

            _windowRect = new Rect(
                Mathf.Max(8f, Screen.width - width - 12f),
                46f,
                width,
                height
            );

            _rectInitialized = true;
        }

        private static void ApplyGuiSkin()
        {
            if (GUI.skin == null)
                return;

            GUI.skin.window.fontSize = 16;
            GUI.skin.label.fontSize = 15;
            GUI.skin.button.fontSize = 15;
        }

        private static string ResolveModDirectory()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "AppData",
                "LocalLow",
                "Colossal Order",
                "Cities Skylines II",
                "Mods",
                "CityTimelineMod"
            );
        }
    }
}
