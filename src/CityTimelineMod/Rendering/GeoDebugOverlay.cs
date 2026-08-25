using System;
using System.Collections.Generic;
using System.Reflection;
using CityTimelineMod.Config;
using CityTimelineMod.Bundles;
using CityTimelineMod.Geometry;
using CityTimelineMod.Importers;
using CityTimelineMod.Rendering.Batching;
using CityTimelineMod.Rendering.Bounds;
using CityTimelineMod.Rendering.Core;
using CityTimelineMod.Rendering.Materials;
using CityTimelineMod.Rendering.Roads;
using CityTimelineMod.Rendering.Railways;
using CityTimelineMod.Rendering.Services;
using CityTimelineMod.Rendering.Statistics;
using CityTimelineMod.Rendering.Water;
using CityTimelineMod.Rendering.Zoning;
using CityTimelineMod.Terrain;
using CityTimelineMod.Util;
using UnityEngine;

namespace CityTimelineMod.Rendering
{
    internal static class GeoDebugOverlay
    {
        private const string RootName = "CityTimelineMod_GroundOverlay";
        private const string OverlayComponentFullName =
            "CityTimelineMod.Rendering.GroundOverlayBehaviour";
        private const string OwnedMaterialPrefix =
            "CityTimelineMod_OverlayMaterial_";
        private static GameObject _root;

        internal static bool IsInstalled
        {
            get
            {
                var overlay = GetCurrentOverlay();
                return overlay != null && overlay.IsOperational;
            }
        }

        internal static bool Install(List<List<GeoPoint>> lines, GeoOverlayConfig config)
        {
            return Install(lines, new List<List<GeoPoint>>(), new List<List<GeoPoint>>(), config);
        }

        internal static bool Install(
            List<List<GeoPoint>> waterLines,
            List<List<GeoPoint>> roadLines,
            GeoOverlayConfig config
        )
        {
            return Install(waterLines, new List<List<GeoPoint>>(), roadLines, config);
        }

        internal static bool Install(
            List<List<GeoPoint>> waterLines,
            List<List<GeoPoint>> waterAreaOutlines,
            List<List<GeoPoint>> roadLines,
            GeoOverlayConfig config
        )
        {
            return Install(waterLines, waterAreaOutlines, ConvertRoadLines(roadLines), config);
        }

        internal static bool Install(
            List<List<GeoPoint>> waterLines,
            List<List<GeoPoint>> waterAreaOutlines,
            List<GeoRoadLine> roadLines,
            GeoOverlayConfig config
        )
        {
            return Install(waterLines, waterAreaOutlines, roadLines, new List<GeoZoningPolygon>(), config);
        }

        internal static bool Install(
            List<List<GeoPoint>> waterLines,
            List<List<GeoPoint>> waterAreaOutlines,
            List<GeoRoadLine> roadLines,
            List<GeoZoningPolygon> zoningPolygons,
            GeoOverlayConfig config
        )
        {
            return Install(
                waterLines,
                waterAreaOutlines,
                roadLines,
                zoningPolygons,
                new List<GeoServicePoint>(),
                new ServiceGeoJsonLoadResult(),
                new BundleHudSnapshot(),
                new List<GeoRailwayLine>(),
                false,
                "Aucune donnée ferroviaire disponible dans ce bundle.",
                config
            );
        }

        internal static bool Install(
            List<List<GeoPoint>> waterLines,
            List<List<GeoPoint>> waterAreaOutlines,
            List<GeoRoadLine> roadLines,
            List<GeoZoningPolygon> zoningPolygons,
            List<GeoServicePoint> servicePoints,
            ServiceGeoJsonLoadResult serviceLoadResult,
            BundleHudSnapshot bundleHudSnapshot,
            List<GeoRailwayLine> railwayLines,
            bool railwayAvailable,
            string railwayStatus,
            GeoOverlayConfig config
        )
        {
            if (!Mod.RuntimeEnabled || config == null)
            {
                Log.Info("GroundOverlay: install blocked because the runtime gate is closed.");
                return false;
            }

            var currentOverlay = GetCurrentOverlay();
            if (currentOverlay != null && currentOverlay.IsOperational)
            {
                if (!currentOverlay.TryStageOverlayReplacement(
                    waterLines,
                    waterAreaOutlines,
                    roadLines,
                    zoningPolygons,
                    servicePoints,
                    serviceLoadResult,
                    bundleHudSnapshot,
                    railwayLines,
                    railwayAvailable,
                    railwayStatus,
                    config
                ))
                {
                    Log.Error(
                        "GroundOverlay: replacement staging failed; current overlay remains active."
                    );
                    return false;
                }

                Log.Info(
                    "GroundOverlay: replacement accepted; current overlay retained until staged commit."
                );
                return true;
            }

            if (!Uninstall())
            {
                Log.Error("GroundOverlay: install blocked because a stale CTM overlay could not be fully torn down.");
                return false;
            }

            GameObject root = null;

            try
            {
                root = new GameObject(RootName);
                _root = root;
                UnityEngine.Object.DontDestroyOnLoad(root);

                var overlay = root.AddComponent<GroundOverlayBehaviour>();
                overlay.Setup(
                    waterLines,
                    waterAreaOutlines,
                    roadLines,
                    zoningPolygons,
                    servicePoints,
                    serviceLoadResult,
                    bundleHudSnapshot,
                    railwayLines,
                    railwayAvailable,
                    railwayStatus,
                    config
                );

                if (!overlay.TryCreateOverlayNow() || !overlay.IsOperational)
                    throw new InvalidOperationException("Ground overlay creation did not reach an operational state.");

                Log.Info("GroundOverlay: installed HARD visible segment overlay.");
                return true;
            }
            catch
            {
                if (root != null && !DestroyOverlayRoot(root))
                    Log.Error("GroundOverlay: failed installation root retained for teardown retry.");

                _root = null;
                throw;
            }
        }

