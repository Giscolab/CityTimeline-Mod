using Colossal.UI.Binding;
using CityTimelineMod.Rendering;
using CityTimelineMod.Rendering.Railways;
using System.Globalization;
using UnityEngine;

namespace CityTimelineMod.UI
{
    internal sealed partial class CityTimelineUISystem
    {
        private ValueBinding<bool> _railwayAvailableBinding;
        private ValueBinding<bool> _railwayVisibleBinding;
        private ValueBinding<float> _railwayOpacityBinding;
        private ValueBinding<float> _railwayThicknessBinding;
        private ValueBinding<bool> _railwayTrainVisibleBinding;
        private ValueBinding<bool> _railwayTramVisibleBinding;
        private ValueBinding<bool> _railwayLightRailVisibleBinding;
        private ValueBinding<bool> _railwaySubwayVisibleBinding;
        private ValueBinding<bool> _railwayServiceVisibleBinding;
        private ValueBinding<bool> _railwayTunnelsVisibleBinding;
        private ValueBinding<int> _railwayTrainCountBinding;
        private ValueBinding<int> _railwayTramCountBinding;
        private ValueBinding<int> _railwayLightRailCountBinding;
        private ValueBinding<int> _railwaySubwayCountBinding;
        private ValueBinding<int> _railwayServiceCountBinding;
        private ValueBinding<int> _railwayTunnelCountBinding;
        private ValueBinding<int> _railwayTotalCountBinding;
        private ValueBinding<string> _railwayStatusBinding;

        private TriggerBinding<bool> _setRailwayVisibleBinding;
        private TriggerBinding<float> _setRailwayOpacityBinding;
        private TriggerBinding<float> _setRailwayThicknessBinding;
        private TriggerBinding<bool> _setRailwayTrainVisibleBinding;
        private TriggerBinding<bool> _setRailwayTramVisibleBinding;
        private TriggerBinding<bool> _setRailwayLightRailVisibleBinding;
        private TriggerBinding<bool> _setRailwaySubwayVisibleBinding;
        private TriggerBinding<bool> _setRailwayServiceVisibleBinding;
        private TriggerBinding<bool> _setRailwayTunnelsVisibleBinding;

        private float _nextRailwayBindingSyncTime;
        private string _lastRailwayBindingSignature;

