// CTM CONTRACT GUARD: Never disable the 57 km LargeMap + PlayableWorld contract unless explicitly requested.
using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Areas;
using Game.Simulation;
using HarmonyLib;
using Unity.Mathematics;
using UnityEngine;

namespace CityTimelineMod.LargeMap
{
    internal static class CityTimelineLargeMapPatcher
    {
        private const string HarmonyId = "CityTimelineMod.LargeMap";
        private static readonly Guid SupportedGameModuleVersionId =
            new Guid("36b776ac-5007-4e06-81b3-242b2abfd5b6");

        private static Harmony _harmony;
        private static bool _installed;
        private static bool _coordinatedRuntimeRollbackComplete;

        internal static bool Install(bool authorized)
        {
            if (!authorized || !Mod.RuntimeEnabled)
            {
                var cleanupComplete = Uninstall();
                Util.Log.Info(
                    "[LargeMap] install skipped: authorization or runtime gate is false; " +
                    "cleanupComplete=" + cleanupComplete + "."
                );
                return false;
            }

            Dictionary<MethodBase, HashSet<MethodBase>> expectedManifest;

            try
            {
                expectedManifest = ResolveExpectedManifest();
            }
            catch (Exception ex)
            {
                return FailInstall(0, "target manifest resolution", ex);
            }

            if (_installed && !CityTimelineLargeMapState.RuntimeFaulted)
            {
                if (TryVerifyExactInstallation(
                        expectedManifest,
                        out var installedTargetCount,
                        out var installedPatchCount))
                {
                    CityTimelineLargeMapState.Enable();
                    Util.Log.Info(
                        "[LargeMap] existing Harmony owner manifest verified. ownedTargets=" +
                        installedTargetCount + ", ownedPatches=" + installedPatchCount +
                        ". Native Burst execution remains a separate runtime validation."
                    );
                    return true;
                }

                Util.Log.Error(
                    "[LargeMap] installed state failed exact verification; reinstalling fail-closed."
                );
            }

            if (!TryStandaloneCleanup(
                    "pre-install cleanup",
                    out var preInstallCleanupReason))
            {
                Util.Log.Error(
                    "[LargeMap] install blocked: " + preInstallCleanupReason
                );
                return false;
            }

            try
            {
                ResetPatchState();
                CityTimelineLargeMapState.PrepareForInstall();
            }
            catch (Exception ex)
            {
                return FailInstall(
                    0,
                    "runtime World scope preparation",
                    ex
                );
            }

            try
            {
                expectedManifest = ResolveExpectedManifest();
            }
            catch (Exception ex)
            {
                return FailInstall(0, "target manifest refresh", ex);
            }

            var installedFamilies = 0;

            try
            {
                _harmony = new Harmony(HarmonyId);

                PatchClass(typeof(LargeMapTerrainSystemPatches));
                installedFamilies++;

                LargeMapCellMapSystemPatches.Apply(_harmony);
                installedFamilies++;

                LargeMapUnifiedWorldPatches.Apply(_harmony);
                installedFamilies++;

                if (!TryVerifyExactInstallation(
                        expectedManifest,
                        out var ownedTargetCount,
                        out var ownedPatchCount))
                {
                    throw new InvalidOperationException(
                        "Exact LargeMap Harmony manifest verification failed."
                    );
                }

                _installed = true;
                CityTimelineLargeMapState.Enable();

                Util.Log.Info(
                    "[LargeMap] Harmony owner manifest installed atomically. families=" +
                    installedFamilies + ", ownedTargets=" + ownedTargetCount +
                    ", ownedPatches=" + ownedPatchCount +
                    ". Native Burst execution is not proven by Harmony metadata."
                );

                return true;
            }
            catch (Exception ex)
            {
                return FailInstall(installedFamilies, "patch installation", ex);
            }
        }

        internal static bool Uninstall()
        {
            if (TryStandaloneCleanup("uninstall", out var reason))
            {
                Util.Log.Info("[LargeMap] patches and runtime mutations removed.");
                return true;
            }

            Util.Log.Error(
                "[LargeMap] uninstall quarantined; Harmony owner and local " +
                "rollback state were retained. " + reason
            );
            return false;
        }