        internal static bool Uninstall()
        {
            var current = _root;
            _root = null;

            var roots = new List<GameObject>();
            var instanceIds = new HashSet<int>();
            var complete = true;

            if (current != null && instanceIds.Add(current.GetInstanceID()))
                roots.Add(current);

            // Resources.FindObjectsOfTypeAll includes inactive objects and
            // DontDestroyOnLoad roots that survived a hot/domain reload.
            try
            {
                var overlays = Resources.FindObjectsOfTypeAll<GroundOverlayBehaviour>();
                for (var i = 0; i < overlays.Length; i++)
                {
                    var overlay = overlays[i];
                    var root = overlay != null ? overlay.gameObject : null;

                    if (root != null &&
                        root.scene.IsValid() &&
                        instanceIds.Add(root.GetInstanceID()))
                    {
                        roots.Add(root);
                    }
                }
            }
            catch (Exception ex)
            {
                complete = false;
                Log.Error("GroundOverlay: current component discovery failed. " + ex);
            }

            // A domain/hot reload can leave a component whose old managed type
            // identity is no longer assignable to the current generic type.
            // The exact CTM root marker covers that case while scene validity
            // prevents touching prefab/assets owned elsewhere.
            try
            {
                var sceneObjects = Resources.FindObjectsOfTypeAll<GameObject>();
                for (var i = 0; i < sceneObjects.Length; i++)
                {
                    var root = sceneObjects[i];
                    if (root == null || !root.scene.IsValid())
                        continue;

                    var isCurrentRoot = string.Equals(root.name, RootName, StringComparison.Ordinal);
                    var isDisposedRoot = root.name != null &&
                        root.name.StartsWith(RootName + "_Disposed_", StringComparison.Ordinal);

                    if ((isCurrentRoot || isDisposedRoot) &&
                        HasCtmComponent(root, OverlayComponentFullName) &&
                        instanceIds.Add(root.GetInstanceID()))
                    {
                        roots.Add(root);
                    }
                }
            }
            catch (Exception ex)
            {
                complete = false;
                Log.Error("GroundOverlay: prior-assembly root discovery failed. " + ex);
            }

            for (var i = 0; i < roots.Count; i++)
            {
                try
                {
                    if (!DestroyOverlayRoot(roots[i]))
                        complete = false;
                }
                catch (Exception ex)
                {
                    complete = false;
                    Log.Error("GroundOverlay: root teardown failed; remaining roots will still be processed. " + ex);
                }
            }

            return complete;
        }

        private static bool DestroyOverlayRoot(GameObject root)
        {
            if (root == null)
                return true;

            // Disable and mark ownership before deferred Unity destruction so
            // no Update/OnGUI callback can revive this root.
            try
            {
                root.SetActive(false);
                root.name = RootName + "_Disposed_" + root.GetInstanceID();
            }
            catch (Exception ex)
            {
                Log.Error("GroundOverlay: root could not be disabled/marked; destruction blocked. " + ex);
                return false;
            }

            try
            {
                var overlay = root.GetComponent<GroundOverlayBehaviour>();
                if (overlay != null && !overlay.TeardownOverlayResources())
                {
                    Log.Error("GroundOverlay: teardown incomplete; inactive root retained for retry.");
                    return false;
                }

                if (overlay == null && !TryTeardownPriorAssemblyOverlay(root))
                {
                    Log.Error("GroundOverlay: prior-assembly teardown incomplete; inactive root retained for retry.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.Error("GroundOverlay: resource teardown failed; inactive root retained for retry. " + ex);
                return false;
            }

            if (!DestroyResidualRootResources(root))
            {
                Log.Error("GroundOverlay: residual cleanup incomplete; inactive root retained for retry.");
                return false;
            }

            try
            {
                UnityEngine.Object.Destroy(root);
                Log.Info("GroundOverlay: uninstalled.");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("GroundOverlay: root destruction failed; inactive root retained for retry. " + ex);
                return false;
            }
        }

        private static bool DestroyResidualRootResources(GameObject root)
        {
            var complete = true;
            var destroyedMaterials = new HashSet<int>();
            Renderer[] renderers;

            try
            {
                renderers = root.GetComponentsInChildren<Renderer>(true);
            }
            catch (Exception ex)
            {
                complete = false;
                renderers = new Renderer[0];
                Log.Error("GroundOverlay: residual renderer discovery failed. " + ex);
            }

            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null)
                    continue;

                try
                {
                    var filter = renderer.GetComponent<MeshFilter>();
                    var mesh = filter != null ? filter.sharedMesh : null;
                    var ownsBatchMaterials = mesh != null &&
                        !string.IsNullOrEmpty(mesh.name) &&
                        mesh.name.EndsWith("_mesh", StringComparison.Ordinal);

                    var materials = renderer.sharedMaterials;
                    var retained = new List<Material>();
                    for (var j = 0; j < materials.Length; j++)
                    {
                        var material = materials[j];
                        if (material == null)
                            continue;

                        var isNamedCtmMaterial = material.name != null &&
                            material.name.StartsWith(OwnedMaterialPrefix, StringComparison.Ordinal);
                        if (!isNamedCtmMaterial && !ownsBatchMaterials)
                        {
                            retained.Add(material);
                            continue;
                        }

                        try
                        {
                            if (destroyedMaterials.Add(material.GetInstanceID()))
                                UnityEngine.Object.Destroy(material);
                        }
                        catch (Exception ex)
                        {
                            complete = false;
                            retained.Add(material);
                            Log.Error("GroundOverlay: residual material cleanup failed. " + ex);
                        }
                    }

                    renderer.sharedMaterials = retained.ToArray();
                }
                catch (Exception ex)
                {
                    complete = false;
                    Log.Error("GroundOverlay: residual renderer cleanup failed. " + ex);
                }
            }

            MeshFilter[] filters;
            try
            {
                filters = root.GetComponentsInChildren<MeshFilter>(true);
            }
            catch (Exception ex)
            {
                complete = false;
                filters = new MeshFilter[0];
                Log.Error("GroundOverlay: residual mesh discovery failed. " + ex);
            }

            for (var i = 0; i < filters.Length; i++)
            {
                var filter = filters[i];
                var mesh = filter != null ? filter.sharedMesh : null;
                if (mesh == null ||
                    string.IsNullOrEmpty(mesh.name) ||
                    !mesh.name.EndsWith("_mesh", StringComparison.Ordinal))
                {
                    continue;
                }

                try
                {
                    UnityEngine.Object.Destroy(mesh);
                    filter.sharedMesh = null;
                }
                catch (Exception ex)
                {
                    complete = false;
                    Log.Error("GroundOverlay: residual mesh cleanup failed. " + ex);
                }
            }

            return complete;
        }

