using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Game;
using Game.Simulation;
using HarmonyLib;

namespace CityTimelineMod.LargeMap
{
    internal static class LargeMapCellMapSystemPatches
    {
        private static readonly Dictionary<FieldInfo, int> ReplacementMap =
            new Dictionary<FieldInfo, int>(new FieldInfoComparer());

        private static readonly HashSet<MethodBase> MethodsToPatch =
            new HashSet<MethodBase>(new MethodComparer());

        private static readonly HashSet<MethodBase> BurstMethods =
            new HashSet<MethodBase>(new MethodComparer());

        private static readonly Type BaseGenericType = typeof(CellMapSystem<>);

        private const int ExpectedFieldCount = 14;
        private const int ExpectedTargetCount = 59;

        private static readonly MethodInfo EffectiveMapSizeMethod =
            AccessTools.Method(
                typeof(CityTimelineLargeMapState),
                nameof(CityTimelineLargeMapState.GetEffectiveCellMapSizeMeters)
            );

        private static bool _manifestPrepared;

        internal static IReadOnlyCollection<MethodBase> PrepareManifest()
        {
            CityTimelineLargeMapPatcher.VerifySupportedGameModuleIdentity();

            ReplacementMap.Clear();
            MethodsToPatch.Clear();
            BurstMethods.Clear();
            _manifestPrepared = false;

            Register<AirPollutionSystem>(4, 1);
            Register<AvailabilityInfoToGridSystem>(4, 1);
            Register<GroundPollutionSystem>(4, 1);
            Register<GroundWaterSystem>(4, 1);
            Register<LandValueSystem>(4, 1);
            Register<NaturalResourceSystem>(4, 1);
            Register<NoisePollutionSystem>(4, 1);
            Register<PopulationToGridSystem>(4, 1);
            Register<SoilWaterSystem>(4, 1);
            Register<TerrainAttractivenessSystem>(4, 1);
            Register<TrafficAmbienceSystem>(4, 1);
            Register<ZoneAmbienceSystem>(4, 1);
            Register<TelecomCoverageSystem>(4, 1);
            Register<WindSystem>(4, 1);

            ScanGameAssemblyForClientMethods();

            if (ReplacementMap.Count != ExpectedFieldCount)
            {
                throw new InvalidOperationException(
                    "[LargeMap] CellMap field manifest mismatch. expected=" +
                    ExpectedFieldCount + ", actual=" + ReplacementMap.Count + "."
                );
            }

            if (MethodsToPatch.Count != ExpectedTargetCount)
            {
                throw new InvalidOperationException(
                    "[LargeMap] CellMap target manifest mismatch. expected=" +
                    ExpectedTargetCount + ", actual=" + MethodsToPatch.Count + "."
                );
            }

            if (EffectiveMapSizeMethod == null)
            {
                throw new MissingMethodException(
                    typeof(CityTimelineLargeMapState).FullName,
                    nameof(CityTimelineLargeMapState.GetEffectiveCellMapSizeMeters)
                );
            }

            _manifestPrepared = true;

            Util.Log.Info(
                "[LargeMap] CellMap manifest resolved. fields=" +
                ReplacementMap.Count + ", targets=" + MethodsToPatch.Count +
                ", burstManagedTargets=" + BurstMethods.Count + "."
            );

            if (BurstMethods.Count != 0)
            {
                Util.Log.Info(
                    "[LargeMap] CellMap Harmony manifest includes " +
                    BurstMethods.Count + " Burst-declared targets. Native Burst " +
                    "execution is a separate in-game validation and is not proven by ownership."
                );
            }

            return new List<MethodBase>(MethodsToPatch);
        }

        internal static MethodInfo GetTranspilerMethod()
        {
            return AccessTools.Method(
                typeof(LargeMapCellMapSystemPatches),
                nameof(Transpiler)
            );
        }

        internal static void Apply(Harmony harmony)
        {
            if (harmony == null)
                throw new ArgumentNullException(nameof(harmony));

            if (!_manifestPrepared)
                throw new InvalidOperationException("CellMap manifest was not prepared.");

            var transpiler = new HarmonyMethod(
                GetTranspilerMethod()
            );

            var patched = 0;

            foreach (var method in MethodsToPatch)
            {
                try
                {
                    harmony.Patch(method, transpiler: transpiler);
                    patched++;
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        "[LargeMap] CellMap transpiler failed for " +
                        DescribeMethod(method) + ".",
                        ex
                    );
                }
            }

