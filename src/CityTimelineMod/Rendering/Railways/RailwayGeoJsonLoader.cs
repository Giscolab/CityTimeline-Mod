using System;
using System.Collections.Generic;
using System.IO;
using CityTimelineMod.Importers;
using Newtonsoft.Json.Linq;

namespace CityTimelineMod.Rendering.Railways
{
    internal static class RailwayGeoJsonLoader
    {
        private static readonly HashSet<string> SupportedServices = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "yard",
            "siding",
            "spur",
            "crossover"
        };

        internal static List<GeoRailwayLine> Load(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException(path);

            JObject root;

            try
            {
                root = JObject.Parse(File.ReadAllText(path));
            }
            catch (Exception ex)
            {
                throw new InvalidDataException("Invalid railway GeoJSON", ex);
            }

            if (!string.Equals(ReadString(root, "type"), "FeatureCollection", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Railway GeoJSON is not a FeatureCollection");

            var features = root["features"] as JArray;
            if (features == null)
                throw new InvalidDataException("Railway GeoJSON has no features array");

            var result = new List<GeoRailwayLine>();

            foreach (var token in features)
            {
                var feature = token as JObject;
                if (feature == null)
                    continue;

                AddFeature(feature, result);
            }

            return result;
        }

        private static void AddFeature(JObject feature, List<GeoRailwayLine> result)
        {
            var properties = feature["properties"] as JObject;
            var tags = properties != null ? properties["tags"] as JObject : null;

            if (IsInactive(properties, tags))
                return;

            var railway = ReadProperty(properties, tags, "railway");
            var service = ReadProperty(properties, tags, "service");
            RailwayCategory category;

            if (!TryClassify(railway, service, out category))
                return;

            var geometry = feature["geometry"] as JObject;
            if (geometry == null)
                return;

            var geometryType = ReadString(geometry, "type");
            var coordinates = geometry["coordinates"];

            if (string.Equals(geometryType, "LineString", StringComparison.OrdinalIgnoreCase))
            {
                AddLine(coordinates as JArray, properties, tags, railway, service, category, result);
                return;
            }

            if (!string.Equals(geometryType, "MultiLineString", StringComparison.OrdinalIgnoreCase))
                return;

            var multi = coordinates as JArray;
            if (multi == null)
                return;

            foreach (var lineToken in multi)
                AddLine(lineToken as JArray, properties, tags, railway, service, category, result);
        }

        private static void AddLine(
            JArray coordinates,
            JObject properties,
            JObject tags,
            string railway,
            string service,
            RailwayCategory category,
            List<GeoRailwayLine> result
        )
        {
            var points = ParseLineString(coordinates);
            if (points.Count < 2)
                return;

            result.Add(new GeoRailwayLine(
                points,
                category,
                Normalize(railway),
                ReadProperty(properties, tags, "usage"),
                SupportedServices.Contains(Normalize(service)) ? Normalize(service) : null,
                ReadProperty(properties, tags, "tracks"),
                ReadProperty(properties, tags, "gauge"),
                ReadBoolProperty(properties, tags, "bridge"),
                ReadBoolProperty(properties, tags, "tunnel"),
                ReadProperty(properties, tags, "electrified"),
                ReadProperty(properties, tags, "name")
            ));
        }

        private static bool TryClassify(string railway, string service, out RailwayCategory category)
        {
            var type = Normalize(railway);
            var serviceType = Normalize(service);

            if (SupportedServices.Contains(serviceType) && IsSupportedRailwayType(type))
            {
                category = RailwayCategory.Service;
                return true;
            }

            switch (type)
            {
                case "rail":
                case "narrow_gauge":
                    category = RailwayCategory.Train;
                    return true;

                case "tram":
                    category = RailwayCategory.Tram;
                    return true;

                case "light_rail":
                    category = RailwayCategory.LightRail;
                    return true;

                case "subway":
                    category = RailwayCategory.Subway;
                    return true;

                default:
                    category = RailwayCategory.Train;
                    return false;
            }
        }

        private static bool IsSupportedRailwayType(string value)
        {
            return value == "rail" ||
                   value == "narrow_gauge" ||
                   value == "tram" ||
                   value == "light_rail" ||
                   value == "subway";
        }

        private static bool IsInactive(JObject properties, JObject tags)
        {
            var status = Normalize(ReadProperty(properties, tags, "status"));
            if (status == "abandoned" || status == "disused" || status == "proposed" || status == "construction")
                return true;

            var lifecycle = new[] { "abandoned", "disused", "proposed", "construction" };
            for (var i = 0; i < lifecycle.Length; i++)
            {
                var key = lifecycle[i];
                if (ReadPresence(properties, tags, key) ||
                    ReadPresence(properties, tags, key + ":railway") ||
                    ReadPresence(properties, tags, "railway:" + key))
                {
                    return true;
                }
            }

            var active = ReadProperty(properties, tags, "active");
            return active != null && !IsPresent(active);
        }

        private static List<GeoPoint> ParseLineString(JArray coordinates)
        {
            var result = new List<GeoPoint>();
            if (coordinates == null)
                return result;

            foreach (var token in coordinates)
            {
                var coordinate = token as JArray;
                if (coordinate == null || coordinate.Count < 2)
                    continue;

                double lon;
                double lat;

                try
                {
                    // Read numeric JSON tokens directly. JToken.ToString() uses the
                    // current culture for floating-point values, which turns decimal
                    // points into commas on French systems before an invariant parse.
                    lon = coordinate[0].Value<double>();
                    lat = coordinate[1].Value<double>();
                }
                catch (Exception)
                {
                    continue;
                }

                if (lat < -90d || lat > 90d || lon < -180d || lon > 180d)
                    continue;

                result.Add(new GeoPoint(lon, lat));
            }

            return result;
        }

        private static string ReadProperty(JObject properties, JObject tags, string key)
        {
            var value = ReadString(properties, key);
            return string.IsNullOrWhiteSpace(value) ? ReadString(tags, key) : value;
        }

        private static bool ReadBoolProperty(JObject properties, JObject tags, string key)
        {
            return IsPresent(ReadProperty(properties, tags, key));
        }

        private static bool ReadPresence(JObject properties, JObject tags, string key)
        {
            var value = ReadProperty(properties, tags, key);
            return value != null && IsPresent(value);
        }

        private static bool IsPresent(string value)
        {
            var normalized = Normalize(value);
            return normalized != "" && normalized != "0" && normalized != "false" && normalized != "no" && normalized != "none";
        }

        private static string ReadString(JObject obj, string key)
        {
            if (obj == null || string.IsNullOrWhiteSpace(key))
                return null;

            var token = obj[key];
            if (token == null || token.Type == JTokenType.Null)
                return null;

            var value = token.ToString();
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "" : value.Trim().ToLowerInvariant();
        }
    }
}
