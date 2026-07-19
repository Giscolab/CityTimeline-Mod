using System;
using System.Collections.Generic;

namespace CityTimelineMod.Rendering.Services
{
    internal enum ServiceMarkerShape
    {
        Diamond,
        Square,
        Cross
    }

    internal sealed class ServiceSubcategoryDefinition
    {
        internal readonly string Key;
        internal readonly string Label;

        internal ServiceSubcategoryDefinition(string key, string label)
        {
            Key = key ?? "";
            Label = label ?? "";
        }
    }

    internal sealed class ServiceFamilyDefinition
    {
        internal readonly string Key;
        internal readonly string Label;
        internal readonly string ColorHex;
        internal readonly ServiceMarkerShape MarkerShape;
        internal readonly IReadOnlyList<ServiceSubcategoryDefinition> Subcategories;

        internal ServiceFamilyDefinition(
            string key,
            string label,
            string colorHex,
            ServiceMarkerShape markerShape,
            params ServiceSubcategoryDefinition[] subcategories
        )
        {
            Key = key ?? "";
            Label = label ?? "";
            ColorHex = colorHex ?? "#ffffff";
            MarkerShape = markerShape;
            Subcategories = Array.AsReadOnly(subcategories ?? new ServiceSubcategoryDefinition[0]);
        }
    }

    /// <summary>
    /// Stable, code-facing taxonomy. Bundle labels remain informative metadata;
    /// filtering and bindings must use these keys.
    /// </summary>
    internal static class ServiceCatalog
    {
        private static readonly ServiceFamilyDefinition[] OrderedFamilies =
        {
            Family("education", "Éducation et recherche", "#4da3ff", ServiceMarkerShape.Diamond,
                Sub("primary", "École primaire"),
                Sub("secondary", "Collège / lycée"),
                Sub("university", "Université"),
                Sub("research", "Recherche")),
            Family("fire", "Sapeurs-pompiers", "#ff5a52", ServiceMarkerShape.Cross,
                Sub("local_station", "Caserne locale"),
                Sub("large_station", "Grande caserne"),
                Sub("special_rescue", "Surveillance / secours spécialisés")),
            Family("medical", "Services médicaux et soins mortuaires", "#ff73a8", ServiceMarkerShape.Cross,
                Sub("clinic", "Clinique"),
                Sub("hospital", "Hôpital"),
                Sub("crematorium", "Crématorium"),
                Sub("cemetery", "Cimetière")),
            Family("parks", "Parcs et loisirs", "#65d66e", ServiceMarkerShape.Diamond,
                Sub("local_park", "Parc local"),
                Sub("large_park", "Grand parc"),
                Sub("sport", "Sport"),
                Sub("leisure", "Loisirs"),
                Sub("tourism", "Tourisme")),
            Family("electricity", "Électricité", "#ffd84d", ServiceMarkerShape.Square,
                Sub("generation", "Production électrique"),
                Sub("transformation", "Transformation"),
                Sub("storage", "Stockage"),
                Sub("grid", "Réseau électrique")),
            Family("waste", "Gestion des déchets", "#ad967a", ServiceMarkerShape.Square,
                Sub("collection", "Collecte"),
                Sub("recycling", "Recyclage"),
                Sub("treatment", "Traitement"),
                Sub("landfill", "Décharge / stockage")),
            Family("transport", "Transports", "#b887ff", ServiceMarkerShape.Diamond,
                Sub("bus", "Bus"),
                Sub("tram", "Tram"),
                Sub("train", "Train"),
                Sub("metro", "Métro"),
                Sub("taxi", "Taxi"),
                Sub("air", "Aérien"),
                Sub("maritime", "Maritime")),
            Family("water", "Eau et égouts", "#38c8ff", ServiceMarkerShape.Diamond,
                Sub("pumping", "Pompage"),
                Sub("water_treatment", "Traitement de l'eau"),
                Sub("sewage", "Égouts"),
                Sub("wastewater", "Traitement des eaux usées")),
            Family("communications", "Communications", "#ff9f43", ServiceMarkerShape.Square,
                Sub("post", "Poste"),
                Sub("telecom", "Télécommunications"),
                Sub("datacenter", "Serveurs / data center"),
                Sub("radio", "Radio / antennes"))
        };

        private static readonly Dictionary<string, ServiceFamilyDefinition> ByKey =
            new Dictionary<string, ServiceFamilyDefinition>(StringComparer.OrdinalIgnoreCase);

        static ServiceCatalog()
        {
            for (var i = 0; i < OrderedFamilies.Length; i++)
                ByKey[OrderedFamilies[i].Key] = OrderedFamilies[i];
        }

        internal static IReadOnlyList<ServiceFamilyDefinition> Families
        {
            get { return Array.AsReadOnly(OrderedFamilies); }
        }

        internal static ServiceFamilyDefinition GetFamily(string key)
        {
            ServiceFamilyDefinition definition;
            return !string.IsNullOrWhiteSpace(key) && ByKey.TryGetValue(key.Trim(), out definition)
                ? definition
                : null;
        }

        internal static ServiceSubcategoryDefinition GetSubcategory(string familyKey, string subcategoryKey)
        {
            var family = GetFamily(familyKey);
            if (family == null || string.IsNullOrWhiteSpace(subcategoryKey))
                return null;

            for (var i = 0; i < family.Subcategories.Count; i++)
            {
                var subcategory = family.Subcategories[i];
                if (string.Equals(subcategory.Key, subcategoryKey.Trim(), StringComparison.OrdinalIgnoreCase))
                    return subcategory;
            }

            return null;
        }

        internal static bool IsKnownFamily(string key)
        {
            return GetFamily(key) != null;
        }

        internal static bool IsKnownSubcategory(string familyKey, string subcategoryKey)
        {
            return GetSubcategory(familyKey, subcategoryKey) != null;
        }

        internal static string ResolveFamilyLabel(string key, string bundleLabel)
        {
            if (!string.IsNullOrWhiteSpace(bundleLabel))
                return bundleLabel.Trim();

            var family = GetFamily(key);
            return family != null ? family.Label : (key ?? "");
        }

        internal static string ResolveSubcategoryLabel(string familyKey, string key, string bundleLabel)
        {
            if (!string.IsNullOrWhiteSpace(bundleLabel))
                return bundleLabel.Trim();

            var subcategory = GetSubcategory(familyKey, key);
            return subcategory != null ? subcategory.Label : (key ?? "");
        }

        private static ServiceFamilyDefinition Family(
            string key,
            string label,
            string colorHex,
            ServiceMarkerShape markerShape,
            params ServiceSubcategoryDefinition[] subcategories
        )
        {
            return new ServiceFamilyDefinition(key, label, colorHex, markerShape, subcategories);
        }

        private static ServiceSubcategoryDefinition Sub(string key, string label)
        {
            return new ServiceSubcategoryDefinition(key, label);
        }
    }
}
