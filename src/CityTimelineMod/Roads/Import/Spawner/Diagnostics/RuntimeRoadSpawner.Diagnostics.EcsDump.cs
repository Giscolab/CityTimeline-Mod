using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace CityTimelineMod.Roads
{
    internal static partial class RuntimeRoadSpawner
    {
        private static string DumpComponentByFullName(EntityManager entityManager, Entity entity, string fullName, int maxFields)
        {
            if (entity == Entity.Null || !entityManager.Exists(entity) || string.IsNullOrWhiteSpace(fullName))
                return "<missing>";

            try
            {
                using (var types = entityManager.GetComponentTypes(entity, Allocator.Temp))
                {
                    for (var i = 0; i < types.Length; i++)
                    {
                        var managedType = types[i].GetManagedType();

                        if (managedType == null)
                            continue;

                        if (!string.Equals(managedType.FullName, fullName, StringComparison.Ordinal) &&
                            !string.Equals(managedType.Name, fullName, StringComparison.Ordinal))
                            continue;

                        var method = ResolveGetComponentDataMethod();

                        if (method == null)
                            return "<GetComponentData-not-found>";

                        var generic = method.MakeGenericMethod(managedType);
                        var value = generic.Invoke(entityManager, new object[] { entity });

                        return DumpValueFields(value, maxFields);
                    }
                }

                return "<not-present>";
            }
            catch (Exception ex)
            {
                return "<dump-failed: " + ex.Message + ">";
            }
        }

        private static MethodInfo ResolveGetComponentDataMethod()
        {
            var methods = typeof(EntityManager).GetMethods(BindingFlags.Instance | BindingFlags.Public);

            for (var i = 0; i < methods.Length; i++)
            {
                var method = methods[i];

                if (!string.Equals(method.Name, "GetComponentData", StringComparison.Ordinal) || !method.IsGenericMethodDefinition)
                    continue;

                var parameters = method.GetParameters();

                if (parameters.Length == 1 && parameters[0].ParameterType == typeof(Entity))
                    return method;
            }

            return null;
        }

        private static string DumpValueFields(object value, int maxFields)
        {
            if (value == null)
                return "<null>";

            try
            {
                var type = value.GetType();
                var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var sb = new StringBuilder();
                var count = 0;

                for (var i = 0; i < fields.Length && count < maxFields; i++)
                {
                    if (count > 0)
                        sb.Append("; ");

                    sb.Append(fields[i].Name);
                    sb.Append("=");
                    sb.Append(SafeDiagnosticValue(fields[i].GetValue(value)));
                    count++;
                }

                for (var i = 0; i < properties.Length && count < maxFields; i++)
                {
                    var property = properties[i];

                    if (!property.CanRead || property.GetIndexParameters().Length != 0)
                        continue;

                    object propertyValue;

                    try
                    {
                        propertyValue = property.GetValue(value, null);
                    }
                    catch
                    {
                        continue;
                    }

                    if (count > 0)
                        sb.Append("; ");

                    sb.Append(property.Name);
                    sb.Append("=");
                    sb.Append(SafeDiagnosticValue(propertyValue));
                    count++;
                }

                if (count == 0)
                    return "<no-fields>";

                if (fields.Length + properties.Length > count)
                    sb.Append("; ...");

                return sb.ToString();
            }
            catch (Exception ex)
            {
                return "<field-dump-failed: " + ex.Message + ">";
            }
        }

        private static string SafeDiagnosticValue(object value)
        {
            if (value == null)
                return "<null>";

            var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
            text = text.Replace("\r", " ").Replace("\n", " ");

            if (text.Length > 160)
                text = text.Substring(0, 160) + "...";

            return text;
        }

        private static string DumpComponentTypes(EntityManager entityManager, Entity entity)
        {
            if (entity == Entity.Null || !entityManager.Exists(entity))
                return "Entity.Null or missing";

            using (var types = entityManager.GetComponentTypes(entity, Allocator.Temp))
            {
                var names = new List<string>();

                for (var i = 0; i < types.Length; i++)
                {
                    var managedType = types[i].GetManagedType();
                    names.Add(managedType != null ? managedType.FullName : types[i].ToString());
                }

                names.Sort(StringComparer.Ordinal);
                return string.Join(" | ", names);
            }
        }

        private static string FormatEntity(Entity entity)
        {
            return entity == Entity.Null ? "Entity.Null" : "#" + entity.Index;
        }

        private static string FormatFloat3(float3 value)
        {
            return
                "(" +
                value.x.ToString("0.###", CultureInfo.InvariantCulture) + "," +
                value.y.ToString("0.###", CultureInfo.InvariantCulture) + "," +
                value.z.ToString("0.###", CultureInfo.InvariantCulture) +
                ")";
        }
    }
}
