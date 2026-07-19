using System;
using System.Collections.Generic;
using CityTimelineMod.Bundles;
using CityTimelineMod.Rendering.Railways;
using CityTimelineMod.Rendering.Roads;
using CityTimelineMod.Rendering.Statistics;
using CityTimelineMod.Util;
using UnityEngine;

namespace CityTimelineMod.Rendering
{
    internal sealed partial class GroundOverlayBehaviour
    {

        private bool _modernHudShowPerformance = false;
        private bool _modernHudShowDetailedRouteStats = false;
        private enum ModernHudTab
        {
            Bundle,
            Couches,
            Statistiques,
            Calage
        }

        private struct ModernHudChangeState
        {
            public bool VisibilityChanged;
            public bool AlphaChanged;
            public bool RebuildPending;
        }
        private ModernHudTab _modernHudTab = ModernHudTab.Bundle;

        private readonly Dictionary<string, bool> _modernHudLayerVisible =
            new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, float> _modernHudLayerOpacity =
            new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        
		private bool _controlPanelMouseInside;
        private static Texture2D _hudTexWindow;
        private static Texture2D _hudTexPanel;
        private static Texture2D _hudTexSection;
        private static Texture2D _hudTexButton;
        private static Texture2D _hudTexButtonHover;
        private static Texture2D _hudTexButtonActive;

        private static GUIStyle _hudTabStyle;
        private static GUIStyle _hudTabActiveStyle;
        private static GUIStyle _hudPanelStyle;
        private static GUIStyle _hudSectionStyle;
        private static GUIStyle _hudFooterStyle;
        private static GUIStyle _hudSubblockStyle;
        private static GUIStyle _hudMeterStyle;

        private static readonly Color HudBgWindow = new Color32(0x22, 0x22, 0x22, 0xff);
        private static readonly Color HudBgPanel = new Color32(0x2b, 0x2b, 0x2b, 0xff);
        private static readonly Color HudBgSection = new Color32(0x33, 0x33, 0x33, 0xff);
        private static readonly Color HudBgButton = new Color32(0x44, 0x44, 0x44, 0xff);
        private static readonly Color HudBgButtonHover = new Color32(0x55, 0x55, 0x55, 0xff);
        private static readonly Color HudBgButtonActive = new Color32(0x66, 0x66, 0x66, 0xff);

        private static readonly Color HudTextMain = new Color32(0xf0, 0xf0, 0xf0, 0xff);
        private static readonly Color HudTextMuted = new Color32(0xbb, 0xbb, 0xbb, 0xff);
        private static readonly Color HudAccent = new Color32(0x7f, 0xb3, 0xff, 0xff);

        private const int HudSafeMaxRoadSegments = 1000000;
        private const int HudSafeMaxPathSegments = 1000;
        private const int HudSafeMaxWaterSegments = 1000;
        private const int HudSafeMaxWaterAreaFillMeshes = 200;
        private const int HudSafeMaxZoningFillMeshes = 500;

        private void UpdateOverlayHud()
        {
            // HUD IMGUI moderne rendu par OnGUI().
        }

        private static Texture2D CreateHudTexture(Color color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.SetPixel(0, 0, color);
            texture.Apply(false, true);
            return texture;
        }

        private static void EnsureHudTextures()
        {
            if (_hudTexWindow != null)
                return;

            _hudTexWindow = CreateHudTexture(HudBgWindow);
            _hudTexPanel = CreateHudTexture(HudBgPanel);
            _hudTexSection = CreateHudTexture(HudBgSection);
            _hudTexButton = CreateHudTexture(HudBgButton);
            _hudTexButtonHover = CreateHudTexture(HudBgButtonHover);
            _hudTexButtonActive = CreateHudTexture(HudBgButtonActive);
        }

        private static void ApplyHudStyleState(
            GUIStyle style,
            Texture2D normal,
            Texture2D hover,
            Texture2D active,
            Color textColor
        )
        {
            if (style == null)
                return;

            style.normal.background = normal;
            style.hover.background = hover ?? normal;
            style.active.background = active ?? normal;
            style.focused.background = normal;

            style.onNormal.background = normal;
            style.onHover.background = hover ?? normal;
            style.onActive.background = active ?? normal;
            style.onFocused.background = normal;

            style.normal.textColor = textColor;
            style.hover.textColor = textColor;
            style.active.textColor = textColor;
            style.focused.textColor = textColor;

            style.onNormal.textColor = textColor;
            style.onHover.textColor = textColor;
            style.onActive.textColor = textColor;
            style.onFocused.textColor = textColor;
        }

        private static void EnsureHudStyles()
        {
            if (_hudTabStyle != null)
                return;

            _hudPanelStyle = new GUIStyle(GUI.skin.box);
            _hudPanelStyle.padding = new RectOffset(8, 8, 8, 8);
            _hudPanelStyle.margin = new RectOffset(0, 0, 0, 0);
            ApplyHudStyleState(
                _hudPanelStyle,
                _hudTexPanel,
                _hudTexPanel,
                _hudTexPanel,
                HudTextMain
            );

            _hudSectionStyle = new GUIStyle(GUI.skin.box);
            _hudSectionStyle.padding = new RectOffset(8, 8, 8, 8);
            _hudSectionStyle.margin = new RectOffset(0, 0, 0, 8);
            ApplyHudStyleState(
                _hudSectionStyle,
                _hudTexSection,
                _hudTexSection,
                _hudTexSection,
                HudTextMain
            );

            _hudFooterStyle = new GUIStyle(GUI.skin.box);
            _hudFooterStyle.padding = new RectOffset(8, 8, 8, 8);
            _hudFooterStyle.margin = new RectOffset(0, 0, 8, 0);
            ApplyHudStyleState(
                _hudFooterStyle,
                _hudTexPanel,
                _hudTexPanel,
                _hudTexPanel,
                HudTextMain
            );

            _hudTabStyle = new GUIStyle(GUI.skin.button);
            _hudTabStyle.fontSize = 15;
            _hudTabStyle.alignment = TextAnchor.MiddleCenter;
            _hudTabStyle.padding = new RectOffset(8, 8, 4, 4);
            _hudTabStyle.margin = new RectOffset(1, 1, 0, 0);
            ApplyHudStyleState(
                _hudTabStyle,
                _hudTexButton,
                _hudTexButtonHover,
                _hudTexButtonActive,
                HudTextMuted
            );

            _hudTabActiveStyle = new GUIStyle(GUI.skin.button);
            _hudTabActiveStyle.fontSize = 15;
            _hudTabActiveStyle.alignment = TextAnchor.MiddleCenter;
            _hudTabActiveStyle.padding = new RectOffset(8, 8, 4, 4);
            _hudTabActiveStyle.margin = new RectOffset(1, 1, 0, 0);
            ApplyHudStyleState(
                _hudTabActiveStyle,
                _hudTexSection,
                _hudTexSection,
                _hudTexButtonActive,
                HudAccent
            );

            _hudSubblockStyle = new GUIStyle(GUI.skin.box);
            _hudSubblockStyle.fontSize = 16;
            _hudSubblockStyle.padding = new RectOffset(8, 8, 6, 6);
            _hudSubblockStyle.margin = new RectOffset(0, 0, 0, 8);
            ApplyHudStyleState(
                _hudSubblockStyle,
                _hudTexPanel,
                _hudTexPanel,
                _hudTexPanel,
                HudTextMain
            );

            _hudMeterStyle = new GUIStyle(GUI.skin.box);
            _hudMeterStyle.fontSize = 15;
            _hudMeterStyle.padding = new RectOffset(8, 8, 6, 6);
            _hudMeterStyle.margin = new RectOffset(0, 0, 4, 6);
            ApplyHudStyleState(
                _hudMeterStyle,
                _hudTexSection,
                _hudTexSection,
                _hudTexSection,
                HudTextMain
            );
        }


        private static void ApplyControlPanelGuiSkin()
        {
            if (GUI.skin == null)
                return;

            EnsureHudTextures();

            GUI.skin.window.fontSize = 16;
            GUI.skin.window.padding = new RectOffset(8, 8, 6, 8);
            GUI.skin.window.margin = new RectOffset(0, 0, 0, 0);

            GUI.skin.label.fontSize = 15;
            GUI.skin.toggle.fontSize = 15;
            GUI.skin.button.fontSize = 15;

            ApplyHudStyleState(
                GUI.skin.window,
                _hudTexWindow,
                _hudTexWindow,
                _hudTexWindow,
                HudTextMain
            );

            ApplyHudStyleState(
                GUI.skin.box,
                _hudTexPanel,
                _hudTexPanel,
                _hudTexPanel,
                HudTextMain
            );

            ApplyHudStyleState(
                GUI.skin.button,
                _hudTexButton,
                _hudTexButtonHover,
                _hudTexButtonActive,
                HudTextMain
            );

            GUI.skin.label.normal.textColor = HudTextMain;
            GUI.skin.label.hover.textColor = HudTextMain;
            GUI.skin.label.active.textColor = HudTextMain;
            GUI.skin.label.focused.textColor = HudTextMain;

            GUI.skin.toggle.normal.textColor = HudTextMain;
            GUI.skin.toggle.hover.textColor = HudTextMain;
            GUI.skin.toggle.active.textColor = HudTextMain;
            GUI.skin.toggle.focused.textColor = HudTextMain;

            GUI.skin.horizontalSlider.fixedHeight = 18f;
            GUI.skin.horizontalSliderThumb.fixedWidth = 18f;
            GUI.skin.horizontalSliderThumb.fixedHeight = 18f;

            EnsureHudStyles();
        }


