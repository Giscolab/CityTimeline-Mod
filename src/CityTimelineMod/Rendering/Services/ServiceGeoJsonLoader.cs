using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CityTimelineMod.Importers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CityTimelineMod.Rendering.Services
{
    internal sealed class ServiceFamilyLoadResult
    {
        internal readonly ServiceFamilyDefinition Definition;
        internal readonly string Path;
        internal readonly List<GeoServicePoint> Points = new List<GeoServicePoint>();
        internal bool Available;
        internal bool Missing;
        internal string Error = "";
        internal int SourceFeatureCount;
        internal int SkippedFeatureCount;

        internal ServiceFamilyLoadResult(ServiceFamilyDefinition definition, string path)
        {
            Definition = definition;
            Path = path ?? "";
        }
    }

    internal sealed class ServiceGeoJsonLoadResult
    {
        internal readonly List<GeoServicePoint> Points = new List<GeoServicePoint>();
        internal readonly List<ServiceFamilyLoadResult> Families = new List<ServiceFamilyLoadResult>();
        internal string ServicesDirectory = "";

        internal int LoadedFamilyCount
        {
            get
            {
                var count = 0;
                for (var i = 0; i < Families.Count; i++)
                {
                    if (Families[i].Available)
                        count++;
                }

                return count;
            }
        }

        internal int MissingFamilyCount
        {
            get
            {
                var count = 0;
                for (var i = 0; i < Families.Count; i++)
                {
                    if (Families[i].Missing)
                        count++;
                }

                return count;
            }
        }

        internal int FailedFamilyCount
        {
            get
            {
                var count = 0;
                for (var i = 0; i < Families.Count; i++)
                {
                    var family = Families[i];
                    if (!family.Available && !family.Missing)
                        count++;
                }

                return count;
            }
        }

        internal ServiceFamilyLoadResult GetFamily(string familyKey)
        {
            if (string.IsNullOrWhiteSpace(familyKey))
                return null;

            for (var i = 0; i < Families.Count; i++)
            {
                var family = Families[i];
                if (family.Definition != null &&
                    string.Equals(family.Definition.Key, familyKey, StringComparison.OrdinalIgnoreCase))
                {
                    return family;
                }
            }

            return null;
        }
    }

    /// <summary>
    /// Loads each service family independently. A missing or malformed family
    /// never prevents the other eight families from loading.
    /// </summary>
    internal static class ServiceGeoJsonLoader
    {
        internal static ServiceGeoJsonLoadResult LoadPack(string bundleOrGeoJsonPackPath)
        {
            var result = new ServiceGeoJsonLoadResult();
            result.ServicesDirectory = ResolveServicesDirectory(bundleOrGeoJsonPackPath);

            var families = ServiceCatalog.Families;
            for (var i = 0; i < families.Count; i++)
            {
                var definition = families[i];
                var path = string.IsNullOrWhiteSpace(result.ServicesDirectory)
                    ? definition.Key + ".geojson"
                    : System.IO.Path.Combine(result.ServicesDirectory, definition.Key + ".geojson");
                var familyResult = new ServiceFamilyLoadResult(definition, path);
                result.Families.Add(familyResult);

                if (!File.Exists(path))
                {
                    familyResult.Missing = true;
                    familyResult.Error = "missing";
                    continue;
                }

                try
                {
                    LoadFamilyCore(path, definition, familyResult);
                    familyResult.Available = true;
                    result.Points.AddRange(familyResult.Points);
                }
                catch (Exception ex)
                {
                    familyResult.Error = SafeError(ex);
                }
            }

            return result;
        }

        internal static List<GeoServicePoint> LoadFamily(string path, string familyKey)
        {
            var definition = ServiceCatalog.GetFamily(familyKey);
            if (definition == null)
                throw new ArgumentException("Unknown service family: " + familyKey, "familyKey");

            if (!File.Exists(path))
                throw new FileNotFoundException(path);

            var familyResult = new ServiceFamilyLoadResult(definition, path);
            LoadFamilyCore(path, definition, familyResult);
            return familyResult.Points;
        }

        internal static string ResolveServicesDirectory(string bundleOrGeoJsonPackPath)
        {
            if (string.IsNullOrWhiteSpace(bundleOrGeoJsonPackPath))
                return "";

            var candidate = bundleOrGeoJsonPackPath.Trim();
            if (File.Exists(candidate))
                candidate = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(candidate));
            else
                candidate = System.IO.Path.GetFullPath(candidate);

            if (string.Equals(System.IO.Path.GetFileName(candidate), "services", StringComparison.OrdinalIgnoreCase))
                return candidate;

            var direct = System.IO.Path.Combine(candidate, "geojson", "services");
            if (Directory.Exists(direct))
                return direct;

            var bundled = System.IO.Path.Combine(candidate, "geojson_pack", "geojson", "services");
            if (Directory.Exists(bundled))
                return bundled;

            // Return the standard location even when absent so every family result
            // reports a deterministic path and the caller can surface availability.
            if (string.Equals(System.IO.Path.GetFileName(candidate), "geojson_pack", StringComparison.OrdinalIgnoreCase))
                return direct;

            return bundled;
        }

        private static void LoadFamilyCore(
            string path,
            ServiceFamilyDefinition definition,
            ServiceFamilyLoadResult result
        )
        {
            JObject root;

            try
            {
                root = JObject.Parse(File.ReadAllText(path));
            }
            catch (Exception ex)
            {
                throw new InvalidDataException("Invalid service GeoJSON", ex);
            }

            if (!string.Equals(ReadString(root, "type"), "FeatureCollection", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Service GeoJSON is not a FeatureCollection");

            var features = root["features"] as JArray;
            if (features == null)
                throw new InvalidDataException("Service GeoJSON has no features array");

            result.SourceFeatureCount = features.Count;

            foreach (var token in features)
            {
                var feature = token as JObject;
                GeoServicePoint point;

                if (feature == null || !TryParseFeature(feature, definition, out point))
                {
                    result.SkippedFeatureCount++;
                    continue;
                }

                result.Points.Add(point);
            }
        }

        private static bool TryParseFeature(
            JObject feature,
            ServiceFamilyDefinition definition,
            out GeoServicePoint servicePoint
        )
        {
            servicePoint = null;
            var geometry = feature["geometry"] as JObject;
            if (geometry == null ||
                !string.Equals(ReadString(geometry, "type"), "Point", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var coordinate = geometry["coordinates"] as JArray;
            if (coordinate == null || coordinate.Count < 2)
                return false;

            double lon;
            double lat;

            try
            {
                // Numeric tokens must be read directly. ToString() is culture-sensitive
                // for doubles and previously broke on French Windows locales.
                lon = coordinate[0].Value<double>();
                lat = coordinate[1].Value<double>();
            }
            catch (Exception)
            {
                return false;
            }

            if (!IsFinite(lon) || !IsFinite(lat) || lon < -180d || lon > 180d || lat < -90d || lat > 90d)
                return false;

            var properties = feature["properties"] as JObject;
            if (properties == null)
                properties = new JObject();

            var declaredFamily = NormalizeKey(ReadString(properties, "family"));
            if (!string.IsNullOrWhiteSpace(declaredFamily) &&
                !string.Equals(declaredFamily, definition.Key, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var subcategory = NormalizeKey(ReadString(properties, "subcategory"));
            if (string.IsNullOrWhiteSpace(subcategory))
                subcategory = "unknown";

            servicePoint = new GeoServicePoint(
                ReadLong(properties, "id"),
                NormalizeKey(ReadString(properties, "type")),
                new GeoPoint(lon, lat),
                ReadString(properties, "name") ?? "",
                definition.Key,
                ServiceCatalog.ResolveFamilyLabel(definition.Key, ReadString(properties, "familyLabel")),
                subcategory,
                ServiceCatalog.ResolveSubcategoryLabel(
                    definition.Key,
                    subcategory,
                    ReadString(properties, "subcategoryLabel")),
                ReadString(properties, "sourceTag") ?? "",
                ReadTags(properties["tags"] as JObject)
            );
            return true;
        }

        private static Dictionary<string, string> ReadTags(JObject tags)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (tags == null)
                return result;

            foreach (var property in tags.Properties())
            {
                if (property.Value == null || property.Value.Type == JTokenType.Null)
                    continue;

                result[property.Name] = property.Value.Type == JTokenType.String
                    ? property.Value.Value<string>()
                    : property.Value.ToString(Formatting.None);
            }

            return result;
        }

        private static long? ReadLong(JObject obj, string key)
        {
            var token = obj != null ? obj[key] : null;
            if (token == null || token.Type == JTokenType.Null)
                return null;

            try
            {
                if (token.Type == JTokenType.Integer)
                    return token.Value<long>();

                long value;
                return long.TryParse(token.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
                    ? value
                    : (long?)null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string ReadString(JObject obj, string key)
        {
            var token = obj != null ? obj[key] : null;
            if (token == null || token.Type == JTokenType.Null)
                return null;

            var value = token.Type == JTokenType.String ? token.Value<string>() : token.ToString();
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static string NormalizeKey(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "" : value.Trim().ToLowerInvariant();
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static string SafeError(Exception ex)
        {
            if (ex == null)
                return "unknown error";

            var message = ex.Message;
            return string.IsNullOrWhiteSpace(message) ? ex.GetType().Name : message.Trim();
        }
    }
}
