using System;
using System.Collections.Generic;
using System.IO;
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

    internal sealed class GeoRoadLine
    {
        internal List<GeoPoint> Points;
        internal string Highway;
        internal string Name;
        internal bool IsPath;

        internal GeoRoadLine(List<GeoPoint> points, string highway, string name, bool isPath = false)
        {
            Points = points ?? new List<GeoPoint>();
            Highway = highway;
            Name = name;
            IsPath = isPath;
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

        internal static List<GeoRoadLine> LoadRoadGeometries(string path)
        {
            var features = LoadFeatures(path);
            var result = new List<GeoRoadLine>();

            foreach (var feature in features)
            {
                var properties = feature["properties"] as JObject;
                var tags = properties != null ? properties["tags"] as JObject : null;

                var highway = GetStringProperty(properties, "highway");

                if (string.IsNullOrWhiteSpace(highway))
                    highway = GetStringProperty(tags, "highway");

                if (string.IsNullOrWhiteSpace(highway))
                    highway = GetSourceTagValue(GetStringProperty(properties, "sourceTag"), "highway");

                var name = GetStringProperty(properties, "name");

                var geometry = feature["geometry"] as JObject;

                if (geometry == null)
                    continue;

                var type = geometry["type"] != null ? geometry["type"].ToString() : null;
                var coordinates = geometry["coordinates"];

                if (type == "LineString")
                {
                    var line = ParseLineString(coordinates as JArray);

                    if (line.Count >= 2)
                        result.Add(new GeoRoadLine(line, highway, name));
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
                            result.Add(new GeoRoadLine(line, highway, name));
                    }
                }
            }

            return result;
        }

        internal static List<GeoZoningPolygon> LoadZoningPolygons(string path)
        {
            var features = LoadFeatures(path);
            var result = new List<GeoZoningPolygon>();

            foreach (var feature in features)
            {
                var properties = feature["properties"] as JObject;

                var zone = GetStringProperty(properties, "zone");
                var cs2 = GetStringProperty(properties, "cs2");
                var name = GetStringProperty(properties, "name");

                var geometry = feature["geometry"] as JObject;

                if (geometry == null)
                    continue;

                var type = geometry["type"] != null ? geometry["type"].ToString() : null;
                var coordinates = geometry["coordinates"] as JArray;

                if (coordinates == null)
                    continue;

                if (type == "Polygon")
                {
                    var rings = ParsePolygonRings(coordinates);

                    if (rings.Count > 0)
                        result.Add(new GeoZoningPolygon(rings, zone, cs2, name));
                }
                else if (type == "MultiPolygon")
                {
                    foreach (var polygonToken in coordinates)
                    {
                        var rings = ParsePolygonRings(polygonToken as JArray);

                        if (rings.Count > 0)
                            result.Add(new GeoZoningPolygon(rings, zone, cs2, name));
                    }
                }
            }

            return result;
        }

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
    }
}
