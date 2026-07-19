using System;
using System.Globalization;
using System.IO;
using System.Text;
using CityTimelineMod.Rendering.Services;
using Newtonsoft.Json.Linq;

namespace CityTimelineMod.Rendering.Statistics
{
    /// <summary>
    /// Reads independent bundle reports. Every source is optional and failure
    /// of one source never discards statistics already loaded from another.
    /// </summary>
    internal static class BundleHudStatsLoader
    {
        internal static BundleHudSnapshot Load(string bundleOrGeoJsonPackPath)
        {
            var snapshot = new BundleHudSnapshot();
            snapshot.PackRoot = ResolveGeoJsonPackRoot(bundleOrGeoJsonPackPath);

            var reports = Path.Combine(snapshot.PackRoot, "reports");
            var geojson = Path.Combine(snapshot.PackRoot, "geojson");

            LoadSource(
                snapshot,
                "layerIndex",
                Path.Combine(reports, "layer_index.json"),
                delegate(JObject root) { ReadLayerIndex(snapshot, root); });
            LoadSource(
                snapshot,
                "roadsIndex",
                Path.Combine(reports, "roads_index.json"),
                delegate(JObject root) { ReadRoadsIndex(snapshot, root); });
            LoadSource(
                snapshot,
                "servicesIndex",
                Path.Combine(reports, "services_index.json"),
                delegate(JObject root) { ReadServicesIndex(snapshot, root); });
            LoadSource(
                snapshot,
                "zoningPolygons",
                Path.Combine(geojson, "zoning_polygons.geojson"),
                delegate(JObject root) { ReadZoningPolygons(snapshot, root); });

            return snapshot;
        }

