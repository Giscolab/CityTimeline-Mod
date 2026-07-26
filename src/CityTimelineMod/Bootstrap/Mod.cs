using System;
using System.Collections.Generic;
using System.Reflection;
using Colossal.UI.Binding;
using Colossal.Logging;
using Game;
using Game.Input;
using Game.Modding;
using Game.SceneFlow;
using Game.Simulation;
using Unity.Entities;
using UnityEngine;
using CityTimelineMod.Config;
using CityTimelineMod.Diagnostics;
using CityTimelineMod.LargeMap;
using CityTimelineMod.Options;
using CityTimelineMod.PlayableWorld;
using CityTimelineMod.Rendering;

namespace CityTimelineMod
{
    public sealed class Mod : IMod
    {
        private CityTimelineSettings _settings;
        private CityTimelineSettingsLocale _frLocale;
        private CityTimelineSettingsLocale _enLocale;
        private bool _frLocaleRegistered;
        private bool _enLocaleRegistered;
        private World _world;
        private CityTimelineMod.UI.CityTimelineUISystem _uiSystem;
        private PlayableWorldRoadProfileSystem _playableWorldRoadProfileSystem;

        internal static CityTimelineSettings Settings { get; private set; }
        internal static GeoOverlayConfig RuntimeConfig { get; private set; }
        internal static bool RuntimeEnabled { get; private set; }

        public static ILog Log = LogManager.GetLogger(nameof(CityTimelineMod)).SetShowsErrorsInUI(true);