            Util.Log.Info(
                "[LargeMap] CellMap patch applied. fields=" +
                ReplacementMap.Count +
                ", methods=" +
                MethodsToPatch.Count +
                ", patched=" +
                patched
            );

            if (patched != MethodsToPatch.Count)
            {
                throw new InvalidOperationException(
                    "[LargeMap] CellMap patch incomplete. expected=" +
                    MethodsToPatch.Count + ", patched=" + patched + "."
                );
            }
        }

        internal static void ResetManifest()
        {
            ReplacementMap.Clear();
            MethodsToPatch.Clear();
            BurstMethods.Clear();
            _manifestPrepared = false;
        }

        private static void Register<TDerived>(int mapMultiplier, int textureMultiplier)
        {
            var derivedType = typeof(TDerived);

            var baseType = derivedType.BaseType;

            while (baseType != null)
            {
                if (baseType.IsGenericType && baseType.GetGenericTypeDefinition() == BaseGenericType)
                {
                    var mapField = baseType.GetField(
                        "kMapSize",
                        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
                    );

                    if (mapField == null)
                    {
                        throw new MissingFieldException(
                            baseType.FullName,
                            "kMapSize"
                        );
                    }

                    RegisterField(mapField, mapMultiplier);

                    ScanDeclaredMethods(baseType);
                    break;
                }

                baseType = baseType.BaseType;
            }

            if (baseType == null)
            {
                throw new InvalidOperationException(
                    "[LargeMap] " + derivedType.FullName +
                    " no longer derives from CellMapSystem<>."
                );
            }

            var texField = derivedType.GetField(
                "kTextureSize",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
            );

            if (texField != null)
                RegisterField(texField, textureMultiplier);

            ScanDeclaredMethods(derivedType);
        }

        private static void RegisterField(FieldInfo field, int multiplier)
        {
            if (field == null || multiplier <= 1)
                return;

            var raw = field.GetValue(null);

            if (!(raw is int))
            {
                throw new InvalidOperationException(
                    "[LargeMap] CellMap field is not Int32: " +
                    field.DeclaringType.FullName + "." + field.Name + "."
                );
            }

            var original = (int)raw;
            if (original != CityTimelineLargeMapState.OriginalMapSizeMeters)
            {
                throw new InvalidOperationException(
                    "[LargeMap] CellMap field has unexpected value: " +
                    field.DeclaringType.FullName + "." + field.Name +
                    "=" + original + "."
                );
            }

            var replacement = original * multiplier;

            if (!ReplacementMap.ContainsKey(field))
            {
                ReplacementMap[field] = replacement;

                Util.Log.Info(
                    "[LargeMap] CellMap field registered: " +
                    field.DeclaringType.FullName +
                    "." +
                    field.Name +
                    " " +
                    original +
                    " -> " +
                    replacement
                );
            }
        }

        private static void ScanDeclaredMethods(Type type)
        {
            if (type == null)
                return;

            var methods = type.GetMethods(
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.Instance |
                BindingFlags.Static |
                BindingFlags.DeclaredOnly
            );

            for (var i = 0; i < methods.Length; i++)
            {
                var method = methods[i];

                if (method == null || method.IsAbstract || method.ContainsGenericParameters)
                    continue;

                if (IsClientMethod(method) && MethodsToPatch.Add(method) &&
                    HasBurstCompileAttribute(method))
                {
                    BurstMethods.Add(method);
                }
            }

            var constructors = type.GetConstructors(
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.Instance |
                BindingFlags.Static |
                BindingFlags.DeclaredOnly
            );

            for (var i = 0; i < constructors.Length; i++)
            {
                var ctor = constructors[i];

                if (ctor == null || ctor.ContainsGenericParameters)
                    continue;

                if (IsClientMethod(ctor) && MethodsToPatch.Add(ctor) &&
                    HasBurstCompileAttribute(ctor))
                {
                    BurstMethods.Add(ctor);
                }
            }
        }

