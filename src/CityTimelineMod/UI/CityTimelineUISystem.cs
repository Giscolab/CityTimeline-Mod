using System;
using System.Collections.Generic;
using Colossal.Serialization.Entities;
using Colossal.UI.Binding;
using Game;
using Game.Input;
using Game.SceneFlow;
using Game.UI;
using UnityEngine.InputSystem;
using CityTimelineMod.Options;
using CityTimelineMod.Util;

namespace CityTimelineMod.UI
{
    /// <summary>
    /// Single source of truth for the React/CoHTML HUD visibility.
    ///
    /// - Alt+Z is a real CS2 input action declared in CityTimelineSettings.
    /// - The CTM React button sets the requested state through a typed TriggerBinding.
    /// - Existing IMGUI HUDs and their shortcuts are not modified.
    /// </summary>
    internal sealed partial class CityTimelineUISystem : UISystemBase
    {
        internal const string BindingGroup = "CityTimelineMod";

        public override GameMode gameMode => GameMode.Game;

        private ValueBinding<bool> _visibleBinding;
        private ValueBinding<bool> _runtimeAvailableBinding;
        private TriggerBinding _toggleBinding;
        private TriggerBinding<bool> _setVisibleBinding;
        private TriggerBinding _closeBinding;
        private ProxyAction _toggleAction;
        private bool _toggleActionSubscribed;
        private bool _runtimeGateOpen;
        private readonly List<IBinding> _runtimeBindings = new List<IBinding>();
        private readonly HashSet<IBinding> _attachedRuntimeBindings = new HashSet<IBinding>();
        private static readonly HashSet<CityTimelineUISystem> _pendingCleanupInstances =
            new HashSet<CityTimelineUISystem>();
        private static CityTimelineUISystem _instance;
        private static CityTimelineUISystem _latestCreatedInstance;

        internal bool RuntimeCallbacksAllowed =>
            _runtimeGateOpen &&
            Mod.RuntimeEnabled &&
            Enabled &&
            ReferenceEquals(_instance, this);

        protected override void OnCreate()
        {
            base.OnCreate();
            _latestCreatedInstance = this;

            try
            {
                _visibleBinding = new ValueBinding<bool>(
                    BindingGroup,
                    "cohtmlHudVisible",
                    false
                );

                _runtimeAvailableBinding = new ValueBinding<bool>(
                    BindingGroup,
                    "runtimeAvailable",
                    false
                );

                _toggleBinding = new TriggerBinding(
                    BindingGroup,
                    "toggleCohtmlHud",
                    ToggleHud
                );

                _setVisibleBinding = new TriggerBinding<bool>(
                    BindingGroup,
                    "setCohtmlHudVisible",
                    SetHudVisible
                );

                _closeBinding = new TriggerBinding(
                    BindingGroup,
                    "closeCohtmlHud",
                    CloseHud
                );

                AddRuntimeBinding(_visibleBinding);
                AddRuntimeBinding(_runtimeAvailableBinding);
                AddRuntimeBinding(_toggleBinding);
                AddRuntimeBinding(_setVisibleBinding);
                AddRuntimeBinding(_closeBinding);
                CreateOverlayLayerBindings();
                CreateRailwayBindings();
                CreateStatisticsBindings();
            }
            catch (Exception ex)
            {
                Log.Error("CityTimelineMod CoHTML UI creation failed; rolling back owned bindings. " + ex);
                CloseRuntimeGate(this);
                throw;
            }

            // Registration is deliberately inert. Mod.OnLoad opens the gate only
            // after the single startup decision has authorized the runtime.
            Enabled = false;
            if (!DetachRuntimeBindings())
            {
                _pendingCleanupInstances.Add(this);
                Log.Error(
                    "CityTimelineMod CoHTML UI creation left binding cleanup pending."
                );
            }
            Log.Info("CityTimelineMod CoHTML UI system created with its runtime gate closed.");
        }


