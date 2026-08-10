using UnityEngine;

namespace CityTimelineMod.Util
{
    internal static class Log
    {
        internal static void Info(string msg)
        {
            Debug.Log("[CityTimelineMod] " + msg);
            Mod.Log.Info("[CityTimelineMod] " + msg);
        }

        internal static void Error(string msg)
        {
            Debug.LogError("[CityTimelineMod][ERR] " + msg);
            Mod.Log.Error("[CityTimelineMod][ERR] " + msg);
        }
    }
}
