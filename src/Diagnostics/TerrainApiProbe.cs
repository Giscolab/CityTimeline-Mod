using System;
using System.Linq;
using System.Reflection;
using CityTimelineMod.Util;

namespace CityTimelineMod.Diagnostics
{
    internal static class TerrainApiProbe
    {
        private static bool _ran;

        internal static void RunOnce()
        {
            if (_ran) return;
            _ran = true;

            try
            {
                Log.Info("TerrainApiProbe: start");

                var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                    .OrderBy(a => a.GetName().Name)
                    .ToArray();

                foreach (var asm in assemblies)
                {
                    var asmName = asm.GetName().Name;

                    if (
                        asmName == null ||
                        (
                            !asmName.StartsWith("Game", StringComparison.OrdinalIgnoreCase) &&
                            !asmName.StartsWith("Colossal", StringComparison.OrdinalIgnoreCase)
                        )
                    )
                    {
                        continue;
                    }

                    Type[] types;

                    try
                    {
                        types = asm.GetTypes();
                    }
                    catch (ReflectionTypeLoadException ex)
                    {
                        types = ex.Types.Where(t => t != null).ToArray();
                    }
                    catch
                    {
                        continue;
                    }

                    foreach (var type in types)
                    {
                        if (type == null || type.FullName == null)
                            continue;

                        var fullName = type.FullName;

                        if (
                            fullName.IndexOf("Terrain", StringComparison.OrdinalIgnoreCase) < 0 &&
                            fullName.IndexOf("Height", StringComparison.OrdinalIgnoreCase) < 0 &&
                            fullName.IndexOf("Elevation", StringComparison.OrdinalIgnoreCase) < 0
                        )
                        {
                            continue;
                        }

                        Log.Info("TerrainApiProbe: TYPE " + fullName + " / asm=" + asmName);

                        var isImportant =
                            fullName.IndexOf("TerrainSystem", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            fullName.IndexOf("TerrainUtils", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            fullName.IndexOf("Heightmap", StringComparison.OrdinalIgnoreCase) >= 0;

                        if (!isImportant)
                            continue;

                        DumpMembers(type);
                    }
                }

                Log.Info("TerrainApiProbe: done");
            }
            catch (Exception ex)
            {
                Log.Error("TerrainApiProbe failed: " + ex);
            }
        }

        private static void DumpMembers(Type type)
        {
            var flags =
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.Instance |
                BindingFlags.Static |
                BindingFlags.DeclaredOnly;

            foreach (var field in type.GetFields(flags))
                Log.Info("TerrainApiProbe: FIELD " + type.FullName + "." + field.Name + " : " + field.FieldType.FullName);

            foreach (var prop in type.GetProperties(flags))
                Log.Info("TerrainApiProbe: PROP " + type.FullName + "." + prop.Name + " : " + SafeTypeName(prop.PropertyType));

            foreach (var method in type.GetMethods(flags))
            {
                if (method.IsSpecialName)
                    continue;

                var parameters = method.GetParameters()
                    .Select(p => SafeTypeName(p.ParameterType) + " " + p.Name)
                    .ToArray();

                Log.Info(
                    "TerrainApiProbe: METHOD " +
                    type.FullName +
                    "." +
                    method.Name +
                    "(" +
                    string.Join(", ", parameters) +
                    ") : " +
                    SafeTypeName(method.ReturnType)
                );
            }
        }

        private static string SafeTypeName(Type type)
        {
            return type != null ? type.FullName : "<null>";
        }
    }
}
