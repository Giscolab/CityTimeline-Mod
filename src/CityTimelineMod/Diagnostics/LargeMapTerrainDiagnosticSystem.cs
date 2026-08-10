using System;
using System.Globalization;
using System.Reflection;
using Colossal.Mathematics;
using Game;
using Game.Simulation;
using Unity.Mathematics;
using UnityEngine.Scripting;

namespace CityTimelineMod.Diagnostics
{
    [Preserve]
    internal sealed partial class LargeMapTerrainDiagnosticSystem : GameSystemBase
    {
        private TerrainSystem _terrainSystem;
        private int _frames;
        private bool _done;

        protected override void OnCreate()
        {
            base.OnCreate();
            _terrainSystem = World.GetOrCreateSystemManaged<TerrainSystem>();
            Enabled = true;
            Util.Log.Info("[LargeMapDiag] created");
        }

        protected override void OnUpdate()
        {
            if (_done)
                return;

            _frames++;

            if (_frames < 600)
                return;

            TerrainHeightData probe = _terrainSystem.GetHeightData(true);

            if (!probe.isCreated)
                return;

            int resolutionX = GetResolutionX(probe);

            if (resolutionX < 1024)
            {
                if ((_frames % 300) == 0)
                {
                    Util.Log.Info(
                        "[LargeMapDiag] waiting for real terrain heightmap. frame=" +
                        _frames +
                        " resolutionX=" +
                        resolutionX
                    );
                }

                return;
            }

            try
            {
                Util.Log.Info("[LargeMapDiag] ===== BEGIN =====");

                DumpTerrainHeightData("GetHeightData(true)", _terrainSystem.GetHeightData(true));
                DumpTerrainHeightData("GetHeightData(false)", _terrainSystem.GetHeightData(false));

                DumpStaticFields("TerrainSystem", typeof(TerrainSystem));
                DumpStaticFields("TerrainUtils", typeof(TerrainUtils));
                DumpInstanceFields("TerrainSystem instance", _terrainSystem);
                DumpStaticFields("WaterSystem", Type.GetType("Game.Simulation.WaterSystem, Game"));
                DumpStaticFields("CellMapSystem", Type.GetType("Game.Simulation.CellMapSystem`1, Game"));

                Util.Log.Info("[LargeMapDiag] ===== END =====");
            }
            catch (Exception ex)
            {
                Util.Log.Error("[LargeMapDiag] failed: " + ex);
            }

            _done = true;
            Enabled = false;
        }

        private static int GetResolutionX(TerrainHeightData data)
        {
            try
            {
                FieldInfo field = typeof(TerrainHeightData).GetField(
                    "<resolution>k__BackingField",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
                );

                if (field == null)
                    return 0;

                int3 resolution = (int3)field.GetValue(data);
                return resolution.x;
            }
            catch
            {
                return 0;
            }
        }
        private static void DumpTerrainHeightData(string label, TerrainHeightData data)
        {
            Util.Log.Info("[LargeMapDiag] " + label + " isCreated=" + data.isCreated);

            if (!data.isCreated)
                return;

            Bounds3 bounds = default(Bounds3);
            bool hasBounds = false;

            try
            {
                bounds = TerrainUtils.GetBounds(ref data);
                hasBounds = true;

                Util.Log.Info(
                    "[LargeMapDiag] " + label +
                    " bounds min=" + F3(bounds.min) +
                    " max=" + F3(bounds.max) +
                    " size=" + F3(bounds.max - bounds.min)
                );
            }
            catch (Exception ex)
            {
                Util.Log.Error("[LargeMapDiag] " + label + " bounds failed: " + ex.Message);
            }

            if (hasBounds)
            {
                float3 center = (bounds.min + bounds.max) * 0.5f;

                Sample(label, ref data, new float3(center.x, 0f, center.z));
                Sample(label, ref data, new float3(bounds.min.x + 10f, 0f, center.z));
                Sample(label, ref data, new float3(bounds.max.x - 10f, 0f, center.z));
                Sample(label, ref data, new float3(bounds.min.x - 10f, 0f, center.z));
                Sample(label, ref data, new float3(bounds.max.x + 10f, 0f, center.z));

                Sample(label, ref data, new float3(0f, 0f, 0f));
                Sample(label, ref data, new float3(7160f, 0f, 7160f));
                Sample(label, ref data, new float3(7200f, 0f, 7200f));
                Sample(label, ref data, new float3(10000f, 0f, 10000f));
                Sample(label, ref data, new float3(20000f, 0f, 20000f));
                Sample(label, ref data, new float3(28600f, 0f, 28600f));
            }
            else
            {
                Sample(label, ref data, new float3(0f, 0f, 0f));
                Sample(label, ref data, new float3(7160f, 0f, 7160f));
                Sample(label, ref data, new float3(7200f, 0f, 7200f));
                Sample(label, ref data, new float3(10000f, 0f, 10000f));
                Sample(label, ref data, new float3(20000f, 0f, 20000f));
                Sample(label, ref data, new float3(28600f, 0f, 28600f));
            }

            DumpInstanceFields(label + " fields", data);
        }