        private void NormalizeControlPanelRect()
        {
            var hudWidth = Mathf.Min(920f, Mathf.Max(360f, Screen.width - 16f));
            var hudHeight = Mathf.Min(780f, Mathf.Max(420f, Screen.height - 16f));

            if (_controlPanelRect.width >= hudWidth && _controlPanelRect.height >= hudHeight)
                return;

            var x = Mathf.Clamp(_controlPanelRect.x, 4f, Mathf.Max(4f, Screen.width - hudWidth - 4f));
            var y = Mathf.Clamp(_controlPanelRect.y, 4f, Mathf.Max(4f, Screen.height - hudHeight - 4f));

            _controlPanelRect = new Rect(x, y, hudWidth, hudHeight);
        }


        private static bool IsHudMouseEvent(EventType type)
        {
            return type == EventType.MouseDown
                || type == EventType.MouseUp
                || type == EventType.MouseDrag
                || type == EventType.MouseMove
                || type == EventType.ScrollWheel
                || type == EventType.ContextClick;
        }

        private void RefreshControlPanelMouseCapture()
        {
            var evt = Event.current;

            if (evt == null || _config == null || !_config.ShowOverlayHud)
            {
                _controlPanelMouseInside = false;
                return;
            }

            _controlPanelMouseInside = _controlPanelRect.Contains(evt.mousePosition);

            if (!_controlPanelMouseInside)
                return;

            Input.ResetInputAxes();

            GUI.FocusWindow(93014);
            GUI.BringWindowToFront(93014);
        }

        private void ConsumeMouseEventsInsideControlPanel()
        {
            var evt = Event.current;

            if (evt == null)
                return;

            if (!_controlPanelMouseInside)
                return;

            Input.ResetInputAxes();

            if (!IsHudMouseEvent(evt.type))
                return;

            if (evt.type != EventType.Used)
                evt.Use();
        }


        private void OnGUI()
        {
            if (_config == null || !_created || !_config.ShowOverlayHud)
                return;

            if (!_controlPanelLogOnce)
            {
                _controlPanelLogOnce = true;
                Log.Info("GroundOverlay HUD panel: opened.");
            }

            ApplyControlPanelGuiSkin();
            NormalizeControlPanelRect();

            RefreshControlPanelMouseCapture();

            _controlPanelRect = GUI.ModalWindow(
                93014,
                _controlPanelRect,
                DrawControlPanelWindow,
                "CityTimelineMod - Panneau HUD"
            );

            RefreshControlPanelMouseCapture();
            ConsumeMouseEventsInsideControlPanel();
			}


        private void DrawControlPanelWindow(int windowId)
        {
            DrawControlPanelWindowModern(windowId);
        }


        private void DrawControlPanelWindowModern(int windowId)
        {
            if (_config == null)
            {
                GUILayout.Label("Configuration indisponible.");
                GUI.DragWindow(new Rect(0f, 0f, 10000f, 24f));
                return;
            }

            var changes = new ModernHudChangeState();

            GUILayout.BeginVertical(_hudPanelStyle);

            DrawModernTabBar();

            _controlPanelScroll = GUILayout.BeginScrollView(
                _controlPanelScroll,
                GUILayout.Width(_controlPanelRect.width - 18f),
                GUILayout.Height(Mathf.Max(220f, _controlPanelRect.height - 176f))
            );

            GUILayout.BeginVertical(_hudSectionStyle);

            switch (_modernHudTab)
            {
                case ModernHudTab.Bundle:
                    DrawModernBundleTab();
                    break;

                case ModernHudTab.Couches:
                    DrawModernLayersTab(ref changes);
                    break;

                case ModernHudTab.Statistiques:
                    DrawModernStatsTab();
                    break;

                case ModernHudTab.Calage:
                    DrawModernCalibrationTab(ref changes);
                    break;
            }

            GUILayout.EndVertical();

            GUILayout.EndScrollView();

            GUILayout.EndVertical();

            ApplyModernHudChanges(changes);

            DrawModernFooter();

            GUI.DragWindow(new Rect(0f, 0f, 10000f, 24f));
        }


        private void DrawModernTabBar()
        {
            GUILayout.BeginHorizontal(_hudPanelStyle);

            DrawModernTabButton("Bundle", ModernHudTab.Bundle);
            DrawModernTabButton("Couches", ModernHudTab.Couches);
            DrawModernTabButton("Statistiques", ModernHudTab.Statistiques);
            DrawModernTabButton("Calage", ModernHudTab.Calage);

            GUILayout.EndHorizontal();

            GUILayout.Space(0f);
        }


        private void DrawModernTabButton(string label, ModernHudTab tab)
        {
            var active = _modernHudTab == tab;
            var style = active ? _hudTabActiveStyle : _hudTabStyle;

            if (GUILayout.Button(label, style, GUILayout.Height(28f)))
                _modernHudTab = tab;
        }

        private static bool DrawModernFoldout(string label, ref bool expanded)
        {
            var previousColor = GUI.contentColor;
            GUI.contentColor = expanded ? HudAccent : HudTextMain;

            var text = (expanded ? "▼ " : "▶ ") + label;
            var clicked = GUILayout.Button(text);

            GUI.contentColor = previousColor;

            if (clicked)
                expanded = !expanded;

            return expanded;
        }

        private static void BeginModernSubblock(string title)
        {
            GUILayout.BeginVertical(_hudSubblockStyle);

            var previousColor = GUI.contentColor;
            GUI.contentColor = HudTextMain;
            GUILayout.Label(title);
            GUI.contentColor = previousColor;

            GUILayout.Space(4f);
        }


        private static void EndModernSubblock()
        {
            GUILayout.EndVertical();
            GUILayout.Space(8f);
        }

        private static void DrawModernKeyValue(string label, string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(180f));
            GUILayout.Label(SafeHudValue(value));
            GUILayout.EndHorizontal();
        }

