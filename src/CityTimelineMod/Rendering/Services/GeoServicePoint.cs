using System;
using System.Collections.Generic;
using CityTimelineMod.Importers;

namespace CityTimelineMod.Rendering.Services
{
    /// <summary>
    /// One informative, purely visual service marker loaded from a bundle.
    /// OsmType is the source OSM element type; every GeoJSON geometry is Point.
    /// </summary>
    internal sealed class GeoServicePoint
    {
        internal readonly long? Id;
        internal readonly string OsmType;
        internal readonly GeoPoint Point;
        internal readonly string Name;
        internal readonly string FamilyKey;
        internal readonly string FamilyLabel;
        internal readonly string SubcategoryKey;
        internal readonly string SubcategoryLabel;
        internal readonly string SourceTag;
        internal readonly Dictionary<string, string> Tags;

        internal GeoServicePoint(
            long? id,
            string osmType,
            GeoPoint point,
            string name,
            string familyKey,
            string familyLabel,
            string subcategoryKey,
            string subcategoryLabel,
            string sourceTag,
            IDictionary<string, string> tags
        )
        {
            Id = id;
            OsmType = osmType ?? "";
            Point = point;
            Name = name ?? "";
            FamilyKey = familyKey ?? "";
            FamilyLabel = familyLabel ?? "";
            SubcategoryKey = subcategoryKey ?? "";
            SubcategoryLabel = subcategoryLabel ?? "";
            SourceTag = sourceTag ?? "";
            Tags = tags != null
                ? new Dictionary<string, string>(tags, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        internal string StableId
        {
            get
            {
                if (Id.HasValue)
                    return FamilyKey + ":" + OsmType + ":" + Id.Value;

                return FamilyKey + ":anonymous:" + SubcategoryKey;
            }
        }

        internal bool TryGetTag(string key, out string value)
        {
            value = null;
            return !string.IsNullOrWhiteSpace(key) && Tags.TryGetValue(key, out value);
        }
    }
}
