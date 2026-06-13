namespace CityTimelineMod.LargeMap
{
    internal static class CityTimelineLargeMapState
    {
        internal const int OriginalMapSizeMeters = 14336;
        internal const int CoreValue = 4;
        internal const int MapSizeMeters = OriginalMapSizeMeters * CoreValue;

        internal const float OriginalMapSizeMetersFloat = 14336f;
        internal const float MapSizeMetersFloat = 57344f;

        internal static bool Enabled = false;
    }
}
