using System;
using System.Collections.Generic;
using CityTimelineMod.Importers;
using CityTimelineMod.Rendering.Railways;
using CityTimelineMod.Rendering.Services;
using CityTimelineMod.Rendering.Statistics;
using Newtonsoft.Json.Linq;

namespace CityTimelineMod.Rendering
{
    internal sealed partial class GroundOverlayBehaviour
    {
        private BundleHudSnapshot _bundleHudSnapshot = new BundleHudSnapshot();

        private void InitializeBundleHudSnapshot(BundleHudSnapshot snapshot)
        {
            _bundleHudSnapshot = snapshot ?? new BundleHudSnapshot();
            ApplyRuntimeStatisticsFallbacks();
            ApplyLoadedServiceStatistics();
        }

        internal string GetBundleHudSnapshotJson()
        {
            var stats = _bundleHudSnapshot ?? new BundleHudSnapshot();
            var root = new JObject
            {
                ["available"] = stats.Available,
                ["status"] = stats.Available
                    ? "Données Realmap chargées dans CityTimelineMod."
                    : "Statistiques détaillées indisponibles pour ce bundle.",
                ["bundleId"] = _config != null ? _config.ActiveBundleId ?? "" : "",
                ["bundleName"] = ResolveBundleDisplayName(stats),
                ["objects"] = stats.GetObjectCount()
            };

            root["zoning"] = BuildZoningHudJson(stats);
            root["roads"] = BuildRoadHudJson(stats);
            root["water"] = new JObject
            {
                ["lines"] = stats.GetLayerCount("water_lines_clipped"),
                ["areas"] = stats.GetLayerCount("water_areas_clipped")
            };
            root["services"] = BuildServiceHudJson(stats);
            root["railway"] = BuildRailwayHudJson();
            return root.ToString(Newtonsoft.Json.Formatting.None);
        }

        private string ResolveBundleDisplayName(BundleHudSnapshot stats)
        {
            if (_config != null && !string.IsNullOrWhiteSpace(_config.ActiveBundleDisplayName))
                return _config.ActiveBundleDisplayName;
            if (stats != null && !string.IsNullOrWhiteSpace(stats.City))
                return stats.City;
            return _config != null ? _config.ActiveBundleId ?? "" : "";
        }

        private static JObject BuildZoningHudJson(BundleHudSnapshot stats)
        {
            var result = new JObject();
            for (var i = 0; i < BundleHudSnapshot.ZoningKeys.Length; i++)
            {
                var key = BundleHudSnapshot.ZoningKeys[i];
                result[key] = stats.GetZoningCount(key);
            }
            return result;
        }

        private static JObject BuildRoadHudJson(BundleHudSnapshot stats)
        {
            return new JObject
            {
                ["highway"] = stats.GetRoadCategoryCount("highway"),
                ["main"] = stats.GetRoadCategoryCount("large_road"),
                ["secondary"] = stats.GetRoadCategoryCount("medium_road"),
                ["tertiary"] = stats.GetRoadCategoryCount("small_road"),
                ["ramp"] = stats.GetRoadCategoryCount("ramp"),
                ["unclassified"] = stats.GetRoadCategoryCount("gravel_road"),
                ["paths"] = stats.GetRoadCategoryCount("pathway"),
                ["total"] = stats.GetLayerCount("roads")
            };
        }

        private JArray BuildServiceHudJson(BundleHudSnapshot stats)
        {
            var result = new JArray();
            var catalog = ServiceCatalog.Families;

            for (var i = 0; i < catalog.Count; i++)
            {
                var definition = catalog[i];
                BundleHudServiceFamilyStats family;
                stats.Services.TryGetValue(definition.Key, out family);
                var available = IsServiceFamilyAvailable(definition.Key);
                var subcategories = new JArray();

                for (var j = 0; j < definition.Subcategories.Count; j++)
                {
                    var subcategory = definition.Subcategories[j];
                    subcategories.Add(new JObject
                    {
                        ["key"] = subcategory.Key,
                        ["label"] = subcategory.Label,
                        ["count"] = NullableHudCount(
                            available,
                            family != null ? family.GetSubcategoryCount(subcategory.Key) : 0
                        )
                    });
                }

                result.Add(new JObject
                {
                    ["key"] = definition.Key,
                    ["label"] = family != null && !string.IsNullOrWhiteSpace(family.Label)
                        ? family.Label
                        : definition.Label,
                    ["available"] = available,
                    ["total"] = NullableHudCount(available, family != null ? family.Count : 0),
                    ["visible"] = GetServiceFamilyVisible(definition.Key),
                    ["opacity"] = GetServiceFamilyOpacity(definition.Key),
                    ["subcategories"] = subcategories
                });
            }

            return result;
        }

