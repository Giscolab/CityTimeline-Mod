using System;
using System.Collections.Generic;
using System.IO;

namespace CityTimelineMod.Bundles
{
    /// <summary>
    /// Canonical visual/informational bundle contract shared by selection and
    /// HUD diagnostics. Aggregate and split files are both audited so a bundle
    /// can be moved, inspected and re-filtered without regenerating it.
    /// </summary>
    internal static class GeoJsonBundleContract
    {
        internal static readonly string[] CompleteRelativeFiles =
        {
            "geojson/all_features.geojson",
            "geojson/commercial.geojson",
            "geojson/industrial.geojson",
            "geojson/mixed.geojson",
            "geojson/office.geojson",
            "geojson/parking.geojson",
            "geojson/paths.geojson",
            "geojson/railways.geojson",
            "geojson/residential.geojson",
            "geojson/retail.geojson",
            "geojson/roads.geojson",
            "geojson/roads_driveable_clipped.geojson",
            "geojson/roads_gravel_road.geojson",
            "geojson/roads_highway.geojson",
            "geojson/roads_large_road.geojson",
            "geojson/roads_major_clipped.geojson",
            "geojson/roads_medium_road.geojson",
            "geojson/roads_ramp.geojson",
            "geojson/roads_small_road.geojson",
            "geojson/water_areas_clipped.geojson",
            "geojson/water_lines_clipped.geojson",
            "geojson/zoning_polygons.geojson",
            "geojson/services/communications.geojson",
            "geojson/services/education.geojson",
            "geojson/services/electricity.geojson",
            "geojson/services/fire.geojson",
            "geojson/services/medical.geojson",
            "geojson/services/parks.geojson",
            "geojson/services/transport.geojson",
            "geojson/services/waste.geojson",
            "geojson/services/water.geojson",
            "reports/extraction_report.json",
            "reports/layer_index.json",
            "reports/roads_index.json",
            "reports/services_index.json"
        };

        internal static List<string> FindMissingFiles(string geoJsonPackRoot, out int presentCount)
        {
            presentCount = 0;
            var missing = new List<string>();
            for (var i = 0; i < CompleteRelativeFiles.Length; i++)
            {
                var relative = CompleteRelativeFiles[i];
                var path = Path.Combine(
                    geoJsonPackRoot ?? "",
                    relative.Replace('/', Path.DirectorySeparatorChar)
                );
                if (IsReadable(path))
                    presentCount++;
                else
                    missing.Add(relative);
            }

            return missing;
        }

        private static bool IsReadable(string path)
        {
            try
            {
                return !string.IsNullOrWhiteSpace(path) &&
                       File.Exists(path) &&
                       new FileInfo(path).Length > 0L;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
