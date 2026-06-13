using System;
using System.Reflection;

namespace CityTimelineMod.Compatibility
{
    internal static class MapExtInterop
    {
        internal static bool TryGetMapExtState(
            out int coreValue,
            out int currentMapSizeMeters,
            out string currentMode,
            out string reason)
        {
            coreValue = 1;
            currentMapSizeMeters = 14336;
            currentMode = "unknown";
            reason = "";

            try
            {
                var patchManagerType = FindType("MapExtPDX.MapExt.Core.PatchManager");

                if (patchManagerType == null)
                {
                    reason = "MapExt PatchManager type not found";
                    return false;
                }

                var coreValueProperty = patchManagerType.GetProperty(
                    "CurrentCoreValue",
                    BindingFlags.Public | BindingFlags.Static
                );

                var currentMapSizeProperty = patchManagerType.GetProperty(
                    "CurrentMapSize",
                    BindingFlags.Public | BindingFlags.Static
                );

                var currentModeProperty = patchManagerType.GetProperty(
                    "CurrentMode",
                    BindingFlags.Public | BindingFlags.Static
                );

                if (coreValueProperty != null)
                {
                    var value = coreValueProperty.GetValue(null, null);

                    if (value != null)
                        coreValue = Convert.ToInt32(value);
                }

                // Fallback fiable meme si CurrentMapSize existe mais echoue.
                currentMapSizeMeters = coreValue * 14336;

                if (currentMapSizeProperty != null)
                {
                    var value = currentMapSizeProperty.GetValue(null, null);

                    if (value != null)
                        currentMapSizeMeters = Convert.ToInt32(value);
                }

                if (currentModeProperty != null)
                {
                    var value = currentModeProperty.GetValue(null, null);
                    currentMode = value != null ? value.ToString() : "unknown";
                }

                if (string.IsNullOrWhiteSpace(currentMode) || currentMode == "unknown")
                {
                    var getModeNameMethod = patchManagerType.GetMethod(
                        "GetModeNameForCoreValue",
                        BindingFlags.Public | BindingFlags.Static
                    );

                    if (getModeNameMethod != null)
                    {
                        var value = getModeNameMethod.Invoke(null, new object[] { coreValue });
                        currentMode = value != null ? value.ToString() : currentMode;
                    }
                }

                reason = "MapExt detected";
                return true;
            }
            catch (Exception ex)
            {
                reason = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        internal static bool IsMapExt57Active()
        {
            int coreValue;
            int mapSize;
            string mode;
            string reason;

            return TryGetMapExtState(out coreValue, out mapSize, out mode, out reason)
                   && coreValue == 4
                   && mapSize == 57344;
        }

        private static Type FindType(string fullName)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            for (var i = 0; i < assemblies.Length; i++)
            {
                Type type = null;

                try
                {
                    type = assemblies[i].GetType(fullName, false);
                }
                catch
                {
                    continue;
                }

                if (type != null)
                    return type;
            }

            return null;
        }
    }
}