        private static void Sample(string label, ref TerrainHeightData data, float3 pos)
        {
            try
            {
                float h = TerrainUtils.SampleHeight(ref data, pos);

                Util.Log.Info(
                    "[LargeMapDiag] sample " +
                    label +
                    " pos=" +
                    F3(pos) +
                    " height=" +
                    h.ToString("0.###", CultureInfo.InvariantCulture)
                );
            }
            catch (Exception ex)
            {
                Util.Log.Error(
                    "[LargeMapDiag] sample " +
                    label +
                    " pos=" +
                    F3(pos) +
                    " failed: " +
                    ex.Message
                );
            }
        }

        private static void DumpStaticFields(string label, Type type)
        {
            if (type == null)
            {
                Util.Log.Info("[LargeMapDiag] " + label + " type not found");
                return;
            }

            FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo f = fields[i];

                if (!LooksRelevant(f.Name))
                    continue;

                try
                {
                    object value = f.GetValue(null);
                    Util.Log.Info("[LargeMapDiag] static " + label + "." + f.Name + "=" + Format(value));
                }
                catch
                {
                }
            }
        }

        private static void DumpInstanceFields(string label, object obj)
        {
            if (obj == null)
                return;

            FieldInfo[] fields = obj.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo f = fields[i];

                if (!LooksRelevant(f.Name))
                    continue;

                try
                {
                    object value = f.GetValue(obj);
                    Util.Log.Info("[LargeMapDiag] " + label + "." + f.Name + "=" + Format(value));
                }
                catch
                {
                }
            }
        }

        private static bool LooksRelevant(string name)
        {
            string n = name.ToLowerInvariant();

            return
                n.Contains("map") ||
                n.Contains("world") ||
                n.Contains("height") ||
                n.Contains("terrain") ||
                n.Contains("size") ||
                n.Contains("scale") ||
                n.Contains("bounds") ||
                n.Contains("cell") ||
                n.Contains("offset") ||
                n.Contains("texture") ||
                n.Contains("resolution");
        }

        private static string Format(object value)
        {
            if (value == null)
                return "null";

            try
            {
                UnityEngine.Texture texture = value as UnityEngine.Texture;

                if (texture != null)
                {
                    return texture.GetType().FullName +
                        " name=" + texture.name +
                        " width=" + texture.width +
                        " height=" + texture.height;
                }
            }
            catch
            {
            }

            try
            {
                Type t = value.GetType();
                PropertyInfo lengthProperty = t.GetProperty("Length");

                if (lengthProperty != null)
                {
                    object length = lengthProperty.GetValue(value, null);
                    return value + " Length=" + length;
                }
            }
            catch
            {
            }

            return value.ToString();
        }

        private static string F3(float3 v)
        {
            return "(" +
                v.x.ToString("0.###", CultureInfo.InvariantCulture) + "," +
                v.y.ToString("0.###", CultureInfo.InvariantCulture) + "," +
                v.z.ToString("0.###", CultureInfo.InvariantCulture) + ")";
        }
    }
}



