using System;
using System.Threading;
using Unity.Entities;

namespace CityTimelineMod.LargeMap
{
    internal static class CityTimelineLargeMapState
    {
        internal const int OriginalMapSizeMeters = 14336;
        internal const int CoreValue = 4;
        internal const int MapSizeMeters = OriginalMapSizeMeters * CoreValue;

        internal const float OriginalWaterCellSizeMeters = 7f;
        internal const float WaterCellSizeMeters =
            OriginalWaterCellSizeMeters * CoreValue;

        internal const float OriginalMapSizeMetersFloat = 14336f;
        internal const float MapSizeMetersFloat = 57344f;
        internal const float HalfMapSizeMetersFloat = MapSizeMetersFloat * 0.5f;

        private static readonly object RuntimeScopeLock = new object();

        private static volatile bool _enabled;
        private static volatile bool _runtimeFaulted;
        private static volatile bool _quarantinePreservesExtendedLayout;
        private static volatile bool _cellMapSimulationExtensionEnabled;
        private static volatile bool _waterSimulationExtensionEnabled;
        private static volatile RuntimeWorldScope _activeRuntimeScope;
        private static RuntimeWorldScope _dirtyRuntimeScope;
        private static int _cellMapExtendedPathObserved;
        private static int _waterExtendedPathObserved;
        private static string _runtimeFailureReason;

        internal static bool Enabled => _enabled;
        internal static bool RuntimeFaulted => _runtimeFaulted;
        internal static string RuntimeFailureReason => _runtimeFailureReason;
        internal static bool RequiresWorldDestructionForCleanup =>
            Volatile.Read(ref _dirtyRuntimeScope) != null;
        internal static bool PreserveExtendedRuntimeLayout =>
            _enabled ||
            _quarantinePreservesExtendedLayout ||
            RequiresWorldDestructionForCleanup;

        // Transpilers call these accessors instead of embedding 57 km constants.
        // This keeps patched paths vanilla until the complete Harmony manifest
        // has been verified. During quarantine, layout accessors remain extended
        // while any installed runtime mutation or dirty simulation storage can
        // still depend on the 57 km geometry.
        internal static int GetEffectiveMapSizeMeters()
        {
            return PreserveExtendedRuntimeLayout
                ? MapSizeMeters
                : OriginalMapSizeMeters;
        }

        internal static int GetEffectiveCellMapSizeMeters()
        {
            if (!_cellMapSimulationExtensionEnabled)
                return OriginalMapSizeMeters;

            if (_enabled && TryRecordSimulationObservation(true))
                return MapSizeMeters;

            // Once a managed CellMap path has consumed the 57 km layout, its
            // initialized storage has no reversible game API. Keep the owner
            // accessor coherent while the dirty World lives, even though the
            // public mutation gate is closed. The scope is cleared only after
            // World.IsCreated becomes false.
            return PreserveExtendedRuntimeLayout
                ? MapSizeMeters
                : OriginalMapSizeMeters;
        }

        internal static int GetEffectiveWaterMapSizeMeters()
        {
            if (!_waterSimulationExtensionEnabled)
                return OriginalMapSizeMeters;

            if (_enabled && TryRecordSimulationObservation(false))
                return MapSizeMeters;

            return PreserveExtendedRuntimeLayout
                ? MapSizeMeters
                : OriginalMapSizeMeters;
        }

        internal static float GetEffectiveWaterCellSizeMeters()
        {
            if (!_waterSimulationExtensionEnabled)
                return OriginalWaterCellSizeMeters;

            if (_enabled && TryRecordSimulationObservation(false))
                return WaterCellSizeMeters;

            return PreserveExtendedRuntimeLayout
                ? WaterCellSizeMeters
                : OriginalWaterCellSizeMeters;
        }

        internal static float GetEffectiveMapSizeMetersFloat()
        {
            return PreserveExtendedRuntimeLayout
                ? MapSizeMetersFloat
                : OriginalMapSizeMetersFloat;
        }

        internal static void SetCellMapSimulationExtensionEnabled(bool enabled)
        {
            LargeMapBurstSafety.EnsureExtensionRequestAllowed("CellMap", enabled);
            _cellMapSimulationExtensionEnabled = enabled;
        }

        internal static void SetWaterSimulationExtensionEnabled(bool enabled)
        {
            LargeMapBurstSafety.EnsureExtensionRequestAllowed("Water", enabled);
            _waterSimulationExtensionEnabled = enabled;
        }

        internal static bool TryBeginRuntimeWorld(
            World world,
            long lifecycleGeneration,
            out string reason)
        {
            reason = null;
            _enabled = false;

            if (world == null || !world.IsCreated)
            {
                reason = "LargeMap requires a created runtime World.";
                return false;
            }

            if (lifecycleGeneration <= 0)
            {
                reason = "LargeMap requires a positive lifecycle generation.";
                return false;
            }

            lock (RuntimeScopeLock)
            {
                ClearDestroyedRuntimeScopeLocked();

                var dirtyScope = Volatile.Read(ref _dirtyRuntimeScope);
                if (dirtyScope != null)
                {
                    reason =
                        "LargeMap simulation state is still dirty in " +
                        dirtyScope.Describe() +
                        "; that World must be destroyed before another install.";
                    return false;
                }

                var currentScope = _activeRuntimeScope;
                if (currentScope != null &&
                    currentScope.Matches(world, lifecycleGeneration))
                {
                    return true;
                }

                _activeRuntimeScope = new RuntimeWorldScope(
                    world,
                    world.SequenceNumber,
                    lifecycleGeneration
                );
                return true;
            }
        }

