using Game.Prefabs;
using Unity.Entities;
using Unity.Mathematics;

namespace CityTimelineMod.Roads
{
    internal struct RoadRequest
    {
        public Entity Prefab;
        public PrefabBase PrefabBase;
        public float3 Start;
        public float3 End;
        public string RoadName;
    }
}
