using System.Collections.Generic;

namespace CityTimelineMod.Rendering.Railways
{
    internal sealed class RailwaySemanticStats
    {
        internal int Train;
        internal int Tram;
        internal int LightRail;
        internal int Subway;
        internal int Service;
        internal int Tunnels;
        internal int Total;

        internal static RailwaySemanticStats FromLines(IList<GeoRailwayLine> lines)
        {
            var stats = new RailwaySemanticStats();
            if (lines == null)
                return stats;

            for (var i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                if (line == null)
                    continue;

                switch (line.Category)
                {
                    case RailwayCategory.Train: stats.Train++; break;
                    case RailwayCategory.Tram: stats.Tram++; break;
                    case RailwayCategory.LightRail: stats.LightRail++; break;
                    case RailwayCategory.Subway: stats.Subway++; break;
                    case RailwayCategory.Service: stats.Service++; break;
                }

                if (line.Tunnel)
                    stats.Tunnels++;

                stats.Total++;
            }

            return stats;
        }
    }
}