        private JObject BuildRailwayHudJson()
        {
            var stats = _railwayStats ?? new RailwaySemanticStats();
            return new JObject
            {
                ["available"] = _railwayAvailable,
                ["train"] = NullableHudCount(_railwayAvailable, stats.Train),
                ["tram"] = NullableHudCount(_railwayAvailable, stats.Tram),
                ["lightRail"] = NullableHudCount(_railwayAvailable, stats.LightRail),
                ["subway"] = NullableHudCount(_railwayAvailable, stats.Subway),
                ["service"] = NullableHudCount(_railwayAvailable, stats.Service),
                ["tunnels"] = NullableHudCount(_railwayAvailable, stats.Tunnels),
                ["total"] = NullableHudCount(_railwayAvailable, stats.Total)
            };
        }

        private static JToken NullableHudCount(bool available, int value)
        {
            return available ? new JValue(value) : JValue.CreateNull();
        }

        private void ApplyLoadedServiceStatistics()
        {
            if (_bundleHudSnapshot == null)
                return;

            var catalog = ServiceCatalog.Families;
            for (var i = 0; i < catalog.Count; i++)
            {
                var definition = catalog[i];
                var family = _bundleHudSnapshot.EnsureServiceFamily(definition.Key, definition.Label);
                family.Available = IsServiceFamilyAvailable(definition.Key);
                family.Count = 0;

                for (var j = 0; j < definition.Subcategories.Count; j++)
                {
                    var subcategory = definition.Subcategories[j];
                    BundleHudSubcategoryStats subcategoryStats;
                    if (!family.Subcategories.TryGetValue(subcategory.Key, out subcategoryStats))
                    {
                        subcategoryStats = new BundleHudSubcategoryStats(subcategory.Key, subcategory.Label);
                        family.Subcategories[subcategory.Key] = subcategoryStats;
                    }
                    subcategoryStats.Count = 0;
                }
            }

            if (_servicePoints == null)
                return;

            for (var i = 0; i < _servicePoints.Count; i++)
            {
                var point = _servicePoints[i];
                if (point == null || string.IsNullOrWhiteSpace(point.FamilyKey))
                    continue;

                var family = _bundleHudSnapshot.EnsureServiceFamily(point.FamilyKey, point.FamilyLabel);
                family.Available = true;
                family.Count++;

                BundleHudSubcategoryStats subcategory;
                if (!family.Subcategories.TryGetValue(point.SubcategoryKey, out subcategory))
                {
                    subcategory = new BundleHudSubcategoryStats(point.SubcategoryKey, point.SubcategoryLabel);
                    family.Subcategories[point.SubcategoryKey] = subcategory;
                }
                subcategory.Count++;
            }
        }

