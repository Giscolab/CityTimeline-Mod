using System;
using System.Globalization;
using System.IO;
using CityTimelineMod.Config;
using CityTimelineMod.Util;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CityTimelineMod.Bundles
{
    internal static class BundleResolver
    {
        private static readonly string[] RequiredGeoJsonFiles =
        {
            "water_lines_clipped.geojson",
            "water_areas_clipped.geojson",
            "zoning_polygons.geojson"
        };

        internal static string ResolveManifestPath(GeoOverlayConfig config, string modDir)
        {
            if (config == null)
                return null;

            config.BundleIndexResolutionSucceeded = false;
            config.BundleIndexResolutionError = null;

            if (!config.UseBundleIndex)
            {
                Log.Info("BundleResolver: useBundleIndex=false. Using legacy bundleManifestPath.");
                return ResolvePath(config.BundleManifestPath, modDir);
            }

            string bundlesRoot;
            try
            {
                bundlesRoot = ResolveBundlesRootPath(config.BundlesRoot, modDir);
            }
            catch (Exception ex)
            {
                return Fail(config, "invalid bundlesRoot: " + ex.Message);
            }

            config.ResolvedBundlesRoot = bundlesRoot;

            if (string.IsNullOrWhiteSpace(bundlesRoot))
                return Fail(config, "bundlesRoot is empty.");

            if (!Directory.Exists(bundlesRoot))
                return Fail(config, "bundlesRoot not found: " + bundlesRoot);

            var indexPath = Path.Combine(bundlesRoot, "bundle_index.json");
            Log.Info("BundleResolver: bundlesRoot=" + bundlesRoot);
            Log.Info("BundleResolver: bundleIndexPath=" + indexPath);

            if (!File.Exists(indexPath))
                return Fail(config, "bundle_index.json not found: " + indexPath);

            try
            {
                var root = JObject.Parse(File.ReadAllText(indexPath));
                var bundles = root["bundles"] as JArray;

                if (bundles == null)
                    return Fail(config, "bundle_index.json has no bundles array.");

                // Le pointeur synchronise est l'autorite. Le champ de config ne sert
                // qu'aux anciens index qui ne possedent pas encore activeBundleId.
                var indexActiveId = ReadString(root, "activeBundleId");
                var requestedId = !string.IsNullOrWhiteSpace(indexActiveId)
                    ? indexActiveId.Trim()
                    : SafeTrim(config.ActiveBundleId);

                if (string.IsNullOrWhiteSpace(requestedId))
                    return Fail(config, "no activeBundleId in bundle_index.json or config.json.");

                JObject selected = null;
                foreach (var item in bundles)
                {
                    var entry = item as JObject;
                    if (entry == null)
                        continue;

                    if (string.Equals(ReadString(entry, "id"), requestedId, StringComparison.OrdinalIgnoreCase))
                    {
                        selected = entry;
                        break;
                    }
                }

                if (selected == null)
                    return Fail(config, "activeBundleId not found in index: " + requestedId);

                string manifestPath;
                string bundleRoot;
                string validationError;
                if (!TryValidateEntry(bundlesRoot, selected, out manifestPath, out bundleRoot, out validationError))
                    return Fail(config, "active bundle is invalid: " + validationError);

                ApplyIndexMetadata(config, selected);
                config.ActiveBundleRoot = bundleRoot;
                config.BundleIndexResolutionSucceeded = true;

                Log.Info(
                    "BundleResolver: selected activeBundleId=" + config.ActiveBundleId +
                    ", source=" + (!string.IsNullOrWhiteSpace(indexActiveId) ? "bundle_index.json" : "config.json compatibility")
                );
                Log.Info("BundleResolver: selected displayName=" + Safe(config.ActiveBundleDisplayName));
                Log.Info("BundleResolver: selected city=" + Safe(config.ActiveBundleCity) + ", country=" + Safe(config.ActiveBundleCountry));
                Log.Info("BundleResolver: selected center lon=" + config.ActiveBundleCenterLon + ", lat=" + config.ActiveBundleCenterLat);
                Log.Info("BundleResolver: selected manifest=" + manifestPath);

                return manifestPath;
            }
            catch (Exception ex)
            {
                return Fail(config, "failed to read bundle_index.json: " + ex.Message);
            }
        }

        internal static string ResolveBundlesRootPath(string configuredRoot, string modDir)
        {
            if (string.IsNullOrWhiteSpace(configuredRoot))
                return null;

            return ResolvePath(configuredRoot, modDir);
        }

        internal static bool TryUpdateActiveBundlePointer(
            string configuredRoot,
            string modDir,
            string requestedId,
            out string canonicalId,
            out string error)
        {
            canonicalId = null;
            error = null;

            if (string.IsNullOrWhiteSpace(requestedId))
            {
                error = "activeBundleId is empty.";
                return false;
            }

            try
            {
                var bundlesRoot = ResolveBundlesRootPath(configuredRoot, modDir);
                if (string.IsNullOrWhiteSpace(bundlesRoot) || !Directory.Exists(bundlesRoot))
                {
                    error = "bundlesRoot not found: " + Safe(bundlesRoot);
                    return false;
                }

                var indexPath = Path.Combine(bundlesRoot, "bundle_index.json");
                if (!File.Exists(indexPath))
                {
                    error = "bundle_index.json not found: " + indexPath;
                    return false;
                }

                var root = JObject.Parse(File.ReadAllText(indexPath));
                var bundles = root["bundles"] as JArray;
                if (bundles == null)
                {
                    error = "bundle_index.json has no bundles array.";
                    return false;
                }

                JObject selected = null;
                foreach (var item in bundles)
                {
                    var entry = item as JObject;
                    if (entry != null && string.Equals(ReadString(entry, "id"), requestedId.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        selected = entry;
                        break;
                    }
                }

                if (selected == null)
                {
                    error = "bundle not found in index: " + requestedId;
                    return false;
                }

                string manifestPath;
                string bundleRoot;
                if (!TryValidateEntry(bundlesRoot, selected, out manifestPath, out bundleRoot, out error))
                    return false;

                canonicalId = ReadString(selected, "id").Trim();
                root["activeBundleId"] = canonicalId;
                string writeError;
                if (!GeoOverlayConfig.TryWriteUtf8TextAtomically(
                    indexPath,
                    root.ToString(Formatting.Indented),
                    out writeError
                ))
                {
                    error = "failed to atomically update bundle_index.json: " + writeError;
                    return false;
                }

                var persistedRoot = JObject.Parse(File.ReadAllText(indexPath));
                var persistedId = ReadString(persistedRoot, "activeBundleId");
                if (!string.Equals(persistedId, canonicalId, StringComparison.Ordinal))
                {
                    error = "bundle_index.json verification failed after atomic update.";
                    return false;
                }

                Log.Info("BundleResolver: bundle_index activeBundleId saved=" + canonicalId);
                return true;
            }
            catch (Exception ex)
            {
                error = "failed to update bundle_index.json: " + ex.Message;
                return false;
            }
        }

        internal static bool TryValidateEntry(
            string bundlesRoot,
            JObject entry,
            out string manifestPath,
            out string bundleRoot,
            out string error)
        {
            manifestPath = null;
            bundleRoot = null;
            error = null;

            if (entry == null)
            {
                error = "index entry is null.";
                return false;
            }

            var id = SafeTrim(ReadString(entry, "id"));
            if (!IsSafeBundleId(id))
            {
                error = "invalid bundle id: " + Safe(id);
                return false;
            }

            var bundlePath = FirstNonEmpty(
                ReadString(entry, "bundlePath"),
                ReadString(entry, "relativePath"),
                id
            );

            if (!TryResolveUnderRoot(bundlesRoot, bundlePath, out bundleRoot))
            {
                error = "bundle path escapes bundlesRoot: " + Safe(bundlePath);
                return false;
            }

            if (!Directory.Exists(bundleRoot))
            {
                error = "bundle directory not found: " + bundleRoot;
                return false;
            }

            var indexedManifestPath = ReadString(entry, "manifestPath");
            if (string.IsNullOrWhiteSpace(indexedManifestPath))
                indexedManifestPath = Path.Combine(bundlePath, "manifest.json");

            if (!TryResolveUnderRoot(bundlesRoot, indexedManifestPath, out manifestPath))
            {
                error = "manifest path escapes bundlesRoot: " + Safe(indexedManifestPath);
                return false;
            }

            if (!string.Equals(Path.GetDirectoryName(manifestPath), bundleRoot, PathComparison))
            {
                error = "manifestPath does not belong to its indexed bundle directory: " + manifestPath;
                return false;
            }

            if (!File.Exists(manifestPath))
            {
                error = "manifest not found: " + manifestPath;
                return false;
            }

            JObject manifest;
            try
            {
                manifest = JObject.Parse(File.ReadAllText(manifestPath));
            }
            catch (Exception ex)
            {
                error = "invalid manifest JSON: " + ex.Message;
                return false;
            }

            var manifestId = SafeTrim(manifest.SelectToken("bundle.id") != null
                ? manifest.SelectToken("bundle.id").ToString()
                : null);

            if (!string.Equals(id, manifestId, StringComparison.OrdinalIgnoreCase))
            {
                error = "index id and manifest bundle.id differ (" + id + " / " + Safe(manifestId) + ").";
                return false;
            }

            var geojsonRoot = Path.Combine(bundleRoot, "geojson_pack", "geojson");
            if (!Directory.Exists(geojsonRoot))
            {
                error = "GeoJSON payload directory not found: " + geojsonRoot;
                return false;
            }

            foreach (var fileName in RequiredGeoJsonFiles)
            {
                var payloadPath = Path.Combine(geojsonRoot, fileName);
                if (!IsReadablePayload(payloadPath))
                {
                    error = "required GeoJSON payload missing or empty: " + payloadPath;
                    return false;
                }
            }

            var majorRoads = Path.Combine(geojsonRoot, "roads_major_clipped.geojson");
            var driveableRoads = Path.Combine(geojsonRoot, "roads_driveable_clipped.geojson");
            if (!IsReadablePayload(majorRoads) && !IsReadablePayload(driveableRoads))
            {
                error = "road GeoJSON payload missing (roads_major_clipped.geojson or roads_driveable_clipped.geojson).";
                return false;
            }

            // Completeness is audited at selection time but remains compatible
            // with legacy bundles. A missing optional layer must never prevent an
            // older city from loading; the exact omissions are exposed in the HUD.
            var geoJsonPackRoot = Path.Combine(bundleRoot, "geojson_pack");
            int presentFileCount;
            var missingFiles = GeoJsonBundleContract.FindMissingFiles(
                geoJsonPackRoot,
                out presentFileCount
            );
            if (missingFiles.Count == 0)
            {
                Log.Info(
                    "BundleResolver: complete visual bundle contract " +
                    presentFileCount + "/" + GeoJsonBundleContract.CompleteRelativeFiles.Length + "."
                );
            }
            else
            {
                Log.Info(
                    "BundleResolver: WARNING compatibility mode; visual bundle contract " +
                    presentFileCount + "/" + GeoJsonBundleContract.CompleteRelativeFiles.Length +
                    ", missing=" + string.Join(",", missingFiles.ToArray()) + "."
                );
            }

            return true;
        }

        private static bool TryResolveUnderRoot(string root, string path, out string resolved)
        {
            resolved = null;
            if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
                return false;

            var fullRoot = Path.GetFullPath(root);
            var candidate = Path.IsPathRooted(path)
                ? Path.GetFullPath(path)
                : Path.GetFullPath(Path.Combine(fullRoot, path));

            var rootPrefix = fullRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!candidate.StartsWith(rootPrefix, PathComparison))
                return false;

            resolved = candidate;
            return true;
        }

        private static bool IsReadablePayload(string path)
        {
            try
            {
                return File.Exists(path) && new FileInfo(path).Length > 0L;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsSafeBundleId(string id)
        {
            return !string.IsNullOrWhiteSpace(id) &&
                   id != "." &&
                   id != ".." &&
                   id.IndexOf('/') < 0 &&
                   id.IndexOf('\\') < 0 &&
                   id.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
        }

        internal static void ApplyIndexMetadata(GeoOverlayConfig config, JObject entry)
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
            if (string.IsNullOrWhiteSpace(modDir))
                throw new ArgumentException("modDir is empty for a relative path.");
            return Path.GetFullPath(Path.Combine(modDir, path));
        }

        private static string Fail(GeoOverlayConfig config, string message)
        {
            config.BundleIndexResolutionSucceeded = false;
            config.BundleIndexResolutionError = message;
            Log.Error("BundleResolver: " + message + " No legacy fallback because useBundleIndex=true.");
            return null;
        }

        private static string ReadString(JObject obj, string name)
        {
            var token = obj != null ? obj[name] : null;
            return token == null || token.Type == JTokenType.Null ? null : token.ToString();
        }

        private static bool TryReadDouble(JObject obj, string name, out double value)
        {
            value = double.NaN;
            var token = obj != null ? obj[name] : null;
            if (token == null)
                return false;
            if (token.Type == JTokenType.Float || token.Type == JTokenType.Integer)
            {
                value = token.Value<double>();
                return true;
            }
            return double.TryParse(token.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    return value.Trim();
            }
            return null;
        }

        private static string SafeTrim(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static string Safe(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "(empty)" : value;
        }

        private static StringComparison PathComparison
        {
            get
            {
                return Environment.OSVersion.Platform == PlatformID.Win32NT
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal;
            }
        }
    }
}
