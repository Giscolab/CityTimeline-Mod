using System.Globalization;
using Newtonsoft.Json.Linq;

namespace CityTimelineMod.Config
{
    internal sealed partial class GeoOverlayConfig
    {
        private static string GetString(JObject root, string name, string fallback)
        {
            var token = root[name];
            if (token == null) return fallback;

            var value = token.ToString();
            if (string.IsNullOrWhiteSpace(value))
                return fallback;

            return value;
        }

        private static bool GetBool(JObject root, string name, bool fallback)
        {
            var token = root[name];
            if (token == null) return fallback;

            bool value;
            if (bool.TryParse(token.ToString(), out value))
                return value;

            return fallback;
        }

        private static float GetFloat(JObject root, string name, float fallback)
        {
            var token = root[name];
            if (token == null || token.Type == JTokenType.Null)
                return fallback;

            if (token.Type == JTokenType.Float || token.Type == JTokenType.Integer)
                return token.Value<float>();

            float value;
            var raw = token.ToString();

            if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                return value;

            if (float.TryParse(raw, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
                return value;

            return fallback;
        }

        private static double GetDouble(JObject root, string name, double fallback)
        {
            var token = root[name];
            if (token == null || token.Type == JTokenType.Null)
                return fallback;

            if (token.Type == JTokenType.Float || token.Type == JTokenType.Integer)
                return token.Value<double>();

            double value;
            var raw = token.ToString();

            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                return value;

            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
                return value;

            return fallback;
        }

        private static int GetInt(JObject root, string name, int fallback)
        {
            var token = root[name];
            if (token == null) return fallback;

            int value;
            if (int.TryParse(token.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                return value;

            return fallback;
        }
    }
}
