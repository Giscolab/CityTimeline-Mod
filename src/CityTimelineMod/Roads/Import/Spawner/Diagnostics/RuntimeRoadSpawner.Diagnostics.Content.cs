using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using CityTimelineMod.Importers;
using CityTimelineMod.Rendering;
using Game.Prefabs;
using Unity.Entities;
using Newtonsoft.Json.Linq;

namespace CityTimelineMod.Roads
{
    internal static partial class RuntimeRoadSpawner
    {
        internal static int DumpCachedRoadContentDiagnostics(int maxTop = 30)
        {
            var roadLines = _cachedRoadLines;

            if (roadLines == null || roadLines.Count == 0)
            {
                Util.Log.Info("[RoadContentStats] unavailable: cached road lines empty.");
                return 0;
            }

            var highwayCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var surfaceCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var laneCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var onewayCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var nameCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var pointCount = 0;
            var validMeta = 0;
            var pathLines = 0;
            var importableLines = 0;
            var named = 0;
            var unnamed = 0;
            var bridges = 0;
            var tunnels = 0;
            var roundabouts = 0;
            var minX = float.MaxValue;
            var minZ = float.MaxValue;
            var maxX = float.MinValue;
            var maxZ = float.MinValue;
            var hasExtent = false;

            for (var i = 0; i < roadLines.Count; i++)
            {
                var roadLine = roadLines[i];

                if (roadLine == null)
                    continue;

                if (roadLine.Points != null)
                    pointCount += roadLine.Points.Count;

                if (roadLine.IsPath)
                    pathLines++;

                var meta = RoadImportMetadata.FromRoadLine(roadLine);

                if (meta == null)
                    continue;

                validMeta++;

                var highway = NormalizeStatKey(meta.Highway, "unknown");
                var surface = NormalizeStatKey(ReadDiagnosticTag(roadLine, meta, "surface", "Surface", "surfaceTag", "SurfaceTag"), "unknown");
                var lanes = NormalizeStatKey(ReadDiagnosticTag(roadLine, meta, "lanes", "Lanes", "targetLaneCount", "TargetLaneCount"), "unknown");
                var name = NormalizeStatKey(meta.Name, "<unnamed>");

                Increment(highwayCounts, highway);
                Increment(surfaceCounts, surface);
                Increment(laneCounts, lanes);
                Increment(onewayCounts, meta.Oneway ? "true" : "false");

                if (string.IsNullOrWhiteSpace(meta.Name))
                {
                    unnamed++;
                }
                else
                {
                    named++;
                    Increment(nameCounts, name);
                }

                if (meta.Bridge)
                    bridges++;

                if (meta.Tunnel)
                    tunnels++;

                if (meta.Roundabout)
                    roundabouts++;

                if (!roadLine.IsPath && (_cachedConfig == null || _cachedConfig.RuntimeRoadImportIncludeBridgeTunnel || (!meta.Bridge && !meta.Tunnel)))
                    importableLines++;

                UpdateWorldExtent(roadLine, ref minX, ref minZ, ref maxX, ref maxZ, ref hasExtent);
            }

            Util.Log.Info(
                "[RoadContentStats] sourceKey=" + Safe(_cachedSourceKey) +
                ", cachedLines=" + roadLines.Count +
                ", validMeta=" + validMeta +
                ", importableLines=" + importableLines +
                ", pathLines=" + pathLines +
                ", pointCount=" + pointCount
            );

            Util.Log.Info(
                "[RoadContentStats] named=" + named +
                ", unnamed=" + unnamed +
                ", uniqueNames=" + nameCounts.Count +
                ", bridges=" + bridges +
                ", tunnels=" + tunnels +
                ", roundabouts=" + roundabouts
            );

            if (hasExtent)
            {
                Util.Log.Info(
                    "[RoadContentStats] worldExtent x=" + minX.ToString("0.###", CultureInfo.InvariantCulture) +
                    ".." + maxX.ToString("0.###", CultureInfo.InvariantCulture) +
                    ", z=" + minZ.ToString("0.###", CultureInfo.InvariantCulture) +
                    ".." + maxZ.ToString("0.###", CultureInfo.InvariantCulture)
                );
            }

            DumpTopCounts("[RoadContentStats] highway", highwayCounts, maxTop);
            DumpTopCounts("[RoadContentStats] surface", surfaceCounts, maxTop);
            DumpTopCounts("[RoadContentStats] lanes", laneCounts, maxTop);
            DumpTopCounts("[RoadContentStats] oneway", onewayCounts, maxTop);
            DumpTopCounts("[RoadContentStats] topName", nameCounts, Math.Min(maxTop, 30));

            return validMeta;
        }

        internal static int DumpVanillaRoadPrefabDiagnostics(int maxPrefabs = 200)
        {
            var resolver = EnsureResolverForDiagnostics();

            if (resolver == null)
                return 0;

            resolver.DumpPrefabDiagnostics(maxPrefabs);
            return resolver.CandidateCount;
        }