        private static void ScanGameAssemblyForClientMethods()
        {
            try
            {
                var gameAssembly = typeof(GameSystemBase).Assembly;
                var types = gameAssembly.GetTypes();

                for (var i = 0; i < types.Length; i++)
                {
                    var type = types[i];

                    if (type == null || type.Namespace == null)
                        continue;

                    if (!IsAllowedNamespace(type.Namespace))
                        continue;

                    if (type.IsInterface || type.IsGenericTypeDefinition)
                        continue;

                    ScanDeclaredMethods(type);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "[LargeMap] CellMap assembly scan failed.",
                    ex
                );
            }
        }

        private static bool IsAllowedNamespace(string ns)
        {
            return
                ns.StartsWith("Game.Simulation", StringComparison.Ordinal) ||
                ns.StartsWith("Game.Debug", StringComparison.Ordinal) ||
                ns.StartsWith("Game.UI.Tooltip", StringComparison.Ordinal) ||
                ns.StartsWith("Game.Tools", StringComparison.Ordinal);
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

        private static bool IsClientMethod(MethodBase method)
        {
            try
            {
                var body = method.GetMethodBody();

                if (body == null)
                    return false;

                var il = body.GetILAsByteArray();

                if (il == null)
                    return false;

                var hasLdsfld = false;

                for (var i = 0; i < il.Length; i++)
                {
                    if (il[i] == 0x7E)
                    {
                        hasLdsfld = true;
                        break;
                    }
                }

                if (!hasLdsfld)
                    return false;

                var instructions = PatchProcessor.GetOriginalInstructions(method);

                for (var i = 0; i < instructions.Count; i++)
                {
                    var instruction = instructions[i];

                    if (instruction.opcode == OpCodes.Ldsfld &&
                        instruction.operand is FieldInfo &&
                        ReplacementMap.ContainsKey((FieldInfo)instruction.operand))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private static IEnumerable<CodeInstruction> Transpiler(
            IEnumerable<CodeInstruction> instructions,
            MethodBase original)
        {
            var codes = new List<CodeInstruction>(instructions);
            var replacements = 0;

            for (var i = 0; i < codes.Count; i++)
            {
                var instruction = codes[i];
                if (instruction.opcode == OpCodes.Ldsfld &&
                    instruction.operand is FieldInfo)
                {
                    var field = (FieldInfo)instruction.operand;
                    int replacement;

                    if (ReplacementMap.TryGetValue(field, out replacement))
                    {
                        if (replacement != CityTimelineLargeMapState.MapSizeMeters)
                        {
                            throw new InvalidOperationException(
                                "[LargeMap] unsupported CellMap replacement=" + replacement +
                                " in " + DescribeMethod(original) + "."
                            );
                        }

                        instruction.opcode = OpCodes.Call;
                        instruction.operand = EffectiveMapSizeMethod;
                        replacements++;
                    }
                }
            }

            if (replacements == 0)
            {
                throw new InvalidOperationException(
                    "[LargeMap] CellMap transpiler made no replacement in " +
                    DescribeMethod(original) + "."
                );
            }

            return codes;
        }

        private static string DescribeMethod(MethodBase method)
        {
            if (method == null)
                return "<null>";

            return
                (method.DeclaringType != null ? method.DeclaringType.FullName : "<no-type>") +
                "::" +
                method.Name;
        }

        private sealed class FieldInfoComparer : IEqualityComparer<FieldInfo>
        {
            public bool Equals(FieldInfo x, FieldInfo y)
            {
                if (x == null || y == null)
                    return x == y;

                return
                    x.MetadataToken == y.MetadataToken &&
                    x.Module == y.Module &&
                    x.DeclaringType == y.DeclaringType;
            }

            public int GetHashCode(FieldInfo obj)
            {
                if (obj == null)
                    return 0;

                return obj.MetadataToken.GetHashCode() ^
                       (obj.DeclaringType != null ? obj.DeclaringType.GetHashCode() : 0);
            }
        }

        private sealed class MethodComparer : IEqualityComparer<MethodBase>
        {
            public bool Equals(MethodBase x, MethodBase y)
            {
                if (x == null || y == null)
                    return x == y;

                return
                    x.MetadataToken == y.MetadataToken &&
                    x.Module == y.Module &&
                    x.DeclaringType == y.DeclaringType;
            }

            public int GetHashCode(MethodBase obj)
            {
                if (obj == null)
                    return 0;

                return obj.MetadataToken.GetHashCode() ^
                       (obj.DeclaringType != null ? obj.DeclaringType.GetHashCode() : 0);
            }
        }
    }
}
