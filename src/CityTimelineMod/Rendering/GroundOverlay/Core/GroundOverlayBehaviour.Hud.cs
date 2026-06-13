using System;
using CityTimelineMod.Bundles;
using CityTimelineMod.Roads;
using CityTimelineMod.Rendering.Roads;
using CityTimelineMod.Util;
using UnityEngine;

namespace CityTimelineMod.Rendering
{
    internal sealed partial class GroundOverlayBehaviour
    {
        private bool _hudShowBundleDetails = false;
        private bool _hudShowOverlayDetails = false;
        private bool _hudShowRouteStats = false;
        private bool _hudShowLayers = true;
        private bool _hudShowRendering = false;
        private bool _hudShowPerformance = false;
        private bool _hudShowCalibration = false;

        private const int HudSafeMaxRoadSegments = 1000000;
        private const int HudSafeMaxPathSegments = 1000;
        private const int HudSafeMaxWaterSegments = 1000;
        private const int HudSafeMaxWaterAreaFillMeshes = 200;
        private const int HudSafeMaxZoningFillMeshes = 500;

        private void UpdateOverlayHud()
        {
            // Ancien HUD TextMesh supprimé.
            // Le panneau interactif est rendu par OnGUI().
        }

        private static void ApplyControlPanelGuiSkin()
        {
            if (GUI.skin == null)
                return;

            GUI.skin.window.fontSize = 16;
            GUI.skin.label.fontSize = 16;
            GUI.skin.toggle.fontSize = 16;
            GUI.skin.button.fontSize = 16;

            GUI.skin.horizontalSlider.fixedHeight = 18f;
            GUI.skin.horizontalSliderThumb.fixedWidth = 18f;
            GUI.skin.horizontalSliderThumb.fixedHeight = 18f;
        }

        private void NormalizeControlPanelRect()
        {
            var hudWidth = Mathf.Min(680f, Mathf.Max(360f, Screen.width - 16f));
            var hudHeight = Mathf.Min(900f, Mathf.Max(420f, Screen.height - 16f));

            if (_controlPanelRect.width >= hudWidth && _controlPanelRect.height >= hudHeight)
                return;

            var x = Mathf.Clamp(_controlPanelRect.x, 4f, Mathf.Max(4f, Screen.width - hudWidth - 4f));
            var y = Mathf.Clamp(_controlPanelRect.y, 4f, Mathf.Max(4f, Screen.height - hudHeight - 4f));

            _controlPanelRect = new Rect(x, y, hudWidth, hudHeight);
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

            _controlPanelRect = GUI.Window(
                93014,
                _controlPanelRect,
                DrawControlPanelWindow,
                "CityTimelineMod — Panneau HUD"
            );
        }

        private static bool DrawHudFoldout(string label, ref bool expanded)
        {
            var text = (expanded ? "▼ " : "▶ ") + label;

            if (GUILayout.Button(text))
                expanded = !expanded;

            return expanded;
        }

        private void DrawCompactHudSummary()
        {
            if (_config == null)
                return;

            var stats = _roadSemanticStats ?? new RoadSemanticStats();
            var state = _progressiveRebuild;

            GUILayout.Space(6f);
            GUILayout.Label("Résumé");

            GUILayout.Label(
                "Bundle : " +
                (!string.IsNullOrWhiteSpace(_config.ActiveBundleDisplayName)
                    ? _config.ActiveBundleDisplayName
                    : SafeHudValue(_config.ActiveBundleId))
            );

            GUILayout.Label(
                "GeoJSON : routes=" + stats.Roads +
                " | chemins=" + stats.Paths +
                " | total=" + stats.Total
            );

            GUILayout.Label(
                "Import CS2 : enabled=" + _config.RuntimeRoadImportEnabled +
                " | pipeline=" + SafeHudValue(_config.RuntimeRoadImportPipelineMode) +
                " | max=" + (_config.RuntimeRoadImportMaxSegments <= 0 ? "illimité" : _config.RuntimeRoadImportMaxSegments.ToString()) +
                " | mode=" + SafeHudValue(_config.RuntimeRoadImportSelectionMode)
            );

            if (_config.RuntimeRoadImportShowProgressInHud)
            {
                GUILayout.Label(RuntimeRoadSpawner.GetRuntimeImportHudText());
            }

            if (state != null)
            {
                GUILayout.Label(
                    "Overlay : " +
                    state.Phase +
                    " | routes " + state.RoadChunkIndex + "/" + state.RoadChunks.Count +
                    " | chemins " + state.PathChunkIndex + "/" + state.PathChunks.Count
                );
            }
            else
            {
                GUILayout.Label("Overlay : idle");
            }
        }

