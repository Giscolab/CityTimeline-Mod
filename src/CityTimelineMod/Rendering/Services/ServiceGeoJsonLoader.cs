using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        // Enable/disable the exhaustive parity validation against legacy parser
        private static readonly bool ValidateServiceParserParity = false;

        // ----------------------------- ServiceFeatureData (new) -----------------------------

        private sealed class ServiceFeatureData
        {
            internal long? Id;
            internal string OsmType;
            internal string Name;
            internal string DeclaredFamily;
            internal string FamilyLabel;
            internal string Subcategory;
            internal string SubcategoryLabel;
            internal string SourceTag;

            internal string GeometryType;
            internal double Lon;
            internal double Lat;
            internal bool HasLon;
            internal bool HasLat;

            internal readonly Dictionary<string, string> Tags =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        // ----------------------------- Public entry points -----------------------------

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

        // ----------------------------- Legacy loader (kept as fallback) -----------------------------

        private static void LoadFamilyCoreLegacy(
            string path,
            ServiceFamilyDefinition definition,
            ServiceFamilyLoadResult result
        )
        {
            var totalTimer = System.Diagnostics.Stopwatch.StartNew();
            var domTimer = System.Diagnostics.Stopwatch.StartNew();

            JObject root;

            try
            {
                root = JObject.Parse(File.ReadAllText(path));
            }
            catch (Exception ex)
            {
                throw new InvalidDataException("Invalid service GeoJSON", ex);
            }

            domTimer.Stop();

            if (!string.Equals(ReadString(root, "type"), "FeatureCollection", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Service GeoJSON is not a FeatureCollection");

            var features = root["features"] as JArray;
            if (features == null)
                throw new InvalidDataException("Service GeoJSON has no features array");

            result.SourceFeatureCount = features.Count;

            var featureTimer = System.Diagnostics.Stopwatch.StartNew();

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

            featureTimer.Stop();
            totalTimer.Stop();

            CityTimelineMod.Util.Log.Info(
                "ServiceGeoJson profile: family=" +
                definition.Key +
                ", features=" +
                result.SourceFeatureCount +
                ", points=" +
                result.Points.Count +
                ", skipped=" +
                result.SkippedFeatureCount +
                ", domMs=" +
                domTimer.ElapsedMilliseconds +
                ", featureMs=" +
                featureTimer.ElapsedMilliseconds +
                ", totalMs=" +
                totalTimer.ElapsedMilliseconds
            );
        }

        // ----------------------------- New direct loader (replaces LoadFamilyCore) -----------------------------

        private static void LoadFamilyCore(
            string path,
            ServiceFamilyDefinition definition,
            ServiceFamilyLoadResult result
        )
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();

            string rootType = null;
            var featuresFound = false;

            try
            {
                using (var stream = new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    1024 * 1024,
                    FileOptions.SequentialScan))
                using (var textReader = new StreamReader(stream))
                using (var reader = new JsonTextReader(textReader))
                {
                    reader.DateParseHandling = DateParseHandling.None;
                    reader.FloatParseHandling = FloatParseHandling.Double;

                    if (!reader.Read() ||
                        reader.TokenType != JsonToken.StartObject)
                    {
                        throw new InvalidDataException(
                            "Invalid service GeoJSON root object"
                        );
                    }

                    while (reader.Read())
                    {
                        if (reader.TokenType == JsonToken.EndObject)
                            break;

                        if (reader.TokenType != JsonToken.PropertyName)
                            continue;

                        var propertyName = reader.Value != null
                            ? reader.Value.ToString()
                            : string.Empty;

                        if (!reader.Read())
                            break;

                        if (string.Equals(
                            propertyName,
                            "type",
                            StringComparison.OrdinalIgnoreCase))
                        {
                            rootType = ReadServiceStringValue(reader);
                            continue;
                        }

                        if (string.Equals(
                            propertyName,
                            "features",
                            StringComparison.OrdinalIgnoreCase))
                        {
                            if (reader.TokenType != JsonToken.StartArray)
                            {
                                throw new InvalidDataException(
                                    "Service GeoJSON has no features array"
                                );
                            }

                            featuresFound = true;

                            while (reader.Read())
                            {
                                if (reader.TokenType == JsonToken.EndArray)
                                    break;

                                // L'ancien features.Count compte aussi les entrées
                                // éventuellement invalides/non-object.
                                result.SourceFeatureCount++;

                                if (reader.TokenType != JsonToken.StartObject)
                                {
                                    SkipServiceValue(reader);
                                    result.SkippedFeatureCount++;
                                    continue;
                                }

                                GeoServicePoint point;

                                if (!TryParseFeatureDirect(
                                    reader,
                                    definition,
                                    out point))
                                {
                                    result.SkippedFeatureCount++;
                                    continue;
                                }

                                result.Points.Add(point);
                            }

                            continue;
                        }

                        SkipServiceValue(reader);
                    }
                }
            }
            catch (InvalidDataException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidDataException(
                    "Invalid service GeoJSON",
                    ex
                );
            }

            if (!string.Equals(
                rootType,
                "FeatureCollection",
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "Service GeoJSON is not a FeatureCollection"
                );
            }

            if (!featuresFound)
            {
                throw new InvalidDataException(
                    "Service GeoJSON has no features array"
                );
            }

            timer.Stop();

            CityTimelineMod.Util.Log.Info(
                "ServiceGeoJson direct profile: family=" +
                definition.Key +
                ", features=" +
                result.SourceFeatureCount +
                ", points=" +
                result.Points.Count +
                ", skipped=" +
                result.SkippedFeatureCount +
                ", totalMs=" +
                timer.ElapsedMilliseconds
            );

            // ----- Exhaustive parity validation against legacy parser -----
            if (ValidateServiceParserParity)
            {
                var legacyResult = new ServiceFamilyLoadResult(definition, path);
                LoadFamilyCoreLegacy(path, definition, legacyResult);
                ValidateServiceParserAgainstLegacy(definition, result, legacyResult, 20);
            }
        }

        // ----------------------------- Direct parsing helpers -----------------------------

        private static bool TryParseFeatureDirect(
            JsonTextReader reader,
            ServiceFamilyDefinition definition,
            out GeoServicePoint servicePoint
        )
        {
            servicePoint = null;

            var data = new ServiceFeatureData();

            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.EndObject)
                    break;

                if (reader.TokenType != JsonToken.PropertyName)
                    continue;

                var propertyName = reader.Value != null
                    ? reader.Value.ToString()
                    : string.Empty;

                if (!reader.Read())
                    break;

                if (string.Equals(
                    propertyName,
                    "properties",
                    StringComparison.OrdinalIgnoreCase))
                {
                    if (reader.TokenType == JsonToken.StartObject)
                        ReadServiceProperties(reader, data);
                    else
                        SkipServiceValue(reader);

                    continue;
                }

                if (string.Equals(
                    propertyName,
                    "geometry",
                    StringComparison.OrdinalIgnoreCase))
                {
                    if (reader.TokenType == JsonToken.StartObject)
                        ReadServiceGeometry(reader, data);
                    else
                        SkipServiceValue(reader);

                    continue;
                }

                SkipServiceValue(reader);
            }

            if (!string.Equals(
                data.GeometryType,
                "Point",
                StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!data.HasLon || !data.HasLat)
                return false;

            if (!IsFinite(data.Lon) ||
                !IsFinite(data.Lat) ||
                data.Lon < -180d ||
                data.Lon > 180d ||
                data.Lat < -90d ||
                data.Lat > 90d)
            {
                return false;
            }

            var declaredFamily =
                NormalizeKey(data.DeclaredFamily);

            if (!string.IsNullOrWhiteSpace(declaredFamily) &&
                !string.Equals(
                    declaredFamily,
                    definition.Key,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var subcategory =
                NormalizeKey(data.Subcategory);

            if (string.IsNullOrWhiteSpace(subcategory))
                subcategory = "unknown";

            servicePoint = new GeoServicePoint(
                data.Id,
                NormalizeKey(data.OsmType),
                new GeoPoint(data.Lon, data.Lat),
                data.Name ?? "",
                definition.Key,
                ServiceCatalog.ResolveFamilyLabel(
                    definition.Key,
                    data.FamilyLabel),
                subcategory,
                ServiceCatalog.ResolveSubcategoryLabel(
                    definition.Key,
                    subcategory,
                    data.SubcategoryLabel),
                data.SourceTag ?? "",
                data.Tags
            );

            return true;
        }

        private static void ReadServiceProperties(
            JsonTextReader reader,
            ServiceFeatureData data
        )
        {
            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.EndObject)
                    break;

                if (reader.TokenType != JsonToken.PropertyName)
                    continue;

                var name = reader.Value != null
                    ? reader.Value.ToString()
                    : string.Empty;

                if (!reader.Read())
                    break;

                if (string.Equals(name, "id", StringComparison.OrdinalIgnoreCase))
                {
                    data.Id = ReadServiceLongValue(reader);
                }
                else if (string.Equals(name, "type", StringComparison.OrdinalIgnoreCase))
                {
                    data.OsmType = ReadServiceStringValue(reader);
                }
                else if (string.Equals(name, "name", StringComparison.OrdinalIgnoreCase))
                {
                    data.Name = ReadServiceStringValue(reader);
                }
                else if (string.Equals(name, "family", StringComparison.OrdinalIgnoreCase))
                {
                    data.DeclaredFamily = ReadServiceStringValue(reader);
                }
                else if (string.Equals(name, "familyLabel", StringComparison.OrdinalIgnoreCase))
                {
                    data.FamilyLabel = ReadServiceStringValue(reader);
                }
                else if (string.Equals(name, "subcategory", StringComparison.OrdinalIgnoreCase))
                {
                    data.Subcategory = ReadServiceStringValue(reader);
                }
                else if (string.Equals(name, "subcategoryLabel", StringComparison.OrdinalIgnoreCase))
                {
                    data.SubcategoryLabel = ReadServiceStringValue(reader);
                }
                else if (string.Equals(name, "sourceTag", StringComparison.OrdinalIgnoreCase))
                {
                    data.SourceTag = ReadServiceStringValue(reader);
                }
                else if (string.Equals(name, "tags", StringComparison.OrdinalIgnoreCase))
                {
                    if (reader.TokenType == JsonToken.StartObject)
                        ReadServiceTags(reader, data.Tags);
                    else
                        SkipServiceValue(reader);
                }
                else
                {
                    SkipServiceValue(reader);
                }
            }
        }

        private static void ReadServiceTags(
            JsonTextReader reader,
            Dictionary<string, string> result
        )
        {
            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.EndObject)
                    break;

                if (reader.TokenType != JsonToken.PropertyName)
                    continue;

                var name = reader.Value != null
                    ? reader.Value.ToString()
                    : string.Empty;

                if (!reader.Read())
                    break;

                if (reader.TokenType == JsonToken.Null)
                    continue;

                string value;

                if (reader.TokenType == JsonToken.String)
                {
                    value = reader.Value != null
                        ? reader.Value.ToString()
                        : null;
                }
                else
                {
                    // On matérialise uniquement CETTE valeur de tag.
                    // Cela préserve exactement la représentation JSON des
                    // booléens, nombres, tableaux ou objets sans construire
                    // toute la feature en JObject.
                    var token = JToken.ReadFrom(reader);
                    value = token.ToString(Formatting.None);
                }

                result[name] = value;
            }
        }

        private static void ReadServiceGeometry(
            JsonTextReader reader,
            ServiceFeatureData data
        )
        {
            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.EndObject)
                    break;

                if (reader.TokenType != JsonToken.PropertyName)
                    continue;

                var name = reader.Value != null
                    ? reader.Value.ToString()
                    : string.Empty;

                if (!reader.Read())
                    break;

                if (string.Equals(
                    name,
                    "type",
                    StringComparison.OrdinalIgnoreCase))
                {
                    data.GeometryType =
                        ReadServiceStringValue(reader);

                    continue;
                }

                if (string.Equals(
                    name,
                    "coordinates",
                    StringComparison.OrdinalIgnoreCase))
                {
                    if (reader.TokenType == JsonToken.StartArray)
                        ReadServiceCoordinates(reader, data);
                    else
                        SkipServiceValue(reader);

                    continue;
                }

                SkipServiceValue(reader);
            }
        }

        private static void ReadServiceCoordinates(
            JsonTextReader reader,
            ServiceFeatureData data
        )
        {
            var index = 0;

            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.EndArray)
                    break;

                if (index == 0)
                {
                    double value;

                    if (TryReadServiceCoordinate(reader, out value))
                    {
                        data.Lon = value;
                        data.HasLon = true;
                    }
                    else
                    {
                        SkipServiceValue(reader);
                    }
                }
                else if (index == 1)
                {
                    double value;

                    if (TryReadServiceCoordinate(reader, out value))
                    {
                        data.Lat = value;
                        data.HasLat = true;
                    }
                    else
                    {
                        SkipServiceValue(reader);
                    }
                }
                else
                {
                    // Altitude ou dimensions supplémentaires :
                    // l'ancien coord[0]/coord[1] les ignorait.
                    SkipServiceValue(reader);
                }

                index++;
            }
        }

        private static bool TryReadServiceCoordinate(
            JsonTextReader reader,
            out double value
        )
        {
            value = 0d;

            if (reader.TokenType != JsonToken.Integer &&
                reader.TokenType != JsonToken.Float &&
                reader.TokenType != JsonToken.String)
            {
                return false;
            }

            if (reader.Value == null)
                return false;

            try
            {
                value = Convert.ToDouble(
                    reader.Value,
                    CultureInfo.InvariantCulture
                );

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string ReadServiceStringValue(
            JsonTextReader reader
        )
        {
            if (reader.TokenType == JsonToken.Null)
                return null;

            string value;

            if (reader.TokenType == JsonToken.String)
            {
                value = reader.Value != null
                    ? reader.Value.ToString()
                    : null;
            }
            else if (reader.TokenType == JsonToken.StartObject ||
                     reader.TokenType == JsonToken.StartArray)
            {
                var token = JToken.ReadFrom(reader);
                value = token.ToString();
            }
            else
            {
                value = reader.Value != null
                    ? Convert.ToString(
                        reader.Value,
                        CultureInfo.InvariantCulture)
                    : null;
            }

            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
        }

        private static long? ReadServiceLongValue(
            JsonTextReader reader
        )
        {
            if (reader.TokenType == JsonToken.Null)
                return null;

            if (reader.TokenType == JsonToken.Integer)
            {
                try
                {
                    return Convert.ToInt64(
                        reader.Value,
                        CultureInfo.InvariantCulture
                    );
                }
                catch (Exception)
                {
                    return null;
                }
            }

            string text;

            if (reader.TokenType == JsonToken.StartObject ||
                reader.TokenType == JsonToken.StartArray)
            {
                var token = JToken.ReadFrom(reader);
                text = token.ToString();
            }
            else
            {
                text = reader.Value != null
                    ? Convert.ToString(
                        reader.Value,
                        CultureInfo.InvariantCulture)
                    : null;
            }

            long value;

            return long.TryParse(
                text,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out value)
                ? value
                : (long?)null;
        }

        private static void SkipServiceValue(
            JsonTextReader reader
        )
        {
            if (reader.TokenType == JsonToken.StartObject ||
                reader.TokenType == JsonToken.StartArray)
            {
                reader.Skip();
            }
        }

        // ----------------------------- Validation helpers -----------------------------

        private static void ValidateServiceParserAgainstLegacy(
            ServiceFamilyDefinition definition,
            ServiceFamilyLoadResult direct,
            ServiceFamilyLoadResult legacy,
            int maxReported
        )
        {
            var mismatchCount = 0;
            var reportedCount = 0;

            if (direct.SourceFeatureCount != legacy.SourceFeatureCount)
            {
                mismatchCount++;

                CityTimelineMod.Util.Log.Error(
                    "Service parser diff: family=" +
                    definition.Key +
                    ", SourceFeatureCount legacy=" +
                    legacy.SourceFeatureCount +
                    " direct=" +
                    direct.SourceFeatureCount
                );

                reportedCount++;
            }

            if (direct.SkippedFeatureCount != legacy.SkippedFeatureCount)
            {
                mismatchCount++;

                if (reportedCount < maxReported)
                {
                    CityTimelineMod.Util.Log.Error(
                        "Service parser diff: family=" +
                        definition.Key +
                        ", SkippedFeatureCount legacy=" +
                        legacy.SkippedFeatureCount +
                        " direct=" +
                        direct.SkippedFeatureCount
                    );

                    reportedCount++;
                }
            }

            if (direct.Points.Count != legacy.Points.Count)
            {
                mismatchCount++;

                if (reportedCount < maxReported)
                {
                    CityTimelineMod.Util.Log.Error(
                        "Service parser diff: family=" +
                        definition.Key +
                        ", point count legacy=" +
                        legacy.Points.Count +
                        " direct=" +
                        direct.Points.Count
                    );

                    reportedCount++;
                }
            }

            var count = Math.Min(
                direct.Points.Count,
                legacy.Points.Count
            );

            for (var i = 0; i < count; i++)
            {
                var difference = DescribeServicePointDifference(
                    legacy.Points[i],
                    direct.Points[i]
                );

                if (difference == null)
                    continue;

                mismatchCount++;

                if (reportedCount < maxReported)
                {
                    CityTimelineMod.Util.Log.Error(
                        "Service parser diff: family=" +
                        definition.Key +
                        ", index=" +
                        i +
                        ", " +
                        difference
                    );

                    reportedCount++;
                }
            }

            CityTimelineMod.Util.Log.Info(
                "Service parser parity: family=" +
                definition.Key +
                ", legacyPoints=" +
                legacy.Points.Count +
                ", directPoints=" +
                direct.Points.Count +
                ", legacyFeatures=" +
                legacy.SourceFeatureCount +
                ", directFeatures=" +
                direct.SourceFeatureCount +
                ", mismatches=" +
                mismatchCount
            );
        }

        private static string DescribeServicePointDifference(
            GeoServicePoint legacy,
            GeoServicePoint direct
        )
        {
            if (legacy == null || direct == null)
            {
                return legacy == direct
                    ? null
                    : "null point mismatch";
            }

            if (legacy.Id != direct.Id)
                return "Id legacy=" + legacy.Id + " direct=" + direct.Id;

            if (!string.Equals(
                legacy.OsmType,
                direct.OsmType,
                StringComparison.Ordinal))
            {
                return "OsmType legacy='" +
                    legacy.OsmType +
                    "' direct='" +
                    direct.OsmType +
                    "'";
            }

            if (legacy.Point == null || direct.Point == null)
            {
                if (legacy.Point != direct.Point)
                    return "Point null mismatch";
            }
            else if (!legacy.Point.Lon.Equals(direct.Point.Lon) ||
                     !legacy.Point.Lat.Equals(direct.Point.Lat))
            {
                return
                    "coordinate legacy=(" +
                    legacy.Point.Lon.ToString(
                        "R",
                        CultureInfo.InvariantCulture) +
                    "," +
                    legacy.Point.Lat.ToString(
                        "R",
                        CultureInfo.InvariantCulture) +
                    ") direct=(" +
                    direct.Point.Lon.ToString(
                        "R",
                        CultureInfo.InvariantCulture) +
                    "," +
                    direct.Point.Lat.ToString(
                        "R",
                        CultureInfo.InvariantCulture) +
                    ")";
            }

            if (!string.Equals(legacy.Name, direct.Name, StringComparison.Ordinal))
                return "Name legacy='" + legacy.Name + "' direct='" + direct.Name + "'";

            if (!string.Equals(legacy.FamilyKey, direct.FamilyKey, StringComparison.Ordinal))
                return "FamilyKey legacy='" + legacy.FamilyKey + "' direct='" + direct.FamilyKey + "'";

            if (!string.Equals(legacy.FamilyLabel, direct.FamilyLabel, StringComparison.Ordinal))
                return "FamilyLabel legacy='" + legacy.FamilyLabel + "' direct='" + direct.FamilyLabel + "'";

            if (!string.Equals(legacy.SubcategoryKey, direct.SubcategoryKey, StringComparison.Ordinal))
                return "SubcategoryKey legacy='" + legacy.SubcategoryKey + "' direct='" + direct.SubcategoryKey + "'";

            if (!string.Equals(legacy.SubcategoryLabel, direct.SubcategoryLabel, StringComparison.Ordinal))
                return "SubcategoryLabel legacy='" + legacy.SubcategoryLabel + "' direct='" + direct.SubcategoryLabel + "'";

            if (!string.Equals(legacy.SourceTag, direct.SourceTag, StringComparison.Ordinal))
                return "SourceTag legacy='" + legacy.SourceTag + "' direct='" + direct.SourceTag + "'";

            var tagsDifference =
                DescribeServiceTagsDifference(
                    legacy.Tags,
                    direct.Tags
                );

            if (tagsDifference != null)
                return tagsDifference;

            if (!string.Equals(
                legacy.StableId,
                direct.StableId,
                StringComparison.Ordinal))
            {
                return "StableId legacy='" +
                    legacy.StableId +
                    "' direct='" +
                    direct.StableId +
                    "'";
            }

            return null;
        }

        private static string DescribeServiceTagsDifference(
            Dictionary<string, string> legacy,
            Dictionary<string, string> direct
        )
        {
            if (legacy == null || direct == null)
            {
                return legacy == direct
                    ? null
                    : "Tags null mismatch";
            }

            if (legacy.Count != direct.Count)
            {
                return
                    "Tags.Count legacy=" +
                    legacy.Count +
                    " direct=" +
                    direct.Count;
            }

            var directExactKeys =
                new HashSet<string>(
                    direct.Keys,
                    StringComparer.Ordinal
                );

            foreach (var pair in legacy)
            {
                if (!directExactKeys.Contains(pair.Key))
                {
                    return
                        "tag key missing/case mismatch: '" +
                        pair.Key +
                        "'";
                }

                string directValue;

                if (!direct.TryGetValue(
                    pair.Key,
                    out directValue))
                {
                    return "tag missing: '" + pair.Key + "'";
                }

                if (!string.Equals(
                    pair.Value,
                    directValue,
                    StringComparison.Ordinal))
                {
                    return
                        "tag '" +
                        pair.Key +
                        "' legacy='" +
                        pair.Value +
                        "' direct='" +
                        directValue +
                        "'";
                }
            }

            return null;
        }

        // ----------------------------- Legacy helpers (kept for LoadFamilyCoreLegacy) -----------------------------

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
