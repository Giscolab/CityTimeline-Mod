using System.Collections.Generic;
using CityTimelineMod.Importers;

namespace CityTimelineMod.Geometry
{
    internal static class GeoBoundsCalculator
    {
        internal static GeoBounds CalculateBounds(List<List<GeoPoint>> lines)
        {
            var bounds = new GeoBounds();
            var initialized = false;

            if (lines != null)
            {
                foreach (var line in lines)
                {
                    if (line == null)
                        continue;

                    foreach (var point in line)
                    {
                        if (!initialized)
                        {
                            bounds.MinLon = point.Lon;
                            bounds.MaxLon = point.Lon;
                            bounds.MinLat = point.Lat;
                            bounds.MaxLat = point.Lat;
                            initialized = true;
                        }
                        else
                        {
                            if (point.Lon < bounds.MinLon) bounds.MinLon = point.Lon;
                            if (point.Lon > bounds.MaxLon) bounds.MaxLon = point.Lon;
                            if (point.Lat < bounds.MinLat) bounds.MinLat = point.Lat;
                            if (point.Lat > bounds.MaxLat) bounds.MaxLat = point.Lat;
                        }
                    }
                }
            }

            if (!initialized)
            {
                bounds.MinLon = 0.0;
                bounds.MaxLon = 0.0;
                bounds.MinLat = 0.0;
                bounds.MaxLat = 0.0;
                bounds.CenterLon = 0.0;
                bounds.CenterLat = 0.0;
                return bounds;
            }

            bounds.CenterLon = (bounds.MinLon + bounds.MaxLon) / 2.0;
            bounds.CenterLat = (bounds.MinLat + bounds.MaxLat) / 2.0;

            return bounds;
        }
    }
}
