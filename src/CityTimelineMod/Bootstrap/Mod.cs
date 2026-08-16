using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Colossal.UI.Binding;
using Colossal.Logging;
using Game;
using Game.Input;
using Game.Modding;
using Game.SceneFlow;
using Game.Serialization;
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
        private PlayableWorldErrorPrefabSystem _playableWorldErrorPrefabSystem;
        private PlayableWorldRoadProfileSystem _playableWorldRoadProfileSystem;
        private RailOutsideConnectionRepairSystem _railOutsideConnectionRepairSystem;
        private PreSerialize<RailOutsideConnectionRepairSystem> _railPreSerializeSystem;
        private Mod _supersededLifecycleOwnerPendingCleanup;
        private long _lifecycleGeneration;

        private static readonly object LifecycleSync = new object();
        private static Mod _activeLifecycleOwner;
        private static long _activeLifecycleGeneration;
        private static long _nextLifecycleGeneration;
        private static ulong _activeRuntimeWorldSequence;
        private static bool _largeMapRuntimeFailurePending;
        private static long _largeMapRuntimeFailureGeneration;
        private static string _largeMapRuntimeFailureReason;

        private sealed class LargeMapPatcherLifecycleApi
        {
            internal Type PatcherType;
            internal MethodInfo Probe;
            internal MethodInfo Rollback;
            internal MethodInfo RemoveSharedOwner;
            internal MethodInfo Commit;
            internal MethodInfo EnterQuarantine;
        }

        internal static CityTimelineSettings Settings { get; private set; }
        internal static GeoOverlayConfig RuntimeConfig { get; private set; }
        internal static bool RuntimeEnabled { get; private set; }

        internal static readonly ILog Log =
            LogManager.GetLogger(nameof(CityTimelineMod)).SetShowsErrorsInUI(true);

        public void OnLoad(UpdateSystem updateSystem)
        {
            Mod priorLifecycleOwner;
            World priorLifecycleWorld;
            long priorLifecycleGeneration;
            CapturePriorLifecycleScope(
                out priorLifecycleOwner,
                out priorLifecycleWorld,
                out priorLifecycleGeneration
            );

            ClaimLifecycleOwnership();

            if (priorLifecycleOwner != null &&
                !ReferenceEquals(priorLifecycleOwner, this))
            {
                _supersededLifecycleOwnerPendingCleanup = priorLifecycleOwner;
            }

            var supersededLifecycleRegistrationsClean =
                TryCleanupSupersededLifecycleRegistrations();
            var priorLifecycleOwnersDisarmed =
                RelinquishPriorAssemblyLifecycleOwners("startup preflight");

            // Hot reload preflight.  No configuration decision may return before
            // stale callbacks, systems, Unity roots and owner-scoped Harmony
            // patches from the previous instance have been neutralised.
            CloseRuntimeAuthorization();
            RuntimeConfig = null;
            var globalUiGateClean =
                CityTimelineMod.UI.CityTimelineUISystem.CloseRuntimeGate();

            var ownedUiGateClean = _uiSystem == null ||
                CityTimelineMod.UI.CityTimelineUISystem.CloseRuntimeGate(_uiSystem);
            var priorUiSystemDisabled =
                DisableSystem(_uiSystem, "previous CoHTML instance");
            var uiCleanupComplete =
                globalUiGateClean &&
                ownedUiGateClean &&
                priorUiSystemDisabled;
            var ownedRuntimeSystemsClean =
                DeactivateOwnedRuntimeSystems("startup preflight");

            if (uiCleanupComplete)
                _uiSystem = null;

            var previousWorld = _world;
            _world = updateSystem != null ? updateSystem.World : null;

            var ecsCleanupComplete = NeutralizeAllExistingWorlds(
                previousWorld,
                _world
            ) && ownedRuntimeSystemsClean;
            var overlayCleanupComplete =
                SafeUninstallOverlay("startup preflight");
            var runtimeControllerCleanupComplete =
                CityTimelineRuntimeController.Uninstall();
            GeoBundleBootstrap.Reset(true);
            CityTimelineLargeMapState.Disable();
            PlayableWorldState.Disable();

            var playableWorldOwnerClean = SafeUninstallPlayableWorld("startup preflight");
            var largeMapOwnerClean =
                TryCoordinatedLargeMapPatcherTeardown("startup preflight");
            var priorLargeMapScopeEnded = false;
            var finalLargeMapScopeReleased = false;

            if (!largeMapOwnerClean)
            {
                Log.Error(
                    "[CityTimelineMod] LargeMap startup preflight quarantined the " +
                    "shared Harmony owner because coordinated rollback across " +
                    "all loaded patcher identities remains incomplete."
                );
            }
            else
            {
                string priorLargeMapScopeEndReason;
                priorLargeMapScopeEnded =
                    priorLifecycleGeneration <= 0 ||
                    SafeEndLargeMapRuntimeWorld(
                        priorLifecycleWorld,
                        priorLifecycleGeneration,
                        "startup preflight",
                        out priorLargeMapScopeEndReason
                    );

                if (priorLargeMapScopeEnded)
                {
                    string finalLargeMapScopeReason;
                    finalLargeMapScopeReleased =
                        SafeReleaseDestroyedLargeMapRuntimeWorld(
                            "startup preflight after teardown",
                            out finalLargeMapScopeReason
                        );
                }
            }

            largeMapOwnerClean =
                largeMapOwnerClean &&
                priorLargeMapScopeEnded &&
                finalLargeMapScopeReleased;

            // If OnLoad is repeated on the same Mod object, avoid duplicating
            // official options/localisation sources owned by this instance.
            var optionsCleanupComplete = SafeUnregisterOptions();
            var localizationCleanupComplete = RemoveLocalizationSources();
            var lifecycleRegistrationsClean =
                supersededLifecycleRegistrationsClean &&
                priorLifecycleOwnersDisarmed &&
                optionsCleanupComplete &&
                localizationCleanupComplete;

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

            if (lifecycleRegistrationsClean)
            {
                _settings = new CityTimelineSettings(this);
                Settings = _settings;
                _settings.SetDefaults();
                _settings.ApplyRuntimeSnapshot(runtimeConfig);

                RegisterOfficialOptions();
                RegisterLocalizationSources();
            }
            else
            {
                Log.Error(
                    "[CityTimelineMod] New options/localisation registration was " +
                    "blocked because prior instance cleanup remains incomplete."
                );
            }

            var configReliable = runtimeConfig != null && runtimeConfig.IsReliable;
            var modEnabled = configReliable && runtimeConfig.ModEnabled;
            var harmonyOwnersClean = playableWorldOwnerClean && largeMapOwnerClean;
            var activeWorldReady = _world != null && _world.IsCreated;
            var runtimeAuthorized =
                configReliable &&
                modEnabled &&
                priorLifecycleOwnersDisarmed &&
                lifecycleRegistrationsClean &&
                harmonyOwnersClean &&
                ecsCleanupComplete &&
                uiCleanupComplete &&
                overlayCleanupComplete &&
                runtimeControllerCleanupComplete &&
                activeWorldReady;

            Log.Info(
                "[CityTimelineMod] startup decision: configReliable=" + configReliable +
                ", modEnabled=" + (runtimeConfig != null && runtimeConfig.ModEnabled) +
                ", largeMapEnabled=" + (runtimeConfig != null && runtimeConfig.LargeMapEnabled) +
                ", playableWorldEnabled=" + (runtimeConfig != null && runtimeConfig.PlayableWorldEnabled) +
                ", priorLifecycleOwnersDisarmed=" + priorLifecycleOwnersDisarmed +
                ", lifecycleRegistrationsClean=" + lifecycleRegistrationsClean +
                ", harmonyOwnersClean=" + harmonyOwnersClean +
                ", ecsCleanupComplete=" + ecsCleanupComplete +
                ", uiCleanupComplete=" + uiCleanupComplete +
                ", overlayCleanupComplete=" + overlayCleanupComplete +
                ", runtimeControllerCleanupComplete=" + runtimeControllerCleanupComplete +
                ", activeWorldReady=" + activeWorldReady +
                ", runtimeAuthorized=" + runtimeAuthorized
            );

            if (!runtimeAuthorized)
            {
                if (!configReliable)
                    Log.Error("[CityTimelineMod] runtime startup blocked: config.json is unreadable or invalid.");
                else if (!lifecycleRegistrationsClean)
                    Log.Error("[CityTimelineMod] runtime startup blocked: prior options/localisation cleanup remains incomplete.");
                else if (!modEnabled)
                    Log.Info("[CityTimelineMod] runtime startup skipped: modEnabled=false.");
                else if (!ecsCleanupComplete)
                    Log.Error("[CityTimelineMod] runtime startup blocked: prior ECS cleanup remains incomplete.");
                else if (!uiCleanupComplete)
                    Log.Error("[CityTimelineMod] runtime startup blocked: prior CoHTML cleanup remains incomplete.");
                else if (!overlayCleanupComplete)
                    Log.Error("[CityTimelineMod] runtime startup blocked: prior overlay cleanup remains incomplete.");
                else if (!runtimeControllerCleanupComplete)
                    Log.Error("[CityTimelineMod] runtime startup blocked: prior runtime-controller cleanup remains incomplete.");
                else if (!activeWorldReady)
                    Log.Error("[CityTimelineMod] runtime startup blocked: active World is unavailable.");
                else if (!priorLifecycleOwnersDisarmed)
                    Log.Error("[CityTimelineMod] runtime startup blocked: a prior Mod lifecycle owner could not be disarmed safely.");
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

            if (!OpenRuntimeAuthorization(_world))
            {
                Log.Error("[CityTimelineMod] runtime startup blocked: lifecycle ownership changed before activation.");
                return;
            }

            var largeMapReady = false;
            var largeMapScopeBegun = false;
            if (runtimeConfig.LargeMapEnabled)
            {
                string beginReason;
                largeMapScopeBegun = SafeBeginLargeMapRuntimeWorld(
                    _world,
                    _lifecycleGeneration,
                    out beginReason
                );

                if (!largeMapScopeBegun)
                {
                    Log.Error(
                        "[CityTimelineMod] LargeMap World scope could not be opened; " +
                        "no patch was installed. " + beginReason
                    );
                }
                else
                {
                    largeMapReady = CityTimelineLargeMapPatcher.Install(true) &&
                        CityTimelineLargeMapState.Enabled;
                }

                if (largeMapReady && !ValidateCurrentLargeMapWorld(_world))
                {
                    Log.Error(
                        "[CityTimelineMod] LargeMap patches are valid, but the active World " +
                        "already owns incompatible terrain dimensions; rolling back."
                    );
                    SafeUninstallLargeMap("active World validation rollback");
                    largeMapReady = false;
                }

                if (!largeMapReady)
                {
                    Log.Error(
                        "[CityTimelineMod] LargeMap requested but its complete " +
                        "57 km contract could not be installed; the module was rolled back."
                    );
                }
                else if (!TryActivateRailOutsideConnectionRepair(updateSystem))
                {
                    Log.Error(
                        "[CityTimelineMod] LargeMap rail support could not be activated; " +
                        "rolling back the complete LargeMap module."
                    );
                    DeactivateRailOutsideConnectionRepair("LargeMap activation rollback");
                    SafeUninstallLargeMap("rail support rollback");
                    largeMapReady = false;
                }

                if (!largeMapReady && largeMapScopeBegun)
                {
                    var failedInstallOwnerClean =
                        SafeUninstallLargeMap("failed activation scope teardown");
                    string endReason;
                    var failedInstallScopeEnded = SafeEndLargeMapRuntimeWorld(
                        _world,
                        _lifecycleGeneration,
                        "failed activation scope teardown",
                        out endReason
                    );

                    if (!failedInstallOwnerClean || !failedInstallScopeEnded)
                    {
                        Log.Error(
                            "[CityTimelineMod] Failed LargeMap activation left cleanup " +
                            "unverified. " + endReason
                        );
                    }
                }
            }
            else
            {
                CityTimelineLargeMapState.Disable();
                Log.Info("[CityTimelineMod] LargeMap disabled by configuration.");
            }

            if (runtimeConfig.PlayableWorldEnabled)
            {
                if (!largeMapReady)
                {
                    PlayableWorldState.Disable();
                    Log.Error(
                        "[CityTimelineMod] PlayableWorld requires a verified LargeMap 57 km " +
                        "installation; no PlayableWorld patch was installed."
                    );
                }
                else if (!PlayableWorldPatcher.Install(true, _world.SequenceNumber))
                {
                    Log.Error("[CityTimelineMod] PlayableWorld requested but atomic installation failed.");
                }
                else
                {
                    TryRecapturePlayableWorldBounds(_world);

                    if (!TryActivatePlayableWorldSystems(updateSystem))
                    {
                        Log.Error(
                            "[CityTimelineMod] PlayableWorld ECS activation failed; " +
                            "rolling back the complete PlayableWorld module."
                        );
                        DeactivatePlayableWorldSystems("PlayableWorld activation rollback");
                        SafeUninstallPlayableWorld("ECS activation rollback");
                    }
                    else
                    {
                        Log.Info(
                            "[CityTimelineMod] PlayableWorld patches and dependent systems active; " +
                            "boundsInitialized=" + PlayableWorldState.Initialized +
                            ", mutationReady=" + PlayableWorldState.Enabled + "."
                        );
                    }
                }
            }
            else
            {
                PlayableWorldState.Disable();
                Log.Info("[CityTimelineMod] PlayableWorld disabled by configuration.");
            }

            // The CoHTML system is independent from both experimental modules,
            // but it starts after them so neither UI nor overlay can create a
            // TerrainSystem before the 57 km patches have been verified.
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

Log.Info(
    "[CityTimelineMod] Visual runtime bootstrap deferred; " +
    "GeoBundleBootstrap will not run during Mod.OnLoad()."
);
		}

        public void OnDispose()
        {


            if (!OwnsActiveLifecycle())
            {
                DisposeSupersededInstance();
                return;
            }

            // 1. Close the runtime gate and stop the exact systems owned/reused
            // by this World before any resource or Harmony teardown.
            CloseRuntimeAuthorization();
            var globalUiGateClean =
                CityTimelineMod.UI.CityTimelineUISystem.CloseRuntimeGate();
            var ownedUiGateClean = _uiSystem == null ||
                CityTimelineMod.UI.CityTimelineUISystem.CloseRuntimeGate(_uiSystem);
            var uiSystemDisabled = DisableSystem(_uiSystem, "CoHTML dispose");
            var uiCleanupComplete =
                globalUiGateClean && ownedUiGateClean && uiSystemDisabled;
            var ownedSystemsClean = DeactivateOwnedRuntimeSystems("dispose");
            var allWorldsClean = NeutralizeAllExistingWorlds(_world, null);
            var ecsCleanupComplete = ownedSystemsClean && allWorldsClean;

            // 2. Flush pending overlay state, then destroy all current and stale
            // CTM roots/resources.  The legacy controller is cleanup-only.
            var overlayCleanupComplete = SafeUninstallOverlay("dispose");
            var runtimeControllerCleanupComplete =
                CityTimelineRuntimeController.Uninstall();

            // 3. Make the next load/reload retry the bootstrap.
            GeoBundleBootstrap.Reset(true);

            // 4. State gates close before owner-scoped Harmony teardown.
            CityTimelineLargeMapState.Disable();
            PlayableWorldState.Disable();

// 5-6. PlayableWorld can be removed normally.
//
// LargeMap is different: during normal application termination,
// GameManager calls ModManager.Dispose() before DestroyWorld().
// A dirty 57 km simulation World must therefore keep its Harmony owner
// and extended layout until the process destroys that World.
var playableWorldPatchesClean = SafeUninstallPlayableWorld("dispose");

var gameManager = GameManager.instance;
var processShutdown =
    gameManager != null &&
    gameManager.state == GameManager.State.Quitting;

var largeMapCleanupComplete = false;

if (processShutdown)
{
    largeMapCleanupComplete = true;

Debug.Log(
    "[CityTimelineMod] LargeMap teardown intentionally skipped during " +
    "game termination; the live World will be destroyed immediately " +
    "after ModManager.Dispose()."
);
}
else
{
    var largeMapPatchesClean =
        TryCoordinatedLargeMapPatcherTeardown("dispose");

    var largeMapScopeEndReason =
        "coordinated LargeMap teardown did not complete";
    var largeMapScopeEnded = false;

    if (largeMapPatchesClean)
    {
        largeMapScopeEnded = SafeEndLargeMapRuntimeWorld(
            _world,
            _lifecycleGeneration,
            "dispose",
            out largeMapScopeEndReason
        );
    }

    if (!largeMapScopeEnded)
    {
        Log.Error(
            "[CityTimelineMod] dispose retained a dirty LargeMap World scope: " +
            largeMapScopeEndReason + "."
        );
    }

    largeMapCleanupComplete =
        largeMapPatchesClean &&
        largeMapScopeEnded;
}

            // 7. Official options and exact localisation sources are removed.
            var supersededLifecycleRegistrationsClean =
                TryCleanupSupersededLifecycleRegistrations();
            var optionsCleanupComplete = SafeUnregisterOptions();
            var localizationCleanupComplete = RemoveLocalizationSources();

            var disposeComplete =
                uiCleanupComplete &&
                ecsCleanupComplete &&
                overlayCleanupComplete &&
                runtimeControllerCleanupComplete &&
                playableWorldPatchesClean &&
				largeMapCleanupComplete &&
                supersededLifecycleRegistrationsClean &&
                optionsCleanupComplete &&
                localizationCleanupComplete;

            if (disposeComplete)
            {
                // 8. Reset every instance/static reference only after each
                // cleanup contract has completed successfully.
                _world = null;
                _uiSystem = null;
                _playableWorldErrorPrefabSystem = null;
                _playableWorldRoadProfileSystem = null;
                _railOutsideConnectionRepairSystem = null;
                _railPreSerializeSystem = null;
                _supersededLifecycleOwnerPendingCleanup = null;
                Settings = null;
                RuntimeConfig = null;

                ReleaseLifecycleOwnership();
                Log.Info("[CityTimelineMod] Disposed.");
            }
            else
                Log.Error("[CityTimelineMod] dispose completed with pending rollback state.");


        }

        private void CapturePriorLifecycleScope(
            out Mod owner,
            out World world,
            out long generation)
        {
            lock (LifecycleSync)
            {
                if (_activeLifecycleOwner != null &&
                    _activeLifecycleGeneration > 0)
                {
                    owner = _activeLifecycleOwner;
                    world = _activeLifecycleOwner._world;
                    generation = _activeLifecycleGeneration;
                    return;
                }

                owner = null;
                world = _world;
                generation = _lifecycleGeneration;
            }
        }

        private void ClaimLifecycleOwnership()
        {
            var generation = Interlocked.Increment(ref _nextLifecycleGeneration);

            lock (LifecycleSync)
            {
                _lifecycleGeneration = generation;
                _activeLifecycleOwner = this;
                _activeLifecycleGeneration = generation;
                _activeRuntimeWorldSequence = 0;
                _largeMapRuntimeFailurePending = false;
                _largeMapRuntimeFailureGeneration = 0;
                _largeMapRuntimeFailureReason = null;
                RuntimeEnabled = false;
            }
        }

        private bool OwnsActiveLifecycle()
        {
            lock (LifecycleSync)
            {
                return
                    _lifecycleGeneration != 0 &&
                    ReferenceEquals(_activeLifecycleOwner, this) &&
                    _activeLifecycleGeneration == _lifecycleGeneration;
            }
        }

        private void CloseRuntimeAuthorization()
        {
            lock (LifecycleSync)
            {
                if (!ReferenceEquals(_activeLifecycleOwner, this) ||
                    _activeLifecycleGeneration != _lifecycleGeneration)
                {
                    return;
                }

                RuntimeEnabled = false;
                _activeRuntimeWorldSequence = 0;
                _largeMapRuntimeFailurePending = false;
                _largeMapRuntimeFailureGeneration = 0;
                _largeMapRuntimeFailureReason = null;
            }
        }

        private bool OpenRuntimeAuthorization(World world)
        {
            if (world == null || !world.IsCreated)
                return false;

            lock (LifecycleSync)
            {
                if (!ReferenceEquals(_activeLifecycleOwner, this) ||
                    _activeLifecycleGeneration != _lifecycleGeneration)
                {
                    return false;
                }

                _activeRuntimeWorldSequence = world.SequenceNumber;
                RuntimeEnabled = true;
                return true;
            }
        }

        private static bool SafeBeginLargeMapRuntimeWorld(
            World world,
            long generation,
            out string reason)
        {
            try
            {
                return CityTimelineLargeMapState.TryBeginRuntimeWorld(
                    world,
                    generation,
                    out reason
                );
            }
            catch (Exception ex)
            {
                reason = "LargeMap World-scope begin threw: " + ex;
                Log.Error("[CityTimelineMod] " + reason);
                return false;
            }
        }

        private static bool SafeEndLargeMapRuntimeWorld(
            World world,
            long generation,
            string stage,
            out string reason)
        {
            try
            {
                var complete = CityTimelineLargeMapState.TryEndRuntimeWorld(
                    world,
                    generation,
                    out reason
                );
                if (!complete)
                {
                    Log.Error(
                        "[CityTimelineMod] LargeMap World-scope end blocked during " +
                        stage + ": " + reason
                    );
                }

                return complete;
            }
            catch (Exception ex)
            {
                reason = "LargeMap World-scope end threw during " + stage + ": " + ex;
                Log.Error("[CityTimelineMod] " + reason);
                return false;
            }
        }

        private static bool SafeReleaseDestroyedLargeMapRuntimeWorld(
            string stage,
            out string reason)
        {
            try
            {
                var complete =
                    CityTimelineLargeMapState.TryReleaseDestroyedRuntimeWorld(
                        out reason
                    );
                if (!complete)
                {
                    Log.Error(
                        "[CityTimelineMod] LargeMap destroyed-World release blocked during " +
                        stage + ": " + reason
                    );
                }

                return complete;
            }
            catch (Exception ex)
            {
                reason = "LargeMap destroyed-World release threw during " + stage + ": " + ex;
                Log.Error("[CityTimelineMod] " + reason);
                return false;
            }
        }

        private void ReleaseLifecycleOwnership()
        {
            lock (LifecycleSync)
            {
                if (ReferenceEquals(_activeLifecycleOwner, this) &&
                    _activeLifecycleGeneration == _lifecycleGeneration)
                {
                    RuntimeEnabled = false;
                    _activeRuntimeWorldSequence = 0;
                    _largeMapRuntimeFailurePending = false;
                    _largeMapRuntimeFailureGeneration = 0;
                    _largeMapRuntimeFailureReason = null;
                    _activeLifecycleOwner = null;
                    _activeLifecycleGeneration = 0;
                }

                _lifecycleGeneration = 0;
            }
        }

        private void DisposeSupersededInstance()
        {
            // A delayed OnDispose from an older Mod object must not touch shared
            // Harmony owners, Worlds, runtime gates, or systems which may now be
            // owned by the replacement instance. Only instance-owned UI options
            // and localisation sources can be released safely here.
            var nestedCleanupComplete =
                TryCleanupSupersededLifecycleRegistrations();
            var optionsCleanupComplete = SafeUnregisterOptions();
            var localizationCleanupComplete = RemoveLocalizationSources();
            var cleanupComplete =
                nestedCleanupComplete &&
                optionsCleanupComplete &&
                localizationCleanupComplete;

            if (cleanupComplete)
            {
                _world = null;
                _uiSystem = null;
                _playableWorldErrorPrefabSystem = null;
                _playableWorldRoadProfileSystem = null;
                _railOutsideConnectionRepairSystem = null;
                _railPreSerializeSystem = null;
                _lifecycleGeneration = 0;

                Log.Info("[CityTimelineMod] Superseded Mod instance disposed without touching the active runtime.");
            }
            else
            {
                Log.Error(
                    "[CityTimelineMod] Superseded Mod instance retained " +
                    "registration references for a later cleanup retry."
                );
            }
        }

        private bool TryCleanupSupersededLifecycleRegistrations()
        {
            var superseded = _supersededLifecycleOwnerPendingCleanup;
            if (superseded == null || ReferenceEquals(superseded, this))
            {
                _supersededLifecycleOwnerPendingCleanup = null;
                return true;
            }

            var nestedClean =
                superseded.TryCleanupSupersededLifecycleRegistrations();
            var optionsClean = superseded.SafeUnregisterOptions();
            var localesClean = superseded.RemoveLocalizationSources();
            var complete = nestedClean && optionsClean && localesClean;

            if (complete)
                _supersededLifecycleOwnerPendingCleanup = null;

            return complete;
        }

        internal static bool RelinquishRuntimeOwnershipForHotReload()
        {
            return RelinquishRuntimeOwnershipForHotReloadV2();
        }

        internal static bool RelinquishRuntimeOwnershipForHotReloadV2()
        {
            Mod owner;

            try
            {
                lock (LifecycleSync)
                {
                    owner = _activeLifecycleOwner;
                    RuntimeEnabled = false;
                    _activeRuntimeWorldSequence = 0;
                    _largeMapRuntimeFailurePending = false;
                    _largeMapRuntimeFailureGeneration = 0;
                    _largeMapRuntimeFailureReason = null;
                    // Keep the owner reachable until its instance registrations
                    // have been removed successfully. Generation zero prevents a
                    // delayed OnDispose from treating it as the active runtime.
                    _activeLifecycleGeneration = 0;
                }

                var uiGateClean =
                    CityTimelineMod.UI.CityTimelineUISystem.CloseRuntimeGate();
                var supersededClean = owner == null ||
                    owner.TryCleanupSupersededLifecycleRegistrations();
                var optionsClean = owner == null
                    ? Settings == null
                    : owner.SafeUnregisterOptions();
                var localesClean = owner == null ||
                    owner.RemoveLocalizationSources();
                var complete =
                    uiGateClean &&
                    supersededClean &&
                    optionsClean &&
                    localesClean;

                lock (LifecycleSync)
                {
                    if (complete && ReferenceEquals(_activeLifecycleOwner, owner))
                        _activeLifecycleOwner = null;
                }

                if (!complete)
                {
                    Log.Error(
                        "[CityTimelineMod] Hot-reload ownership relinquish kept " +
                        "the prior instance reachable for cleanup retry: uiGateClean=" +
                        uiGateClean + ", supersededClean=" + supersededClean +
                        ", optionsClean=" + optionsClean +
                        ", localesClean=" + localesClean + "."
                    );
                }

                return complete;
            }
            catch (Exception ex)
            {
                Log.Error(
                    "[CityTimelineMod] Lifecycle ownership relinquish failed: " + ex
                );
                return false;
            }
        }

        private static bool RelinquishPriorAssemblyLifecycleOwners(string stage)
        {
            var complete = true;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly == typeof(Mod).Assembly ||
                    !string.Equals(
                        assembly.GetName().Name,
                        typeof(Mod).Assembly.GetName().Name,
                        StringComparison.Ordinal
                    ))
                {
                    continue;
                }

                Type priorModType;
                try
                {
                    priorModType = assembly.GetType("CityTimelineMod.Mod", false);
                }
                catch (Exception ex)
                {
                    Log.Error(
                        "[CityTimelineMod] Prior Mod type lookup failed during " +
                        stage + ": " + ex
                    );
                    return false;
                }

                if (priorModType == null)
                    continue;

                try
                {
                    var relinquish = priorModType.GetMethod(
                        "RelinquishRuntimeOwnershipForHotReloadV2",
                        BindingFlags.Static |
                        BindingFlags.Public |
                        BindingFlags.NonPublic,
                        null,
                        Type.EmptyTypes,
                        null
                    );

                    if (relinquish != null && relinquish.ReturnType == typeof(bool))
                    {
                        var result = relinquish.Invoke(null, null);
                        if (!(result is bool) || !(bool)result)
                            complete = false;
                        continue;
                    }

                    // Legacy hook/field fallback is cleanup-only: it closes the
                    // old runtime gate where possible, but cannot prove removal
                    // of that instance's options/localisation registrations. The
                    // result therefore remains false and no new registrations or
                    // runtime are allowed until the process restarts.
                    var legacyRelinquish = priorModType.GetMethod(
                        "RelinquishRuntimeOwnershipForHotReload",
                        BindingFlags.Static |
                        BindingFlags.Public |
                        BindingFlags.NonPublic,
                        null,
                        Type.EmptyTypes,
                        null
                    );
                    if (legacyRelinquish != null &&
                        legacyRelinquish.ReturnType == typeof(bool))
                    {
                        legacyRelinquish.Invoke(null, null);
                    }
                    else
                    {
                        TryRelinquishKnownPriorLifecycleFields(priorModType);
                    }

                    Log.Error(
                        "[CityTimelineMod] Prior Mod assembly lacks the V2 lifecycle " +
                        "contract that removes its options/localisation registrations " +
                        "during " + stage + ": " + assembly.FullName +
                        ". Runtime remains fail-closed until process restart."
                    );
                    complete = false;
                }
                catch (Exception ex)
                {
                    Log.Error(
                        "[CityTimelineMod] Prior Mod lifecycle disarm failed during " +
                        stage + " for " + assembly.FullName + ": " + ex
                    );
                    complete = false;
                }
            }

            return complete;
        }

        private static bool TryRelinquishKnownPriorLifecycleFields(Type priorModType)
        {
            var flags = BindingFlags.Static |
                BindingFlags.Public |
                BindingFlags.NonPublic;
            var ownerField = priorModType.GetField(
                "_activeLifecycleOwner",
                flags
            );
            var generationField = priorModType.GetField(
                "_activeLifecycleGeneration",
                flags
            );
            var runtimeField = priorModType.GetField(
                "<RuntimeEnabled>k__BackingField",
                flags
            );

            if (ownerField == null || generationField == null ||
                runtimeField == null || runtimeField.FieldType != typeof(bool))
            {
                return false;
            }

            runtimeField.SetValue(null, false);
            ownerField.SetValue(null, null);
            generationField.SetValue(null, Convert.ChangeType(0, generationField.FieldType));

            SetPriorStaticNumericField(priorModType, "_activeRuntimeWorldSequence", 0);
            SetPriorStaticBooleanField(priorModType, "_largeMapRuntimeFailurePending", false);
            SetPriorStaticNumericField(priorModType, "_largeMapRuntimeFailureGeneration", 0);
            SetPriorStaticReferenceField(priorModType, "_largeMapRuntimeFailureReason", null);
            return true;
        }

        private static bool IsPriorLifecycleAlreadyInactive(Type priorModType)
        {
            var flags = BindingFlags.Static |
                BindingFlags.Public |
                BindingFlags.NonPublic;
            var runtimeField = priorModType.GetField(
                "<RuntimeEnabled>k__BackingField",
                flags
            );
            var settingsField = priorModType.GetField(
                "<Settings>k__BackingField",
                flags
            );

            if (runtimeField == null || settingsField == null ||
                runtimeField.FieldType != typeof(bool))
            {
                return false;
            }

            return !(bool)runtimeField.GetValue(null) &&
                settingsField.GetValue(null) == null;
        }

        private static void SetPriorStaticNumericField(
            Type type,
            string name,
            long value)
        {
            var field = type.GetField(
                name,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
            );
            if (field != null)
                field.SetValue(null, Convert.ChangeType(value, field.FieldType));
        }

        private static void SetPriorStaticBooleanField(
            Type type,
            string name,
            bool value)
        {
            var field = type.GetField(
                name,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
            );
            if (field != null && field.FieldType == typeof(bool))
                field.SetValue(null, value);
        }

        private static void SetPriorStaticReferenceField(
            Type type,
            string name,
            object value)
        {
            var field = type.GetField(
                name,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
            );
            if (field != null && !field.FieldType.IsValueType)
                field.SetValue(null, value);
        }

        internal static bool IsActiveRuntimeWorld(World world)
        {
            if (world == null || !world.IsCreated)
                return false;

            lock (LifecycleSync)
            {
                return
                    RuntimeEnabled &&
                    _activeLifecycleOwner != null &&
                    _activeLifecycleGeneration != 0 &&
                    _activeLifecycleOwner._lifecycleGeneration ==
                        _activeLifecycleGeneration &&
                    world.SequenceNumber == _activeRuntimeWorldSequence;
            }
        }

        internal static void NotifyLargeMapRuntimeFailure(string reason)
        {
            lock (LifecycleSync)
            {
                if (!RuntimeEnabled || _activeLifecycleOwner == null ||
                    _activeLifecycleGeneration == 0)
                {
                    return;
                }

                _largeMapRuntimeFailurePending = true;
                _largeMapRuntimeFailureGeneration =
                    _activeLifecycleGeneration;
                _largeMapRuntimeFailureReason = string.IsNullOrWhiteSpace(reason)
                    ? "unspecified LargeMap runtime failure"
                    : reason;
            }
        }

        internal static bool TryProcessPendingLargeMapRuntimeFailure(World callerWorld)
        {
            Mod owner;
            long generation;
            string reason;

            lock (LifecycleSync)
            {
                if (!_largeMapRuntimeFailurePending ||
                    _activeLifecycleOwner == null ||
                    _largeMapRuntimeFailureGeneration !=
                        _activeLifecycleGeneration ||
                    callerWorld == null || !callerWorld.IsCreated ||
                    callerWorld.SequenceNumber != _activeRuntimeWorldSequence)
                {
                    return false;
                }

                owner = _activeLifecycleOwner;
                generation = _activeLifecycleGeneration;
                reason = _largeMapRuntimeFailureReason;
            }

            var cleanupComplete = owner.HandleLargeMapRuntimeFailure(reason);

            lock (LifecycleSync)
            {
                if (ReferenceEquals(_activeLifecycleOwner, owner) &&
                    _activeLifecycleGeneration == generation &&
                    cleanupComplete)
                {
                    _largeMapRuntimeFailurePending = false;
                    _largeMapRuntimeFailureGeneration = 0;
                    _largeMapRuntimeFailureReason = null;
                }
            }

            return true;
        }

        private bool HandleLargeMapRuntimeFailure(string reason)
        {
            if (!OwnsActiveLifecycle())
                return false;

            // FailRuntime has already closed the LargeMap state gate. This safe
            // ECS update boundary first restores every dependent ECS mutation.
            // PlayableWorld Harmony can then be removed because this method is
            // called from an unpatched system update. The dirty LargeMap owner
            // deliberately remains installed until preflight or dispose, where
            // its World-scoped rollback contract can be evaluated safely.
            PlayableWorldState.Disable();
            var ecsClean = DeactivateOwnedRuntimeSystems(
                "LargeMap runtime failure"
            );
            var playableWorldOwnerClean = false;

            if (ecsClean)
            {
                playableWorldOwnerClean = SafeUninstallPlayableWorld(
                    "LargeMap runtime failure"
                );
            }

            var complete = ecsClean && playableWorldOwnerClean;

            if (!complete && !KeepLargeMapFailureRetryConsumerEnabled())
            {
                Log.Error(
                    "[CityTimelineMod] LargeMap runtime failure retry consumer " +
                    "could not be kept active. Cleanup remains fail-closed."
                );
            }

            if (complete)
            {
                Log.Error(
                    "[CityTimelineMod] LargeMap runtime failure neutralized dependent " +
                    "ECS mutation systems and removed the PlayableWorld Harmony " +
                    "owner: " + reason + "."
                );
            }
            else
            {
                Log.Error(
                    "[CityTimelineMod] LargeMap runtime failure cleanup remains pending: " +
                    reason + "."
                );
            }

            return complete;
        }

        private bool KeepLargeMapFailureRetryConsumerEnabled()
        {
            var complete = true;

            if (_railOutsideConnectionRepairSystem == null)
            {
                Log.Error(
                    "[CityTimelineMod] LargeMap runtime failure has no rail " +
                    "repair system available to consume the deferred cleanup."
                );
                complete = false;
            }
            else
            {
                try
                {
                    // DeactivateAndRollback may have disabled this system after
                    // completing its own restoration. Keep it inert but scheduled
                    // until every ECS restoration and the PlayableWorld owner
                    // uninstall have succeeded.
                    _railOutsideConnectionRepairSystem.Enabled = true;
                }
                catch (Exception ex)
                {
                    Log.Error(
                        "[CityTimelineMod] LargeMap runtime failure rail retry " +
                        "consumer activation failed: " + ex
                    );
                    complete = false;
                }
            }

            if (_railPreSerializeSystem == null)
            {
                Log.Error(
                    "[CityTimelineMod] LargeMap runtime failure has no rail " +
                    "pre-serialize guard available during deferred cleanup."
                );
                complete = false;
            }
            else
            {
                try
                {
                    _railPreSerializeSystem.Enabled = true;
                }
                catch (Exception ex)
                {
                    Log.Error(
                        "[CityTimelineMod] LargeMap runtime failure rail " +
                        "pre-serialize guard activation failed: " + ex
                    );
                    complete = false;
                }
            }

            return complete;
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

        private bool TryActivateRailOutsideConnectionRepair(UpdateSystem updateSystem)
        {
            try
            {
                _railOutsideConnectionRepairSystem =
                    GetOrRegisterSystem<RailOutsideConnectionRepairSystem>(
                        updateSystem,
                        SystemUpdatePhase.Modification1
                    );

                _railPreSerializeSystem =
                    GetOrRegisterSystemBefore<PreSerialize<RailOutsideConnectionRepairSystem>>(
                        updateSystem,
                        SystemUpdatePhase.Serialize
                    );

                if (!_railOutsideConnectionRepairSystem.ActivateForRuntime())
                    throw new InvalidOperationException("Rail repair runtime gate could not be opened.");

                _railPreSerializeSystem.Enabled = true;

                return true;
            }
            catch (Exception ex)
            {
                Log.Error("[CityTimelineMod] Rail repair activation failed: " + ex);
                DeactivateRailOutsideConnectionRepair("rail activation failure");
                return false;
            }
        }

        private static T GetOrRegisterSystemBefore<T>(
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
                updateSystem.UpdateBefore<T>(phase);
                system = world.GetExistingSystemManaged<T>();

                if (system == null)
                    throw new InvalidOperationException("UpdateBefore did not create " + typeof(T).FullName + ".");

                Log.Info(
                    "[CityTimelineMod] ECS pre-serialize system registered once for World sequence=" +
                    world.SequenceNumber + ": " + typeof(T).FullName
                );
            }
            else
            {
                Log.Info(
                    "[CityTimelineMod] ECS pre-serialize system reused for World sequence=" +
                    world.SequenceNumber + ": " + typeof(T).FullName
                );
            }

            return system;
        }

        private bool TryActivatePlayableWorldSystems(UpdateSystem updateSystem)
        {
            if (!TryActivatePlayableWorldErrorPrefab(updateSystem))
                return false;

            if (!TryActivatePlayableWorldRoadProfile(updateSystem))
            {
                DeactivatePlayableWorldSystems("road-profile activation rollback");
                return false;
            }

            return true;
        }

        private bool TryActivatePlayableWorldErrorPrefab(UpdateSystem updateSystem)
        {
            try
            {
                _playableWorldErrorPrefabSystem =
                    GetOrRegisterSystem<PlayableWorldErrorPrefabSystem>(
                        updateSystem,
                        SystemUpdatePhase.ToolUpdate
                    );

                if (!_playableWorldErrorPrefabSystem.ActivateForRuntime())
                    throw new InvalidOperationException("Error-prefab runtime gate could not be opened.");

                return true;
            }
            catch (Exception ex)
            {
                Log.Error("[CityTimelineMod] PlayableWorld error-prefab activation failed: " + ex);
                DeactivatePlayableWorldErrorPrefab("error-prefab activation failure");
                return false;
            }
        }

        private bool TryActivatePlayableWorldRoadProfile(UpdateSystem updateSystem)
        {
            try
            {
                _playableWorldRoadProfileSystem =
                    GetOrRegisterSystem<PlayableWorldRoadProfileSystem>(
                        updateSystem,
                        SystemUpdatePhase.Modification1
                    );

                if (!_playableWorldRoadProfileSystem.ActivateForRuntime())
                    throw new InvalidOperationException("Road-profile runtime gate could not be opened.");

                return true;
            }
            catch (Exception ex)
            {
                Log.Error("[CityTimelineMod] PlayableWorld road-profile activation failed: " + ex);
                DeactivatePlayableWorldRoadProfile("road-profile activation failure");
                return false;
            }
        }

        private bool DeactivateOwnedRuntimeSystems(string stage)
        {
            var playableWorldClean = DeactivatePlayableWorldSystems(stage);
            var railClean = DeactivateRailOutsideConnectionRepair(stage);
            return playableWorldClean && railClean;
        }

        private bool DeactivatePlayableWorldSystems(string stage)
        {
            var errorPrefabClean = DeactivatePlayableWorldErrorPrefab(stage);
            var roadProfileClean = DeactivatePlayableWorldRoadProfile(stage);
            return errorPrefabClean && roadProfileClean;
        }

        private bool DeactivatePlayableWorldErrorPrefab(string stage)
        {
            if (_playableWorldErrorPrefabSystem == null)
                return true;

            var complete = false;

            try
            {
                complete = _playableWorldErrorPrefabSystem.DeactivateAndRestore();
                if (!complete)
                {
                    Log.Error(
                        "[CityTimelineMod] PlayableWorld error-prefab " + stage +
                        " restoration remains incomplete."
                    );
                }
            }
            catch (Exception ex)
            {
                Log.Error("[CityTimelineMod] PlayableWorld error-prefab " + stage + " cleanup failed: " + ex);
            }

            if (complete)
                return DisableSystem(_playableWorldErrorPrefabSystem, "PlayableWorld error-prefab " + stage);

            return false;
        }

        private bool DeactivatePlayableWorldRoadProfile(string stage)
        {
            if (_playableWorldRoadProfileSystem == null)
                return true;

            try
            {
                _playableWorldRoadProfileSystem.DeactivateForRuntime();
            }
            catch (Exception ex)
            {
                Log.Error("[CityTimelineMod] PlayableWorld road-profile " + stage + " cleanup failed: " + ex);
                return false;
            }

            return DisableSystem(_playableWorldRoadProfileSystem, "PlayableWorld road-profile " + stage);
        }

        private bool DeactivateRailOutsideConnectionRepair(string stage)
        {
            if (_railOutsideConnectionRepairSystem == null)
            {
                return DisableSystem(
                    _railPreSerializeSystem,
                    "rail repair pre-serialize " + stage
                );
            }

            var complete = false;

            try
            {
                complete = _railOutsideConnectionRepairSystem.DeactivateAndRollback();
                if (!complete)
                {
                    Log.Error(
                        "[CityTimelineMod] Rail repair " + stage +
                        " rollback remains incomplete."
                    );
                }
            }
            catch (Exception ex)
            {
                Log.Error("[CityTimelineMod] Rail repair " + stage + " cleanup failed: " + ex);
            }

            if (!complete)
            {
                if (_railPreSerializeSystem != null)
                    _railPreSerializeSystem.Enabled = true;
                return false;
            }

            var repairDisabled = DisableSystem(
                _railOutsideConnectionRepairSystem,
                "rail repair " + stage
            );
            var serializerDisabled = DisableSystem(
                _railPreSerializeSystem,
                "rail repair pre-serialize " + stage
            );
            return repairDisabled && serializerDisabled;
        }

        private static bool NeutralizeAllExistingWorlds(
            World firstExplicitWorld,
            World secondExplicitWorld)
        {
            var worlds = new List<World>();
            var seenSequences = new HashSet<ulong>();
            var complete = true;

            try
            {
                var allWorlds = World.All;
                for (var i = 0; i < allWorlds.Count; i++)
                    AddWorldForCleanup(allWorlds[i], worlds, seenSequences);
            }
            catch (Exception ex)
            {
                complete = false;
                Log.Error("[CityTimelineMod] World.All enumeration failed: " + ex);
            }

            AddWorldForCleanup(firstExplicitWorld, worlds, seenSequences);
            AddWorldForCleanup(secondExplicitWorld, worlds, seenSequences);

            for (var i = 0; i < worlds.Count; i++)
                complete = NeutralizeExistingSystems(worlds[i]) && complete;

            return complete;
        }

        private static void AddWorldForCleanup(
            World world,
            List<World> worlds,
            HashSet<ulong> seenSequences)
        {
            if (world == null || !world.IsCreated)
                return;

            if (seenSequences.Add(world.SequenceNumber))
                worlds.Add(world);
        }

        private static bool NeutralizeExistingSystems(World world)
        {
            if (world == null || !world.IsCreated)
                return true;

            var complete = DeactivateExistingRuntimeSystems(world);

            try
            {
                var modernUi = world.GetExistingSystemManaged<CityTimelineMod.UI.CityTimelineUISystem>();
                var gateClean = modernUi == null ||
                    CityTimelineMod.UI.CityTimelineUISystem.CloseRuntimeGate(modernUi);
                complete = gateClean && complete;
                complete = DisableSystem(modernUi, "existing modern UI") && complete;
            }
            catch (Exception ex)
            {
                complete = false;
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
                complete = false;
                Log.Error("[CityTimelineMod] Legacy UI cleanup failed: " + ex);
            }

            complete = DisableExistingSystem<LargeMapTerrainDiagnosticSystem>(world) && complete;

            // A hot reload may leave systems whose CLR type belongs to the
            // previous CityTimelineMod assembly identity. Generic lookup cannot
            // see them, so scan the exact CTM full names in every loaded assembly.
            complete = DisableExistingSystemByName(world, "CityTimelineMod.UI.CityTimelineUISystem", true) && complete;
            complete = DisableExistingSystemByName(world, "CityTimelineMod.Systems.CityTimelineUISystem", true) && complete;
            complete = DisableExistingSystemByName(
                world,
                "CityTimelineMod.PlayableWorld.PlayableWorldRoadProfileSystem",
                false,
                true
            ) && complete;
            complete = DisableExistingSystemByName(
                world,
                "CityTimelineMod.PlayableWorld.PlayableWorldErrorPrefabSystem",
                false,
                true
            ) && complete;
            complete = DisableExistingSystemByName(
                world,
                "CityTimelineMod.LargeMap.RailOutsideConnectionRepairSystem",
                false,
                true
            ) && complete;
            complete = DisableExistingSystemByName(world, "CityTimelineMod.Diagnostics.LargeMapTerrainDiagnosticSystem") && complete;

            // These development/legacy systems are excluded from some build
            // profiles.  Resolve them only when their type is actually present.
            complete = DisableExistingSystemByName(world, "CityTimelineMod.LargeMap.AirOutsideConnectionRepairSystem") && complete;
            complete = DisableExistingSystemByName(world, "CityTimelineMod.Systems.RuntimeRoadToolDriverSystem") && complete;
            complete = DisableExistingSystemByName(world, "CityTimelineMod.Roads.RuntimeRoadToolSystem") && complete;
            complete = DisableExistingSystemByName(world, "CityTimelineMod.Roads.VanillaNetCourseProbeSystem") && complete;

            return complete;
        }

        private static bool DeactivateExistingRuntimeSystems(World world)
        {
            var complete = true;

            try
            {
                var system = world.GetExistingSystemManaged<PlayableWorldErrorPrefabSystem>();
                var restored = system == null || system.DeactivateAndRestore();
                if (!restored)
                {
                    complete = false;
                    Log.Error("[CityTimelineMod] Existing error-prefab restoration remains incomplete.");
                }
                else
                {
                    complete = DisableSystem(system, "existing PlayableWorld error-prefab") && complete;
                }
            }
            catch (Exception ex)
            {
                complete = false;
                Log.Error("[CityTimelineMod] Existing error-prefab cleanup failed: " + ex);
            }

            try
            {
                var system = world.GetExistingSystemManaged<PlayableWorldRoadProfileSystem>();
                system?.DeactivateForRuntime();
                complete = DisableSystem(
                    system,
                    "existing PlayableWorld road-profile"
                ) && complete;
            }
            catch (Exception ex)
            {
                complete = false;
                Log.Error("[CityTimelineMod] Existing road-profile cleanup failed: " + ex);
            }

            try
            {
                var system = world.GetExistingSystemManaged<RailOutsideConnectionRepairSystem>();
                var rolledBack = system == null || system.DeactivateAndRollback();
                var serializer = world.GetExistingSystemManaged<
                    PreSerialize<RailOutsideConnectionRepairSystem>>();
                if (!rolledBack)
                {
                    complete = false;
                    if (serializer != null)
                        serializer.Enabled = true;
                    Log.Error("[CityTimelineMod] Existing rail rollback remains incomplete.");
                }
                else
                {
                    complete = DisableSystem(system, "existing rail repair") && complete;
                    complete = DisableSystem(serializer, "existing rail pre-serialize") && complete;
                }
            }
            catch (Exception ex)
            {
                complete = false;
                Log.Error("[CityTimelineMod] Existing rail cleanup failed: " + ex);
            }

            return complete;
        }

        private static bool DisableExistingSystem<T>(World world)
            where T : ComponentSystemBase
        {
            try
            {
                return DisableSystem(world.GetExistingSystemManaged<T>(), typeof(T).FullName);
            }
            catch (Exception ex)
            {
                Log.Error("[CityTimelineMod] Existing ECS cleanup failed for " + typeof(T).FullName + ": " + ex);
                return false;
            }
        }

        private static bool DisableExistingSystemByName(
            World world,
            string fullName,
            bool detachUiCallbacks = false,
            bool invokeRuntimeTeardown = false)
        {
            var foundType = false;
            var complete = true;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var type = assembly.GetType(fullName, false);
                    if (type == null || !typeof(ComponentSystemBase).IsAssignableFrom(type))
                        continue;

                    foundType = true;
                    var system = world.GetExistingSystemManaged(type);

                    // Current-assembly mutators were already handled through
                    // their strongly typed, result-bearing teardown above. A
                    // second name-based disable would suppress a pending retry.
                    if (invokeRuntimeTeardown &&
                        type.Assembly == typeof(Mod).Assembly)
                    {
                        continue;
                    }

                    if (system != null &&
                        detachUiCallbacks &&
                        type.Assembly != typeof(Mod).Assembly)
                    {
                        complete = DetachPriorAssemblyUi(system, type) && complete;
                    }

                    if (system != null &&
                        invokeRuntimeTeardown &&
                        type.Assembly != typeof(Mod).Assembly)
                    {
                        var teardownComplete =
                            InvokePriorAssemblyRuntimeTeardown(system, type);
                        complete = teardownComplete && complete;

                        if (teardownComplete &&
                            fullName == "CityTimelineMod.LargeMap.RailOutsideConnectionRepairSystem")
                        {
                            complete = DisablePriorRailPreSerializeWrapper(
                                world,
                                type
                            ) && complete;
                        }

                        if (!teardownComplete)
                            continue;
                    }

                    complete = DisableSystem(system, fullName) && complete;
                }
                catch (Exception ex)
                {
                    complete = false;
                    Log.Error(
                        "[CityTimelineMod] Existing optional ECS cleanup failed for " +
                        fullName + " in assembly " + assembly.FullName + ": " + ex
                    );
                }
            }

            if (foundType)
                Log.Info("[CityTimelineMod] Optional ECS type inspected for cleanup: " + fullName + ".");

            return complete;
        }

        private static bool InvokePriorAssemblyRuntimeTeardown(
            ComponentSystemBase system,
            Type systemType)
        {
            var methodNames = new[]
            {
                "DeactivateAndRestore",
                "DeactivateAndRollback",
                "DeactivateForRuntime"
            };

            foreach (var methodName in methodNames)
            {
                try
                {
                    var method = systemType.GetMethod(
                        methodName,
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                        null,
                        Type.EmptyTypes,
                        null
                    );

                    if (method == null)
                        continue;

                    var result = method.Invoke(system, null);
                    if (method.ReturnType == typeof(bool) && result is bool complete && !complete)
                    {
                        Log.Error(
                            "[CityTimelineMod] Prior assembly teardown remains incomplete: " +
                            systemType.FullName + "." + methodName + "."
                        );
                        return false;
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    Log.Error(
                        "[CityTimelineMod] Prior assembly teardown failed: " +
                        systemType.FullName + "." + methodName + ". " + ex
                    );
                    return false;
                }
            }

            Log.Error(
                "[CityTimelineMod] Prior assembly has no recognized runtime teardown: " +
                systemType.FullName + "."
            );
            return false;
        }

        private static bool DisablePriorRailPreSerializeWrapper(
            World world,
            Type priorRailSystemType)
        {
            try
            {
                if (!typeof(IPreSerialize).IsAssignableFrom(priorRailSystemType))
                    return true;

                var wrapperType = typeof(PreSerialize<>).MakeGenericType(
                    priorRailSystemType
                );
                var wrapper = world.GetExistingSystemManaged(wrapperType);
                return DisableSystem(
                    wrapper,
                    "prior rail repair pre-serialize"
                );
            }
            catch (Exception ex)
            {
                Log.Error(
                    "[CityTimelineMod] Prior rail pre-serialize cleanup failed: " + ex
                );
                return false;
            }
        }

        private static bool DetachPriorAssemblyUi(ComponentSystemBase system, Type systemType)
        {
            var complete = true;

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
                complete = false;
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
                complete = false;
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
                complete = false;
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
                complete = false;
                Log.Error("[CityTimelineMod] Prior UI input cleanup failed: " + ex);
            }

            var registry = GameManager.instance != null &&
                GameManager.instance.userInterface != null
                ? GameManager.instance.userInterface.bindings
                : null;

            if (registry == null)
            {
                Log.Error("[CityTimelineMod] Prior UI binding cleanup failed: registry unavailable.");
                return false;
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
                            complete = false;
                            Log.Error("[CityTimelineMod] Prior UI binding field inspection failed: " + ex);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                complete = false;
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
                    complete = false;
                    Log.Error("[CityTimelineMod] Prior UI binding removal failed: " + ex);
                }
            }

            return complete;
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

        private static bool DisableSystem(ComponentSystemBase system, string reason)
        {
            if (system == null)
                return true;

            try
            {
                system.Enabled = false;
                Log.Info("[CityTimelineMod] ECS system disabled: " + reason + ".");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("[CityTimelineMod] ECS system disable failed for " + reason + ": " + ex);
                return false;
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
                var worldOffset = terrainSystem.worldOffset;
                var worldSize = terrainSystem.worldSize;
                var expectedHalf = CityTimelineLargeMapState.HalfMapSizeMetersFloat;
                var expectedSize = CityTimelineLargeMapState.MapSizeMetersFloat;
                var largeMapBounds =
                    CityTimelineLargeMapState.Enabled &&
                    Mathf.Abs(worldOffset.x + expectedHalf) <= 1f &&
                    Mathf.Abs(worldOffset.y + expectedHalf) <= 1f &&
                    Mathf.Abs(worldSize.x - expectedSize) <= 1f &&
                    Mathf.Abs(worldSize.y - expectedSize) <= 1f;

                if (!terrainData.isCreated ||
                    !largeMapBounds ||
                    !PlayableWorldState.TryUpdate(
                        world.SequenceNumber,
                        worldOffset,
                        worldSize))
                {
                    Log.Info(
                        "[PlayableWorld] current 57 km terrain bounds unavailable; " +
                        "installed=true, initialized=false."
                    );
                }
            }
            catch (Exception ex)
            {
                Log.Error("[PlayableWorld] current terrain bounds recapture failed; initialized=false. " + ex);
            }
        }

        private static bool ValidateCurrentLargeMapWorld(World world)
        {
            try
            {
                if (world == null || !world.IsCreated)
                    return false;

                var terrainSystem = world.GetExistingSystemManaged<TerrainSystem>();
                if (terrainSystem == null)
                    return true;

                var terrainData = terrainSystem.GetHeightData(false);
                if (!terrainData.isCreated)
                    return true;

                var offset = terrainSystem.worldOffset;
                var size = terrainSystem.worldSize;
				var boundsPending =
    Mathf.Abs(offset.x) <= 0.001f &&
    Mathf.Abs(offset.y) <= 0.001f &&
    Mathf.Abs(size.x) <= 0.001f &&
    Mathf.Abs(size.y) <= 0.001f;

if (boundsPending)
{
    Log.Info(
        "[LargeMap] active TerrainSystem bounds are not initialized yet; " +
        "validation deferred until FinalizeTerrainData."
    );
    return true;
}
                var expectedHalf = CityTimelineLargeMapState.HalfMapSizeMetersFloat;
                var expectedSize = CityTimelineLargeMapState.MapSizeMetersFloat;
                var valid =
                    Mathf.Abs(offset.x + expectedHalf) <= 1f &&
                    Mathf.Abs(offset.y + expectedHalf) <= 1f &&
                    Mathf.Abs(size.x - expectedSize) <= 1f &&
                    Mathf.Abs(size.y - expectedSize) <= 1f;

                if (!valid)
                {
                    Log.Error(
                        "[LargeMap] active terrain is already initialized with incompatible " +
                        "bounds. offset=" + offset + ", size=" + size + "."
                    );
                }

                return valid;
            }
            catch (Exception ex)
            {
                Log.Error("[LargeMap] active World validation failed: " + ex);
                return false;
            }
        }

        private void AbortRuntimeStartup(string reason)
        {
            CloseRuntimeAuthorization();

            var globalUiGateClean =
                CityTimelineMod.UI.CityTimelineUISystem.CloseRuntimeGate();
            var ownedUiGateClean = _uiSystem == null ||
                CityTimelineMod.UI.CityTimelineUISystem.CloseRuntimeGate(_uiSystem);
            var uiSystemDisabled =
                DisableSystem(_uiSystem, "startup rollback UI");
            var uiClean =
                globalUiGateClean && ownedUiGateClean && uiSystemDisabled;
            var ecsClean = DeactivateOwnedRuntimeSystems("startup rollback");
            var overlayClean = SafeUninstallOverlay("startup rollback");
            var runtimeControllerClean =
                CityTimelineRuntimeController.Uninstall();
            GeoBundleBootstrap.Reset(true);
            PlayableWorldState.Disable();
            CityTimelineLargeMapState.Disable();
            var playableWorldOwnerClean =
                SafeUninstallPlayableWorld("startup rollback");
            var largeMapOwnerClean =
                TryCoordinatedLargeMapPatcherTeardown("startup rollback");
            var largeMapScopeEndReason =
                "coordinated LargeMap teardown did not complete";
            var largeMapScopeEnded = false;

            if (largeMapOwnerClean)
            {
                largeMapScopeEnded = SafeEndLargeMapRuntimeWorld(
                    _world,
                    _lifecycleGeneration,
                    "startup rollback",
                    out largeMapScopeEndReason
                );
            }
            var supersededLifecycleRegistrationsClean =
                TryCleanupSupersededLifecycleRegistrations();
            var optionsClean = SafeUnregisterOptions();
            var localizationClean = RemoveLocalizationSources();
            var abortComplete =
                uiClean &&
                ecsClean &&
                overlayClean &&
                runtimeControllerClean &&
                playableWorldOwnerClean &&
                largeMapOwnerClean &&
                largeMapScopeEnded &&
                supersededLifecycleRegistrationsClean &&
                optionsClean &&
                localizationClean;

            if (!abortComplete)
            {
                Log.Error(
                    "[CityTimelineMod] startup rollback retained unverified " +
                    "state: uiClean=" + uiClean +
                    ", ecsClean=" + ecsClean +
                    ", overlayClean=" + overlayClean +
                    ", runtimeControllerClean=" + runtimeControllerClean +
                    ", playableWorldOwnerClean=" + playableWorldOwnerClean +
                    ", largeMapOwnerClean=" + largeMapOwnerClean +
                    ", largeMapScopeEnded=" + largeMapScopeEnded +
                    ", supersededLifecycleRegistrationsClean=" +
                        supersededLifecycleRegistrationsClean +
                    ", optionsClean=" + optionsClean +
                    ", localizationClean=" + localizationClean +
                    ", largeMapReason=" + largeMapScopeEndReason
                );
            }
            Log.Error(
                "[CityTimelineMod] runtime gate closed: " + reason +
                "; rollbackComplete=" + abortComplete + "."
            );
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

        private bool RemoveLocalizationSources()
        {
            var complete = true;

            if (_frLocaleRegistered)
            {
                if (_frLocale == null)
                {
                    Log.Error(
                        "[CityTimelineMod] French localisation cleanup cannot " +
                        "identify its registered source."
                    );
                    complete = false;
                }
                else
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
                        complete = false;
                    }
                }
            }

            if (_enLocaleRegistered)
            {
                if (_enLocale == null)
                {
                    Log.Error(
                        "[CityTimelineMod] English localisation cleanup cannot " +
                        "identify its registered source."
                    );
                    complete = false;
                }
                else
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
                        complete = false;
                    }
                }
            }

            if (!_frLocaleRegistered)
                _frLocale = null;
            if (!_enLocaleRegistered)
                _enLocale = null;

            return complete;
        }

        private bool SafeUnregisterOptions()
        {
            if (_settings == null)
                return true;

            try
            {
                var settings = _settings;
                settings.UnregisterInOptionsUI();

                if (ReferenceEquals(Settings, settings))
                    Settings = null;

                _settings = null;
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("[CityTimelineMod] Options cleanup failed: " + ex);
                return false;
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

        private static bool TryCoordinatedLargeMapPatcherTeardown(string stage)
        {
            var patchers = new List<LargeMapPatcherLifecycleApi>();
            var contractComplete = true;
            var identityDiscoveryComplete = true;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!string.Equals(
                        assembly.GetName().Name,
                        typeof(Mod).Assembly.GetName().Name,
                        StringComparison.Ordinal
                    ))
                {
                    continue;
                }

                Type patcherType;
                try
                {
                    patcherType = assembly.GetType(
                        "CityTimelineMod.LargeMap.CityTimelineLargeMapPatcher",
                        false
                    );
                }
                catch (Exception ex)
                {
                    Log.Error(
                        "[CityTimelineMod] LargeMap patcher lookup failed during " +
                        stage + ": " + ex
                    );
                    identityDiscoveryComplete = false;
                    continue;
                }

                if (patcherType == null)
                    continue;

                var api = new LargeMapPatcherLifecycleApi
                {
                    PatcherType = patcherType
                };

                var apiComplete =
                    TryGetLargeMapLifecycleMethod(
                        patcherType,
                        "CanUninstallWithoutMutation",
                        stage,
                        out api.Probe
                    ) &
                    TryGetLargeMapLifecycleMethod(
                        patcherType,
                        "TryRollbackForCoordinatedUninstall",
                        stage,
                        out api.Rollback
                    ) &
                    TryGetLargeMapLifecycleMethod(
                        patcherType,
                        "TryRemoveSharedHarmonyOwner",
                        stage,
                        out api.RemoveSharedOwner
                    ) &
                    TryGetLargeMapLifecycleMethod(
                        patcherType,
                        "CommitCoordinatedUnpatch",
                        stage,
                        out api.Commit
                    ) &
                    TryGetLargeMapQuarantineMethod(
                        patcherType,
                        stage,
                        out api.EnterQuarantine
                    );

                patchers.Add(api);
                contractComplete = apiComplete && contractComplete;
            }

            contractComplete = identityDiscoveryComplete && contractComplete;

            if (patchers.Count == 0)
            {
                Log.Error(
                    "[CityTimelineMod] No LargeMap patcher identity was available " +
                    "for coordinated teardown during " + stage + "."
                );
                return false;
            }

            if (!contractComplete)
            {
                EnterLargeMapRuntimeQuarantine(
                    patchers,
                    "coordinated teardown contract is incomplete during " + stage
                );
                Log.Error(
                    "[CityTimelineMod] LargeMap teardown is quarantined because " +
                    "at least one loaded assembly lacks the complete lifecycle " +
                    "contract. A process restart is required; no rollback or " +
                    "Harmony unpatch was attempted."
                );
                return false;
            }

            // Phase 1 is deliberately non-mutating. Every loaded identity must
            // prove that its World can be restored before any rollback begins.
            foreach (var patcher in patchers)
            {
                string reason;
                if (!InvokeLargeMapLifecyclePhase(
                        patcher,
                        patcher.Probe,
                        "probe",
                        stage,
                        out reason
                    ))
                {
                    EnterLargeMapRuntimeQuarantine(
                        patchers,
                        "coordinated probe failed during " + stage + ": " + reason
                    );
                    return false;
                }
            }

            // Phase 2 restores each assembly's owned runtime mutations while all
            // Harmony patches remain installed. A failure leaves the shared owner
            // quarantined and makes the complete operation safely retentable.
            foreach (var patcher in patchers)
            {
                string reason;
                if (!InvokeLargeMapLifecyclePhase(
                        patcher,
                        patcher.Rollback,
                        "rollback",
                        stage,
                        out reason
                    ))
                {
                    EnterLargeMapRuntimeQuarantine(
                        patchers,
                        "coordinated rollback failed during " + stage + ": " + reason
                    );
                    return false;
                }
            }

            // Every assembly uses the same Harmony ID. Remove that shared owner
            // exactly once, preferring the current assembly's verified API.
            var ownerRemover = patchers[0];
            foreach (var patcher in patchers)
            {
                if (patcher.PatcherType.Assembly == typeof(Mod).Assembly)
                {
                    ownerRemover = patcher;
                    break;
                }
            }

            string ownerRemovalReason;
            if (!InvokeLargeMapLifecyclePhase(
                    ownerRemover,
                    ownerRemover.RemoveSharedOwner,
                    "shared owner removal",
                    stage,
                    out ownerRemovalReason
                ))
            {
                EnterLargeMapRuntimeQuarantine(
                    patchers,
                    "shared Harmony owner removal failed during " + stage + ": " +
                        ownerRemovalReason
                );
                return false;
            }

            // Phase 4 verifies the owner absence and clears the local static state
            // in every loaded assembly only after the single global unpatch.
            foreach (var patcher in patchers)
            {
                string reason;
                if (!InvokeLargeMapLifecyclePhase(
                        patcher,
                        patcher.Commit,
                        "commit",
                        stage,
                        out reason
                    ))
                {
                    EnterLargeMapRuntimeQuarantine(
                        patchers,
                        "coordinated commit failed during " + stage + ": " + reason
                    );
                    return false;
                }
            }

            Log.Info(
                "[CityTimelineMod] Coordinated LargeMap teardown verified during " +
                stage + "; patcherIdentities=" + patchers.Count + "."
            );

            return true;
        }

        private static bool TryGetLargeMapLifecycleMethod(
            Type patcherType,
            string methodName,
            string stage,
            out MethodInfo method)
        {
            method = null;

            try
            {
                method = patcherType.GetMethod(
                    methodName,
                    BindingFlags.Static |
                    BindingFlags.Public |
                    BindingFlags.NonPublic,
                    null,
                    new[] { typeof(string).MakeByRefType() },
                    null
                );
            }
            catch (Exception ex)
            {
                Log.Error(
                    "[CityTimelineMod] LargeMap lifecycle method lookup failed " +
                    "during " + stage + " for " + patcherType.Assembly.FullName +
                    ", method=" + methodName + ": " + ex
                );
                return false;
            }

            var parameters = method != null ? method.GetParameters() : null;
            if (method == null ||
                method.ReturnType != typeof(bool) ||
                parameters == null ||
                parameters.Length != 1 ||
                !parameters[0].IsOut ||
                parameters[0].ParameterType != typeof(string).MakeByRefType())
            {
                Log.Error(
                    "[CityTimelineMod] LargeMap patcher lacks the required " +
                    "coordinated teardown contract during " + stage + ": " +
                    patcherType.Assembly.FullName + ", method=" + methodName + "."
                );
                method = null;
                return false;
            }

            return true;
        }

        private static bool TryGetLargeMapQuarantineMethod(
            Type patcherType,
            string stage,
            out MethodInfo method)
        {
            method = null;

            try
            {
                method = patcherType.GetMethod(
                    "EnterRuntimeQuarantine",
                    BindingFlags.Static |
                    BindingFlags.Public |
                    BindingFlags.NonPublic,
                    null,
                    new[] { typeof(string) },
                    null
                );
            }
            catch (Exception ex)
            {
                Log.Error(
                    "[CityTimelineMod] LargeMap quarantine method lookup failed " +
                    "during " + stage + " for " + patcherType.Assembly.FullName +
                    ": " + ex
                );
                return false;
            }

            if (method == null || method.ReturnType != typeof(bool))
            {
                Log.Error(
                    "[CityTimelineMod] LargeMap patcher lacks EnterRuntimeQuarantine " +
                    "during " + stage + ": " + patcherType.Assembly.FullName + "."
                );
                method = null;
                return false;
            }

            return true;
        }

        private static bool EnterLargeMapRuntimeQuarantine(
            IList<LargeMapPatcherLifecycleApi> patchers,
            string reason)
        {
            var complete = true;

            foreach (var patcher in patchers)
            {
                if (patcher.EnterQuarantine == null)
                {
                    complete = false;
                    continue;
                }

                try
                {
                    var result = patcher.EnterQuarantine.Invoke(
                        null,
                        new object[] { reason }
                    );
                    if (!(result is bool) || !(bool)result)
                    {
                        Log.Error(
                            "[CityTimelineMod] LargeMap quarantine was not " +
                            "confirmed by " +
                            patcher.PatcherType.Assembly.FullName + "."
                        );
                        complete = false;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(
                        "[CityTimelineMod] LargeMap quarantine failed for " +
                        patcher.PatcherType.Assembly.FullName + ": " + ex
                    );
                    complete = false;
                }
            }

            return complete;
        }

        private static bool InvokeLargeMapLifecyclePhase(
            LargeMapPatcherLifecycleApi patcher,
            MethodInfo method,
            string phase,
            string stage,
            out string reason)
        {
            reason = null;

            try
            {
                var arguments = new object[] { null };
                var result = method.Invoke(null, arguments);
                reason = arguments[0] as string;

                if (!(result is bool) || !(bool)result)
                {
                    Log.Error(
                        "[CityTimelineMod] LargeMap coordinated " + phase +
                        " refused during " + stage + " for " +
                        patcher.PatcherType.Assembly.FullName + ": " +
                        (string.IsNullOrWhiteSpace(reason) ? "no reason supplied" : reason)
                    );
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                reason = ex.ToString();
                Log.Error(
                    "[CityTimelineMod] LargeMap coordinated " + phase +
                    " failed during " + stage + " for " +
                    patcher.PatcherType.Assembly.FullName + ": " + ex
                );
                return false;
            }
        }

        private static bool SafeUninstallOverlay(string stage)
        {
            try
            {
                var complete = GeoDebugOverlay.Uninstall();
                if (!complete)
                    Log.Error("[CityTimelineMod] Overlay " + stage + " cleanup remains pending and will be retried.");
                return complete;
            }
            catch (Exception ex)
            {
                Debug.LogError("[CityTimelineMod] Overlay " + stage + " cleanup failed: " + ex);
                Log.Error(ex);
                return false;
            }
        }
    }
}
