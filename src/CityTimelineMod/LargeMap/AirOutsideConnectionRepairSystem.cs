using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Game;
using Game.Common;
using Game.Net;
using Game.Pathfind;
using Game.Prefabs;
using Game.Simulation;
using Game.Tools;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Scripting;

namespace CityTimelineMod.LargeMap
{
    [Preserve]
    internal sealed partial class AirOutsideConnectionRepairSystem : GameSystemBase
    {
        private const float BorderTolerance = 32f;
        private const float MinimumAirwaySpan = 50000f;
        private const int CheckEveryFrames = 30;
        private const int MaxRefreshAttempts = 8;

        private AirwaySystem _airwaySystem;
        private TerrainSystem _terrainSystem;
        private WaterSystem _waterSystem;

        private EntityQuery _connectionLanePrefabQuery;
        private EntityQuery _airConnections;

        private readonly Dictionary<Entity, int> _refreshAttempts =
            new Dictionary<Entity, int>();

        private int _frame;
        private bool _airwaysInitialized;
        private bool _smallSpanLogged;
        private string _lastSnapshot;

        protected override void OnCreate()
        {
            base.OnCreate();

            _airwaySystem =
                World.GetOrCreateSystemManaged<AirwaySystem>();

            _terrainSystem =
                World.GetOrCreateSystemManaged<TerrainSystem>();

            _waterSystem =
                World.GetOrCreateSystemManaged<WaterSystem>();

            _connectionLanePrefabQuery = GetEntityQuery(
                ComponentType.ReadOnly<ConnectionLaneData>(),
                ComponentType.ReadOnly<PrefabData>()
            );

            _airConnections = GetEntityQuery(
                new EntityQueryDesc
                {
                    All = new ComponentType[]
                    {
                        ComponentType.ReadOnly<Game.Routes.AirplaneStop>(),
                        ComponentType.ReadOnly<Game.Routes.TakeoffLocation>(),
                        ComponentType.ReadOnly<Game.Objects.Transform>(),
                        ComponentType.ReadOnly<PrefabRef>()
                    },
                    None = new ComponentType[]
                    {
                        ComponentType.ReadOnly<Temp>(),
                        ComponentType.ReadOnly<Deleted>()
                    }
                }
            );

            Enabled = true;

            Util.Log.Info(
                "[AirOutside] system created; border=" +
                CityTimelineLargeMapState.HalfMapSizeMetersFloat +
                " m"
            );
        }