        internal static bool TryEndRuntimeWorld(
            World world,
            long lifecycleGeneration,
            out string reason)
        {
            reason = null;
            _enabled = false;

            lock (RuntimeScopeLock)
            {
                ClearDestroyedRuntimeScopeLocked();

                var activeScope = _activeRuntimeScope;
                var dirtyScope = Volatile.Read(ref _dirtyRuntimeScope);

                if (activeScope != null &&
                    !activeScope.Matches(world, lifecycleGeneration))
                {
                    reason =
                        "LargeMap runtime scope mismatch. active=" +
                        activeScope.Describe() +
                        ", requestedGeneration=" + lifecycleGeneration + ".";
                    return false;
                }

                if (dirtyScope != null)
                {
                    if (!dirtyScope.Matches(world, lifecycleGeneration))
                    {
                        reason =
                            "LargeMap dirty simulation scope does not match the " +
                            "lifecycle being ended. dirty=" + dirtyScope.Describe() + ".";
                        return false;
                    }

                    if (!dirtyScope.IsDestroyed)
                    {
                        reason =
                            "LargeMap simulation state remains dirty in live " +
                            dirtyScope.Describe() + ".";
                        return false;
                    }

                    ClearDirtyRuntimeScopeLocked(dirtyScope);
                }

                if (ReferenceEquals(_activeRuntimeScope, activeScope))
                    _activeRuntimeScope = null;

                return true;
            }
        }

        internal static bool TryReleaseDestroyedRuntimeWorld(out string reason)
        {
            reason = null;
            _enabled = false;

            lock (RuntimeScopeLock)
            {
                ClearDestroyedRuntimeScopeLocked();

                var dirtyScope = Volatile.Read(ref _dirtyRuntimeScope);
                if (dirtyScope == null)
                    return true;

                reason =
                    "LargeMap simulation state remains dirty in live " +
                    dirtyScope.Describe() + ".";
                return false;
            }
        }

        internal static void Enable()
        {
            if (_runtimeFaulted)
            {
                throw new InvalidOperationException(
                    "LargeMap cannot be enabled while a runtime fault is pending: " +
                    (_runtimeFailureReason ?? "unknown failure")
                );
            }

            var scope = _activeRuntimeScope;
            if (scope == null || scope.IsDestroyed)
            {
                throw new InvalidOperationException(
                    "LargeMap cannot be enabled without a live runtime World scope."
                );
            }

            _enabled = true;
        }

        internal static void Disable()
        {
            // Disable all newly-authorized LargeMap mutations. CellMap/Water
            // accessors deliberately remain extended when a live dirty scope
            // exists; reverting those constants before World destruction would
            // make already-initialized simulation storage inconsistent.
            _enabled = false;
        }

        internal static void PrepareForInstall()
        {
            _enabled = false;

            lock (RuntimeScopeLock)
            {
                ClearDestroyedRuntimeScopeLocked();

                if (_activeRuntimeScope == null ||
                    _activeRuntimeScope.IsDestroyed)
                {
                    throw new InvalidOperationException(
                        "LargeMap runtime World scope was not prepared before install."
                    );
                }

                var dirtyScope = Volatile.Read(ref _dirtyRuntimeScope);
                if (dirtyScope != null)
                {
                    throw new InvalidOperationException(
                        "LargeMap cannot install over dirty simulation state in " +
                        dirtyScope.Describe() + "."
                    );
                }

                _runtimeFaulted = false;
                _runtimeFailureReason = null;
                Volatile.Write(ref _cellMapExtendedPathObserved, 0);
                Volatile.Write(ref _waterExtendedPathObserved, 0);
            }
        }

        internal static void ClearRuntimeFaultAfterVerifiedRollback()
        {
            lock (RuntimeScopeLock)
            {
                ClearDestroyedRuntimeScopeLocked();

                if (Volatile.Read(ref _dirtyRuntimeScope) != null)
                    return;

                _runtimeFaulted = false;
                _runtimeFailureReason = null;
                _quarantinePreservesExtendedLayout = false;
                Volatile.Write(ref _cellMapExtendedPathObserved, 0);
                Volatile.Write(ref _waterExtendedPathObserved, 0);
            }
        }

