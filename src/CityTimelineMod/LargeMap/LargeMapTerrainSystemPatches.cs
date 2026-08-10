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

                // Les heightmaps capturees sont des UnityEngine.Object : elles
                // peuvent avoir ete detruites entre la capture et le rollback.
                if (!snapshot.TexturesAreLive)
                {
                    reason =
                        "Captured vanilla heightmap textures were destroyed by Unity.";
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

                if (!snapshot.TexturesAreLive)
                {
                    failed++;
                    Util.Log.Error(
                        "[LargeMap] Terrain rollback aborted for World sequence=" +
                        terrainSystem.World.SequenceNumber +
                        ": the captured vanilla heightmap textures were destroyed."
                    );
                    continue;
                }

                try
                {
                    // Le gate runtime est ferme (verifie en tete de methode), donc
                    // FinalizeTerrainData_Prefix sortira immediatement et ne
                    // re-appliquera pas la conversion unifiee sur cet appel.
                    FinalizeTerrainDataMethod.Invoke(
                        terrainSystem,
                        new object[]
                        {
                            snapshot.Map,
                            snapshot.WorldMap,
                            snapshot.HeightScaleOffset,
                            snapshot.MapCorner,
                            snapshot.MapSize,
                            snapshot.WorldCorner,
                            snapshot.WorldSize,
                            snapshot.WorldHeightMinMax
                        }
                    );

                    string mismatch;
                    if (!snapshot.Matches(terrainSystem, out mismatch))
                    {
                        throw new InvalidOperationException(
                            "TerrainSystem properties do not match the captured " +
                            "vanilla inputs. " + mismatch
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

        // ATTENTION : Harmony lie les parametres PAR NOM avec la methode
        // originale. La signature reelle du jeu est :
        //
        //   FinalizeTerrainData(Texture2D map, Texture2D worldMap,
        //                       float2 heightScaleOffset,
        //                       float2 inMapCorner, float2 inMapSize,
        //                       float2 inWorldCorner, float2 inWorldSize,
        //                       float2 inWorldHeightMinMax)
        //
        // Le 3e parametre n'a PAS le prefixe "in". Toute divergence de nom
        // provoque une HarmonyException "IL Compile Error" a l'installation.
        [HarmonyPatch("FinalizeTerrainData")]
        [HarmonyPrefix]
        private static void FinalizeTerrainData_Prefix(
            TerrainSystem __instance,
            Texture2D map,
            Texture2D worldMap,
            float2 heightScaleOffset,
            ref float2 inMapCorner,
            ref float2 inMapSize,
            ref float2 inWorldCorner,
            ref float2 inWorldSize,
            float2 inWorldHeightMinMax)
        {
            if (!CityTimelineLargeMapState.Enabled)
                return;

            Util.Log.Info(
                "[LargeMap][TerrainProbe] FinalizeTerrainData BEFORE CTM: " +
                "map=" + (map != null ? map.width + "x" + map.height : "null") +
                ", worldMap=" + (worldMap != null ? worldMap.width + "x" + worldMap.height : "null") +
                ", mapSize=" + inMapSize +
                ", worldSize=" + inWorldSize +
                ", mapCorner=" + inMapCorner +
                ", worldCorner=" + inWorldCorner +
                ", heightScaleOffset=" + heightScaleOffset +
                ", worldHeightMinMax=" + inWorldHeightMinMax
            );

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

            bool isVanillaPrimary =
                Approximately(
                    inMapSize.x,
                    CityTimelineLargeMapState.OriginalMapSizeMetersFloat
                );

            bool isPersistedLargeMap =
                Approximately(
                    inMapSize.x,
                    CityTimelineLargeMapState.MapSizeMetersFloat
                ) &&
                Approximately(
                    inWorldSize.x,
                    CityTimelineLargeMapState.MapSizeMetersFloat
                ) &&
                Approximately(
                    inMapCorner.x,
                    -CityTimelineLargeMapState.HalfMapSizeMetersFloat
                ) &&
                Approximately(
                    inMapCorner.y,
                    -CityTimelineLargeMapState.HalfMapSizeMetersFloat
                ) &&
                Approximately(
                    inWorldCorner.x,
                    -CityTimelineLargeMapState.HalfMapSizeMetersFloat
                ) &&
                Approximately(
                    inWorldCorner.y,
                    -CityTimelineLargeMapState.HalfMapSizeMetersFloat
                ) &&
                worldMap == null;

            if (isPersistedLargeMap)
            {
                if (!_finalizeLogged)
                {
                    _finalizeLogged = true;

                    Util.Log.Info(
                        "[LargeMap] adopted persisted 57 km terrain state. " +
                        "mapSize=" + inMapSize.x +
                        ", worldSize=" + inWorldSize.x +
                        ", mapCorner=" + inMapCorner +
                        ", worldCorner=" + inWorldCorner
                    );
                }

                return;
            }

            if (!RuntimeSnapshots.ContainsKey(__instance))
            {
                bool restorable = isVanillaPrimary;

                var snapshot = new TerrainFinalizeSnapshot(
                    __instance,
                    map,
                    worldMap,
                    heightScaleOffset,
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

            inMapSize = unifiedSize;
            inWorldSize = unifiedSize;
            inMapCorner = unifiedCorner;
            inWorldCorner = unifiedCorner;

            if (!_finalizeLogged)
            {
                _finalizeLogged = true;

                Util.Log.Info(
                    "[LargeMap] unified terrain authority enabled from vanilla hierarchy. " +
                    "mapSize=" + inMapSize.x +
                    ", worldSize=" + inWorldSize.x +
                    ", corner=" + inMapCorner.x
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
            internal readonly Texture2D Map;
            internal readonly Texture2D WorldMap;
            internal readonly float2 HeightScaleOffset;
            internal readonly float2 MapCorner;
            internal readonly float2 MapSize;
            internal readonly float2 WorldCorner;
            internal readonly float2 WorldSize;
            internal readonly float2 WorldHeightMinMax;
            internal readonly bool Restorable;
            private readonly bool _mapCapturedNonNull;
            private readonly bool _worldMapCapturedNonNull;
            private readonly float2 _expectedWorldCorner;
            private readonly float2 _expectedWorldSize;
            private readonly int _expectedBaseLod;

            internal TerrainFinalizeSnapshot(
                TerrainSystem terrainSystem,
                Texture2D map,
                Texture2D worldMap,
                float2 heightScaleOffset,
                float2 mapCorner,
                float2 mapSize,
                float2 worldCorner,
                float2 worldSize,
                float2 worldHeightMinMax,
                bool restorable)
            {
                TerrainSystem = terrainSystem;
                Map = map;
                WorldMap = worldMap;
                HeightScaleOffset = heightScaleOffset;
                MapCorner = mapCorner;
                MapSize = mapSize;
                WorldCorner = worldCorner;
                WorldSize = worldSize;
                WorldHeightMinMax = worldHeightMinMax;
                Restorable = restorable;

                _mapCapturedNonNull = map != null;
                _worldMapCapturedNonNull = worldMap != null;

                // On raisonne sur le PARAMETRE worldMap, pas sur le champ
                // terrainSystem.worldHeightmap : dans un prefixe ce champ
                // reflete encore l'etat precedent, pas celui que
                // FinalizeTerrainData est en train d'installer.
                bool vanillaUsesPrimaryOnly =
                    Approximately(mapSize, worldSize) ||
                    worldMap == null;

                _expectedWorldCorner = vanillaUsesPrimaryOnly
                    ? mapCorner
                    : worldCorner;
                _expectedWorldSize = vanillaUsesPrimaryOnly
                    ? mapSize
                    : worldSize;
                _expectedBaseLod = vanillaUsesPrimaryOnly ? 0 : 1;
            }

            /// <summary>
            /// Faux si une texture capturee non nulle a depuis ete detruite
            /// par Unity : la reinjecter dans FinalizeTerrainData donnerait
            /// une reference morte.
            /// </summary>
            internal bool TexturesAreLive
            {
                get
                {
                    return
                        (!_mapCapturedNonNull || Map != null) &&
                        (!_worldMapCapturedNonNull || WorldMap != null);
                }
            }

            internal bool Matches(TerrainSystem terrainSystem, out string mismatch)
            {
                if (!Approximately(terrainSystem.playableOffset, MapCorner))
                {
                    mismatch = "playableOffset=" + terrainSystem.playableOffset +
                               ", expected=" + MapCorner;
                    return false;
                }

                if (!Approximately(terrainSystem.playableArea, MapSize))
                {
                    mismatch = "playableArea=" + terrainSystem.playableArea +
                               ", expected=" + MapSize;
                    return false;
                }

                if (!Approximately(terrainSystem.worldOffset, _expectedWorldCorner))
                {
                    mismatch = "worldOffset=" + terrainSystem.worldOffset +
                               ", expected=" + _expectedWorldCorner;
                    return false;
                }

                if (!Approximately(terrainSystem.worldSize, _expectedWorldSize))
                {
                    mismatch = "worldSize=" + terrainSystem.worldSize +
                               ", expected=" + _expectedWorldSize;
                    return false;
                }

                if (!Approximately(terrainSystem.heightScaleOffset, HeightScaleOffset))
                {
                    mismatch = "heightScaleOffset=" + terrainSystem.heightScaleOffset +
                               ", expected=" + HeightScaleOffset;
                    return false;
                }

                if (!Approximately(terrainSystem.worldHeightMinMax, WorldHeightMinMax))
                {
                    mismatch = "worldHeightMinMax=" + terrainSystem.worldHeightMinMax +
                               ", expected=" + WorldHeightMinMax;
                    return false;
                }

                int baseLod;
                try
                {
                    // baseLod peut etre statique ou d'instance selon la version
                    // du jeu : passer systematiquement null leverait une
                    // TargetException sur une propriete d'instance.
                    object owner = BaseLodField.IsStatic ? null : (object)terrainSystem;
                    baseLod = (int)BaseLodField.GetValue(owner);
                }
                catch (Exception ex)
                {
                    mismatch = "baseLod could not be read (" + ex.GetType().Name +
                               ": " + ex.Message + ")";
                    return false;
                }

                if (baseLod != _expectedBaseLod)
                {
                    mismatch = "baseLod=" + baseLod +
                               ", expected=" + _expectedBaseLod;
                    return false;
                }

                mismatch = null;
                return true;
            }

            private static bool Approximately(float2 a, float2 b)
            {
                return math.all(math.abs(a - b) < new float2(0.01f));
            }
        }
    }
}