        protected override void OnGamePreload(Purpose purpose, GameMode mode)
        {
            // Never carry an opened gameplay HUD into the main menu or editor.
            if (ReferenceEquals(_instance, this) &&
                (mode & GameMode.Game) == 0 &&
                _visibleBinding != null && _visibleBinding.value)
            {
                ForceCloseHud();
                Log.Info("CityTimelineMod CoHTML HUD closed outside gameplay.");
            }

            // UISystemBase enables this system only when its declared gameMode
            // matches the mode being loaded.
            base.OnGamePreload(purpose, mode);
        }

        protected override void OnDestroy()
        {
            // Close this exact World instance. A delayed OnDestroy from an old
            // World must never close the gate owned by a newer instance.
            var cleanupComplete = CloseRuntimeGate(this);
            base.OnDestroy();

            if (cleanupComplete)
            {
                _attachedRuntimeBindings.Clear();
                _runtimeBindings.Clear();
            }
            else
            {
                Log.Error(
                    "CityTimelineMod CoHTML OnDestroy cleanup is incomplete; " +
                    "binding and input ownership evidence was retained for retry."
                );
            }
        }

        internal static bool OpenRuntimeGate()
        {
            return OpenRuntimeGate(_latestCreatedInstance);
        }

        internal static bool OpenRuntimeGate(CityTimelineUISystem instance)
        {
            if (instance == null)
            {
                Log.Error("CityTimelineMod CoHTML runtime gate open failed: no UI system instance.");
                return false;
            }

            if (!Mod.RuntimeEnabled || Mod.Settings == null)
            {
                CloseRuntimeGate(instance);
                CloseRuntimeGate();
                Log.Info("CityTimelineMod CoHTML runtime gate remains closed by startup policy.");
                return false;
            }

            if (ReferenceEquals(_instance, instance) && instance._runtimeGateOpen)
                return true;

            var previous = _instance;
            if (previous != null && !ReferenceEquals(previous, instance))
            {
                if (!CloseRuntimeGate(previous))
                {
                    Log.Error(
                        "CityTimelineMod CoHTML runtime gate open blocked: " +
                        "the previous World instance still owns pending UI resources."
                    );
                    return false;
                }
            }

            _instance = instance;
            _latestCreatedInstance = instance;

            try
            {
                if (!instance.AttachRuntimeBindings())
                    throw new InvalidOperationException("one or more CoHTML bindings could not be attached");

                if (!instance.AttachToggleAction())
                    throw new InvalidOperationException("the Alt+Z input action could not be attached");

                instance._runtimeGateOpen = true;
                instance.Enabled = true;
                instance.ResetBindingSignatures();
                instance.SyncOverlayLayerBindings();
                instance.SyncRailwayBindings();
                instance.SyncStatisticsBindings();
                UpdateBinding(instance._runtimeAvailableBinding, true);

                Log.Info("CityTimelineMod CoHTML runtime gate opened.");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("CityTimelineMod CoHTML runtime gate open failed: " + ex);
                CloseRuntimeGate(instance);
                return false;
            }
        }

        internal static bool CloseRuntimeGate()
        {
            var instances = new HashSet<CityTimelineUISystem>(
                _pendingCleanupInstances
            );

            if (_instance != null)
                instances.Add(_instance);
            if (_latestCreatedInstance != null)
                instances.Add(_latestCreatedInstance);

            var complete = true;
            foreach (var instance in instances)
                complete = CloseRuntimeGate(instance) && complete;

            return complete;
        }

        internal static bool CloseRuntimeGate(CityTimelineUISystem instance)
        {
            if (instance == null)
                return true;

            var activeInstance = _instance;
            var isActiveInstance = ReferenceEquals(activeInstance, instance);
            var disableAction =
                isActiveInstance ||
                activeInstance == null ||
                !ReferenceEquals(activeInstance._toggleAction, instance._toggleAction);
            var publishClosedState = isActiveInstance || activeInstance == null;
            var complete = false;

            try
            {
                complete = instance.CloseRuntimeGateInternal(
                    disableAction,
                    publishClosedState
                );
            }
            catch (Exception ex)
            {
                Log.Error("CityTimelineMod CoHTML runtime gate close failed: " + ex);
            }

            if (!complete)
            {
                _pendingCleanupInstances.Add(instance);
                return false;
            }

            _pendingCleanupInstances.Remove(instance);

            // Only the exact instance that was successfully cleaned may clear
            // these static selectors. A delayed old-World OnDestroy must never
            // close or replace a newer active UI instance.
            if (isActiveInstance && ReferenceEquals(_instance, instance))
                _instance = null;

            if (ReferenceEquals(_latestCreatedInstance, instance))
                _latestCreatedInstance = null;

            return true;
        }

