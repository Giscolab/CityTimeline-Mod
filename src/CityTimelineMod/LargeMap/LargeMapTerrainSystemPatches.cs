using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Game.Simulation;
using HarmonyLib;
using Unity.Mathematics;
using UnityEngine;

namespace CityTimelineMod.LargeMap
{
    [HarmonyPatch(typeof(TerrainSystem))]
    internal static class LargeMapTerrainSystemPatches
    {
        private static bool _finalizeLogged;

        private static readonly Dictionary<TerrainSystem, TerrainFinalizeSnapshot>
            RuntimeSnapshots =
                new Dictionary<TerrainSystem, TerrainFinalizeSnapshot>();

        private static readonly MethodInfo EffectiveMapSizeMethod =
            AccessTools.Method(
                typeof(CityTimelineLargeMapState),
                nameof(CityTimelineLargeMapState.GetEffectiveMapSizeMetersFloat)
            );

        private static readonly MethodInfo FinalizeTerrainDataMethod =
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
            );

        private static readonly FieldInfo BaseLodField =
            AccessTools.Field(typeof(TerrainSystem), "<baseLod>k__BackingField");

        internal static void ResetRuntimeState()
        {
            _finalizeLogged = false;

            if (RuntimeSnapshots.Count != 0)
            {
                Util.Log.Error(
                    "[LargeMap] Terrain reset retained rollback snapshots=" +
                    RuntimeSnapshots.Count + "."
                );
            }
        }

        internal static bool CanRollbackRuntimeMutationsWithoutMutation(
            out string reason)
        {
            if (RuntimeSnapshots.Count == 0)
            {
                reason = null;
                return true;
            }

            if (FinalizeTerrainDataMethod == null || BaseLodField == null)
            {
                reason =
                    "Terrain rollback API FinalizeTerrainData/baseLod is unavailable.";
                return false;
            }

            foreach (var snapshot in RuntimeSnapshots.Values)
            {
                if (snapshot == null || snapshot.TerrainSystem == null)
                {
                    reason = "A Terrain rollback snapshot has no TerrainSystem.";
                    return false;
                }

                if (snapshot.TerrainSystem.World == null ||
                    !snapshot.TerrainSystem.World.IsCreated)
                {
                    continue;
                }

                if (!snapshot.Restorable)
                {
                    reason =
                        "Live TerrainSystem rollback lacks proven vanilla inputs.";
                    return false;
                }
            }

            reason = null;
            return true;
        }

        internal static bool TryRollbackRuntimeMutations()
        {
            if (CityTimelineLargeMapState.Enabled)
            {
                Util.Log.Error(
                    "[LargeMap] Terrain rollback refused while the runtime gate is open."
                );
                return false;
            }

            if (RuntimeSnapshots.Count == 0)
                return true;

            if (FinalizeTerrainDataMethod == null || BaseLodField == null)
            {
                Util.Log.Error(
                    "[LargeMap] Terrain rollback cannot resolve FinalizeTerrainData."
                );
                return false;
            }

            var snapshots = new List<TerrainFinalizeSnapshot>(RuntimeSnapshots.Values);
            var failed = 0;

            for (var i = 0; i < snapshots.Count; i++)
            {
                var snapshot = snapshots[i];
                var terrainSystem = snapshot.TerrainSystem;

                if (terrainSystem == null)
                {
                    failed++;
                    continue;
                }

                if (terrainSystem.World == null || !terrainSystem.World.IsCreated)
                {
                    RuntimeSnapshots.Remove(terrainSystem);
                    continue;
                }

                if (!snapshot.Restorable)
                {
                    failed++;
                    continue;
                }

                try
                {
                    FinalizeTerrainDataMethod.Invoke(
                        terrainSystem,
                        new object[]
                        {
                            null,
                            null,
                            snapshot.HeightScaleOffset,
                            snapshot.MapCorner,
                            snapshot.MapSize,
                            snapshot.WorldCorner,
                            snapshot.WorldSize,
                            snapshot.WorldHeightMinMax
                        }
                    );

                    if (!snapshot.Matches(terrainSystem))
                    {
                        throw new InvalidOperationException(
                            "TerrainSystem properties do not match the captured vanilla inputs."
                        );
                    }

                    RuntimeSnapshots.Remove(terrainSystem);
                }
                catch (Exception ex)
                {
                    failed++;
                    Util.Log.Error(
                        "[LargeMap] Terrain rollback failed for World sequence=" +
                        terrainSystem.World.SequenceNumber + ". " + ex
                    );
                }
            }

            if (failed != 0 || RuntimeSnapshots.Count != 0)
            {
                Util.Log.Error(
                    "[LargeMap] Terrain rollback incomplete. failed=" + failed +
                    ", pendingSnapshots=" + RuntimeSnapshots.Count + "."
                );
                return false;
            }

            return true;
        }

