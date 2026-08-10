using System;
using System.Collections.Generic;
using CityTimelineMod.Rendering.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CityTimelineMod.Rendering.Statistics
{
    internal sealed class BundleHudSourceState
    {
        internal readonly string Key;
        internal bool Available;
        internal string Path = "";
        internal string Error = "";

        internal BundleHudSourceState(string key)
        {
            Key = key ?? "";
        }
    }

    internal sealed class BundleHudSubcategoryStats
    {
        internal readonly string Key;
        internal string Label;
        internal int Count;

        internal BundleHudSubcategoryStats(string key, string label)
        {
            Key = key ?? "";
            Label = label ?? "";
        }
    }

    internal sealed class BundleHudServiceFamilyStats
    {
        internal readonly string Key;
        internal string Label;
        internal bool Implemented;
        internal bool Available;
        internal int Count;
        internal readonly SortedDictionary<string, BundleHudSubcategoryStats> Subcategories =
            new SortedDictionary<string, BundleHudSubcategoryStats>(StringComparer.Ordinal);

        internal BundleHudServiceFamilyStats(string key, string label)
        {
            Key = key ?? "";
            Label = label ?? "";
        }

        internal int GetSubcategoryCount(string key)
        {
            BundleHudSubcategoryStats subcategory;
            return !string.IsNullOrWhiteSpace(key) && Subcategories.TryGetValue(key, out subcategory)
                ? subcategory.Count
                : 0;
        }
    }

    /// <summary>
    /// Data-only HUD contract. It distinguishes a legitimate zero from an
    /// unavailable source and serializes keys in deterministic order.
    /// </summary>
    internal sealed class BundleHudSnapshot
    {
        internal const string Schema = "citytimeline.bundle-hud-stats.v1";

        internal static readonly string[] ZoningKeys =
        {
            "residential_high",
            "residential_medium",
            "residential_low",
            "commercial_high",
            "commercial_low",
            "retail",
            "industrial",
            "parking_structure",
            "parking_surface",
            "office",
            "mixed",
            "unknown"
        };

        private static readonly KeyValuePair<string, string>[] RoadCategories =
        {
            new KeyValuePair<string, string>("highway", "Autoroute"),
            new KeyValuePair<string, string>("large_road", "Axe principal"),
            new KeyValuePair<string, string>("medium_road", "Route secondaire"),
            new KeyValuePair<string, string>("small_road", "Route tertiaire / résidentielle"),
            new KeyValuePair<string, string>("ramp", "Bretelle / liaison"),
            new KeyValuePair<string, string>("gravel_road", "Route non classée"),
            new KeyValuePair<string, string>("pathway", "Chemin / piéton")
        };

        internal static readonly string[] RoadCategoryKeys =
            Array.ConvertAll(RoadCategories, category => category.Key);

        internal string PackRoot = "";
        internal string GeneratedAt = "";
        internal string City = "";
        internal string Bbox = "";
        internal int ZoningFeatureCount;
        internal int? AllBundleFeatureCount;
        internal int? UniqueOsmElementCount;
        internal int ExpectedSourceFileCount;
        internal int PresentSourceFileCount;
        internal readonly List<string> MissingSourceFiles = new List<string>();

        internal bool CoverageComplete
        {
            get
            {
                return ExpectedSourceFileCount > 0 &&
                    PresentSourceFileCount == ExpectedSourceFileCount &&
                    MissingSourceFiles.Count == 0;
            }
        }

        internal readonly SortedDictionary<string, int> LayerCounts =
            new SortedDictionary<string, int>(StringComparer.Ordinal);
        internal readonly SortedDictionary<string, int> RoadCategoryCounts =
            new SortedDictionary<string, int>(StringComparer.Ordinal);
        internal readonly SortedDictionary<string, string> RoadCategoryLabels =
            new SortedDictionary<string, string>(StringComparer.Ordinal);
        internal readonly SortedDictionary<string, string> RoadCategoryColors =
            new SortedDictionary<string, string>(StringComparer.Ordinal);
        internal readonly SortedDictionary<string, int> ZoningCounts =
            new SortedDictionary<string, int>(StringComparer.Ordinal);
        internal readonly SortedDictionary<string, BundleHudServiceFamilyStats> Services =
            new SortedDictionary<string, BundleHudServiceFamilyStats>(StringComparer.Ordinal);
        internal readonly SortedDictionary<string, BundleHudSourceState> Sources =
            new SortedDictionary<string, BundleHudSourceState>(StringComparer.Ordinal);

        internal BundleHudSnapshot()
        {
            EnsureDefaults();
        }

        internal bool Available
        {
            get
            {
                foreach (var source in Sources.Values)
                {
                    if (source.Available)
                        return true;
                }

                return false;
            }
        }

        internal int GetLayerCount(string key)
        {
            return GetCount(LayerCounts, key);
        }

        internal int GetRoadCategoryCount(string key)
        {
            return GetCount(RoadCategoryCounts, key);
        }

        internal int GetZoningCount(string key)
        {
            return GetCount(ZoningCounts, key);
        }

        internal int GetServiceFamilyCount(string familyKey)
        {
            BundleHudServiceFamilyStats family;
            return !string.IsNullOrWhiteSpace(familyKey) && Services.TryGetValue(familyKey, out family)
                ? family.Count
                : 0;
        }

        internal int GetServiceSubcategoryCount(string familyKey, string subcategoryKey)
        {
            BundleHudServiceFamilyStats family;
            return !string.IsNullOrWhiteSpace(familyKey) && Services.TryGetValue(familyKey, out family)
                ? family.GetSubcategoryCount(subcategoryKey)
                : 0;
        }

        internal BundleHudSourceState GetSource(string key)
        {
            BundleHudSourceState source;
            return !string.IsNullOrWhiteSpace(key) && Sources.TryGetValue(key, out source)
                ? source
                : null;
        }

        internal int GetTotalServices()
        {
            var total = 0;
            foreach (var family in Services.Values)
                total += family.Count;
            return total;
        }

        internal int GetTotalZoning()
        {
            int indexed;
            if (LayerCounts.TryGetValue("zoning_polygons", out indexed))
                return indexed;

            var total = 0;
            foreach (var pair in ZoningCounts)
                total += pair.Value;
            return total;
        }

        internal int GetObjectCount()
        {
            return GetLayerCount("all_features");
        }

        internal string ToJson()
        {
            return ToJObject().ToString(Formatting.None);
        }

        internal JObject ToJObject()
        {
            var root = new JObject
            {
                ["schema"] = Schema,
                ["available"] = Available,
                ["generatedAt"] = GeneratedAt ?? "",
                ["city"] = City ?? "",
                ["bbox"] = Bbox ?? ""
            };

            root["sources"] = BuildSourcesJson();
            root["coverage"] = new JObject
            {
                ["complete"] = CoverageComplete,
                ["presentFiles"] = PresentSourceFileCount,
                ["expectedFiles"] = ExpectedSourceFileCount,
                ["missingFiles"] = new JArray(MissingSourceFiles)
            };
            root["totals"] = new JObject
            {
                ["allFeatures"] = GetObjectCount(),
                ["zoning"] = GetTotalZoning(),
                ["roads"] = GetLayerCount("roads"),
                ["paths"] = GetLayerCount("paths"),
                ["railways"] = GetLayerCount("railways"),
                ["waterLines"] = GetLayerCount("water_lines_clipped"),
                ["waterAreas"] = GetLayerCount("water_areas_clipped"),
                ["services"] = GetTotalServices()
            };
            root["layers"] = BuildCountObject(LayerCounts);
            root["roads"] = BuildRoadsJson();
            root["zoning"] = BuildZoningJson();
            root["services"] = BuildServicesJson();
            return root;
        }

        internal void SetSource(string key, string path, bool available, string error)
        {
            BundleHudSourceState state;
            if (!Sources.TryGetValue(key, out state))
            {
                state = new BundleHudSourceState(key);
                Sources[key] = state;
            }

            state.Path = path ?? "";
            state.Available = available;
            state.Error = error ?? "";
        }

        internal BundleHudServiceFamilyStats EnsureServiceFamily(string key, string label)
        {
            BundleHudServiceFamilyStats family;
            if (!Services.TryGetValue(key, out family))
            {
                family = new BundleHudServiceFamilyStats(key, label);
                Services[key] = family;
            }
            else if (!string.IsNullOrWhiteSpace(label))
            {
                family.Label = label;
            }

            return family;
        }

        private void EnsureDefaults()
        {
            Sources["layerIndex"] = new BundleHudSourceState("layerIndex");
            Sources["roadsIndex"] = new BundleHudSourceState("roadsIndex");
            Sources["servicesIndex"] = new BundleHudSourceState("servicesIndex");
            Sources["zoningPolygons"] = new BundleHudSourceState("zoningPolygons");
            Sources["extractionReport"] = new BundleHudSourceState("extractionReport");

            for (var i = 0; i < ZoningKeys.Length; i++)
                ZoningCounts[ZoningKeys[i]] = 0;

            for (var i = 0; i < RoadCategories.Length; i++)
            {
                var category = RoadCategories[i];
                RoadCategoryCounts[category.Key] = 0;
                RoadCategoryLabels[category.Key] = category.Value;
            }

            var families = ServiceCatalog.Families;
            for (var i = 0; i < families.Count; i++)
            {
                var definition = families[i];
                var family = EnsureServiceFamily(definition.Key, definition.Label);
                family.Implemented = true;

                for (var j = 0; j < definition.Subcategories.Count; j++)
                {
                    var subcategory = definition.Subcategories[j];
                    family.Subcategories[subcategory.Key] =
                        new BundleHudSubcategoryStats(subcategory.Key, subcategory.Label);
                }
            }
        }

        private JObject BuildSourcesJson()
        {
            var result = new JObject();
            var ordered = new[] { "layerIndex", "roadsIndex", "servicesIndex", "zoningPolygons", "extractionReport" };
            var emitted = new HashSet<string>(StringComparer.Ordinal);

            for (var i = 0; i < ordered.Length; i++)
            {
                BundleHudSourceState source;
                if (Sources.TryGetValue(ordered[i], out source))
                {
                    result[ordered[i]] = SourceToJson(source);
                    emitted.Add(ordered[i]);
                }
            }

            foreach (var pair in Sources)
            {
                if (!emitted.Contains(pair.Key))
                    result[pair.Key] = SourceToJson(pair.Value);
            }

            return result;
        }

        private static JObject SourceToJson(BundleHudSourceState source)
        {
            return new JObject
            {
                ["available"] = source != null && source.Available,
                ["error"] = source != null ? source.Error ?? "" : ""
            };
        }

        private JObject BuildRoadsJson()
        {
            var categories = new JObject();
            for (var i = 0; i < RoadCategoryKeys.Length; i++)
                AddRoadCategoryJson(categories, RoadCategoryKeys[i]);

            foreach (var pair in RoadCategoryCounts)
            {
                if (Array.IndexOf(RoadCategoryKeys, pair.Key) < 0)
                    AddRoadCategoryJson(categories, pair.Key);
            }

            return new JObject { ["categories"] = categories };
        }

        private void AddRoadCategoryJson(JObject target, string key)
        {
            string label;
            string color;
            RoadCategoryLabels.TryGetValue(key, out label);
            RoadCategoryColors.TryGetValue(key, out color);
            target[key] = new JObject
            {
                ["label"] = label ?? key,
                ["color"] = color ?? "",
                ["count"] = GetRoadCategoryCount(key)
            };
        }

        private JObject BuildZoningJson()
        {
            var result = new JObject { ["featureCount"] = ZoningFeatureCount };
            var counts = new JObject();
            for (var i = 0; i < ZoningKeys.Length; i++)
                counts[ZoningKeys[i]] = GetZoningCount(ZoningKeys[i]);

            foreach (var pair in ZoningCounts)
            {
                if (Array.IndexOf(ZoningKeys, pair.Key) < 0)
                    counts[pair.Key] = pair.Value;
            }

            result["counts"] = counts;
            return result;
        }

        private JObject BuildServicesJson()
        {
            var result = new JObject();
            var emitted = new HashSet<string>(StringComparer.Ordinal);
            var catalog = ServiceCatalog.Families;

            for (var i = 0; i < catalog.Count; i++)
            {
                BundleHudServiceFamilyStats family;
                if (Services.TryGetValue(catalog[i].Key, out family))
                {
                    result[family.Key] = ServiceToJson(family, catalog[i]);
                    emitted.Add(family.Key);
                }
            }

            foreach (var pair in Services)
            {
                if (!emitted.Contains(pair.Key))
                    result[pair.Key] = ServiceToJson(pair.Value, null);
            }

            return result;
        }

        private static JObject ServiceToJson(
            BundleHudServiceFamilyStats family,
            ServiceFamilyDefinition catalogDefinition
        )
        {
            var subcategories = new JObject();
            var emitted = new HashSet<string>(StringComparer.Ordinal);

            if (catalogDefinition != null)
            {
                for (var i = 0; i < catalogDefinition.Subcategories.Count; i++)
                {
                    var definition = catalogDefinition.Subcategories[i];
                    BundleHudSubcategoryStats stats;
                    if (!family.Subcategories.TryGetValue(definition.Key, out stats))
                        stats = new BundleHudSubcategoryStats(definition.Key, definition.Label);

                    subcategories[definition.Key] = SubcategoryToJson(stats);
                    emitted.Add(definition.Key);
                }
            }

            foreach (var pair in family.Subcategories)
            {
                if (!emitted.Contains(pair.Key))
                    subcategories[pair.Key] = SubcategoryToJson(pair.Value);
            }

            return new JObject
            {
                ["label"] = family.Label ?? family.Key,
                ["implemented"] = family.Implemented,
                ["available"] = family.Available,
                ["count"] = family.Count,
                ["subcategories"] = subcategories
            };
        }

        private static JObject SubcategoryToJson(BundleHudSubcategoryStats stats)
        {
            return new JObject
            {
                ["label"] = stats.Label ?? stats.Key,
                ["count"] = stats.Count
            };
        }

        private static JObject BuildCountObject(SortedDictionary<string, int> counts)
        {
            var result = new JObject();
            foreach (var pair in counts)
                result[pair.Key] = pair.Value;
            return result;
        }

        private static int GetCount(IDictionary<string, int> counts, string key)
        {
            int value;
            return counts != null && !string.IsNullOrWhiteSpace(key) && counts.TryGetValue(key, out value)
                ? value
                : 0;
        }
    }
}
