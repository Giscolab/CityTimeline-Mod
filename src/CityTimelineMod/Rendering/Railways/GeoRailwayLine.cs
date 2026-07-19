using System.Collections.Generic;
using CityTimelineMod.Importers;

namespace CityTimelineMod.Rendering.Railways
{
    internal enum RailwayCategory
    {
        Train,
        Tram,
        LightRail,
        Subway,
        Service
    }

    /// <summary>
    /// Purely visual railway geometry loaded from railways.geojson.
    /// </summary>
    internal sealed class GeoRailwayLine
    {
        internal readonly List<GeoPoint> Points;
        internal readonly RailwayCategory Category;
        internal readonly string Railway;
        internal readonly string Usage;
        internal readonly string Service;
        internal readonly string Tracks;
        internal readonly string Gauge;
        internal readonly bool Bridge;
        internal readonly bool Tunnel;
        internal readonly string Electrified;
        internal readonly string Name;

        internal GeoRailwayLine(
            List<GeoPoint> points,
            RailwayCategory category,
            string railway,
            string usage,
            string service,
            string tracks,
            string gauge,
            bool bridge,
            bool tunnel,
            string electrified,
            string name
        )
        {
            Points = points ?? new List<GeoPoint>();
            Category = category;
            Railway = railway;
            Usage = usage;
            Service = service;
            Tracks = tracks;
            Gauge = gauge;
            Bridge = bridge;
            Tunnel = tunnel;
            Electrified = electrified;
            Name = name;
        }
    }
}
