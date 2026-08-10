using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Common;
using Game.Objects;
using Game.Prefabs;
using Game.Simulation;
using Game.Tools;
using HarmonyLib;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace CityTimelineMod.PlayableWorld
{
    internal static class PlayableWorldPatcher
    {
        private const string HarmonyId = "CityTimelineMod.PlayableWorld";
        private const int ExpectedPatchClassCount = 8;

        private static readonly Type[] PatchTypes =
        {
            typeof(CapturePlayableWorldBoundsPatch),
            typeof(NetToolSystemSnapControlPointsPatch),
            typeof(NetToolSnapJobPlayableBoundsPatch),
            typeof(ObjectToolSnapJobPlayableBoundsPatch),
            typeof(GenerateNodesPlayableBoundsPatch),
            typeof(NetGeometryPlayableBoundsPatch),
            typeof(ObjectWorldBoundsValidationPatch),
            typeof(OutsideConnectionValidationPatch)
        };

        private static Harmony _harmony;
        private static bool _installed;

        internal static bool Install(bool authorized, ulong worldSequence)
        {
            if (!authorized || !Mod.RuntimeEnabled)
            {
                var cleanupComplete = Uninstall();
                Util.Log.Info(
                    "[PlayableWorld] install skipped: configuration authorization or runtime gate is false; " +
                    "cleanupComplete=" + cleanupComplete + "."
                );
                return false;
            }

            Dictionary<Type, MethodBase> expectedTargets;

            try
            {
                expectedTargets = ResolveExpectedTargets();
            }
            catch (Exception ex)
            {
                return FailInstall(0, "target resolution", ex);
            }

            if (_installed && PlayableWorldState.Installed)
            {
                if (TryVerifyExactInstallation(
                        expectedTargets,
                        out var installedTargetCount,
                        out var installedPatchMethodCount))
                {
                    Util.Log.Info(
                        "[PlayableWorld] existing atomic install verified. ownedTargets=" +
                        installedTargetCount + ", ownedPatchMethods=" +
                        installedPatchMethodCount + "."
                    );
                    PlayableWorldState.Enable(worldSequence);
                    return true;
                }

                Util.Log.Error("[PlayableWorld] installed state failed verification; reinstalling fail-closed.");
            }

            PlayableWorldState.Disable();
            _installed = false;

            if (!RemoveOwnedPatches("pre-install cleanup"))
            {
                _harmony = null;
                Util.Log.Error("[PlayableWorld] install blocked: stale owned patches could not be removed.");
                return false;
            }

            var patchedClasses = 0;

            try
            {
                _harmony = new Harmony(HarmonyId);

                foreach (var patchType in PatchTypes)
                {
                    PatchClass(patchType);
                    patchedClasses++;

                    if (!TryVerifyExactPatchClass(
                            patchType,
                            expectedTargets[patchType],
                            out var actualPatchMethodCount))
                    {
                        throw new InvalidOperationException(
                            "Exact Harmony patch verification failed for " +
                            patchType.Name + "; ownedPatchMethods=" +
                            actualPatchMethodCount + "."
                        );
                    }
                }

                if (patchedClasses != ExpectedPatchClassCount)
                {
                    throw new InvalidOperationException(
                        "Expected " + ExpectedPatchClassCount +
                        " patch classes, installed " + patchedClasses + "."
                    );
                }

                if (!TryVerifyExactInstallation(
                        expectedTargets,
                        out var ownedTargetCount,
                        out var ownedPatchMethodCount))
                {
                    throw new InvalidOperationException(
                        "Exact Harmony installation verification failed."
                    );
                }

                _installed = true;
                PlayableWorldState.Enable(worldSequence);

                Util.Log.Info(
                    "[PlayableWorld] atomic install verified. patchClasses=" +
                    patchedClasses + ", ownedTargets=" + ownedTargetCount +
                    ", ownedPatchMethods=" + ownedPatchMethodCount
                );

                return true;
            }
            catch (Exception ex)
            {
                return FailInstall(patchedClasses, "patch installation", ex);
            }
        }

        internal static bool Uninstall()
        {
            PlayableWorldState.Disable();

            var rollbackComplete = RemoveOwnedPatches("uninstall");
            _installed = false;
            _harmony = null;

            if (rollbackComplete)
                Util.Log.Info("[PlayableWorld] patches removed.");
            else
                Util.Log.Error("[PlayableWorld] uninstall incomplete: owned Harmony patches remain or could not be verified.");

            return rollbackComplete;
        }

        private static bool FailInstall(
            int patchedClasses,
            string stage,
            Exception exception)
        {
            PlayableWorldState.Disable();
            var rollbackComplete = RemoveOwnedPatches("install rollback");
            _installed = false;
            _harmony = null;

            Util.Log.Error(
                "[PlayableWorld] install failed during " + stage +
                " after patchClasses=" + patchedClasses +
                "; rollbackComplete=" + rollbackComplete + ". " + exception
            );

            return false;
        }

        private static void PatchClass(Type patchType)
        {
            if (patchType == null)
                throw new ArgumentNullException(nameof(patchType));

            var patchedMethods = _harmony
                .CreateClassProcessor(patchType)
                .Patch();

            if (patchedMethods == null || patchedMethods.Count == 0)
            {
                throw new InvalidOperationException(
                    "Harmony produced no patch for " + patchType.Name + "."
                );
            }

            Util.Log.Info("[PlayableWorld] patch installed: " + patchType.Name);
        }

        private static Dictionary<Type, MethodBase> ResolveExpectedTargets()
        {
            if (PatchTypes.Length != ExpectedPatchClassCount)
            {
                throw new InvalidOperationException(
                    "Expected " + ExpectedPatchClassCount +
                    " patch classes, configured " + PatchTypes.Length + "."
                );
            }

            var targets = new Dictionary<Type, MethodBase>();

            AddExpectedTarget(
                targets,
                typeof(CapturePlayableWorldBoundsPatch),
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
                )
            );

            AddExpectedTarget(
                targets,
                typeof(NetToolSystemSnapControlPointsPatch),
                AccessTools.Method(
                    typeof(NetToolSystem),
                    "SnapControlPoints",
                    new[] { typeof(JobHandle), typeof(bool) }
                )
            );
            AddExpectedTarget(
                targets,
                typeof(NetToolSnapJobPlayableBoundsPatch),
                AccessTools.Method(
                    AccessTools.Inner(typeof(NetToolSystem), "SnapJob"),
                    "HandleWorldSize",
                    new[]
                    {
                        typeof(ControlPoint).MakeByRefType(),
                        typeof(ControlPoint),
                        typeof(NetGeometryData)
                    }
                )
            );
            AddExpectedTarget(
                targets,
                typeof(ObjectToolSnapJobPlayableBoundsPatch),
                AccessTools.Method(
                    AccessTools.Inner(typeof(ObjectToolSystem), "SnapJob"),
                    "HandleWorldSize",
                    new[]
                    {
                        typeof(ControlPoint).MakeByRefType(),
                        typeof(ControlPoint)
                    }
                )
            );
            AddExpectedTarget(
                targets,
                typeof(GenerateNodesPlayableBoundsPatch),
                AccessTools.Method(
                    typeof(GenerateNodesSystem),
                    "OnUpdate",
                    Type.EmptyTypes
                )
            );
            AddExpectedTarget(
                targets,
                typeof(NetGeometryPlayableBoundsPatch),
                AccessTools.Method(
                    typeof(Game.Net.GeometrySystem),
                    "OnUpdate",
                    Type.EmptyTypes
                )
            );
            AddExpectedTarget(
                targets,
                typeof(ObjectWorldBoundsValidationPatch),
                AccessTools.Method(
                    typeof(ValidationHelpers),
                    "ValidateWorldBounds",
                    new[]
                    {
                        typeof(Entity),
                        typeof(Owner),
                        typeof(Colossal.Mathematics.Bounds3),
                        typeof(ValidationSystem.EntityData),
                        typeof(TerrainHeightData),
                        typeof(NativeQueue<ErrorData>.ParallelWriter)
                    }
                )
            );
            AddExpectedTarget(
                targets,
                typeof(OutsideConnectionValidationPatch),
                AccessTools.Method(
                    typeof(ValidationHelpers),
                    "ValidateOutsideConnection",
                    new[]
                    {
                        typeof(Entity),
                        typeof(Game.Objects.Transform),
                        typeof(TerrainHeightData),
                        typeof(NativeQueue<ErrorData>.ParallelWriter)
                    }
                )
            );

            var uniqueTargets = new HashSet<MethodBase>(
                targets.Values,
                MethodBaseComparer.Instance
            );

            if (targets.Count != ExpectedPatchClassCount ||
                uniqueTargets.Count != ExpectedPatchClassCount)
            {
                throw new InvalidOperationException(
                    "PlayableWorld target manifest must contain exactly " +
                    ExpectedPatchClassCount + " distinct methods."
                );
            }

            return targets;
        }

        private static void AddExpectedTarget(
            Dictionary<Type, MethodBase> targets,
            Type patchType,
            MethodBase target)
        {
            if (patchType == null)
                throw new ArgumentNullException(nameof(patchType));

            if (target == null)
            {
                throw new MissingMethodException(
                    "PlayableWorld target resolution failed for " + patchType.Name + "."
                );
            }

            if (targets.ContainsKey(patchType))
            {
                throw new InvalidOperationException(
                    "Duplicate PlayableWorld patch type: " + patchType.Name + "."
                );
            }

            targets.Add(patchType, target);
        }

        private static bool TryVerifyExactInstallation(
            Dictionary<Type, MethodBase> expectedTargets,
            out int ownedTargetCount,
            out int ownedPatchMethodCount)
        {
            ownedTargetCount = 0;
            ownedPatchMethodCount = 0;

            try
            {
                if (!TryGetOwnedPatchedMethods(out var ownedTargets))
                    return false;

                ownedTargetCount = ownedTargets.Count;

                var expectedTargetSet = new HashSet<MethodBase>(
                    expectedTargets.Values,
                    MethodBaseComparer.Instance
                );

                if (expectedTargetSet.Count != ExpectedPatchClassCount ||
                    !ownedTargets.SetEquals(expectedTargetSet))
                {
                    Util.Log.Error(
                        "[PlayableWorld] owned target set mismatch. expected=" +
                        expectedTargetSet.Count + ", actual=" + ownedTargets.Count + "."
                    );
                    return false;
                }

                foreach (var patchType in PatchTypes)
                {
                    if (!expectedTargets.TryGetValue(patchType, out var target))
                        return false;

                    if (!TryVerifyExactPatchClass(
                            patchType,
                            target,
                            out var actualPatchMethods))
                    {
                        return false;
                    }

                    ownedPatchMethodCount += actualPatchMethods;
                }

                return true;
            }
            catch (Exception ex)
            {
                Util.Log.Error(
                    "[PlayableWorld] exact Harmony verification failed: " + ex
                );
                ownedTargetCount = 0;
                ownedPatchMethodCount = 0;
                return false;
            }
        }

        private static bool TryVerifyExactPatchClass(
            Type patchType,
            MethodBase target,
            out int actualPatchMethodCount)
        {
            actualPatchMethodCount = 0;

            var patchInfo = Harmony.GetPatchInfo(target);
            if (patchInfo == null)
                return false;

            var expectedPatchMethods = GetDeclaredHarmonyPatchMethods(patchType);
            var actualPatchMethods = new HashSet<MethodBase>(
                MethodBaseComparer.Instance
            );

            AddOwnedPatchMethods(actualPatchMethods, patchInfo.Prefixes);
            AddOwnedPatchMethods(actualPatchMethods, patchInfo.Postfixes);
            AddOwnedPatchMethods(actualPatchMethods, patchInfo.Transpilers);
            AddOwnedPatchMethods(actualPatchMethods, patchInfo.Finalizers);

            actualPatchMethodCount = actualPatchMethods.Count;

            if (!actualPatchMethods.SetEquals(expectedPatchMethods))
            {
                Util.Log.Error(
                    "[PlayableWorld] patch method set mismatch for " +
                    patchType.Name + ". expected=" + expectedPatchMethods.Count +
                    ", actual=" + actualPatchMethods.Count + "."
                );
                return false;
            }

            return true;
        }

        private static HashSet<MethodBase> GetDeclaredHarmonyPatchMethods(
            Type patchType)
        {
            var result = new HashSet<MethodBase>(MethodBaseComparer.Instance);
            var methods = patchType.GetMethods(
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.Static |
                BindingFlags.DeclaredOnly
            );

            foreach (var method in methods)
            {
                if (method.IsDefined(typeof(HarmonyPrefix), false) ||
                    method.IsDefined(typeof(HarmonyPostfix), false) ||
                    method.IsDefined(typeof(HarmonyTranspiler), false) ||
                    method.IsDefined(typeof(HarmonyFinalizer), false))
                {
                    result.Add(method);
                }
            }

            if (result.Count == 0)
            {
                throw new InvalidOperationException(
                    "No Harmony patch method declared by " + patchType.Name + "."
                );
            }

            return result;
        }

        private static void AddOwnedPatchMethods(
            HashSet<MethodBase> destination,
            IEnumerable<Patch> patches)
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

                destination.Add(patch.PatchMethod);
            }
        }

        private static bool RemoveOwnedPatches(string operation)
        {
            try
            {
                var harmony = _harmony ?? new Harmony(HarmonyId);
                harmony.UnpatchAll(HarmonyId);
            }
            catch (Exception ex)
            {
                Util.Log.Error("[PlayableWorld] " + operation + " UnpatchAll failed: " + ex);
            }

            if (!TryGetOwnedPatchedMethods(out var remaining))
            {
                Util.Log.Error("[PlayableWorld] " + operation + " owner verification failed.");
                return false;
            }

            if (remaining.Count != 0)
            {
                Util.Log.Error(
                    "[PlayableWorld] " + operation +
                    " left owned patched targets=" + remaining.Count + "."
                );
                return false;
            }

            return true;
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
                Util.Log.Error("[PlayableWorld] Harmony owner inspection failed: " + ex);
                methods.Clear();
                return false;
            }
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