        internal static bool IsSimulationRollbackProvable(out string reason)
        {
            lock (RuntimeScopeLock)
            {
                ClearDestroyedRuntimeScopeLocked();

                var dirtyScope = Volatile.Read(ref _dirtyRuntimeScope);
                if (dirtyScope != null)
                {
                    reason =
                        "extended simulation state executed without a reversible " +
                        "game API in " + dirtyScope.Describe() +
                        "; cellMapObserved=" +
                        (Volatile.Read(ref _cellMapExtendedPathObserved) != 0) +
                        ", waterObserved=" +
                        (Volatile.Read(ref _waterExtendedPathObserved) != 0);
                    return false;
                }

                reason = null;
                return true;
            }
        }

        internal static bool CanRollbackSimulationWithoutMutation(
            out string reason)
        {
            var dirtyScope = Volatile.Read(ref _dirtyRuntimeScope);
            if (dirtyScope == null || dirtyScope.IsDestroyed)
            {
                reason = null;
                return true;
            }

            reason =
                "CellMap/Water storage is initialized for live " +
                dirtyScope.Describe() +
                "; World destruction is required before rollback.";
            return false;
        }

        internal static void FailRuntime(string reason)
        {
            _enabled = false;
            _runtimeFaulted = true;
            // A runtime failure can occur after Terrain/worldmap/MapTiles/Airway
            // have already committed 57 km state, before CellMap or Water has
            // marked the simulation scope dirty. Keep every owner accessor on
            // the extended layout until coordinated rollback and owner removal
            // have both been verified.
            _quarantinePreservesExtendedLayout = true;
            _runtimeFailureReason = string.IsNullOrWhiteSpace(reason)
                ? "unspecified LargeMap runtime failure"
                : reason;

            try
            {
                Mod.NotifyLargeMapRuntimeFailure(_runtimeFailureReason);
            }
            catch (Exception ex)
            {
                Util.Log.Error(
                    "[LargeMap] runtime failure notification failed: " + ex
                );
            }
        }

        internal static void MarkRollbackIncomplete(string reason)
        {
            _enabled = false;
            _runtimeFaulted = true;
            _quarantinePreservesExtendedLayout = true;
            _runtimeFailureReason = string.IsNullOrWhiteSpace(reason)
                ? "LargeMap runtime rollback is incomplete"
                : reason;
        }

        private static bool TryRecordSimulationObservation(bool cellMap)
        {
            var scope = _activeRuntimeScope;
            // This accessor can be reached from managed job paths. World
            // lifetime is validated on the main-thread begin/end/preflight
            // APIs; never dereference Unity.Entities.World from a worker here.
            if (scope == null)
            {
                FailRuntime(
                    "LargeMap simulation path executed without a bound runtime World scope"
                );
                return false;
            }

            var existingScope = Interlocked.CompareExchange(
                ref _dirtyRuntimeScope,
                scope,
                null
            );

            if (existingScope != null && !ReferenceEquals(existingScope, scope))
            {
                FailRuntime(
                    "LargeMap simulation path crossed runtime World scopes. active=" +
                    scope.Describe() + ", dirty=" + existingScope.Describe()
                );
                return false;
            }

            if (cellMap)
                Volatile.Write(ref _cellMapExtendedPathObserved, 1);
            else
                Volatile.Write(ref _waterExtendedPathObserved, 1);

            return _enabled;
        }

        private static void ClearDestroyedRuntimeScopeLocked()
        {
            var dirtyScope = Volatile.Read(ref _dirtyRuntimeScope);
            if (dirtyScope != null && dirtyScope.IsDestroyed)
                ClearDirtyRuntimeScopeLocked(dirtyScope);

            var activeScope = _activeRuntimeScope;
            if (activeScope != null && activeScope.IsDestroyed &&
                Volatile.Read(ref _dirtyRuntimeScope) == null)
            {
                _activeRuntimeScope = null;
            }
        }

        private static void ClearDirtyRuntimeScopeLocked(
            RuntimeWorldScope expectedScope)
        {
            if (!ReferenceEquals(
                    Volatile.Read(ref _dirtyRuntimeScope),
                    expectedScope))
            {
                return;
            }

            Volatile.Write(ref _cellMapExtendedPathObserved, 0);
            Volatile.Write(ref _waterExtendedPathObserved, 0);
            Volatile.Write(ref _dirtyRuntimeScope, null);

            if (ReferenceEquals(_activeRuntimeScope, expectedScope))
                _activeRuntimeScope = null;
        }

        private sealed class RuntimeWorldScope
        {
            internal readonly World World;
            internal readonly ulong WorldSequence;
            internal readonly long LifecycleGeneration;

            internal RuntimeWorldScope(
                World world,
                ulong worldSequence,
                long lifecycleGeneration)
            {
                World = world;
                WorldSequence = worldSequence;
                LifecycleGeneration = lifecycleGeneration;
            }

            internal bool IsDestroyed => World == null || !World.IsCreated;

            internal bool Matches(World world, long lifecycleGeneration)
            {
                return ReferenceEquals(World, world) &&
                    WorldSequence == (world == null ? 0 : world.SequenceNumber) &&
                    LifecycleGeneration == lifecycleGeneration;
            }

            internal string Describe()
            {
                return "World(sequence=" + WorldSequence +
                    ", generation=" + LifecycleGeneration +
                    ", created=" + !IsDestroyed + ")";
            }
        }
    }
}