        private void ApplyRuntimeStatisticsFallbacks()
        {
            if (_bundleHudSnapshot == null)
                return;

            var runtimeUsed = false;
            if (!_bundleHudSnapshot.GetSource("zoningPolygons").Available && _zoningPolygons != null)
            {
                for (var i = 0; i < BundleHudSnapshot.ZoningKeys.Length; i++)
                    _bundleHudSnapshot.ZoningCounts[BundleHudSnapshot.ZoningKeys[i]] = 0;

                for (var i = 0; i < _zoningPolygons.Count; i++)
                {
                    var polygon = _zoningPolygons[i];
                    if (polygon == null)
                        continue;
                    var key = BundleHudStatsLoader.ClassifyZoning(polygon.Cs2, polygon.Zone);
                    _bundleHudSnapshot.ZoningCounts[key] = _bundleHudSnapshot.GetZoningCount(key) + 1;
                }

                _bundleHudSnapshot.ZoningFeatureCount = _zoningPolygons.Count;
                _bundleHudSnapshot.LayerCounts["zoning_polygons"] = _zoningPolygons.Count;
                runtimeUsed = true;
            }

            if (!_bundleHudSnapshot.GetSource("roadsIndex").Available && _roadLines != null)
            {
                for (var i = 0; i < BundleHudSnapshot.RoadCategoryKeys.Length; i++)
                    _bundleHudSnapshot.RoadCategoryCounts[BundleHudSnapshot.RoadCategoryKeys[i]] = 0;

                for (var i = 0; i < _roadLines.Count; i++)
                {
                    var road = _roadLines[i];
                    var category = ResolveRoadStatisticsCategory(road);
                    _bundleHudSnapshot.RoadCategoryCounts[category] =
                        _bundleHudSnapshot.GetRoadCategoryCount(category) + 1;
                }
                runtimeUsed = true;
            }

            var roadStats = _roadSemanticStats;
            if (!_bundleHudSnapshot.GetSource("layerIndex").Available)
            {
                _bundleHudSnapshot.LayerCounts["roads"] = roadStats != null ? roadStats.Roads : 0;
                _bundleHudSnapshot.LayerCounts["paths"] = roadStats != null ? roadStats.Paths : 0;
                _bundleHudSnapshot.LayerCounts["water_lines_clipped"] = _waterLines != null ? _waterLines.Count : 0;
                _bundleHudSnapshot.LayerCounts["water_areas_clipped"] = _waterAreaOutlines != null ? _waterAreaOutlines.Count : 0;
                _bundleHudSnapshot.LayerCounts["railways"] = _railwayLines != null ? _railwayLines.Count : 0;
                _bundleHudSnapshot.LayerCounts["all_features"] =
                    _bundleHudSnapshot.GetLayerCount("zoning_polygons") +
                    _bundleHudSnapshot.GetLayerCount("roads") +
                    _bundleHudSnapshot.GetLayerCount("paths") +
                    _bundleHudSnapshot.GetLayerCount("water_lines_clipped") +
                    _bundleHudSnapshot.GetLayerCount("water_areas_clipped");
                runtimeUsed = true;
            }

            if (runtimeUsed)
                _bundleHudSnapshot.SetSource("runtime", "", true, "");
        }

        private static string ResolveRoadStatisticsCategory(GeoRoadLine road)
        {
            if (road != null && road.IsPath)
                return "pathway";

            var highway = road != null && !string.IsNullOrWhiteSpace(road.Highway)
                ? road.Highway.Trim().ToLowerInvariant()
                : "";
            if (highway.EndsWith("_link", StringComparison.Ordinal))
                return "ramp";

            switch (highway)
            {
                case "motorway":
                case "trunk": return "highway";
                case "primary": return "large_road";
                case "secondary": return "medium_road";
                case "tertiary":
                case "residential":
                case "living_street": return "small_road";
                default: return "gravel_road";
            }
        }

        private string FormatBundleHudCount(string section, int value)
        {
            var stats = _bundleHudSnapshot;
            if (stats == null)
                return "—";

            if (section == "railway")
                return _railwayAvailable ? value.ToString() : "—";

            if (section.StartsWith("service:", StringComparison.Ordinal))
                return IsServiceFamilyAvailable(section.Substring("service:".Length)) ? value.ToString() : "—";

            var source = stats.GetSource(section);
            var runtime = stats.GetSource("runtime");
            return (source != null && source.Available) || (runtime != null && runtime.Available)
                ? value.ToString()
                : "—";
        }

        private string FormatServiceHudTotal(BundleHudSnapshot stats)
        {
            if (stats == null)
                return "—";

            var catalog = ServiceCatalog.Families;
            for (var i = 0; i < catalog.Count; i++)
            {
                if (IsServiceFamilyAvailable(catalog[i].Key))
                    return stats.GetTotalServices().ToString();
            }

            return "—";
        }
    }
}
