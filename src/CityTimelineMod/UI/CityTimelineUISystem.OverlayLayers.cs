using System.Globalization;
using Colossal.UI.Binding;
using CityTimelineMod.Rendering;
using CityTimelineMod.Rendering.Statistics;
using Newtonsoft.Json.Linq;

namespace CityTimelineMod.UI
{
    internal sealed partial class CityTimelineUISystem
    {
        private ValueBinding<bool> _overlayLayersAvailableBinding;
        private ValueBinding<bool> _overlayZoningVisibleBinding;
        private ValueBinding<bool> _overlayRoadsVisibleBinding;
        private ValueBinding<bool> _overlayPathsVisibleBinding;
        private ValueBinding<bool> _overlayWaterVisibleBinding;
        private ValueBinding<bool> _overlayBoundsVisibleBinding;
        private ValueBinding<float> _overlayZoningOpacityBinding;
        private ValueBinding<float> _overlayRoadsOpacityBinding;
        private ValueBinding<float> _overlayPathsOpacityBinding;
        private ValueBinding<float> _overlayWaterOpacityBinding;
        private ValueBinding<float> _overlayBoundsOpacityBinding;
        private ValueBinding<string> _overlaySublayersJsonBinding;

        private string _lastOverlayLayerBindingSignature;
        private string _lastOverlaySublayersJson;

        private void CreateOverlayLayerBindings()
        {
            _overlayLayersAvailableBinding = new ValueBinding<bool>(BindingGroup, "overlayLayersAvailable", false);
            _overlayZoningVisibleBinding = new ValueBinding<bool>(BindingGroup, "overlayZoningVisible", true);
            _overlayRoadsVisibleBinding = new ValueBinding<bool>(BindingGroup, "overlayRoadsVisible", true);
            _overlayPathsVisibleBinding = new ValueBinding<bool>(BindingGroup, "overlayPathsVisible", true);
            _overlayWaterVisibleBinding = new ValueBinding<bool>(BindingGroup, "overlayWaterVisible", true);
            _overlayBoundsVisibleBinding = new ValueBinding<bool>(BindingGroup, "overlayBoundsVisible", true);
            _overlayZoningOpacityBinding = new ValueBinding<float>(BindingGroup, "overlayZoningOpacity", 1f);
            _overlayRoadsOpacityBinding = new ValueBinding<float>(BindingGroup, "overlayRoadsOpacity", 1f);
            _overlayPathsOpacityBinding = new ValueBinding<float>(BindingGroup, "overlayPathsOpacity", 1f);
            _overlayWaterOpacityBinding = new ValueBinding<float>(BindingGroup, "overlayWaterOpacity", 1f);
            _overlayBoundsOpacityBinding = new ValueBinding<float>(BindingGroup, "overlayBoundsOpacity", 1f);
            _overlaySublayersJsonBinding = new ValueBinding<string>(BindingGroup, "overlaySublayersJson", "{}");

            AddRuntimeBinding(_overlayLayersAvailableBinding);
            AddRuntimeBinding(_overlayZoningVisibleBinding);
            AddRuntimeBinding(_overlayRoadsVisibleBinding);
            AddRuntimeBinding(_overlayPathsVisibleBinding);
            AddRuntimeBinding(_overlayWaterVisibleBinding);
            AddRuntimeBinding(_overlayBoundsVisibleBinding);
            AddRuntimeBinding(_overlayZoningOpacityBinding);
            AddRuntimeBinding(_overlayRoadsOpacityBinding);
            AddRuntimeBinding(_overlayPathsOpacityBinding);
            AddRuntimeBinding(_overlayWaterOpacityBinding);
            AddRuntimeBinding(_overlayBoundsOpacityBinding);
            AddRuntimeBinding(_overlaySublayersJsonBinding);

            AddRuntimeBinding(CreateOverlayBooleanTrigger("setOverlayZoningVisible", "zoning"));
            AddRuntimeBinding(CreateOverlayBooleanTrigger("setOverlayRoadsVisible", "roads"));
            AddRuntimeBinding(CreateOverlayBooleanTrigger("setOverlayPathsVisible", "paths"));
            AddRuntimeBinding(CreateOverlayBooleanTrigger("setOverlayWaterVisible", "water"));
            AddRuntimeBinding(CreateOverlayBooleanTrigger("setOverlayBoundsVisible", "bounds"));
            AddRuntimeBinding(CreateOverlayFloatTrigger("setOverlayZoningOpacity", "zoning"));
            AddRuntimeBinding(CreateOverlayFloatTrigger("setOverlayRoadsOpacity", "roads"));
            AddRuntimeBinding(CreateOverlayFloatTrigger("setOverlayPathsOpacity", "paths"));
            AddRuntimeBinding(CreateOverlayFloatTrigger("setOverlayWaterOpacity", "water"));
            AddRuntimeBinding(CreateOverlayFloatTrigger("setOverlayBoundsOpacity", "bounds"));
            AddRuntimeBinding(new TriggerBinding<string>(
                BindingGroup,
                "setOverlaySublayerVisible",
                ApplyOverlaySublayerPayload
            ));

            SyncOverlayLayerBindings();
        }