        private static void DrawModernStatRow(string label, string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(260f));
            GUILayout.FlexibleSpace();
            GUILayout.Label(SafeHudValue(value), GUILayout.Width(90f));
            GUILayout.EndHorizontal();
        }

        private static string FormatDoubleOrEllipsis(double value, string format = "0.###")
        {
            return double.IsNaN(value)
                ? "..."
                : value.ToString(format, System.Globalization.CultureInfo.InvariantCulture);
        }

        private static string FormatFloat(float value, string format)
        {
            return value.ToString(format, System.Globalization.CultureInfo.InvariantCulture);
        }

        private static void DrawModernMeterScale(string left, string midLeft, string center, string midRight, string right)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(left, GUILayout.Width(64f));
            GUILayout.FlexibleSpace();
            GUILayout.Label(midLeft, GUILayout.Width(64f));
            GUILayout.FlexibleSpace();
            GUILayout.Label(center, GUILayout.Width(64f));
            GUILayout.FlexibleSpace();
            GUILayout.Label(midRight, GUILayout.Width(64f));
            GUILayout.FlexibleSpace();
            GUILayout.Label(right, GUILayout.Width(64f));
            GUILayout.EndHorizontal();
        }

        private static float QuantizeSliderValue(float value, float min, float max, float step)
        {
            if (step <= 0f)
                return Mathf.Clamp(value, min, max);

            var normalized = Mathf.Round((value - min) / step) * step + min;
            return Mathf.Clamp(normalized, min, max);
        }

        private static bool DrawModernPercentMeter(string label, ref float percent)
        {
            var safeValue = Mathf.Clamp(percent, 0f, 100f);

            GUILayout.BeginVertical(_hudMeterStyle);

            GUILayout.BeginHorizontal();

            var previousColor = GUI.contentColor;
            GUI.contentColor = HudTextMuted;
            GUILayout.Label(label);
            GUI.contentColor = previousColor;

            GUILayout.FlexibleSpace();
            GUILayout.Label(Mathf.RoundToInt(safeValue).ToString(System.Globalization.CultureInfo.InvariantCulture) + " %", GUILayout.Width(56f));

            GUILayout.EndHorizontal();

            var next = GUILayout.HorizontalSlider(safeValue, 0f, 100f, GUILayout.Height(22f));
            next = QuantizeSliderValue(next, 0f, 100f, 5f);

            DrawModernMeterScale("0 %", "25 %", "50 %", "75 %", "100 %");

            GUILayout.EndVertical();

            if (Mathf.Abs(next - percent) < 0.001f)
                return false;

            percent = next;
            return true;
        }


        private static bool DrawModernFloatMeter(
            string label,
            ref float value,
            float min,
            float max,
            float step,
            string format,
            string left,
            string midLeft,
            string center,
            string midRight,
            string right
        )
        {
            var safeValue = Mathf.Clamp(value, min, max);

            GUILayout.BeginVertical(_hudMeterStyle);

            GUILayout.BeginHorizontal();

            var previousColor = GUI.contentColor;
            GUI.contentColor = HudTextMuted;
            GUILayout.Label(label);
            GUI.contentColor = previousColor;

            GUILayout.FlexibleSpace();
            GUILayout.Label(FormatFloat(safeValue, format), GUILayout.Width(72f));

            GUILayout.EndHorizontal();

            var next = GUILayout.HorizontalSlider(safeValue, min, max, GUILayout.Height(22f));
            next = QuantizeSliderValue(next, min, max, step);

            DrawModernMeterScale(left, midLeft, center, midRight, right);

            GUILayout.EndVertical();

            if (Mathf.Abs(next - value) < 0.0001f)
                return false;

            value = next;
            return true;
        }


        private void DrawModernBundleTab()
        {
            EnsureBundleCatalogLoaded();

            BeginModernSubblock("Bundle actif");

            DrawModernKeyValue("Actif :", _config.ActiveBundleId);
            DrawModernKeyValue(
                "Nom :",
                !string.IsNullOrWhiteSpace(_config.ActiveBundleDisplayName)
                    ? _config.ActiveBundleDisplayName
                    : "(vide)"
            );

            if (_bundleCatalogEntries != null && _bundleCatalogEntries.Count > 0)
            {
                if (_selectedBundleIndex < 0 || _selectedBundleIndex >= _bundleCatalogEntries.Count)
                {
                    _selectedBundleIndex = BundleCatalog.FindIndexById(_bundleCatalogEntries, _config.ActiveBundleId);

                    if (_selectedBundleIndex < 0)
                        _selectedBundleIndex = 0;
                }

                var selected = _bundleCatalogEntries[_selectedBundleIndex];

                DrawModernKeyValue("ID :", selected.Id);
                DrawModernKeyValue(
                    "Centre :",
                    (!double.IsNaN(selected.CenterLon) && !double.IsNaN(selected.CenterLat))
                        ? selected.CenterLon.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture) +
                          " / " +
                          selected.CenterLat.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture)
                        : "..."
                );
            }
            else
            {
                DrawModernKeyValue("ID :", "...");
                DrawModernKeyValue("Centre :", "...");
            }

            GUILayout.Label("Statut catalogue : " + SafeHudValue(_bundleSelectorStatusMessage));

            EndModernSubblock();

            BeginModernSubblock("Résumé GeoJSON");

            var stats = _roadSemanticStats ?? new RoadSemanticStats();

            DrawModernKeyValue("Routes :", stats.Roads.ToString(System.Globalization.CultureInfo.InvariantCulture));
            DrawModernKeyValue("Chemins :", stats.Paths.ToString(System.Globalization.CultureInfo.InvariantCulture));
            DrawModernKeyValue("Total objets :", stats.Total.ToString(System.Globalization.CultureInfo.InvariantCulture));

            var state = _progressiveRebuild;

            if (state != null)
            {
                GUILayout.Label(
                    "Overlay : " +
                    state.Phase +
                    " | services " + state.ServiceChunkIndex + "/" + state.ServiceChunks.Count +
                    " | rail " + state.RailwayChunkIndex + "/" + state.RailwayChunks.Count +
                    " | routes " + state.RoadChunkIndex + "/" + state.RoadChunks.Count +
                    " | chemins " + state.PathChunkIndex + "/" + state.PathChunks.Count
                );
            }
            else
            {
                GUILayout.Label("Overlay : idle");
            }

            EndModernSubblock();

            BeginModernSubblock("État overlay / rebuild");
            DrawVisualSettingsStatus();
            DrawProgressiveRebuildStatus();
            EndModernSubblock();

            BeginModernSubblock("Contrat eau du bundle");

            DrawModernKeyValue("Niveau d'eau réel conseillé :", FormatDoubleOrEllipsis(_config.RecommendedCs2WaterLevel) + " m");
            DrawModernKeyValue("Réserve sous eau :", FormatDoubleOrEllipsis(_config.BelowSeaReserveMeters) + " m");
            DrawModernKeyValue("Niveau mer / océan :", FormatDoubleOrEllipsis(_config.WaterReferenceElevationMeters) + " m");
            DrawModernKeyValue("Niveau lacs / rivières :", "...");
            DrawModernKeyValue("Niveau eaux artificielles :", "...");

            EndModernSubblock();

            BeginModernSubblock("Actions bundle");

            if (_bundleCatalogEntries == null || _bundleCatalogEntries.Count == 0)
            {
                GUILayout.Label(_bundleSelectorStatusMessage);

                if (GUILayout.Button("Recharger catalogue"))
                    ReloadBundleCatalog();

                EndModernSubblock();
                return;
            }

            var activeSelected = _bundleCatalogEntries[_selectedBundleIndex];

            GUILayout.Label("Sélection : " + activeSelected.DisplayName);

            GUILayout.BeginHorizontal();

            if (GUILayout.Button("← Précédent"))
                SelectPreviousBundle();

            if (GUILayout.Button("Suivant →"))
                SelectNextBundle();

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Charger bundle sélectionné"))
            {
                _pendingBundleReloadId = activeSelected.Id;
                _bundleSelectorStatusMessage = "Chargement demandé : " + activeSelected.DisplayName;
                Log.Info("GroundOverlay HUD bundle selector: load requested id=" + activeSelected.Id);
            }

            if (GUILayout.Button("Recharger catalogue"))
                ReloadBundleCatalog();

            GUILayout.EndHorizontal();

            EndModernSubblock();
        }

        private void DrawModernLayersTab(ref ModernHudChangeState changes)
        {
            DrawModernLegendSection();

            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical();
            DrawModernLayerControl("zoning.residential", "Bâtiments résidentiels", ref changes);
            DrawModernLayerControl("zoning.commercial", "Commercial", ref changes);
            DrawModernLayerControl("zoning.industrial", "Industrie", ref changes);
            DrawModernLayerControl("zoning.office", "Bureaux", ref changes);
            DrawModernLayerControl("parking", "Parkings", ref changes);
            DrawModernLayerControl("roads.paths", "Routes et chemins", ref changes);
            DrawModernLayerControl("water", "Hydrographie", ref changes);
            DrawModernLayerControl("services.water", "Eau et égouts", ref changes);
            DrawModernLayerControl("services.electricity", "Électricité", ref changes);
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            DrawModernLayerControl("services.education", "Éducation et recherche", ref changes);
            DrawModernLayerControl("services.fire", "Sapeurs-pompiers", ref changes);
            DrawModernLayerControl("services.health", "Services médicaux et soins mortuaires", ref changes);
            DrawModernLayerControl("services.parks", "Parcs et loisirs", ref changes);
            DrawModernLayerControl("services.waste", "Gestion des déchets", ref changes);
            DrawModernLayerControl("services.transport", "Transports", ref changes);
            DrawModernLayerControl("services.communication", "Communications", ref changes);
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
        }


        private void DrawModernLayerControl(string layerId, string label, ref ModernHudChangeState changes)
        {
            BeginModernSubblock(label);

            var visible = GetModernLayerVisible(layerId);
            var nextVisible = GUILayout.Toggle(visible, label);

            if (nextVisible != visible)
            {
                if (SetModernLayerVisible(layerId, nextVisible))
                    changes.VisibilityChanged = true;
            }

            DrawModernLayerSwatches(layerId);

            var previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && nextVisible;

            var opacityPercent = Mathf.Clamp01(GetModernLayerOpacity(layerId)) * 100f;

            if (DrawModernPercentMeter("Opacité", ref opacityPercent))
            {
                if (SetModernLayerOpacity(layerId, opacityPercent / 100f))
                    changes.AlphaChanged = true;
            }

            GUI.enabled = previousEnabled;

            EndModernSubblock();
        }


        private void SyncModernLayerStateFromConfig()
        {
            if (_config == null)
                return;

            _modernHudLayerVisible["zoning.residential"] = _config.ZoningResidentialVisible;
            _modernHudLayerVisible["zoning.commercial"] = _config.ZoningCommercialVisible;
            _modernHudLayerVisible["zoning.industrial"] = _config.ZoningIndustrialVisible;
            _modernHudLayerVisible["zoning.office"] = _config.ZoningOfficeVisible;
            _modernHudLayerVisible["parking"] = _config.ParkingVisible;
            _modernHudLayerVisible["roads.paths"] = _config.RenderRoads && _config.RenderPaths;
            _modernHudLayerVisible["water"] = _config.RenderWaterLines && _config.RenderWaterAreas;
            _modernHudLayerVisible["services.water"] = GetServiceFamilyVisible("water");
            _modernHudLayerVisible["services.electricity"] = GetServiceFamilyVisible("electricity");
            _modernHudLayerVisible["services.education"] = GetServiceFamilyVisible("education");
            _modernHudLayerVisible["services.fire"] = GetServiceFamilyVisible("fire");
            _modernHudLayerVisible["services.health"] = GetServiceFamilyVisible("medical");
            _modernHudLayerVisible["services.parks"] = GetServiceFamilyVisible("parks");
            _modernHudLayerVisible["services.waste"] = GetServiceFamilyVisible("waste");
            _modernHudLayerVisible["services.transport"] = GetServiceFamilyVisible("transport");
            _modernHudLayerVisible["services.communication"] = GetServiceFamilyVisible("communications");

            _modernHudLayerOpacity["zoning.residential"] = Mathf.Clamp01(_config.ZoningResidentialAlpha);
            _modernHudLayerOpacity["zoning.commercial"] = Mathf.Clamp01(_config.ZoningCommercialAlpha);
            _modernHudLayerOpacity["zoning.industrial"] = Mathf.Clamp01(_config.ZoningIndustrialAlpha);
            _modernHudLayerOpacity["zoning.office"] = Mathf.Clamp01(_config.ZoningOfficeAlpha);
            _modernHudLayerOpacity["parking"] = Mathf.Clamp01(_config.ParkingAlpha);
            _modernHudLayerOpacity["roads.paths"] = Mathf.Clamp01((_config.RoadAlpha + _config.PathAlpha) * 0.5f);
            _modernHudLayerOpacity["water"] = Mathf.Clamp01(_config.WaterLineAlpha);
            _modernHudLayerOpacity["services.water"] = Mathf.Clamp01(_config.ServicesWaterAlpha);
            _modernHudLayerOpacity["services.electricity"] = Mathf.Clamp01(_config.ServicesElectricityAlpha);
            _modernHudLayerOpacity["services.education"] = Mathf.Clamp01(_config.ServicesEducationAlpha);
            _modernHudLayerOpacity["services.fire"] = Mathf.Clamp01(_config.ServicesFireAlpha);
            _modernHudLayerOpacity["services.health"] = Mathf.Clamp01(_config.ServicesHealthAlpha);
            _modernHudLayerOpacity["services.parks"] = Mathf.Clamp01(_config.ServicesParksAlpha);
            _modernHudLayerOpacity["services.waste"] = Mathf.Clamp01(_config.ServicesWasteAlpha);
            _modernHudLayerOpacity["services.transport"] = Mathf.Clamp01(_config.ServicesTransportAlpha);
            _modernHudLayerOpacity["services.communication"] = Mathf.Clamp01(_config.ServicesCommunicationAlpha);
        }


        private bool GetModernLayerVisible(string layerId)
        {
            if (_config == null)
                return true;

            switch (layerId)
            {
                case "zoning.residential": return _config.ZoningResidentialVisible;
                case "zoning.commercial": return _config.ZoningCommercialVisible;
                case "zoning.industrial": return _config.ZoningIndustrialVisible;
                case "zoning.office": return _config.ZoningOfficeVisible;
                case "parking": return _config.ParkingVisible;
                case "roads": return _config.RenderRoads;
                case "paths": return _config.RenderPaths;
                case "roads.paths": return _config.RenderRoads && _config.RenderPaths;
                case "water": return _config.RenderWaterLines && _config.RenderWaterAreas;
                case "map.bounds": return _config.RenderMapBounds;
                case "services.water": return GetServiceFamilyVisible("water");
                case "services.electricity": return GetServiceFamilyVisible("electricity");
                case "services.education": return GetServiceFamilyVisible("education");
                case "services.fire": return GetServiceFamilyVisible("fire");
                case "services.health": return GetServiceFamilyVisible("medical");
                case "services.parks": return GetServiceFamilyVisible("parks");
                case "services.waste": return GetServiceFamilyVisible("waste");
                case "services.transport": return GetServiceFamilyVisible("transport");
                case "services.communication": return GetServiceFamilyVisible("communications");
            }

            bool storedValue;

            if (_modernHudLayerVisible.TryGetValue(layerId, out storedValue))
                return storedValue;

            _modernHudLayerVisible[layerId] = true;
            return true;
        }


        private bool SetModernLayerVisible(string layerId, bool value)
        {
            _modernHudLayerVisible[layerId] = value;

            if (_config == null)
                return false;

            switch (layerId)
            {
                case "zoning.residential": _config.ZoningResidentialVisible = value; if (value) _config.RenderZoning = true; return true;
                case "zoning.commercial": _config.ZoningCommercialVisible = value; if (value) _config.RenderZoning = true; return true;
                case "zoning.industrial": _config.ZoningIndustrialVisible = value; if (value) _config.RenderZoning = true; return true;
                case "zoning.office": _config.ZoningOfficeVisible = value; if (value) _config.RenderZoning = true; return true;
                case "parking": _config.ParkingVisible = value; if (value) _config.RenderZoning = true; return true;

                case "roads":
                    _config.RenderRoads = value;
                    return true;

                case "paths":
                    _config.RenderPaths = value;
                    return true;

                case "roads.paths":
                    _config.RenderRoads = value;
                    _config.RenderPaths = value;
                    return true;

                case "water":
                    _config.RenderWaterLines = value;
                    _config.RenderWaterAreas = value;
                    return true;

                case "map.bounds":
                    _config.RenderMapBounds = value;
                    return true;

                case "services.water": return SetServiceBoolean("water", value);
                case "services.electricity": return SetServiceBoolean("electricity", value);
                case "services.education": return SetServiceBoolean("education", value);
                case "services.fire": return SetServiceBoolean("fire", value);
                case "services.health": return SetServiceBoolean("medical", value);
                case "services.parks": return SetServiceBoolean("parks", value);
                case "services.waste": return SetServiceBoolean("waste", value);
                case "services.transport": return SetServiceBoolean("transport", value);
                case "services.communication": return SetServiceBoolean("communications", value);
            }

            return false;
        }


        private float GetModernLayerOpacity(string layerId)
        {
            if (_config == null)
                return 1f;

            switch (layerId)
            {
                case "zoning.residential": return Mathf.Clamp01(_config.ZoningResidentialAlpha);
                case "zoning.commercial": return Mathf.Clamp01(_config.ZoningCommercialAlpha);
                case "zoning.industrial": return Mathf.Clamp01(_config.ZoningIndustrialAlpha);
                case "zoning.office": return Mathf.Clamp01(_config.ZoningOfficeAlpha);
                case "parking": return Mathf.Clamp01(_config.ParkingAlpha);
                case "roads": return Mathf.Clamp01(_config.RoadAlpha);
                case "paths": return Mathf.Clamp01(_config.PathAlpha);
                case "roads.paths": return Mathf.Clamp01((_config.RoadAlpha + _config.PathAlpha) * 0.5f);
                case "water": return Mathf.Clamp01(_config.WaterLineAlpha);
                case "map.bounds": return Mathf.Clamp01(_config.MapBoundsAlpha);
                case "services.water": return Mathf.Clamp01(_config.ServicesWaterAlpha);
                case "services.electricity": return Mathf.Clamp01(_config.ServicesElectricityAlpha);
                case "services.education": return Mathf.Clamp01(_config.ServicesEducationAlpha);
                case "services.fire": return Mathf.Clamp01(_config.ServicesFireAlpha);
                case "services.health": return Mathf.Clamp01(_config.ServicesHealthAlpha);
                case "services.parks": return Mathf.Clamp01(_config.ServicesParksAlpha);
                case "services.waste": return Mathf.Clamp01(_config.ServicesWasteAlpha);
                case "services.transport": return Mathf.Clamp01(_config.ServicesTransportAlpha);
                case "services.communication": return Mathf.Clamp01(_config.ServicesCommunicationAlpha);
            }

            float storedValue;

            if (_modernHudLayerOpacity.TryGetValue(layerId, out storedValue))
                return Mathf.Clamp01(storedValue);

            _modernHudLayerOpacity[layerId] = 1f;
            return 1f;
        }


        private bool SetModernLayerOpacity(string layerId, float value)
        {
            var safeValue = Mathf.Clamp01(value);
            _modernHudLayerOpacity[layerId] = safeValue;

            if (_config == null)
                return false;

            switch (layerId)
            {
                case "zoning.residential": _config.ZoningResidentialAlpha = safeValue; return true;
                case "zoning.commercial": _config.ZoningCommercialAlpha = safeValue; return true;
                case "zoning.industrial": _config.ZoningIndustrialAlpha = safeValue; return true;
                case "zoning.office": _config.ZoningOfficeAlpha = safeValue; return true;
                case "parking": _config.ParkingAlpha = safeValue; return true;

                case "roads":
                    _config.RoadAlpha = safeValue;
                    return true;

                case "paths":
                    _config.PathAlpha = safeValue;
                    return true;

                case "roads.paths":
                    _config.RoadAlpha = safeValue;
                    _config.PathAlpha = safeValue;
                    return true;

                case "water":
                    _config.WaterLineAlpha = safeValue;
                    _config.WaterAreaOutlineAlpha = safeValue;
                    _config.WaterAreaFillAlpha = safeValue;
                    return true;

                case "map.bounds":
                    _config.MapBoundsAlpha = safeValue;
                    return true;

                case "services.water": _config.ServicesWaterAlpha = safeValue; return true;
                case "services.electricity": _config.ServicesElectricityAlpha = safeValue; return true;
                case "services.education": _config.ServicesEducationAlpha = safeValue; return true;
                case "services.fire": _config.ServicesFireAlpha = safeValue; return true;
                case "services.health": _config.ServicesHealthAlpha = safeValue; return true;
                case "services.parks": _config.ServicesParksAlpha = safeValue; return true;
                case "services.waste": _config.ServicesWasteAlpha = safeValue; return true;
                case "services.transport": _config.ServicesTransportAlpha = safeValue; return true;
                case "services.communication": _config.ServicesCommunicationAlpha = safeValue; return true;
            }

            return false;
        }


        private void DrawModernStatsTab()
        {
            var stats = _bundleHudSnapshot ?? new BundleHudSnapshot();
            var railway = _railwayStats ?? new RailwaySemanticStats();

            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical();

            BeginModernSubblock("Bundle Realmap");
            DrawModernStatRow("Objets OSM exploitables :", FormatBundleHudCount("layerIndex", stats.GetObjectCount()));
            DrawModernStatRow("Zonages :", FormatBundleHudCount("zoningPolygons", stats.GetTotalZoning()));
            DrawModernStatRow("Services :", FormatServiceHudTotal(stats));
            DrawModernStatRow("Voies ferrées :", FormatBundleHudCount("railway", railway.Total));
            EndModernSubblock();

            BeginModernSubblock("Bâtiments résidentiels");
            DrawModernStatRow("Résidentiel haute densité :", FormatBundleHudCount("zoningPolygons", stats.GetZoningCount("residential_high")));
            DrawModernStatRow("Résidentiel moyenne densité :", FormatBundleHudCount("zoningPolygons", stats.GetZoningCount("residential_medium")));
            DrawModernStatRow("Résidentiel basse densité :", FormatBundleHudCount("zoningPolygons", stats.GetZoningCount("residential_low")));
            EndModernSubblock();

            BeginModernSubblock("Commercial");
            DrawModernStatRow("Commercial haute densité :", FormatBundleHudCount("zoningPolygons", stats.GetZoningCount("commercial_high")));
            DrawModernStatRow("Commercial basse densité :", FormatBundleHudCount("zoningPolygons", stats.GetZoningCount("commercial_low")));
            DrawModernStatRow("Commerce de détail :", FormatBundleHudCount("zoningPolygons", stats.GetZoningCount("retail")));
            DrawModernStatRow("Usage mixte :", FormatBundleHudCount("zoningPolygons", stats.GetZoningCount("mixed")));
            EndModernSubblock();

            BeginModernSubblock("Industrie");
            DrawModernStatRow("Industrie :", FormatBundleHudCount("zoningPolygons", stats.GetZoningCount("industrial")));
            EndModernSubblock();

            BeginModernSubblock("Bureaux");
            DrawModernStatRow("Bureaux :", FormatBundleHudCount("zoningPolygons", stats.GetZoningCount("office")));
            EndModernSubblock();

            BeginModernSubblock("Parkings");
            DrawModernStatRow("Parking en ouvrage :", FormatBundleHudCount("zoningPolygons", stats.GetZoningCount("parking_structure")));
            DrawModernStatRow("Parking de surface :", FormatBundleHudCount("zoningPolygons", stats.GetZoningCount("parking_surface")));
            EndModernSubblock();

            BeginModernSubblock("Routes et chemins");
            DrawModernStatRow("Autoroute :", FormatBundleHudCount("roadsIndex", stats.GetRoadCategoryCount("highway")));
            DrawModernStatRow("Axe principal :", FormatBundleHudCount("roadsIndex", stats.GetRoadCategoryCount("large_road")));
            DrawModernStatRow("Route secondaire :", FormatBundleHudCount("roadsIndex", stats.GetRoadCategoryCount("medium_road")));
            DrawModernStatRow("Route tertiaire / résidentielle :", FormatBundleHudCount("roadsIndex", stats.GetRoadCategoryCount("small_road")));
            DrawModernStatRow("Bretelle / liaison :", FormatBundleHudCount("roadsIndex", stats.GetRoadCategoryCount("ramp")));
            DrawModernStatRow("Route non classée :", FormatBundleHudCount("roadsIndex", stats.GetRoadCategoryCount("gravel_road")));
            DrawModernStatRow("Routes :", FormatBundleHudCount("layerIndex", stats.GetLayerCount("roads")));
            DrawModernStatRow("Chemin / piéton :", FormatBundleHudCount("roadsIndex", stats.GetRoadCategoryCount("pathway")));
            EndModernSubblock();

            BeginModernSubblock("Hydrographie");
            DrawModernStatRow("Cours d'eau / rivières :", FormatBundleHudCount("layerIndex", stats.GetLayerCount("water_lines_clipped")));
            DrawModernStatRow("Plans d'eau / surfaces :", FormatBundleHudCount("layerIndex", stats.GetLayerCount("water_areas_clipped")));
            EndModernSubblock();

            BeginModernSubblock("Eau et égouts");
            DrawModernStatRow("Pompage :", FormatBundleHudCount("service:water", stats.GetServiceSubcategoryCount("water", "pumping")));
            DrawModernStatRow("Traitement de l'eau :", FormatBundleHudCount("service:water", stats.GetServiceSubcategoryCount("water", "water_treatment")));
            DrawModernStatRow("Égouts :", FormatBundleHudCount("service:water", stats.GetServiceSubcategoryCount("water", "sewage")));
            DrawModernStatRow("Traitement des eaux usées :", FormatBundleHudCount("service:water", stats.GetServiceSubcategoryCount("water", "wastewater")));
            DrawModernStatRow("Total :", FormatBundleHudCount("service:water", stats.GetServiceFamilyCount("water")));
            EndModernSubblock();

            BeginModernSubblock("Électricité");
            DrawModernStatRow("Production électrique :", FormatBundleHudCount("service:electricity", stats.GetServiceSubcategoryCount("electricity", "generation")));
            DrawModernStatRow("Transformation :", FormatBundleHudCount("service:electricity", stats.GetServiceSubcategoryCount("electricity", "transformation")));
            DrawModernStatRow("Stockage :", FormatBundleHudCount("service:electricity", stats.GetServiceSubcategoryCount("electricity", "storage")));
            DrawModernStatRow("Réseau électrique :", FormatBundleHudCount("service:electricity", stats.GetServiceSubcategoryCount("electricity", "grid")));
            DrawModernStatRow("Total :", FormatBundleHudCount("service:electricity", stats.GetServiceFamilyCount("electricity")));
            EndModernSubblock();

            GUILayout.EndVertical();

            GUILayout.BeginVertical();

            BeginModernSubblock("Éducation et recherche");
            DrawModernStatRow("École primaire :", FormatBundleHudCount("service:education", stats.GetServiceSubcategoryCount("education", "primary")));
            DrawModernStatRow("Collège / lycée :", FormatBundleHudCount("service:education", stats.GetServiceSubcategoryCount("education", "secondary")));
            DrawModernStatRow("Université :", FormatBundleHudCount("service:education", stats.GetServiceSubcategoryCount("education", "university")));
            DrawModernStatRow("Recherche :", FormatBundleHudCount("service:education", stats.GetServiceSubcategoryCount("education", "research")));
            DrawModernStatRow("Total :", FormatBundleHudCount("service:education", stats.GetServiceFamilyCount("education")));
            EndModernSubblock();

            BeginModernSubblock("Sapeurs-pompiers");
            DrawModernStatRow("Caserne locale :", FormatBundleHudCount("service:fire", stats.GetServiceSubcategoryCount("fire", "local_station")));
            DrawModernStatRow("Grande caserne :", FormatBundleHudCount("service:fire", stats.GetServiceSubcategoryCount("fire", "large_station")));
            DrawModernStatRow("Surveillance / secours spécialisés :", FormatBundleHudCount("service:fire", stats.GetServiceSubcategoryCount("fire", "special_rescue")));
            DrawModernStatRow("Total :", FormatBundleHudCount("service:fire", stats.GetServiceFamilyCount("fire")));
            EndModernSubblock();

            BeginModernSubblock("Services médicaux et soins mortuaires");
            DrawModernStatRow("Clinique :", FormatBundleHudCount("service:medical", stats.GetServiceSubcategoryCount("medical", "clinic")));
            DrawModernStatRow("Hôpital :", FormatBundleHudCount("service:medical", stats.GetServiceSubcategoryCount("medical", "hospital")));
            DrawModernStatRow("Crématorium :", FormatBundleHudCount("service:medical", stats.GetServiceSubcategoryCount("medical", "crematorium")));
            DrawModernStatRow("Cimetière :", FormatBundleHudCount("service:medical", stats.GetServiceSubcategoryCount("medical", "cemetery")));
            DrawModernStatRow("Total :", FormatBundleHudCount("service:medical", stats.GetServiceFamilyCount("medical")));
            EndModernSubblock();

            BeginModernSubblock("Parcs et loisirs");
            DrawModernStatRow("Parc local :", FormatBundleHudCount("service:parks", stats.GetServiceSubcategoryCount("parks", "local_park")));
            DrawModernStatRow("Grand parc :", FormatBundleHudCount("service:parks", stats.GetServiceSubcategoryCount("parks", "large_park")));
            DrawModernStatRow("Sport :", FormatBundleHudCount("service:parks", stats.GetServiceSubcategoryCount("parks", "sport")));
            DrawModernStatRow("Loisirs :", FormatBundleHudCount("service:parks", stats.GetServiceSubcategoryCount("parks", "leisure")));
            DrawModernStatRow("Tourisme :", FormatBundleHudCount("service:parks", stats.GetServiceSubcategoryCount("parks", "tourism")));
            DrawModernStatRow("Total :", FormatBundleHudCount("service:parks", stats.GetServiceFamilyCount("parks")));
            EndModernSubblock();

            BeginModernSubblock("Gestion des déchets");
            DrawModernStatRow("Collecte :", FormatBundleHudCount("service:waste", stats.GetServiceSubcategoryCount("waste", "collection")));
            DrawModernStatRow("Recyclage :", FormatBundleHudCount("service:waste", stats.GetServiceSubcategoryCount("waste", "recycling")));
            DrawModernStatRow("Traitement :", FormatBundleHudCount("service:waste", stats.GetServiceSubcategoryCount("waste", "treatment")));
            DrawModernStatRow("Décharge / stockage :", FormatBundleHudCount("service:waste", stats.GetServiceSubcategoryCount("waste", "landfill")));
            DrawModernStatRow("Total :", FormatBundleHudCount("service:waste", stats.GetServiceFamilyCount("waste")));
            EndModernSubblock();

            BeginModernSubblock("Transports");
            DrawModernStatRow("Bus :", FormatBundleHudCount("service:transport", stats.GetServiceSubcategoryCount("transport", "bus")));
            DrawModernStatRow("Tram :", FormatBundleHudCount("service:transport", stats.GetServiceSubcategoryCount("transport", "tram")));
            DrawModernStatRow("Train :", FormatBundleHudCount("service:transport", stats.GetServiceSubcategoryCount("transport", "train")));
            DrawModernStatRow("Métro :", FormatBundleHudCount("service:transport", stats.GetServiceSubcategoryCount("transport", "metro")));
            DrawModernStatRow("Taxi :", FormatBundleHudCount("service:transport", stats.GetServiceSubcategoryCount("transport", "taxi")));
            DrawModernStatRow("Aérien :", FormatBundleHudCount("service:transport", stats.GetServiceSubcategoryCount("transport", "air")));
            DrawModernStatRow("Maritime :", FormatBundleHudCount("service:transport", stats.GetServiceSubcategoryCount("transport", "maritime")));
            DrawModernStatRow("Total :", FormatBundleHudCount("service:transport", stats.GetServiceFamilyCount("transport")));
            EndModernSubblock();

            BeginModernSubblock("Communications");
            DrawModernStatRow("Poste :", FormatBundleHudCount("service:communications", stats.GetServiceSubcategoryCount("communications", "post")));
            DrawModernStatRow("Télécommunications :", FormatBundleHudCount("service:communications", stats.GetServiceSubcategoryCount("communications", "telecom")));
            DrawModernStatRow("Serveurs / data center :", FormatBundleHudCount("service:communications", stats.GetServiceSubcategoryCount("communications", "datacenter")));
            DrawModernStatRow("Radio / antennes :", FormatBundleHudCount("service:communications", stats.GetServiceSubcategoryCount("communications", "radio")));
            DrawModernStatRow("Total :", FormatBundleHudCount("service:communications", stats.GetServiceFamilyCount("communications")));
            EndModernSubblock();

            BeginModernSubblock("Réseau ferroviaire");
            DrawModernStatRow("Train :", FormatBundleHudCount("railway", railway.Train));
            DrawModernStatRow("Tramway :", FormatBundleHudCount("railway", railway.Tram));
            DrawModernStatRow("Métro léger :", FormatBundleHudCount("railway", railway.LightRail));
            DrawModernStatRow("Métro :", FormatBundleHudCount("railway", railway.Subway));
            DrawModernStatRow("Voies de service :", FormatBundleHudCount("railway", railway.Service));
            DrawModernStatRow("Tunnels :", FormatBundleHudCount("railway", railway.Tunnels));
            DrawModernStatRow("Total :", FormatBundleHudCount("railway", railway.Total));
            EndModernSubblock();

            GUILayout.EndVertical();

            GUILayout.EndHorizontal();

            if (DrawModernFoldout("Performance overlay", ref _modernHudShowPerformance))
            {
                BeginModernSubblock("Performance overlay");
                DrawModernPerformanceSection();
                EndModernSubblock();
            }

            if (DrawModernFoldout("Statistiques routes détaillées", ref _modernHudShowDetailedRouteStats))
            {
                BeginModernSubblock("Statistiques routes détaillées");
                DrawRoadSemanticStatsSection();
                EndModernSubblock();
            }
        }


        private void DrawModernPerformanceSection()
        {
            if (_config == null)
            {
                GUILayout.Label("Configuration indisponible.");
                return;
            }

            if (DrawIntSlider("Budget zonage", ref _config.MaxZoningFillMeshesDebug, 0, 20000))
                _controlPanelRebuildPending = true;

            if (DrawIntSlider("Budget routes", ref _config.MaxRoadSegmentsDebug, 1000, 800000))
                _controlPanelRebuildPending = true;

            if (DrawIntSlider("Budget chemins", ref _config.MaxPathSegmentsDebug, 1000, 800000))
                _controlPanelRebuildPending = true;

            if (DrawIntSlider("Budget eau segments", ref _config.MaxWaterSegmentsDebug, 1000, 400000))
                _controlPanelRebuildPending = true;

            if (DrawIntSlider("Budget eau surfaces", ref _config.MaxWaterAreaFillMeshesDebug, 0, 2000))
                _controlPanelRebuildPending = true;

            if (DrawIntSlider("Niveau routes", ref _config.MinimumRoadDebugTier, 0, 4))
                _controlPanelRebuildPending = true;

            if (DrawIntSlider("Pas / pointStride", ref _config.PointStride, 1, 20))
                _controlPanelRebuildPending = true;

            GUILayout.Space(8f);
            GUILayout.Label("Diagnostic services");
            var serviceCounters = _progressiveRebuild != null && _progressiveRebuild.IsActive
                ? _progressiveRebuild.ServiceCounters
                : _lastServiceRenderCounters;
            if (serviceCounters != null)
            {
                DrawModernKeyValue("Points source :", serviceCounters.SourcePoints.ToString());
                DrawModernKeyValue("Points éligibles :", serviceCounters.EligiblePoints.ToString());
                DrawModernKeyValue("Points rendus :", serviceCounters.CreatedPoints.ToString());
                DrawModernKeyValue("Chunks / meshes :", serviceCounters.CreatedChunks + " / " + serviceCounters.CreatedMeshObjects);
                DrawModernKeyValue("Hors carte :", serviceCounters.SkippedOutsideMap.ToString());
                DrawModernKeyValue("Filtrés / invalides :", serviceCounters.SkippedByFilter + " / " + serviceCounters.SkippedInvalid);
                DrawModernKeyValue(
                    "Familles chargées / absentes / erreurs :",
                    serviceCounters.LoadedFamilies + " / " + serviceCounters.MissingFamilies + " / " + serviceCounters.FailedFamilies
                );
            }

            GUILayout.Space(8f);
            GUILayout.Label("Diagnostic réseau ferroviaire");
            var railwayCounters = _progressiveRebuild != null && _progressiveRebuild.IsActive
                ? _progressiveRebuild.RailwayCounters
                : _lastRailwayRenderCounters;
            if (railwayCounters != null)
            {
                DrawModernKeyValue("Lignes source / éligibles :", railwayCounters.SourceLines + " / " + railwayCounters.EligibleLines);
                DrawModernKeyValue("Lignes / segments rendus :", railwayCounters.CreatedLines + " / " + railwayCounters.CreatedSegments);
                DrawModernKeyValue("Objets mesh :", railwayCounters.CreatedMeshObjects.ToString());
                DrawModernKeyValue(
                    "Filtrées / limite / invalides :",
                    railwayCounters.SkippedByFilter + " / " + railwayCounters.SkippedByLimit + " / " + railwayCounters.SkippedInvalid
                );
            }
        }


        private void DrawModernCalibrationTab(ref ModernHudChangeState changes)
        {
            BeginModernSubblock("Calage avancé");

            _controlPanelCalibrationUnlocked = GUILayout.Toggle(
                _controlPanelCalibrationUnlocked,
                "Déverrouiller le calage"
            );

            if (!_controlPanelCalibrationUnlocked)
            {
                GUILayout.Label("Calage verrouillé.");
                EndModernSubblock();
                return;
            }

            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical();

            if (DrawModernFloatMeter(
                "Offset X",
                ref _config.WorldOriginX,
                -5000f,
                5000f,
                0.1f,
                "0.0",
                "-5000",
                "-2500",
                "0",
                "2500",
                "5000"
            ))
                changes.RebuildPending = true;

            if (DrawModernFloatMeter(
                "Offset Z",
                ref _config.WorldOriginZ,
                -5000f,
                5000f,
                0.1f,
                "0.0",
                "-5000",
                "-2500",
                "0",
                "2500",
                "5000"
            ))
                changes.RebuildPending = true;

            if (DrawModernFloatMeter(
                "Rotation degrés",
                ref _config.OverlayRotationDegrees,
                -30f,
                30f,
                0.01f,
                "0.00",
                "-30",
                "-15",
                "0",
                "15",
                "30"
            ))
                changes.RebuildPending = true;

            GUILayout.EndVertical();

            GUILayout.BeginVertical();

            if (DrawModernFloatMeter(
                "Scale X",
                ref _config.OverlayScaleX,
                0.5f,
                1.5f,
                0.001f,
                "0.000",
                "0.5",
                "0.75",
                "1.0",
                "1.25",
                "1.5"
            ))
                changes.RebuildPending = true;

            if (DrawModernFloatMeter(
                "Scale Z",
                ref _config.OverlayScaleZ,
                0.5f,
                1.5f,
                0.001f,
                "0.000",
                "0.5",
                "0.75",
                "1.0",
                "1.25",
                "1.5"
            ))
                changes.RebuildPending = true;

            if (DrawModernFloatMeter(
                "Ground margin",
                ref _config.GroundMargin,
                0f,
                600f,
                1f,
                "0",
                "0",
                "150",
                "300",
                "450",
                "600"
            ))
                changes.RebuildPending = true;

            GUILayout.EndVertical();

            GUILayout.EndHorizontal();

            EndModernSubblock();
        }


        private void DrawModernFooter()
        {
            GUILayout.BeginVertical(_hudFooterStyle);

            GUILayout.BeginHorizontal();

            if (GUILayout.Button(_controlPanelRebuildPending ? "Appliquer / reconstruire *" : "Appliquer / reconstruire", GUILayout.Height(28f)))
            {
                var hadPendingChanges = _controlPanelRebuildPending;
                _controlPanelRebuildPending = false;

                if (hadPendingChanges)
                    MarkVisualSettingsDirty("Réglages reconstruits — sauvegarde optionnelle.");

                ApplyOverlayRebuildSafetyGuard("HUD apply");

                Log.Info("GroundOverlay HUD panel: rebuild requested.");
                RequestOverlayRebuild("HUD apply", true);
            }

            if (GUILayout.Button("Fermer", GUILayout.Height(28f)))
            {
                _config.ShowOverlayHud = false;
                _controlPanelLogOnce = false;
                Log.Info("GroundOverlay HUD panel: closed.");
            }

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Sauvegarder visuels", GUILayout.Height(28f)))
            {
                _config.SaveVisualSettingsToConfig();
                _visualSettingsDirty = false;
                _visualSettingsStatusMessage = "Visuels sauvegardés dans config.json.";
                Log.Info("GroundOverlay HUD panel: visual settings saved from HUD.");
            }

            if (GUILayout.Button("Recharger visuels", GUILayout.Height(28f)))
            {
                _config.LoadVisualSettingsFromConfig();
                SyncVisibilityStateFromConfig();
                SyncModernLayerStateFromConfig();
                _visualSettingsDirty = false;
                _visualSettingsStatusMessage = "Visuels rechargés depuis config.json.";
                _controlPanelRebuildPending = false;

                Log.Info("GroundOverlay HUD panel: visual settings reloaded from config.");

                ApplyOverlayRebuildSafetyGuard("HUD reload visual settings");
                RequestOverlayRebuild("HUD reload visual settings", true);
            }

            GUILayout.EndHorizontal();

            if (_controlPanelRebuildPending)
            {
                var previousColor = GUI.contentColor;
                GUI.contentColor = HudTextMuted;
                GUILayout.Label("Changement en attente : cliquer Appliquer / reconstruire.");
                GUI.contentColor = previousColor;
            }

            GUILayout.EndVertical();
        }


        private void ApplyModernHudChanges(ModernHudChangeState changes)
        {
            if (changes.RebuildPending)
                _controlPanelRebuildPending = true;

            if (changes.VisibilityChanged)
            {
                SyncVisibilityStateFromConfig();
                MarkVisualSettingsDirty("Couches modifiées — non sauvegardées.");

                Log.Info(
                    "GroundOverlay HUD panel: visibility changed " +
                    "zoning=" + _config.RenderZoning +
                    ", roads=" + _config.RenderRoads +
                    ", paths=" + _config.RenderPaths +
                    ", waterLines=" + _config.RenderWaterLines +
                    ", waterAreas=" + _config.RenderWaterAreas +
                    ", bounds=" + _config.RenderMapBounds
                );

                ApplyCurrentOverlayVisibilityToMaterials();
            }
            else if (changes.AlphaChanged)
            {
                MarkVisualSettingsDirty("Transparence modifiée — non sauvegardée.");
                ApplyCurrentOverlayVisibilityToMaterials();
            }
        }


        private void DrawBundleSelector()
        {
            GUILayout.Space(8f);
            GUILayout.Label("Ville / bundle actif");

            EnsureBundleCatalogLoaded();

            var activeId = _config != null ? _config.ActiveBundleId : "";
            GUILayout.Label("Actif : " + SafeHudValue(activeId));

            if (_bundleCatalogEntries == null || _bundleCatalogEntries.Count == 0)
            {
                GUILayout.Label(_bundleSelectorStatusMessage);

                if (GUILayout.Button("Recharger catalogue bundles"))
                    ReloadBundleCatalog();

                return;
            }

            if (_selectedBundleIndex < 0 || _selectedBundleIndex >= _bundleCatalogEntries.Count)
            {
                _selectedBundleIndex = BundleCatalog.FindIndexById(_bundleCatalogEntries, activeId);

                if (_selectedBundleIndex < 0)
                    _selectedBundleIndex = 0;
            }

            var selected = _bundleCatalogEntries[_selectedBundleIndex];

            GUILayout.Label("Sélection : " + selected.DisplayName);
            GUILayout.Label("ID : " + selected.Id);

            if (!double.IsNaN(selected.CenterLon) && !double.IsNaN(selected.CenterLat))
            {
                GUILayout.Label(
                    "Centre : " +
                    selected.CenterLon.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture) +
                    " / " +
                    selected.CenterLat.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture)
                );
            }

            if (!double.IsNaN(selected.RecommendedWaterLevel))
                GUILayout.Label("Eau conseillée : " + selected.RecommendedWaterLevel.ToString("0.###"));

            GUILayout.BeginHorizontal();

            if (GUILayout.Button("← Précédent"))
                SelectPreviousBundle();

            if (GUILayout.Button("Suivant →"))
                SelectNextBundle();

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Charger bundle sélectionné"))
            {
                _pendingBundleReloadId = selected.Id;
                _bundleSelectorStatusMessage = "Chargement demandé : " + selected.DisplayName;
                Log.Info("GroundOverlay HUD bundle selector: load requested id=" + selected.Id);
            }

            if (GUILayout.Button("Recharger catalogue"))
                ReloadBundleCatalog();

            GUILayout.EndHorizontal();

            GUILayout.Label(_bundleSelectorStatusMessage);
        }

        private void EnsureBundleCatalogLoaded()
        {
            if (_bundleCatalogEntries != null && _bundleCatalogEntries.Count > 0)
                return;

            ReloadBundleCatalog();
        }

        private void ReloadBundleCatalog()
        {
            _bundleCatalogEntries = BundleCatalog.Load(_config);
            _selectedBundleIndex = BundleCatalog.FindIndexById(
                _bundleCatalogEntries,
                _config != null ? _config.ActiveBundleId : null
            );

            if (_selectedBundleIndex < 0 && _bundleCatalogEntries != null && _bundleCatalogEntries.Count > 0)
                _selectedBundleIndex = 0;

            _bundleSelectorStatusMessage = _bundleCatalogEntries != null && _bundleCatalogEntries.Count > 0
                ? "Catalogue chargé : " + _bundleCatalogEntries.Count + " bundle(s)."
                : "Aucun bundle trouvé dans bundle_index.json.";
        }

        private void SelectPreviousBundle()
        {
            if (_bundleCatalogEntries == null || _bundleCatalogEntries.Count == 0)
                return;

            _selectedBundleIndex--;

            if (_selectedBundleIndex < 0)
                _selectedBundleIndex = _bundleCatalogEntries.Count - 1;
        }

        private void SelectNextBundle()
        {
            if (_bundleCatalogEntries == null || _bundleCatalogEntries.Count == 0)
                return;

            _selectedBundleIndex++;

            if (_selectedBundleIndex >= _bundleCatalogEntries.Count)
                _selectedBundleIndex = 0;
        }

        private void HandlePendingBundleReload()
        {
            if (string.IsNullOrWhiteSpace(_pendingBundleReloadId))
                return;

            var bundleId = _pendingBundleReloadId;
            _pendingBundleReloadId = null;

            Log.Info("GroundOverlay HUD bundle selector: executing reload id=" + bundleId);

            CityTimelineMod.GeoBundleBootstrap.ReloadActiveBundle(bundleId);
        }

        private static string SafeHudValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "(vide)" : value;
        }

        private static string SafeLogValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "(empty)" : value;
        }

        private bool ApplyOverlayRebuildSafetyGuard(string reason)
        {
            Log.Info("GroundOverlay HUD safety guard disabled: DEV FULL overlay mode. reason=" + SafeLogValue(reason));
            return false;
        }

        private void DrawBundleWaterContractInfo()
        {
            if (_config == null || double.IsNaN(_config.RecommendedCs2WaterLevel))
                return;

            GUILayout.Space(8f);
            GUILayout.Label("Contrat eau du bundle");
            GUILayout.Label("Niveau d'eau conseillé CS2 : " + _config.RecommendedCs2WaterLevel.ToString("0.###"));

            if (!double.IsNaN(_config.BelowSeaReserveMeters))
                GUILayout.Label("Réserve sous eau : " + _config.BelowSeaReserveMeters.ToString("0.###") + " m");

            if (!double.IsNaN(_config.WaterReferenceElevationMeters))
                GUILayout.Label("Altitude eau référence : " + _config.WaterReferenceElevationMeters.ToString("0.###") + " m");

            GUILayout.Label("À reporter dans l'éditeur CS2 si tu veux le niveau réel conseillé.");
        }

        private void DrawBundleSummarySection()
        {
            if (_config == null)
                return;

            GUILayout.Space(8f);
            GUILayout.Label("Résumé bundle");
            GUILayout.Label("ActiveBundleId : " + SafeHudValue(_config.ActiveBundleId));

            if (!string.IsNullOrWhiteSpace(_config.ActiveBundleDisplayName))
                GUILayout.Label("Nom : " + _config.ActiveBundleDisplayName);

            GUILayout.Label("WorldMapSizeKm : " + _config.WorldMapSizeKm.ToString("0.###"));
            GUILayout.Label("HeightMapSizeKm : " + _config.HeightMapSizeKm.ToString("0.###"));

            if (!double.IsNaN(_config.RecommendedCs2WaterLevel))
                GUILayout.Label("RecommendedCs2WaterLevel : " + _config.RecommendedCs2WaterLevel.ToString("0.###"));
        }

        private void DrawRoadSemanticStatsSection()
        {
            var stats = _roadSemanticStats ?? new RoadSemanticStats();

            GUILayout.Space(8f);
            GUILayout.Label("Résumé routes");
            GUILayout.Label("Routes chargées : " + stats.Roads);
            GUILayout.Label("Chemins chargés : " + stats.Paths);
            GUILayout.Label("Total routes+chemins : " + stats.Total);
            GUILayout.Label("Avec nom : " + stats.WithName);
            GUILayout.Label("Sens unique : " + stats.OnewayTrue);
            GUILayout.Label("Sens inverse : " + stats.OnewayReverse);
            GUILayout.Label("Oneway connu : " + stats.WithOneway);
            GUILayout.Label("Voies connues : " + stats.WithLanes);
            GUILayout.Label("TargetLaneCount connu : " + stats.WithTargetLaneCount);
            GUILayout.Label("Vitesse connue : " + stats.WithMaxSpeed);
            GUILayout.Label("Ponts : " + stats.Bridges);
            GUILayout.Label("Tunnels : " + stats.Tunnels);
            GUILayout.Label("Ronds-points : " + stats.Roundabouts);
        }

        private void DrawExhaustiveRenderSection()
        {
            GUILayout.Space(12f);
            GUILayout.Label("Rendu exhaustif");

            var changed = false;

            changed |= DrawLayerToggle("Rendu exhaustif global", ref _config.RenderEverything);
            changed |= DrawLayerToggle("Toutes routes", ref _config.RenderAllRoadSegments);
            changed |= DrawLayerToggle("Tous chemins", ref _config.RenderAllPathSegments);
            changed |= DrawLayerToggle("Tout zonage", ref _config.RenderAllZoningPolygons);
            changed |= DrawLayerToggle("Toutes lignes eau", ref _config.RenderAllWaterSegments);
            changed |= DrawLayerToggle("Toutes surfaces eau", ref _config.RenderAllWaterAreaFills);

            if (changed)
            {
                _controlPanelRebuildPending = true;
                MarkVisualSettingsDirty("Rendu exhaustif modifié — non sauvegardé.");
            }
        }

        private void DrawRoadRenderingOptionsSection()
        {
            GUILayout.Space(12f);
            GUILayout.Label("Rendu routes enrichi");
            GUILayout.Label("Rendu routes : " + ResolveRoadRenderMode(_config.RoadRenderMode));
            GUILayout.Label("Rendu chemins : " + ResolveRoadRenderMode(_config.PathRenderMode));

            var changed = false;

            changed |= DrawLayerToggle("Chunking spatial routes", ref _config.EnableRoadSpatialChunking);
            changed |= DrawLayerToggle("Rebuild progressif", ref _config.EnableProgressiveOverlayRebuild);
            changed |= DrawLayerToggle("Largeur selon voies", ref _config.UseLaneWidthScaling);
            changed |= DrawLayerToggle("Highlight sens uniques", ref _config.HighlightOneWayRoads);
            changed |= DrawLayerToggle("Highlight ponts", ref _config.HighlightBridges);
            changed |= DrawLayerToggle("Highlight tunnels", ref _config.HighlightTunnels);
            changed |= DrawLayerToggle("Highlight ronds-points", ref _config.HighlightRoundabouts);
            changed |= DrawLayerToggle("Flèches one-way", ref _config.RenderRoadDirectionArrows);
            changed |= DrawLayerToggle("Labels noms de rues", ref _config.RenderRoadLabels);

            if (DrawFloatSlider("Facteur largeur voies", ref _config.LaneWidthScaleFactor, 0f, 1f, "0.00"))
                changed = true;

            if (DrawFloatSlider("Taille chunk routes", ref _config.RoadChunkSizeMeters, 128f, 4096f, "0"))
                changed = true;

            if (DrawIntSlider("Chunks routes/frame", ref _config.RoadChunksPerFrame, 1, 64))
                changed = true;

            if (DrawIntSlider("Chunks chemins/frame", ref _config.PathChunksPerFrame, 1, 64))
                changed = true;

            if (DrawIntSlider("Max flèches one-way", ref _config.RoadArrowMaxCount, 0, 200000))
                changed = true;

            if (DrawIntSlider("Max labels rues", ref _config.RoadLabelMaxCount, 0, 20000))
                changed = true;

            if (changed)
            {
                _controlPanelRebuildPending = true;
                MarkVisualSettingsDirty("Rendu routes modifié — non sauvegardé.");
            }
        }

        private void DrawRoadSemanticFilterSection()
        {
            GUILayout.Space(12f);
            GUILayout.Label("Filtres routes");
            GUILayout.Label("Filtre actif : " + _config.RoadSemanticFilterMode);

            GUILayout.BeginHorizontal();
            DrawRoadSemanticFilterButton("Toutes routes", "all");
            DrawRoadSemanticFilterButton("Majeures", "major");
            DrawRoadSemanticFilterButton("Locales", "local");
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            DrawRoadSemanticFilterButton("Sens uniques", "oneway");
            DrawRoadSemanticFilterButton("Ponts", "bridges");
            DrawRoadSemanticFilterButton("Tunnels", "tunnels");
            DrawRoadSemanticFilterButton("Ronds-points", "roundabouts");
            GUILayout.EndHorizontal();
        }

        private void DrawRoadSemanticFilterButton(string label, string mode)
        {
            var active = string.Equals(_config.RoadSemanticFilterMode, mode, StringComparison.OrdinalIgnoreCase);
            var buttonLabel = active ? "[" + label + "]" : label;

            if (!GUILayout.Button(buttonLabel))
                return;

            _config.RoadSemanticFilterMode = mode;
            _controlPanelRebuildPending = true;
            MarkVisualSettingsDirty("Filtre routes modifié — non sauvegardé.");
        }

        private void DrawVisualSettingsStatus()
        {
            GUILayout.Space(6f);

            var status = _visualSettingsDirty
                ? "Modifié non sauvegardé"
                : _visualSettingsStatusMessage;

            GUILayout.Label("État live : " + status);
        }

        private void DrawProgressiveRebuildStatus()
        {
            var state = _progressiveRebuild;
            var hasPending = !string.IsNullOrWhiteSpace(_pendingRebuildReason);

            if (state == null && !hasPending && !_rebuildRestartPending && !_rebuildCancelRequested)
                return;

            GUILayout.Space(8f);
            GUILayout.Label("Rebuild overlay");
            GUILayout.Label("Phase : " + (state != null ? state.Phase.ToString() : "None"));
            GUILayout.Label("Restart pending : " + _rebuildRestartPending);
            GUILayout.Label("Cancel requested : " + _rebuildCancelRequested);
            GUILayout.Label("Pending reason : " + SafeHudValue(_pendingRebuildReason));

            if (state != null)
            {
                GUILayout.Label("Chunks services : " + state.ServiceChunkIndex + " / " + state.ServiceChunks.Count);
                GUILayout.Label("Points services : " + state.ServiceCounters.CreatedPoints);
                GUILayout.Label("Services hors carte : " + state.ServiceCounters.SkippedOutsideMap);
                GUILayout.Label("Chunks ferroviaires : " + state.RailwayChunkIndex + " / " + state.RailwayChunks.Count);
                GUILayout.Label("Segments ferroviaires : " + state.RailwayCounters.CreatedSegments);
                GUILayout.Label("Chunks routes : " + state.RoadChunkIndex + " / " + state.RoadChunks.Count);
                GUILayout.Label("Chunks chemins : " + state.PathChunkIndex + " / " + state.PathChunks.Count);
                GUILayout.Label("Segments routes : " + state.RoadCounters.CreatedRoadSegments);
                GUILayout.Label("Segments chemins : " + state.RoadCounters.CreatedPathSegments);
            }

            if (state != null && state.IsActive && GUILayout.Button("Annuler rebuild"))
            {
                _rebuildCancelRequested = true;
                _visualSettingsStatusMessage = "Annulation du rebuild demandée.";
                Log.Info("GroundOverlay HUD panel: progressive rebuild cancel requested.");
            }
        }

        private void MarkVisualSettingsDirty(string message)
        {
            _visualSettingsDirty = true;
            _visualSettingsStatusMessage = string.IsNullOrWhiteSpace(message)
                ? "Modifié non sauvegardé"
                : message;
        }

        private static bool DrawLayerToggle(string label, ref bool value)
        {
            var next = GUILayout.Toggle(value, label);

            if (next == value)
                return false;

            value = next;
            return true;
        }

        private static bool DrawFloatSlider(string label, ref float value, float min, float max, string format)
        {
            var safeValue = Mathf.Clamp(value, min, max);

            GUILayout.Label(
                label + " : " +
                safeValue.ToString(format, System.Globalization.CultureInfo.InvariantCulture)
            );

            var next = GUILayout.HorizontalSlider(safeValue, min, max, GUILayout.Height(22f));
            GUILayout.Space(4f);

            if (System.Math.Abs(next - value) < 0.0001f)
                return false;

            value = next;
            return true;
        }

        private static bool DrawIntSlider(string label, ref int value, int min, int max)
        {
            var safeValue = Mathf.Clamp(value, min, max);

            GUILayout.Label(label + " : " + safeValue);

            var next = Mathf.RoundToInt(GUILayout.HorizontalSlider(safeValue, min, max, GUILayout.Height(22f)));
            GUILayout.Space(4f);

            if (next == value)
                return false;

            value = next;
            return true;
        }
    }
}