        private static bool TryTeardownPriorAssemblyOverlay(GameObject root)
        {
            var components = root.GetComponents<Component>();
            for (var i = 0; i < components.Length; i++)
            {
                var component = components[i];
                if (!IsCtmComponent(component, OverlayComponentFullName))
                    continue;

                var method = component.GetType().GetMethod(
                    "TeardownOverlayResources",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    Type.EmptyTypes,
                    null
                );
                if (method == null)
                    return true;

                var result = method.Invoke(component, null);
                return method.ReturnType != typeof(bool) || (result is bool value && value);
            }

            return true;
        }

        private static bool HasCtmComponent(GameObject root, string fullName)
        {
            if (root == null)
                return false;

            var components = root.GetComponents<Component>();
            for (var i = 0; i < components.Length; i++)
            {
                if (IsCtmComponent(components[i], fullName))
                    return true;
            }

            return false;
        }

        private static bool IsCtmComponent(Component component, string fullName)
        {
            if (component == null)
                return false;

            var type = component.GetType();
            return string.Equals(type.FullName, fullName, StringComparison.Ordinal) &&
                string.Equals(
                    type.Assembly.GetName().Name,
                    typeof(Mod).Assembly.GetName().Name,
                    StringComparison.Ordinal
                );
        }

        private static GroundOverlayBehaviour GetCurrentOverlay()
        {
            if (_root == null || !_root.activeSelf)
                return null;

            return _root.GetComponent<GroundOverlayBehaviour>();
        }

        internal static void ApplyRuntimeConfigChange(string configKey, object value)
        {
            if (!Mod.RuntimeEnabled)
                return;

            var runtimeConfig = Mod.RuntimeConfig;
            if (runtimeConfig == null)
            {
                Log.Error("GroundOverlay: runtime snapshot unavailable for " + configKey + ".");
                return;
            }

            var overlay = GetCurrentOverlay();
            if (overlay == null)
                return;

            overlay.ApplyRuntimeConfigChange(configKey);
        }

        internal static bool SetRoadHighwayFilter(string filter)
        {
            if (!Mod.RuntimeEnabled)
                return false;

            var runtimeConfig = Mod.RuntimeConfig;
            if (runtimeConfig == null || !runtimeConfig.IsReliable)
            {
                Log.Error(
                    "GroundOverlay: road highway filter change blocked because " +
                    "the runtime configuration is unavailable."
                );
                return false;
            }

            var normalized = string.IsNullOrWhiteSpace(filter)
                ? "all"
                : filter.Trim().ToLowerInvariant();

            var path =
                CityTimelineMod.Config.CityTimelineConfigStorage.ResolveWritableConfigPath();

            Newtonsoft.Json.Linq.JObject updatedRoot;
            string updateError;

            if (!CityTimelineMod.Config.GeoOverlayConfig.TryUpdateRuntimeConfigFile(
                path,
                root =>
                {
                    root["roadHighwayFilter"] = normalized;
                },
                out updatedRoot,
                out updateError
            ))
            {
                Log.Error(
                    "GroundOverlay: road highway filter could not be saved: " +
                    updateError
                );
                return false;
            }

            if (!runtimeConfig.ApplyRuntimeOption(
                "roadHighwayFilter",
                normalized
            ))
            {
                Log.Error(
                    "GroundOverlay: runtime snapshot rejected roadHighwayFilter=" +
                    normalized + "."
                );
                return false;
            }

            ApplyRuntimeConfigChange(
                "roadHighwayFilter",
                normalized
            );

            Log.Info(
                "GroundOverlay: roadHighwayFilter=" +
                normalized
            );

            return true;
        }

        internal static RailwayHudSnapshot GetRailwayHudSnapshot()
        {
            if (!Mod.RuntimeEnabled)
                return RailwayHudSnapshot.Unavailable("Overlay ferroviaire non initialisé.");

            var overlay = GetCurrentOverlay();
            return overlay != null
                ? overlay.GetRailwayHudSnapshot()
                : RailwayHudSnapshot.Unavailable("Overlay ferroviaire non initialisé.");
        }

        internal static bool SetRailwayBoolean(string key, bool value)
        {
            if (!Mod.RuntimeEnabled)
                return false;

            var overlay = GetCurrentOverlay();
            return overlay != null && overlay.SetRailwayBoolean(key, value);
        }

        internal static bool SetRailwayFloat(string key, float value)
        {
            if (!Mod.RuntimeEnabled)
                return false;

            var overlay = GetCurrentOverlay();
            return overlay != null && overlay.SetRailwayFloat(key, value);
        }