        private void CreateRailwayBindings()
        {
            _railwayAvailableBinding = new ValueBinding<bool>(BindingGroup, "railwayAvailable", false);
            _railwayVisibleBinding = new ValueBinding<bool>(BindingGroup, "railwayVisible", true);
            _railwayOpacityBinding = new ValueBinding<float>(BindingGroup, "railwayOpacity", 0.9f);
            _railwayThicknessBinding = new ValueBinding<float>(BindingGroup, "railwayThickness", 3f);
            _railwayTrainVisibleBinding = new ValueBinding<bool>(BindingGroup, "railwayTrainVisible", true);
            _railwayTramVisibleBinding = new ValueBinding<bool>(BindingGroup, "railwayTramVisible", true);
            _railwayLightRailVisibleBinding = new ValueBinding<bool>(BindingGroup, "railwayLightRailVisible", true);
            _railwaySubwayVisibleBinding = new ValueBinding<bool>(BindingGroup, "railwaySubwayVisible", true);
            _railwayServiceVisibleBinding = new ValueBinding<bool>(BindingGroup, "railwayServiceVisible", true);
            _railwayTunnelsVisibleBinding = new ValueBinding<bool>(BindingGroup, "railwayTunnelsVisible", true);
            _railwayTrainCountBinding = new ValueBinding<int>(BindingGroup, "railwayTrainCount", 0);
            _railwayTramCountBinding = new ValueBinding<int>(BindingGroup, "railwayTramCount", 0);
            _railwayLightRailCountBinding = new ValueBinding<int>(BindingGroup, "railwayLightRailCount", 0);
            _railwaySubwayCountBinding = new ValueBinding<int>(BindingGroup, "railwaySubwayCount", 0);
            _railwayServiceCountBinding = new ValueBinding<int>(BindingGroup, "railwayServiceCount", 0);
            _railwayTunnelCountBinding = new ValueBinding<int>(BindingGroup, "railwayTunnelCount", 0);
            _railwayTotalCountBinding = new ValueBinding<int>(BindingGroup, "railwayTotalCount", 0);
            _railwayStatusBinding = new ValueBinding<string>(
                BindingGroup,
                "railwayStatus",
                "Aucune donnée ferroviaire disponible dans ce bundle."
            );

            AddBinding(_railwayAvailableBinding);
            AddBinding(_railwayVisibleBinding);
            AddBinding(_railwayOpacityBinding);
            AddBinding(_railwayThicknessBinding);
            AddBinding(_railwayTrainVisibleBinding);
            AddBinding(_railwayTramVisibleBinding);
            AddBinding(_railwayLightRailVisibleBinding);
            AddBinding(_railwaySubwayVisibleBinding);
            AddBinding(_railwayServiceVisibleBinding);
            AddBinding(_railwayTunnelsVisibleBinding);
            AddBinding(_railwayTrainCountBinding);
            AddBinding(_railwayTramCountBinding);
            AddBinding(_railwayLightRailCountBinding);
            AddBinding(_railwaySubwayCountBinding);
            AddBinding(_railwayServiceCountBinding);
            AddBinding(_railwayTunnelCountBinding);
            AddBinding(_railwayTotalCountBinding);
            AddBinding(_railwayStatusBinding);

            _setRailwayVisibleBinding = CreateRailwayBooleanTrigger("setRailwayVisible", "visible");
            _setRailwayTrainVisibleBinding = CreateRailwayBooleanTrigger("setRailwayTrainVisible", "trainVisible");
            _setRailwayTramVisibleBinding = CreateRailwayBooleanTrigger("setRailwayTramVisible", "tramVisible");
            _setRailwayLightRailVisibleBinding = CreateRailwayBooleanTrigger("setRailwayLightRailVisible", "lightRailVisible");
            _setRailwaySubwayVisibleBinding = CreateRailwayBooleanTrigger("setRailwaySubwayVisible", "subwayVisible");
            _setRailwayServiceVisibleBinding = CreateRailwayBooleanTrigger("setRailwayServiceVisible", "serviceVisible");
            _setRailwayTunnelsVisibleBinding = CreateRailwayBooleanTrigger("setRailwayTunnelsVisible", "tunnelsVisible");

            _setRailwayOpacityBinding = new TriggerBinding<float>(
                BindingGroup,
                "setRailwayOpacity",
                value => SetRailwayFloat("opacity", value)
            );
            _setRailwayThicknessBinding = new TriggerBinding<float>(
                BindingGroup,
                "setRailwayThickness",
                value => SetRailwayFloat("thickness", value)
            );

            AddBinding(_setRailwayVisibleBinding);
            AddBinding(_setRailwayOpacityBinding);
            AddBinding(_setRailwayThicknessBinding);
            AddBinding(_setRailwayTrainVisibleBinding);
            AddBinding(_setRailwayTramVisibleBinding);
            AddBinding(_setRailwayLightRailVisibleBinding);
            AddBinding(_setRailwaySubwayVisibleBinding);
            AddBinding(_setRailwayServiceVisibleBinding);
            AddBinding(_setRailwayTunnelsVisibleBinding);

            SyncRailwayBindings();
        }

        private TriggerBinding<bool> CreateRailwayBooleanTrigger(string triggerName, string configKey)
        {
            return new TriggerBinding<bool>(
                BindingGroup,
                triggerName,
                value => SetRailwayBoolean(configKey, value)
            );
        }

        private void SetRailwayBoolean(string configKey, bool value)
        {
            GeoDebugOverlay.SetRailwayBoolean(configKey, value);
            SyncRailwayBindings();
        }

        private void SetRailwayFloat(string configKey, float value)
        {
            GeoDebugOverlay.SetRailwayFloat(configKey, value);
            SyncRailwayBindings();
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();

            // Keep CoHTML completely idle while the panel is closed.  The old
            // 4 Hz poll rebuilt statistics JSON and queried overlay progress
            // throughout gameplay even though no UI could consume it.
            if (_visibleBinding == null || !_visibleBinding.value)
                return;

            if (UnityEngine.Time.unscaledTime < _nextRailwayBindingSyncTime)
                return;

            _nextRailwayBindingSyncTime = UnityEngine.Time.unscaledTime + 1f;
            SyncOverlayLayerBindings();
            SyncRailwayBindings();
            SyncStatisticsBindings();
        }

