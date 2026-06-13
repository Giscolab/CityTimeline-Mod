using System;
using Game.Simulation;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using CityTimelineMod.Util;

namespace CityTimelineMod.Terrain
{
    internal static class TerrainHeightSampler
    {
        private static TerrainSystem _terrainSystem;
        private static bool _loggedInitOk;
        private static bool _loggedInitFail;
        private static bool _loggedSampleOk;
        private static bool _loggedSampleFail;

        internal static bool TrySampleHeight(Vector3 worldPosition, out float height)
        {
            height = worldPosition.y;

            if (_terrainSystem == null)
                TryInitialize();

            if (_terrainSystem == null)
                return false;

            try
            {
                var data = _terrainSystem.GetHeightData(true);
                var pos = new float3(worldPosition.x, worldPosition.y, worldPosition.z);

                height = TerrainUtils.SampleHeight(ref data, pos);

                if (float.IsNaN(height) || float.IsInfinity(height))
                {
                    if (!_loggedSampleFail)
                    {
                        _loggedSampleFail = true;
                        Log.Info("TerrainHeightSampler: SampleHeight returned invalid value.");
                    }

                    return false;
                }

                if (!_loggedSampleOk)
                {
                    _loggedSampleOk = true;
                    Log.Info("TerrainHeightSampler: SampleHeight OK. input=" + worldPosition + ", height=" + height);
                }

                return true;
            }
            catch (Exception ex)
            {
                if (!_loggedSampleFail)
                {
                    _loggedSampleFail = true;
                    Log.Error("TerrainHeightSampler: SampleHeight failed: " + ex);
                }

                return false;
            }
        }

        private static void TryInitialize()
        {
            try
            {
                var world = World.DefaultGameObjectInjectionWorld;

                if (world == null)
                {
                    if (!_loggedInitFail)
                    {
                        _loggedInitFail = true;
                        Log.Info("TerrainHeightSampler: DefaultGameObjectInjectionWorld is null.");
                    }

                    return;
                }

                _terrainSystem = world.GetOrCreateSystemManaged<TerrainSystem>();

                if (_terrainSystem != null && !_loggedInitOk)
                {
                    _loggedInitOk = true;
                    Log.Info("TerrainHeightSampler: TerrainSystem initialized.");
                }
            }
            catch (Exception ex)
            {
                if (!_loggedInitFail)
                {
                    _loggedInitFail = true;
                    Log.Error("TerrainHeightSampler: init failed: " + ex);
                }
            }
        }
    }
}
