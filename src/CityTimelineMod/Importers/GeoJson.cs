using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CityTimelineMod.Importers
{
    internal sealed class GeoPoint
    {
        internal double Lon;
        internal double Lat;

        internal GeoPoint(double lon, double lat)
        {
            Lon = lon;
            Lat = lat;
        }
    }

    internal sealed class GeoLineAnalysis
    {
        internal int FeatureCount;
        internal int LineGeometryCount;
        internal int PointCount;
        internal int UnsupportedGeometryCount;
        internal GeoPoint FirstPoint;
        internal GeoPoint LastPoint;
    }

    internal sealed class GeoLineLoadResult
    {
        internal GeoLineAnalysis Analysis;
        internal List<List<GeoPoint>> Geometries;

        internal GeoLineLoadResult(
            GeoLineAnalysis analysis,
            List<List<GeoPoint>> geometries)
        {
            Analysis = analysis;
            Geometries = geometries;
        }
    }

    internal sealed class GeoPolygonOutlineLoadResult
    {
        internal int FeatureCount;
        internal List<List<GeoPoint>> Outlines;

        internal GeoPolygonOutlineLoadResult(
            int featureCount,
            List<List<GeoPoint>> outlines)
        {
            FeatureCount = featureCount;
            Outlines = outlines;
        }
    }
    internal sealed class GeoRoadLine
    {
        internal List<GeoPoint> Points;
        internal string Highway;
        internal string Name;
        internal bool IsPath;

        internal string Oneway;
        internal int? Lanes;
        internal int? TargetLaneCount;
        internal string MaxSpeed;
        internal string Surface;
        internal bool Bridge;
        internal bool Tunnel;
        internal bool Roundabout;
        internal string Ref;

        internal GeoRoadLine(
            List<GeoPoint> points,
            string highway,
            string name,
            bool isPath = false,
            string oneway = null,
            int? lanes = null,
            int? targetLaneCount = null,
            string maxSpeed = null,
            string surface = null,
            bool bridge = false,
            bool tunnel = false,
            bool roundabout = false,
            string refValue = null)
        {
            Points = points ?? new List<GeoPoint>();
            Highway = highway;
            Name = name;
            IsPath = isPath;

            Oneway = oneway;
            Lanes = lanes;
            TargetLaneCount = targetLaneCount;
            MaxSpeed = maxSpeed;
            Surface = surface;
            Bridge = bridge;
            Tunnel = tunnel;
            Roundabout = roundabout;
            Ref = refValue;
        }
    }

    internal sealed class GeoZoningPolygon
    {
        internal List<List<GeoPoint>> Rings;
        internal string Zone;
        internal string Cs2;
        internal string Name;

        internal GeoZoningPolygon(List<List<GeoPoint>> rings, string zone, string cs2, string name)
        {
            Rings = rings ?? new List<List<GeoPoint>>();
            Zone = zone;
            Cs2 = cs2;
            Name = name;
        }
    }

    internal static class GeoJson
    {
        private static readonly bool ValidateRoadParserParity = false;
        private const bool ValidateZoningParserParity = false;

        internal static int CountFeatures(string path)
        {
            return LoadFeatures(path).Count;
        }

        internal static GeoLineAnalysis AnalyzeLines(string path)
        {
            var features = LoadFeatures(path);

            var result = new GeoLineAnalysis();
            result.FeatureCount = features.Count;

            foreach (var feature in features)
            {
                var geometry = feature["geometry"] as JObject;

                if (geometry == null)
                {
                    result.UnsupportedGeometryCount++;
                    continue;
                }

                var type = geometry["type"] != null ? geometry["type"].ToString() : null;
                var coordinates = geometry["coordinates"];

                if (type == "LineString")
                {
                    var line = ParseLineString(coordinates as JArray);

                    if (line.Count >= 2)
                    {
                        result.LineGeometryCount++;
                        AddLineStats(result, line);
                    }
                    else
                    {
                        result.UnsupportedGeometryCount++;
                    }
                }
                else if (type == "MultiLineString")
                {
                    var multi = coordinates as JArray;

                    if (multi == null)
                    {
                        result.UnsupportedGeometryCount++;
                        continue;
                    }

                    foreach (var lineToken in multi)
                    {
                        var line = ParseLineString(lineToken as JArray);

                        if (line.Count >= 2)
                        {
                            result.LineGeometryCount++;
                            AddLineStats(result, line);
                        }
                        else
                        {
                            result.UnsupportedGeometryCount++;
                        }
                    }
                }
                else
                {
                    result.UnsupportedGeometryCount++;
                }
            }

            return result;
        }

        internal static List<List<GeoPoint>> LoadLineGeometries(string path)
        {
            var features = LoadFeatures(path);
            var result = new List<List<GeoPoint>>();

            foreach (var feature in features)
            {
                var geometry = feature["geometry"] as JObject;

                if (geometry == null)
                    continue;

                var type = geometry["type"] != null ? geometry["type"].ToString() : null;
                var coordinates = geometry["coordinates"];

                if (type == "LineString")
                {
                    var line = ParseLineString(coordinates as JArray);

                    if (line.Count >= 2)
                        result.Add(line);
                }
                else if (type == "MultiLineString")
                {
                    var multi = coordinates as JArray;

                    if (multi == null)
                        continue;

                    foreach (var lineToken in multi)
                    {
                        var line = ParseLineString(lineToken as JArray);

                        if (line.Count >= 2)
                            result.Add(line);
                    }
                }
            }

            return result;
        }

        private sealed class RoadFeatureData
        {
            internal string Highway;
            internal string TagsHighway;
            internal string SourceTag;
            internal string Name;

            internal string Oneway;
            internal string TagsOneway;
            internal string ImportOneway;

            internal string Lanes;
            internal string TagsLanes;
            internal string TargetLaneCount;

            internal string MaxSpeed;
            internal string TagsMaxSpeed;
            internal string ImportMaxSpeed;

            internal string Surface;
            internal string TagsSurface;
            internal string ImportSurface;

            internal bool Bridge;
            internal bool TagsBridge;
            internal bool ImportBridge;

            internal bool Tunnel;
            internal bool TagsTunnel;
            internal bool ImportTunnel;

            internal bool ImportRoundabout;

            internal string Junction;
            internal string TagsJunction;

            internal string Ref;
            internal string TagsRef;
        }

        private sealed class ZoningFeatureData
        {
            internal string Zone;
            internal string Cs2;
            internal string Name;
            internal string GeometryType;
            internal List<List<List<GeoPoint>>> Polygons;
        }

        internal static GeoLineLoadResult LoadLineGeometriesWithAnalysis(
            string path)
        {
            var features = LoadFeatures(path);

            var analysis = new GeoLineAnalysis();
            analysis.FeatureCount = features.Count;

            var geometries = new List<List<GeoPoint>>();

            foreach (var feature in features)
            {
                var geometry = feature["geometry"] as JObject;

                if (geometry == null)
                {
                    analysis.UnsupportedGeometryCount++;
                    continue;
                }

                var type = geometry["type"] != null
                    ? geometry["type"].ToString()
                    : null;

                var coordinates = geometry["coordinates"];

                if (type == "LineString")
                {
                    var line = ParseLineString(coordinates as JArray);

                    if (line.Count >= 2)
                    {
                        analysis.LineGeometryCount++;
                        AddLineStats(analysis, line);
                        geometries.Add(line);
                    }
                    else
                    {
                        analysis.UnsupportedGeometryCount++;
                    }
                }
                else if (type == "MultiLineString")
                {
                    var multi = coordinates as JArray;

                    if (multi == null)
                    {
                        analysis.UnsupportedGeometryCount++;
                        continue;
                    }

                    foreach (var lineToken in multi)
                    {
                        var line = ParseLineString(lineToken as JArray);

                        if (line.Count >= 2)
                        {
                            analysis.LineGeometryCount++;
                            AddLineStats(analysis, line);
                            geometries.Add(line);
                        }
                        else
                        {
                            analysis.UnsupportedGeometryCount++;
                        }
                    }
                }
                else
                {
                    analysis.UnsupportedGeometryCount++;
                }
            }

            return new GeoLineLoadResult(
                analysis,
                geometries
            );
        }

        internal static GeoPolygonOutlineLoadResult LoadPolygonOutlinesWithCount(
            string path)
        {
            var features = LoadFeatures(path);
            var result = new List<List<GeoPoint>>();

            foreach (var feature in features)
            {
                var geometry = feature["geometry"] as JObject;

                if (geometry == null)
                    continue;

                var type = geometry["type"] != null
                    ? geometry["type"].ToString()
                    : null;

                var coordinates =
                    geometry["coordinates"] as JArray;

                if (coordinates == null)
                    continue;

                if (type == "Polygon")
                {
                    AddPolygonRings(
                        coordinates,
                        result
                    );
                }
                else if (type == "MultiPolygon")
                {
                    foreach (var polygonToken in coordinates)
                    {
                        AddPolygonRings(
                            polygonToken as JArray,
                            result
                        );
                    }
                }
            }

            return new GeoPolygonOutlineLoadResult(
                features.Count,
                result
            );
        }
        internal static List<GeoRoadLine> LoadRoadGeometries(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException(path);

            var result = new List<GeoRoadLine>();

            var profileTotal = System.Diagnostics.Stopwatch.StartNew();
            long directParseTicks = 0;
            var featureCount = 0;

            string rootType = null;
            var featuresFound = false;

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
                reader.DateParseHandling = DateParseHandling.None;
                reader.FloatParseHandling = FloatParseHandling.Double;

                if (!reader.Read() || reader.TokenType != JsonToken.StartObject)
                    throw new InvalidDataException("Invalid GeoJSON root object");

                while (reader.Read())
                {
                    if (reader.TokenType == JsonToken.EndObject)
                        break;

                    if (reader.TokenType != JsonToken.PropertyName)
                        continue;

                    var propertyName = reader.Value != null
                        ? reader.Value.ToString()
                        : string.Empty;

                    if (!reader.Read())
                        break;

                    if (string.Equals(
                        propertyName,
                        "type",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        rootType = ReadJsonStringValue(reader);
                        continue;
                    }

                    if (string.Equals(
                        propertyName,
                        "features",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        if (reader.TokenType != JsonToken.StartArray)
                            throw new InvalidDataException(
                                "'features' is missing or not an array"
                            );

                        featuresFound = true;

                        while (reader.Read())
                        {
                            if (reader.TokenType == JsonToken.EndArray)
                                break;

                            if (reader.TokenType != JsonToken.StartObject)
                            {
                                SkipJsonValue(reader);
                                continue;
                            }

                            var start =
                                System.Diagnostics.Stopwatch.GetTimestamp();

                            ReadRoadFeature(reader, result);

                            directParseTicks +=
                                System.Diagnostics.Stopwatch.GetTimestamp() - start;

                            featureCount++;
                        }

                        continue;
                    }

                    SkipJsonValue(reader);
                }
            }

            if (!string.Equals(
                rootType,
                "FeatureCollection",
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Not a FeatureCollection");
            }

            if (!featuresFound)
                throw new InvalidDataException(
                    "'features' is missing or not an array"
                );

            profileTotal.Stop();

            var ticksToMs =
                1000.0 / System.Diagnostics.Stopwatch.Frequency;

            CityTimelineMod.Util.Log.Info(
                "GeoJson direct road parser profile: features=" +
                featureCount +
                ", lines=" +
                result.Count +
                ", totalMs=" +
                profileTotal.ElapsedMilliseconds +
                ", directParseMs=" +
                (long)(directParseTicks * ticksToMs)
            );
            if (ValidateRoadParserParity)
                ValidateRoadParserAgainstLegacy(path, result, 20);

            return result;
        }

        private static void ReadRoadFeature(
            JsonTextReader reader,
            List<GeoRoadLine> result)
        {
            var data = new RoadFeatureData();

            string geometryType = null;
            List<List<GeoPoint>> geometryLines = null;

            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.EndObject)
                    break;

                if (reader.TokenType != JsonToken.PropertyName)
                    continue;

                var propertyName = reader.Value != null
                    ? reader.Value.ToString()
                    : string.Empty;

                if (!reader.Read())
                    break;

                if (string.Equals(
                    propertyName,
                    "properties",
                    StringComparison.OrdinalIgnoreCase))
                {
                    if (reader.TokenType == JsonToken.StartObject)
                        ReadRoadProperties(reader, data);
                    else
                        SkipJsonValue(reader);

                    continue;
                }

                if (string.Equals(
                    propertyName,
                    "geometry",
                    StringComparison.OrdinalIgnoreCase))
                {
                    if (reader.TokenType == JsonToken.StartObject)
                    {
                        ReadRoadGeometry(
                            reader,
                            out geometryType,
                            out geometryLines
                        );
                    }
                    else
                    {
                        SkipJsonValue(reader);
                    }

                    continue;
                }

                SkipJsonValue(reader);
            }

            if (geometryLines == null)
                return;

            var highway = FirstNonEmpty(
                data.Highway,
                data.TagsHighway
            );

            if (string.IsNullOrWhiteSpace(highway))
            {
                highway = GetSourceTagValue(
                    data.SourceTag,
                    "highway"
                );
            }

            var oneway = FirstNonEmpty(
                data.Oneway,
                data.TagsOneway,
                data.ImportOneway
            );

            var lanes = ParseNullableInt(
                FirstNonEmpty(
                    data.Lanes,
                    data.TagsLanes
                )
            );

            var targetLaneCount =
                ParseNullableInt(data.TargetLaneCount);

            var maxSpeed = FirstNonEmpty(
                data.MaxSpeed,
                data.TagsMaxSpeed,
                data.ImportMaxSpeed
            );

            var surface = FirstNonEmpty(
                data.Surface,
                data.TagsSurface,
                data.ImportSurface
            );

            var bridge =
                data.Bridge ||
                data.TagsBridge ||
                data.ImportBridge;

            var tunnel =
                data.Tunnel ||
                data.TagsTunnel ||
                data.ImportTunnel;

            var junction = FirstNonEmpty(
                data.Junction,
                data.TagsJunction
            );

            var roundabout =
                data.ImportRoundabout ||
                string.Equals(
                    junction,
                    "roundabout",
                    StringComparison.OrdinalIgnoreCase
                );

            var refValue = FirstNonEmpty(
                data.Ref,
                data.TagsRef
            );

            if (string.Equals(
                geometryType,
                "LineString",
                StringComparison.Ordinal))
            {
                if (geometryLines.Count > 0)
                {
                    AddParsedRoadLine(
                        geometryLines[0],
                        result,
                        highway,
                        data.Name,
                        oneway,
                        lanes,
                        targetLaneCount,
                        maxSpeed,
                        surface,
                        bridge,
                        tunnel,
                        roundabout,
                        refValue
                    );
                }

                return;
            }

            if (!string.Equals(
                geometryType,
                "MultiLineString",
                StringComparison.Ordinal))
            {
                return;
            }

            foreach (var line in geometryLines)
            {
                AddParsedRoadLine(
                    line,
                    result,
                    highway,
                    data.Name,
                    oneway,
                    lanes,
                    targetLaneCount,
                    maxSpeed,
                    surface,
                    bridge,
                    tunnel,
                    roundabout,
                    refValue
                );
            }
        }

        private static void ReadRoadProperties(
            JsonTextReader reader,
            RoadFeatureData data)
        {
            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.EndObject)
                    break;

                if (reader.TokenType != JsonToken.PropertyName)
                    continue;

                var name = reader.Value != null
                    ? reader.Value.ToString()
                    : string.Empty;

                if (!reader.Read())
                    break;

                if (string.Equals(name, "highway", StringComparison.OrdinalIgnoreCase))
                    data.Highway = ReadJsonStringValue(reader);
                else if (string.Equals(name, "name", StringComparison.OrdinalIgnoreCase))
                    data.Name = ReadJsonStringValue(reader);
                else if (string.Equals(name, "sourceTag", StringComparison.OrdinalIgnoreCase))
                    data.SourceTag = ReadJsonStringValue(reader);
                else if (string.Equals(name, "oneway", StringComparison.OrdinalIgnoreCase))
                    data.Oneway = ReadJsonStringValue(reader);
                else if (string.Equals(name, "lanes", StringComparison.OrdinalIgnoreCase))
                    data.Lanes = ReadJsonStringValue(reader);
                else if (string.Equals(name, "maxspeed", StringComparison.OrdinalIgnoreCase))
                    data.MaxSpeed = ReadJsonStringValue(reader);
                else if (string.Equals(name, "surface", StringComparison.OrdinalIgnoreCase))
                    data.Surface = ReadJsonStringValue(reader);
                else if (string.Equals(name, "bridge", StringComparison.OrdinalIgnoreCase))
                    data.Bridge = ReadJsonBoolValue(reader);
                else if (string.Equals(name, "tunnel", StringComparison.OrdinalIgnoreCase))
                    data.Tunnel = ReadJsonBoolValue(reader);
                else if (string.Equals(name, "junction", StringComparison.OrdinalIgnoreCase))
                    data.Junction = ReadJsonStringValue(reader);
                else if (string.Equals(name, "ref", StringComparison.OrdinalIgnoreCase))
                    data.Ref = ReadJsonStringValue(reader);
                else if (string.Equals(name, "tags", StringComparison.OrdinalIgnoreCase))
                {
                    if (reader.TokenType == JsonToken.StartObject)
                        ReadRoadTags(reader, data);
                    else
                        SkipJsonValue(reader);
                }
                else if (string.Equals(name, "roadImport", StringComparison.OrdinalIgnoreCase))
                {
                    if (reader.TokenType == JsonToken.StartObject)
                        ReadRoadImport(reader, data);
                    else
                        SkipJsonValue(reader);
                }
                else
                {
                    SkipJsonValue(reader);
                }
            }
        }

        private static void ReadRoadTags(
            JsonTextReader reader,
            RoadFeatureData data)
        {
            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.EndObject)
                    break;

                if (reader.TokenType != JsonToken.PropertyName)
                    continue;

                var name = reader.Value != null
                    ? reader.Value.ToString()
                    : string.Empty;

                if (!reader.Read())
                    break;

                if (string.Equals(name, "highway", StringComparison.OrdinalIgnoreCase))
                    data.TagsHighway = ReadJsonStringValue(reader);
                else if (string.Equals(name, "oneway", StringComparison.OrdinalIgnoreCase))
                    data.TagsOneway = ReadJsonStringValue(reader);
                else if (string.Equals(name, "lanes", StringComparison.OrdinalIgnoreCase))
                    data.TagsLanes = ReadJsonStringValue(reader);
                else if (string.Equals(name, "maxspeed", StringComparison.OrdinalIgnoreCase))
                    data.TagsMaxSpeed = ReadJsonStringValue(reader);
                else if (string.Equals(name, "surface", StringComparison.OrdinalIgnoreCase))
                    data.TagsSurface = ReadJsonStringValue(reader);
                else if (string.Equals(name, "bridge", StringComparison.OrdinalIgnoreCase))
                    data.TagsBridge = ReadJsonBoolValue(reader);
                else if (string.Equals(name, "tunnel", StringComparison.OrdinalIgnoreCase))
                    data.TagsTunnel = ReadJsonBoolValue(reader);
                else if (string.Equals(name, "junction", StringComparison.OrdinalIgnoreCase))
                    data.TagsJunction = ReadJsonStringValue(reader);
                else if (string.Equals(name, "ref", StringComparison.OrdinalIgnoreCase))
                    data.TagsRef = ReadJsonStringValue(reader);
                else
                    SkipJsonValue(reader);
            }
        }

        private static void ReadRoadImport(
            JsonTextReader reader,
            RoadFeatureData data)
        {
            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.EndObject)
                    break;

                if (reader.TokenType != JsonToken.PropertyName)
                    continue;

                var name = reader.Value != null
                    ? reader.Value.ToString()
                    : string.Empty;

                if (!reader.Read())
                    break;

                if (string.Equals(name, "oneway", StringComparison.OrdinalIgnoreCase))
                    data.ImportOneway = ReadJsonStringValue(reader);
                else if (string.Equals(name, "targetLaneCount", StringComparison.OrdinalIgnoreCase))
                    data.TargetLaneCount = ReadJsonStringValue(reader);
                else if (string.Equals(name, "maxspeed", StringComparison.OrdinalIgnoreCase))
                    data.ImportMaxSpeed = ReadJsonStringValue(reader);
                else if (string.Equals(name, "surface", StringComparison.OrdinalIgnoreCase))
                    data.ImportSurface = ReadJsonStringValue(reader);
                else if (string.Equals(name, "bridge", StringComparison.OrdinalIgnoreCase))
                    data.ImportBridge = ReadJsonBoolValue(reader);
                else if (string.Equals(name, "tunnel", StringComparison.OrdinalIgnoreCase))
                    data.ImportTunnel = ReadJsonBoolValue(reader);
                else if (string.Equals(name, "roundabout", StringComparison.OrdinalIgnoreCase))
                    data.ImportRoundabout = ReadJsonBoolValue(reader);
                else
                    SkipJsonValue(reader);
            }
        }

        private static void ReadRoadGeometry(
            JsonTextReader reader,
            out string geometryType,
            out List<List<GeoPoint>> geometryLines)
        {
            geometryType = null;
            geometryLines = null;

            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.EndObject)
                    break;

                if (reader.TokenType != JsonToken.PropertyName)
                    continue;

                var name = reader.Value != null
                    ? reader.Value.ToString()
                    : string.Empty;

                if (!reader.Read())
                    break;

                if (string.Equals(
                    name,
                    "type",
                    StringComparison.OrdinalIgnoreCase))
                {
                    geometryType = ReadJsonStringValue(reader);
                    continue;
                }

                if (string.Equals(
                    name,
                    "coordinates",
                    StringComparison.OrdinalIgnoreCase))
                {
                    if (reader.TokenType == JsonToken.StartArray)
                        geometryLines = ReadRoadCoordinateLines(reader);
                    else
                        SkipJsonValue(reader);

                    continue;
                }

                SkipJsonValue(reader);
            }
        }

        private static List<List<GeoPoint>> ReadRoadCoordinateLines(
            JsonTextReader reader)
        {
            var result = new List<List<GeoPoint>>();

            var rootDepth = reader.Depth;

            List<GeoPoint> currentLine = null;

            int? pointArrayDepth = null;
            int lineArrayDepth = -1;
            bool shapeKnown = false;
            bool multiLine = false;

            var coordinateIndex = 0;
            var lon = 0.0;
            var lat = 0.0;
            var lonValid = false;
            var latValid = false;

            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.StartArray)
                {
                    if (shapeKnown &&
                        reader.Depth == pointArrayDepth.Value)
                    {
                        coordinateIndex = 0;
                        lonValid = false;
                        latValid = false;
                    }

                    continue;
                }

                if (reader.TokenType == JsonToken.EndArray)
                {
                    if (shapeKnown &&
                        reader.Depth == pointArrayDepth.Value)
                    {
                        if (lonValid && latValid)
                        {
                            if (currentLine == null)
                                currentLine = new List<GeoPoint>();

                            currentLine.Add(
                                new GeoPoint(lon, lat)
                            );
                        }

                        coordinateIndex = 0;
                        lonValid = false;
                        latValid = false;

                        continue;
                    }

                    if (shapeKnown &&
                        multiLine &&
                        reader.Depth == lineArrayDepth)
                    {
                        if (currentLine != null &&
                            currentLine.Count > 0)
                        {
                            result.Add(currentLine);
                        }

                        currentLine = null;
                        continue;
                    }

                    if (reader.Depth == rootDepth)
                    {
                        if (shapeKnown &&
                            !multiLine &&
                            currentLine != null &&
                            currentLine.Count > 0)
                        {
                            result.Add(currentLine);
                        }

                        break;
                    }

                    continue;
                }

                double number;

                if (!TryReadCoordinateNumber(reader, out number))
                    continue;

                var parentArrayDepth = reader.Depth - 1;

                if (!shapeKnown)
                {
                    pointArrayDepth = parentArrayDepth;

                    multiLine =
                        pointArrayDepth.Value >
                        rootDepth + 1;

                    lineArrayDepth = multiLine
                        ? pointArrayDepth.Value - 1
                        : rootDepth;

                    currentLine = new List<GeoPoint>();

                    shapeKnown = true;
                    coordinateIndex = 0;
                }

                if (parentArrayDepth != pointArrayDepth.Value)
                    continue;

                if (coordinateIndex == 0)
                {
                    lon = number;
                    lonValid = true;
                }
                else if (coordinateIndex == 1)
                {
                    lat = number;
                    latValid = true;
                }

                coordinateIndex++;
            }

            return result;
        }

        private static bool TryReadCoordinateNumber(
            JsonTextReader reader,
            out double value)
        {
            value = 0.0;

            if (reader.TokenType != JsonToken.Integer &&
                reader.TokenType != JsonToken.Float &&
                reader.TokenType != JsonToken.String)
            {
                return false;
            }

            if (reader.Value == null)
                return false;

            try
            {
                value = Convert.ToDouble(
                    reader.Value,
                    System.Globalization.CultureInfo.InvariantCulture
                );

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string ReadJsonStringValue(
            JsonTextReader reader)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;

            if (reader.TokenType == JsonToken.StartArray)
            {
                string first = null;

                while (reader.Read())
                {
                    if (reader.TokenType == JsonToken.EndArray)
                        break;

                    if (reader.TokenType == JsonToken.StartObject ||
                        reader.TokenType == JsonToken.StartArray)
                    {
                        SkipJsonValue(reader);
                        continue;
                    }

                    if (first == null && reader.Value != null)
                    {
                        var text = Convert.ToString(
                            reader.Value,
                            System.Globalization.CultureInfo.InvariantCulture
                        );

                        if (!string.IsNullOrWhiteSpace(text))
                            first = text;
                    }
                }

                return first;
            }

            if (reader.TokenType == JsonToken.StartObject)
            {
                SkipJsonValue(reader);
                return null;
            }

            if (reader.Value == null)
                return null;

            var value = Convert.ToString(
                reader.Value,
                System.Globalization.CultureInfo.InvariantCulture
            );

            return string.IsNullOrWhiteSpace(value)
                ? null
                : value;
        }

        private static bool ReadJsonBoolValue(
            JsonTextReader reader)
        {
            var value = ReadJsonStringValue(reader);

            if (string.IsNullOrWhiteSpace(value))
                return false;

            return
                string.Equals(
                    value,
                    "true",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    value,
                    "yes",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    value,
                    "1",
                    StringComparison.OrdinalIgnoreCase);
        }

        private static void SkipJsonValue(
            JsonTextReader reader)
        {
            if (reader.TokenType == JsonToken.StartObject ||
                reader.TokenType == JsonToken.StartArray)
            {
                reader.Skip();
            }
        }

        private static string FirstNonEmpty(
            params string[] values)
        {
            if (values == null)
                return null;

            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }

            return null;
        }

        private static int? ParseNullableInt(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            int value;

            if (int.TryParse(text, out value))
                return value;

            return null;
        }

        private static void AddParsedRoadLine(
            List<GeoPoint> line,
            List<GeoRoadLine> result,
            string highway,
            string name,
            string oneway,
            int? lanes,
            int? targetLaneCount,
            string maxSpeed,
            string surface,
            bool bridge,
            bool tunnel,
            bool roundabout,
            string refValue)
        {
            if (line == null || line.Count < 2)
                return;

            result.Add(
                new GeoRoadLine(
                    line,
                    highway,
                    name,
                    false,
                    oneway,
                    lanes,
                    targetLaneCount,
                    maxSpeed,
                    surface,
                    bridge,
                    tunnel,
                    roundabout,
                    refValue
                )
            );
        }

        private static void AddRoadFeature(
            JObject feature,
            List<GeoRoadLine> result)
        {
            if (feature == null || result == null)
                return;

            var properties = feature["properties"] as JObject;
            var tags = properties != null
                ? properties["tags"] as JObject
                : null;

            var highway = GetStringProperty(properties, "highway");

            if (string.IsNullOrWhiteSpace(highway))
                highway = GetStringProperty(tags, "highway");

            if (string.IsNullOrWhiteSpace(highway))
            {
                highway = GetSourceTagValue(
                    GetStringProperty(properties, "sourceTag"),
                    "highway"
                );
            }

            var name = GetStringProperty(properties, "name");

            var roadImport = properties != null
                ? properties["roadImport"] as JObject
                : null;

            var oneway = GetStringProperty(properties, "oneway");

            if (string.IsNullOrWhiteSpace(oneway))
                oneway = GetStringProperty(tags, "oneway");

            if (string.IsNullOrWhiteSpace(oneway))
                oneway = GetStringProperty(roadImport, "oneway");

            var lanes = GetIntProperty(properties, "lanes");

            if (!lanes.HasValue)
                lanes = GetIntProperty(tags, "lanes");

            var targetLaneCount =
                GetIntProperty(roadImport, "targetLaneCount");

            var maxSpeed = GetStringProperty(properties, "maxspeed");

            if (string.IsNullOrWhiteSpace(maxSpeed))
                maxSpeed = GetStringProperty(tags, "maxspeed");

            if (string.IsNullOrWhiteSpace(maxSpeed))
                maxSpeed = GetStringProperty(roadImport, "maxspeed");

            var surface = GetStringProperty(properties, "surface");

            if (string.IsNullOrWhiteSpace(surface))
                surface = GetStringProperty(tags, "surface");

            if (string.IsNullOrWhiteSpace(surface))
                surface = GetStringProperty(roadImport, "surface");

            var bridge =
                GetBoolProperty(properties, "bridge") ||
                GetBoolProperty(tags, "bridge") ||
                GetBoolProperty(roadImport, "bridge");

            var tunnel =
                GetBoolProperty(properties, "tunnel") ||
                GetBoolProperty(tags, "tunnel") ||
                GetBoolProperty(roadImport, "tunnel");

            var roundabout =
                GetBoolProperty(roadImport, "roundabout");

            if (!roundabout)
            {
                var junction =
                    GetStringProperty(properties, "junction");

                if (string.IsNullOrWhiteSpace(junction))
                    junction = GetStringProperty(tags, "junction");

                roundabout = string.Equals(
                    junction,
                    "roundabout",
                    StringComparison.OrdinalIgnoreCase
                );
            }

            var refValue =
                GetStringProperty(properties, "ref");

            if (string.IsNullOrWhiteSpace(refValue))
                refValue = GetStringProperty(tags, "ref");

            var geometry = feature["geometry"] as JObject;

            if (geometry == null)
                return;

            var type = geometry["type"] != null
                ? geometry["type"].ToString()
                : null;

            var coordinates = geometry["coordinates"];

            if (type == "LineString")
            {
                AddRoadLine(
                    coordinates as JArray,
                    result,
                    highway,
                    name,
                    oneway,
                    lanes,
                    targetLaneCount,
                    maxSpeed,
                    surface,
                    bridge,
                    tunnel,
                    roundabout,
                    refValue
                );

                return;
            }

            if (type != "MultiLineString")
                return;

            var multi = coordinates as JArray;

            if (multi == null)
                return;

            foreach (var lineToken in multi)
            {
                AddRoadLine(
                    lineToken as JArray,
                    result,
                    highway,
                    name,
                    oneway,
                    lanes,
                    targetLaneCount,
                    maxSpeed,
                    surface,
                    bridge,
                    tunnel,
                    roundabout,
                    refValue
                );
            }
        }

        private static void AddRoadLine(
            JArray coordinates,
            List<GeoRoadLine> result,
            string highway,
            string name,
            string oneway,
            int? lanes,
            int? targetLaneCount,
            string maxSpeed,
            string surface,
            bool bridge,
            bool tunnel,
            bool roundabout,
            string refValue)
        {
            var line = ParseLineString(coordinates);

            if (line.Count < 2)
                return;

            result.Add(
                new GeoRoadLine(
                    line,
                    highway,
                    name,
                    false,
                    oneway,
                    lanes,
                    targetLaneCount,
                    maxSpeed,
                    surface,
                    bridge,
                    tunnel,
                    roundabout,
                    refValue
                )
            );
        }

        // -------- Ancien loader Zoning (DOM) renommé en Legacy --------
        internal static List<GeoZoningPolygon> LoadZoningPolygonsLegacy(string path)
        {
            var totalTimer = System.Diagnostics.Stopwatch.StartNew();

            var domTimer = System.Diagnostics.Stopwatch.StartNew();
            var features = LoadFeatures(path);
            domTimer.Stop();

            var result = new List<GeoZoningPolygon>();

            var conversionTimer = System.Diagnostics.Stopwatch.StartNew();

            foreach (var feature in features)
            {
                var properties = feature["properties"] as JObject;

                var zone = GetStringProperty(properties, "zone");
                var cs2 = GetStringProperty(properties, "cs2");
                var name = GetStringProperty(properties, "name");

                var geometry = feature["geometry"] as JObject;

                if (geometry == null)
                    continue;

                var type = geometry["type"] != null
                    ? geometry["type"].ToString()
                    : null;

                var coordinates = geometry["coordinates"] as JArray;

                if (coordinates == null)
                    continue;

                if (type == "Polygon")
                {
                    var rings = ParsePolygonRings(coordinates);

                    if (rings.Count > 0)
                        result.Add(
                            new GeoZoningPolygon(
                                rings,
                                zone,
                                cs2,
                                name
                            )
                        );
                }
                else if (type == "MultiPolygon")
                {
                    foreach (var polygonToken in coordinates)
                    {
                        var rings =
                            ParsePolygonRings(
                                polygonToken as JArray
                            );

                        if (rings.Count > 0)
                        {
                            result.Add(
                                new GeoZoningPolygon(
                                    rings,
                                    zone,
                                    cs2,
                                    name
                                )
                            );
                        }
                    }
                }
            }

            conversionTimer.Stop();
            totalTimer.Stop();

            CityTimelineMod.Util.Log.Info(
                "GeoJson zoning legacy profile: features=" +
                features.Count +
                ", polygons=" +
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

        // -------- Nouveau loader Zoning (streaming direct) --------
        internal static List<GeoZoningPolygon> LoadZoningPolygons(string path)
        {
            return LoadZoningPolygons(path, null);
        }

        internal static List<GeoZoningPolygon> LoadZoningPolygons(
            string path,
            Action<string, string> onFeature)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException(path);

            var timer = System.Diagnostics.Stopwatch.StartNew();
            var result = new List<GeoZoningPolygon>();

            string rootType = null;
            var featuresFound = false;
            var featureCount = 0;

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
                reader.DateParseHandling = DateParseHandling.None;
                reader.FloatParseHandling = FloatParseHandling.Double;

                if (!reader.Read() || reader.TokenType != JsonToken.StartObject)
                    throw new InvalidDataException("Invalid zoning GeoJSON root object");

                while (reader.Read())
                {
                    if (reader.TokenType == JsonToken.EndObject)
                        break;

                    if (reader.TokenType != JsonToken.PropertyName)
                        continue;

                    var propertyName = reader.Value != null
                        ? reader.Value.ToString()
                        : string.Empty;

                    if (!reader.Read())
                        break;

                    if (string.Equals(propertyName, "type", StringComparison.OrdinalIgnoreCase))
                    {
                        rootType = ReadJsonStringValue(reader);
                        continue;
                    }

                    if (string.Equals(propertyName, "features", StringComparison.OrdinalIgnoreCase))
                    {
                        if (reader.TokenType != JsonToken.StartArray)
                            throw new InvalidDataException(
                                "Zoning GeoJSON has no features array"
                            );

                        featuresFound = true;

                        while (reader.Read())
                        {
                            if (reader.TokenType == JsonToken.EndArray)
                                break;

                            featureCount++;

                            if (reader.TokenType != JsonToken.StartObject)
                            {
                                SkipJsonValue(reader);

                                if (onFeature != null)
                                    onFeature(null, null);

                                continue;
                            }

                            string featureCs2;
                            string featureZone;

                            ReadZoningFeature(
                                reader,
                                result,
                                out featureCs2,
                                out featureZone
                            );

                            if (onFeature != null)
                                onFeature(featureCs2, featureZone);
                        }

                        continue;
                    }

                    SkipJsonValue(reader);
                }
            }

            if (!string.Equals(
                rootType,
                "FeatureCollection",
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Not a FeatureCollection");
            }

            if (!featuresFound)
                throw new InvalidDataException("'features' is missing or not an array");

            timer.Stop();

            CityTimelineMod.Util.Log.Info(
                "GeoJson direct zoning profile: features=" +
                featureCount +
                ", polygons=" +
                result.Count +
                ", totalMs=" +
                timer.ElapsedMilliseconds
            );

            if (ValidateZoningParserParity)
            {
                var legacy = LoadZoningPolygonsLegacy(path);
                ValidateZoningParserAgainstLegacy(legacy, result, 20);
            }

            return result;
        }

        // -------- Lecture d'une feature Zoning --------
        private static void ReadZoningFeature(
            JsonTextReader reader,
            List<GeoZoningPolygon> result)
        {
            string ignoredCs2;
            string ignoredZone;

            ReadZoningFeature(
                reader,
                result,
                out ignoredCs2,
                out ignoredZone
            );
        }
        private static void ReadZoningFeature(
            JsonTextReader reader,
            List<GeoZoningPolygon> result,
            out string cs2,
            out string zone)
        {
            cs2 = null;
            zone = null;

            var data = new ZoningFeatureData();

            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.EndObject)
                    break;

                if (reader.TokenType != JsonToken.PropertyName)
                    continue;

                var propertyName = reader.Value != null
                    ? reader.Value.ToString()
                    : string.Empty;

                if (!reader.Read())
                    break;

                if (string.Equals(
                    propertyName,
                    "properties",
                    StringComparison.OrdinalIgnoreCase))
                {
                    if (reader.TokenType == JsonToken.StartObject)
                        ReadZoningProperties(reader, data);
                    else
                        SkipJsonValue(reader);

                    continue;
                }

                if (string.Equals(
                    propertyName,
                    "geometry",
                    StringComparison.OrdinalIgnoreCase))
                {
                    if (reader.TokenType == JsonToken.StartObject)
                        ReadZoningGeometry(reader, data);
                    else
                        SkipJsonValue(reader);

                    continue;
                }

                SkipJsonValue(reader);
            }

            cs2 = data.Cs2;
            zone = data.Zone;

            if (data.Polygons == null)
                return;

            if (!string.Equals(data.GeometryType, "Polygon", StringComparison.Ordinal) &&
                !string.Equals(data.GeometryType, "MultiPolygon", StringComparison.Ordinal))
            {
                return;
            }

            foreach (var rings in data.Polygons)
            {
                if (rings != null && rings.Count > 0)
                {
                    result.Add(
                        new GeoZoningPolygon(
                            rings,
                            data.Zone,
                            data.Cs2,
                            data.Name
                        )
                    );
                }
            }
        }

        // -------- Propriétés Zoning --------
        private static void ReadZoningProperties(
            JsonTextReader reader,
            ZoningFeatureData data)
        {
            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.EndObject)
                    break;

                if (reader.TokenType != JsonToken.PropertyName)
                    continue;

                var name = reader.Value != null
                    ? reader.Value.ToString()
                    : string.Empty;

                if (!reader.Read())
                    break;

                if (string.Equals(name, "zone", StringComparison.OrdinalIgnoreCase))
                    data.Zone = ReadJsonStringValue(reader);
                else if (string.Equals(name, "cs2", StringComparison.OrdinalIgnoreCase))
                    data.Cs2 = ReadJsonStringValue(reader);
                else if (string.Equals(name, "name", StringComparison.OrdinalIgnoreCase))
                    data.Name = ReadJsonStringValue(reader);
                else
                    SkipJsonValue(reader);
            }
        }

        // -------- Géométrie Zoning --------
        private static void ReadZoningGeometry(
            JsonTextReader reader,
            ZoningFeatureData data)
        {
            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.EndObject)
                    break;

                if (reader.TokenType != JsonToken.PropertyName)
                    continue;

                var name = reader.Value != null
                    ? reader.Value.ToString()
                    : string.Empty;

                if (!reader.Read())
                    break;

                if (string.Equals(name, "type", StringComparison.OrdinalIgnoreCase))
                {
                    data.GeometryType = ReadJsonStringValue(reader);
                    continue;
                }

                if (string.Equals(name, "coordinates", StringComparison.OrdinalIgnoreCase))
                {
                    if (reader.TokenType == JsonToken.StartArray)
                        data.Polygons = ReadZoningCoordinatePolygons(reader);
                    else
                        SkipJsonValue(reader);

                    continue;
                }

                SkipJsonValue(reader);
            }
        }

        // -------- Lecture directe des coordonnées Polygon / MultiPolygon --------
        private static List<List<List<GeoPoint>>> ReadZoningCoordinatePolygons(
            JsonTextReader reader)
        {
            var result = new List<List<List<GeoPoint>>>();

            var rootDepth = reader.Depth;

            List<List<GeoPoint>> currentPolygon = null;
            List<GeoPoint> currentRing = null;

            int? pointArrayDepth = null;
            var ringArrayDepth = -1;
            var polygonArrayDepth = -1;
            var shapeKnown = false;
            var multiPolygon = false;

            var coordinateIndex = 0;
            var lon = 0.0;
            var lat = 0.0;
            var lonValid = false;
            var latValid = false;

            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.StartArray)
                {
                    if (shapeKnown &&
                        reader.Depth == pointArrayDepth.Value)
                    {
                        coordinateIndex = 0;
                        lonValid = false;
                        latValid = false;
                    }

                    continue;
                }

                if (reader.TokenType == JsonToken.EndArray)
                {
                    if (shapeKnown &&
                        reader.Depth == pointArrayDepth.Value)
                    {
                        if (lonValid && latValid)
                        {
                            if (currentRing == null)
                                currentRing = new List<GeoPoint>();

                            currentRing.Add(new GeoPoint(lon, lat));
                        }

                        continue;
                    }

                    if (shapeKnown &&
                        reader.Depth == ringArrayDepth)
                    {
                        if (currentRing != null && currentRing.Count >= 3)
                        {
                            if (currentPolygon == null)
                                currentPolygon = new List<List<GeoPoint>>();

                            currentPolygon.Add(currentRing);
                        }

                        currentRing = null;
                        continue;
                    }

                    if (shapeKnown &&
                        multiPolygon &&
                        reader.Depth == polygonArrayDepth)
                    {
                        if (currentPolygon != null && currentPolygon.Count > 0)
                            result.Add(currentPolygon);

                        currentPolygon = null;
                        continue;
                    }

                    if (reader.Depth == rootDepth)
                    {
                        if (shapeKnown &&
                            !multiPolygon &&
                            currentPolygon != null &&
                            currentPolygon.Count > 0)
                        {
                            result.Add(currentPolygon);
                        }

                        break;
                    }

                    continue;
                }

                double number;

                if (!TryReadCoordinateNumber(reader, out number))
                    continue;

                var parentArrayDepth = reader.Depth - 1;

                if (!shapeKnown)
                {
                    pointArrayDepth = parentArrayDepth;

                    multiPolygon =
                        pointArrayDepth.Value > rootDepth + 2;

                    ringArrayDepth =
                        pointArrayDepth.Value - 1;

                    polygonArrayDepth = multiPolygon
                        ? pointArrayDepth.Value - 2
                        : rootDepth;

                    currentPolygon =
                        new List<List<GeoPoint>>();

                    currentRing =
                        new List<GeoPoint>();

                    shapeKnown = true;
                    coordinateIndex = 0;
                }

                if (parentArrayDepth != pointArrayDepth.Value)
                    continue;

                if (coordinateIndex == 0)
                {
                    lon = number;
                    lonValid = true;
                }
                else if (coordinateIndex == 1)
                {
                    lat = number;
                    latValid = true;
                }

                coordinateIndex++;
            }

            return result;
        }

        // -------- Validation de la parité entre legacy et direct --------
        private static void ValidateZoningParserAgainstLegacy(
            List<GeoZoningPolygon> legacy,
            List<GeoZoningPolygon> direct,
            int maxReported)
        {
            var mismatches = 0;
            var reported = 0;

            if (legacy.Count != direct.Count)
            {
                mismatches++;

                CityTimelineMod.Util.Log.Error(
                    "Zoning parser diff: polygon count legacy=" +
                    legacy.Count +
                    " direct=" +
                    direct.Count
                );

                reported++;
            }

            var count = Math.Min(legacy.Count, direct.Count);

            for (var i = 0; i < count; i++)
            {
                var difference =
                    DescribeZoningPolygonDifference(
                        legacy[i],
                        direct[i]
                    );

                if (difference == null)
                    continue;

                mismatches++;

                if (reported < maxReported)
                {
                    CityTimelineMod.Util.Log.Error(
                        "Zoning parser diff: index=" +
                        i +
                        ", " +
                        difference
                    );

                    reported++;
                }
            }

            CityTimelineMod.Util.Log.Info(
                "Zoning parser parity: legacyPolygons=" +
                legacy.Count +
                ", directPolygons=" +
                direct.Count +
                ", mismatches=" +
                mismatches
            );
        }

        private static string DescribeZoningPolygonDifference(
            GeoZoningPolygon legacy,
            GeoZoningPolygon direct)
        {
            if (legacy == null || direct == null)
                return legacy == direct ? null : "null polygon mismatch";

            if (!string.Equals(legacy.Zone, direct.Zone, StringComparison.Ordinal))
                return "Zone mismatch";

            if (!string.Equals(legacy.Cs2, direct.Cs2, StringComparison.Ordinal))
                return "Cs2 mismatch";

            if (!string.Equals(legacy.Name, direct.Name, StringComparison.Ordinal))
                return "Name mismatch";

            if (legacy.Rings.Count != direct.Rings.Count)
            {
                return
                    "Rings.Count legacy=" +
                    legacy.Rings.Count +
                    " direct=" +
                    direct.Rings.Count;
            }

            for (var r = 0; r < legacy.Rings.Count; r++)
            {
                var a = legacy.Rings[r];
                var b = direct.Rings[r];

                if (a.Count != b.Count)
                {
                    return
                        "ring[" + r + "].Count legacy=" +
                        a.Count +
                        " direct=" +
                        b.Count;
                }

                for (var p = 0; p < a.Count; p++)
                {
                    if (!a[p].Lon.Equals(b[p].Lon) ||
                        !a[p].Lat.Equals(b[p].Lat))
                    {
                        return
                            "ring[" + r + "] point[" + p +
                            "] coordinate mismatch";
                    }
                }
            }

            return null;
        }

        // -------- Méthodes utilitaires existantes (non modifiées) --------
        internal static List<List<GeoPoint>> LoadPolygonOutlines(string path)
        {
            var features = LoadFeatures(path);
            var result = new List<List<GeoPoint>>();

            foreach (var feature in features)
            {
                var geometry = feature["geometry"] as JObject;

                if (geometry == null)
                    continue;

                var type = geometry["type"] != null ? geometry["type"].ToString() : null;
                var coordinates = geometry["coordinates"] as JArray;

                if (coordinates == null)
                    continue;

                if (type == "Polygon")
                {
                    AddPolygonRings(coordinates, result);
                }
                else if (type == "MultiPolygon")
                {
                    foreach (var polygonToken in coordinates)
                        AddPolygonRings(polygonToken as JArray, result);
                }
            }

            return result;
        }

        private static void AddLineStats(GeoLineAnalysis result, List<GeoPoint> line)
        {
            result.PointCount += line.Count;

            if (line.Count > 0)
            {
                if (result.FirstPoint == null)
                    result.FirstPoint = line[0];

                result.LastPoint = line[line.Count - 1];
            }
        }

        private static List<List<GeoPoint>> ParsePolygonRings(JArray polygonCoordinates)
        {
            var result = new List<List<GeoPoint>>();

            if (polygonCoordinates == null)
                return result;

            foreach (var ringToken in polygonCoordinates)
            {
                var ring = ParseLineString(ringToken as JArray);

                if (ring.Count >= 3)
                    result.Add(ring);
            }

            return result;
        }

        private static void AddPolygonRings(JArray polygonCoordinates, List<List<GeoPoint>> result)
        {
            if (polygonCoordinates == null)
                return;

            foreach (var ringToken in polygonCoordinates)
            {
                var ring = ParseLineString(ringToken as JArray);

                if (ring.Count >= 2)
                    result.Add(ring);
            }
        }

        private static string GetStringProperty(JObject properties, string name)
        {
            if (properties == null || string.IsNullOrWhiteSpace(name))
                return null;

            var token = properties[name];

            if (token == null)
                return null;

            var array = token as JArray;

            if (array != null)
            {
                foreach (var item in array)
                {
                    if (item == null)
                        continue;

                    var value = item.ToString();

                    if (!string.IsNullOrWhiteSpace(value))
                        return value;
                }

                return null;
            }

            var text = token.ToString();

            if (string.IsNullOrWhiteSpace(text))
                return null;

            return text;
        }

        private static int? GetIntProperty(JObject properties, string name)
        {
            var text = GetStringProperty(properties, name);

            if (string.IsNullOrWhiteSpace(text))
                return null;

            int value;

            if (int.TryParse(text, out value))
                return value;

            return null;
        }

        private static bool GetBoolProperty(JObject properties, string name)
        {
            var text = GetStringProperty(properties, name);

            if (string.IsNullOrWhiteSpace(text))
                return false;

            return
                string.Equals(text, "true", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(text, "yes", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(text, "1", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetSourceTagValue(string sourceTag, string key)
        {
            if (string.IsNullOrWhiteSpace(sourceTag) || string.IsNullOrWhiteSpace(key))
                return null;

            var prefix = key + "=";

            if (!sourceTag.StartsWith(prefix))
                return null;

            var value = sourceTag.Substring(prefix.Length);

            if (string.IsNullOrWhiteSpace(value))
                return null;

            return value;
        }

        private static List<GeoPoint> ParseLineString(JArray coordinates)
        {
            var result = new List<GeoPoint>();

            if (coordinates == null)
                return result;

            foreach (var coordToken in coordinates)
            {
                var coord = coordToken as JArray;

                if (coord == null || coord.Count < 2)
                    continue;

                var lon = coord[0].Value<double>();
                var lat = coord[1].Value<double>();

                result.Add(new GeoPoint(lon, lat));
            }

            return result;
        }

        private static JArray LoadFeatures(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException(path);

            var json = File.ReadAllText(path);

            JObject root;

            try
            {
                root = JObject.Parse(json);
            }
            catch (Exception ex)
            {
                throw new InvalidDataException("Invalid JSON", ex);
            }

            var typeToken = root["type"];
            var type = typeToken != null ? typeToken.ToString() : null;

            if (type == null || !string.Equals(type, "FeatureCollection", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Not a FeatureCollection");

            var features = root["features"] as JArray;

            if (features == null)
                throw new InvalidDataException("'features' is missing or not an array");

            return features;
        }

        // -------- Validation route (legacy) existante --------
        private static void ValidateRoadParserAgainstLegacy(
            string path,
            List<GeoRoadLine> direct,
            int maxReported)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();

            var featureIndex = 0;
            var directLineIndex = 0;
            var legacyLineCount = 0;
            var mismatchCount = 0;
            var reportedCount = 0;

            string rootType = null;
            var featuresFound = false;

            var legacyFeatureLines = new List<GeoRoadLine>();

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
                reader.DateParseHandling = DateParseHandling.None;
                reader.FloatParseHandling = FloatParseHandling.Double;

                if (!reader.Read() || reader.TokenType != JsonToken.StartObject)
                    throw new InvalidDataException(
                        "Legacy parser validation: invalid GeoJSON root object"
                    );

                while (reader.Read())
                {
                    if (reader.TokenType == JsonToken.EndObject)
                        break;

                    if (reader.TokenType != JsonToken.PropertyName)
                        continue;

                    var propertyName = reader.Value != null
                        ? reader.Value.ToString()
                        : string.Empty;

                    if (!reader.Read())
                        break;

                    if (string.Equals(
                        propertyName,
                        "type",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        rootType = reader.Value != null
                            ? reader.Value.ToString()
                            : null;

                        continue;
                    }

                    if (string.Equals(
                        propertyName,
                        "features",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        if (reader.TokenType != JsonToken.StartArray)
                            throw new InvalidDataException(
                                "Legacy parser validation: features is not an array"
                            );

                        featuresFound = true;

                        while (reader.Read())
                        {
                            if (reader.TokenType == JsonToken.EndArray)
                                break;

                            if (reader.TokenType != JsonToken.StartObject)
                            {
                                reader.Skip();
                                continue;
                            }

                            var feature = JObject.Load(reader);

                            legacyFeatureLines.Clear();
                            AddRoadFeature(feature, legacyFeatureLines);

                            for (var i = 0; i < legacyFeatureLines.Count; i++)
                            {
                                var legacyLine = legacyFeatureLines[i];
                                legacyLineCount++;

                                if (directLineIndex >= direct.Count)
                                {
                                    mismatchCount++;

                                    if (reportedCount < maxReported)
                                    {
                                        CityTimelineMod.Util.Log.Error(
                                            "GeoJson parser diff: feature=" +
                                            featureIndex +
                                            ", line=" +
                                            directLineIndex +
                                            ", direct line missing"
                                        );

                                        reportedCount++;
                                    }

                                    directLineIndex++;
                                    continue;
                                }

                                var difference = DescribeRoadLineDifference(
                                    legacyLine,
                                    direct[directLineIndex]
                                );

                                if (difference != null)
                                {
                                    mismatchCount++;

                                    if (reportedCount < maxReported)
                                    {
                                        CityTimelineMod.Util.Log.Error(
                                            "GeoJson parser diff: feature=" +
                                            featureIndex +
                                            ", line=" +
                                            directLineIndex +
                                            ", " +
                                            difference
                                        );

                                        reportedCount++;
                                    }
                                }

                                directLineIndex++;
                            }

                            featureIndex++;
                        }

                        continue;
                    }

                    reader.Skip();
                }
            }

            if (!string.Equals(
                rootType,
                "FeatureCollection",
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "Legacy parser validation: not a FeatureCollection"
                );
            }

            if (!featuresFound)
                throw new InvalidDataException(
                    "Legacy parser validation: features missing"
                );

            if (directLineIndex < direct.Count)
            {
                var extraDirectLines =
                    direct.Count - directLineIndex;

                mismatchCount += extraDirectLines;

                if (reportedCount < maxReported)
                {
                    CityTimelineMod.Util.Log.Error(
                        "GeoJson parser diff: direct parser has " +
                        extraDirectLines +
                        " extra line(s) starting at index " +
                        directLineIndex
                    );
                }
            }

            timer.Stop();

            CityTimelineMod.Util.Log.Info(
                "GeoJson parser parity: file=" +
                Path.GetFileName(path) +
                ", features=" +
                featureIndex +
                ", legacyLines=" +
                legacyLineCount +
                ", directLines=" +
                direct.Count +
                ", mismatches=" +
                mismatchCount +
                ", elapsedMs=" +
                timer.ElapsedMilliseconds
            );
        }

        private static string DescribeRoadLineDifference(
            GeoRoadLine legacy,
            GeoRoadLine direct)
        {
            if (legacy == null || direct == null)
                return legacy == direct
                    ? null
                    : "null line mismatch";

            if (legacy.Points == null || direct.Points == null)
            {
                if (legacy.Points != direct.Points)
                    return "Points null mismatch";
            }
            else
            {
                if (legacy.Points.Count != direct.Points.Count)
                {
                    return
                        "Points.Count legacy=" +
                        legacy.Points.Count +
                        " direct=" +
                        direct.Points.Count;
                }

                for (var i = 0; i < legacy.Points.Count; i++)
                {
                    var a = legacy.Points[i];
                    var b = direct.Points[i];

                    if (a == null || b == null)
                    {
                        if (a != b)
                            return "point[" + i + "] null mismatch";

                        continue;
                    }

                    if (!a.Lon.Equals(b.Lon) ||
                        !a.Lat.Equals(b.Lat))
                    {
                        return
                            "point[" + i + "] legacy=(" +
                            a.Lon.ToString(
                                "R",
                                System.Globalization.CultureInfo.InvariantCulture) +
                            "," +
                            a.Lat.ToString(
                                "R",
                                System.Globalization.CultureInfo.InvariantCulture) +
                            ") direct=(" +
                            b.Lon.ToString(
                                "R",
                                System.Globalization.CultureInfo.InvariantCulture) +
                            "," +
                            b.Lat.ToString(
                                "R",
                                System.Globalization.CultureInfo.InvariantCulture) +
                            ")";
                    }
                }
            }

            if (!string.Equals(legacy.Highway, direct.Highway, StringComparison.Ordinal))
                return "Highway legacy='" + legacy.Highway + "' direct='" + direct.Highway + "'";

            if (!string.Equals(legacy.Name, direct.Name, StringComparison.Ordinal))
                return "Name legacy='" + legacy.Name + "' direct='" + direct.Name + "'";

            if (legacy.IsPath != direct.IsPath)
                return "IsPath legacy=" + legacy.IsPath + " direct=" + direct.IsPath;

            if (!string.Equals(legacy.Oneway, direct.Oneway, StringComparison.Ordinal))
                return "Oneway legacy='" + legacy.Oneway + "' direct='" + direct.Oneway + "'";

            if (legacy.Lanes != direct.Lanes)
                return "Lanes legacy=" + legacy.Lanes + " direct=" + direct.Lanes;

            if (legacy.TargetLaneCount != direct.TargetLaneCount)
                return "TargetLaneCount legacy=" + legacy.TargetLaneCount + " direct=" + direct.TargetLaneCount;

            if (!string.Equals(legacy.MaxSpeed, direct.MaxSpeed, StringComparison.Ordinal))
                return "MaxSpeed legacy='" + legacy.MaxSpeed + "' direct='" + direct.MaxSpeed + "'";

            if (!string.Equals(legacy.Surface, direct.Surface, StringComparison.Ordinal))
                return "Surface legacy='" + legacy.Surface + "' direct='" + direct.Surface + "'";

            if (legacy.Bridge != direct.Bridge)
                return "Bridge legacy=" + legacy.Bridge + " direct=" + direct.Bridge;

            if (legacy.Tunnel != direct.Tunnel)
                return "Tunnel legacy=" + legacy.Tunnel + " direct=" + direct.Tunnel;

            if (legacy.Roundabout != direct.Roundabout)
                return "Roundabout legacy=" + legacy.Roundabout + " direct=" + direct.Roundabout;

            if (!string.Equals(legacy.Ref, direct.Ref, StringComparison.Ordinal))
                return "Ref legacy='" + legacy.Ref + "' direct='" + direct.Ref + "'";

            return null;
        }
    }
}