        private TriggerBinding<bool> CreateOverlayBooleanTrigger(string triggerName, string key)
        {
            return new TriggerBinding<bool>(
                BindingGroup,
                triggerName,
                value =>
                {
                    if (!RuntimeCallbacksAllowed)
                        return;

                    GeoDebugOverlay.SetOverlayLayerBoolean(key, value);
                    SyncOverlayLayerBindings();
                    SyncStatisticsBindings();
                }
            );
        }

        private TriggerBinding<float> CreateOverlayFloatTrigger(string triggerName, string key)
        {
            return new TriggerBinding<float>(
                BindingGroup,
                triggerName,
                value =>
                {
                    if (!RuntimeCallbacksAllowed)
                        return;

                    GeoDebugOverlay.SetOverlayLayerFloat(key, value);
                    SyncOverlayLayerBindings();
                }
            );
        }

        private void SyncOverlayLayerBindings()
        {
            if (!RuntimeCallbacksAllowed)
                return;

            var state = GeoDebugOverlay.GetOverlayLayerHudSnapshot() ??
                OverlayLayerHudSnapshot.Unavailable();
            var signature = BuildOverlayLayerBindingSignature(state);
            if (string.Equals(signature, _lastOverlayLayerBindingSignature, System.StringComparison.Ordinal))
                return;

            _lastOverlayLayerBindingSignature = signature;
            UpdateBinding(_overlayLayersAvailableBinding, state.Available);
            UpdateBinding(_overlayZoningVisibleBinding, state.ZoningVisible);
            UpdateBinding(_overlayRoadsVisibleBinding, state.RoadsVisible);
            UpdateBinding(_overlayPathsVisibleBinding, state.PathsVisible);
            UpdateBinding(_overlayWaterVisibleBinding, state.WaterVisible);
            UpdateBinding(_overlayBoundsVisibleBinding, state.BoundsVisible);
            UpdateBinding(_overlayZoningOpacityBinding, state.ZoningOpacity);
            UpdateBinding(_overlayRoadsOpacityBinding, state.RoadsOpacity);
            UpdateBinding(_overlayPathsOpacityBinding, state.PathsOpacity);
            UpdateBinding(_overlayWaterOpacityBinding, state.WaterOpacity);
            UpdateBinding(_overlayBoundsOpacityBinding, state.BoundsOpacity);

            var sublayersJson = GeoDebugOverlay.GetOverlaySublayersJson();
            if (!string.Equals(sublayersJson, _lastOverlaySublayersJson, System.StringComparison.Ordinal))
            {
                _lastOverlaySublayersJson = sublayersJson;
                UpdateBinding(_overlaySublayersJsonBinding,
                    string.IsNullOrWhiteSpace(sublayersJson) ? "{}" : sublayersJson);
            }
        }

        private void ApplyOverlaySublayerPayload(string payload)
        {
            if (!RuntimeCallbacksAllowed)
                return;

            if (string.IsNullOrWhiteSpace(payload))
                return;

            try
            {
                var root = JObject.Parse(payload);
                var key = root.Value<string>("key");
                var value = root["value"];
                if (string.IsNullOrWhiteSpace(key) || value == null || value.Type != JTokenType.Boolean)
                    return;

                GeoDebugOverlay.SetOverlaySublayerVisible(key, value.Value<bool>());
                _lastOverlayLayerBindingSignature = null;
                _lastOverlaySublayersJson = null;
                SyncOverlayLayerBindings();
                SyncStatisticsBindings();
            }
            catch (System.Exception)
            {
                // Invalid UI payloads are ignored; they must never affect gameplay.
            }
        }

        private static string BuildOverlayLayerBindingSignature(OverlayLayerHudSnapshot state)
        {
            return string.Join("|", new[]
            {
                state.Available ? "1" : "0",
                state.ZoningVisible ? "1" : "0",
                state.RoadsVisible ? "1" : "0",
                state.PathsVisible ? "1" : "0",
                state.WaterVisible ? "1" : "0",
                state.BoundsVisible ? "1" : "0",
                state.ZoningOpacity.ToString("R", CultureInfo.InvariantCulture),
                state.RoadsOpacity.ToString("R", CultureInfo.InvariantCulture),
                state.PathsOpacity.ToString("R", CultureInfo.InvariantCulture),
                state.WaterOpacity.ToString("R", CultureInfo.InvariantCulture),
                state.BoundsOpacity.ToString("R", CultureInfo.InvariantCulture)
            });
        }
    }
}