        [HarmonyPatch("FinalizeTerrainData")]
        [HarmonyPrefix]
        private static void FinalizeTerrainData_Prefix(
            TerrainSystem __instance,
            float2 inHeightScaleOffset,
            ref float2 inMapCorner,
            ref float2 inMapSize,
            ref float2 inWorldCorner,
            ref float2 inWorldSize,
            float2 inWorldHeightMinMax)
        {
            if (!CityTimelineLargeMapState.Enabled)
                return;

            bool recognizedMapSize =
                IsFinite(inMapSize) &&
                Approximately(inMapSize.x, inMapSize.y) &&
                (Approximately(inMapSize.x, CityTimelineLargeMapState.OriginalMapSizeMetersFloat) ||
                 Approximately(inMapSize.x, CityTimelineLargeMapState.MapSizeMetersFloat));

            bool recognizedWorldSize =
                IsFinite(inWorldSize) &&
                Approximately(inWorldSize.x, inWorldSize.y) &&
                (Approximately(inWorldSize.x, CityTimelineLargeMapState.OriginalMapSizeMetersFloat) ||
                 Approximately(inWorldSize.x, CityTimelineLargeMapState.MapSizeMetersFloat) ||
                 Approximately(
                     inWorldSize.x,
                     CityTimelineLargeMapState.MapSizeMetersFloat * CityTimelineLargeMapState.CoreValue
                 ));

            if (__instance == null || __instance.World == null ||
                !__instance.World.IsCreated ||
                !recognizedMapSize || !recognizedWorldSize ||
                !IsFinite(inMapCorner) || !IsFinite(inWorldCorner))
            {
                CityTimelineLargeMapState.FailRuntime(
                    "FinalizeTerrainData received unsupported dimensions or World state"
                );
                Util.Log.Error(
                    "[LargeMap] FinalizeTerrainData received unsupported dimensions. " +
                    "mapSize=" + inMapSize + ", worldSize=" + inWorldSize +
                    ", mapCorner=" + inMapCorner + ", worldCorner=" + inWorldCorner +
                    ". Runtime gate closed and unified conversion skipped."
                );
                return;
            }

            if (!RuntimeSnapshots.ContainsKey(__instance))
            {
                bool restorable =
                    Approximately(
                        inMapSize.x,
                        CityTimelineLargeMapState.OriginalMapSizeMetersFloat
                    );

                var snapshot = new TerrainFinalizeSnapshot(
                    __instance,
                    inHeightScaleOffset,
                    inMapCorner,
                    inMapSize,
                    inWorldCorner,
                    inWorldSize,
                    inWorldHeightMinMax,
                    restorable
                );
                RuntimeSnapshots.Add(__instance, snapshot);

                if (!restorable)
                {
                    CityTimelineLargeMapState.FailRuntime(
                        "the first observed TerrainSystem state was already extended and " +
                        "cannot be rolled back to proven vanilla inputs"
                    );
                    Util.Log.Error(
                        "[LargeMap] Terrain conversion refused: no proven vanilla " +
                        "FinalizeTerrainData inputs are available for rollback."
                    );
                    return;
                }
            }

            float2 unifiedSize = new float2(
                CityTimelineLargeMapState.MapSizeMetersFloat,
                CityTimelineLargeMapState.MapSizeMetersFloat
            );

            float2 unifiedCorner = -0.5f * unifiedSize;

            // Solution A : le terrain principal et le monde ont exactement
            // la même emprise. TerrainSystem choisit alors baseLod = 0 et
            // n'utilise plus le backdrop comme seconde source de hauteur.
            inMapSize = unifiedSize;
            inWorldSize = unifiedSize;
            inMapCorner = unifiedCorner;
            inWorldCorner = unifiedCorner;

            if (!_finalizeLogged)
            {
                _finalizeLogged = true;

                Util.Log.Info(
                    "[LargeMap] unified terrain authority enabled. " +
                    "mapSize=" + inMapSize.x +
                    ", worldSize=" + inWorldSize.x +
                    ", corner=" + inMapCorner
                );
            }
        }

