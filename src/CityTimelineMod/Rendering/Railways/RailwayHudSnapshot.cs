namespace CityTimelineMod.Rendering.Railways
{
    internal sealed class RailwayHudSnapshot
    {
        internal bool Available { get; set; }
        internal bool Visible { get; set; }
        internal float Opacity { get; set; }
        internal float Thickness { get; set; }
        internal bool TrainVisible { get; set; }
        internal bool TramVisible { get; set; }
        internal bool LightRailVisible { get; set; }
        internal bool SubwayVisible { get; set; }
        internal bool ServiceVisible { get; set; }
        internal bool TunnelsVisible { get; set; }
        internal int TrainCount { get; set; }
        internal int TramCount { get; set; }
        internal int LightRailCount { get; set; }
        internal int SubwayCount { get; set; }
        internal int ServiceCount { get; set; }
        internal int TunnelCount { get; set; }
        internal int TotalCount { get; set; }
        internal string Status { get; set; }

        internal static RailwayHudSnapshot Unavailable(string status)
        {
            return new RailwayHudSnapshot
            {
                Available = false,
                Visible = true,
                Opacity = 0.9f,
                Thickness = 3f,
                TrainVisible = true,
                TramVisible = true,
                LightRailVisible = true,
                SubwayVisible = true,
                ServiceVisible = true,
                TunnelsVisible = true,
                Status = status ?? "Aucune donnée ferroviaire disponible dans ce bundle."
            };
        }
    }
}