        public void OnLoad(UpdateSystem updateSystem)
        {
            // Hot reload preflight.  No configuration decision may return before
            // stale callbacks, systems, Unity roots and owner-scoped Harmony
            // patches from the previous instance have been neutralised.
            RuntimeEnabled = false;
            RuntimeConfig = null;
            CityTimelineMod.UI.CityTimelineUISystem.CloseRuntimeGate();

            if (_uiSystem != null)
                CityTimelineMod.UI.CityTimelineUISystem.CloseRuntimeGate(_uiSystem);
            DisableSystem(_uiSystem, "previous CoHTML instance");
            DisableSystem(_playableWorldRoadProfileSystem, "previous PlayableWorld instance");
            _uiSystem = null;
            _playableWorldRoadProfileSystem = null;

            var previousWorld = _world;
            _world = updateSystem != null ? updateSystem.World : null;

            if (previousWorld != null && !ReferenceEquals(previousWorld, _world))
                NeutralizeExistingSystems(previousWorld);

            NeutralizeExistingSystems(_world);
            SafeUninstallOverlay("startup preflight");
            CityTimelineRuntimeController.Uninstall();
            GeoBundleBootstrap.Reset();
            CityTimelineLargeMapState.Disable();
            PlayableWorldState.Disable();

            var playableWorldOwnerClean = SafeUninstallPlayableWorld("startup preflight");
            var largeMapOwnerClean = SafeUninstallLargeMap("startup preflight");

            // If OnLoad is repeated on the same Mod object, avoid duplicating
            // official options/localisation sources owned by this instance.
            SafeUnregisterOptions();
            RemoveLocalizationSources();

            GeoOverlayConfig runtimeConfig = null;
            try
            {
                runtimeConfig = GeoBundleBootstrap.LoadRuntimeConfigSnapshot();
            }
            catch (Exception ex)
            {
                Debug.LogError("[CityTimelineMod] Runtime configuration load failed: " + ex);
                Log.Error(ex);
            }

            RuntimeConfig = runtimeConfig;

            _settings = new CityTimelineSettings(this);
            Settings = _settings;
            _settings.SetDefaults();
            _settings.ApplyRuntimeSnapshot(runtimeConfig);

            RegisterOfficialOptions();
            RegisterLocalizationSources();

            var configReliable = runtimeConfig != null && runtimeConfig.IsReliable;
            var modEnabled = configReliable && runtimeConfig.ModEnabled;
            var harmonyOwnersClean = playableWorldOwnerClean && largeMapOwnerClean;
            var runtimeAuthorized = configReliable && modEnabled && harmonyOwnersClean;

            Log.Info(
                "[CityTimelineMod] startup decision: configReliable=" + configReliable +
                ", modEnabled=" + (runtimeConfig != null && runtimeConfig.ModEnabled) +
                ", largeMapEnabled=" + (runtimeConfig != null && runtimeConfig.LargeMapEnabled) +
                ", playableWorldEnabled=" + (runtimeConfig != null && runtimeConfig.PlayableWorldEnabled) +
                ", harmonyOwnersClean=" + harmonyOwnersClean +
                ", runtimeAuthorized=" + runtimeAuthorized
            );

            if (!runtimeAuthorized)
            {
                if (!configReliable)
                    Log.Error("[CityTimelineMod] runtime startup blocked: config.json is unreadable or invalid.");
                else if (!modEnabled)
                    Log.Info("[CityTimelineMod] runtime startup skipped: modEnabled=false.");
                else
                    Log.Error("[CityTimelineMod] runtime startup blocked: stale Harmony owner cleanup was not verified.");

                return;
            }

            try
            {
                _settings.RegisterKeyBindings();
            }
            catch (Exception ex)
            {
                Debug.LogError("[CityTimelineMod] Keybinding registration error: " + ex);
                Log.Error(ex);
            }

            RuntimeEnabled = true;

            // The CoHTML system is independent from both experimental modules.
            try
            {
                _uiSystem = GetOrRegisterSystem<CityTimelineMod.UI.CityTimelineUISystem>(
                    updateSystem,
                    SystemUpdatePhase.UIUpdate
                );

                if (!CityTimelineMod.UI.CityTimelineUISystem.OpenRuntimeGate(_uiSystem))
                    throw new InvalidOperationException("The CoHTML runtime gate could not be opened.");

                Log.Info("[CityTimelineMod] CoHTML UI system active.");
            }
            catch (Exception ex)
            {
                Debug.LogError("[CityTimelineMod] CoHTML UI registration error: " + ex);
                Log.Error(ex);
                AbortRuntimeStartup("CoHTML UI registration failed");
                return;
            }

            // This diagnostic enumeration existed at the checkpoint and is
            // unrelated to the lifecycle fix, so it remains intact.
            foreach (SystemUpdatePhase phase in Enum.GetValues(typeof(SystemUpdatePhase)))
                Log.Info("[CityTimelineMod] SystemUpdatePhase " + phase + " = " + (int)phase);

            try
            {
                if (!GeoBundleBootstrap.RunOnce(runtimeConfig))
                {
                    Log.Error("[CityTimelineMod] Overlay bootstrap did not install an operational overlay.");
                    AbortRuntimeStartup("overlay bootstrap failed");
                    return;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[CityTimelineMod] Bootstrap error: " + ex);
                Log.Error(ex);
                AbortRuntimeStartup("overlay bootstrap threw");
                return;
            }

            if (runtimeConfig.LargeMapEnabled)
            {
                CityTimelineLargeMapState.Disable();
                Log.Error(
                    "[CityTimelineMod] LargeMap was requested, but the experimental module is " +
                    "temporarily blocked fail-closed in Lot 1; no LargeMap patch was installed."
                );
            }
            else
            {
                CityTimelineLargeMapState.Disable();
                Log.Info("[CityTimelineMod] LargeMap disabled by configuration.");
            }

            if (!runtimeConfig.PlayableWorldEnabled)
            {
                PlayableWorldState.Disable();
                Log.Info("[CityTimelineMod] PlayableWorld disabled by configuration.");
                return;
            }

            if (!PlayableWorldPatcher.Install(true, _world.SequenceNumber))
            {
                Log.Error("[CityTimelineMod] PlayableWorld requested but atomic installation failed.");
                return;
            }

            TryRecapturePlayableWorldBounds(_world);

            try
            {
                _playableWorldRoadProfileSystem =
                    GetOrRegisterSystem<PlayableWorldRoadProfileSystem>(
                        updateSystem,
                        SystemUpdatePhase.Modification1
                    );
                _playableWorldRoadProfileSystem.Enabled = true;

                Log.Info(
                    "[CityTimelineMod] PlayableWorld patches installed; road-profile system registered; " +
                    "boundsInitialized=" + PlayableWorldState.Initialized +
                    ", mutationReady=" + PlayableWorldState.Enabled + "."
                );
            }
            catch (Exception ex)
            {
                Debug.LogError("[CityTimelineMod] PlayableWorld system registration error: " + ex);
                Log.Error(ex);
                DisableSystem(_playableWorldRoadProfileSystem, "PlayableWorld road-profile rollback");

                try
                {
                    if (_world != null && _world.IsCreated)
                    {
                        DisableSystem(
                            _world.GetExistingSystemManaged<PlayableWorldRoadProfileSystem>(),
                            "PlayableWorld partial UpdateAt rollback"
                        );
                    }
                }
                catch (Exception cleanupEx)
                {
                    Log.Error("[CityTimelineMod] PlayableWorld partial ECS rollback failed: " + cleanupEx);
                }

                _playableWorldRoadProfileSystem = null;
                SafeUninstallPlayableWorld("ECS registration rollback");
            }
        }

        public void OnDispose()
        {
            // 1. Close the runtime gate and stop the exact systems owned/reused
            // by this World before any resource or Harmony teardown.
            RuntimeEnabled = false;
            if (_uiSystem != null)
                CityTimelineMod.UI.CityTimelineUISystem.CloseRuntimeGate(_uiSystem);

            DisableSystem(_uiSystem, "CoHTML dispose");
            DisableSystem(_playableWorldRoadProfileSystem, "PlayableWorld dispose");
            NeutralizeExistingSystems(_world);

            // 2. Flush pending overlay state, then destroy all current and stale
            // CTM roots/resources.  The legacy controller is cleanup-only.
            SafeUninstallOverlay("dispose");
            CityTimelineRuntimeController.Uninstall();

            // 3. Make the next load/reload retry the bootstrap.
            GeoBundleBootstrap.Reset();

            // 4. State gates close before owner-scoped Harmony teardown.
            CityTimelineLargeMapState.Disable();
            PlayableWorldState.Disable();

            // 5-6. Owner-scoped patch removal remains independent/best effort.
            SafeUninstallPlayableWorld("dispose");
            SafeUninstallLargeMap("dispose");

            // 7. Official options and exact localisation sources are removed.
            SafeUnregisterOptions();
            RemoveLocalizationSources();

            // 8. Reset every instance/static reference.
            _settings = null;
            _world = null;
            _uiSystem = null;
            _playableWorldRoadProfileSystem = null;
            Settings = null;
            RuntimeConfig = null;

            Log.Info("[CityTimelineMod] Disposed.");
        }

        private static T GetOrRegisterSystem<T>(
            UpdateSystem updateSystem,
            SystemUpdatePhase phase)
            where T : ComponentSystemBase
        {
            if (updateSystem == null || updateSystem.World == null || !updateSystem.World.IsCreated)
                throw new InvalidOperationException("Cannot register an ECS system without a created World.");

            var world = updateSystem.World;
            var system = world.GetExistingSystemManaged<T>();

            if (system == null)
            {
                updateSystem.UpdateAt<T>(phase);
                system = world.GetExistingSystemManaged<T>();

                if (system == null)
                    throw new InvalidOperationException("UpdateAt did not create " + typeof(T).FullName + ".");

                Log.Info(
                    "[CityTimelineMod] ECS system registered once for World sequence=" +
                    world.SequenceNumber + ": " + typeof(T).FullName
                );
            }
            else
            {
                Log.Info(
                    "[CityTimelineMod] ECS system reused for World sequence=" +
                    world.SequenceNumber + ": " + typeof(T).FullName
                );
            }

            return system;
        }

        private static void NeutralizeExistingSystems(World world)
        {
            if (world == null || !world.IsCreated)
                return;

            try
            {
                var modernUi = world.GetExistingSystemManaged<CityTimelineMod.UI.CityTimelineUISystem>();
                if (modernUi != null)
                    CityTimelineMod.UI.CityTimelineUISystem.CloseRuntimeGate(modernUi);
                DisableSystem(modernUi, "existing modern UI");
            }
            catch (Exception ex)
            {
                Log.Error("[CityTimelineMod] Existing modern UI cleanup failed: " + ex);
            }

            try
            {
                var legacyUi = world.GetExistingSystemManaged<CityTimelineMod.Systems.CityTimelineUISystem>();
                if (legacyUi != null)
                    legacyUi.DeactivateForMigration();
            }
            catch (Exception ex)
            {
                Log.Error("[CityTimelineMod] Legacy UI cleanup failed: " + ex);
            }

            DisableExistingSystem<PlayableWorldRoadProfileSystem>(world);
            DisableExistingSystem<PlayableWorldErrorPrefabSystem>(world);
            DisableExistingSystem<RailOutsideConnectionRepairSystem>(world);
            DisableExistingSystem<LargeMapTerrainDiagnosticSystem>(world);

            // A hot reload may leave systems whose CLR type belongs to the
            // previous CityTimelineMod assembly identity. Generic lookup cannot
            // see them, so scan the exact CTM full names in every loaded assembly.
            DisableExistingSystemByName(world, "CityTimelineMod.UI.CityTimelineUISystem", true);
            DisableExistingSystemByName(world, "CityTimelineMod.Systems.CityTimelineUISystem", true);
            DisableExistingSystemByName(world, "CityTimelineMod.PlayableWorld.PlayableWorldRoadProfileSystem");
            DisableExistingSystemByName(world, "CityTimelineMod.PlayableWorld.PlayableWorldErrorPrefabSystem");
            DisableExistingSystemByName(world, "CityTimelineMod.LargeMap.RailOutsideConnectionRepairSystem");
            DisableExistingSystemByName(world, "CityTimelineMod.Diagnostics.LargeMapTerrainDiagnosticSystem");

            // These development/legacy systems are excluded from some build
            // profiles.  Resolve them only when their type is actually present.
            DisableExistingSystemByName(world, "CityTimelineMod.LargeMap.AirOutsideConnectionRepairSystem");
            DisableExistingSystemByName(world, "CityTimelineMod.Systems.RuntimeRoadToolDriverSystem");
            DisableExistingSystemByName(world, "CityTimelineMod.Roads.RuntimeRoadToolSystem");
            DisableExistingSystemByName(world, "CityTimelineMod.Roads.VanillaNetCourseProbeSystem");
        }

        private static void DisableExistingSystem<T>(World world)
            where T : ComponentSystemBase
        {
            try
            {
                DisableSystem(world.GetExistingSystemManaged<T>(), typeof(T).FullName);
            }
            catch (Exception ex)
            {
                Log.Error("[CityTimelineMod] Existing ECS cleanup failed for " + typeof(T).FullName + ": " + ex);
            }
        }

        private static void DisableExistingSystemByName(
            World world,
            string fullName,
            bool detachUiCallbacks = false)
        {
            var foundType = false;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var type = assembly.GetType(fullName, false);
                    if (type == null || !typeof(ComponentSystemBase).IsAssignableFrom(type))
                        continue;

                    foundType = true;
                    var system = world.GetExistingSystemManaged(type);

                    if (system != null &&
                        detachUiCallbacks &&
                        type.Assembly != typeof(Mod).Assembly)
                    {
                        DetachPriorAssemblyUi(system, type);
                    }

                    DisableSystem(system, fullName);
                }
                catch (Exception ex)
                {
                    Log.Error(
                        "[CityTimelineMod] Existing optional ECS cleanup failed for " +
                        fullName + " in assembly " + assembly.FullName + ": " + ex
                    );
                }
            }

