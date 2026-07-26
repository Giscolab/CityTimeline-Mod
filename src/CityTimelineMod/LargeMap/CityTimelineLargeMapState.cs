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

        // Lot 1 keeps this experimental module hard-disabled. No runtime path
        // may call Enable until the complete LargeMap contract is safe again.
        internal static bool Enabled { get; private set; } = false;

        internal static void Enable()
        {
            Enabled = true;
        }

        internal static void Disable()
        {
            Enabled = false;
        }
    }
}