        internal static bool CanUninstallWithoutMutation(out string reason)
        {
            if (!CityTimelineLargeMapState
                    .CanRollbackSimulationWithoutMutation(out reason))
            {
                reason = "simulation: " + reason;
                return false;
            }

            if (!LargeMapUnifiedWorldPatches
                    .CanRollbackRuntimeMutationsWithoutMutation(out reason))
            {
                reason = "unified world: " + reason;
                return false;
            }

            if (!LargeMapTerrainSystemPatches
                    .CanRollbackRuntimeMutationsWithoutMutation(out reason))
            {
                reason = "terrain: " + reason;
                return false;
            }

            if (!TryGetOwnedPatchedMethods(out _))
            {
                reason = "Harmony owner metadata cannot be inspected.";
                return false;
            }

            reason = null;
            return true;
        }

        internal static bool EnterRuntimeQuarantine(string reason)
        {
            _coordinatedRuntimeRollbackComplete = false;
            CityTimelineLargeMapState.MarkRollbackIncomplete(
                string.IsNullOrWhiteSpace(reason)
                    ? "LargeMap runtime cleanup is quarantined"
                    : reason
            );

            Util.Log.Error(
                "[LargeMap] runtime quarantined; mutation gate closed, shared " +
                "Harmony owner and rollback scope retained. " +
                (CityTimelineLargeMapState.RequiresWorldDestructionForCleanup
                    ? "A World reload/destruction is required before retry."
                    : "A later coordinated cleanup retry is required.")
            );
            return
                !CityTimelineLargeMapState.Enabled &&
                CityTimelineLargeMapState.RuntimeFaulted;
        }

        internal static bool TryRollbackForCoordinatedUninstall(
            out string reason)
        {
            _coordinatedRuntimeRollbackComplete = false;

            if (!CanUninstallWithoutMutation(out reason))
                return false;

            CityTimelineLargeMapState.Disable();
            if (!RollbackRuntimeMutations("coordinated uninstall"))
            {
                reason =
                    "Runtime rollback failed after a successful preflight; " +
                    "the shared Harmony owner was retained.";
                CityTimelineLargeMapState.MarkRollbackIncomplete(reason);
                return false;
            }

            _coordinatedRuntimeRollbackComplete = true;
            reason = null;
            return true;
        }

        internal static bool TryRemoveSharedHarmonyOwner(out string reason)
        {
            if (!_coordinatedRuntimeRollbackComplete)
            {
                reason =
                    "This assembly has not completed coordinated runtime rollback.";
                return false;
            }

            if (!RemoveSharedHarmonyOwner("coordinated uninstall"))
            {
                reason = "Shared Harmony owner removal failed verification.";
                CityTimelineLargeMapState.MarkRollbackIncomplete(reason);
                return false;
            }

            reason = null;
            return true;
        }

        internal static bool CommitCoordinatedUnpatch(out string reason)
        {
            if (!_coordinatedRuntimeRollbackComplete)
            {
                reason =
                    "This assembly has no completed coordinated runtime rollback.";
                return false;
            }

            if (!TryGetOwnedPatchedMethods(out var remaining) ||
                remaining.Count != 0)
            {
                reason =
                    "Shared Harmony owner is still installed; local state was retained.";
                return false;
            }

            try
            {
                ResetPatchState();
                _installed = false;
                _harmony = null;
                _coordinatedRuntimeRollbackComplete = false;
                CityTimelineLargeMapState.ClearRuntimeFaultAfterVerifiedRollback();
                reason = null;
                return true;
            }
            catch (Exception ex)
            {
                reason = "Local coordinated-unpatch commit failed: " + ex;
                CityTimelineLargeMapState.MarkRollbackIncomplete(reason);
                return false;
            }
        }

        private static bool FailInstall(
            int installedFamilies,
            string stage,
            Exception exception)
        {
            var cleanupComplete = TryStandaloneCleanup(
                "install rollback",
                out var cleanupReason
            );

            Util.Log.Error(
                "[LargeMap] install failed during " + stage +
                " after families=" + installedFamilies +
                "; cleanupComplete=" + cleanupComplete +
                ", cleanupReason=" + cleanupReason + ". " + exception
            );

            return false;
        }

        private static void PatchClass(Type patchType)
        {
            if (patchType == null)
                throw new ArgumentNullException(nameof(patchType));

            var patchedMethods = _harmony.CreateClassProcessor(patchType).Patch();
            if (patchedMethods == null || patchedMethods.Count == 0)
            {
                throw new InvalidOperationException(
                    "Harmony produced no patch for " + patchType.Name + "."
                );
            }

            Util.Log.Info("[LargeMap] patch installed: " + patchType.Name);
        }