        protected override void OnUpdate()
        {
            _frame++;

            if ((_frame % CheckEveryFrames) != 0)
                return;

            Dependency.Complete();

            bool airwayReady = EnsureAirplaneAirways();

            int candidates = 0;
            int outsideAdded = 0;
            int structuresAdded = 0;
            int connected = 0;
            int markers = 0;

            StringBuilder details = new StringBuilder();

            using (NativeArray<Entity> entities =
                _airConnections.ToEntityArray(Allocator.Temp))
            {
                for (int i = 0; i < entities.Length; i++)
                {
                    Entity entity = entities[i];

                    if (!TryGetAirOutsideCandidate(
                            entity,
                            out Game.Objects.Transform transform,
                            out Entity prefab))
                    {
                        continue;
                    }

                    candidates++;

                    bool changed = false;

                    bool objectOutside =
                        EntityManager.HasComponent<
                            Game.Objects.OutsideConnection
                        >(entity);

                    if (!objectOutside)
                    {
                        EntityManager.AddComponent<
                            Game.Objects.OutsideConnection
                        >(entity);

                        objectOutside = true;
                        outsideAdded++;
                        changed = true;

                        Util.Log.Info(
                            "[AirOutside] added Game.Objects.OutsideConnection" +
                            " entity=" + Id(entity) +
                            " pos=" + F3(transform.m_Position)
                        );
                    }

                    if (!EntityManager.HasBuffer<Game.Net.SubLane>(entity))
                    {
                        EntityManager.AddBuffer<Game.Net.SubLane>(entity);
                        structuresAdded++;
                        changed = true;
                    }

                    if (!EntityManager.HasComponent<
                            Game.Routes.AccessLane
                        >(entity))
                    {
                        EntityManager.AddComponent<
                            Game.Routes.AccessLane
                        >(entity);

                        structuresAdded++;
                        changed = true;
                    }

                    if (!EntityManager.HasComponent<
                            Game.Routes.RouteLane
                        >(entity))
                    {
                        EntityManager.AddComponent<
                            Game.Routes.RouteLane
                        >(entity);

                        structuresAdded++;
                        changed = true;
                    }

                    int subLaneCount =
                        EntityManager.HasBuffer<Game.Net.SubLane>(entity)
                            ? EntityManager.GetBuffer<Game.Net.SubLane>(entity).Length
                            : 0;

                    Game.Routes.AccessLane accessLane =
                        EntityManager.GetComponentData<
                            Game.Routes.AccessLane
                        >(entity);

                    Game.Routes.RouteLane routeLane =
                        EntityManager.GetComponentData<
                            Game.Routes.RouteLane
                        >(entity);

                    bool accessValid =
                        IsExistingLane(accessLane.m_Lane);

                    Entity airwayLane = Entity.Null;

                    bool routeAirway =
                        TryGetAirplaneAirway(
                            routeLane,
                            out airwayLane
                        );

                    int iconCount = GetIconCount(entity);

                    if (routeAirway)
                        connected++;

                    if (iconCount > 0)
                        markers++;

                    bool firstSeen =
                        !_refreshAttempts.ContainsKey(entity);

                    bool needsRefresh =
                        firstSeen ||
                        changed ||
                        !objectOutside ||
                        (airwayReady && !routeAirway) ||
                        iconCount == 0;

                    if (needsRefresh)
                    {
                        TryRefresh(
                            entity,
                            changed
                                ? "air outside structure"
                                : (!routeAirway
                                    ? "airway connection"
                                    : "marker"),
                            transform.m_Position
                        );
                    }

                    RouteConnectionData routeData =
                        EntityManager.GetComponentData<
                            RouteConnectionData
                        >(prefab);

                    details.Append("[AirOutside] entity=");
                    details.Append(Id(entity));
                    details.Append(" pos=");
                    details.Append(F3(transform.m_Position));
                    details.Append(" outside=");
                    details.Append(objectOutside);
                    details.Append(" subLanes=");
                    details.Append(subLaneCount);
                    details.Append(" accessValid=");
                    details.Append(accessValid);
                    details.Append(" routeAirway=");
                    details.Append(routeAirway);
                    details.Append(" airwayLane=");
                    details.Append(Id(airwayLane));
                    details.Append(" icons=");
                    details.Append(iconCount);
                    details.Append(" accessType=");
                    details.Append(routeData.m_AccessConnectionType);
                    details.Append(" routeType=");
                    details.Append(routeData.m_RouteConnectionType);
                    details.Append(" accessRoad=");
                    details.Append(routeData.m_AccessRoadType);
                    details.Append(" routeRoad=");
                    details.Append(routeData.m_RouteRoadType);
                    details.AppendLine();
                }
            }

            string snapshot =
                "[AirOutside] summary airwayReady=" + airwayReady +
                " candidates=" + candidates +
                " outsideAdded=" + outsideAdded +
                " structuresAdded=" + structuresAdded +
                " connected=" + connected +
                " markers=" + markers +
                Environment.NewLine +
                details;

            if (!string.Equals(
                    snapshot,
                    _lastSnapshot,
                    StringComparison.Ordinal))
            {
                Util.Log.Info(snapshot);
                _lastSnapshot = snapshot;
            }
        }