        internal static string ResolveGeoJsonPackRoot(string bundleOrGeoJsonPackPath)
        {
            if (string.IsNullOrWhiteSpace(bundleOrGeoJsonPackPath))
                return Path.GetFullPath("geojson_pack");

            var candidate = bundleOrGeoJsonPackPath.Trim();
            if (File.Exists(candidate))
                candidate = Path.GetDirectoryName(Path.GetFullPath(candidate));
            else
                candidate = Path.GetFullPath(candidate);

            var name = Path.GetFileName(candidate);
            if (string.Equals(name, "reports", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "geojson", StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetDirectoryName(candidate);
            }

            if (string.Equals(name, "geojson_pack", StringComparison.OrdinalIgnoreCase))
                return candidate;

            var nested = Path.Combine(candidate, "geojson_pack");
            return Directory.Exists(nested) ? nested : candidate;
        }

        private static void LoadSource(
            BundleHudSnapshot snapshot,
            string key,
            string path,
            Action<JObject> reader
        )
        {
            if (!File.Exists(path))
            {
                snapshot.SetSource(key, path, false, "missing");
                return;
            }

            try
            {
                var root = JObject.Parse(File.ReadAllText(path));
                reader(root);
                snapshot.SetSource(key, path, true, "");
            }
            catch (Exception ex)
            {
                snapshot.SetSource(key, path, false, SafeError(ex));
            }
        }

        private static void ReadLayerIndex(BundleHudSnapshot snapshot, JObject root)
        {
            UpdateMetadata(snapshot, root);
            var layers = root["layers"] as JArray;
            if (layers == null)
                throw new InvalidDataException("layers is missing");

            foreach (var token in layers)
            {
                var layer = token as JObject;
                var key = NormalizeKey(ReadString(layer, "name"));
                if (layer == null || string.IsNullOrWhiteSpace(key))
                    continue;

                snapshot.LayerCounts[key] = ReadCount(layer, "count");
            }
        }

        private static void ReadRoadsIndex(BundleHudSnapshot snapshot, JObject root)
        {
            UpdateMetadata(snapshot, root);
            var categories = root["categories"] as JArray;
            if (categories == null)
                throw new InvalidDataException("categories is missing");

            foreach (var token in categories)
            {
                var category = token as JObject;
                var key = NormalizeKey(ReadString(category, "key"));
                if (category == null || string.IsNullOrWhiteSpace(key))
                    continue;

                snapshot.RoadCategoryCounts[key] = ReadCount(category, "count");
                var label = ReadString(category, "label");
                var color = ReadString(category, "color");
                if (!string.IsNullOrWhiteSpace(label))
                    snapshot.RoadCategoryLabels[key] = label;
                if (!string.IsNullOrWhiteSpace(color))
                    snapshot.RoadCategoryColors[key] = color;
            }
        }

        private static void ReadServicesIndex(BundleHudSnapshot snapshot, JObject root)
        {
            UpdateMetadata(snapshot, root);
            var families = root["families"] as JArray;
            if (families == null)
                throw new InvalidDataException("families is missing");

            foreach (var token in families)
            {
                var familyObject = token as JObject;
                var key = NormalizeKey(ReadString(familyObject, "key"));
                if (familyObject == null || string.IsNullOrWhiteSpace(key))
                    continue;

                var catalog = ServiceCatalog.GetFamily(key);
                var label = ReadString(familyObject, "label");
                if (string.IsNullOrWhiteSpace(label) && catalog != null)
                    label = catalog.Label;

                var family = snapshot.EnsureServiceFamily(key, label ?? key);
                family.Available = true;
                family.Implemented = ReadBool(familyObject, "implemented", true);
                family.Count = ReadCount(familyObject, "count");

                var subcategories = familyObject["subcategories"] as JArray;
                if (subcategories == null)
                    continue;

                foreach (var subToken in subcategories)
                {
                    var subObject = subToken as JObject;
                    var subKey = NormalizeKey(ReadString(subObject, "key"));
                    if (subObject == null || string.IsNullOrWhiteSpace(subKey))
                        continue;

                    var subLabel = ReadString(subObject, "label");
                    var catalogSub = ServiceCatalog.GetSubcategory(key, subKey);
                    if (string.IsNullOrWhiteSpace(subLabel) && catalogSub != null)
                        subLabel = catalogSub.Label;

                    BundleHudSubcategoryStats subcategory;
                    if (!family.Subcategories.TryGetValue(subKey, out subcategory))
                    {
                        subcategory = new BundleHudSubcategoryStats(subKey, subLabel ?? subKey);
                        family.Subcategories[subKey] = subcategory;
                    }
                    else if (!string.IsNullOrWhiteSpace(subLabel))
                    {
                        subcategory.Label = subLabel;
                    }

                    subcategory.Count = ReadCount(subObject, "count");
                }
            }
        }

        private static void ReadZoningPolygons(BundleHudSnapshot snapshot, JObject root)
        {
            if (!string.Equals(ReadString(root, "type"), "FeatureCollection", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("zoning GeoJSON is not a FeatureCollection");

            var features = root["features"] as JArray;
            if (features == null)
                throw new InvalidDataException("features is missing");

            for (var i = 0; i < BundleHudSnapshot.ZoningKeys.Length; i++)
                snapshot.ZoningCounts[BundleHudSnapshot.ZoningKeys[i]] = 0;

            snapshot.ZoningFeatureCount = features.Count;
            foreach (var token in features)
            {
                var feature = token as JObject;
                var properties = feature != null ? feature["properties"] as JObject : null;
                var key = ClassifyZoning(
                    ReadString(properties, "cs2"),
                    ReadString(properties, "zone"));
                int count;
                snapshot.ZoningCounts.TryGetValue(key, out count);
                snapshot.ZoningCounts[key] = count + 1;
            }
        }

        internal static string ClassifyZoning(string cs2, string zone)
        {
            var label = NormalizeText(cs2);

            if (label.Contains("residentiel") && label.Contains("haute")) return "residential_high";
            if (label.Contains("residentiel") && label.Contains("moyenne")) return "residential_medium";
            if (label.Contains("residentiel") && label.Contains("basse")) return "residential_low";
            if (label.Contains("commercial") && label.Contains("haute")) return "commercial_high";
            if (label.Contains("commercial") && label.Contains("basse")) return "commercial_low";
            if (label.Contains("commerce de detail") || label == "retail") return "retail";
            if (label.Contains("industrie")) return "industrial";
            if (label.Contains("parking en ouvrage")) return "parking_structure";
            if (label.Contains("parking de surface")) return "parking_surface";
            if (label.Contains("bureaux") || label.Contains("administration")) return "office";
            if (label.Contains("usage mixte") || label.Contains("mixed")) return "mixed";

            var zoneKey = NormalizeKey(zone).Replace('-', '_').Replace(' ', '_');
            switch (zoneKey)
            {
                case "high":
                case "res_high":
                case "residential_high": return "residential_high";
                case "medium":
                case "res_med":
                case "residential_medium": return "residential_medium";
                case "low":
                case "res_low":
                case "residential_low": return "residential_low";
                case "com_high":
                case "commercial_high": return "commercial_high";
                case "com_low":
                case "commercial_low": return "commercial_low";
                case "retail": return "retail";
                case "industrial":
                case "industry": return "industrial";
                case "ramp":
                case "structure":
                case "parking_structure": return "parking_structure";
                case "surface":
                case "parking_surface": return "parking_surface";
                case "office": return "office";
                case "mixed":
                case "mixed_use": return "mixed";
                default: return "unknown";
            }
        }

        private static void UpdateMetadata(BundleHudSnapshot snapshot, JObject root)
        {
            var generatedAt = ReadString(root, "generatedAt");
            var city = ReadString(root, "city");
            var bbox = ReadString(root, "bbox");
            if (string.IsNullOrWhiteSpace(snapshot.GeneratedAt) && !string.IsNullOrWhiteSpace(generatedAt))
                snapshot.GeneratedAt = generatedAt;
            if (string.IsNullOrWhiteSpace(snapshot.City) && !string.IsNullOrWhiteSpace(city))
                snapshot.City = city;
            if (string.IsNullOrWhiteSpace(snapshot.Bbox) && !string.IsNullOrWhiteSpace(bbox))
                snapshot.Bbox = bbox;
        }

        private static string ReadString(JObject obj, string key)
        {
            var token = obj != null ? obj[key] : null;
            if (token == null || token.Type == JTokenType.Null)
                return null;

            var value = token.Type == JTokenType.String ? token.Value<string>() : token.ToString();
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static int ReadCount(JObject obj, string key)
        {
            var token = obj != null ? obj[key] : null;
            if (token == null || token.Type == JTokenType.Null)
                return 0;

            long value;
            try
            {
                value = token.Type == JTokenType.Integer
                    ? token.Value<long>()
                    : long.Parse(token.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                return 0;
            }

            if (value <= 0)
                return 0;
            return value > int.MaxValue ? int.MaxValue : (int)value;
        }

        private static bool ReadBool(JObject obj, string key, bool fallback)
        {
            var token = obj != null ? obj[key] : null;
            if (token == null || token.Type == JTokenType.Null)
                return fallback;
            if (token.Type == JTokenType.Boolean)
                return token.Value<bool>();

            bool value;
            return bool.TryParse(token.ToString(), out value) ? value : fallback;
        }

        private static string NormalizeKey(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "" : value.Trim().ToLowerInvariant();
        }

        private static string NormalizeText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "";

            var decomposed = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var output = new StringBuilder(decomposed.Length);
            for (var i = 0; i < decomposed.Length; i++)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(decomposed[i]);
                if (category != UnicodeCategory.NonSpacingMark)
                    output.Append(decomposed[i]);
            }

            return output.ToString().Normalize(NormalizationForm.FormC);
        }

        private static string SafeError(Exception ex)
        {
            if (ex == null)
                return "unknown error";
            return string.IsNullOrWhiteSpace(ex.Message) ? ex.GetType().Name : ex.Message.Trim();
        }
    }
}
