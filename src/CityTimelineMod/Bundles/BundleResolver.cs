using System;
using System.Globalization;
using System.IO;
using CityTimelineMod.Config;
using CityTimelineMod.Util;
using Newtonsoft.Json.Linq;

namespace CityTimelineMod.Bundles
{
    internal static class BundleResolver
    {
        internal static string ResolveManifestPath(GeoOverlayConfig config, string modDir)
        {
            if (config == null)
                return null;

            var legacyManifestPath = ResolvePath(config.BundleManifestPath, modDir);

            if (!config.UseBundleIndex)
            {
                Log.Info("BundleResolver: useBundleIndex=false. Using legacy bundleManifestPath.");
                return legacyManifestPath;
            }

            if (string.IsNullOrWhiteSpace(config.BundlesRoot))
            {
                Log.Error("BundleResolver: bundlesRoot is empty. Fallback to legacy bundleManifestPath.");
                return legacyManifestPath;
            }

            var bundlesRoot = ResolvePath(config.BundlesRoot, modDir);
            config.ResolvedBundlesRoot = bundlesRoot;

            if (string.IsNullOrWhiteSpace(bundlesRoot) || !Directory.Exists(bundlesRoot))
            {
                Log.Error("BundleResolver: bundlesRoot not found: " + bundlesRoot + ". Fallback to legacy bundleManifestPath.");
                return legacyManifestPath;
            }

            var indexPath = Path.Combine(bundlesRoot, "bundle_index.json");
            Log.Info("BundleResolver: bundlesRoot=" + bundlesRoot);
            Log.Info("BundleResolver: bundleIndexPath=" + indexPath);

            if (!File.Exists(indexPath))
            {
                Log.Error("BundleResolver: bundle_index.json not found. Fallback to legacy bundleManifestPath.");
                return legacyManifestPath;
            }

            if (string.IsNullOrWhiteSpace(config.ActiveBundleId))
            {
                Log.Error("BundleResolver: activeBundleId is empty. Fallback to legacy bundleManifestPath.");
                return legacyManifestPath;
            }

            try
            {
                var root = JObject.Parse(File.ReadAllText(indexPath));
                var bundles = root["bundles"] as JArray;

                if (bundles == null)
                {
                    Log.Error("BundleResolver: bundle_index.json has no bundles array. Fallback to legacy bundleManifestPath.");
                    return legacyManifestPath;
                }

                JObject selected = null;
                var requestedId = config.ActiveBundleId.Trim();

                foreach (var item in bundles)
                {
                    var entry = item as JObject;
                    if (entry == null)
                        continue;

                    var id = ReadString(entry, "id");

                    if (string.Equals(id, requestedId, StringComparison.OrdinalIgnoreCase))
                    {
                        selected = entry;
                        break;
                    }
                }

                if (selected == null)
                {
                    Log.Error("BundleResolver: activeBundleId not found: " + requestedId + ". Fallback to legacy bundleManifestPath.");
                    return legacyManifestPath;
                }

                ApplyIndexMetadata(config, selected);

                var manifestPath = ResolveManifestFromEntry(bundlesRoot, selected);

                if (string.IsNullOrWhiteSpace(manifestPath) || !File.Exists(manifestPath))
                {
                    Log.Error("BundleResolver: selected bundle manifest not found: " + manifestPath + ". Fallback to legacy bundleManifestPath.");
                    return legacyManifestPath;
                }

                config.ActiveBundleRoot = Path.GetDirectoryName(manifestPath);

                Log.Info("BundleResolver: selected activeBundleId=" + config.ActiveBundleId);
                Log.Info("BundleResolver: selected displayName=" + Safe(config.ActiveBundleDisplayName));
                Log.Info("BundleResolver: selected city=" + Safe(config.ActiveBundleCity) + ", country=" + Safe(config.ActiveBundleCountry));
                Log.Info("BundleResolver: selected center lon=" + config.ActiveBundleCenterLon + ", lat=" + config.ActiveBundleCenterLat);
                Log.Info("BundleResolver: selected manifest=" + manifestPath);

                return manifestPath;
            }
            catch (Exception ex)
            {
                Log.Error("BundleResolver: failed to read bundle_index.json. " + ex);
                return legacyManifestPath;
            }
        }

        private static string ResolveManifestFromEntry(string bundlesRoot, JObject entry)
        {
            var manifestPath = ReadString(entry, "manifestPath");

            if (!string.IsNullOrWhiteSpace(manifestPath))
            {
                if (Path.IsPathRooted(manifestPath))
                    return manifestPath;

                return Path.GetFullPath(Path.Combine(bundlesRoot, manifestPath));
            }

            var relativePath = ReadString(entry, "relativePath");

            if (!string.IsNullOrWhiteSpace(relativePath))
                return Path.GetFullPath(Path.Combine(bundlesRoot, relativePath, "manifest.json"));

            var id = ReadString(entry, "id");

            if (!string.IsNullOrWhiteSpace(id))
                return Path.GetFullPath(Path.Combine(bundlesRoot, id, "manifest.json"));

            return null;
        }

        private static void ApplyIndexMetadata(GeoOverlayConfig config, JObject entry)
        {
            config.ActiveBundleId = ReadString(entry, "id");
            config.ActiveBundleDisplayName = ReadString(entry, "displayName");
            config.ActiveBundleCity = ReadString(entry, "city");
            config.ActiveBundleCountry = ReadString(entry, "country");

            double value;

            if (TryReadDouble(entry, "centerLon", out value))
                config.ActiveBundleCenterLon = value;

            if (TryReadDouble(entry, "centerLat", out value))
                config.ActiveBundleCenterLat = value;

            if (TryReadDouble(entry, "recommendedWaterLevel", out value) && double.IsNaN(config.RecommendedCs2WaterLevel))
                config.RecommendedCs2WaterLevel = value;
        }

        private static string ResolvePath(string path, string modDir)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            if (Path.IsPathRooted(path))
                return Path.GetFullPath(path);

            return Path.GetFullPath(Path.Combine(modDir, path));
        }

        private static string ReadString(JObject obj, string name)
        {
            if (obj == null)
                return null;

            var token = obj[name];

            if (token == null)
                return null;

            return token.ToString();
        }

        private static bool TryReadDouble(JObject obj, string name, out double value)
        {
            value = double.NaN;

            if (obj == null)
                return false;

            var token = obj[name];

            if (token == null)
                return false;

            if (token.Type == JTokenType.Float || token.Type == JTokenType.Integer)
            {
                value = token.Value<double>();
                return true;
            }

            return double.TryParse(token.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static string Safe(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "(empty)" : value;
        }
    }
}