        internal static string GetBundleHudSnapshotJson()
        {
            if (!Mod.RuntimeEnabled)
                return "{}";

            var overlay = GetCurrentOverlay();
            return overlay != null ? overlay.GetBundleHudSnapshotJson() : "{}";
        }

        internal static OverlayLayerHudSnapshot GetOverlayLayerHudSnapshot()
        {
            if (!Mod.RuntimeEnabled)
                return OverlayLayerHudSnapshot.Unavailable();

            var overlay = GetCurrentOverlay();
            return overlay != null
                ? overlay.GetOverlayLayerHudSnapshot()
                : OverlayLayerHudSnapshot.Unavailable();
        }

        internal static bool SetOverlayLayerBoolean(string key, bool value)
        {
            if (!Mod.RuntimeEnabled)
                return false;

            var overlay = GetCurrentOverlay();
            return overlay != null && overlay.SetOverlayLayerBoolean(key, value);
        }

        internal static bool SetOverlayLayerFloat(string key, float value)
        {
            if (!Mod.RuntimeEnabled)
                return false;

            var overlay = GetCurrentOverlay();
            return overlay != null && overlay.SetOverlayLayerFloat(key, value);
        }

        internal static string GetOverlaySublayersJson()
        {
            if (!Mod.RuntimeEnabled)
                return "{}";

            var overlay = GetCurrentOverlay();
            return overlay != null ? overlay.GetOverlaySublayersJson() : "{}";
        }

        internal static bool SetOverlaySublayerVisible(string key, bool value)
        {
            if (!Mod.RuntimeEnabled)
                return false;

            var overlay = GetCurrentOverlay();
            return overlay != null && overlay.SetOverlaySublayerVisible(key, value);
        }

        internal static bool SetServiceBoolean(string key, bool value)
        {
            if (!Mod.RuntimeEnabled)
                return false;

            var overlay = GetCurrentOverlay();
            return overlay != null && overlay.SetServiceBoolean(key, value);
        }

        internal static bool SetServiceFloat(string key, float value)
        {
            if (!Mod.RuntimeEnabled)
                return false;

            var overlay = GetCurrentOverlay();
            return overlay != null && overlay.SetServiceFloat(key, value);
        }
        internal static List<GeoRoadLine> ConvertRoadLines(List<List<GeoPoint>> lines)
        {
            var result = new List<GeoRoadLine>();

            if (lines == null)
                return result;

            foreach (var line in lines)
            {
                if (line != null && line.Count >= 2)
                    result.Add(new GeoRoadLine(line, null, null));
            }

            return result;
        }
    }

    internal sealed partial class GroundOverlayBehaviour : MonoBehaviour
    {
        private enum CalibrationMode
        {
            Global,
            Water,
            Roads,
            Render
        }

        private List<List<GeoPoint>> _waterLines;
        private List<List<GeoPoint>> _waterAreaOutlines;
        private List<GeoRoadLine> _roadLines;
        private List<GeoZoningPolygon> _zoningPolygons;
        private GeoOverlayConfig _config;

        private bool VerboseOverlayLogs
        {
            get { return _config != null && _config.VerboseOverlayLogs; }
        }

        private void LogVerboseOverlay(string message)
        {
            if (VerboseOverlayLogs)
                Log.Info(message);
        }

        private GeoBounds _bounds;
        private bool _created;
        private Transform _overlayRenderParent;

        private Transform OverlayRenderParent
        {
            get { return _overlayRenderParent != null ? _overlayRenderParent : transform; }
        }

        private CalibrationMode _activeCalibrationMode = CalibrationMode.Global;

        private readonly List<Material> _zoningMaterials = new List<Material>();
        private readonly List<Material> _roadMaterials = new List<Material>();
        private readonly List<Material> _pathMaterials = new List<Material>();
        private readonly List<Material> _waterLineMaterials = new List<Material>();
        private readonly List<Material> _waterAreaOutlineMaterials = new List<Material>();
        private readonly List<Material> _waterAreaFillMaterials = new List<Material>();
        private readonly List<Material> _mapBoundsMaterials = new List<Material>();
        private readonly List<TextMesh> _roadLabelMeshes = new List<TextMesh>();
        private RoadSemanticStats _roadSemanticStats = new RoadSemanticStats();

        private bool _zoningVisible = true;
        private bool _roadsVisible = true;
        private bool _waterVisible = true;

        private Rect _controlPanelRect = new Rect(24f, 24f, 620f, 760f);
        private Vector2 _controlPanelScroll = Vector2.zero;
        private bool _controlPanelLogOnce;
        private bool _controlPanelCalibrationUnlocked;
        private bool _controlPanelRebuildPending;
        private string _visualSettingsStatusMessage = "Réglages live.";
        private bool _visualSettingsDirty;
        private List<BundleCatalogEntry> _bundleCatalogEntries = new List<BundleCatalogEntry>();
        private int _selectedBundleIndex = -1;
        private string _bundleSelectorStatusMessage = "Catalogue bundles non chargé.";
        private string _pendingBundleReloadId = null;

        private int _debugWaterSegmentLogs;
        private int _debugRoadSegmentLogs;
        private Vector3 _liveCalibrationOffset = Vector3.zero;
        private float _liveCalibrationRotationDegrees = 0f;
        private float _liveCalibrationScaleX = 1f;
        private float _liveCalibrationScaleZ = 1f;
        private ProgressiveOverlayRebuildState _progressiveRebuild;
        private bool _rebuildRestartPending;
        private bool _rebuildCancelRequested;
        private string _pendingRebuildReason;
        private float _pendingRebuildRequestTime;
        private const float RebuildDebounceSeconds = 0.4f;