        private bool EnsureAirplaneAirways()
        {
            if (_connectionLanePrefabQuery.IsEmptyIgnoreFilter)
                return false;

            AirwayHelpers.AirwayData airwayData =
                _airwaySystem.GetAirwayData();

            AirwayHelpers.AirwayMap map =
                airwayData.airplaneMap;

            NativeArray<Entity> entities =
                map.entities;

            if (!entities.IsCreated ||
                entities.Length == 0)
            {
                return false;
            }

            int gridSize = CalculateSquareGridSize(
                entities.Length
            );

            if (gridSize <= 0)
            {
                Util.Log.Info(
                    "[AirOutside] unsupported airplane airway entity count=" +
                    entities.Length
                );

                return false;
            }

            float3 first =
                map.GetNodePosition(new int2(0, 0));

            float3 last =
                map.GetNodePosition(
                    new int2(gridSize, gridSize)
                );

            float span =
                math.abs(last.x - first.x);

            if (span < MinimumAirwaySpan)
            {
                if (!_smallSpanLogged)
                {
                    _smallSpanLogged = true;

                    Util.Log.Info(
                        "[AirOutside] airplane airway span is still vanilla: " +
                        span.ToString(
                            "0.###",
                            CultureInfo.InvariantCulture
                        ) +
                        " m; waiting for the 57 km AirwaySystem scale patch."
                    );
                }

                return false;
            }

            _smallSpanLogged = false;

            if (_airwaysInitialized &&
                CountValidAirwayEntities(entities) ==
                    entities.Length)
            {
                return true;
            }

            using (NativeArray<Entity> prefabs =
                _connectionLanePrefabQuery.ToEntityArray(
                    Allocator.Temp))
            {
                if (prefabs.Length == 0)
                    return false;

                Entity lanePrefab =
                    prefabs[0];

                if (!EntityManager.HasComponent<
                        NetLaneArchetypeData
                    >(lanePrefab))
                {
                    Util.Log.Info(
                        "[AirOutside] connection lane prefab has no " +
                        "NetLaneArchetypeData."
                    );

                    return false;
                }

                NetLaneArchetypeData archetypeData =
                    EntityManager.GetComponentData<
                        NetLaneArchetypeData
                    >(lanePrefab);

                int created = 0;

                for (int i = 0; i < entities.Length; i++)
                {
                    Entity airway = entities[i];

                    if (IsValidAirwayEntity(airway))
                        continue;

                    entities[i] =
                        EntityManager.CreateEntity(
                            archetypeData.m_LaneArchetype
                        );

                    created++;
                }

                TerrainHeightData terrainData =
                    _terrainSystem.GetHeightData(true);

                JobHandle waterDependency;

                WaterSurfaceData<SurfaceWater> waterData =
                    _waterSystem.GetSurfaceData(
                        out waterDependency
                    );

                AirwayBuildJob job =
                    new AirwayBuildJob
                    {
                        m_AirwayMap = map,
                        m_Prefab = lanePrefab,
                        m_TerrainHeightData = terrainData,
                        m_WaterSurfaceData = waterData,

                        m_PrefabRefData =
                            GetComponentLookup<PrefabRef>(
                                false
                            ),

                        m_LaneData =
                            GetComponentLookup<Lane>(
                                false
                            ),

                        m_CurveData =
                            GetComponentLookup<Curve>(
                                false
                            ),

                        m_ConnectionLaneData =
                            GetComponentLookup<
                                Game.Net.ConnectionLane
                            >(false)
                    };

                JobHandle buildHandle =
                    job.Schedule(
                        entities.Length,
                        4,
                        JobHandle.CombineDependencies(
                            Dependency,
                            waterDependency
                        )
                    );

                _terrainSystem.AddCPUHeightReader(
                    buildHandle
                );

                _waterSystem.AddSurfaceReader(
                    buildHandle
                );

                buildHandle.Complete();
                Dependency = buildHandle;

                _airwaysInitialized = true;

                Util.Log.Info(
                    "[AirOutside] airplane airway grid ready" +
                    " entities=" + entities.Length +
                    " created=" + created +
                    " grid=" + gridSize + "x" + gridSize +
                    " span=" +
                    span.ToString(
                        "0.###",
                        CultureInfo.InvariantCulture
                    ) +
                    " m"
                );

                return true;
            }
        }

        private bool TryGetAirOutsideCandidate(
            Entity entity,
            out Game.Objects.Transform transform,
            out Entity prefab)
        {
            transform =
                EntityManager.GetComponentData<
                    Game.Objects.Transform
                >(entity);

            prefab =
                EntityManager.GetComponentData<PrefabRef>(
                    entity
                ).m_Prefab;

            if (prefab == Entity.Null ||
                !EntityManager.Exists(prefab))
            {
                return false;
            }

            if (!EntityManager.HasComponent<
                    OutsideConnectionData
                >(prefab) ||
                !EntityManager.HasComponent<
                    RouteConnectionData
                >(prefab) ||
                !EntityManager.HasComponent<
                    TransportStopData
                >(prefab))
            {
                return false;
            }

            TransportStopData stopData =
                EntityManager.GetComponentData<
                    TransportStopData
                >(prefab);

            if (stopData.m_TransportType !=
                TransportType.Airplane)
            {
                return false;
            }

            RouteConnectionData routeData =
                EntityManager.GetComponentData<
                    RouteConnectionData
                >(prefab);

            bool usesAir =
                routeData.m_AccessConnectionType ==
                    RouteConnectionType.Air ||
                routeData.m_RouteConnectionType ==
                    RouteConnectionType.Air ||
                (routeData.m_AccessRoadType &
                    RoadTypes.Airplane) !=
                    RoadTypes.None ||
                (routeData.m_RouteRoadType &
                    RoadTypes.Airplane) !=
                    RoadTypes.None;

            if (!usesAir)
                return false;

            float border =
                CityTimelineLargeMapState
                    .HalfMapSizeMetersFloat -
                BorderTolerance;

            float maxAbs =
                math.max(
                    math.abs(transform.m_Position.x),
                    math.abs(transform.m_Position.z)
                );

            return maxAbs >= border;
        }