            if (foundType)
                Log.Info("[CityTimelineMod] Optional ECS type inspected for cleanup: " + fullName + ".");
        }

        private static void DetachPriorAssemblyUi(ComponentSystemBase system, Type systemType)
        {
            try
            {
                var closeGate = systemType.GetMethod(
                    "CloseRuntimeGateInternal",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(bool), typeof(bool) },
                    null
                );
                // Publish the closed state before removing the old bindings. If
                // binding removal is unavailable or fails, the global launcher
                // must still observe runtimeAvailable=false during hot reload.
                closeGate?.Invoke(system, new object[] { false, true });
            }
            catch (Exception ex)
            {
                Log.Error("[CityTimelineMod] Prior UI instance gate cleanup failed: " + ex);
            }

            try
            {
                var deactivate = systemType.GetMethod(
                    "DeactivateForMigration",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    Type.EmptyTypes,
                    null
                );
                deactivate?.Invoke(system, null);
            }
            catch (Exception ex)
            {
                Log.Error("[CityTimelineMod] Prior UI migration hook failed: " + ex);
            }

            try
            {
                var gateField = FindInstanceField(systemType, "_runtimeGateOpen");
                if (gateField != null && gateField.FieldType == typeof(bool))
                    gateField.SetValue(system, false);
            }
            catch (Exception ex)
            {
                Log.Error("[CityTimelineMod] Prior UI gate reset failed: " + ex);
            }