        internal static bool OwnsRuntimeAction(ProxyAction action)
        {
            var instance = _instance;
            return action != null &&
                instance != null &&
                instance._runtimeGateOpen &&
                Mod.RuntimeEnabled &&
                ReferenceEquals(instance._toggleAction, action);
        }

        internal static bool HasActiveRuntimeGate()
        {
            var instance = _instance;
            return instance != null &&
                instance._runtimeGateOpen &&
                Mod.RuntimeEnabled;
        }

        private void AddRuntimeBinding(IBinding binding)
        {
            if (binding == null)
                return;

            _runtimeBindings.Add(binding);
            _attachedRuntimeBindings.Add(binding);

            try
            {
                AddBinding(binding);
            }
            catch
            {
                // AddBinding may have reached the registry before throwing.
                // The exact binding is already tracked so rollback can remove it.
                DetachRuntimeBindings();
                throw;
            }
        }

        private bool AttachRuntimeBindings()
        {
            var registry = GetBindingRegistry();
            if (registry == null)
            {
                Log.Error("CityTimelineMod CoHTML bindings cannot be attached: registry unavailable.");
                return false;
            }

            // Normalize any uncertain residue from a previous partial teardown.
            // RemoveBinding is owner-object scoped and tolerates absence.
            var normalized = true;
            foreach (var binding in _runtimeBindings)
            {
                if (!_attachedRuntimeBindings.Contains(binding))
                    continue;

                try
                {
                    registry.RemoveBinding(binding);
                    _attachedRuntimeBindings.Remove(binding);
                }
                catch (Exception ex)
                {
                    normalized = false;
                    Log.Error("CityTimelineMod CoHTML binding normalization failed: " + ex);
                }
            }

            if (!normalized)
                return false;

            var complete = true;
            foreach (var binding in _runtimeBindings)
            {
                _attachedRuntimeBindings.Add(binding);
                try
                {
                    registry.AddBinding(binding);
                }
                catch (Exception ex)
                {
                    complete = false;
                    Log.Error("CityTimelineMod CoHTML binding attach failed: " + ex);
                }
            }

            return complete && _attachedRuntimeBindings.Count == _runtimeBindings.Count;
        }

        private bool DetachRuntimeBindings()
        {
            var registry = GetBindingRegistry();
            if (registry == null)
            {
                if (_attachedRuntimeBindings.Count > 0)
                    Log.Error("CityTimelineMod CoHTML bindings cannot be detached: registry unavailable.");
                return _attachedRuntimeBindings.Count == 0;
            }

            var complete = true;
            foreach (var binding in _runtimeBindings)
            {
                if (!_attachedRuntimeBindings.Contains(binding))
                    continue;

                try
                {
                    registry.RemoveBinding(binding);
                    _attachedRuntimeBindings.Remove(binding);
                }
                catch (Exception ex)
                {
                    complete = false;
                    Log.Error("CityTimelineMod CoHTML binding detach failed: " + ex);
                }
            }

            return complete && _attachedRuntimeBindings.Count == 0;
        }

        private static IBindingRegistry GetBindingRegistry()
        {
            var gameManager = GameManager.instance;
            return gameManager == null || gameManager.userInterface == null
                ? null
                : gameManager.userInterface.bindings;
        }

