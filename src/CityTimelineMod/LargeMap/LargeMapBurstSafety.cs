using System;

namespace CityTimelineMod.LargeMap
{
    /// <summary>
    /// Defines the hard boundary between managed Harmony patches and AOT Burst jobs.
    ///
    /// CellMap and Water are deliberately treated as two independent safety
    /// contracts.
    ///
    /// CellMap:
    /// The supported Game.dll contains nine Burst-compiled CellMap clients.
    /// Extending CellMap storage while those native jobs still use the vanilla
    /// allocation/indexing contract can produce invalid native memory access.
    ///
    /// Water:
    /// The supported Game.dll contains three Burst-compiled Water clients that
    /// read WaterSystem.kMapSize. They are spatial Water consumers, but they do
    /// not own the managed kCellSize solver paths. Water therefore has its own
    /// validation gate and must not be blocked merely because CellMap ReBurst
    /// coverage is incomplete.
    ///
    /// Neither family is enabled until CTM explicitly validates the corresponding
    /// native/Burst contract.
    /// </summary>
    internal static class LargeMapBurstSafety
    {
        internal const int ExpectedCellMapBurstTargets = 9;
        internal const int ExpectedWaterBurstTargets = 3;

        /*
         * Independent readiness gates.
         *
         * Keep each false until CTM owns and validates the corresponding
         * native/Burst contract for the supported Game.dll.
         */
        internal static bool CellMapReBurst57Ready => false;
        internal static bool WaterReBurst57Ready => false;
		
		internal static bool WaterSimulation57Safe => true;

        /*
         * Compatibility gate for callers that have not yet been migrated to
         * the family-specific contract.
         *
         * Do not use this property in new CellMap or Water code.
         */
        internal static bool ReBurst57Ready =>
            CellMapReBurst57Ready &&
            WaterReBurst57Ready;

        internal static void EnsureExtensionRequestAllowed(
            string family,
            bool enabled)
        {
            if (!enabled)
                return;

            bool ready;

            if (string.Equals(
                    family,
                    "CellMap",
                    StringComparison.Ordinal))
            {
                ready = CellMapReBurst57Ready;
            }
            else if (string.Equals(
                         family,
                         "Water",
                         StringComparison.Ordinal))
            {
                ready = WaterSimulation57Safe;
            }
            else
            {
                throw new ArgumentOutOfRangeException(
                    nameof(family),
                    family,
                    "Unknown LargeMap simulation extension family."
                );
            }

            if (ready)
                return;

            throw new InvalidOperationException(
                "[LargeMap] " + family +
                " simulation extension activation refused: " +
                family + " ReBurst57Ready=false."
            );
        }

        internal static bool CanExtendCellMapSimulation(
            int cellMapBurstTargets,
            out string reason)
        {
            if (cellMapBurstTargets != ExpectedCellMapBurstTargets)
            {
                reason =
                    "CellMap Burst target contract changed: expected=" +
                    ExpectedCellMapBurstTargets +
                    ", actual=" +
                    cellMapBurstTargets + ".";
                return false;
            }

            if (!CellMapReBurst57Ready)
            {
                reason =
                    "validated 57 km CellMap ReBurst replacements are not installed; " +
                    "CellMap storage remains on the vanilla simulation contract " +
                    "to prevent native Burst memory corruption";
                return false;
            }

            reason = null;
            return true;
        }

        internal static bool CanExtendWaterSimulation(
            int waterBurstTargets,
            out string reason)
        {
            if (waterBurstTargets != ExpectedWaterBurstTargets)
            {
                reason =
                    "Water Burst target contract changed: expected=" +
                    ExpectedWaterBurstTargets +
                    ", actual=" +
                    waterBurstTargets + ".";
                return false;
            }

if (!WaterSimulation57Safe)
{
    reason =
        "57 km Water simulation safety contract is not validated";
    return false;
}

            reason = null;
            return true;
        }

        /*
         * Legacy combined API.
         *
         * Retained temporarily so replacing this file alone does not break
         * existing callers. CellMap and Water callers must migrate to the
         * family-specific methods above.
         */
        internal static bool CanExtendSimulationMaps(
            int cellMapBurstTargets,
            int waterBurstTargets,
            out string reason)
        {
            if (cellMapBurstTargets != ExpectedCellMapBurstTargets)
            {
                reason =
                    "CellMap Burst target contract changed: expected=" +
                    ExpectedCellMapBurstTargets +
                    ", actual=" +
                    cellMapBurstTargets + ".";
                return false;
            }

            if (waterBurstTargets != ExpectedWaterBurstTargets)
            {
                reason =
                    "Water Burst target contract changed: expected=" +
                    ExpectedWaterBurstTargets +
                    ", actual=" +
                    waterBurstTargets + ".";
                return false;
            }

            if (!ReBurst57Ready)
            {
                reason =
                    "legacy combined CellMap/Water ReBurst gate is closed; " +
                    "the caller must use the appropriate family-specific " +
                    "simulation safety contract";
                return false;
            }

            reason = null;
            return true;
        }
    }
}