        private static Dictionary<MethodBase, HashSet<MethodBase>>
            ResolveExpectedManifest()
        {
            VerifySupportedGameModuleIdentity();

            var manifest = new Dictionary<MethodBase, HashSet<MethodBase>>(
                MethodBaseComparer.Instance
            );

            AddExpectedPatch(
                manifest,
                AccessTools.Method(
                    typeof(TerrainSystem),
                    "FinalizeTerrainData",
                    new[]
                    {
                        typeof(Texture2D),
                        typeof(Texture2D),
                        typeof(float2),
                        typeof(float2),
                        typeof(float2),
                        typeof(float2),
                        typeof(float2),
                        typeof(float2)
                    }
                ),
                AccessTools.Method(
                    typeof(LargeMapTerrainSystemPatches),
                    "FinalizeTerrainData_Prefix"
                )
            );
            AddExpectedPatch(
                manifest,
                AccessTools.Method(
                    typeof(TerrainSystem),
                    "InitializeTerrainData",
                    new[]
                    {
                        typeof(Texture2D),
                        typeof(Texture2D),
                        typeof(float2),
                        typeof(float2),
                        typeof(float2),
                        typeof(float2),
                        typeof(float2),
                        typeof(float2)
                    }
                ),
                AccessTools.Method(
                    typeof(LargeMapTerrainSystemPatches),
                    "InitializeTerrainData_Postfix"
                )
            );
            AddExpectedPatch(
                manifest,
                AccessTools.Method(
                    typeof(TerrainSystem),
                    nameof(TerrainSystem.GetTerrainBounds),
                    Type.EmptyTypes
                ),
                AccessTools.Method(
                    typeof(LargeMapTerrainSystemPatches),
                    "GetTerrainBounds_Transpiler"
                )
            );
            AddExpectedPatch(
                manifest,
                AccessTools.Method(
                    typeof(TerrainSystem),
                    nameof(TerrainSystem.GetHeightData),
                    new[] { typeof(bool) }
                ),
                AccessTools.Method(
                    typeof(LargeMapTerrainSystemPatches),
                    "GetHeightData_Transpiler"
                )
            );
            AddExpectedPatch(
                manifest,
                AccessTools.Method(
                    typeof(TerrainSystem),
                    nameof(TerrainSystem.ReplaceWorldHeightmap),
                    new[] { typeof(Texture2D) }
                ),
                AccessTools.Method(typeof(UnifiedWorldmapImportPatch), "Prefix")
            );
            AddExpectedPatch(
                manifest,
                AccessTools.Method(
                    typeof(MapTileSystem),
                    "LegacyGenerateMapTiles",
                    new[] { typeof(bool) }
                ),
                AccessTools.Method(typeof(LargeMapTileScalePatch), "Postfix")
            );
            AddExpectedPatch(
                manifest,
                AccessTools.Method(
                    typeof(Game.Net.AirwaySystem),
                    "OnCreate",
                    Type.EmptyTypes
                ),
                AccessTools.Method(
                    typeof(LargeMapAirwayScalePatch),
                    "OnCreateTranspiler"
                )
            );
            AddExpectedPatch(
                manifest,
                AccessTools.Method(
                    typeof(Game.Net.AirwaySystem),
                    "OnCreate",
                    Type.EmptyTypes
                ),
                AccessTools.Method(
                    typeof(LargeMapAirwayScalePatch),
                    "OnCreatePostfix"
                )
            );
            AddExpectedPatch(
                manifest,
                AccessTools.Method(
                    typeof(Game.Net.AirwaySystem),
                    "OnUpdate",
                    Type.EmptyTypes
                ),
                AccessTools.Method(
                    typeof(LargeMapAirwayScalePatch),
                    "OnUpdatePrefix"
                )
            );
            /*
            AddExpectedPatch(
                manifest,
                AccessTools.Method(
                    typeof(Game.Tools.ToolFeedbackSystem),
                    "OnUpdate",
                    Type.EmptyTypes
                ),
                AccessTools.Method(
                    typeof(LargeMapToolFeedbackLoadingPatch),
                    "OnUpdatePrefix"
                )
            );
            */

            var cellMapTranspiler =
                LargeMapCellMapSystemPatches.GetTranspilerMethod();
            foreach (var target in LargeMapCellMapSystemPatches.PrepareManifest())
                AddExpectedPatch(manifest, target, cellMapTranspiler);

            var waterTranspiler =
                LargeMapWaterMapSizePatches.GetTranspilerMethod();
            foreach (var target in LargeMapWaterMapSizePatches.PrepareManifest())
                AddExpectedPatch(manifest, target, waterTranspiler);

            if (manifest.Count == 0)
                throw new InvalidOperationException("LargeMap target manifest is empty.");

            Util.Log.Info(
                "[LargeMap] exact target manifest resolved. targets=" +
                manifest.Count + ", patchMethods=" + CountExpectedPatches(manifest) + "."
            );

            return manifest;
        }