        private bool AttachToggleAction()
        {
            if (!ReleaseToggleAction(true))
                return false;

            if (InputManager.instance == null ||
                !InputManager.instance.TryFindAction(
                    Mod.Settings.KeyBindingToggleCohtmlHud,
                    out _toggleAction
                ) ||
                _toggleAction == null)
            {
                // The HUD button remains usable even if the optional shortcut
                // could not be resolved at this point in the game lifecycle.
                Log.Error("CityTimelineMod CoHTML Alt+Z action was not found; HUD bindings remain available.");
                return true;
            }

            _toggleAction.onInteraction += OnToggleActionInteraction;
            _toggleActionSubscribed = true;
            _toggleAction.shouldBeEnabled = true;
            return true;
        }

        private bool ReleaseToggleAction(bool disableAction)
        {
            var action = _toggleAction;
            if (action == null)
                return true;

            var complete = true;

            if (_toggleActionSubscribed)
            {
                try
                {
                    action.onInteraction -= OnToggleActionInteraction;
                    _toggleActionSubscribed = false;
                }
                catch (Exception ex)
                {
                    complete = false;
                    Log.Error("CityTimelineMod CoHTML input callback detach failed: " + ex);
                }
            }

            if (disableAction)
            {
                try
                {
                    action.shouldBeEnabled = false;
                }
                catch (Exception ex)
                {
                    complete = false;
                    Log.Error("CityTimelineMod CoHTML input disable failed: " + ex);
                }
            }

            if (complete)
                _toggleAction = null;

            return complete;
        }

        private bool CloseRuntimeGateInternal(
            bool disableAction,
            bool publishClosedState)
        {
            _runtimeGateOpen = false;
            Enabled = false;
            var complete = true;

            if (publishClosedState)
            {
                try
                {
                    ForceCloseHud();
                }
                catch (Exception ex)
                {
                    complete = false;
                    Log.Error("CityTimelineMod CoHTML HUD close failed: " + ex);
                }

                try
                {
                    UpdateBinding(_runtimeAvailableBinding, false);
                }
                catch (Exception ex)
                {
                    complete = false;
                    Log.Error("CityTimelineMod CoHTML availability reset failed: " + ex);
                }
            }

            complete = ReleaseToggleAction(disableAction) && complete;
            complete = DetachRuntimeBindings() && complete;
            return complete;
        }

        private void ForceCloseHud()
        {
            if (_visibleBinding != null && _visibleBinding.value)
                _visibleBinding.Update(false);
        }

        private void ResetBindingSignatures()
        {
            _lastRailwayBindingSignature = null;
            _lastOverlayLayerBindingSignature = null;
            _lastOverlaySublayersJson = null;
            _lastBundleStatsJson = null;
        }

        private void OnToggleActionInteraction(
            ProxyAction action,
            InputActionPhase phase
        )
        {
            if (!RuntimeCallbacksAllowed)
                return;

            if (phase != InputActionPhase.Performed)
            {
                return;
            }

            if (InputManager.instance != null && InputManager.instance.hasInputFieldFocus)
            {
                return;
            }

            ToggleHud();
        }

        private void ToggleHud()
        {
            if (!RuntimeCallbacksAllowed || _visibleBinding == null)
            {
                return;
            }

            SetHudVisible(!_visibleBinding.value);
        }

        private void SetHudVisible(bool visible)
        {
            if (!RuntimeCallbacksAllowed || _visibleBinding == null)
            {
                return;
            }

            if (_visibleBinding.value == visible)
            {
                return;
            }

            _visibleBinding.Update(visible);

            if (visible)
            {
                // Opening the panel performs one immediate refresh; periodic
                // polling remains suspended whenever the panel is closed.
                _lastRailwayBindingSignature = null;
                _lastOverlayLayerBindingSignature = null;
                _lastOverlaySublayersJson = null;
                SyncOverlayLayerBindings();
                SyncRailwayBindings();
                SyncStatisticsBindings();
                _nextRailwayBindingSyncTime = UnityEngine.Time.unscaledTime + 1f;
            }

            Log.Info("CityTimelineMod CoHTML HUD visible=" + visible);
        }

        private void CloseHud()
        {
            if (!RuntimeCallbacksAllowed)
                return;

            SetHudVisible(false);
        }
    }
}
