using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Game;
using Game.Common;
using Game.Net;
using Game.Prefabs;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.Scripting;

namespace CityTimelineMod.LargeMap
{
    [Preserve]
    internal sealed partial class RailOutsideConnectionRepairSystem : GameSystemBase
    {
        private const float BorderTolerance = 4f;
        private const int CheckEveryFrames = 30;
        private const int MaxRefreshAttempts = 4;

        private EntityQuery _nodes;
        private EntityQuery _outsideLanes;

        private readonly Dictionary<Entity, int> _nodeRefreshAttempts =
            new Dictionary<Entity, int>();

        private readonly Dictionary<Entity, int> _objectRefreshAttempts =
            new Dictionary<Entity, int>();

        private int _frame;
        private string _lastSnapshot;

        protected override void OnCreate()
        {
            base.OnCreate();

            _nodes = GetEntityQuery(
                new EntityQueryDesc
                {
                    All = new ComponentType[]
                    {
                        ComponentType.ReadOnly<Node>(),
                        ComponentType.ReadOnly<ConnectedEdge>(),
                        ComponentType.ReadOnly<PrefabRef>()
                    },
                    None = new ComponentType[]
                    {
                        ComponentType.ReadOnly<Temp>(),
                        ComponentType.ReadOnly<Deleted>()
                    }
                }
            );

            _outsideLanes = GetEntityQuery(
                new EntityQueryDesc
                {
                    All = new ComponentType[]
                    {
                        ComponentType.ReadOnly<Game.Net.ConnectionLane>(),
                        ComponentType.ReadOnly<Game.Net.OutsideConnection>(),
                        ComponentType.ReadOnly<Owner>()
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
                "[RailOutside] system created; border=" +
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

            Dictionary<Entity, int> laneCounts = GetTrackLaneCounts();

            int candidates = 0;
            int outsideAdded = 0;
            int trainObjects = 0;
            int trackLanes = 0;

            StringBuilder details = new StringBuilder();

            using (NativeArray<Entity> entities =
                _nodes.ToEntityArray(Allocator.Temp))
            {
                for (int i = 0; i < entities.Length; i++)
                {
                    Entity nodeEntity = entities[i];

                    Node node;
                    int edgeCount;
                    int trainEdgeCount;

                    if (!IsBorderTrainDeadEnd(
                            nodeEntity,
                            out node,
                            out edgeCount,
                            out trainEdgeCount))
                    {
                        continue;
                    }

                    candidates++;

                    bool netOutside =
                        EntityManager.HasComponent<Game.Net.OutsideConnection>(
                            nodeEntity);

                    if (!netOutside)
                    {
                        EntityManager.AddComponent<Game.Net.OutsideConnection>(
                            nodeEntity);

                        netOutside = true;
                        outsideAdded++;

                        Util.Log.Info(
                            "[RailOutside] added Game.Net.OutsideConnection" +
                            " node=" + Id(nodeEntity) +
                            " pos=" + F3(node.m_Position)
                        );
                    }

                    bool anyOutsideObject;
                    bool trainStop;
                    bool transportStop;
                    int subObjectCount;

                    Entity outsideObject = FindOutsideObject(
                        nodeEntity,
                        out anyOutsideObject,
                        out trainStop,
                        out transportStop,
                        out subObjectCount
                    );

                    if (!trainStop)
                    {
                        TryRefresh(
                            nodeEntity,
                            _nodeRefreshAttempts,
                            "node/subobject"
                        );
                    }

                    int totalLanes =
                        CountFor(laneCounts, nodeEntity) +
                        CountFor(laneCounts, outsideObject);

                    trackLanes += totalLanes;

                    if (trainStop)
                    {
                        trainObjects++;

                        if (totalLanes == 0)
                        {
                            TryRefresh(
                                outsideObject,
                                _objectRefreshAttempts,
                                "outside track lanes"
                            );
                        }
                    }

                    details.Append("node=");
                    details.Append(Id(nodeEntity));
                    details.Append(" pos=");
                    details.Append(F3(node.m_Position));
                    details.Append(" edges=");
                    details.Append(edgeCount);
                    details.Append(" trainEdges=");
                    details.Append(trainEdgeCount);
                    details.Append(" netOutside=");
                    details.Append(netOutside);
                    details.Append(" subObjects=");
                    details.Append(subObjectCount);
                    details.Append(" objectOutside=");
                    details.Append(anyOutsideObject);
                    details.Append(" trainStop=");
                    details.Append(trainStop);
                    details.Append(" transportStop=");
                    details.Append(transportStop);
                    details.Append(" outsideObject=");
                    details.Append(Id(outsideObject));
                    details.Append(" trackOutsideLanes=");
                    details.Append(totalLanes);
                    details.AppendLine();
                }
            }

            string snapshot =
                "candidates=" + candidates +
                " outsideAdded=" + outsideAdded +
                " trainObjects=" + trainObjects +
                " trackOutsideLanes=" + trackLanes +
                Environment.NewLine +
                details;

            if (!string.Equals(
                    snapshot,
                    _lastSnapshot,
                    StringComparison.Ordinal))
            {
                Util.Log.Info(
                    "[RailOutside] ===== STATE =====" +
                    Environment.NewLine +
                    snapshot +
                    "[RailOutside] ===== END ====="
                );

                _lastSnapshot = snapshot;
            }
        }

        private bool IsBorderTrainDeadEnd(
            Entity entity,
            out Node node,
            out int edgeCount,
            out int trainEdgeCount)
        {
            node = EntityManager.GetComponentData<Node>(entity);
            edgeCount = 0;
            trainEdgeCount = 0;

            float border =
                CityTimelineLargeMapState.HalfMapSizeMetersFloat -
                BorderTolerance;

            float maxAbs = math.max(
                math.abs(node.m_Position.x),
                math.abs(node.m_Position.z)
            );

            if (maxAbs < border)
                return false;

            DynamicBuffer<ConnectedEdge> edges =
                EntityManager.GetBuffer<ConnectedEdge>(entity);

            for (int i = 0; i < edges.Length; i++)
            {
                Entity edge = edges[i].m_Edge;

                if (!EntityManager.Exists(edge) ||
                    EntityManager.HasComponent<Deleted>(edge))
                {
                    continue;
                }

                edgeCount++;

                if (!EntityManager.HasComponent<PrefabRef>(edge))
                    continue;

                Entity prefab =
                    EntityManager.GetComponentData<PrefabRef>(edge).m_Prefab;

                if (prefab == Entity.Null ||
                    !EntityManager.Exists(prefab) ||
                    !EntityManager.HasComponent<TrackData>(prefab))
                {
                    continue;
                }

                TrackData track =
                    EntityManager.GetComponentData<TrackData>(prefab);

                if ((track.m_TrackType & TrackTypes.Train) != TrackTypes.None)
                    trainEdgeCount++;
            }

            return edgeCount == 1 && trainEdgeCount == 1;
        }

        private Entity FindOutsideObject(
            Entity node,
            out bool anyOutsideObject,
            out bool trainStop,
            out bool transportStop,
            out int subObjectCount)
        {
            anyOutsideObject = false;
            trainStop = false;
            transportStop = false;
            subObjectCount = 0;

            if (!EntityManager.HasBuffer<Game.Objects.SubObject>(node))
                return Entity.Null;

            DynamicBuffer<Game.Objects.SubObject> children =
                EntityManager.GetBuffer<Game.Objects.SubObject>(node);

            subObjectCount = children.Length;
            Entity firstOutside = Entity.Null;

            for (int i = 0; i < children.Length; i++)
            {
                Entity child = children[i].m_SubObject;

                if (!EntityManager.Exists(child) ||
                    EntityManager.HasComponent<Deleted>(child))
                {
                    continue;
                }

                if (!EntityManager.HasComponent<Game.Objects.OutsideConnection>(
                        child))
                {
                    continue;
                }

                anyOutsideObject = true;

                if (firstOutside == Entity.Null)
                    firstOutside = child;

                bool currentTrainStop =
                    EntityManager.HasComponent<Game.Routes.TrainStop>(child);

                bool currentTransportStop =
                    EntityManager.HasComponent<Game.Routes.TransportStop>(
                        child);

                if (currentTrainStop)
                {
                    trainStop = true;
                    transportStop = currentTransportStop;
                    return child;
                }
            }

            return firstOutside;
        }

        private Dictionary<Entity, int> GetTrackLaneCounts()
        {
            Dictionary<Entity, int> result =
                new Dictionary<Entity, int>();

            using (NativeArray<Entity> lanes =
                _outsideLanes.ToEntityArray(Allocator.Temp))
            {
                for (int i = 0; i < lanes.Length; i++)
                {
                    Entity laneEntity = lanes[i];

                    Game.Net.ConnectionLane lane =
                        EntityManager.GetComponentData<Game.Net.ConnectionLane>(
                            laneEntity);

                    if ((lane.m_Flags & ConnectionLaneFlags.Track) == 0)
                        continue;

                    Entity owner =
                        EntityManager.GetComponentData<Owner>(laneEntity)
                            .m_Owner;

                    int count;
                    result.TryGetValue(owner, out count);
                    result[owner] = count + 1;
                }
            }

            return result;
        }

        private void TryRefresh(
            Entity entity,
            Dictionary<Entity, int> attempts,
            string reason)
        {
            if (entity == Entity.Null ||
                !EntityManager.Exists(entity) ||
                EntityManager.HasComponent<Deleted>(entity) ||
                EntityManager.HasComponent<Temp>(entity))
            {
                return;
            }

            int attempt;
            attempts.TryGetValue(entity, out attempt);

            if (attempt >= MaxRefreshAttempts)
                return;

            if (EntityManager.HasComponent<Updated>(entity))
                return;

            EntityManager.AddComponent<Updated>(entity);

            attempt++;
            attempts[entity] = attempt;

            Util.Log.Info(
                "[RailOutside] refresh " +
                reason +
                " entity=" + Id(entity) +
                " attempt=" + attempt
            );
        }

        private static int CountFor(
            Dictionary<Entity, int> values,
            Entity entity)
        {
            if (entity == Entity.Null)
                return 0;

            int value;

            return values.TryGetValue(entity, out value)
                ? value
                : 0;
        }

        private static string Id(Entity entity)
        {
            if (entity == Entity.Null)
                return "null";

            return entity.Index + ":" + entity.Version;
        }

        private static string F3(float3 value)
        {
            return
                "(" +
                value.x.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture) +
                "," +
                value.y.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture) +
                "," +
                value.z.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture) +
                ")";
        }
    }
}