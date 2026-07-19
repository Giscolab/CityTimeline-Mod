using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CityTimelineMod.Config;
using CityTimelineMod.Util;
using Newtonsoft.Json.Linq;

namespace CityTimelineMod.Bundles
{
    internal sealed class BundleCatalogEntry
    {
        internal string Id = "";
        internal string DisplayName = "";
        internal string City = "";
        internal string Country = "";
        internal string CountryCode = "";
        internal double CenterLon = double.NaN;
        internal double CenterLat = double.NaN;
        internal double RecommendedWaterLevel = double.NaN;
    }

    internal static class BundleCatalog
    {
        internal static List<BundleCatalogEntry> Load(GeoOverlayConfig config)
        {
            var result = new List<BundleCatalogEntry>();

            if (config == null)
                return result;

            if (!config.UseBundleIndex)
            {
                Log.Info("BundleCatalog: bundle index disabled.");
                return result;
            }

            var bundlesRoot = config.ResolvedBundlesRoot;
            if (string.IsNullOrWhiteSpace(bundlesRoot))
            {
                var modDir = string.IsNullOrWhiteSpace(config.ConfigPath)
                    ? null
                    : Path.GetDirectoryName(config.ConfigPath);

                try
                {
                    bundlesRoot = BundleResolver.ResolveBundlesRootPath(config.BundlesRoot, modDir);
                }
                catch (Exception ex)
                {
                    Log.Error("BundleCatalog: invalid bundlesRoot. " + ex.Message);
                    return result;
                }
            }

            if (string.IsNullOrWhiteSpace(bundlesRoot))
            {
                Log.Info("BundleCatalog: bundle index disabled or bundlesRoot is empty.");
                return result;
            }

            if (!Directory.Exists(bundlesRoot))
            {
                Log.Error("BundleCatalog: bundlesRoot not found: " + bundlesRoot);
                return result;
            }

            var indexPath = Path.Combine(bundlesRoot, "bundle_index.json");

            if (!File.Exists(indexPath))
            {
                Log.Error("BundleCatalog: bundle_index.json not found: " + indexPath);
                return result;
            }

            try
            {
                var root = JObject.Parse(File.ReadAllText(indexPath));
                var bundles = root["bundles"] as JArray;

                if (bundles == null)
                {
                    Log.Error("BundleCatalog: bundles array not found in " + indexPath);
                    return result;
                }

                foreach (var item in bundles)
                {
                    var obj = item as JObject;
                    if (obj == null)
                        continue;

                    var id = ReadString(obj, "id");

                    if (string.IsNullOrWhiteSpace(id))
                        continue;

                    string manifestPath;
                    string bundleRoot;
                    string validationError;
                    if (!BundleResolver.TryValidateEntry(
                        bundlesRoot,
                        obj,
                        out manifestPath,
                        out bundleRoot,
                        out validationError))
                    {
                        Log.Error("BundleCatalog: ignored invalid bundle " + id + ". " + validationError);
                        continue;
                    }

                    var entry = new BundleCatalogEntry();
                    entry.Id = id;
                    entry.DisplayName = ReadString(obj, "displayName");
                    entry.City = ReadString(obj, "city");
                    entry.Country = ReadString(obj, "country");
                    entry.CountryCode = ReadString(obj, "countryCode");
                    entry.CenterLon = ReadDouble(obj, "centerLon");
                    entry.CenterLat = ReadDouble(obj, "centerLat");
                    entry.RecommendedWaterLevel = ReadDouble(obj, "recommendedWaterLevel");

                    if (double.IsNaN(entry.RecommendedWaterLevel))
                        entry.RecommendedWaterLevel = ReadDouble(obj, "recommendedCs2WaterLevel");

                    if (string.IsNullOrWhiteSpace(entry.DisplayName))
                        entry.DisplayName = BuildDisplayName(entry);

                    result.Add(entry);
                }

                Log.Info("BundleCatalog: loaded entries=" + result.Count + " from " + indexPath);
            }
            catch (Exception ex)
            {
                Log.Error("BundleCatalog: failed to load catalog. " + ex);
            }

            return result;
        }

        internal static int FindIndexById(List<BundleCatalogEntry> entries, string id)
        {
            if (entries == null || string.IsNullOrWhiteSpace(id))
                return -1;

            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null && string.Equals(entries[i].Id, id, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        private static string BuildDisplayName(BundleCatalogEntry entry)
        {
            if (entry == null)
                return "(bundle inconnu)";

            var city = entry.City;
            var country = !string.IsNullOrWhiteSpace(entry.CountryCode)
                ? entry.CountryCode.ToUpperInvariant()
                : entry.Country;

            if (!string.IsNullOrWhiteSpace(city) && !string.IsNullOrWhiteSpace(country))
                return city + ", " + country;

            if (!string.IsNullOrWhiteSpace(city))
                return city;

            if (!string.IsNullOrWhiteSpace(entry.Id))
                return entry.Id;

            return "(bundle inconnu)";
        }

        private static string ReadString(JObject obj, string name)
        {
            var token = obj != null ? obj[name] : null;
            return token == null ? "" : token.ToString();
        }

        private static double ReadDouble(JObject obj, string name)
        {
            var token = obj != null ? obj[name] : null;

            if (token == null || token.Type == JTokenType.Null)
                return double.NaN;

            if (token.Type == JTokenType.Float || token.Type == JTokenType.Integer)
                return token.Value<double>();

            double value;
            return double.TryParse(token.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                ? value
                : double.NaN;
        }
    }
}