        internal void Setup(
            List<List<GeoPoint>> waterLines,
            List<List<GeoPoint>> roadLines,
            GeoOverlayConfig config
        )
        {
            Setup(waterLines, new List<List<GeoPoint>>(), GeoDebugOverlay.ConvertRoadLines(roadLines), config);
        }

        internal void Setup(
            List<List<GeoPoint>> waterLines,
            List<List<GeoPoint>> waterAreaOutlines,
            List<List<GeoPoint>> roadLines,
            GeoOverlayConfig config
        )
        {
            Setup(waterLines, waterAreaOutlines, GeoDebugOverlay.ConvertRoadLines(roadLines), config);
        }

        internal void Setup(
            List<List<GeoPoint>> waterLines,
            List<List<GeoPoint>> waterAreaOutlines,
            List<GeoRoadLine> roadLines,
            GeoOverlayConfig config
        )
        {
            Setup(waterLines, waterAreaOutlines, roadLines, new List<GeoZoningPolygon>(), config);
        }

        internal void Setup(
            List<List<GeoPoint>> waterLines,
            List<List<GeoPoint>> waterAreaOutlines,
            List<GeoRoadLine> roadLines,
            List<GeoZoningPolygon> zoningPolygons,
            GeoOverlayConfig config
        )
        {
            Setup(
                waterLines,
                waterAreaOutlines,
                roadLines,
                zoningPolygons,
                new List<GeoServicePoint>(),
                new ServiceGeoJsonLoadResult(),
                new BundleHudSnapshot(),
                new List<GeoRailwayLine>(),
                false,
                "Aucune donnée ferroviaire disponible dans ce bundle.",
                config
            );
        }

        internal void Setup(
            List<List<GeoPoint>> waterLines,
            List<List<GeoPoint>> waterAreaOutlines,
            List<GeoRoadLine> roadLines,
            List<GeoZoningPolygon> zoningPolygons,
            List<GeoServicePoint> servicePoints,
            ServiceGeoJsonLoadResult serviceLoadResult,
            BundleHudSnapshot bundleHudSnapshot,
            List<GeoRailwayLine> railwayLines,
            bool railwayAvailable,
            string railwayStatus,
            GeoOverlayConfig config
        )
        {
            _overlayTeardownRequested = false;
            _overlayTeardownInProgress = false;
            _overlayTeardownComplete = false;
            _waterLines = waterLines ?? new List<List<GeoPoint>>();
            _waterAreaOutlines = waterAreaOutlines ?? new List<List<GeoPoint>>();
            _roadLines = roadLines ?? new List<GeoRoadLine>();
            _zoningPolygons = zoningPolygons ?? new List<GeoZoningPolygon>();
            _config = config;
            InitializeRailwayOverlay(railwayLines, railwayAvailable, railwayStatus);
            InitializeServiceOverlay(servicePoints, serviceLoadResult);

            // Important :
            // on garde les bounds de l'eau comme référence,
            // sinon les routes déplaceraient le centre/origine validé.
            var boundsSource = new List<List<GeoPoint>>();
            boundsSource.AddRange(_waterLines);
            boundsSource.AddRange(_waterAreaOutlines);

            // Preserve water as the calibrated reference whenever it exists.
            // A dry bundle still needs a valid center for its independent railway layer.
            if (boundsSource.Count == 0 && _railwayLines != null)
            {
                for (var i = 0; i < _railwayLines.Count; i++)
                {
                    var railwayLine = _railwayLines[i];

                    if (railwayLine != null && railwayLine.Points != null && railwayLine.Points.Count > 0)
                        boundsSource.Add(railwayLine.Points);
                }
            }

            if (boundsSource.Count == 0 && _servicePoints != null)
            {
                for (var i = 0; i < _servicePoints.Count; i++)
                {
                    var servicePoint = _servicePoints[i];
                    if (servicePoint != null && servicePoint.Point != null)
                        boundsSource.Add(new List<GeoPoint> { servicePoint.Point });
                }
            }

            _bounds = GeoBoundsCalculator.CalculateBounds(boundsSource);
            RecomputeRoadSemanticStats();
            InitializeBundleHudSnapshot(bundleHudSnapshot);
        }

        internal bool TryStageOverlayReplacement(
            List<List<GeoPoint>> waterLines,
            List<List<GeoPoint>> waterAreaOutlines,
            List<GeoRoadLine> roadLines,
            List<GeoZoningPolygon> zoningPolygons,
            List<GeoServicePoint> servicePoints,
            ServiceGeoJsonLoadResult serviceLoadResult,
            BundleHudSnapshot bundleHudSnapshot,
            List<GeoRailwayLine> railwayLines,
            bool railwayAvailable,
            string railwayStatus,
            GeoOverlayConfig config
        )
        {
            if (!IsOperational || config == null)
                return false;

            try
            {
                if (_progressiveRebuild != null && _progressiveRebuild.IsActive)
                    CancelProgressiveOverlayRebuild("bundle replacement superseded staging", true);

                _pendingRebuildReason = null;
                _pendingRebuildRequestTime = 0f;
                _rebuildRestartPending = false;
                _rebuildCancelRequested = false;

                Setup(
                    waterLines,
                    waterAreaOutlines,
                    roadLines,
                    zoningPolygons,
                    servicePoints,
                    serviceLoadResult,
                    bundleHudSnapshot,
                    railwayLines,
                    railwayAvailable,
                    railwayStatus,
                    config
                );

                StartProgressiveOverlayRebuild(
                    "bundle replacement: " +
                    (string.IsNullOrWhiteSpace(config.ActiveBundleId)
                        ? "(unknown)"
                        : config.ActiveBundleId)
                );

                return _progressiveRebuild != null && _progressiveRebuild.IsActive;
            }
            catch (Exception ex)
            {
                if (_progressiveRebuild != null && _progressiveRebuild.IsActive)
                    CancelProgressiveOverlayRebuild("bundle replacement staging error", false);

                Log.Error("GroundOverlay: bundle replacement staging failed. " + ex);
                return false;
            }
        }

