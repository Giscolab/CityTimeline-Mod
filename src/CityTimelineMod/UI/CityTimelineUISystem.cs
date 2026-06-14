using Colossal.Serialization.Entities;
using Colossal.UI.Binding;
using Game;
using Game.Input;
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
        private TriggerBinding _toggleBinding;
        private TriggerBinding<bool> _setVisibleBinding;
        private TriggerBinding _closeBinding;
        private ProxyAction _toggleAction;

        protected override void OnCreate()
        {
            base.OnCreate();

            _visibleBinding = new ValueBinding<bool>(
                BindingGroup,
                "cohtmlHudVisible",
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

            AddBinding(_visibleBinding);
            AddBinding(_toggleBinding);
            AddBinding(_setVisibleBinding);
            AddBinding(_closeBinding);

            if (!InputManager.instance.TryFindAction(
                Mod.Settings.KeyBindingToggleCohtmlHud,
                out _toggleAction
            ) || _toggleAction == null)
            {
                Log.Error("CityTimelineMod CoHTML Alt+Z action was not found.");
                return;
            }

            _toggleAction.onInteraction += OnToggleActionInteraction;
            _toggleAction.shouldBeEnabled = true;

            Log.Info("CityTimelineMod CoHTML UI system created for gameplay mode.");
        }


        protected override void OnGamePreload(Purpose purpose, GameMode mode)
        {
            // Never carry an opened gameplay HUD into the main menu or editor.
            if ((mode & GameMode.Game) == 0 &&
                _visibleBinding != null &&
                _visibleBinding.value)
            {
                _visibleBinding.Update(false);
                Log.Info("CityTimelineMod CoHTML HUD closed outside gameplay.");
            }

            // UISystemBase enables this system only when its declared gameMode
            // matches the mode being loaded.
            base.OnGamePreload(purpose, mode);
        }

        protected override void OnDestroy()
        {
            if (_toggleAction != null)
            {
                _toggleAction.onInteraction -= OnToggleActionInteraction;
                _toggleAction.shouldBeEnabled = false;
            }

            base.OnDestroy();
        }

        private void OnToggleActionInteraction(
            ProxyAction action,
            InputActionPhase phase
        )
        {
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
            if (!Enabled)
            {
                return;
            }

            SetHudVisible(!_visibleBinding.value);
        }

        private void SetHudVisible(bool visible)
        {
            if (!Enabled)
            {
                return;
            }

            if (_visibleBinding.value == visible)
            {
                return;
            }

            _visibleBinding.Update(visible);
            Log.Info("CityTimelineMod CoHTML HUD visible=" + visible);
        }

        private void CloseHud()
        {
            SetHudVisible(false);
        }
    }
}
