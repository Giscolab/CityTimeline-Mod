namespace CityTimelineMod.LargeMap
{
    internal static class CityTimelineLargeMapState
    {
        internal const int OriginalMapSizeMeters = 14336;
        internal const int CoreValue = 4;
        internal const int MapSizeMeters = OriginalMapSizeMeters * CoreValue;

        internal const float OriginalMapSizeMetersFloat = 14336f;
        internal const float MapSizeMetersFloat = 57344f;
        internal const float HalfMapSizeMetersFloat = MapSizeMetersFloat * 0.5f;

        // Solution A : une seule heightmap principale de 57,344 km.
        internal static bool Enabled = true;
    }
}