        private bool TryGetAirplaneAirway(
            Game.Routes.RouteLane routeLane,
            out Entity airwayLane)
        {
            if (IsAirplaneAirwayLane(
                    routeLane.m_EndLane))
            {
                airwayLane = routeLane.m_EndLane;
                return true;
            }

            if (IsAirplaneAirwayLane(
                    routeLane.m_StartLane))
            {
                airwayLane = routeLane.m_StartLane;
                return true;
            }

            airwayLane = Entity.Null;
            return false;
        }

        private bool IsAirplaneAirwayLane(
            Entity laneEntity)
        {
            if (!IsExistingLane(laneEntity) ||
                !EntityManager.HasComponent<
                    Game.Net.ConnectionLane
                >(laneEntity))
            {
                return false;
            }

            Game.Net.ConnectionLane lane =
                EntityManager.GetComponentData<
                    Game.Net.ConnectionLane
                >(laneEntity);

            return
                (lane.m_Flags &
                    ConnectionLaneFlags.Airway) != 0 &&
                (lane.m_RoadTypes &
                    RoadTypes.Airplane) !=
                    RoadTypes.None;
        }

        private bool IsExistingLane(Entity entity)
        {
            return
                entity != Entity.Null &&
                EntityManager.Exists(entity) &&
                !EntityManager.HasComponent<Deleted>(
                    entity
                ) &&
                EntityManager.HasComponent<Lane>(
                    entity
                );
        }

        private bool IsValidAirwayEntity(
            Entity entity)
        {
            return
                IsExistingLane(entity) &&
                EntityManager.HasComponent<PrefabRef>(
                    entity
                ) &&
                EntityManager.HasComponent<Curve>(
                    entity
                ) &&
                EntityManager.HasComponent<
                    Game.Net.ConnectionLane
                >(entity);
        }

        private int CountValidAirwayEntities(
            NativeArray<Entity> entities)
        {
            int count = 0;

            for (int i = 0; i < entities.Length; i++)
            {
                if (IsValidAirwayEntity(entities[i]))
                    count++;
            }

            return count;
        }

        private static int CalculateSquareGridSize(
            int entityCount)
        {
            float value =
                (-2f +
                 math.sqrt(
                     4f + 12f * entityCount
                 )) /
                6f;

            int gridSize =
                (int)math.round(value);

            return
                3 * gridSize * gridSize +
                2 * gridSize ==
                entityCount
                    ? gridSize
                    : -1;
        }

        private int GetIconCount(Entity entity)
        {
            if (!EntityManager.HasBuffer<
                    Game.Notifications.IconElement
                >(entity))
            {
                return 0;
            }

            return EntityManager.GetBuffer<
                Game.Notifications.IconElement
            >(entity).Length;
        }

        private void TryRefresh(
            Entity entity,
            string reason,
            float3 position)
        {
            int attempt;

            _refreshAttempts.TryGetValue(
                entity,
                out attempt
            );

            if (attempt >= MaxRefreshAttempts)
                return;

            if (EntityManager.HasComponent<Updated>(
                    entity))
            {
                return;
            }

            EntityManager.AddComponent<Updated>(
                entity
            );

            attempt++;
            _refreshAttempts[entity] = attempt;

            Util.Log.Info(
                "[AirOutside] refresh " +
                reason +
                " entity=" + Id(entity) +
                " attempt=" + attempt +
                " pos=" + F3(position)
            );
        }

        private static string Id(Entity entity)
        {
            if (entity == Entity.Null)
                return "null";

            return
                entity.Index +
                ":" +
                entity.Version;
        }

