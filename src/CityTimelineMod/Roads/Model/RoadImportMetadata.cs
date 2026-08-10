using System;
using CityTimelineMod.Importers;
using Newtonsoft.Json.Linq;

namespace CityTimelineMod.Roads
{
    internal sealed class RoadImportMetadata
    {
        internal string Highway = "";
        internal string Subcategory = "";
        internal int TargetLaneCount = 1;
        internal bool Oneway;
        internal bool Roundabout;
        internal bool Bridge;
        internal bool Tunnel;
        internal string Maxspeed = "";
        internal string Surface = "";
        internal string Name = "";

        internal static RoadImportMetadata FromFeature(JObject feature)
        {
            var props = feature != null ? feature["properties"] as JObject : null;

            if (props == null)
                return null;

            var roadImport = props["roadImport"] as JObject;

            var highway = ReadString(roadImport, props, "highway");
            var subcategory = ReadString(roadImport, props, "subcategory");
            var surface = ReadString(roadImport, props, "surface");
            var maxspeed = ReadString(roadImport, props, "maxspeed");
            var name = ReadString(null, props, "name");

            var oneway = ReadBoolFlexible(roadImport, props, "oneway");
            var roundabout = ReadBoolFlexible(roadImport, props, "roundabout") ||
                             string.Equals(ReadString(null, props, "junction"), "roundabout", StringComparison.OrdinalIgnoreCase);

            var bridge = ReadBoolFlexible(roadImport, props, "bridge");
            var tunnel = ReadBoolFlexible(roadImport, props, "tunnel");

            var laneCount = ReadLaneCount(roadImport, props, highway, oneway);

            return new RoadImportMetadata
            {
                Highway = NormalizeHighway(highway),
                Subcategory = NormalizeText(subcategory),
                TargetLaneCount = Math.Max(1, laneCount),
                Oneway = oneway,
                Roundabout = roundabout,
                Bridge = bridge,
                Tunnel = tunnel,
                Maxspeed = NormalizeText(maxspeed),
                Surface = NormalizeText(surface),
                Name = name ?? "",
            };
        }

        internal static RoadImportMetadata FromRoadLine(GeoRoadLine line)
        {
            if (line == null)
                return null;

            var oneway = ParseOneway(line.Oneway);
            var highway = NormalizeHighway(line.Highway);
            var lanes = ResolveLaneCount(line.TargetLaneCount, line.Lanes, highway, oneway);

            return new RoadImportMetadata
            {
                Highway = highway,
                Subcategory = "",
                TargetLaneCount = Math.Max(1, lanes),
                Oneway = oneway,
                Roundabout = line.Roundabout,
                Bridge = line.Bridge,
                Tunnel = line.Tunnel,
                Maxspeed = NormalizeText(line.MaxSpeed),
                Surface = NormalizeText(line.Surface),
                Name = line.Name ?? "",
            };
        }

        private static string ReadString(JObject primary, JObject fallback, string key)
        {
            var value = primary != null ? primary.Value<string>(key) : null;

            if (string.IsNullOrWhiteSpace(value) && fallback != null)
                value = fallback.Value<string>(key);

            return value ?? "";
        }

        private static bool ReadBoolFlexible(JObject primary, JObject fallback, string key)
        {
            JToken token = null;

            if (primary != null)
                token = primary[key];

            if (token == null && fallback != null)
                token = fallback[key];

            if (token == null || token.Type == JTokenType.Null)
                return false;

            if (token.Type == JTokenType.Boolean)
                return token.Value<bool>();

            if (token.Type == JTokenType.Integer)
                return token.Value<int>() != 0;

            return ParseOneway(token.ToString());
        }

        private static int ReadLaneCount(JObject primary, JObject fallback, string highway, bool oneway)
        {
            var explicitTarget = ReadNullableInt(primary, fallback, "targetLaneCount");

            if (explicitTarget.HasValue && explicitTarget.Value > 0)
                return ClampLaneCount(explicitTarget.Value);

            var explicitLanes = ReadNullableInt(primary, fallback, "lanes");

            if (explicitLanes.HasValue && explicitLanes.Value > 0)
                return ClampLaneCount(explicitLanes.Value);

            return ResolveDefaultLaneCount(highway, oneway);
        }

        private static int? ReadNullableInt(JObject primary, JObject fallback, string key)
        {
            var value = ReadString(primary, fallback, key);

            if (string.IsNullOrWhiteSpace(value))
                return null;

            value = value.Trim();

            var separator = value.IndexOf(';');

            if (separator >= 0)
                value = value.Substring(0, separator);

            separator = value.IndexOf(',');

            if (separator >= 0)
                value = value.Substring(0, separator);

            int parsed;

            if (int.TryParse(value, out parsed))
                return parsed;

            double parsedDouble;

            if (double.TryParse(
                    value.Replace(',', '.'),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out parsedDouble))
            {
                return (int)Math.Round(parsedDouble);
            }

            return null;
        }

        private static int ResolveLaneCount(int? targetLaneCount, int? lanes, string highway, bool oneway)
        {
            if (targetLaneCount.HasValue && targetLaneCount.Value > 0)
                return ClampLaneCount(targetLaneCount.Value);

            if (lanes.HasValue && lanes.Value > 0)
                return ClampLaneCount(lanes.Value);

            return ResolveDefaultLaneCount(highway, oneway);
        }

        private static int ClampLaneCount(int value)
        {
            if (value < 1)
                return 1;

            if (value > 6)
                return 6;

            return value;
        }

        private static bool ParseOneway(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            var text = value.Trim().ToLowerInvariant();

            return text == "yes" ||
                   text == "true" ||
                   text == "1" ||
                   text == "-1" ||
                   text == "reverse";
        }

        private static string NormalizeHighway(string highway)
        {
            var h = NormalizeText(highway);

            if (string.IsNullOrWhiteSpace(h))
                return "residential";

            if (h == "road")
                return "residential";

            if (h == "trunk")
                return "trunk";

            if (h == "motorway" ||
                h == "primary" ||
                h == "secondary" ||
                h == "tertiary" ||
                h == "residential" ||
                h == "unclassified" ||
                h == "living_street" ||
                h == "service" ||
                h == "track" ||
                h == "motorway_link" ||
                h == "trunk_link" ||
                h == "primary_link" ||
                h == "secondary_link" ||
                h == "tertiary_link")
            {
                return h;
            }

            // Les chemins non routiers ne devraient normalement pas être dans roads_driveable.
            // Si un tag exotique arrive ici, on le rend importable comme rue locale plutôt que highway.
            return "residential";
        }

        private static int ResolveDefaultLaneCount(string highway, bool oneway)
        {
            var h = NormalizeHighway(highway);

            switch (h)
            {
                case "motorway":
                case "trunk":
                    return oneway ? 3 : 4;

                case "motorway_link":
                case "trunk_link":
                case "primary_link":
                case "secondary_link":
                case "tertiary_link":
                    return oneway ? 1 : 2;

                case "primary":
                    return oneway ? 2 : 4;

                case "secondary":
                    return oneway ? 2 : 3;

                case "tertiary":
                    return 2;

                case "service":
                case "living_street":
                case "track":
                    return oneway ? 1 : 2;

                case "residential":
                case "unclassified":
                default:
                    return oneway ? 1 : 2;
            }
        }

        private static string NormalizeText(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? ""
                : value.Trim().ToLowerInvariant();
        }
    }
}
