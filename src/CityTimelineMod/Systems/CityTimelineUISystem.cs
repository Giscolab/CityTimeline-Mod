using Colossal.UI.Binding;
using Game.Input;
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

            _closeBinding = new TriggerBinding(
                BindingGroup,
                "closeCohtmlHud",
                CloseHud
            );

            AddBinding(_visibleBinding);
            AddBinding(_toggleBinding);
            AddBinding(_closeBinding);

            if (
                Mod.Settings == null ||
                !InputManager.instance.TryFindAction(
                    Mod.Settings.KeyBindingToggleCohtmlHud,
                    out _toggleAction
                ) ||
                _toggleAction == null
            )
            {
                Log.Error(
                    "Action Alt+Z du HUD CoHTML introuvable."
                );

                return;
            }

            _toggleAction.onInteraction +=
                OnToggleActionInteraction;

            _toggleAction.shouldBeEnabled = true;

            /*
             * Aucun OnUpdate par frame n'est nécessaire.
             * Les changements proviennent des bindings et
             * de l'événement ProxyAction.
             */
            Enabled = false;

            Log.Info(
                "Système UI CoHTML créé. Action Alt+Z activée."
            );
        }

        protected override void OnDestroy()
        {
            if (_toggleAction != null)
            {
                _toggleAction.onInteraction -=
                    OnToggleActionInteraction;

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
            bool next = !_visibleBinding.value;

            _visibleBinding.Update(next);

            Log.Info(
                "HUD CoHTML visible=" + next
            );
        }

        private void CloseHud()
        {
            if (!_visibleBinding.value)
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