        internal static void VerifySupportedGameModuleIdentity()
        {
            var gameModule = typeof(Game.GameSystemBase).Assembly.ManifestModule;
            var actual = gameModule.ModuleVersionId;

            if (actual != SupportedGameModuleVersionId)
            {
                throw new InvalidOperationException(
                    "[LargeMap] unsupported Game.dll module identity. expected=" +
                    SupportedGameModuleVersionId + ", actual=" + actual +
                    ". Dynamic target cardinality is not accepted as an exact manifest."
                );
            }
        }

        private static void AddExpectedPatch(
            Dictionary<MethodBase, HashSet<MethodBase>> manifest,
            MethodBase target,
            MethodBase patchMethod)
        {
            if (target == null)
                throw new MissingMethodException("LargeMap target resolution failed.");
            if (patchMethod == null)
                throw new MissingMethodException("LargeMap patch method resolution failed.");

            if (!manifest.TryGetValue(target, out var patchMethods))
            {
                patchMethods = new HashSet<MethodBase>(MethodBaseComparer.Instance);
                manifest.Add(target, patchMethods);
            }

            if (!patchMethods.Add(patchMethod))
            {
                throw new InvalidOperationException(
                    "Duplicate LargeMap patch manifest entry: " +
                    DescribeMethod(target) + " <- " + DescribeMethod(patchMethod) + "."
                );
            }
        }

        private static bool TryVerifyExactInstallation(
            Dictionary<MethodBase, HashSet<MethodBase>> expectedManifest,
            out int ownedTargetCount,
            out int ownedPatchCount)
        {
            ownedTargetCount = 0;
            ownedPatchCount = 0;

            try
            {
                if (!TryGetOwnedPatchedMethods(out var ownedTargets))
                    return false;

                ownedTargetCount = ownedTargets.Count;
                var expectedTargets = new HashSet<MethodBase>(
                    expectedManifest.Keys,
                    MethodBaseComparer.Instance
                );

                if (!ownedTargets.SetEquals(expectedTargets))
                {
                    Util.Log.Error(
                        "[LargeMap] owned target set mismatch. expected=" +
                        expectedTargets.Count + ", actual=" + ownedTargets.Count + "."
                    );
                    return false;
                }

                foreach (var pair in expectedManifest)
                {
                    var patchInfo = Harmony.GetPatchInfo(pair.Key);
                    if (patchInfo == null)
                        return false;

                    var actualMethods = new HashSet<MethodBase>(
                        MethodBaseComparer.Instance
                    );
                    var actualRecords = 0;
                    AddOwnedPatchMethods(actualMethods, patchInfo.Prefixes, ref actualRecords);
                    AddOwnedPatchMethods(actualMethods, patchInfo.Postfixes, ref actualRecords);
                    AddOwnedPatchMethods(actualMethods, patchInfo.Transpilers, ref actualRecords);
                    AddOwnedPatchMethods(actualMethods, patchInfo.Finalizers, ref actualRecords);

                    if (actualRecords != pair.Value.Count ||
                        !actualMethods.SetEquals(pair.Value))
                    {
                        Util.Log.Error(
                            "[LargeMap] patch set mismatch for " +
                            DescribeMethod(pair.Key) + ". expected=" +
                            pair.Value.Count + ", actualMethods=" +
                            actualMethods.Count + ", actualRecords=" + actualRecords + "."
                        );
                        return false;
                    }

                    ownedPatchCount += actualRecords;
                }

                return ownedPatchCount == CountExpectedPatches(expectedManifest);
            }
            catch (Exception ex)
            {
                Util.Log.Error("[LargeMap] exact Harmony verification failed: " + ex);
                ownedTargetCount = 0;
                ownedPatchCount = 0;
                return false;
            }
        }

