using System.Collections.Generic;
using CityTimelineMod.Importers;
using CityTimelineMod.Rendering;
using CityTimelineMod.Terrain;
using Unity.Mathematics;
using UnityEngine;

namespace CityTimelineMod.Roads
{
    internal static partial class RuntimeRoadSpawner
    {
        private sealed partial class RuntimeRoadImportSession
        {
            private struct JunctionAccumulator
            {
                public int Count;
                public double SumX;
                public double SumZ;
            }

            private Dictionary<long, float2> BuildJunctionPositions(IList<GeoRoadLine> roadLines, float quantizeStep)
            {
                var accumulators = new Dictionary<long, JunctionAccumulator>();
                var seenInLine = new HashSet<long>();
                var result = new Dictionary<long, float2>();

                if (roadLines == null)
                    return result;

                for (var r = 0; r < roadLines.Count; r++)
                {
                    var roadLine = roadLines[r];

                    if (roadLine == null || roadLine.IsPath || roadLine.Points == null || roadLine.Points.Count < 2)
                        continue;

                    seenInLine.Clear();

                    for (var i = 0; i < roadLine.Points.Count; i++)
                    {
                        var world = GeoTransform.ToWorld(roadLine.Points[i], _config, _originLon, _originLat);
                        var position = new float3(world.x, world.y, world.z);
                        var key = MakePointKey(position, quantizeStep);

                        if (!seenInLine.Add(key))
                            continue;

                        JunctionAccumulator accumulator;
                        accumulators.TryGetValue(key, out accumulator);

                        accumulator.Count++;
                        accumulator.SumX += position.x;
                        accumulator.SumZ += position.z;

                        accumulators[key] = accumulator;
                    }
                }

                foreach (var pair in accumulators)
                {
                    var accumulator = pair.Value;

                    if (accumulator.Count <= 1)
                        continue;

                    result[pair.Key] = new float2(
                        (float)(accumulator.SumX / accumulator.Count),
                        (float)(accumulator.SumZ / accumulator.Count)
                    );
                }

                return result;
            }

            private List<WorldRoadPoint> BuildWorldPoints(
                IList<GeoPoint> source,
                int stride,
                HashSet<long> junctionKeys,
                Dictionary<long, float2> junctionPositions,
                float junctionQuantizeStep)
            {
                var result = new List<WorldRoadPoint>(source.Count);

                for (var i = 0; i < source.Count; i++)
                {
                    var world = GeoTransform.ToWorld(source[i], _config, _originLon, _originLat);
                    var position = new float3(world.x, world.y, world.z);
                    var junctionKey = MakePointKey(position, junctionQuantizeStep);

                    var isJunction =
                        i == 0 ||
                        i == source.Count - 1 ||
                        (junctionKeys != null && junctionKeys.Contains(junctionKey));

                    if (!isJunction && i % stride != 0)
                        continue;

                    if (isJunction && junctionPositions != null)
                    {
                        float2 canonical;

                        if (junctionPositions.TryGetValue(junctionKey, out canonical))
                        {
                            position.x = canonical.x;
                            position.z = canonical.y;
                        }
                    }

                    result.Add(new WorldRoadPoint
                    {
                        Position = position,
                        IsJunction = isJunction
                    });
                }

                var last = source[source.Count - 1];
                var lastWorld = GeoTransform.ToWorld(last, _config, _originLon, _originLat);
                var lastPoint = new float3(lastWorld.x, lastWorld.y, lastWorld.z);
                var lastKey = MakePointKey(lastPoint, junctionQuantizeStep);

                if (junctionPositions != null)
                {
                    float2 canonical;

                    if (junctionPositions.TryGetValue(lastKey, out canonical))
                    {
                        lastPoint.x = canonical.x;
                        lastPoint.z = canonical.y;
                    }
                }

                if (result.Count == 0 || math.distance(result[result.Count - 1].Position, lastPoint) > 0.01f)
                {
                    result.Add(new WorldRoadPoint
                    {
                        Position = lastPoint,
                        IsJunction = true
                    });
                }

                return result;
            }

            private bool ApplyTerrainY(ref float3 point)
            {
                var vector = new Vector3(point.x, point.y, point.z);
                float height;

                if (TerrainHeightSampler.TrySampleHeight(vector, out height))
                {
                    point.y = height;
                    return true;
                }

                point.y = GeoRoadImportPlacement.GroundY;
                return false;
            }
        }
    }
}
