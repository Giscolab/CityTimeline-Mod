using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Game.Areas;
using Game.Simulation;
using HarmonyLib;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace CityTimelineMod.LargeMap
{
    internal static class LargeMapUnifiedWorldPatches
    {
        internal static void Apply(Harmony harmony)
        {
            PatchClass(harmony, typeof(UnifiedWorldmapImportPatch));
            PatchClass(harmony, typeof(LargeMapTileScalePatch));
            PatchClass(harmony, typeof(LargeMapAirwayScalePatch));
            // PatchClass(harmony, typeof(LargeMapToolFeedbackLoadingPatch));
            LargeMapWaterMapSizePatches.Apply(harmony);
        }

        internal static bool RollbackRuntimeMutations()
        {
            var worldmapRestored = TryRollbackFamily(
                "worldmap",
                UnifiedWorldmapImportPatch.TryRollbackRuntimeMutation
            );
            var tilesRestored = TryRollbackFamily(
                "MapTiles",
                LargeMapTileScalePatch.TryRollbackRuntimeMutations
            );
            var airwayRestored = TryRollbackFamily(
                "Airway",
                LargeMapAirwayScalePatch.TryRollbackRuntimeMutations
            );
            return worldmapRestored && tilesRestored && airwayRestored;
        }

        internal static bool CanRollbackRuntimeMutationsWithoutMutation(
            out string reason)
        {
            if (!UnifiedWorldmapImportPatch
                    .CanRollbackRuntimeMutationWithoutMutation(out reason))
            {
                reason = "worldmap: " + reason;
                return false;
            }

            if (!LargeMapTileScalePatch
                    .CanRollbackRuntimeMutationsWithoutMutation(out reason))
            {
                reason = "MapTiles: " + reason;
                return false;
            }

            if (!LargeMapAirwayScalePatch
                    .CanRollbackRuntimeMutationsWithoutMutation(out reason))
            {
                reason = "Airway: " + reason;
                return false;
            }

            reason = null;
            return true;
        }

        private static bool TryRollbackFamily(string name, Func<bool> rollback)
        {
            try
            {
                return rollback();
            }
            catch (Exception ex)
            {
                Util.Log.Error(
                    "[LargeMap] " + name + " runtime rollback threw: " + ex
                );
                return false;
            }
        }

        internal static void ResetPatchState()
        {
            UnifiedWorldmapImportPatch.ResetRuntimeState();
            LargeMapTileScalePatch.ResetRuntimeState();
            LargeMapAirwayScalePatch.ResetRuntimeState();
            LargeMapToolFeedbackLoadingPatch.ResetRuntimeState();
            LargeMapWaterMapSizePatches.ResetManifest();
        }

        private static void PatchClass(Harmony harmony, Type patchType)
        {
            var patchedMethods = harmony.CreateClassProcessor(patchType).Patch();
            if (patchedMethods == null || patchedMethods.Count == 0)
            {
                throw new InvalidOperationException(
                    "Harmony produced no patch for " + patchType.Name + "."
                );
            }

            Util.Log.Info("[LargeMap] patch installed: " + patchType.Name);
        }
    }

    /*
     * The 57 km workflow promotes the file selected through the game's
     * worldmap importer to the primary terrain heightmap. Unlike the archived
     * implementation, the backdrop is not destroyed before promotion: with
     * equal map/world extents FinalizeTerrainData selects baseLod 0, so keeping
     * the old backdrop reference is harmless and leaves a recoverable fallback.
     */
    [HarmonyPatch(typeof(TerrainSystem), nameof(TerrainSystem.ReplaceWorldHeightmap))]
    internal static class UnifiedWorldmapImportPatch
    {
        private static readonly MethodInfo IsValidHeightmapFormatMethod =
            AccessTools.Method(
                typeof(TerrainSystem),
                "IsValidHeightmapFormat",
                new[] { typeof(Texture2D) }
            );

        private static bool _transactionActive;
        private static CommittedWorldmapSnapshot _committedSnapshot;

        [HarmonyPrefix]
        private static bool Prefix(TerrainSystem __instance, Texture2D inMap)
        {
            if (!CityTimelineLargeMapState.Enabled)
                return true;

            // Null is the official "remove/reset worldmap" path. It does not
            // need promotion and is safest in the vanilla implementation.
            if (inMap == null)
                return true;

            if (__instance == null || _transactionActive ||
                IsValidHeightmapFormatMethod == null ||
                !TerrainPropertySnapshot.AccessorsAvailable ||
                inMap.width <= 0 || inMap.height <= 0)
            {
                Util.Log.Error(
                    "[LargeMap] worldmap promotion precondition failed; vanilla importer retained."
                );
                return true;
            }

            bool validFormat;
            try
            {
                validFormat = (bool)IsValidHeightmapFormatMethod.Invoke(
                    null,
                    new object[] { inMap }
                );
            }
            catch (Exception ex)
            {
                Util.Log.Error(
                    "[LargeMap] worldmap format validation failed before mutation; " +
                    "vanilla importer retained. " + ex
                );
                return true;
            }

            if (!validFormat)
            {
                Util.Log.Error(
                    "[LargeMap] worldmap texture format rejected before mutation; " +
                    "vanilla importer retained."
                );
                return true;
            }

            if (!ReleaseCommittedSnapshotFromDestroyedWorld(__instance))
                return true;

            var currentHeightmap = __instance.heightmap as RenderTexture;
            if (currentHeightmap == null || !currentHeightmap.IsCreated())
            {
                Util.Log.Error(
                    "[LargeMap] primary heightmap is unavailable before worldmap promotion; " +
                    "vanilla importer retained."
                );
                return true;
            }

            Texture2D heightmapBackup = null;
            TerrainPropertySnapshot snapshot = null;

            try
            {
                snapshot = new TerrainPropertySnapshot(__instance);
                heightmapBackup = CreateReadableHeightmapBackup(
                    __instance,
                    currentHeightmap
                );
            }
            catch (Exception ex)
            {
                if (heightmapBackup != null)
                    UnityEngine.Object.Destroy(heightmapBackup);

                Util.Log.Error(
                    "[LargeMap] primary heightmap snapshot failed before mutation; " +
                    "vanilla importer retained. " + ex
                );
                return true;
            }

            _transactionActive = true;
            var mutationStarted = false;

            try
            {
                mutationStarted = true;
                __instance.ReplaceHeightmap(inMap);

                if (!CityTimelineLargeMapState.Enabled ||
                    !Approximately(
                        __instance.playableArea.x,
                        CityTimelineLargeMapState.MapSizeMetersFloat) ||
                    !Approximately(
                        __instance.playableArea.y,
                        CityTimelineLargeMapState.MapSizeMetersFloat))
                {
                    throw new InvalidOperationException(
                        "TerrainSystem did not confirm a unified 57 km playable area."
                    );
                }

                Util.Log.Info(
                    "[LargeMap] imported worldmap promoted transactionally to the " +
                    "single 57 km primary heightmap. Existing backdrop retained as fallback."
                );

                // Keep the first known-good pre-import state for this World.
                // Later imports use their local backup for immediate failure
                // rollback, while uninstall still returns to the original
                // terrain state from before CTM's first successful promotion.
                if (_committedSnapshot == null)
                {
                    _committedSnapshot = new CommittedWorldmapSnapshot(
                        __instance,
                        heightmapBackup,
                        snapshot
                    );
                    heightmapBackup = null;
                }

                return false;
            }
            catch (Exception ex)
            {
                bool rollbackComplete = !mutationStarted ||
                    TryRollback(__instance, heightmapBackup, snapshot);

                Util.Log.Error(
                    "[LargeMap] unified worldmap promotion failed; rollbackComplete=" +
                    rollbackComplete + ". " + ex
                );

                if (!rollbackComplete)
                {
                    CityTimelineLargeMapState.FailRuntime(
                        "worldmap promotion failed and its transactional rollback was incomplete"
                    );
                }

                // Vanilla is safe only if no mutation occurred or the snapshot
                // was restored completely. Otherwise do not layer a second
                // mutation over an unknown terrain state.
                return rollbackComplete;
            }
            finally
            {
                _transactionActive = false;
                if (heightmapBackup != null)
                    UnityEngine.Object.Destroy(heightmapBackup);
            }
        }

        internal static void ResetRuntimeState()
        {
            _transactionActive = false;
        }

        internal static bool CanRollbackRuntimeMutationWithoutMutation(
            out string reason)
        {
            var committed = _committedSnapshot;
            if (committed == null)
            {
                reason = null;
                return true;
            }

            if (_transactionActive)
            {
                reason = "an import transaction is still active";
                return false;
            }

            if (committed.TerrainSystem == null ||
                committed.TerrainSystem.World == null ||
                !committed.TerrainSystem.World.IsCreated)
            {
                // Destroyed-World cleanup only releases the owned backup.
                reason = null;
                return true;
            }

            if (committed.Properties == null ||
                committed.HeightmapBackup == null ||
                committed.HeightmapBackup.width <= 0 ||
                committed.HeightmapBackup.height <= 0 ||
                IsValidHeightmapFormatMethod == null ||
                !TerrainPropertySnapshot.AccessorsAvailable)
            {
                reason = "the live worldmap rollback snapshot is incomplete";
                return false;
            }

            try
            {
                var valid = (bool)IsValidHeightmapFormatMethod.Invoke(
                    null,
                    new object[] { committed.HeightmapBackup }
                );
                if (!valid)
                {
                    reason = "the live worldmap backup format is invalid";
                    return false;
                }
            }
            catch (Exception ex)
            {
                reason = "worldmap backup validation failed: " + ex.Message;
                return false;
            }

            reason = null;
            return true;
        }

        internal static bool TryRollbackRuntimeMutation()
        {
            var committed = _committedSnapshot;
            if (committed == null)
                return true;

            if (_transactionActive)
            {
                Util.Log.Error(
                    "[LargeMap] worldmap rollback deferred because an import transaction is active."
                );
                return false;
            }

            if (committed.TerrainSystem == null ||
                committed.TerrainSystem.World == null ||
                !committed.TerrainSystem.World.IsCreated)
            {
                if (!TryReleaseCommittedSnapshot(committed))
                    return false;
                _committedSnapshot = null;
                return true;
            }

            if (!TryRollback(
                    committed.TerrainSystem,
                    committed.HeightmapBackup,
                    committed.Properties))
            {
                return false;
            }

            if (!TryReleaseCommittedSnapshot(committed))
                return false;
            _committedSnapshot = null;
            Util.Log.Info("[LargeMap] primary heightmap restored to its pre-import state.");
            return true;
        }

        private static bool ReleaseCommittedSnapshotFromDestroyedWorld(
            TerrainSystem currentTerrainSystem)
        {
            var committed = _committedSnapshot;
            if (committed == null ||
                ReferenceEquals(committed.TerrainSystem, currentTerrainSystem))
            {
                return true;
            }

            if (committed.TerrainSystem != null &&
                committed.TerrainSystem.World != null &&
                committed.TerrainSystem.World.IsCreated)
            {
                Util.Log.Error(
                    "[LargeMap] worldmap promotion blocked because a rollback " +
                    "snapshot belongs to another active World."
                );
                return false;
            }

            if (!TryReleaseCommittedSnapshot(committed))
                return false;
            _committedSnapshot = null;
            return true;
        }

        private static bool TryReleaseCommittedSnapshot(
            CommittedWorldmapSnapshot committed)
        {
            try
            {
                if (committed != null && committed.HeightmapBackup != null)
                    UnityEngine.Object.Destroy(committed.HeightmapBackup);
                return true;
            }
            catch (Exception ex)
            {
                Util.Log.Error(
                    "[LargeMap] worldmap snapshot release failed: " + ex
                );
                return false;
            }
        }

        private static bool TryRollback(
            TerrainSystem terrainSystem,
            Texture2D heightmapBackup,
            TerrainPropertySnapshot snapshot)
        {
            var resumeLargeMap = CityTimelineLargeMapState.Enabled;

            try
            {
                if (terrainSystem == null || snapshot == null ||
                    heightmapBackup == null ||
                    heightmapBackup.width <= 0 || heightmapBackup.height <= 0)
                {
                    return false;
                }

                // Use the game's authoritative path so the GPU texture,
                // m_CPUHeights and WaterSystem notifications are restored as
                // one operation. A Graphics.Blit-only rollback is insufficient.
                CityTimelineLargeMapState.Disable();
                terrainSystem.ReplaceHeightmap(heightmapBackup);
                snapshot.Restore(terrainSystem);
                terrainSystem.SetTerrainProperties(snapshot.HeightScaleOffset);

                if (!snapshot.Matches(terrainSystem))
                {
                    throw new InvalidOperationException(
                        "TerrainSystem properties do not match the pre-import snapshot."
                    );
                }

                if (resumeLargeMap)
                    CityTimelineLargeMapState.Enable();

                return true;
            }
            catch (Exception rollbackException)
            {
                Util.Log.Error(
                    "[LargeMap] worldmap promotion rollback failed: " +
                    rollbackException
                );
                return false;
            }
        }

        private static Texture2D CreateReadableHeightmapBackup(
            TerrainSystem terrainSystem,
            RenderTexture source)
        {
            if (terrainSystem == null || source == null || !source.IsCreated())
                throw new InvalidOperationException("Primary heightmap is unavailable.");

            var previousActive = RenderTexture.active;
            Texture2D backup = null;

            try
            {
                backup = new Texture2D(
                    source.width,
                    source.height,
                    TextureFormat.R16,
                    false,
                    true
                );
                backup.name = "CityTimelineMod_PreImportHeightmap";
                backup.hideFlags = HideFlags.HideAndDontSave;

                RenderTexture.active = source;
                backup.ReadPixels(
                    new Rect(0f, 0f, source.width, source.height),
                    0,
                    0,
                    false
                );
                backup.Apply(false, false);

                bool valid = (bool)IsValidHeightmapFormatMethod.Invoke(
                    null,
                    new object[] { backup }
                );
                if (!valid)
                {
                    throw new InvalidOperationException(
                        "The readable pre-import heightmap does not satisfy TerrainSystem."
                    );
                }

                return backup;
            }
            catch
            {
                if (backup != null)
                    UnityEngine.Object.Destroy(backup);
                throw;
            }
            finally
            {
                RenderTexture.active = previousActive;
            }
        }

        private static bool Approximately(float a, float b)
        {
            return Mathf.Abs(a - b) < 0.01f;
        }

        private sealed class TerrainPropertySnapshot
        {
            private static readonly FieldInfo HeightScaleOffsetField =
                AccessTools.Field(typeof(TerrainSystem), "<heightScaleOffset>k__BackingField");
            private static readonly FieldInfo PlayableAreaField =
                AccessTools.Field(typeof(TerrainSystem), "<playableArea>k__BackingField");
            private static readonly FieldInfo PlayableOffsetField =
                AccessTools.Field(typeof(TerrainSystem), "<playableOffset>k__BackingField");
            private static readonly FieldInfo WorldSizeField =
                AccessTools.Field(typeof(TerrainSystem), "<worldSize>k__BackingField");
            private static readonly FieldInfo WorldOffsetField =
                AccessTools.Field(typeof(TerrainSystem), "<worldOffset>k__BackingField");
            private static readonly FieldInfo WorldHeightMinMaxField =
                AccessTools.Field(typeof(TerrainSystem), "<worldHeightMinMax>k__BackingField");
            private static readonly FieldInfo BaseLodField =
                AccessTools.Field(typeof(TerrainSystem), "<baseLod>k__BackingField");

            internal static bool AccessorsAvailable =>
                HeightScaleOffsetField != null &&
                PlayableAreaField != null &&
                PlayableOffsetField != null &&
                WorldSizeField != null &&
                WorldOffsetField != null &&
                WorldHeightMinMaxField != null &&
                BaseLodField != null;

            internal readonly Unity.Mathematics.float2 HeightScaleOffset;
            private readonly Unity.Mathematics.float2 _playableArea;
            private readonly Unity.Mathematics.float2 _playableOffset;
            private readonly Unity.Mathematics.float2 _worldSize;
            private readonly Unity.Mathematics.float2 _worldOffset;
            private readonly Unity.Mathematics.float2 _worldHeightMinMax;
            private readonly int _baseLod;

            internal TerrainPropertySnapshot(TerrainSystem terrainSystem)
            {
                if (!AccessorsAvailable)
                    throw new MissingFieldException("TerrainSystem rollback backing fields");

                HeightScaleOffset = ReadFloat2(HeightScaleOffsetField, terrainSystem);
                _playableArea = ReadFloat2(PlayableAreaField, terrainSystem);
                _playableOffset = ReadFloat2(PlayableOffsetField, terrainSystem);
                _worldSize = ReadFloat2(WorldSizeField, terrainSystem);
                _worldOffset = ReadFloat2(WorldOffsetField, terrainSystem);
                _worldHeightMinMax = ReadFloat2(WorldHeightMinMaxField, terrainSystem);
                _baseLod = (int)BaseLodField.GetValue(null);
            }

            internal void Restore(TerrainSystem terrainSystem)
            {
                HeightScaleOffsetField.SetValue(terrainSystem, HeightScaleOffset);
                PlayableAreaField.SetValue(terrainSystem, _playableArea);
                PlayableOffsetField.SetValue(terrainSystem, _playableOffset);
                WorldSizeField.SetValue(terrainSystem, _worldSize);
                WorldOffsetField.SetValue(terrainSystem, _worldOffset);
                WorldHeightMinMaxField.SetValue(terrainSystem, _worldHeightMinMax);
                BaseLodField.SetValue(null, _baseLod);
            }

            internal bool Matches(TerrainSystem terrainSystem)
            {
                return
                    Approximately(terrainSystem.heightScaleOffset, HeightScaleOffset) &&
                    Approximately(terrainSystem.playableArea, _playableArea) &&
                    Approximately(terrainSystem.playableOffset, _playableOffset) &&
                    Approximately(terrainSystem.worldSize, _worldSize) &&
                    Approximately(terrainSystem.worldOffset, _worldOffset) &&
                    Approximately(terrainSystem.worldHeightMinMax, _worldHeightMinMax) &&
                    (int)BaseLodField.GetValue(null) == _baseLod;
            }

            private static Unity.Mathematics.float2 ReadFloat2(
                FieldInfo field,
                TerrainSystem terrainSystem)
            {
                return (Unity.Mathematics.float2)field.GetValue(terrainSystem);
            }

            private static bool Approximately(
                Unity.Mathematics.float2 a,
                Unity.Mathematics.float2 b)
            {
                return
                    Mathf.Abs(a.x - b.x) < 0.01f &&
                    Mathf.Abs(a.y - b.y) < 0.01f;
            }
        }

        private sealed class CommittedWorldmapSnapshot
        {
            internal readonly TerrainSystem TerrainSystem;
            internal readonly Texture2D HeightmapBackup;
            internal readonly TerrainPropertySnapshot Properties;

            internal CommittedWorldmapSnapshot(
                TerrainSystem terrainSystem,
                Texture2D heightmapBackup,
                TerrainPropertySnapshot properties)
            {
                TerrainSystem = terrainSystem;
                HeightmapBackup = heightmapBackup;
                Properties = properties;
            }
        }
    }

    /*
     * Le jeu crée 23 x 23 tuiles couvrant 14,336 km. On conserve le nombre
     * officiel de tuiles et on multiplie leurs coordonnées par quatre :
     * même topologie, emprise totale de 57,344 km, sauvegarde inchangée.
     */
    [HarmonyPatch(typeof(MapTileSystem), "LegacyGenerateMapTiles")]
    internal static class LargeMapTileScalePatch
    {
        private const int ExpectedTileCount = 23 * 23;
        private const int ExpectedNodesPerTile = 4;
        private const int ExpectedCoordinateCount = 24;
        private const float VanillaHalfExtent =
            CityTimelineLargeMapState.OriginalMapSizeMetersFloat * 0.5f;
        private const float CoordinateTolerance = 2f;

        private static readonly Dictionary<ulong, TileGridSnapshot>
            SnapshotsByWorldSequence =
                new Dictionary<ulong, TileGridSnapshot>();

        [HarmonyPostfix]
        private static void Postfix(MapTileSystem __instance)
        {
            if (!CityTimelineLargeMapState.Enabled)
                return;

            EntityManager entityManager = __instance.World.EntityManager;
            EntityQuery query = default(EntityQuery);
            bool queryCreated = false;

            try
            {
                query = entityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<MapTile>(),
                    ComponentType.ReadWrite<Node>()
                );
                queryCreated = true;

                using (NativeArray<Entity> entities =
                    query.ToEntityArray(Allocator.Temp))
                {
                    if (entities.Length != ExpectedTileCount)
                    {
                        throw new InvalidOperationException(
                            "Expected " + ExpectedTileCount +
                            " MapTile entities, found=" + entities.Length + "."
                        );
                    }

                    var entityCopies = new Entity[entities.Length];
                    var originalNodes = new Node[entities.Length][];
                    var distinctX = new HashSet<int>();
                    var distinctZ = new HashSet<int>();
                    float maxAbs = 0f;
                    float minX = float.MaxValue;
                    float maxX = float.MinValue;
                    float minZ = float.MaxValue;
                    float maxZ = float.MinValue;

                    for (int i = 0; i < entities.Length; i++)
                    {
                        entityCopies[i] = entities[i];
                        DynamicBuffer<Node> nodes =
                            entityManager.GetBuffer<Node>(entities[i]);

                        if (nodes.Length != ExpectedNodesPerTile)
                        {
                            throw new InvalidOperationException(
                                "MapTile " + entities[i] + " has nodeCount=" +
                                nodes.Length + ", expected=" + ExpectedNodesPerTile + "."
                            );
                        }

                        originalNodes[i] = new Node[nodes.Length];

                        for (int j = 0; j < nodes.Length; j++)
                        {
                            Node node = nodes[j];
                            originalNodes[i][j] = node;

                            if (!IsFinite(node.m_Position.x) ||
                                !IsFinite(node.m_Position.z))
                            {
                                throw new InvalidOperationException(
                                    "MapTile grid contains a non-finite node coordinate."
                                );
                            }

                            minX = Math.Min(minX, node.m_Position.x);
                            maxX = Math.Max(maxX, node.m_Position.x);
                            minZ = Math.Min(minZ, node.m_Position.z);
                            maxZ = Math.Max(maxZ, node.m_Position.z);
                            distinctX.Add(Quantize(node.m_Position.x));
                            distinctZ.Add(Quantize(node.m_Position.z));
                            maxAbs = Math.Max(
                                maxAbs,
                                Math.Max(
                                    Math.Abs(node.m_Position.x),
                                    Math.Abs(node.m_Position.z)
                                )
                            );
                        }
                    }

                    if (distinctX.Count != ExpectedCoordinateCount ||
                        distinctZ.Count != ExpectedCoordinateCount)
                    {
                        throw new InvalidOperationException(
                            "MapTile topology mismatch. distinctX=" + distinctX.Count +
                            ", distinctZ=" + distinctZ.Count + "."
                        );
                    }

                    bool vanillaGrid =
                        Approximately(minX, -VanillaHalfExtent) &&
                        Approximately(maxX, VanillaHalfExtent) &&
                        Approximately(minZ, -VanillaHalfExtent) &&
                        Approximately(maxZ, VanillaHalfExtent);

                    bool extendedGrid =
                        Approximately(minX, -CityTimelineLargeMapState.HalfMapSizeMetersFloat) &&
                        Approximately(maxX, CityTimelineLargeMapState.HalfMapSizeMetersFloat) &&
                        Approximately(minZ, -CityTimelineLargeMapState.HalfMapSizeMetersFloat) &&
                        Approximately(maxZ, CityTimelineLargeMapState.HalfMapSizeMetersFloat);

                    if (!vanillaGrid && !extendedGrid)
                    {
                        throw new InvalidOperationException(
                            "MapTile extent is neither exact vanilla nor CTM 57 km. " +
                            "minX=" + minX + ", maxX=" + maxX +
                            ", minZ=" + minZ + ", maxZ=" + maxZ +
                            ", maxAbs=" + maxAbs + "."
                        );
                    }

                    var rollbackNodes = CloneNodes(originalNodes);
                    if (extendedGrid)
                        ScaleNodes(rollbackNodes, 1f / CityTimelineLargeMapState.CoreValue);

                    var worldSequence = __instance.World.SequenceNumber;
                    if (SnapshotsByWorldSequence.TryGetValue(
                            worldSequence,
                            out var existingSnapshot))
                    {
                        if (!ReferenceEquals(existingSnapshot.World, __instance.World))
                        {
                            throw new InvalidOperationException(
                                "MapTile snapshot World sequence collision=" +
                                worldSequence + "."
                            );
                        }

                        if (extendedGrid)
                        {
                            Util.Log.Info(
                                "[LargeMap] MapTile grid already extended and tracked for " +
                                "World sequence=" + worldSequence + "."
                            );
                            return;
                        }

                        // Vanilla regenerated the complete exact grid in this
                        // World, so the previous extended mutation no longer
                        // exists and a fresh snapshot can replace it.
                        SnapshotsByWorldSequence.Remove(worldSequence);
                    }

                    SnapshotsByWorldSequence.Add(
                        worldSequence,
                        new TileGridSnapshot(
                        __instance.World,
                        worldSequence,
                        entityCopies,
                        rollbackNodes
                        )
                    );

                    if (extendedGrid)
                    {
                        Util.Log.Info(
                            "[LargeMap] exact 23x23 MapTile grid already covers 57 km; " +
                            "rollback snapshot reconstructed."
                        );
                        return;
                    }

                    int changedNodes = 0;
                    try
                    {
                        for (int i = 0; i < entities.Length; i++)
                        {
                            DynamicBuffer<Node> nodes =
                                entityManager.GetBuffer<Node>(entities[i]);

                            for (int j = 0; j < nodes.Length; j++)
                            {
                                Node node = nodes[j];
                                node.m_Position.x *= CityTimelineLargeMapState.CoreValue;
                                node.m_Position.z *= CityTimelineLargeMapState.CoreValue;
                                nodes[j] = node;
                                changedNodes++;
                            }
                        }
                    }
                    catch
                    {
                        TryRollbackRuntimeMutations();
                        throw;
                    }

                    Util.Log.Info(
                        "[LargeMap] map-tile grid scaled to 57 km. " +
                        "tiles=" + entities.Length +
                        ", nodes=" + changedNodes
                    );
                }
            }
            catch (Exception ex)
            {
                CityTimelineLargeMapState.FailRuntime(
                    "MapTile 57 km scaling or validation failed"
                );
                Util.Log.Error(
                    "[LargeMap] map-tile scaling failed: " + ex
                );
            }
            finally
            {
                if (queryCreated)
                    query.Dispose();
            }
        }

        internal static bool TryRollbackRuntimeMutations()
        {
            if (SnapshotsByWorldSequence.Count == 0)
                return true;

            var snapshots = new List<TileGridSnapshot>(
                SnapshotsByWorldSequence.Values
            );
            var failed = 0;

            for (var snapshotIndex = 0;
                 snapshotIndex < snapshots.Count;
                 snapshotIndex++)
            {
                var snapshot = snapshots[snapshotIndex];

                if (snapshot.World == null || !snapshot.World.IsCreated)
                {
                    SnapshotsByWorldSequence.Remove(snapshot.WorldSequence);
                    continue;
                }

                var snapshotFailed = false;

                for (var i = 0; i < snapshot.Entities.Length; i++)
                {
                    try
                    {
                    var entityManager = snapshot.World.EntityManager;
                    var entity = snapshot.Entities[i];
                    if (!entityManager.Exists(entity) ||
                        !entityManager.HasBuffer<Node>(entity))
                    {
                        failed++;
                        snapshotFailed = true;
                        Util.Log.Error(
                            "[LargeMap] MapTile rollback target is missing. World=" +
                            snapshot.WorldSequence + ", index=" + i + "."
                        );
                        continue;
                    }

                    var nodes = entityManager.GetBuffer<Node>(entity);
                    var savedNodes = snapshot.Nodes[i];
                    if (nodes.Length != savedNodes.Length)
                    {
                        failed++;
                        snapshotFailed = true;
                        continue;
                    }

                    for (var j = 0; j < savedNodes.Length; j++)
                        nodes[j] = savedNodes[j];
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        snapshotFailed = true;
                        Util.Log.Error(
                            "[LargeMap] MapTile rollback failed for World=" +
                            snapshot.WorldSequence + ", index=" + i + ". " + ex
                        );
                    }
                }

                if (!snapshotFailed)
                    SnapshotsByWorldSequence.Remove(snapshot.WorldSequence);
            }

            if (failed != 0 || SnapshotsByWorldSequence.Count != 0)
            {
                Util.Log.Error(
                    "[LargeMap] MapTile rollback incomplete. failedTiles=" + failed +
                    ", pendingWorlds=" + SnapshotsByWorldSequence.Count + "."
                );
                return false;
            }

            Util.Log.Info("[LargeMap] MapTile runtime coordinates restored.");
            return true;
        }

        internal static bool CanRollbackRuntimeMutationsWithoutMutation(
            out string reason)
        {
            try
            {
                foreach (var snapshot in SnapshotsByWorldSequence.Values)
                {
                    if (snapshot == null || snapshot.World == null ||
                        !snapshot.World.IsCreated)
                    {
                        continue;
                    }

                    if (snapshot.Entities == null || snapshot.Nodes == null ||
                        snapshot.Entities.Length != snapshot.Nodes.Length)
                    {
                        reason =
                            "live World " + snapshot.WorldSequence +
                            " has an incomplete tile snapshot";
                        return false;
                    }

                    var entityManager = snapshot.World.EntityManager;
                    for (var i = 0; i < snapshot.Entities.Length; i++)
                    {
                        var entity = snapshot.Entities[i];
                        if (snapshot.Nodes[i] == null ||
                            !entityManager.Exists(entity) ||
                            !entityManager.HasBuffer<Node>(entity))
                        {
                            reason =
                                "live World " + snapshot.WorldSequence +
                                " is missing MapTile rollback target " + i;
                            return false;
                        }

                        var nodes = entityManager.GetBuffer<Node>(entity);
                        if (nodes.Length != snapshot.Nodes[i].Length)
                        {
                            reason =
                                "live World " + snapshot.WorldSequence +
                                " has a changed MapTile node layout at " + i;
                            return false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                reason = "MapTile rollback preflight failed: " + ex.Message;
                return false;
            }

            reason = null;
            return true;
        }

        internal static void ResetRuntimeState()
        {
            if (SnapshotsByWorldSequence.Count == 0)
                return;

            if (!TryRollbackRuntimeMutations())
            {
                Util.Log.Error(
                    "[LargeMap] MapTile reset retained its snapshot for a later rollback retry."
                );
            }
        }

        private static Node[][] CloneNodes(Node[][] source)
        {
            var clone = new Node[source.Length][];
            for (var i = 0; i < source.Length; i++)
            {
                clone[i] = new Node[source[i].Length];
                Array.Copy(source[i], clone[i], source[i].Length);
            }
            return clone;
        }

        private static void ScaleNodes(Node[][] nodes, float multiplier)
        {
            for (var i = 0; i < nodes.Length; i++)
            {
                for (var j = 0; j < nodes[i].Length; j++)
                {
                    var node = nodes[i][j];
                    node.m_Position.x *= multiplier;
                    node.m_Position.z *= multiplier;
                    nodes[i][j] = node;
                }
            }
        }

        private static int Quantize(float value)
        {
            return (int)Math.Round(value * 100f);
        }

        private static bool Approximately(float a, float b)
        {
            return Math.Abs(a - b) <= CoordinateTolerance;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private sealed class TileGridSnapshot
        {
            internal readonly World World;
            internal readonly ulong WorldSequence;
            internal readonly Entity[] Entities;
            internal readonly Node[][] Nodes;

            internal TileGridSnapshot(
                World world,
                ulong worldSequence,
                Entity[] entities,
                Node[][] nodes)
            {
                World = world;
                WorldSequence = worldSequence;
                Entities = entities;
                Nodes = nodes;
            }
        }
    }

    /*
     * AirwaySystem peut être créé avant l'installation des patches du mod.
     *
     * Le transpiler OnCreate couvre les créations futures.
     * Le Postfix OnUpdate corrige aussi une instance déjà créée en modifiant
     * directement les deux AirwayMap existantes, sans recréer leurs NativeArray.
     */
    [HarmonyPatch(typeof(Game.Net.AirwaySystem))]
    internal static class LargeMapAirwayScalePatch
    {
        private const float VanillaHelicopterCellSize = 494.34482f;
        private const float VanillaAirplaneCellSize = 988.68964f;

        private static readonly float TargetHelicopterCellSize =
            VanillaHelicopterCellSize *
            CityTimelineLargeMapState.CoreValue;

        private static readonly float TargetAirplaneCellSize =
            VanillaAirplaneCellSize *
            CityTimelineLargeMapState.CoreValue;

        private static readonly FieldInfo AirwayDataField =
            AccessTools.Field(
                typeof(Game.Net.AirwaySystem),
                "m_AirwayData"
            );

        private static readonly PropertyInfo HelicopterMapProperty =
            AccessTools.Property(
                typeof(Game.Net.AirwayHelpers.AirwayData),
                "helicopterMap"
            );

        private static readonly PropertyInfo AirplaneMapProperty =
            AccessTools.Property(
                typeof(Game.Net.AirwayHelpers.AirwayData),
                "airplaneMap"
            );

        private static readonly FieldInfo CellSizeField =
            AccessTools.Field(
                typeof(Game.Net.AirwayHelpers.AirwayMap),
                "m_CellSize"
            );

        private static readonly MethodInfo EffectiveHelicopterCellSizeMethod =
            AccessTools.Method(
                typeof(LargeMapAirwayScalePatch),
                nameof(GetEffectiveHelicopterCellSize)
            );

        private static readonly MethodInfo EffectiveAirplaneCellSizeMethod =
            AccessTools.Method(
                typeof(LargeMapAirwayScalePatch),
                nameof(GetEffectiveAirplaneCellSize)
            );

        private static bool _unexpectedScaleLogged;
        private static bool _runtimeFailureLogged;
        private static readonly Dictionary<Game.Net.AirwaySystem, AirwaySnapshot>
            RuntimeSnapshots =
                new Dictionary<Game.Net.AirwaySystem, AirwaySnapshot>();

        private static readonly HashSet<Game.Net.AirwaySystem>
            RuntimeScaledInstances =
                new HashSet<Game.Net.AirwaySystem>();

        [HarmonyPrepare]
        private static bool Prepare()
        {
            if (AirwayDataField == null)
            {
                throw new MissingFieldException(
                    "Game.Net.AirwaySystem.m_AirwayData"
                );
            }

            if (HelicopterMapProperty == null)
            {
                throw new MissingMemberException(
                    "Game.Net.AirwayHelpers.AirwayData.helicopterMap"
                );
            }

            if (AirplaneMapProperty == null)
            {
                throw new MissingMemberException(
                    "Game.Net.AirwayHelpers.AirwayData.airplaneMap"
                );
            }

            if (CellSizeField == null)
            {
                throw new MissingFieldException(
                    "Game.Net.AirwayHelpers.AirwayMap.m_CellSize"
                );
            }

            if (EffectiveHelicopterCellSizeMethod == null ||
                EffectiveAirplaneCellSizeMethod == null)
            {
                throw new MissingMethodException(
                    "LargeMap Airway effective cell-size accessors"
                );
            }

            return true;
        }

        /*
         * Cas où AirwaySystem est créé après l'installation Harmony.
         */
        [HarmonyPatch("OnCreate")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> OnCreateTranspiler(
            IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            int patches = 0;

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode != OpCodes.Ldc_R4 ||
                    !(codes[i].operand is float))
                {
                    continue;
                }

                float value = (float)codes[i].operand;

                if (Math.Abs(
                        value -
                        VanillaHelicopterCellSize
                    ) < 0.01f)
                {
                    codes[i].opcode = OpCodes.Call;
                    codes[i].operand = EffectiveHelicopterCellSizeMethod;

                    patches++;
                    continue;
                }

                if (Math.Abs(
                        value -
                        VanillaAirplaneCellSize
                    ) < 0.01f)
                {
                    codes[i].opcode = OpCodes.Call;
                    codes[i].operand = EffectiveAirplaneCellSizeMethod;

                    patches++;
                }
            }

            if (patches != 2)
            {
                throw new InvalidOperationException(
                    "[LargeMap] AirwaySystem.OnCreate expected 2 " +
                    "cell-size constants, found=" + patches +
                    ". Game.dll changed."
                );
            }

            Util.Log.Info(
                "[LargeMap] AirwaySystem OnCreate grid patch ready. " +
                "constants=" + patches
            );

            return codes;
        }

        [HarmonyPatch("OnCreate")]
        [HarmonyPostfix]
        private static void OnCreatePostfix(
            Game.Net.AirwaySystem __instance)
        {
            // The transpiler can scale a newly created instance before its first
            // OnUpdate. Capture the known vanilla rollback values immediately so
            // no dispose/hot-reload window can leave that mutation untracked.
            OnUpdatePrefix(__instance);
        }

        /*
         * Cas réel rencontré dans l'éditeur :
         * AirwaySystem existe déjà avec les tailles vanilla.
         *
         * AirwayData et AirwayMap sont des structs. Ils doivent donc être
         * modifiés dans leurs objets boxés, puis réinjectés successivement :
         *
         * AirwayMap boxée -> propriété AirwayData boxée -> champ AirwaySystem.
         */
        [HarmonyPatch("OnUpdate")]
        [HarmonyPrefix]
        private static void OnUpdatePrefix(
            Game.Net.AirwaySystem __instance)
        {
            if (!CityTimelineLargeMapState.Enabled)
                return;

            // AirwayMap and AirwayData are structs. Reflection boxes both on
            // every call, so repeating this probe from AirwaySystem.OnUpdate
            // creates continuous garbage and periodic GC hitches.  Scale (or
            // validate) each concrete system instance once.
            if (RuntimeScaledInstances.Contains(__instance))
                return;

            var mutationCommitted = false;
            var snapshotAdded = false;

            try
            {
                object airwayDataBox =
                    AirwayDataField.GetValue(__instance);

                if (airwayDataBox == null)
                    return;

                object helicopterMapBox =
                    HelicopterMapProperty.GetValue(
                        airwayDataBox,
                        null
                    );

                object airplaneMapBox =
                    AirplaneMapProperty.GetValue(
                        airwayDataBox,
                        null
                    );

                if (helicopterMapBox == null ||
                    airplaneMapBox == null)
                {
                    return;
                }

                float currentHelicopterCellSize =
                    (float)CellSizeField.GetValue(
                        helicopterMapBox
                    );

                float currentAirplaneCellSize =
                    (float)CellSizeField.GetValue(
                        airplaneMapBox
                    );

                bool helicopterAlreadyScaled =
                    Mathf.Approximately(
                        currentHelicopterCellSize,
                        TargetHelicopterCellSize
                    );

                bool airplaneAlreadyScaled =
                    Mathf.Approximately(
                        currentAirplaneCellSize,
                        TargetAirplaneCellSize
                    );

                bool helicopterVanilla =
                    Mathf.Approximately(
                        currentHelicopterCellSize,
                        VanillaHelicopterCellSize
                    );

                bool airplaneVanilla =
                    Mathf.Approximately(
                        currentAirplaneCellSize,
                        VanillaAirplaneCellSize
                    );

                if (helicopterAlreadyScaled &&
                    airplaneAlreadyScaled)
                {
                    // A previous CTM instance may have scaled this World before
                    // hot reload. Reconstruct the known vanilla rollback values.
                    if (!RuntimeSnapshots.ContainsKey(__instance))
                    {
                        RuntimeSnapshots.Add(
                            __instance,
                            new AirwaySnapshot(
                                __instance,
                                VanillaHelicopterCellSize,
                                VanillaAirplaneCellSize
                            )
                        );
                    }

                    RuntimeScaledInstances.Add(__instance);
                    return;
                }

                /*
                 * Une baseline cohérente est soit entièrement vanilla, soit
                 * entièrement CTM. Un couple mixte prouve qu'une mutation
                 * partielle a déjà eu lieu : ne pas l'enregistrer comme état
                 * rollbackable et ne rien modifier davantage.
                 */
                if (!helicopterVanilla || !airplaneVanilla)
                {
                    if (!_unexpectedScaleLogged)
                    {
                        _unexpectedScaleLogged = true;

                        Util.Log.Error(
                            "[LargeMap] unexpected AirwaySystem cell sizes; " +
                            "expected a vanilla/vanilla or CTM/CTM pair; " +
                            "runtime scaling cancelled. helicopter=" +
                            currentHelicopterCellSize +
                            ", airplane=" +
                            currentAirplaneCellSize
                        );
                    }

                    CityTimelineLargeMapState.FailRuntime(
                        "AirwaySystem exposed an incompatible or partially " +
                        "scaled cell-size pair"
                    );
                    return;
                }

                if (!RuntimeSnapshots.ContainsKey(__instance))
                {
                    RuntimeSnapshots.Add(
                        __instance,
                        new AirwaySnapshot(
                            __instance,
                            currentHelicopterCellSize,
                            currentAirplaneCellSize
                        )
                    );
                    snapshotAdded = true;
                }

                CellSizeField.SetValue(
                    helicopterMapBox,
                    TargetHelicopterCellSize
                );

                CellSizeField.SetValue(
                    airplaneMapBox,
                    TargetAirplaneCellSize
                );

                HelicopterMapProperty.SetValue(
                    airwayDataBox,
                    helicopterMapBox,
                    null
                );

                AirplaneMapProperty.SetValue(
                    airwayDataBox,
                    airplaneMapBox,
                    null
                );

                AirwayDataField.SetValue(
                    __instance,
                    airwayDataBox
                );

                mutationCommitted = true;
                RuntimeScaledInstances.Add(__instance);
                _unexpectedScaleLogged = false;
                _runtimeFailureLogged = false;

                Util.Log.Info(
                    "[LargeMap] AirwaySystem runtime grid scaled. " +
                    "helicopterCell=" +
                    TargetHelicopterCellSize +
                    ", airplaneCell=" +
                    TargetAirplaneCellSize
                );
            }
            catch (Exception ex)
            {
                if (snapshotAdded && !mutationCommitted)
                    RuntimeSnapshots.Remove(__instance);

                CityTimelineLargeMapState.FailRuntime(
                    "AirwaySystem runtime scaling failed"
                );

                if (_runtimeFailureLogged)
                    return;

                _runtimeFailureLogged = true;

                Util.Log.Error(
                    "[LargeMap] AirwaySystem runtime scaling failed: " +
                    ex
                );
            }
        }

        internal static bool TryRollbackRuntimeMutations()
        {
            var snapshots = new List<AirwaySnapshot>(RuntimeSnapshots.Values);
            var failed = 0;

            for (var i = 0; i < snapshots.Count; i++)
            {
                var snapshot = snapshots[i];
                try
                {
                    if (snapshot.Instance == null)
                    {
                        failed++;
                        continue;
                    }

                    if (snapshot.Instance.World == null ||
                        !snapshot.Instance.World.IsCreated)
                    {
                        RuntimeSnapshots.Remove(snapshot.Instance);
                        RuntimeScaledInstances.Remove(snapshot.Instance);
                        continue;
                    }

                    SetRuntimeCellSizes(
                        snapshot.Instance,
                        snapshot.HelicopterCellSize,
                        snapshot.AirplaneCellSize
                    );

                    RuntimeSnapshots.Remove(snapshot.Instance);
                    RuntimeScaledInstances.Remove(snapshot.Instance);
                }
                catch (Exception ex)
                {
                    failed++;
                    Util.Log.Error(
                        "[LargeMap] Airway runtime rollback failed. " + ex
                    );
                }
            }

            if (failed != 0 || RuntimeSnapshots.Count != 0)
                return false;

            RuntimeScaledInstances.Clear();
            _unexpectedScaleLogged = false;
            _runtimeFailureLogged = false;
            Util.Log.Info("[LargeMap] Airway runtime cell sizes restored.");
            return true;
        }

        internal static bool CanRollbackRuntimeMutationsWithoutMutation(
            out string reason)
        {
            try
            {
                foreach (var snapshot in RuntimeSnapshots.Values)
                {
                    if (snapshot == null || snapshot.Instance == null)
                    {
                        reason = "an Airway rollback snapshot has no system instance";
                        return false;
                    }

                    if (snapshot.Instance.World == null ||
                        !snapshot.Instance.World.IsCreated)
                    {
                        continue;
                    }

                    object airwayDataBox = AirwayDataField.GetValue(
                        snapshot.Instance
                    );
                    if (airwayDataBox == null ||
                        HelicopterMapProperty.GetValue(airwayDataBox, null) == null ||
                        AirplaneMapProperty.GetValue(airwayDataBox, null) == null)
                    {
                        reason = "a live Airway rollback target is unavailable";
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                reason = "Airway rollback preflight failed: " + ex.Message;
                return false;
            }

            reason = null;
            return true;
        }

        internal static void ResetRuntimeState()
        {
            if (!TryRollbackRuntimeMutations())
            {
                Util.Log.Error(
                    "[LargeMap] Airway reset retained snapshots for a later rollback retry."
                );
            }
        }

        internal static float GetEffectiveHelicopterCellSize()
        {
            return CityTimelineLargeMapState.PreserveExtendedRuntimeLayout
                ? TargetHelicopterCellSize
                : VanillaHelicopterCellSize;
        }

        internal static float GetEffectiveAirplaneCellSize()
        {
            return CityTimelineLargeMapState.PreserveExtendedRuntimeLayout
                ? TargetAirplaneCellSize
                : VanillaAirplaneCellSize;
        }

        private static void SetRuntimeCellSizes(
            Game.Net.AirwaySystem instance,
            float helicopterCellSize,
            float airplaneCellSize)
        {
            object airwayDataBox = AirwayDataField.GetValue(instance);
            if (airwayDataBox == null)
                throw new InvalidOperationException("AirwayData is unavailable.");

            object helicopterMapBox = HelicopterMapProperty.GetValue(
                airwayDataBox,
                null
            );
            object airplaneMapBox = AirplaneMapProperty.GetValue(
                airwayDataBox,
                null
            );

            if (helicopterMapBox == null || airplaneMapBox == null)
                throw new InvalidOperationException("Airway maps are unavailable.");

            CellSizeField.SetValue(helicopterMapBox, helicopterCellSize);
            CellSizeField.SetValue(airplaneMapBox, airplaneCellSize);
            HelicopterMapProperty.SetValue(
                airwayDataBox,
                helicopterMapBox,
                null
            );
            AirplaneMapProperty.SetValue(
                airwayDataBox,
                airplaneMapBox,
                null
            );
            AirwayDataField.SetValue(instance, airwayDataBox);

            object verifiedDataBox = AirwayDataField.GetValue(instance);
            object verifiedHelicopterMapBox = HelicopterMapProperty.GetValue(
                verifiedDataBox,
                null
            );
            object verifiedAirplaneMapBox = AirplaneMapProperty.GetValue(
                verifiedDataBox,
                null
            );

            float verifiedHelicopterCellSize =
                (float)CellSizeField.GetValue(verifiedHelicopterMapBox);
            float verifiedAirplaneCellSize =
                (float)CellSizeField.GetValue(verifiedAirplaneMapBox);

            if (!Mathf.Approximately(
                    verifiedHelicopterCellSize,
                    helicopterCellSize) ||
                !Mathf.Approximately(
                    verifiedAirplaneCellSize,
                    airplaneCellSize))
            {
                throw new InvalidOperationException(
                    "Airway cell-size write-back verification failed."
                );
            }
        }

        private sealed class AirwaySnapshot
        {
            internal readonly Game.Net.AirwaySystem Instance;
            internal readonly float HelicopterCellSize;
            internal readonly float AirplaneCellSize;

            internal AirwaySnapshot(
                Game.Net.AirwaySystem instance,
                float helicopterCellSize,
                float airplaneCellSize)
            {
                Instance = instance;
                HelicopterCellSize = helicopterCellSize;
                AirplaneCellSize = airplaneCellSize;
            }
        }
    }

    /*
     * ToolFeedbackSystem is enabled as soon as a game-mode preload begins.
     * During that window it reads AirwaySystem data, creates coverage searches,
     * and can schedule TargetCheckJob before the deserialized Airway grid has
     * reached its final 57 km scale. Keep the pending managed state intact and
     * defer only OnUpdate until GameManager reports that loading is complete.
     */
    [HarmonyPatch(typeof(Game.Tools.ToolFeedbackSystem))]
    internal static class LargeMapToolFeedbackLoadingPatch
    {
        private static bool _suppressionLogged;

        [HarmonyPatch("OnUpdate")]
        [HarmonyPrefix]
        private static bool OnUpdatePrefix()
        {
            if (!CityTimelineLargeMapState.Enabled)
            {
                _suppressionLogged = false;
                return true;
            }

            var gameManager = Game.SceneFlow.GameManager.instance;
            if (gameManager == null || !gameManager.isGameLoading)
            {
                if (_suppressionLogged)
                {
                    Util.Log.Info(
                        "[LargeMap] ToolFeedbackSystem resumed after the 57 km " +
                        "loading barrier."
                    );
                }

                _suppressionLogged = false;
                return true;
            }

            if (!_suppressionLogged)
            {
                _suppressionLogged = true;
                Util.Log.Info(
                    "[LargeMap] ToolFeedbackSystem deferred while the 57 km " +
                    "Airway grid is stabilizing during LoadGame."
                );
            }

            return false;
        }

        internal static void ResetRuntimeState()
        {
            _suppressionLogged = false;
        }
    }

    /*
     * WaterSystem.kMapSize est static readonly et lu dans plusieurs systèmes.
     * On remplace chaque ldsfld connu par 57344, y compris les clients audio
     * et les jobs dont une version managée reste patchable par Harmony.
     * Aucun champ global n'est muté. Si un World a déjà initialisé son stockage
     * 57 km, l'owner et l'accessor restent toutefois actifs en quarantaine ;
     * UnpatchAll n'est autorisé qu'après destruction de ce World.
     */
    internal static class LargeMapWaterMapSizePatches
    {
        private static readonly HashSet<MethodBase> MethodsToPatch =
            new HashSet<MethodBase>(new MethodComparer());

        private static readonly HashSet<MethodBase> ExcludedBurstMethods =
            new HashSet<MethodBase>(new MethodComparer());

        private static FieldInfo _mapSizeField;
        private static FieldInfo _cellSizeField;

        private static bool _manifestPrepared;
        private static bool _simulationExtensionEnabled;

        private const int ExpectedMapSizeManagedTargetCount = 6;
        private const int ExpectedMapSizeExcludedBurstTargetCount = 3;

        private const int ExpectedCellSizeManagedTargetCount = 14;
        private const int ExpectedCellSizeManagedReferenceCount = 20;
        private const int ExpectedCellSizeExcludedBurstTargetCount = 0;

        private const int ExpectedCombinedManagedTargetCount = 20;
        private const int ExpectedCombinedExcludedBurstTargetCount = 3;

        private static readonly Dictionary<MethodBase, FieldUseCounts>
            ExpectedManagedFieldUses =
                new Dictionary<MethodBase, FieldUseCounts>(
                    new MethodComparer()
                );

        private static readonly HashSet<MethodBase> MapSizeManagedMethods =
            new HashSet<MethodBase>(new MethodComparer());

        private static readonly HashSet<MethodBase> CellSizeManagedMethods =
            new HashSet<MethodBase>(new MethodComparer());

        private static readonly HashSet<MethodBase> MapSizeExcludedBurstMethods =
            new HashSet<MethodBase>(new MethodComparer());

        private static readonly HashSet<MethodBase> CellSizeExcludedBurstMethods =
            new HashSet<MethodBase>(new MethodComparer());

        private static readonly MethodInfo EffectiveMapSizeMethod =
            AccessTools.Method(
                typeof(CityTimelineLargeMapState),
                nameof(CityTimelineLargeMapState.GetEffectiveWaterMapSizeMeters)
            );

        private static readonly MethodInfo EffectiveCellSizeMethod =
            AccessTools.Method(
                typeof(CityTimelineLargeMapState),
                nameof(CityTimelineLargeMapState.GetEffectiveWaterCellSizeMeters)
            );

        internal static IReadOnlyCollection<MethodBase> PrepareManifest()
        {
            CityTimelineLargeMapPatcher.VerifySupportedGameModuleIdentity();

            _manifestPrepared = false;
            _mapSizeField = typeof(WaterSystem).GetField(
                "kMapSize",
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.Static
            );

            _cellSizeField = typeof(WaterSystem).GetField(
                "kCellSize",
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.Static
            );

            if (_mapSizeField == null)
            {
                throw new MissingFieldException(
                    "Game.Simulation.WaterSystem.kMapSize"
                );
            }

            if (_cellSizeField == null)
            {
                throw new MissingFieldException(
                    "Game.Simulation.WaterSystem.kCellSize"
                );
            }

            var rawMapSize = _mapSizeField.GetValue(null);
            if (!(rawMapSize is int) ||
                (int)rawMapSize != CityTimelineLargeMapState.OriginalMapSizeMeters)
            {
                throw new InvalidOperationException(
                    "Game.Simulation.WaterSystem.kMapSize has an unexpected value."
                );
            }

            var rawCellSize = _cellSizeField.GetValue(null);

            if (!(rawCellSize is float) ||
                Math.Abs(
                    (float)rawCellSize -
                    CityTimelineLargeMapState.OriginalWaterCellSizeMeters
                ) > 0.0001f)
            {
                throw new InvalidOperationException(
                    "Game.Simulation.WaterSystem.kCellSize has an unexpected value."
                );
            }

            if (EffectiveMapSizeMethod == null)
            {
                throw new MissingMethodException(
                    typeof(CityTimelineLargeMapState).FullName,
                    nameof(CityTimelineLargeMapState.GetEffectiveWaterMapSizeMeters)
                );
            }

            if (EffectiveCellSizeMethod == null)
            {
                throw new MissingMethodException(
                    typeof(CityTimelineLargeMapState).FullName,
                    nameof(CityTimelineLargeMapState.GetEffectiveWaterCellSizeMeters)
                );
            }

            MethodsToPatch.Clear();
            ExcludedBurstMethods.Clear();

            MapSizeManagedMethods.Clear();
            CellSizeManagedMethods.Clear();
            MapSizeExcludedBurstMethods.Clear();
            CellSizeExcludedBurstMethods.Clear();
            ExpectedManagedFieldUses.Clear();
            _simulationExtensionEnabled = false;
            CityTimelineLargeMapState.SetWaterSimulationExtensionEnabled(false);
            ScanGameAssembly();

            if (MapSizeManagedMethods.Count != ExpectedMapSizeManagedTargetCount)
            {
                throw new InvalidOperationException(
                    "[LargeMap] Water kMapSize managed target manifest mismatch. expected=" +
                    ExpectedMapSizeManagedTargetCount +
                    ", actual=" + MapSizeManagedMethods.Count + "."
                );
            }

            if (MapSizeExcludedBurstMethods.Count !=
                ExpectedMapSizeExcludedBurstTargetCount)
            {
                throw new InvalidOperationException(
                    "[LargeMap] Water kMapSize Burst exclusion manifest mismatch. expected=" +
                    ExpectedMapSizeExcludedBurstTargetCount +
                    ", actual=" + MapSizeExcludedBurstMethods.Count + "."
                );
            }

            if (CellSizeManagedMethods.Count != ExpectedCellSizeManagedTargetCount)
            {
                throw new InvalidOperationException(
                    "[LargeMap] Water kCellSize managed target manifest mismatch. expected=" +
                    ExpectedCellSizeManagedTargetCount +
                    ", actual=" + CellSizeManagedMethods.Count + "."
                );
            }

            if (CellSizeExcludedBurstMethods.Count !=
                ExpectedCellSizeExcludedBurstTargetCount)
            {
                throw new InvalidOperationException(
                    "[LargeMap] Water kCellSize Burst exclusion manifest mismatch. expected=" +
                    ExpectedCellSizeExcludedBurstTargetCount +
                    ", actual=" + CellSizeExcludedBurstMethods.Count + "."
                );
            }

            if (MethodsToPatch.Count != ExpectedCombinedManagedTargetCount)
            {
                throw new InvalidOperationException(
                    "[LargeMap] Water combined managed manifest mismatch. expected=" +
                    ExpectedCombinedManagedTargetCount +
                    ", actual=" + MethodsToPatch.Count + "."
                );
            }

            if (ExcludedBurstMethods.Count !=
                ExpectedCombinedExcludedBurstTargetCount)
            {
                throw new InvalidOperationException(
                    "[LargeMap] Water combined Burst exclusion manifest mismatch. expected=" +
                    ExpectedCombinedExcludedBurstTargetCount +
                    ", actual=" + ExcludedBurstMethods.Count + "."
                );
            }

            var cellSizeReferenceCount = 0;

            foreach (var pair in ExpectedManagedFieldUses)
                cellSizeReferenceCount += pair.Value.CellSizeUses;

            if (cellSizeReferenceCount != ExpectedCellSizeManagedReferenceCount)
            {
                throw new InvalidOperationException(
                    "[LargeMap] Water kCellSize reference manifest mismatch. expected=" +
                    ExpectedCellSizeManagedReferenceCount +
                    ", actual=" + cellSizeReferenceCount + "."
                );
            }

            EnsureNoBurstTargetsInManagedManifest();

            string burstSafetyReason;
_simulationExtensionEnabled =
    LargeMapBurstSafety.CanExtendWaterSimulation(
        ExcludedBurstMethods.Count,
        out burstSafetyReason
    );
            CityTimelineLargeMapState.SetWaterSimulationExtensionEnabled(
                _simulationExtensionEnabled
            );

            _manifestPrepared = true;

            Util.Log.Info(
                "[LargeMap] Water geometry manifest resolved. " +
                "kMapSize=14336 -> 57344, " +
                "kCellSize=7 -> 28 " +
                "(no static field mutation), managedTargets=" +
                MethodsToPatch.Count + ", excludedBurstTargets=" +
                ExcludedBurstMethods.Count +
                ", cellSizeReferences=" +
                ExpectedCellSizeManagedReferenceCount +
                ", simulationExtensionEnabled=" +
                _simulationExtensionEnabled + "."
            );

            if (!_simulationExtensionEnabled)
            {
                Util.Log.Info(
                    "[LargeMap] Water 57 km simulation extension disabled: " +
                    burstSafetyReason + ". Managed Water transpilers are intentionally omitted."
                );
            }

            return _simulationExtensionEnabled
                ? new List<MethodBase>(MethodsToPatch)
                : new List<MethodBase>();
        }

        internal static MethodInfo GetTranspilerMethod()
        {
            return AccessTools.Method(
                typeof(LargeMapWaterMapSizePatches),
                nameof(Transpiler)
            );
        }

        internal static void Apply(Harmony harmony)
        {
            if (harmony == null)
                throw new ArgumentNullException(nameof(harmony));

            if (!_manifestPrepared)
                throw new InvalidOperationException("Water manifest was not prepared.");

            if (!_simulationExtensionEnabled)
            {
                Util.Log.Info(
                    "[LargeMap] Water managed patch application skipped; " +
                    "57 km simulation storage is blocked until ReBurst coverage is complete."
                );
                return;
            }

            HarmonyMethod transpiler = new HarmonyMethod(
                GetTranspilerMethod()
            );

            int patched = 0;

            foreach (MethodBase method in MethodsToPatch)
            {
                try
                {
                    harmony.Patch(method, transpiler: transpiler);
                    patched++;
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        "[LargeMap] Water map-size transpiler failed for " +
                        DescribeMethod(method) + ".",
                        ex
                    );
                }
            }

            Util.Log.Info(
                "[LargeMap] Water map-size patch applied. methods=" +
                MethodsToPatch.Count +
                ", patched=" + patched
            );

            if (patched != MethodsToPatch.Count)
            {
                throw new InvalidOperationException(
                    "[LargeMap] Water map-size patch incomplete. expected=" +
                    MethodsToPatch.Count + ", patched=" + patched + "."
                );
            }
        }

        internal static void ResetManifest()
        {
            MethodsToPatch.Clear();
            ExcludedBurstMethods.Clear();

            MapSizeManagedMethods.Clear();
            CellSizeManagedMethods.Clear();
            MapSizeExcludedBurstMethods.Clear();
            CellSizeExcludedBurstMethods.Clear();
            ExpectedManagedFieldUses.Clear();

            _mapSizeField = null;
            _cellSizeField = null;
            _manifestPrepared = false;
            _simulationExtensionEnabled = false;
            CityTimelineLargeMapState.SetWaterSimulationExtensionEnabled(false);
        }

        private static void ScanGameAssembly()
        {
            Type[] types = typeof(Game.GameSystemBase).Assembly.GetTypes();

            for (int i = 0; i < types.Length; i++)
            {
                Type type = types[i];

                if (type == null || type.Namespace == null)
                    continue;

                if (!type.Namespace.StartsWith(
                        "Game.Simulation",
                        StringComparison.Ordinal) &&
                    !type.Namespace.StartsWith(
                        "Game.Audio",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                ScanDeclaredMethods(type);
            }
        }

        private static void ScanDeclaredMethods(Type type)
        {
            MethodInfo[] methods = type.GetMethods(
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.Instance |
                BindingFlags.Static |
                BindingFlags.DeclaredOnly
            );

            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];

                if (method == null || method.IsAbstract ||
                    method.ContainsGenericParameters)
                {
                    continue;
                }

                RegisterTarget(method);
            }

            ConstructorInfo[] constructors = type.GetConstructors(
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.Instance |
                BindingFlags.Static |
                BindingFlags.DeclaredOnly
            );

            for (int i = 0; i < constructors.Length; i++)
            {
                ConstructorInfo constructor = constructors[i];

                if (constructor == null || constructor.ContainsGenericParameters)
                    continue;

                RegisterTarget(constructor);
            }
        }

        private static void EnsureNoBurstTargetsInManagedManifest()
        {
            foreach (var method in MethodsToPatch)
            {
                if (HasBurstCompileAttribute(method))
                {
                    throw new InvalidOperationException(
                        "[LargeMap] Burst-declared Water target entered the managed Harmony manifest: " +
                        DescribeMethod(method) + "."
                    );
                }

                if (ExcludedBurstMethods.Contains(method))
                {
                    throw new InvalidOperationException(
                        "[LargeMap] Water target is present in both managed and Burst-excluded manifests: " +
                        DescribeMethod(method) + "."
                    );
                }
            }
        }

        private static void RegisterTarget(MethodBase method)
        {
            var mapSizeUses = CountFieldUses(method, _mapSizeField);
            var cellSizeUses = CountFieldUses(method, _cellSizeField);

            if (mapSizeUses == 0 && cellSizeUses == 0)
                return;

            if (HasBurstCompileAttribute(method))
            {
                ExcludedBurstMethods.Add(method);

                if (mapSizeUses > 0)
                    MapSizeExcludedBurstMethods.Add(method);

                if (cellSizeUses > 0)
                    CellSizeExcludedBurstMethods.Add(method);

                return;
            }

            MethodsToPatch.Add(method);

            if (mapSizeUses > 0)
                MapSizeManagedMethods.Add(method);

            if (cellSizeUses > 0)
                CellSizeManagedMethods.Add(method);

            ExpectedManagedFieldUses[method] =
                new FieldUseCounts(mapSizeUses, cellSizeUses);
        }

        private static int CountFieldUses(
            MethodBase method,
            FieldInfo field)
        {
            try
            {
                if (method == null ||
                    field == null ||
                    method.GetMethodBody() == null)
                {
                    return 0;
                }

                var instructions =
                    PatchProcessor.GetOriginalInstructions(method);

                var count = 0;

                for (var i = 0; i < instructions.Count; i++)
                {
                    if (instructions[i].opcode == OpCodes.Ldsfld &&
                        SameField(
                            instructions[i].operand as FieldInfo,
                            field
                        ))
                    {
                        count++;
                    }
                }

                return count;
            }
            catch
            {
                return 0;
            }
        }

        private static IEnumerable<CodeInstruction> Transpiler(
            IEnumerable<CodeInstruction> instructions,
            MethodBase original)
        {
if (!_simulationExtensionEnabled ||
    !LargeMapBurstSafety.WaterSimulation57Safe)
            {
                throw new InvalidOperationException(
                    "[LargeMap] Water transpiler execution refused while " +
                    "the atomic ReBurst gate is closed: " +
                    DescribeMethod(original) + "."
                );
            }

            FieldUseCounts expected;

            if (!ExpectedManagedFieldUses.TryGetValue(
                    original,
                    out expected))
            {
                throw new InvalidOperationException(
                    "[LargeMap] Water transpiler target is absent from the " +
                    "prepared field-use manifest: " +
                    DescribeMethod(original) + "."
                );
            }

            var codes = new List<CodeInstruction>(instructions);

            var mapSizeReplacements = 0;
            var cellSizeReplacements = 0;

            for (var i = 0; i < codes.Count; i++)
            {
                var instruction = codes[i];

                if (instruction.opcode != OpCodes.Ldsfld)
                    continue;

                var field = instruction.operand as FieldInfo;

                if (SameField(field, _mapSizeField))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = EffectiveMapSizeMethod;
                    mapSizeReplacements++;
                    continue;
                }

                if (SameField(field, _cellSizeField))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = EffectiveCellSizeMethod;
                    cellSizeReplacements++;
                }
            }

            if (mapSizeReplacements != expected.MapSizeUses ||
                cellSizeReplacements != expected.CellSizeUses)
            {
                throw new InvalidOperationException(
                    "[LargeMap] Water transpiler replacement mismatch in " +
                    DescribeMethod(original) +
                    ". expectedMapSize=" + expected.MapSizeUses +
                    ", actualMapSize=" + mapSizeReplacements +
                    ", expectedCellSize=" + expected.CellSizeUses +
                    ", actualCellSize=" + cellSizeReplacements + "."
                );
            }

            if (mapSizeReplacements == 0 &&
                cellSizeReplacements == 0)
            {
                throw new InvalidOperationException(
                    "[LargeMap] Water transpiler made no replacement in " +
                    DescribeMethod(original) + "."
                );
            }

            return codes;
        }

        private static bool HasBurstCompileAttribute(MemberInfo member)
        {
            try
            {
                for (MemberInfo current = member;
                     current != null;
                     current = current is Type
                         ? ((Type)current).DeclaringType
                         : current.DeclaringType)
                {
                    var attributes = CustomAttributeData.GetCustomAttributes(current);
                    for (var i = 0; i < attributes.Count; i++)
                    {
                        if (attributes[i].AttributeType.Name == "BurstCompileAttribute")
                            return true;
                    }
                }
            }
            catch
            {
            }

            return false;
        }

        private static bool SameField(FieldInfo a, FieldInfo b)
        {
            if (a == null || b == null)
                return false;

            return a.MetadataToken == b.MetadataToken &&
                   a.Module == b.Module &&
                   a.DeclaringType == b.DeclaringType;
        }

        private static string DescribeMethod(MethodBase method)
        {
            return
                (method.DeclaringType != null
                    ? method.DeclaringType.FullName
                    : "<no-type>") +
                "::" + method.Name;
        }

        private struct FieldUseCounts
        {
            internal readonly int MapSizeUses;
            internal readonly int CellSizeUses;

            internal FieldUseCounts(
                int mapSizeUses,
                int cellSizeUses)
            {
                MapSizeUses = mapSizeUses;
                CellSizeUses = cellSizeUses;
            }
        }

        private sealed class MethodComparer : IEqualityComparer<MethodBase>
        {
            public bool Equals(MethodBase x, MethodBase y)
            {
                if (x == null || y == null)
                    return x == y;

                return x.MetadataToken == y.MetadataToken &&
                       x.Module == y.Module &&
                       x.DeclaringType == y.DeclaringType;
            }

            public int GetHashCode(MethodBase obj)
            {
                if (obj == null)
                    return 0;

                return obj.MetadataToken.GetHashCode() ^
                       (obj.DeclaringType != null
                           ? obj.DeclaringType.GetHashCode()
                           : 0);
            }
        }
    }
}