        private static void AddOwnedPatchMethods(
            HashSet<MethodBase> destination,
            IEnumerable<Patch> patches,
            ref int recordCount)
        {
            if (patches == null)
                return;

            foreach (var patch in patches)
            {
                if (patch == null ||
                    !string.Equals(patch.owner, HarmonyId, StringComparison.Ordinal) ||
                    patch.PatchMethod == null)
                {
                    continue;
                }

                recordCount++;
                destination.Add(patch.PatchMethod);
            }
        }

        private static int CountExpectedPatches(
            Dictionary<MethodBase, HashSet<MethodBase>> manifest)
        {
            var count = 0;
            foreach (var pair in manifest)
                count += pair.Value.Count;
            return count;
        }

        private static bool TryStandaloneCleanup(
            string operation,
            out string reason)
        {
            _coordinatedRuntimeRollbackComplete = false;

            if (!CanUninstallWithoutMutation(out reason))
            {
                EnterRuntimeQuarantine(reason);
                reason +=
                    " Harmony owner retained; no runtime rollback was attempted. " +
                    (CityTimelineLargeMapState.RequiresWorldDestructionForCleanup
                        ? "A World reload/destruction is required before retry."
                        : "A later coordinated cleanup retry is required.");
                return false;
            }

            if (!CanRemoveSharedOwnerFromCurrentAssembly(out reason))
            {
                EnterRuntimeQuarantine(reason);
                reason +=
                    " Shared owner retained so patches from another assembly " +
                    "cannot be removed accidentally.";
                return false;
            }

            if (!TryRollbackForCoordinatedUninstall(out reason))
                return false;

            if (!TryRemoveSharedHarmonyOwner(out reason))
                return false;

            if (!CommitCoordinatedUnpatch(out reason))
                return false;

            Util.Log.Info(
                "[LargeMap] " + operation +
                " completed through probe/rollback/unpatch/commit phases."
            );
            return true;
        }

        private static bool RollbackRuntimeMutations(string operation)
        {
            try
            {
                var unifiedComplete =
                    LargeMapUnifiedWorldPatches.RollbackRuntimeMutations();
                var terrainComplete =
                    LargeMapTerrainSystemPatches.TryRollbackRuntimeMutations();
                var simulationComplete =
                    CityTimelineLargeMapState.IsSimulationRollbackProvable(
                        out var simulationReason
                    );
                var complete =
                    unifiedComplete && terrainComplete && simulationComplete;

                if (!simulationComplete)
                {
                    Util.Log.Error(
                        "[LargeMap] " + operation +
                        " cannot prove CellMap/Water rollback: " +
                        simulationReason + ". A World reload is required."
                    );
                }

                if (!complete)
                {
                    Util.Log.Error(
                        "[LargeMap] " + operation +
                        " runtime rollback reported incomplete resources. " +
                        "unified=" + unifiedComplete +
                        ", terrain=" + terrainComplete +
                        ", simulation=" + simulationComplete + "."
                    );
                }
                return complete;
            }
            catch (Exception ex)
            {
                Util.Log.Error(
                    "[LargeMap] " + operation + " runtime rollback failed: " + ex
                );
                return false;
            }
        }

        private static void ResetPatchState()
        {
            LargeMapTerrainSystemPatches.ResetRuntimeState();
            LargeMapCellMapSystemPatches.ResetManifest();
            LargeMapUnifiedWorldPatches.ResetPatchState();
        }

        private static bool RemoveSharedHarmonyOwner(string operation)
        {
            try
            {
                var harmony = _harmony ?? new Harmony(HarmonyId);
                harmony.UnpatchAll(HarmonyId);
            }
            catch (Exception ex)
            {
                Util.Log.Error("[LargeMap] " + operation + " UnpatchAll failed: " + ex);
            }

            if (!TryGetOwnedPatchedMethods(out var remaining))
            {
                Util.Log.Error("[LargeMap] " + operation + " owner verification failed.");
                return false;
            }

            if (remaining.Count != 0)
            {
                Util.Log.Error(
                    "[LargeMap] " + operation +
                    " left owned patched targets=" + remaining.Count + "."
                );
                return false;
            }

            return true;
        }

