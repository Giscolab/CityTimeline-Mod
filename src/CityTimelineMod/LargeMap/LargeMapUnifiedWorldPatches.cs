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
            LargeMapWaterMapSizePatches.Apply(harmony);
        }

        private static void PatchClass(Harmony harmony, Type patchType)
        {
            harmony.CreateClassProcessor(patchType).Patch();
            Util.Log.Info("[LargeMap] patch installed: " + patchType.Name);
        }
    }

    /*
     * En mode terrain unique, l'import officiel « worldmap » devient
     * volontairement un import de heightmap principale. Le backdrop
     * existant est supprimé avant la copie afin de maintenir baseLod=0.
     */
    [HarmonyPatch(typeof(TerrainSystem), nameof(TerrainSystem.ReplaceWorldHeightmap))]
    internal static class UnifiedWorldmapImportPatch
    {
        private static readonly MethodInfo DestroyWorldMapMethod =
            AccessTools.Method(typeof(TerrainSystem), "DestroyWorldMap");

        [HarmonyPrefix]
        private static bool Prefix(TerrainSystem __instance, Texture2D inMap)
        {
            if (!CityTimelineLargeMapState.Enabled)
                return true;

            try
            {
                if (DestroyWorldMapMethod == null)
                {
                    throw new MissingMethodException(
                        "TerrainSystem.DestroyWorldMap"
                    );
                }

                DestroyWorldMapMethod.Invoke(__instance, null);

                if (inMap != null)
                {
                    __instance.ReplaceHeightmap(inMap);

                    Util.Log.Info(
                        "[LargeMap] imported worldmap promoted to the " +
                        "single 57 km primary heightmap."
                    );
                }
                else
                {
                    __instance.SetTerrainProperties(
                        __instance.heightScaleOffset
                    );

                    Util.Log.Info(
                        "[LargeMap] backdrop removed; primary terrain retained."
                    );
                }
            }
            catch (Exception ex)
            {
                Util.Log.Error(
                    "[LargeMap] unified worldmap import failed: " + ex
                );
            }

            // Ne jamais laisser l'implémentation vanilla recréer un backdrop
            // en mode terrain unique.
            return false;
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
                    float maxAbs = 0f;

                    for (int i = 0; i < entities.Length; i++)
                    {
                        DynamicBuffer<Node> nodes =
                            entityManager.GetBuffer<Node>(entities[i]);

                        for (int j = 0; j < nodes.Length; j++)
                        {
                            Node node = nodes[j];
                            maxAbs = Math.Max(
                                maxAbs,
                                Math.Max(
                                    Math.Abs(node.m_Position.x),
                                    Math.Abs(node.m_Position.z)
                                )
                            );
                        }
                    }

                    // Idempotence : une grille déjà étendue atteint environ
                    // 28,672 km depuis le centre et ne doit pas être rescalée.
                    if (maxAbs > 10000f)
                    {
                        Util.Log.Info(
                            "[LargeMap] map tiles already cover the extended world. " +
                            "maxAbs=" + maxAbs
                        );
                        return;
                    }

                    int changedNodes = 0;

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

                    Util.Log.Info(
                        "[LargeMap] map-tile grid scaled to 57 km. " +
                        "tiles=" + entities.Length +
                        ", nodes=" + changedNodes
                    );
                }
            }
            catch (Exception ex)
            {
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
    }

    /*
     * AirwaySystem peut être créé avant l'installation des patches du mod.
     *
     * Le transpiler OnCreate couvre les créations futures.
     * Le Postfix OnUpdate corrige aussi une instance déjà créée en modifiant
     * directement les deux AirwayMap existantes, sans recréer leurs NativeArray.
     *
     * AirOutsideConnectionRepairSystem détectera ensuite le nouveau span et
     * reconstruira les Curve de toutes les lanes aériennes.
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

        private static bool _unexpectedScaleLogged;
        private static bool _runtimeFailureLogged;
        private static Game.Net.AirwaySystem _runtimeScaledInstance;

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
                    codes[i].operand =
                        TargetHelicopterCellSize;

                    patches++;
                    continue;
                }

                if (Math.Abs(
                        value -
                        VanillaAirplaneCellSize
                    ) < 0.01f)
                {
                    codes[i].operand =
                        TargetAirplaneCellSize;

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
        [HarmonyPostfix]
        private static void OnUpdatePostfix(
            Game.Net.AirwaySystem __instance)
        {
            if (!CityTimelineLargeMapState.Enabled)
                return;

            // AirwayMap and AirwayData are structs. Reflection boxes both on
            // every call, so repeating this probe from AirwaySystem.OnUpdate
            // creates continuous garbage and periodic GC hitches.  Scale (or
            // validate) each concrete system instance once.
            if (ReferenceEquals(_runtimeScaledInstance, __instance))
                return;

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

                if (helicopterAlreadyScaled &&
                    airplaneAlreadyScaled)
                {
                    _runtimeScaledInstance = __instance;
                    return;
                }

                bool helicopterKnown =
                    helicopterAlreadyScaled ||
                    Mathf.Approximately(
                        currentHelicopterCellSize,
                        VanillaHelicopterCellSize
                    );

                bool airplaneKnown =
                    airplaneAlreadyScaled ||
                    Mathf.Approximately(
                        currentAirplaneCellSize,
                        VanillaAirplaneCellSize
                    );

                /*
                 * Ne pas écraser silencieusement les dimensions installées
                 * par un autre mod ou par une nouvelle version du jeu.
                 */
                if (!helicopterKnown || !airplaneKnown)
                {
                    if (!_unexpectedScaleLogged)
                    {
                        _unexpectedScaleLogged = true;

                        Util.Log.Error(
                            "[LargeMap] unexpected AirwaySystem cell sizes; " +
                            "runtime scaling cancelled. helicopter=" +
                            currentHelicopterCellSize +
                            ", airplane=" +
                            currentAirplaneCellSize
                        );
                    }

                    return;
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

                _runtimeScaledInstance = __instance;
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
                if (_runtimeFailureLogged)
                    return;

                _runtimeFailureLogged = true;

                Util.Log.Error(
                    "[LargeMap] AirwaySystem runtime scaling failed: " +
                    ex
                );
            }
        }
    }

    /*
     * WaterSystem.kMapSize est static readonly et lu dans plusieurs systèmes.
     * On remplace chaque ldsfld connu par 57344, y compris les clients audio
     * et les jobs dont une version managée reste patchable par Harmony.
     * Aucun champ global n'est muté : UnpatchAll restaure donc intégralement
     * le comportement vanilla en cas d'échec d'installation.
     */
    internal static class LargeMapWaterMapSizePatches
    {
        private static readonly HashSet<MethodBase> MethodsToPatch =
            new HashSet<MethodBase>(new MethodComparer());

        private static FieldInfo _mapSizeField;

        internal static void Apply(Harmony harmony)
        {
            _mapSizeField = typeof(WaterSystem).GetField(
                "kMapSize",
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

            MethodsToPatch.Clear();
            ScanGameAssembly();

            HarmonyMethod transpiler = new HarmonyMethod(
                typeof(LargeMapWaterMapSizePatches),
                nameof(Transpiler)
            );

            int patched = 0;
            int failed = 0;

            foreach (MethodBase method in MethodsToPatch)
            {
                try
                {
                    harmony.Patch(method, transpiler: transpiler);
                    patched++;
                }
                catch (Exception ex)
                {
                    failed++;
                    Util.Log.Error(
                        "[LargeMap] Water map-size transpiler failed: " +
                        DescribeMethod(method) + " error=" +
                        ex.GetType().Name + ": " + ex.Message
                    );
                }
            }

            Util.Log.Info(
                "[LargeMap] Water map-size patch applied. methods=" +
                MethodsToPatch.Count +
                ", patched=" + patched +
                ", failed=" + failed
            );

            if (failed != 0)
            {
                throw new InvalidOperationException(
                    "[LargeMap] Water map-size patch incomplete. failed=" +
                    failed
                );
            }
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

                if (UsesMapSizeField(method))
                    MethodsToPatch.Add(method);
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

                if (UsesMapSizeField(constructor))
                    MethodsToPatch.Add(constructor);
            }
        }

        private static bool UsesMapSizeField(MethodBase method)
        {
            try
            {
                if (method.GetMethodBody() == null)
                    return false;

                var instructions =
                    PatchProcessor.GetOriginalInstructions(method);

                for (int i = 0; i < instructions.Count; i++)
                {
                    if (instructions[i].opcode == OpCodes.Ldsfld &&
                        SameField(
                            instructions[i].operand as FieldInfo,
                            _mapSizeField
                        ))
                    {
                        return true;
                    }
                }
            }
            catch
            {
            }

            return false;
        }

        private static IEnumerable<CodeInstruction> Transpiler(
            IEnumerable<CodeInstruction> instructions)
        {
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Ldsfld &&
                    SameField(instruction.operand as FieldInfo, _mapSizeField))
                {
                    instruction.opcode = OpCodes.Ldc_I4;
                    instruction.operand =
                        CityTimelineLargeMapState.MapSizeMeters;
                }

                yield return instruction;
            }
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