        internal bool IsOperational
        {
            get
            {
                return _created && !_overlayTeardownRequested && _config != null && Mod.RuntimeEnabled;
            }
        }

        internal bool TryCreateOverlayNow()
        {
            if (IsOperational)
                return true;

            if (!Mod.RuntimeEnabled || _overlayTeardownRequested || _config == null)
                return false;

            try
            {
                CreateOverlayWithTiming("initial");
                _created = true;
                return true;
            }
            catch (Exception ex)
            {
                _created = false;
                GeoBundleBootstrap.Reset();
                Log.Error("GroundOverlay: initial creation failed; bootstrap remains retryable. " + ex);
                throw;
            }
        }

        internal void ApplyRuntimeConfigChange(string configKey)
        {
            if (!Mod.RuntimeEnabled || _overlayTeardownRequested || _config == null)
                return;

            try
            {
                SyncVisibilityStateFromConfig();

                _visualSettingsDirty = false;
                _visualSettingsStatusMessage = "Options CS2 appliquées au snapshot runtime.";
                _controlPanelRebuildPending = false;

                if (RequiresOverlayRebuildForConfigKey(configKey))
                {
                    Log.Info("GroundOverlay: applying Options UI change with rebuild: " + configKey);
                    RequestOverlayRebuild("Options UI change: " + configKey, true);
                }
                else
                {
                    Log.Info("GroundOverlay: applying Options UI change live: " + configKey);
                    ApplyCurrentOverlayVisibilityToMaterials();
                }
            }
            catch (Exception ex)
            {
                Log.Info("GroundOverlay: failed to apply Options UI change " + configKey + " => " + ex);
            }
        }

        private void Update()
        {
            if (!RuntimeCallbackCanContinue(false))
                return;

            try
            {
                if (!_created)
                {
                    TryCreateOverlayNow();
                    return;
                }

                HandleCalibrationInput();
                if (!RuntimeCallbackCanContinue())
                    return;

                HandleOverlayVisibilityInput();
                if (!RuntimeCallbackCanContinue())
                    return;

                HandlePendingBundleReload();

                if (!RuntimeCallbackCanContinue())
                    return;

                UpdateProgressiveOverlayRebuild();

                if (!RuntimeCallbackCanContinue())
                    return;

                UpdatePendingOverlayRebuildRequests();
                if (!RuntimeCallbackCanContinue())
                    return;

                UpdatePendingRailwaySettingsSave();
                if (!RuntimeCallbackCanContinue())
                    return;

                UpdateRoadLabelBillboards();
                if (!RuntimeCallbackCanContinue())
                    return;

                UpdateOverlayHud();
            }
            catch (Exception ex)
            {
                _created = false;
                Log.Error("GroundOverlay: runtime update failed; overlay will be torn down and retry enabled. " + ex);
                GeoBundleBootstrap.Reset();
                GeoDebugOverlay.Uninstall();
            }
        }

        private bool RuntimeCallbackCanContinue(bool requireCreated = true)
        {
            return Mod.RuntimeEnabled &&
                !_overlayTeardownRequested &&
                (!requireCreated || _created) &&
                gameObject != null &&
                gameObject.activeInHierarchy;
        }



