        private static bool CanRemoveSharedOwnerFromCurrentAssembly(
            out string reason)
        {
            if (!TryGetForeignOwnedPatchRecords(
                    out var foreignPatchCount,
                    out var foreignAssemblies))
            {
                reason = "Harmony patch assembly ownership cannot be inspected.";
                return false;
            }

            if (foreignPatchCount != 0)
            {
                reason =
                    "Harmony owner " + HarmonyId + " includes " +
                    foreignPatchCount + " patch records from other assemblies: " +
                    string.Join(", ", foreignAssemblies) + ".";
                return false;
            }

            reason = null;
            return true;
        }

        private static bool TryGetForeignOwnedPatchRecords(
            out int foreignPatchCount,
            out string[] foreignAssemblies)
        {
            foreignPatchCount = 0;
            var assemblies = new HashSet<string>(StringComparer.Ordinal);

            try
            {
                foreach (var method in Harmony.GetAllPatchedMethods())
                {
                    var patchInfo = Harmony.GetPatchInfo(method);
                    if (patchInfo == null)
                        continue;

                    CountForeignOwnedPatchRecords(
                        patchInfo.Prefixes,
                        assemblies,
                        ref foreignPatchCount
                    );
                    CountForeignOwnedPatchRecords(
                        patchInfo.Postfixes,
                        assemblies,
                        ref foreignPatchCount
                    );
                    CountForeignOwnedPatchRecords(
                        patchInfo.Transpilers,
                        assemblies,
                        ref foreignPatchCount
                    );
                    CountForeignOwnedPatchRecords(
                        patchInfo.Finalizers,
                        assemblies,
                        ref foreignPatchCount
                    );
                }

                foreignAssemblies = new List<string>(assemblies).ToArray();
                return true;
            }
            catch (Exception ex)
            {
                Util.Log.Error(
                    "[LargeMap] Harmony patch assembly inspection failed: " + ex
                );
                foreignPatchCount = 0;
                foreignAssemblies = new string[0];
                return false;
            }
        }

        private static void CountForeignOwnedPatchRecords(
            IEnumerable<Patch> patches,
            HashSet<string> assemblies,
            ref int foreignPatchCount)
        {
            if (patches == null)
                return;

            foreach (var patch in patches)
            {
                if (patch == null ||
                    !string.Equals(patch.owner, HarmonyId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (patch.PatchMethod == null)
                {
                    throw new InvalidOperationException(
                        "Harmony owner record has no patch method."
                    );
                }

                var patchAssembly = patch.PatchMethod.Module.Assembly;
                if (patchAssembly == typeof(CityTimelineLargeMapPatcher).Assembly)
                    continue;

                foreignPatchCount++;
                assemblies.Add(patchAssembly.FullName ?? "<unknown assembly>");
            }
        }

        private static bool TryGetOwnedPatchedMethods(
            out HashSet<MethodBase> methods)
        {
            methods = new HashSet<MethodBase>(MethodBaseComparer.Instance);

            try
            {
                foreach (var method in Harmony.GetAllPatchedMethods())
                {
                    var patchInfo = Harmony.GetPatchInfo(method);
                    if (patchInfo == null)
                        continue;

                    foreach (var owner in patchInfo.Owners)
                    {
                        if (string.Equals(owner, HarmonyId, StringComparison.Ordinal))
                        {
                            methods.Add(method);
                            break;
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Util.Log.Error("[LargeMap] Harmony owner inspection failed: " + ex);
                methods.Clear();
                return false;
            }
        }

        private static string DescribeMethod(MethodBase method)
        {
            if (method == null)
                return "<null>";

            return
                (method.DeclaringType != null
                    ? method.DeclaringType.FullName
                    : "<no-type>") +
                "::" + method.Name;
        }

        private sealed class MethodBaseComparer : IEqualityComparer<MethodBase>
        {
            internal static readonly MethodBaseComparer Instance =
                new MethodBaseComparer();

            public bool Equals(MethodBase x, MethodBase y)
            {
                if (ReferenceEquals(x, y))
                    return true;
                if (x == null || y == null)
                    return false;

                return x.MetadataToken == y.MetadataToken &&
                       x.Module == y.Module &&
                       x.DeclaringType == y.DeclaringType;
            }

            public int GetHashCode(MethodBase method)
            {
                if (method == null)
                    return 0;

                return method.MetadataToken.GetHashCode() ^
                       method.Module.GetHashCode() ^
                       (method.DeclaringType != null
                           ? method.DeclaringType.GetHashCode()
                           : 0);
            }
        }
    }
}

