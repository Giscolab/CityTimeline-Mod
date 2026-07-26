using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CityTimelineMod.Config
{
    internal sealed partial class GeoOverlayConfig
    {
        private static string GetString(JObject root, string name, string fallback)
        {
            var token = GetToken(root, name);
            if (token == null)
                return fallback;

            if (token.Type != JTokenType.String)
                throw WrongType(name, "string", token);

            var value = token.Value<string>();
            if (string.IsNullOrWhiteSpace(value))
                return fallback;

            return value;
        }

        private static bool GetBool(JObject root, string name, bool fallback)
        {
            var token = GetToken(root, name);
            if (token == null)
                return fallback;

            if (token.Type != JTokenType.Boolean)
                throw WrongType(name, "boolean", token);

            return token.Value<bool>();
        }

        private static float GetFloat(JObject root, string name, float fallback)
        {
            var token = GetToken(root, name);
            if (token == null)
                return fallback;

            if (token.Type != JTokenType.Float && token.Type != JTokenType.Integer)
                throw WrongType(name, "number", token);

            var value = token.Value<float>();
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new JsonSerializationException(
                    "Config value '" + name + "' must be a finite number."
                );

            return value;
        }

        private static double GetDouble(JObject root, string name, double fallback)
        {
            var token = GetToken(root, name);
            if (token == null)
                return fallback;

            if (token.Type != JTokenType.Float && token.Type != JTokenType.Integer)
                throw WrongType(name, "number", token);

            var value = token.Value<double>();
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new JsonSerializationException(
                    "Config value '" + name + "' must be a finite number."
                );

            return value;
        }

        private static int GetInt(JObject root, string name, int fallback)
        {
            var token = GetToken(root, name);
            if (token == null)
                return fallback;

            if (token.Type != JTokenType.Integer)
                throw WrongType(name, "integer", token);

            return token.Value<int>();
        }

        private static JToken GetToken(JObject root, string name)
        {
            if (root == null || string.IsNullOrWhiteSpace(name))
                return null;

            JToken token;
            return root.TryGetValue(name, StringComparison.OrdinalIgnoreCase, out token)
                ? token
                : null;
        }

        private static JsonSerializationException WrongType(
            string name,
            string expected,
            JToken token
        )
        {
            return new JsonSerializationException(
                "Config value '" + name + "' must be " + expected +
                "; found " + (token == null ? "missing" : token.Type.ToString()) + "."
            );
        }
    }
}