        private void CreateOverlay()
        {
            ClearOverlayMaterialRegistries();
            if (_waterLines == null || _waterLines.Count == 0)
            {
                Log.Info("GroundOverlay: no water line geometry; continuing with independent overlay layers.");
            }

            var originLon = _config.UseGeoJsonCenter ? _bounds.CenterLon : _config.OriginLon;
            var originLat = _config.UseGeoJsonCenter ? _bounds.CenterLat : _config.OriginLat;

            LogVerboseOverlay("GroundOverlay: creating HARD visible segment overlay.");
            LogVerboseOverlay("GroundOverlay: bounds lon=[" + _bounds.MinLon + ", " + _bounds.MaxLon + "], lat=[" + _bounds.MinLat + ", " + _bounds.MaxLat + "]");
            LogVerboseOverlay("GroundOverlay: center lon=" + _bounds.CenterLon + ", lat=" + _bounds.CenterLat);
            LogVerboseOverlay("GroundOverlay: origin lon=" + originLon + ", lat=" + originLat);
            LogVerboseOverlay("GroundOverlay: world origin x=" + _config.WorldOriginX + ", z=" + _config.WorldOriginZ);
            LogVerboseOverlay("GroundOverlay: worldScale=" + _config.WorldScale + ", groundMargin=" + _config.GroundMargin);

            LogVerboseOverlay(
                "GroundOverlay: overlayRotationDegrees=" + _config.OverlayRotationDegrees +
                ", overlayScaleX=" + _config.OverlayScaleX +
                ", overlayScaleZ=" + _config.OverlayScaleZ +
                ", flipX=" + _config.FlipX +
                ", flipZ=" + _config.FlipZ
            );

            LogVerboseOverlay("GroundOverlay: water segmentWidth=" + _config.SegmentWidth + ", segmentHeight=" + _config.SegmentHeight);
            LogVerboseOverlay("GroundOverlay: waterLineSegmentWidth=" + _config.WaterLineSegmentWidth + ", waterLineSegmentHeight=" + _config.WaterLineSegmentHeight);
            LogVerboseOverlay("GroundOverlay: waterAreaSegmentWidth=" + _config.WaterAreaSegmentWidth + ", waterAreaSegmentHeight=" + _config.WaterAreaSegmentHeight);
            LogVerboseOverlay(
                "GroundOverlay: renderWaterAreaOutlines=" + _config.RenderWaterAreaOutlines +
                ", renderWaterAreaFillMeshes=" + _config.RenderWaterAreaFillMeshes +
                ", maxWaterAreaFillMeshesDebug=" + _config.MaxWaterAreaFillMeshesDebug +
                ", waterAreaFillYOffset=" + _config.WaterAreaFillYOffset
            );
            Log.Info(
                "GroundOverlay: render mode: exhaustive=" + IsRenderEverythingEnabled() +
                ", renderAllRoadSegments=" + ShouldRenderAllRoadSegments() +
                ", renderAllPathSegments=" + ShouldRenderAllPathSegments() +
                ", renderAllZoningPolygons=" + ShouldRenderAllZoningPolygons() +
                ", renderAllWaterSegments=" + ShouldRenderAllWaterSegments() +
                ", renderAllWaterAreaFills=" + ShouldRenderAllWaterAreaFills() +
                ", roadRenderMode=" + _config.RoadRenderMode +
                ", pathRenderMode=" + _config.PathRenderMode +
                ", semanticFilter=" + _config.RoadSemanticFilterMode
            );
            LogVerboseOverlay("GroundOverlay: maxRenderedSegments=" + _config.MaxRenderedSegments + ", maxWaterSegmentsDebug=" + _config.MaxWaterSegmentsDebug + ", pointStride=" + _config.PointStride);
            LogVerboseOverlay(
                "GroundOverlay: roadSegmentWidth=" + _config.RoadSegmentWidth +
                ", roadSegmentHeight=" + _config.RoadSegmentHeight +
                ", pathSegmentWidth=" + _config.PathSegmentWidth +
                ", pathSegmentHeight=" + _config.PathSegmentHeight +
                ", pathYOffset=" + _config.PathYOffset +
                ", pathAlpha=" + _config.PathAlpha +
                ", maxRoadSegmentsDebug=" + _config.MaxRoadSegmentsDebug +
                ", maxPathSegmentsDebug=" + _config.MaxPathSegmentsDebug
            );
            LogVerboseOverlay("GroundOverlay: road hierarchy style=highway");
            LogVerboseOverlay("GroundOverlay: minimumRoadDebugTier=" + _config.MinimumRoadDebugTier);
            LogVerboseOverlay(
                "GroundOverlay: layerYOffsets road=" + _config.RoadYOffset +
                ", waterLine=" + _config.WaterLineYOffset +
                ", waterAreaOutline=" + _config.WaterAreaOutlineYOffset +
                ", zoningFill=" + _config.ZoningFillYOffset +
                ", waterAreaFill=" + _config.WaterAreaFillYOffset
            );
            LogVerboseOverlay(
                "GroundOverlay: layerAlphas zoning=" + _config.ZoningAlpha +
                ", waterAreaFill=" + _config.WaterAreaFillAlpha +
                ", road=" + _config.RoadAlpha +
                ", waterLine=" + _config.WaterLineAlpha +
                ", waterAreaOutline=" + _config.WaterAreaOutlineAlpha
            );
            LogVerboseOverlay(
                "GroundOverlay: zoning polygons available=" +
                (_zoningPolygons != null ? _zoningPolygons.Count : 0) +
                ", renderZoning=" + _config.RenderZoning
            );
            LogVerboseOverlay(
                "GroundOverlay: zoningDebugFilterZone=" +
                (string.IsNullOrWhiteSpace(_config.ZoningDebugFilterZone) ? "(empty)" : _config.ZoningDebugFilterZone)
            );
            LogVerboseOverlay(
                "GroundOverlay: zoningDebugFilterCs2Contains=" +
                (string.IsNullOrWhiteSpace(_config.ZoningDebugFilterCs2Contains) ? "(empty)" : _config.ZoningDebugFilterCs2Contains)
            );

            LogVerboseOverlay(
                "GroundOverlay: zoningDebugFilterMaterialKey=" +
                (string.IsNullOrWhiteSpace(_config.ZoningDebugFilterMaterialKey) ? "(empty)" : _config.ZoningDebugFilterMaterialKey)
            );
            LogVerboseOverlay("GroundOverlay: zoning material style=cs2-first");
            LogZoningMaterialLegend();

            var materials = CreateOverlayRenderMaterials();
            var cyan = materials.Cyan;
            var waterAreaBlue = materials.WaterAreaBlue;
            var waterAreaFillBlue = materials.WaterAreaFillBlue;
            var orange = materials.FallbackRoad;
            var roadMotorwayMaterial = materials.RoadMotorway;
            var roadPrimaryMaterial = materials.RoadPrimary;
            var roadSecondaryMaterial = materials.RoadSecondary;
            var roadTertiaryMaterial = materials.RoadTertiary;
            var roadTertiaryTexturedMaterial = materials.RoadTertiaryTextured;
            var roadLinkMaterial = materials.RoadLink;
            var pathMaterial = materials.Path;
            var roadOneWayMaterial = materials.RoadOneWay;
            var roadBridgeMaterial = materials.RoadBridge;
            var roadTunnelMaterial = materials.RoadTunnel;
            var roadRoundaboutMaterial = materials.RoadRoundabout;
            var roadArrowMaterial = materials.RoadArrow;
            var roadLabelMaterial = materials.RoadLabel;
            var zoningResidentialLowMaterial = materials.ZoningResidentialLow;
            var zoningResidentialMediumMaterial = materials.ZoningResidentialMedium;
            var zoningResidentialHighMaterial = materials.ZoningResidentialHigh;
            var zoningCommercialLowMaterial = materials.ZoningCommercialLow;
            var zoningCommercialHighMaterial = materials.ZoningCommercialHigh;
            var zoningRetailDetailMaterial = materials.ZoningRetailDetail;
            var zoningIndustrialMaterial = materials.ZoningIndustrial;
            var zoningOfficeMaterial = materials.ZoningOffice;
            var zoningSurfaceMaterial = materials.ZoningSurface;
            var zoningRampMaterial = materials.ZoningRamp;
            var zoningMixedMaterial = materials.ZoningMixed;
            var zoningFallbackMaterial = materials.ZoningFallback;

            ApplyCurrentOverlayVisibilityToMaterials();

            var red = materials.DebugRed;
            var green = materials.DebugGreen;
            var yellow = materials.DebugYellow;
            var magenta = materials.DebugMagenta;

            var worldMapBoundsMaterial = materials.WorldMapBounds;
            var heightMapBoundsMaterial = materials.HeightMapBounds;
            var mapCenterMaterial = materials.MapCenter;


            var stride = Math.Max(1, _config.PointStride);
            MapBoundsRenderer.Render(
                _config,
                OverlayRenderParent,
                worldMapBoundsMaterial,
                heightMapBoundsMaterial,
                mapCenterMaterial,
                originLon,
                originLat,
                ResolveY,
                LogVerboseOverlay
            );

            var createdWaterSegments = 0;
            var createdWaterLines = 0;

            Vector3 firstWorld = Vector3.zero;
            Vector3 lastWorld = Vector3.zero;
            var hasEndpoints = false;

            // TODO(progressive rebuild): split zoning into its own coroutine phase.
            var createdZoningFillMeshes = RenderZoningFillMeshes(
                zoningResidentialLowMaterial,
                zoningResidentialMediumMaterial,
                zoningResidentialHighMaterial,
                zoningCommercialLowMaterial,
                zoningCommercialHighMaterial,
                zoningRetailDetailMaterial,
                zoningIndustrialMaterial,
                zoningOfficeMaterial,
                zoningSurfaceMaterial,
                zoningRampMaterial,
                zoningMixedMaterial,
                zoningFallbackMaterial,
                stride
            );

            if (_config.RenderZoning)
                LogVerboseOverlay("GroundOverlay: created zoning fill meshes=" + createdZoningFillMeshes);

            // TODO(progressive rebuild): split water lines/areas into their own coroutine phase.
            RenderWaterOverlayGroups(
                cyan,
                waterAreaBlue,
                waterAreaFillBlue,
                stride,
                ref createdWaterSegments,
                ref createdWaterLines,
                ref firstWorld,
                ref lastWorld,
                ref hasEndpoints
            );

            var serviceCounters = RenderServiceGroup(materials, originLon, originLat);
            var railwayCounters = RenderRailwayGroup(materials, stride, originLon, originLat);

            var createdRoadLines = 0;
            var createdPathLines = 0;
            var createdPathSegments = 0;
            // TODO(progressive rebuild): split road/path mesh batching into resumable chunks.
            var createdRoadAndPathSegments = RenderRoadGroup(
                roadMotorwayMaterial,
                roadPrimaryMaterial,
                roadSecondaryMaterial,
                roadTertiaryMaterial,
                roadTertiaryTexturedMaterial,
                roadLinkMaterial,
                pathMaterial,
                orange,
                roadOneWayMaterial,
                roadBridgeMaterial,
                roadTunnelMaterial,
                roadRoundaboutMaterial,
                stride,
                ref createdRoadLines,
                ref createdPathLines,
                ref createdPathSegments
            );

            // TODO(progressive rebuild): keep arrows and labels as separate optional phases.
            var createdRoadArrows = RenderRoadDirectionArrows(roadArrowMaterial, stride);
            var createdRoadLabels = RenderRoadLabels(roadLabelMaterial, stride);

            var center = new Vector3(
                _config.WorldOriginX,
                ResolveY(new Vector3(_config.WorldOriginX, 0f, _config.WorldOriginZ)) + _config.GroundMargin,
                _config.WorldOriginZ
            );

            if (_config.DebugBeacons)
            {
                CreateBeacon("ground_red_center_beacon", center, red);

                if (hasEndpoints)
                {
                    CreateBeacon("ground_green_first_beacon", firstWorld, green);
                    CreateBeacon("ground_yellow_last_beacon", lastWorld, yellow);
                }
            }

            if (_config.DebugTower)
                CreateTower("ground_magenta_debug_tower", center, magenta);

            LogVerboseOverlay("GroundOverlay: first world point=" + firstWorld);
            LogVerboseOverlay("GroundOverlay: last world point=" + lastWorld);
            LogVerboseOverlay("GroundOverlay: center world point=" + center);

            LogVerboseOverlay(
                "GroundOverlay: created water lines=" + createdWaterLines +
                ", water batched segments=" + createdWaterSegments +
                ", service points=" + serviceCounters.CreatedPoints +
                ", service mesh objects=" + serviceCounters.CreatedMeshObjects +
                ", railway lines=" + railwayCounters.CreatedLines +
                ", railway batched segments=" + railwayCounters.CreatedSegments +
                ", road lines=" + createdRoadLines +
                ", path lines=" + createdPathLines +
                ", road/path batched segments=" + createdRoadAndPathSegments +
                ", path batched segments=" + createdPathSegments +
                ", one-way arrows=" + createdRoadArrows +
                ", road labels=" + createdRoadLabels
            );

LogRoadHighwaySummary();
LogRoadSemanticSummary();
LogRenderedAndSkippedRoadHighwaySummary();
LogRoadTierFilterSummary(createdRoadLines + createdPathLines);
        }




















































































    }
}