            try
            {
                var actionField = FindInstanceField(systemType, "_toggleAction");
                var action = actionField != null
                    ? actionField.GetValue(system) as ProxyAction
                    : null;
                var callback = systemType.GetMethod(
                    "OnToggleActionInteraction",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                );
                var interactionEvent = typeof(ProxyAction).GetEvent(
                    "onInteraction",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                );

                if (action != null && callback != null)
                {
                    if (interactionEvent != null)
                    {
                        var handler = Delegate.CreateDelegate(
                            interactionEvent.EventHandlerType,
                            system,
                            callback,
                            false
                        );
                        if (handler != null)
                            interactionEvent.RemoveEventHandler(action, handler);
                    }
                    else
                    {
                        var interactionField = typeof(ProxyAction).GetField(
                            "onInteraction",
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                        );
                        var currentHandlers = interactionField != null
                            ? interactionField.GetValue(action) as Delegate
                            : null;
                        var handler = interactionField != null
                            ? Delegate.CreateDelegate(
                                interactionField.FieldType,
                                system,
                                callback,
                                false
                            )
                            : null;
                        if (interactionField != null && handler != null)
                        {
                            interactionField.SetValue(
                                action,
                                Delegate.Remove(currentHandlers, handler)
                            );
                        }
                    }
                }

                var subscribedField = FindInstanceField(systemType, "_toggleActionSubscribed");
                if (subscribedField != null && subscribedField.FieldType == typeof(bool))
                    subscribedField.SetValue(system, false);

                if (action != null &&
                    !CityTimelineMod.UI.CityTimelineUISystem.OwnsRuntimeAction(action))
                {
                    action.shouldBeEnabled = false;
                }
            }
            catch (Exception ex)
            {
                Log.Error("[CityTimelineMod] Prior UI input cleanup failed: " + ex);
            }