        private void DrawControlPanelWindow(int windowId)
        {
            var visibilityChanged = false;
            var alphaChanged = false;

            _controlPanelScroll = GUILayout.BeginScrollView(
                _controlPanelScroll,
                GUILayout.Width(_controlPanelRect.width - 18f),
                GUILayout.Height(_controlPanelRect.height - 38f)
            );

            DrawDisplayPresetButtons();
            DrawCompactHudSummary();
            DrawRuntimeRoadImportSection();

            if (DrawHudFoldout("Détails bundle / eau", ref _hudShowBundleDetails))
            {
                DrawBundleSelector();
                DrawBundleSummarySection();
                DrawBundleWaterContractInfo();
            }

            if (DrawHudFoldout("État overlay / rebuild", ref _hudShowOverlayDetails))
            {
                DrawVisualSettingsStatus();
                DrawProgressiveRebuildStatus();
            }

            if (DrawHudFoldout("Statistiques routes GeoJSON", ref _hudShowRouteStats))
            {
                DrawRoadSemanticStatsSection();
            }

            if (!DrawHudFoldout("Couches visibles / transparence", ref _hudShowLayers))
            {
                GUILayout.EndScrollView();
                GUI.DragWindow(new Rect(0f, 0f, 10000f, 24f));
                return;
            }

            GUILayout.Space(12f);
            GUILayout.Label("Couches visibles");

            visibilityChanged |= DrawLayerToggle("Zonage", ref _config.RenderZoning);
            visibilityChanged |= DrawLayerToggle("Routes", ref _config.RenderRoads);
            visibilityChanged |= DrawLayerToggle("Chemins", ref _config.RenderPaths);

            var waterVisible = _config.RenderWaterLines && _config.RenderWaterAreas;
            var newWaterVisible = GUILayout.Toggle(waterVisible, "Eau");
            if (newWaterVisible != waterVisible)
            {
                _config.RenderWaterLines = newWaterVisible;
                _config.RenderWaterAreas = newWaterVisible;
                visibilityChanged = true;
            }

            visibilityChanged |= DrawLayerToggle("Limites worldmap / heightmap", ref _config.RenderMapBounds);

            GUILayout.Space(12f);
            GUILayout.Label("Transparence");

            alphaChanged |= DrawFloatSlider("Alpha zonage", ref _config.ZoningAlpha, 0f, 1f, "0.00");
            alphaChanged |= DrawFloatSlider("Alpha routes", ref _config.RoadAlpha, 0f, 1f, "0.00");
            alphaChanged |= DrawFloatSlider("Alpha chemins", ref _config.PathAlpha, 0f, 1f, "0.00");

            var waterMainAlpha = _config.WaterLineAlpha;
            if (DrawFloatSlider("Alpha eau lignes/contours", ref waterMainAlpha, 0f, 1f, "0.00"))
            {
                _config.WaterLineAlpha = waterMainAlpha;
                _config.WaterAreaOutlineAlpha = waterMainAlpha;
                alphaChanged = true;
            }

            alphaChanged |= DrawFloatSlider("Alpha eau remplissage", ref _config.WaterAreaFillAlpha, 0f, 1f, "0.00");

            if (DrawFloatSlider("Alpha limites", ref _config.MapBoundsAlpha, 0f, 1f, "0.00"))
            {
                alphaChanged = true;
                _controlPanelRebuildPending = true;
            }

            GUILayout.Space(12f);
            GUILayout.Label("Hauteur");

            if (DrawFloatSlider("Ground margin", ref _config.GroundMargin, 0f, 600f, "0"))
            {
                _controlPanelRebuildPending = true;
            }

            if (!DrawHudFoldout("Performance overlay", ref _hudShowPerformance))
            {
                GUILayout.Space(8f);
            }
            else
            {
                GUILayout.Space(12f);
                GUILayout.Label("Performance");

                if (DrawIntSlider("Budget zonage", ref _config.MaxZoningFillMeshesDebug, 0, 20000))
                {
                    _controlPanelRebuildPending = true;
                }

                if (DrawIntSlider("Budget routes", ref _config.MaxRoadSegmentsDebug, 1000, 800000))
                {
                    _controlPanelRebuildPending = true;
                }

                if (DrawIntSlider("Budget chemins", ref _config.MaxPathSegmentsDebug, 1000, 800000))
                {
                    _controlPanelRebuildPending = true;
                }

                if (DrawIntSlider("Budget eau segments", ref _config.MaxWaterSegmentsDebug, 1000, 400000))
                {
                    _controlPanelRebuildPending = true;
                }

                if (DrawIntSlider("Budget eau surfaces", ref _config.MaxWaterAreaFillMeshesDebug, 0, 2000))
                {
                    _controlPanelRebuildPending = true;
                }

                if (DrawIntSlider("Niveau routes", ref _config.MinimumRoadDebugTier, 0, 4))
                {
                    _controlPanelRebuildPending = true;
                }

                if (DrawIntSlider("Pas / pointStride", ref _config.PointStride, 1, 20))
                {
                    _controlPanelRebuildPending = true;
                }
            }

            if (DrawHudFoldout("Rendu avancé routes / filtres", ref _hudShowRendering))
            {
                DrawExhaustiveRenderSection();
                DrawRoadRenderingOptionsSection();
                DrawRoadSemanticFilterSection();
            }

            if (DrawHudFoldout("Calage avancé", ref _hudShowCalibration))
            {
                GUILayout.Space(12f);
                GUILayout.Label("Calage avancé");

                _controlPanelCalibrationUnlocked = GUILayout.Toggle(
                    _controlPanelCalibrationUnlocked,
                    "Déverrouiller le calage"
                );

                if (_controlPanelCalibrationUnlocked)
                {
                    if (DrawFloatSlider("Offset X", ref _config.WorldOriginX, -5000f, 5000f, "0.0"))
                    {
                        _controlPanelRebuildPending = true;
                    }

                    if (DrawFloatSlider("Offset Z", ref _config.WorldOriginZ, -5000f, 5000f, "0.0"))
                    {
                        _controlPanelRebuildPending = true;
                    }

                    if (DrawFloatSlider("Rotation degrés", ref _config.OverlayRotationDegrees, -30f, 30f, "0.00"))
                    {
                        _controlPanelRebuildPending = true;
                    }

                    if (DrawFloatSlider("Scale X", ref _config.OverlayScaleX, 0.5f, 1.5f, "0.000"))
                    {
                        _controlPanelRebuildPending = true;
                    }

                    if (DrawFloatSlider("Scale Z", ref _config.OverlayScaleZ, 0.5f, 1.5f, "0.000"))
                    {
                        _controlPanelRebuildPending = true;
                    }

                    GUILayout.Label(
                        "worldScale : " +
                        _config.WorldScale.ToString("0.000000", System.Globalization.CultureInfo.InvariantCulture) +
                        " / lecture seule"
                    );
                }
                else
                {
                    GUILayout.Label("Calage verrouillé.");
                }
            }

            GUILayout.Space(12f);

            GUILayout.BeginHorizontal();

            if (GUILayout.Button(_controlPanelRebuildPending ? "Appliquer / reconstruire *" : "Appliquer / reconstruire"))
            {
                var hadPendingChanges = _controlPanelRebuildPending;
                _controlPanelRebuildPending = false;

                if (hadPendingChanges)
                    MarkVisualSettingsDirty("Réglages reconstruits — sauvegarde optionnelle.");

                ApplyOverlayRebuildSafetyGuard("HUD apply");

                Log.Info("GroundOverlay HUD panel: rebuild requested.");
                RequestOverlayRebuild("HUD apply", true);
            }

            if (GUILayout.Button("Fermer"))
            {
                _config.ShowOverlayHud = false;
                _controlPanelLogOnce = false;
                Log.Info("GroundOverlay HUD panel: closed.");
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(4f);

            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Sauvegarder visuels"))
            {
                _config.SaveVisualSettingsToConfig();
                _visualSettingsDirty = false;
                _visualSettingsStatusMessage = "Visuels sauvegardés dans config.json.";
                Log.Info("GroundOverlay HUD panel: visual settings saved from HUD.");
            }

            if (GUILayout.Button("Recharger visuels"))
            {
                _config.LoadVisualSettingsFromConfig();
                SyncVisibilityStateFromConfig();
                _currentDisplayPresetLabel = "Config";
                _visualSettingsDirty = false;
                _visualSettingsStatusMessage = "Visuels rechargés depuis config.json.";
                _controlPanelRebuildPending = false;
Log.Info("GroundOverlay HUD panel: visual settings reloaded from config.");
ApplyOverlayRebuildSafetyGuard("HUD reload visual settings");
RequestOverlayRebuild("HUD reload visual settings", true);
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(4f);
            if (_controlPanelRebuildPending)
                GUILayout.Label("Changement en attente : cliquer Appliquer / reconstruire.");
            GUILayout.Label("Live HUD — sauvegarde visuelle optionnelle.");

            if (visibilityChanged)
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
            else if (alphaChanged)
            {
                MarkVisualSettingsDirty("Transparence modifiée — non sauvegardée.");
                ApplyCurrentOverlayVisibilityToMaterials();
            }

            GUILayout.EndScrollView();

            GUI.DragWindow(new Rect(0f, 0f, 10000f, 24f));
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

        private void DrawRuntimeRoadImportSection()
        {
            if (_config == null)
                return;

            GUILayout.Space(8f);
            GUILayout.Label("Import routes CS2");
            GUILayout.Label(RuntimeRoadSpawner.GetCacheStatusText());
            GUILayout.Label("Garde-fou runtimeRoadImportEnabled : " + _config.RuntimeRoadImportEnabled);
            GUILayout.Label("runOnce : " + _config.RuntimeRoadImportRunOnce);
            GUILayout.Label("Pipeline : " + SafeHudValue(_config.RuntimeRoadImportPipelineMode));

            DrawLayerToggle("Autoriser import runtime", ref _config.RuntimeRoadImportEnabled);
            DrawLayerToggle("Refuser un second import identique", ref _config.RuntimeRoadImportRunOnce);
            DrawLayerToggle("Afficher progression runtime HUD", ref _config.RuntimeRoadImportShowProgressInHud);
            DrawLayerToggle("Logs sélection import verbose", ref _config.RuntimeRoadImportVerboseSelectionLogs);

            DrawIntSlider("Max segments import (0 = illimité)", ref _config.RuntimeRoadImportMaxSegments, 0, 1000000);
            DrawIntSlider("Batch import runtime", ref _config.RuntimeRoadImportBatchSize, 1, 4096);

            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Pipeline fast-flush"))
            {
                _config.RuntimeRoadImportPipelineMode = "fast-flush";
                MarkVisualSettingsDirty("Pipeline import runtime = fast-flush.");
            }

            if (GUILayout.Button("Pipeline batch-safe"))
            {
                _config.RuntimeRoadImportPipelineMode = "batch-safe";
                MarkVisualSettingsDirty("Pipeline import runtime = batch-safe.");
            }

            GUILayout.EndHorizontal();

            GUILayout.Label("Batch : 64 qualité / 128-256 compromis / 512+ rapide.");
            GUILayout.Label("Mode import : " + SafeHudValue(_config.RuntimeRoadImportSelectionMode));
            GUILayout.Label("Source : " + SafeHudValue(_config.RuntimeRoadImportSourceFilter));
            GUILayout.Label("Highway : " + SafeHudValue(_config.RuntimeRoadImportHighwayFilter));
            GUILayout.Label("Stage : " + SafeHudValue(_config.RuntimeRoadImportStageFilter));
            GUILayout.Label("Distance bucket : " + _config.RuntimeRoadImportDistanceBucketMeters.ToString("0"));
            GUILayout.Label("0 = illimité / tous les segments éligibles du bundle.");

            if (_config.RuntimeRoadImportShowProgressInHud)
            {
                GUILayout.Space(4f);
                GUILayout.Label(RuntimeRoadSpawner.GetRuntimeImportHudText());
            }

            GUILayout.Space(4f);
            GUILayout.Label("Filtres rapides import runtime");

            GUILayout.BeginHorizontal();
            DrawRuntimeImportFilterButton("Toutes routes", "roads", "all", "all");
            DrawRuntimeImportFilterButton("Primary", "roads", "primary", "all");
            DrawRuntimeImportFilterButton("Secondary", "roads", "secondary", "all");
            DrawRuntimeImportFilterButton("Tertiary", "roads", "tertiary", "all");
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            DrawRuntimeImportFilterButton("Primary+Secondary", "roads", "primary,secondary", "all");
            DrawRuntimeImportFilterButton("Backbone", "roads", "primary,secondary,trunk,motorway", "backbone");
            DrawRuntimeImportFilterButton("Urbain", "roads", "primary,secondary,tertiary,residential", "urban");
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            DrawRuntimeImportFilterButton("Residential", "roads", "residential", "all");
            DrawRuntimeImportFilterButton("Service", "roads", "service", "all");
            DrawRuntimeImportFilterButton("Chemins voir cache", "paths", "footway,cycleway,path", "all");
            GUILayout.EndHorizontal();

            var previousGuiEnabled = GUI.enabled;
            GUI.enabled = previousGuiEnabled && _config.RuntimeRoadImportEnabled;

            if (GUILayout.Button("Importer routes runtime"))
                RuntimeRoadSpawner.ImportCachedRoadLinesNow();

            GUI.enabled = previousGuiEnabled;

            if (!_config.RuntimeRoadImportEnabled)
                GUILayout.Label("Import bloque : runtimeRoadImportEnabled=false.");

            GUI.enabled = false;
            GUILayout.Button("Undo import routes runtime (désactivé — non sûr)");
            GUI.enabled = previousGuiEnabled;
            GUILayout.Label("Undo désactivé : suppression directe des entités réseau non sûre.");

            if (GUILayout.Button("Diagnostic contenu GeoJSON routes"))
                RuntimeRoadSpawner.DumpCachedRoadContentDiagnostics(30);

            if (GUILayout.Button("Diagnostic prefabs vanilla routes"))
                RuntimeRoadSpawner.DumpVanillaRoadPrefabDiagnostics(200);

            if (GUILayout.Button("Diagnostic noms exacts prefabs routes"))
                RuntimeRoadSpawner.DumpExactRoadPrefabNameDiagnostics(200);

            if (GUILayout.Button("Diagnostic mapping import routes"))
                RuntimeRoadSpawner.DumpCachedRoadImportMappingDiagnostics(50);

            if (GUILayout.Button("Diagnostic routes importées"))
                RuntimeRoadSpawner.DumpImportedRoadDiagnostics(20);

            if (GUILayout.Button("Diagnostic routes vanilla placées"))
                RuntimeRoadSpawner.DumpNearbyVanillaRoadDiagnostics(30, 10000f);
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

        private void DrawRuntimeImportFilterButton(string label, string source, string highway, string stage)
        {
            var active =
                string.Equals(_config.RuntimeRoadImportSourceFilter, source, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(_config.RuntimeRoadImportHighwayFilter, highway, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(_config.RuntimeRoadImportStageFilter, stage, StringComparison.OrdinalIgnoreCase);

            var buttonLabel = active ? "[" + label + "]" : label;

            if (!GUILayout.Button(buttonLabel))
                return;

            _config.RuntimeRoadImportSourceFilter = source;
            _config.RuntimeRoadImportHighwayFilter = highway;
            _config.RuntimeRoadImportStageFilter = stage;

            Log.Info(
                "HUD runtime import filter changed: source=" + SafeLogValue(source) +
                ", highway=" + SafeLogValue(highway) +
                ", stage=" + SafeLogValue(stage)
            );
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
            GUILayout.Label("Preset live : " + _currentDisplayPresetLabel);

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

        private void DrawDisplayPresetButtons()
        {
            GUILayout.Label("Presets affichage");

            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Debug complet"))
                ApplyDisplayPreset("debug");

            if (GUILayout.Button("Lisible"))
                ApplyDisplayPreset("readable");

            if (GUILayout.Button("Routes"))
                ApplyDisplayPreset("roads");

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Eau"))
                ApplyDisplayPreset("water");

            if (GUILayout.Button("Zoning"))
                ApplyDisplayPreset("zoning");

            if (GUILayout.Button("Bounds"))
                ApplyDisplayPreset("bounds");

            GUILayout.EndHorizontal();
        }

        private void ApplyDisplayPreset(string preset)
        {
            if (_config == null)
                return;

            var normalized = string.IsNullOrWhiteSpace(preset)
                ? string.Empty
                : preset.Trim().ToLowerInvariant();

            switch (normalized)
            {
                case "debug":
                    _config.RenderZoning = true;
                    _config.RenderRoads = true;
                    _config.RenderPaths = true;
                    SetWaterRenderFlags(true);
                    _config.RenderMapBounds = true;

                    _config.ZoningAlpha = 0.25f;
                    _config.RoadAlpha = 0.75f;
                    _config.PathAlpha = 0.55f;
                    _config.WaterLineAlpha = 0.75f;
                    _config.WaterAreaOutlineAlpha = 0.75f;
                    _config.WaterAreaFillAlpha = 0.25f;
                    _config.MapBoundsAlpha = 0.95f;
                    break;

                case "readable":
                    _config.RenderZoning = true;
                    _config.RenderRoads = true;
                    _config.RenderPaths = true;
                    SetWaterRenderFlags(true);
                    _config.RenderMapBounds = true;

                    _config.ZoningAlpha = 0.05f;
                    _config.RoadAlpha = 0.20f;
                    _config.PathAlpha = 0.35f;
                    _config.WaterLineAlpha = 0.20f;
                    _config.WaterAreaOutlineAlpha = 0.20f;
                    _config.WaterAreaFillAlpha = 0.05f;
                    _config.MapBoundsAlpha = 0.85f;
                    break;

                case "roads":
                    _config.RenderZoning = false;
                    _config.RenderRoads = true;
                    _config.RenderPaths = true;
                    SetWaterRenderFlags(false);
                    _config.RenderMapBounds = true;

                    _config.RoadAlpha = 0.75f;
                    _config.PathAlpha = 0.55f;
                    _config.MapBoundsAlpha = 0.60f;
                    break;

                case "water":
                    _config.RenderZoning = false;
                    _config.RenderRoads = false;
                    _config.RenderPaths = false;
                    SetWaterRenderFlags(true);
                    _config.RenderMapBounds = true;

                    _config.WaterLineAlpha = 0.80f;
                    _config.WaterAreaOutlineAlpha = 0.80f;
                    _config.WaterAreaFillAlpha = 0.22f;
                    _config.MapBoundsAlpha = 0.60f;
                    break;

                case "zoning":
                    _config.RenderZoning = true;
                    _config.RenderRoads = false;
                    _config.RenderPaths = false;
                    SetWaterRenderFlags(false);
                    _config.RenderMapBounds = true;

                    _config.ZoningAlpha = 0.18f;
                    _config.MapBoundsAlpha = 0.60f;
                    break;

                case "bounds":
                    _config.RenderZoning = false;
                    _config.RenderRoads = false;
                    _config.RenderPaths = false;
                    SetWaterRenderFlags(false);
                    _config.RenderMapBounds = true;

                    _config.MapBoundsAlpha = 0.95f;
                    break;

                default:
                    Log.Info("GroundOverlay HUD panel: unknown display preset=" + preset);
                    return;
            }

            SyncVisibilityStateFromConfig();
            _controlPanelRebuildPending = false;
            _currentDisplayPresetLabel = ResolveDisplayPresetLabel(normalized);
            _visualSettingsDirty = true;
            _visualSettingsStatusMessage = "Preset appliqué — non sauvegardé.";

            Log.Info(
                "GroundOverlay HUD panel: display preset applied=" + normalized +
                ", zoning=" + _config.RenderZoning +
                ", roads=" + _config.RenderRoads +
                ", paths=" + _config.RenderPaths +
                ", waterLines=" + _config.RenderWaterLines +
                ", waterAreas=" + _config.RenderWaterAreas +
                ", bounds=" + _config.RenderMapBounds
            );

            ApplyOverlayRebuildSafetyGuard("HUD display preset: " + normalized);
            RequestOverlayRebuild("HUD display preset: " + normalized, true);
        }

        private static string ResolveDisplayPresetLabel(string preset)
        {
            switch (preset)
            {
                case "debug":
                    return "Debug complet";
                case "readable":
                    return "Lisible";
                case "roads":
                    return "Routes";
                case "water":
                    return "Eau";
                case "zoning":
                    return "Zoning";
                case "bounds":
                    return "Bounds";
                default:
                    return "Custom";
            }
        }

        private void MarkVisualSettingsDirty(string message)
        {
            _currentDisplayPresetLabel = "Custom";
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
