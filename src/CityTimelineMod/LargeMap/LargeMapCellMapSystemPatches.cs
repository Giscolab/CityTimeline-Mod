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

        private static readonly Type BaseGenericType = typeof(CellMapSystem<>);

        internal static void Apply(Harmony harmony)
        {
            ReplacementMap.Clear();
            MethodsToPatch.Clear();

            Register<AirPollutionSystem>(4, 4);
            Register<AvailabilityInfoToGridSystem>(4, 4);
            Register<GroundPollutionSystem>(4, 4);
            Register<GroundWaterSystem>(4, 4);
            Register<LandValueSystem>(4, 4);
            Register<NaturalResourceSystem>(4, 4);
            Register<NoisePollutionSystem>(4, 4);
            Register<PopulationToGridSystem>(4, 4);
            Register<SoilWaterSystem>(4, 4);
            Register<TerrainAttractivenessSystem>(4, 4);
            Register<TrafficAmbienceSystem>(4, 4);
            Register<ZoneAmbienceSystem>(4, 4);
            Register<TelecomCoverageSystem>(4, 1);
            Register<WindSystem>(4, 1);

            ScanGameAssemblyForClientMethods();

            var transpiler = new HarmonyMethod(
                typeof(LargeMapCellMapSystemPatches),
                nameof(Transpiler)
            );

            var patched = 0;
            var failed = 0;

            foreach (var method in MethodsToPatch)
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
                        "[LargeMap] CellMap transpiler failed: " +
                        DescribeMethod(method) +
                        " error=" +
                        ex.GetType().Name +
                        ": " +
                        ex.Message
                    );
                }
            }

            Util.Log.Info(
                "[LargeMap] CellMap patch applied. fields=" +
                ReplacementMap.Count +
                ", methods=" +
                MethodsToPatch.Count +
                ", patched=" +
                patched +
                ", failed=" +
                failed
            );
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

                    if (mapField != null)
                        RegisterField(mapField, mapMultiplier);

                    ScanDeclaredMethods(baseType);
                    break;
                }

                baseType = baseType.BaseType;
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

            try
            {
                var raw = field.GetValue(null);

                if (!(raw is int))
                    return;

                var original = (int)raw;
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
            catch (Exception ex)
            {
                Util.Log.Error(
                    "[LargeMap] CellMap field register failed: " +
                    field.Name +
                    " error=" +
                    ex.Message
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

                if (IsClientMethod(method))
                    MethodsToPatch.Add(method);
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

                if (IsClientMethod(ctor))
                    MethodsToPatch.Add(ctor);
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

                    if (HasBurstCompileAttribute(type))
                        continue;

                    ScanDeclaredMethods(type);
                }
            }
            catch (Exception ex)
            {
                Util.Log.Error("[LargeMap] CellMap assembly scan failed: " + ex);
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

        private static bool HasBurstCompileAttribute(Type type)
        {
            try
            {
                var attributes = type.GetCustomAttributes(false);

                for (var i = 0; i < attributes.Length; i++)
                {
                    var attrType = attributes[i].GetType();

                    if (attrType.Name == "BurstCompileAttribute")
                        return true;
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
            foreach (var instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Ldsfld &&
                    instruction.operand is FieldInfo)
                {
                    var field = (FieldInfo)instruction.operand;
                    int replacement;

                    if (ReplacementMap.TryGetValue(field, out replacement))
                    {
                        instruction.opcode = OpCodes.Ldc_I4;
                        instruction.operand = replacement;
                    }
                }

                yield return instruction;
            }
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
