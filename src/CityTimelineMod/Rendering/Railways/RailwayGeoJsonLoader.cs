using System;
using System.Collections.Generic;
using System.IO;
using CityTimelineMod.Importers;
using Newtonsoft.Json;
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

        private static readonly bool ValidateDirectParserParity = false;

        private static readonly HashSet<string> DirectRelevantKeys =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "railway",
                "service",
                "status",
                "abandoned",
                "abandoned:railway",
                "railway:abandoned",
                "disused",
                "disused:railway",
                "railway:disused",
                "proposed",
                "proposed:railway",
                "railway:proposed",
                "construction",
                "construction:railway",
                "railway:construction",
                "active",
                "usage",
                "tracks",
                "gauge",
                "bridge",
                "tunnel",
                "electrified",
                "name"
            };

        private sealed class DirectFeatureData
        {
            internal readonly Dictionary<string, string> Properties =
                new Dictionary<string, string>(StringComparer.Ordinal);

            internal readonly Dictionary<string, string> Tags =
                new Dictionary<string, string>(StringComparer.Ordinal);

            internal string GeometryType;

            internal readonly List<List<GeoPoint>> Lines =
                new List<List<GeoPoint>>();
        }

        internal static List<GeoRailwayLine> Load(string path)
        {
            var direct = LoadDirect(path);

            if (ValidateDirectParserParity)
            {
                var legacy = LoadLegacy(path);
                ValidateParserParity(
                    legacy,
                    direct,
                    20
                );
            }

            return direct;
        }

        private static List<GeoRailwayLine> LoadDirect(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException(path);

            var timer =
                System.Diagnostics.Stopwatch.StartNew();

            var result = new List<GeoRailwayLine>();

            string rootType = null;
            var featuresFound = false;
            var featureCount = 0;

            try
            {
                using (var stream = new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    1024 * 1024,
                    FileOptions.SequentialScan))
                using (var textReader = new StreamReader(stream))
                using (var reader = new JsonTextReader(textReader))
                {
                    reader.DateParseHandling =
                        DateParseHandling.None;

                    reader.FloatParseHandling =
                        FloatParseHandling.Double;

                    if (!reader.Read() ||
                        reader.TokenType != JsonToken.StartObject)
                    {
                        throw new InvalidDataException(
                            "Railway GeoJSON root is not an object"
                        );
                    }

                    while (reader.Read())
                    {
                        if (reader.TokenType ==
                            JsonToken.EndObject)
                        {
                            break;
                        }

                        if (reader.TokenType !=
                            JsonToken.PropertyName)
                        {
                            continue;
                        }

                        var propertyName =
                            reader.Value != null
                                ? reader.Value.ToString()
                                : string.Empty;

                        if (!reader.Read())
                            break;

                        if (string.Equals(
                            propertyName,
                            "type",
                            StringComparison.Ordinal))
                        {
                            rootType =
                                ReadDirectStringValue(reader);

                            continue;
                        }

                        if (string.Equals(
                            propertyName,
                            "features",
                            StringComparison.Ordinal))
                        {
                            if (reader.TokenType !=
                                JsonToken.StartArray)
                            {
                                throw new InvalidDataException(
                                    "Railway GeoJSON has no features array"
                                );
                            }

                            featuresFound = true;

                            while (reader.Read())
                            {
                                if (reader.TokenType ==
                                    JsonToken.EndArray)
                                {
                                    break;
                                }

                                featureCount++;

                                if (reader.TokenType !=
                                    JsonToken.StartObject)
                                {
                                    SkipDirectValue(reader);
                                    continue;
                                }

                                var feature =
                                    new DirectFeatureData();

                                ReadDirectFeature(
                                    reader,
                                    feature
                                );

                                AddDirectFeature(
                                    feature,
                                    result
                                );
                            }

                            continue;
                        }

                        SkipDirectValue(reader);
                    }
                }
            }
            catch (InvalidDataException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidDataException(
                    "Invalid railway GeoJSON",
                    ex
                );
            }

            if (!string.Equals(
                rootType,
                "FeatureCollection",
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "Railway GeoJSON is not a FeatureCollection"
                );
            }

            if (!featuresFound)
            {
                throw new InvalidDataException(
                    "Railway GeoJSON has no features array"
                );
            }

            timer.Stop();

            CityTimelineMod.Util.Log.Info(
                "RailwayGeoJson direct profile: features=" +
                featureCount +
                ", lines=" +
                result.Count +
                ", totalMs=" +
                timer.ElapsedMilliseconds
            );

            return result;
        }

        private static void ReadDirectFeature(
            JsonTextReader reader,
            DirectFeatureData data)
        {
            while (reader.Read())
            {
                if (reader.TokenType ==
                    JsonToken.EndObject)
                {
                    break;
                }

                if (reader.TokenType !=
                    JsonToken.PropertyName)
                {
                    continue;
                }

                var propertyName =
                    reader.Value != null
                        ? reader.Value.ToString()
                        : string.Empty;

                if (!reader.Read())
                    break;

                if (string.Equals(
                    propertyName,
                    "properties",
                    StringComparison.Ordinal))
                {
                    data.Properties.Clear();
                    data.Tags.Clear();

                    if (reader.TokenType ==
                        JsonToken.StartObject)
                    {
                        ReadDirectProperties(
                            reader,
                            data
                        );
                    }
                    else
                    {
                        SkipDirectValue(reader);
                    }

                    continue;
                }

                if (string.Equals(
                    propertyName,
                    "geometry",
                    StringComparison.Ordinal))
                {
                    data.GeometryType = null;
                    data.Lines.Clear();

                    if (reader.TokenType ==
                        JsonToken.StartObject)
                    {
                        ReadDirectGeometry(
                            reader,
                            data
                        );
                    }
                    else
                    {
                        SkipDirectValue(reader);
                    }

                    continue;
                }

                SkipDirectValue(reader);
            }
        }

        private static void ReadDirectProperties(
            JsonTextReader reader,
            DirectFeatureData data)
        {
            while (reader.Read())
            {
                if (reader.TokenType ==
                    JsonToken.EndObject)
                {
                    break;
                }

                if (reader.TokenType !=
                    JsonToken.PropertyName)
                {
                    continue;
                }

                var propertyName =
                    reader.Value != null
                        ? reader.Value.ToString()
                        : string.Empty;

                if (!reader.Read())
                    break;

                if (string.Equals(
                    propertyName,
                    "tags",
                    StringComparison.Ordinal))
                {
                    data.Tags.Clear();

                    if (reader.TokenType ==
                        JsonToken.StartObject)
                    {
                        ReadDirectTags(
                            reader,
                            data.Tags
                        );
                    }
                    else
                    {
                        SkipDirectValue(reader);
                    }

                    continue;
                }

                if (DirectRelevantKeys.Contains(
                    propertyName))
                {
                    data.Properties[propertyName] =
                        ReadDirectStringValue(reader);

                    continue;
                }

                SkipDirectValue(reader);
            }
        }

        private static void ReadDirectTags(
            JsonTextReader reader,
            Dictionary<string, string> tags)
        {
            while (reader.Read())
            {
                if (reader.TokenType ==
                    JsonToken.EndObject)
                {
                    break;
                }

                if (reader.TokenType !=
                    JsonToken.PropertyName)
                {
                    continue;
                }

                var propertyName =
                    reader.Value != null
                        ? reader.Value.ToString()
                        : string.Empty;

                if (!reader.Read())
                    break;

                if (DirectRelevantKeys.Contains(
                    propertyName))
                {
                    tags[propertyName] =
                        ReadDirectStringValue(reader);

                    continue;
                }

                SkipDirectValue(reader);
            }
        }

        private static void ReadDirectGeometry(
            JsonTextReader reader,
            DirectFeatureData data)
        {
            JArray deferredCoordinates = null;

            while (reader.Read())
            {
                if (reader.TokenType ==
                    JsonToken.EndObject)
                {
                    break;
                }

                if (reader.TokenType !=
                    JsonToken.PropertyName)
                {
                    continue;
                }

                var propertyName =
                    reader.Value != null
                        ? reader.Value.ToString()
                        : string.Empty;

                if (!reader.Read())
                    break;

                if (string.Equals(
                    propertyName,
                    "type",
                    StringComparison.Ordinal))
                {
                    data.GeometryType =
                        ReadDirectStringValue(reader);

                    continue;
                }

                if (!string.Equals(
                    propertyName,
                    "coordinates",
                    StringComparison.Ordinal))
                {
                    SkipDirectValue(reader);
                    continue;
                }

                data.Lines.Clear();

                if (reader.TokenType !=
                    JsonToken.StartArray)
                {
                    SkipDirectValue(reader);
                    continue;
                }

                if (string.Equals(
                    data.GeometryType,
                    "LineString",
                    StringComparison.OrdinalIgnoreCase))
                {
                    var line =
                        ReadDirectLineString(reader);

                    if (line.Count >= 2)
                        data.Lines.Add(line);

                    continue;
                }

                if (string.Equals(
                    data.GeometryType,
                    "MultiLineString",
                    StringComparison.OrdinalIgnoreCase))
                {
                    ReadDirectMultiLineString(
                        reader,
                        data.Lines
                    );

                    continue;
                }

                // Geometry type can legally appear after coordinates.
                // In that uncommon ordering, materialize only coordinates,
                // never the whole feature or document.
                deferredCoordinates =
                    JToken.ReadFrom(reader) as JArray;
            }

            if (deferredCoordinates == null)
                return;

            data.Lines.Clear();

            if (string.Equals(
                data.GeometryType,
                "LineString",
                StringComparison.OrdinalIgnoreCase))
            {
                var line =
                    ParseLineString(
                        deferredCoordinates
                    );

                if (line.Count >= 2)
                    data.Lines.Add(line);

                return;
            }

            if (!string.Equals(
                data.GeometryType,
                "MultiLineString",
                StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            foreach (var token in deferredCoordinates)
            {
                var line =
                    ParseLineString(
                        token as JArray
                    );

                if (line.Count >= 2)
                    data.Lines.Add(line);
            }
        }

        private static List<GeoPoint> ReadDirectLineString(
            JsonTextReader reader)
        {
            var result = new List<GeoPoint>();

            while (reader.Read())
            {
                if (reader.TokenType ==
                    JsonToken.EndArray)
                {
                    break;
                }

                if (reader.TokenType !=
                    JsonToken.StartArray)
                {
                    SkipDirectValue(reader);
                    continue;
                }

                GeoPoint point;

                if (TryReadDirectCoordinate(
                    reader,
                    out point))
                {
                    result.Add(point);
                }
            }

            return result;
        }

        private static void ReadDirectMultiLineString(
            JsonTextReader reader,
            List<List<GeoPoint>> result)
        {
            while (reader.Read())
            {
                if (reader.TokenType ==
                    JsonToken.EndArray)
                {
                    break;
                }

                if (reader.TokenType !=
                    JsonToken.StartArray)
                {
                    SkipDirectValue(reader);
                    continue;
                }

                var line =
                    ReadDirectLineString(reader);

                if (line.Count >= 2)
                    result.Add(line);
            }
        }

        private static bool TryReadDirectCoordinate(
            JsonTextReader reader,
            out GeoPoint point)
        {
            point = null;

            var index = 0;
            var lon = 0d;
            var lat = 0d;
            var lonValid = false;
            var latValid = false;
            var invalid = false;

            while (reader.Read())
            {
                if (reader.TokenType ==
                    JsonToken.EndArray)
                {
                    break;
                }

                if (reader.TokenType ==
                        JsonToken.StartArray ||
                    reader.TokenType ==
                        JsonToken.StartObject)
                {
                    if (index < 2)
                        invalid = true;

                    SkipDirectValue(reader);
                    index++;
                    continue;
                }

                if (index == 0)
                {
                    double value;

                    if (TryReadDirectNumber(
                        reader,
                        out value))
                    {
                        lon = value;
                        lonValid = true;
                    }
                    else
                    {
                        invalid = true;
                    }
                }
                else if (index == 1)
                {
                    double value;

                    if (TryReadDirectNumber(
                        reader,
                        out value))
                    {
                        lat = value;
                        latValid = true;
                    }
                    else
                    {
                        invalid = true;
                    }
                }

                index++;
            }

            if (invalid ||
                !lonValid ||
                !latValid)
            {
                return false;
            }

            if (lat < -90d ||
                lat > 90d ||
                lon < -180d ||
                lon > 180d)
            {
                return false;
            }

            point = new GeoPoint(
                lon,
                lat
            );

            return true;
        }

        private static bool TryReadDirectNumber(
            JsonTextReader reader,
            out double value)
        {
            value = 0d;

            if (reader.TokenType ==
                    JsonToken.Null ||
                reader.TokenType ==
                    JsonToken.Undefined ||
                reader.TokenType ==
                    JsonToken.StartArray ||
                reader.TokenType ==
                    JsonToken.StartObject)
            {
                return false;
            }

            try
            {
                value = Convert.ToDouble(
                    reader.Value,
                    System.Globalization.CultureInfo.InvariantCulture
                );

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string ReadDirectStringValue(
            JsonTextReader reader)
        {
            if (reader.TokenType ==
                    JsonToken.Null ||
                reader.TokenType ==
                    JsonToken.Undefined)
            {
                return null;
            }

            string value;

            if (reader.TokenType ==
                    JsonToken.StartObject ||
                reader.TokenType ==
                    JsonToken.StartArray)
            {
                var token =
                    JToken.ReadFrom(reader);

                value =
                    token != null
                        ? token.ToString()
                        : null;
            }
            else
            {
                var token =
                    new JValue(reader.Value);

                value = token.ToString();
            }

            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
        }

        private static void SkipDirectValue(
            JsonTextReader reader)
        {
            if (reader.TokenType ==
                    JsonToken.StartObject ||
                reader.TokenType ==
                    JsonToken.StartArray)
            {
                reader.Skip();
            }
        }

        private static string ReadDirectProperty(
            DirectFeatureData data,
            string key)
        {
            string value;

            if (data.Properties.TryGetValue(
                    key,
                    out value) &&
                !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            return data.Tags.TryGetValue(
                key,
                out value)
                    ? value
                    : null;
        }

        private static bool ReadDirectPresence(
            DirectFeatureData data,
            string key)
        {
            var value =
                ReadDirectProperty(
                    data,
                    key
                );

            return value != null &&
                   IsPresent(value);
        }

        private static bool IsDirectInactive(
            DirectFeatureData data)
        {
            var status =
                Normalize(
                    ReadDirectProperty(
                        data,
                        "status"
                    )
                );

            if (status == "abandoned" ||
                status == "disused" ||
                status == "proposed" ||
                status == "construction")
            {
                return true;
            }

            var lifecycle =
                new[]
                {
                    "abandoned",
                    "disused",
                    "proposed",
                    "construction"
                };

            for (var i = 0;
                 i < lifecycle.Length;
                 i++)
            {
                var key = lifecycle[i];

                if (ReadDirectPresence(
                        data,
                        key) ||
                    ReadDirectPresence(
                        data,
                        key + ":railway") ||
                    ReadDirectPresence(
                        data,
                        "railway:" + key))
                {
                    return true;
                }
            }

            var active =
                ReadDirectProperty(
                    data,
                    "active"
                );

            return active != null &&
                   !IsPresent(active);
        }

        private static void AddDirectFeature(
            DirectFeatureData data,
            List<GeoRailwayLine> result)
        {
            if (IsDirectInactive(data))
                return;

            var railway =
                ReadDirectProperty(
                    data,
                    "railway"
                );

            var service =
                ReadDirectProperty(
                    data,
                    "service"
                );

            RailwayCategory category;

            if (!TryClassify(
                railway,
                service,
                out category))
            {
                return;
            }

            for (var i = 0;
                 i < data.Lines.Count;
                 i++)
            {
                var points = data.Lines[i];

                if (points == null ||
                    points.Count < 2)
                {
                    continue;
                }

                result.Add(
                    new GeoRailwayLine(
                        points,
                        category,
                        Normalize(railway),
                        ReadDirectProperty(
                            data,
                            "usage"
                        ),
                        SupportedServices.Contains(
                            Normalize(service)
                        )
                            ? Normalize(service)
                            : null,
                        ReadDirectProperty(
                            data,
                            "tracks"
                        ),
                        ReadDirectProperty(
                            data,
                            "gauge"
                        ),
                        IsPresent(
                            ReadDirectProperty(
                                data,
                                "bridge"
                            )
                        ),
                        IsPresent(
                            ReadDirectProperty(
                                data,
                                "tunnel"
                            )
                        ),
                        ReadDirectProperty(
                            data,
                            "electrified"
                        ),
                        ReadDirectProperty(
                            data,
                            "name"
                        )
                    )
                );
            }
        }

        private static void ValidateParserParity(
            List<GeoRailwayLine> legacy,
            List<GeoRailwayLine> direct,
            int maxReported)
        {
            var mismatches = 0;
            var reported = 0;

            if (legacy.Count != direct.Count)
            {
                mismatches++;

                CityTimelineMod.Util.Log.Error(
                    "Railway parser diff: line count legacy=" +
                    legacy.Count +
                    " direct=" +
                    direct.Count
                );

                reported++;
            }

            var count =
                Math.Min(
                    legacy.Count,
                    direct.Count
                );

            for (var i = 0;
                 i < count;
                 i++)
            {
                var difference =
                    DescribeLineDifference(
                        legacy[i],
                        direct[i]
                    );

                if (difference == null)
                    continue;

                mismatches++;

                if (reported <
                    maxReported)
                {
                    CityTimelineMod.Util.Log.Error(
                        "Railway parser diff: index=" +
                        i +
                        ", " +
                        difference
                    );

                    reported++;
                }
            }

            CityTimelineMod.Util.Log.Info(
                "Railway parser parity: legacyLines=" +
                legacy.Count +
                ", directLines=" +
                direct.Count +
                ", mismatches=" +
                mismatches
            );
        }

        private static string DescribeLineDifference(
            GeoRailwayLine legacy,
            GeoRailwayLine direct)
        {
            if (legacy == null ||
                direct == null)
            {
                return legacy == direct
                    ? null
                    : "null line mismatch";
            }

            if (legacy.Category != direct.Category)
                return "Category mismatch";

            if (!string.Equals(
                legacy.Railway,
                direct.Railway,
                StringComparison.Ordinal))
            {
                return "Railway mismatch";
            }

            if (!string.Equals(
                legacy.Usage,
                direct.Usage,
                StringComparison.Ordinal))
            {
                return "Usage mismatch";
            }

            if (!string.Equals(
                legacy.Service,
                direct.Service,
                StringComparison.Ordinal))
            {
                return "Service mismatch";
            }

            if (!string.Equals(
                legacy.Tracks,
                direct.Tracks,
                StringComparison.Ordinal))
            {
                return "Tracks mismatch";
            }

            if (!string.Equals(
                legacy.Gauge,
                direct.Gauge,
                StringComparison.Ordinal))
            {
                return "Gauge mismatch";
            }

            if (legacy.Bridge != direct.Bridge)
                return "Bridge mismatch";

            if (legacy.Tunnel != direct.Tunnel)
                return "Tunnel mismatch";

            if (!string.Equals(
                legacy.Electrified,
                direct.Electrified,
                StringComparison.Ordinal))
            {
                return "Electrified mismatch";
            }

            if (!string.Equals(
                legacy.Name,
                direct.Name,
                StringComparison.Ordinal))
            {
                return "Name mismatch";
            }

            if (legacy.Points.Count !=
                direct.Points.Count)
            {
                return
                    "Points.Count legacy=" +
                    legacy.Points.Count +
                    " direct=" +
                    direct.Points.Count;
            }

            for (var i = 0;
                 i < legacy.Points.Count;
                 i++)
            {
                var a = legacy.Points[i];
                var b = direct.Points[i];

                if (!a.Lon.Equals(b.Lon) ||
                    !a.Lat.Equals(b.Lat))
                {
                    return
                        "point[" +
                        i +
                        "] coordinate mismatch";
                }
            }

            return null;
        }
        private static List<GeoRailwayLine> LoadLegacy(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException(path);

            var totalTimer =
                System.Diagnostics.Stopwatch.StartNew();

            JObject root;

            var domTimer =
                System.Diagnostics.Stopwatch.StartNew();

            try
            {
                root = JObject.Parse(File.ReadAllText(path));
            }
            catch (Exception ex)
            {
                throw new InvalidDataException("Invalid railway GeoJSON", ex);
            }

            domTimer.Stop();

            if (!string.Equals(ReadString(root, "type"), "FeatureCollection", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Railway GeoJSON is not a FeatureCollection");

            var features = root["features"] as JArray;
            if (features == null)
                throw new InvalidDataException("Railway GeoJSON has no features array");

            var result = new List<GeoRailwayLine>();

            var conversionTimer =
                System.Diagnostics.Stopwatch.StartNew();

            foreach (var token in features)
            {
                var feature = token as JObject;
                if (feature == null)
                    continue;

                AddFeature(feature, result);
            }

            conversionTimer.Stop();
            totalTimer.Stop();

            CityTimelineMod.Util.Log.Info(
                "RailwayGeoJson profile: features=" +
                features.Count +
                ", lines=" +
                result.Count +
                ", domMs=" +
                domTimer.ElapsedMilliseconds +
                ", conversionMs=" +
                conversionTimer.ElapsedMilliseconds +
                ", totalMs=" +
                totalTimer.ElapsedMilliseconds
            );

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
