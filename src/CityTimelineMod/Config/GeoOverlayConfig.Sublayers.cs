using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace CityTimelineMod.Config
{
    internal sealed partial class GeoOverlayConfig
    {
        internal readonly Dictionary<string, bool> OverlaySublayerVisibility =
            new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        internal bool GetOverlaySublayerVisible(string key)
        {
            bool value;
            return string.IsNullOrWhiteSpace(key) ||
                !OverlaySublayerVisibility.TryGetValue(key.Trim(), out value) || value;
        }

        internal void SetOverlaySublayerVisible(string key, bool value)
        {
            if (!string.IsNullOrWhiteSpace(key))
                OverlaySublayerVisibility[key.Trim()] = value;
        }

        private void LoadOverlaySublayerSettings(JObject root)
        {
            OverlaySublayerVisibility.Clear();
            var settings = root != null ? root["overlaySublayers"] as JObject : null;
            if (settings == null)
                return;

            foreach (var property in settings.Properties())
            {
                if (property.Value != null && property.Value.Type == JTokenType.Boolean)
                    OverlaySublayerVisibility[property.Name] = property.Value.Value<bool>();
            }
        }

        private void WriteOverlaySublayerSettings(JObject root)
        {
            var settings = new JObject();
            foreach (var pair in OverlaySublayerVisibility)
                settings[pair.Key] = pair.Value;
            root["overlaySublayers"] = settings;
        }
    }
}