        private void SyncRailwayBindings()
        {
            var state = GeoDebugOverlay.GetRailwayHudSnapshot();

            if (state == null)
                state = RailwayHudSnapshot.Unavailable(
                    "Aucune donnée ferroviaire disponible dans ce bundle."
                );

            var signature = BuildRailwayBindingSignature(state);
            if (string.Equals(signature, _lastRailwayBindingSignature, System.StringComparison.Ordinal))
                return;

            _lastRailwayBindingSignature = signature;

            UpdateBinding(_railwayAvailableBinding, state.Available);
            UpdateBinding(_railwayVisibleBinding, state.Visible);
            UpdateBinding(_railwayOpacityBinding, state.Opacity);
            UpdateBinding(_railwayThicknessBinding, state.Thickness);
            UpdateBinding(_railwayTrainVisibleBinding, state.TrainVisible);
            UpdateBinding(_railwayTramVisibleBinding, state.TramVisible);
            UpdateBinding(_railwayLightRailVisibleBinding, state.LightRailVisible);
            UpdateBinding(_railwaySubwayVisibleBinding, state.SubwayVisible);
            UpdateBinding(_railwayServiceVisibleBinding, state.ServiceVisible);
            UpdateBinding(_railwayTunnelsVisibleBinding, state.TunnelsVisible);
            UpdateBinding(_railwayTrainCountBinding, state.TrainCount);
            UpdateBinding(_railwayTramCountBinding, state.TramCount);
            UpdateBinding(_railwayLightRailCountBinding, state.LightRailCount);
            UpdateBinding(_railwaySubwayCountBinding, state.SubwayCount);
            UpdateBinding(_railwayServiceCountBinding, state.ServiceCount);
            UpdateBinding(_railwayTunnelCountBinding, state.TunnelCount);
            UpdateBinding(_railwayTotalCountBinding, state.TotalCount);
            UpdateBinding(_railwayStatusBinding, state.Status ?? "");
        }

        private static string BuildRailwayBindingSignature(RailwayHudSnapshot state)
        {
            return string.Join("|", new[]
            {
                state.Available ? "1" : "0",
                state.Visible ? "1" : "0",
                state.Opacity.ToString("R", CultureInfo.InvariantCulture),
                state.Thickness.ToString("R", CultureInfo.InvariantCulture),
                state.TrainVisible ? "1" : "0",
                state.TramVisible ? "1" : "0",
                state.LightRailVisible ? "1" : "0",
                state.SubwayVisible ? "1" : "0",
                state.ServiceVisible ? "1" : "0",
                state.TunnelsVisible ? "1" : "0",
                state.TrainCount.ToString(CultureInfo.InvariantCulture),
                state.TramCount.ToString(CultureInfo.InvariantCulture),
                state.LightRailCount.ToString(CultureInfo.InvariantCulture),
                state.SubwayCount.ToString(CultureInfo.InvariantCulture),
                state.ServiceCount.ToString(CultureInfo.InvariantCulture),
                state.TunnelCount.ToString(CultureInfo.InvariantCulture),
                state.TotalCount.ToString(CultureInfo.InvariantCulture),
                state.Status ?? ""
            });
        }

        private static void UpdateBinding(ValueBinding<bool> binding, bool value)
        {
            if (binding != null && binding.value != value)
                binding.Update(value);
        }

        private static void UpdateBinding(ValueBinding<int> binding, int value)
        {
            if (binding != null && binding.value != value)
                binding.Update(value);
        }

        private static void UpdateBinding(ValueBinding<float> binding, float value)
        {
            if (binding != null && Mathf.Abs(binding.value - value) > 0.0001f)
                binding.Update(value);
        }

        private static void UpdateBinding(ValueBinding<string> binding, string value)
        {
            if (binding != null && binding.value != value)
                binding.Update(value);
        }
    }
}
