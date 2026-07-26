using System;
using Colossal.UI.Binding;
using Game.Input;
using Game.SceneFlow;
using Game.UI;
using UnityEngine.InputSystem;
using CityTimelineMod.Util;

namespace CityTimelineMod.Systems
{
    /// <summary>
    /// Gère l'état visible du HUD React/CoHTML.
    ///
    /// Le HUD IMGUI Alt+H reste indépendant.
    /// Le bouton React CTM et le raccourci Alt+Z utilisent
    /// la même source d'état côté C#.
    /// </summary>
    internal sealed partial class CityTimelineUISystem : UISystemBase
    {
        internal const string BindingGroup = "CityTimelineMod";

        private ValueBinding<bool> _visibleBinding;
        private TriggerBinding _toggleBinding;
        private TriggerBinding _closeBinding;
        private ProxyAction _toggleAction;

        protected override void OnCreate()
        {
            base.OnCreate();
            DeactivateForMigration();
            Log.Info("Legacy CityTimelineMod UI system kept inactive for migration.");
        }

        protected override void OnDestroy()
        {
            DeactivateForMigration();
            base.OnDestroy();
        }

        internal void DeactivateForMigration()
        {
            Enabled = false;

            try
            {
                if (!CityTimelineMod.UI.CityTimelineUISystem.HasActiveRuntimeGate() &&
                    _visibleBinding != null &&
                    _visibleBinding.value)
                {
                    _visibleBinding.Update(false);
                }
            }
            catch (Exception ex)
            {
                Log.Error("Legacy CityTimelineMod HUD close failed: " + ex);
            }

            if (_toggleAction != null)
            {
                var callbackDetached = false;
                var actionReleased = false;
                try
                {
                    // Event removal is idempotent and removes this instance's
                    // exact callback without disturbing the modern system.
                    _toggleAction.onInteraction -= OnToggleActionInteraction;
                    callbackDetached = true;
                }
                catch (Exception ex)
                {
                    Log.Error("Legacy CityTimelineMod input detach failed: " + ex);
                }

                try
                {
                    if (!CityTimelineMod.UI.CityTimelineUISystem.OwnsRuntimeAction(_toggleAction))
                        _toggleAction.shouldBeEnabled = false;
                    actionReleased = callbackDetached;
                }
                catch (Exception ex)
                {
                    Log.Error("Legacy CityTimelineMod input disable failed: " + ex);
                }

                if (actionReleased)
                    _toggleAction = null;
            }

            if (TryRemoveBinding(_visibleBinding))
                _visibleBinding = null;
            if (TryRemoveBinding(_toggleBinding))
                _toggleBinding = null;
            if (TryRemoveBinding(_closeBinding))
                _closeBinding = null;
        }

        private static bool TryRemoveBinding(IBinding binding)
        {
            if (binding == null)
                return true;

            try
            {
                var gameManager = GameManager.instance;
                var registry = gameManager == null || gameManager.userInterface == null
                    ? null
                    : gameManager.userInterface.bindings;
                if (registry == null)
                    return false;

                registry.RemoveBinding(binding);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("Legacy CityTimelineMod binding detach failed: " + ex);
                return false;
            }
        }

        private void OnToggleActionInteraction(
            ProxyAction action,
            InputActionPhase phase
        )
        {
            if (!Mod.RuntimeEnabled || !Enabled)
                return;

            if (phase != InputActionPhase.Performed)
            {
                return;
            }

            if (
                InputManager.instance != null &&
                InputManager.instance.hasInputFieldFocus
            )
            {
                return;
            }

            ToggleHud();
        }

        private void ToggleHud()
        {
            if (!Mod.RuntimeEnabled || !Enabled || _visibleBinding == null)
                return;

            bool next = !_visibleBinding.value;

            _visibleBinding.Update(next);

            Log.Info(
                "HUD CoHTML visible=" + next
            );
        }

        private void CloseHud()
        {
            if (!Mod.RuntimeEnabled || !Enabled || _visibleBinding == null || !_visibleBinding.value)
            {
                return;
            }

            _visibleBinding.Update(false);

            Log.Info(
                "HUD CoHTML visible=false"
            );
        }
    }
}