            var registry = GameManager.instance != null &&
                GameManager.instance.userInterface != null
                ? GameManager.instance.userInterface.bindings
                : null;

            if (registry == null)
            {
                Log.Error("[CityTimelineMod] Prior UI binding cleanup failed: registry unavailable.");
                return;
            }

            var bindings = new HashSet<IBinding>();
            try
            {
                for (var type = systemType; type != null; type = type.BaseType)
                {
                    var fields = type.GetFields(
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic |
                        BindingFlags.DeclaredOnly
                    );

                    foreach (var field in fields)
                    {
                        try
                        {
                            var value = field.GetValue(system);
                            var binding = value as IBinding;
                            if (binding != null)
                                bindings.Add(binding);

                            var bindingCollection = value as System.Collections.IEnumerable;
                            if (bindingCollection != null && !(value is string))
                            {
                                foreach (var item in bindingCollection)
                                {
                                    var itemBinding = item as IBinding;
                                    if (itemBinding != null)
                                        bindings.Add(itemBinding);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error("[CityTimelineMod] Prior UI binding field inspection failed: " + ex);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("[CityTimelineMod] Prior UI binding cleanup failed: " + ex);
            }

            foreach (var binding in bindings)
            {
                try
                {
                    registry.RemoveBinding(binding);
                }
                catch (Exception ex)
                {
                    Log.Error("[CityTimelineMod] Prior UI binding removal failed: " + ex);
                }
            }
        }

        private static FieldInfo FindInstanceField(Type type, string name)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                var field = current.GetField(
                    name,
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly
                );
                if (field != null)
                    return field;
            }

            return null;
        }

        private static void DisableSystem(ComponentSystemBase system, string reason)
        {
            if (system == null)
                return;

            try
            {
                system.Enabled = false;
                Log.Info("[CityTimelineMod] ECS system disabled: " + reason + ".");
            }
            catch (Exception ex)
            {
                Log.Error("[CityTimelineMod] ECS system disable failed for " + reason + ": " + ex);
            }
        }

        private static void TryRecapturePlayableWorldBounds(World world)
        {
            // A verified Harmony owner may survive as the patcher's fast path,
            // but bounds belong to the current World and must never be reused.
            PlayableWorldState.MarkUninitialized();

            try
            {
                if (world == null || !world.IsCreated)
                    return;

                var terrainSystem = world.GetExistingSystemManaged<TerrainSystem>();
                if (terrainSystem == null)
                {
                    Log.Info("[PlayableWorld] terrain system unavailable; installed=true, initialized=false.");
                    return;
                }

                var terrainData = terrainSystem.GetHeightData(false);
                if (!terrainData.isCreated ||
                    !PlayableWorldState.TryUpdate(
                        world.SequenceNumber,
                        terrainSystem.worldOffset,
                        terrainSystem.worldSize))
                {
                    Log.Info("[PlayableWorld] current terrain bounds unavailable; installed=true, initialized=false.");
                }
            }
            catch (Exception ex)
            {
                Log.Error("[PlayableWorld] current terrain bounds recapture failed; initialized=false. " + ex);
            }
        }

        private void AbortRuntimeStartup(string reason)
        {
            RuntimeEnabled = false;

            if (_uiSystem != null)
                CityTimelineMod.UI.CityTimelineUISystem.CloseRuntimeGate(_uiSystem);

            DisableSystem(_uiSystem, "startup rollback UI");
            DisableSystem(_playableWorldRoadProfileSystem, "startup rollback PlayableWorld");
            SafeUninstallOverlay("startup rollback");
            CityTimelineRuntimeController.Uninstall();
            GeoBundleBootstrap.Reset();
            PlayableWorldState.Disable();
            CityTimelineLargeMapState.Disable();
            SafeUninstallPlayableWorld("startup rollback");
            SafeUninstallLargeMap("startup rollback");
            Log.Error("[CityTimelineMod] runtime gate closed: " + reason + ".");
        }

        private void RegisterOfficialOptions()
        {
            try
            {
                _settings.RegisterInOptionsUI();
            }
            catch (Exception ex)
            {
                Debug.LogError("[CityTimelineMod] Options registration error: " + ex);
                Log.Error(ex);
            }
        }

        private void RegisterLocalizationSources()
        {
            if (!_frLocaleRegistered)
            {
                try
                {
                    _frLocale = new CityTimelineSettingsLocale(_settings);
                    GameManager.instance.localizationManager.AddSource("fr-FR", _frLocale);
                    _frLocaleRegistered = true;
                }
                catch (Exception ex)
                {
                    Log.Error("[CityTimelineMod] French localisation registration failed: " + ex);
                }
            }

            if (!_enLocaleRegistered)
            {
                try
                {
                    _enLocale = new CityTimelineSettingsLocale(_settings);
                    GameManager.instance.localizationManager.AddSource("en-US", _enLocale);
                    _enLocaleRegistered = true;
                }
                catch (Exception ex)
                {
                    Log.Error("[CityTimelineMod] English localisation registration failed: " + ex);
                }
            }
        }

        private void RemoveLocalizationSources()
        {
            if (_frLocaleRegistered && _frLocale != null)
            {
                try
                {
                    GameManager.instance.localizationManager.RemoveSource("fr-FR", _frLocale);
                    _frLocaleRegistered = false;
                    _frLocale = null;
                }
                catch (Exception ex)
                {
                    Log.Error("[CityTimelineMod] French localisation cleanup failed: " + ex);
                }
            }

            if (_enLocaleRegistered && _enLocale != null)
            {
                try
                {
                    GameManager.instance.localizationManager.RemoveSource("en-US", _enLocale);
                    _enLocaleRegistered = false;
                    _enLocale = null;
                }
                catch (Exception ex)
                {
                    Log.Error("[CityTimelineMod] English localisation cleanup failed: " + ex);
                }
            }

            if (!_frLocaleRegistered)
                _frLocale = null;
            if (!_enLocaleRegistered)
                _enLocale = null;
        }

        private void SafeUnregisterOptions()
        {
            try
            {
                _settings?.UnregisterInOptionsUI();
            }
            catch (Exception ex)
            {
                Log.Error("[CityTimelineMod] Options cleanup failed: " + ex);
            }
        }

        private static bool SafeUninstallPlayableWorld(string stage)
        {
            try
            {
                return PlayableWorldPatcher.Uninstall();
            }
            catch (Exception ex)
            {
                Log.Error("[CityTimelineMod] PlayableWorld " + stage + " cleanup failed: " + ex);
                PlayableWorldState.Disable();
                return false;
            }
        }

        private static bool SafeUninstallLargeMap(string stage)
        {
            try
            {
                return CityTimelineLargeMapPatcher.Uninstall();
            }
            catch (Exception ex)
            {
                Log.Error("[CityTimelineMod] LargeMap " + stage + " cleanup failed: " + ex);
                CityTimelineLargeMapState.Disable();
                return false;
            }
        }

        private static void SafeUninstallOverlay(string stage)
        {
            try
            {
                if (!GeoDebugOverlay.Uninstall())
                    Log.Error("[CityTimelineMod] Overlay " + stage + " cleanup remains pending and will be retried.");
            }
            catch (Exception ex)
            {
                Debug.LogError("[CityTimelineMod] Overlay " + stage + " cleanup failed: " + ex);
                Log.Error(ex);
            }
        }
    }
}