        internal static int DumpExactRoadPrefabNameDiagnostics(int maxPrefabs = 200)
        {
            var resolver = EnsureResolverForDiagnostics();

            if (resolver == null)
                return 0;

            resolver.DumpExactPrefabNameDiagnostics(maxPrefabs);
            return resolver.CandidateCount;
        }

        internal static int DumpCachedRoadImportMappingDiagnostics(int maxTop = 50)
        {
            var roadLines = _cachedRoadLines;

            if (roadLines == null || roadLines.Count == 0)
            {
                Util.Log.Info("[RoadImportMap] unavailable: cached road lines empty.");
                return 0;
            }

            var resolver = EnsureResolverForDiagnostics();

            if (resolver == null)
            {
                Util.Log.Info("[RoadImportMap] unavailable: no resolver.");
                return 0;
            }

            var mappingCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var prefabCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var fallbackLikeCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var scanned = 0;
            var skippedPath = 0;
            var skippedNoMeta = 0;
            var skippedBridgeTunnel = 0;
            var nullPrefab = 0;

            for (var i = 0; i < roadLines.Count; i++)
            {
                var roadLine = roadLines[i];

                if (roadLine == null)
                    continue;

                if (roadLine.IsPath)
                {
                    skippedPath++;
                    continue;
                }

                var meta = RoadImportMetadata.FromRoadLine(roadLine);

                if (meta == null)
                {
                    skippedNoMeta++;
                    continue;
                }

                if (_cachedConfig != null && !_cachedConfig.RuntimeRoadImportIncludeBridgeTunnel && (meta.Bridge || meta.Tunnel))
                {
                    skippedBridgeTunnel++;
                    continue;
                }

                scanned++;

                var prefab = resolver.ResolveForRoad(meta);

                if (prefab == Entity.Null)
                {
                    nullPrefab++;
                    continue;
                }

                var prefabName = resolver.GetPrefabName(prefab);
                var highway = NormalizeStatKey(meta.Highway, "unknown");
                var surface = NormalizeStatKey(ReadDiagnosticTag(roadLine, meta, "surface", "Surface", "surfaceTag", "SurfaceTag"), "unknown");
                var lanes = NormalizeStatKey(ReadDiagnosticTag(roadLine, meta, "lanes", "Lanes", "targetLaneCount", "TargetLaneCount"), "unknown");
                var key =
                    "highway=" + highway +
                    " lanes=" + lanes +
                    " oneway=" + meta.Oneway +
                    " surface=" + surface +
                    " -> prefab=" + NormalizeStatKey(prefabName, "<null>") +
                    " entity=" + prefab.Index;

                Increment(mappingCounts, key);
                Increment(prefabCounts, NormalizeStatKey(prefabName, "<null>") + " entity=" + prefab.Index);

                if (IsFallbackLikeMapping(highway, surface, prefabName))
                    Increment(fallbackLikeCounts, key);
            }

            Util.Log.Info(
                "[RoadImportMap] scanned=" + scanned +
                ", skippedPath=" + skippedPath +
                ", skippedNoMeta=" + skippedNoMeta +
                ", skippedBridgeTunnel=" + skippedBridgeTunnel +
                ", nullPrefab=" + nullPrefab +
                ", uniqueMappings=" + mappingCounts.Count +
                ", uniquePrefabs=" + prefabCounts.Count
            );

            DumpTopCounts("[RoadImportMap] prefab", prefabCounts, Math.Min(maxTop, 80));
            DumpTopCounts("[RoadImportMap] mapping", mappingCounts, maxTop);
            DumpTopCounts("[RoadImportMap] fallbackLike", fallbackLikeCounts, Math.Min(maxTop, 30));

            return scanned;
        }

        private static RoadPrefabResolver EnsureResolverForDiagnostics()
        {
            var world = World.DefaultGameObjectInjectionWorld;

            if (world == null)
            {
                Util.Log.Info("[RoadImportDiag] resolver unavailable: World null.");
                return null;
            }

            try
            {
                var entityManager = world.EntityManager;
                var prefabSystem = world.GetOrCreateSystemManaged<PrefabSystem>();
                var resolver = EnsureResolver(entityManager, prefabSystem);

                if (resolver == null || !resolver.TryInitialize())
                {
                    Util.Log.Info("[RoadImportDiag] resolver unavailable: initialization failed.");
                    return null;
                }

                return resolver;
            }
            catch (Exception ex)
            {
                Util.Log.Error("[RoadImportDiag] resolver diagnostic failed: " + ex.Message);
                return null;
            }
        }

        private static void UpdateWorldExtent(GeoRoadLine roadLine, ref float minX, ref float minZ, ref float maxX, ref float maxZ, ref bool hasExtent)
        {
            if (roadLine == null || roadLine.Points == null || _cachedConfig == null)
                return;

            for (var i = 0; i < roadLine.Points.Count; i++)
            {
                var world = GeoTransform.ToWorld(roadLine.Points[i], _cachedConfig, _cachedOriginLon, _cachedOriginLat);

                if (world.x < minX) minX = world.x;
                if (world.z < minZ) minZ = world.z;
                if (world.x > maxX) maxX = world.x;
                if (world.z > maxZ) maxZ = world.z;

                hasExtent = true;
            }
        }