        [HarmonyPatch(nameof(TerrainSystem.GetTerrainBounds))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> GetTerrainBounds_Transpiler(
            IEnumerable<CodeInstruction> instructions)
        {
            return ReplaceMapSizeConstants(
                instructions,
                "TerrainSystem.GetTerrainBounds"
            );
        }

        [HarmonyPatch(nameof(TerrainSystem.GetHeightData))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> GetHeightData_Transpiler(
            IEnumerable<CodeInstruction> instructions)
        {
            return ReplaceMapSizeConstants(
                instructions,
                "TerrainSystem.GetHeightData"
            );
        }

        private static IEnumerable<CodeInstruction> ReplaceMapSizeConstants(
            IEnumerable<CodeInstruction> instructions,
            string methodName)
        {
            int patches = 0;
            var codes = new List<CodeInstruction>(instructions);

            if (EffectiveMapSizeMethod == null)
            {
                throw new MissingMethodException(
                    typeof(CityTimelineLargeMapState).FullName,
                    nameof(CityTimelineLargeMapState.GetEffectiveMapSizeMetersFloat)
                );
            }

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode != OpCodes.Ldc_R4 || !(codes[i].operand is float))
                    continue;

                float value = (float)codes[i].operand;

                if (!Approximately(value, CityTimelineLargeMapState.OriginalMapSizeMetersFloat))
                    continue;

                codes[i].opcode = OpCodes.Call;
                codes[i].operand = EffectiveMapSizeMethod;
                patches++;
            }

            if (patches != 2)
            {
                throw new InvalidOperationException(
                    "[LargeMap] " + methodName +
                    " transpiler expected exactly 2 occurrences of 14336f, found=" +
                    patches + ". Game.dll changed."
                );
            }

            Util.Log.Info(
                "[LargeMap] " + methodName +
                " transpiler patched constants=" + patches
            );

            return codes;
        }

        private static bool Approximately(float a, float b)
        {
            return math.abs(a - b) < 0.01f;
        }

        private static bool IsFinite(float2 value)
        {
            return
                !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                !float.IsNaN(value.y) && !float.IsInfinity(value.y);
        }

        private sealed class TerrainFinalizeSnapshot
        {
            internal readonly TerrainSystem TerrainSystem;
            internal readonly float2 HeightScaleOffset;
            internal readonly float2 MapCorner;
            internal readonly float2 MapSize;
            internal readonly float2 WorldCorner;
            internal readonly float2 WorldSize;
            internal readonly float2 WorldHeightMinMax;
            internal readonly bool Restorable;
            private readonly float2 _expectedWorldCorner;
            private readonly float2 _expectedWorldSize;
            private readonly int _expectedBaseLod;

            internal TerrainFinalizeSnapshot(
                TerrainSystem terrainSystem,
                float2 heightScaleOffset,
                float2 mapCorner,
                float2 mapSize,
                float2 worldCorner,
                float2 worldSize,
                float2 worldHeightMinMax,
                bool restorable)
            {
                TerrainSystem = terrainSystem;
                HeightScaleOffset = heightScaleOffset;
                MapCorner = mapCorner;
                MapSize = mapSize;
                WorldCorner = worldCorner;
                WorldSize = worldSize;
                WorldHeightMinMax = worldHeightMinMax;
                Restorable = restorable;

                bool vanillaUsesPrimaryOnly =
                    Approximately(mapSize, worldSize) ||
                    terrainSystem.worldHeightmap == null;
                _expectedWorldCorner = vanillaUsesPrimaryOnly
                    ? mapCorner
                    : worldCorner;
                _expectedWorldSize = vanillaUsesPrimaryOnly
                    ? mapSize
                    : worldSize;
                _expectedBaseLod = vanillaUsesPrimaryOnly ? 0 : 1;
            }

            internal bool Matches(TerrainSystem terrainSystem)
            {
                return
                    Approximately(terrainSystem.playableOffset, MapCorner) &&
                    Approximately(terrainSystem.playableArea, MapSize) &&
                    Approximately(terrainSystem.worldOffset, _expectedWorldCorner) &&
                    Approximately(terrainSystem.worldSize, _expectedWorldSize) &&
                    Approximately(terrainSystem.heightScaleOffset, HeightScaleOffset) &&
                    Approximately(terrainSystem.worldHeightMinMax, WorldHeightMinMax) &&
                    (int)BaseLodField.GetValue(null) == _expectedBaseLod;
            }

            private static bool Approximately(float2 a, float2 b)
            {
                return math.all(math.abs(a - b) < new float2(0.01f));
            }
        }
    }
}
