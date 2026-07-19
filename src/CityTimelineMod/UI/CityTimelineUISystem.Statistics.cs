using Colossal.UI.Binding;
using CityTimelineMod.Rendering;
using CityTimelineMod.Util;
using Newtonsoft.Json.Linq;

namespace CityTimelineMod.UI
{
    internal sealed partial class CityTimelineUISystem
    {
        private ValueBinding<string> _bundleStatsJsonBinding;
        private TriggerBinding<string> _setServiceVisibleBinding;
        private TriggerBinding<string> _setServiceOpacityBinding;

        private void CreateStatisticsBindings()
        {
            _bundleStatsJsonBinding = new ValueBinding<string>(
                BindingGroup,
                "bundleStatsJson",
                "{}"
            );

            _setServiceVisibleBinding = new TriggerBinding<string>(
                BindingGroup,
                "setServiceVisible",
                payload => ApplyServiceBooleanPayload(payload)
            );

            _setServiceOpacityBinding = new TriggerBinding<string>(
                BindingGroup,
                "setServiceOpacity",
                payload => ApplyServiceFloatPayload(payload)
            );

            AddBinding(_bundleStatsJsonBinding);
            AddBinding(_setServiceVisibleBinding);
            AddBinding(_setServiceOpacityBinding);
            SyncStatisticsBindings();
        }

        private void ApplyServiceBooleanPayload(string payload)
        {
            string key;
            JToken value;
            if (!TryReadServicePayload(payload, out key, out value) || value.Type != JTokenType.Boolean)
                return;

            GeoDebugOverlay.SetServiceBoolean(key, value.Value<bool>());
            SyncStatisticsBindings();
        }

        private void ApplyServiceFloatPayload(string payload)
        {
            string key;
            JToken value;
            if (!TryReadServicePayload(payload, out key, out value) ||
                (value.Type != JTokenType.Float && value.Type != JTokenType.Integer))
            {
                return;
            }

            GeoDebugOverlay.SetServiceFloat(key, value.Value<float>());
            SyncStatisticsBindings();
        }

        private static bool TryReadServicePayload(string payload, out string key, out JToken value)
        {
            key = null;
            value = null;

            if (string.IsNullOrWhiteSpace(payload))
                return false;

            try
            {
                var root = JObject.Parse(payload);
                key = root.Value<string>("key");
                value = root["value"];
                return !string.IsNullOrWhiteSpace(key) && value != null;
            }
            catch (System.Exception ex)
            {
                Log.Error("CityTimelineMod: invalid service HUD payload. " + ex.Message);
                return false;
            }
        }

        private void SyncStatisticsBindings()
        {
            var json = GeoDebugOverlay.GetBundleHudSnapshotJson();
            UpdateBinding(_bundleStatsJsonBinding, string.IsNullOrWhiteSpace(json) ? "{}" : json);
        }
    }
}