        private static bool IsFallbackLikeMapping(string highway, string surface, string prefabName)
        {
            var h = NormalizeStatKey(highway, "unknown").ToLowerInvariant();
            var s = NormalizeStatKey(surface, "unknown").ToLowerInvariant();
            var p = NormalizeStatKey(prefabName, "unknown").ToLowerInvariant();

            if ((h == "primary" || h == "secondary" || h == "tertiary" || h == "residential" || h == "living_street") && p.Contains("gravel"))
                return true;

            if (s != "gravel" && s != "dirt" && s != "unpaved" && p.Contains("gravel"))
                return true;

            if ((h == "primary" || h == "secondary") && (p.Contains("small road") || p.Contains("alley")))
                return true;

            if ((h == "residential" || h == "living_street") && p.Contains("highway"))
                return true;

            return false;
        }

        private static void DumpTopCounts(string prefix, Dictionary<string, int> counts, int max)
        {
            if (counts == null || counts.Count == 0)
            {
                Util.Log.Info(prefix + " <none>=0");
                return;
            }

            var items = new List<KeyValuePair<string, int>>(counts);
            items.Sort((a, b) =>
            {
                var compare = b.Value.CompareTo(a.Value);
                return compare != 0 ? compare : string.Compare(a.Key, b.Key, StringComparison.OrdinalIgnoreCase);
            });

            var limit = Math.Max(0, Math.Min(max, items.Count));

            for (var i = 0; i < limit; i++)
            {
                Util.Log.Info(prefix + " " + Safe(items[i].Key) + "=" + items[i].Value);
            }
        }

        private static void Increment(Dictionary<string, int> counts, string key)
        {
            key = NormalizeStatKey(key, "unknown");

            int count;
            counts.TryGetValue(key, out count);
            counts[key] = count + 1;
        }

        private static string NormalizeStatKey(string value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
                return fallback;

            return value.Trim().Replace("\r", " ").Replace("\n", " ");
        }

        private static string ReadDiagnosticTag(object primary, object secondary, params string[] names)
        {
            var value = ReadDiagnosticTag(primary, names);

            if (!string.IsNullOrWhiteSpace(value))
                return value;

            return ReadDiagnosticTag(secondary, names);
        }

        private static string ReadDiagnosticTag(object target, params string[] names)
        {
            if (target == null || names == null)
                return null;

            for (var i = 0; i < names.Length; i++)
            {
                object value;

                if (TryReadDiagnosticMember(target, names[i], out value))
                    return ConvertDiagnosticValue(value);
            }

            var type = target.GetType();
            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            for (var i = 0; i < fields.Length; i++)
            {
                object value;

                try
                {
                    value = fields[i].GetValue(target);
                }
                catch
                {
                    continue;
                }

                var json = value as JObject;

                if (json != null)
                {
                    var found = ReadDiagnosticJson(json, names);

                    if (!string.IsNullOrWhiteSpace(found))
                        return found;
                }
            }

            var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            for (var i = 0; i < properties.Length; i++)
            {
                var property = properties[i];

                if (!property.CanRead || property.GetIndexParameters().Length != 0)
                    continue;

                object value;

                try
                {
                    value = property.GetValue(target, null);
                }
                catch
                {
                    continue;
                }

                var json = value as JObject;

                if (json != null)
                {
                    var found = ReadDiagnosticJson(json, names);

                    if (!string.IsNullOrWhiteSpace(found))
                        return found;
                }
            }

            return null;
        }

        private static bool TryReadDiagnosticMember(object target, string name, out object value)
        {
            value = null;

            if (target == null || string.IsNullOrWhiteSpace(name))
                return false;

            var type = target.GetType();
            var property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase);

            if (property != null && property.CanRead && property.GetIndexParameters().Length == 0)
            {
                try
                {
                    value = property.GetValue(target, null);
                    return value != null;
                }
                catch
                {
                }
            }

            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase);

            if (field != null)
            {
                try
                {
                    value = field.GetValue(target);
                    return value != null;
                }
                catch
                {
                }
            }

            return false;
        }

        private static string ReadDiagnosticJson(JObject json, string[] names)
        {
            if (json == null || names == null)
                return null;

            for (var i = 0; i < names.Length; i++)
            {
                JToken token;

                if (json.TryGetValue(names[i], StringComparison.OrdinalIgnoreCase, out token) && token != null && token.Type != JTokenType.Null)
                    return ConvertDiagnosticValue(token);
            }

            return null;
        }

        private static string ConvertDiagnosticValue(object value)
        {
            if (value == null)
                return null;

            var token = value as JToken;

            if (token != null)
                return token.Type == JTokenType.Null ? null : token.ToString(Newtonsoft.Json.Formatting.None);

            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }
    }
}