        private static string F3(float3 value)
        {
            return
                "(" +
                value.x.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture
                ) +
                "," +
                value.y.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture
                ) +
                "," +
                value.z.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture
                ) +
                ")";
        }

        [BurstCompile]
        private struct AirwayBuildJob :
            IJobParallelFor
        {
            [ReadOnly]
            internal AirwayHelpers.AirwayMap
                m_AirwayMap;

            [ReadOnly]
            internal Entity m_Prefab;

            [ReadOnly]
            internal TerrainHeightData
                m_TerrainHeightData;

            [ReadOnly]
            internal WaterSurfaceData<SurfaceWater>
                m_WaterSurfaceData;

            [NativeDisableParallelForRestriction]
            internal ComponentLookup<PrefabRef>
                m_PrefabRefData;

            [NativeDisableParallelForRestriction]
            internal ComponentLookup<Lane>
                m_LaneData;

            [NativeDisableParallelForRestriction]
            internal ComponentLookup<Curve>
                m_CurveData;

            [NativeDisableParallelForRestriction]
            internal ComponentLookup<
                Game.Net.ConnectionLane
            > m_ConnectionLaneData;

            public void Execute(int entityIndex)
            {
                AirwayHelpers.LaneDirection direction;

                int2 cellIndex =
                    m_AirwayMap.GetCellIndex(
                        entityIndex,
                        out direction
                    );

                switch (direction)
                {
                    case AirwayHelpers.LaneDirection
                        .HorizontalZ:

                        CreateLane(
                            entityIndex,
                            cellIndex,
                            new int2(
                                cellIndex.x,
                                cellIndex.y + 1
                            )
                        );

                        break;

                    case AirwayHelpers.LaneDirection
                        .HorizontalX:

                        CreateLane(
                            entityIndex,
                            cellIndex,
                            new int2(
                                cellIndex.x + 1,
                                cellIndex.y
                            )
                        );

                        break;

                    case AirwayHelpers.LaneDirection
                        .Diagonal:

                        CreateLane(
                            entityIndex,
                            cellIndex,
                            cellIndex + 1
                        );

                        break;

                    case AirwayHelpers.LaneDirection
                        .DiagonalCross:

                        CreateLane(
                            entityIndex,
                            new int2(
                                cellIndex.x + 1,
                                cellIndex.y
                            ),
                            new int2(
                                cellIndex.x,
                                cellIndex.y + 1
                            )
                        );

                        break;
                }
            }

            private void CreateLane(
                int entityIndex,
                int2 startNode,
                int2 endNode)
            {
                Entity entity =
                    m_AirwayMap.entities[
                        entityIndex
                    ];

                Lane lane;

                lane.m_StartNode =
                    m_AirwayMap.GetPathNode(
                        startNode
                    );

                lane.m_MiddleNode =
                    new PathNode(entity, 1);

                lane.m_EndNode =
                    m_AirwayMap.GetPathNode(
                        endNode
                    );

                float3 startPosition =
                    m_AirwayMap.GetNodePosition(
                        startNode
                    );

                float3 endPosition =
                    m_AirwayMap.GetNodePosition(
                        endNode
                    );

                startPosition.y +=
                    WaterUtils.SampleHeight(
                        ref m_WaterSurfaceData,
                        ref m_TerrainHeightData,
                        startPosition
                    );

                endPosition.y +=
                    WaterUtils.SampleHeight(
                        ref m_WaterSurfaceData,
                        ref m_TerrainHeightData,
                        endPosition
                    );

                Curve curve;

                curve.m_Bezier =
                    NetUtils.StraightCurve(
                        startPosition,
                        endPosition
                    );

                curve.m_Length =
                    math.distance(
                        curve.m_Bezier.a,
                        curve.m_Bezier.d
                    );

                Game.Net.ConnectionLane
                    connectionLane;

                connectionLane.m_AccessRestriction =
                    Entity.Null;

                connectionLane.m_Flags =
                    ConnectionLaneFlags.AllowMiddle |
                    ConnectionLaneFlags.Airway;

                connectionLane.m_TrackTypes =
                    TrackTypes.None;

                connectionLane.m_RoadTypes =
                    RoadTypes.Airplane;

                m_PrefabRefData[entity] =
                    new PrefabRef(m_Prefab);

                m_LaneData[entity] = lane;
                m_CurveData[entity] = curve;

                m_ConnectionLaneData[entity] =
                    connectionLane;
            }
        }
    }
}
