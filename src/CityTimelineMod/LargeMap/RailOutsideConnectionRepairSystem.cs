using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Colossal.Serialization.Entities;
using Game;
using Game.Common;
using Game.Net;
using Game.Prefabs;
using Game.Serialization;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.Scripting;

namespace CityTimelineMod.LargeMap
{
    [Preserve]
    internal struct CityTimelineRailOutsideConnectionOwner :
        IComponentData,
        IQueryTypeParameter,
        IEmptySerializable
    {
    }

    [Preserve]
    internal sealed partial class RailOutsideConnectionRepairSystem :
        GameSystemBase,
        IPreSerialize
    {
        private const float BorderTolerance = 4f;
        private const int CheckEveryFrames = 30;
        private const int MaxRefreshAttempts = 4;

        private EntityQuery _nodes;
        private EntityQuery _outsideLanes;
        private EntityQuery _ownedOutsideConnectionMarkers;

        private readonly Dictionary<Entity, int> _nodeRefreshAttempts =
            new Dictionary<Entity, int>();

        private readonly Dictionary<Entity, int> _objectRefreshAttempts =
            new Dictionary<Entity, int>();

        private readonly HashSet<Entity> _ownedOutsideConnections =
            new HashSet<Entity>();

        private bool _runtimeActive;
        private bool _rollbackPending;
        private bool _restoreAfterSerialization;
        private ulong _activeWorldSequence;
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

            _ownedOutsideConnectionMarkers = GetEntityQuery(
                ComponentType.ReadOnly<CityTimelineRailOutsideConnectionOwner>()
            );

            // UpdateAt may reuse this system after a hot reload. Registration
            // alone must never authorize a network mutation.
            _runtimeActive = false;
            _rollbackPending = false;
            _restoreAfterSerialization = false;
            _activeWorldSequence = 0;
            Enabled = false;

            Util.Log.Info(
                "[RailOutside] system created; border=" +
                CityTimelineLargeMapState.HalfMapSizeMetersFloat +
                " m"
            );
        }

        protected override void OnDestroy()
        {
            try
            {
                if (!DeactivateAndRollback())
                    Util.Log.Error("[RailOutside] destroy cleanup incomplete.");
            }
            catch (Exception ex)
            {
                Util.Log.Error(
                    "[RailOutside] destroy cleanup failed: " + ex
                );
            }

            base.OnDestroy();
        }

        internal bool ActivateForRuntime()
        {
            if (_runtimeActive && CanMutateCurrentWorld())
                return true;

            _runtimeActive = false;
            _activeWorldSequence = 0;
            Enabled = false;

            if (!RollbackOwnedOutsideConnections("activation preflight"))
            {
                _rollbackPending = true;
                Enabled = true;
                Util.Log.Error(
                    "[RailOutside] activation blocked: a previous CTM " +
                    "OutsideConnection mutation could not be rolled back."
                );
                return false;
            }

            _rollbackPending = false;
            _restoreAfterSerialization = false;
            if (!CanActivateForCurrentWorld())
            {
                Util.Log.Error(
                    "[RailOutside] activation blocked: runtime, LargeMap, " +
                    "or World authorization is unavailable."
                );
                return false;
            }

            _nodeRefreshAttempts.Clear();
            _objectRefreshAttempts.Clear();
            _frame = 0;
            _lastSnapshot = null;
            _activeWorldSequence = World.SequenceNumber;
            _runtimeActive = true;
            Enabled = true;

            Util.Log.Info(
                "[RailOutside] system activated for World sequence=" +
                _activeWorldSequence + "."
            );

            return true;
        }

        internal bool DeactivateAndRollback()
        {
            _runtimeActive = false;
            _activeWorldSequence = 0;
            _nodeRefreshAttempts.Clear();
            _objectRefreshAttempts.Clear();
            _frame = 0;
            _lastSnapshot = null;

            var complete = RollbackOwnedOutsideConnections("runtime teardown");
            _rollbackPending = !complete;
            _restoreAfterSerialization = false;
            Enabled = !complete;
            return complete;
        }

        protected override void OnUpdate()
        {
            if (Mod.TryProcessPendingLargeMapRuntimeFailure(World))
                return;

            if (_rollbackPending)
            {
                if (RollbackOwnedOutsideConnections("deferred runtime teardown"))
                {
                    _rollbackPending = false;
                    Enabled = false;
                }

                return;
            }

            if (_restoreAfterSerialization)
            {
                if (!CanMutateCurrentWorld())
                {
                    DeactivateAndRollback();
                    return;
                }

                RestoreOwnedOutsideConnectionsAfterSerialization();
                return;
            }

            if (_runtimeActive && !CanMutateCurrentWorld())
            {
                DeactivateAndRollback();
                return;
            }

            if (!CanMutateCurrentWorld())
                return;

            _frame++;

            if ((_frame % CheckEveryFrames) != 0)
                return;

            if (!CanMutateCurrentWorld())
                return;

            try
            {
                Dependency.Complete();
            }
            catch (Exception ex)
            {
                Util.Log.Error(
                    "[RailOutside] dependency completion failed; update skipped. " +
                    ex
                );
                return;
            }

            if (!CanMutateCurrentWorld())
                return;

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
                    if (!CanMutateCurrentWorld())
                        return;

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
                        if (!CanMutateCurrentWorld())
                            return;

                        if (!TryAddOwnedOutsideConnection(nodeEntity))
                            continue;

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

        private bool CanActivateForCurrentWorld()
        {
            // This repair reads only the 57 km border and game network data. It
            // has no direct PlayableWorld dependency, so LargeMap readiness and
            // an exact World identity are the complete authorization contract.
            return
                Mod.RuntimeEnabled &&
                Mod.IsActiveRuntimeWorld(World) &&
                CityTimelineLargeMapState.Enabled &&
                World != null &&
                World.IsCreated;
        }

        private bool CanMutateCurrentWorld()
        {
            return
                _runtimeActive &&
                Mod.IsActiveRuntimeWorld(World) &&
                CityTimelineLargeMapState.Enabled &&
                World != null &&
                World.IsCreated &&
                World.SequenceNumber == _activeWorldSequence;
        }

        void IPreSerialize.PreSerialize(Context context)
        {
            if (_restoreAfterSerialization)
                return;

            if (!StripOwnedOutsideConnectionsForSerialization())
            {
                _restoreAfterSerialization = true;
                throw new InvalidOperationException(
                    "CityTimelineMod could not strip every CTM-owned rail " +
                    "OutsideConnection before serialization. The save is " +
                    "blocked to avoid persisting an unowned network mutation."
                );
            }

            _restoreAfterSerialization =
                _ownedOutsideConnections.Count != 0;
        }

        private bool TryAddOwnedOutsideConnection(Entity entity)
        {
            var markerAlreadyPresent = false;

            try
            {
                markerAlreadyPresent = EntityManager.HasComponent<
                    CityTimelineRailOutsideConnectionOwner>(entity);

                if (!markerAlreadyPresent &&
                    !EntityManager.AddComponent<
                        CityTimelineRailOutsideConnectionOwner>(entity))
                {
                    return false;
                }

                // The serialized marker is the durable ownership proof. Keep the
                // in-memory set in sync immediately after the marker is created.
                _ownedOutsideConnections.Add(entity);

                if (!CanMutateCurrentWorld() ||
                    !EntityManager.AddComponent<Game.Net.OutsideConnection>(entity))
                {
                    if (!markerAlreadyPresent &&
                        EntityManager.RemoveComponent<
                            CityTimelineRailOutsideConnectionOwner>(entity))
                    {
                        _ownedOutsideConnections.Remove(entity);
                    }

                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                try
                {
                    if (!markerAlreadyPresent && EntityManager.Exists(entity) &&
                        EntityManager.HasComponent<
                            CityTimelineRailOutsideConnectionOwner>(entity) &&
                        EntityManager.RemoveComponent<
                            CityTimelineRailOutsideConnectionOwner>(entity))
                    {
                        _ownedOutsideConnections.Remove(entity);
                    }
                }
                catch (Exception rollbackEx)
                {
                    Util.Log.Error(
                        "[RailOutside] ownership-marker rollback failed for entity=" +
                        Id(entity) + ". " + rollbackEx
                    );
                }

                Util.Log.Error(
                    "[RailOutside] owned OutsideConnection add failed for entity=" +
                    Id(entity) + ". " + ex
                );
                return false;
            }
        }

        private void CaptureOwnedOutsideConnectionMarkers()
        {
            if (_ownedOutsideConnectionMarkers.IsEmptyIgnoreFilter)
                return;

            using (var entities =
                _ownedOutsideConnectionMarkers.ToEntityArray(Allocator.Temp))
            {
                for (var i = 0; i < entities.Length; i++)
                    _ownedOutsideConnections.Add(entities[i]);
            }
        }

        private bool StripOwnedOutsideConnectionsForSerialization()
        {
            if (World == null || !World.IsCreated)
                return _ownedOutsideConnections.Count == 0;

            try
            {
                Dependency.Complete();
                CaptureOwnedOutsideConnectionMarkers();
            }
            catch (Exception ex)
            {
                Util.Log.Error(
                    "[RailOutside] pre-serialize ownership capture failed: " + ex
                );
                return false;
            }

            var complete = true;
            var resolved = new List<Entity>();

            foreach (var entity in _ownedOutsideConnections)
            {
                try
                {
                    if (!EntityManager.Exists(entity) ||
                        EntityManager.HasComponent<Deleted>(entity))
                    {
                        resolved.Add(entity);
                        continue;
                    }

                    if (EntityManager.HasComponent<Temp>(entity))
                    {
                        complete = false;
                        continue;
                    }

                    if (!EntityManager.HasComponent<
                            CityTimelineRailOutsideConnectionOwner>(entity))
                    {
                        // Never infer ownership from OutsideConnection alone.
                        resolved.Add(entity);
                        continue;
                    }

                    if (EntityManager.HasComponent<Game.Net.OutsideConnection>(entity) &&
                        !EntityManager.RemoveComponent<Game.Net.OutsideConnection>(entity))
                    {
                        complete = false;
                        continue;
                    }

                    if (!EntityManager.RemoveComponent<
                            CityTimelineRailOutsideConnectionOwner>(entity))
                    {
                        complete = false;
                    }
                }
                catch (Exception ex)
                {
                    complete = false;
                    Util.Log.Error(
                        "[RailOutside] pre-serialize strip failed for entity=" +
                        Id(entity) + ". " + ex
                    );
                }
            }

            for (var i = 0; i < resolved.Count; i++)
                _ownedOutsideConnections.Remove(resolved[i]);

            return complete;
        }

        private bool RestoreOwnedOutsideConnectionsAfterSerialization()
        {
            if (World == null || !World.IsCreated)
            {
                _ownedOutsideConnections.Clear();
                _restoreAfterSerialization = false;
                return true;
            }

            try
            {
                Dependency.Complete();
            }
            catch (Exception ex)
            {
                Util.Log.Error(
                    "[RailOutside] post-serialize dependency completion failed: " + ex
                );
                return false;
            }

            var complete = true;
            var resolved = new List<Entity>();

            foreach (var entity in _ownedOutsideConnections)
            {
                try
                {
                    if (!EntityManager.Exists(entity) ||
                        EntityManager.HasComponent<Deleted>(entity))
                    {
                        resolved.Add(entity);
                        continue;
                    }

                    if (EntityManager.HasComponent<Temp>(entity))
                    {
                        complete = false;
                        continue;
                    }

                    var hasMarker = EntityManager.HasComponent<
                        CityTimelineRailOutsideConnectionOwner>(entity);
                    var hasOutside = EntityManager.HasComponent<
                        Game.Net.OutsideConnection>(entity);

                    if (!hasMarker && hasOutside)
                    {
                        // Another owner recreated the component during the save
                        // window. Do not claim or later remove it.
                        resolved.Add(entity);
                        continue;
                    }

                    if (!hasMarker &&
                        !EntityManager.AddComponent<
                            CityTimelineRailOutsideConnectionOwner>(entity))
                    {
                        complete = false;
                        continue;
                    }

                    if (!hasOutside &&
                        !EntityManager.AddComponent<Game.Net.OutsideConnection>(entity))
                    {
                        complete = false;
                    }
                }
                catch (Exception ex)
                {
                    complete = false;
                    Util.Log.Error(
                        "[RailOutside] post-serialize restore failed for entity=" +
                        Id(entity) + ". " + ex
                    );
                }
            }

            for (var i = 0; i < resolved.Count; i++)
                _ownedOutsideConnections.Remove(resolved[i]);

            _restoreAfterSerialization = !complete;
            return complete;
        }

        private bool RollbackOwnedOutsideConnections(string stage)
        {
            if (World == null || !World.IsCreated)
            {
                // Entity handles cannot outlive their World.
                _ownedOutsideConnections.Clear();
                return true;
            }

            try
            {
                Dependency.Complete();
                CaptureOwnedOutsideConnectionMarkers();
            }
            catch (Exception ex)
            {
                Util.Log.Error(
                    "[RailOutside] " + stage +
                    " dependency completion failed: " + ex
                );
                return false;
            }

            if (_ownedOutsideConnections.Count == 0)
                return true;

            bool complete = true;
            int rolledBack = 0;
            var resolved = new List<Entity>();

            foreach (Entity entity in _ownedOutsideConnections)
            {
                try
                {
                    if (!EntityManager.Exists(entity) ||
                        EntityManager.HasComponent<Deleted>(entity))
                    {
                        resolved.Add(entity);
                        continue;
                    }

                    if (EntityManager.HasComponent<Temp>(entity))
                    {
                        complete = false;
                        continue;
                    }

                    if (!EntityManager.HasComponent<
                            CityTimelineRailOutsideConnectionOwner>(entity))
                    {
                        // The marker, not the mutable vanilla component, is the
                        // authoritative ownership proof.
                        resolved.Add(entity);
                        continue;
                    }

                    if (EntityManager.HasComponent<Game.Net.OutsideConnection>(entity) &&
                        !EntityManager.RemoveComponent<Game.Net.OutsideConnection>(
                            entity))
                    {
                        complete = false;
                        continue;
                    }

                    if (!EntityManager.RemoveComponent<
                            CityTimelineRailOutsideConnectionOwner>(entity))
                    {
                        complete = false;
                        continue;
                    }

                    // Ask the vanilla systems to rebuild the affected node after
                    // CTM has removed the tag it owned.
                    if (!EntityManager.HasComponent<Updated>(entity))
                        EntityManager.AddComponent<Updated>(entity);

                    resolved.Add(entity);
                    rolledBack++;
                }
                catch (Exception ex)
                {
                    complete = false;
                    Util.Log.Error(
                        "[RailOutside] " + stage +
                        " rollback failed for entity=" + Id(entity) + ". " + ex
                    );
                }
            }

            for (int i = 0; i < resolved.Count; i++)
                _ownedOutsideConnections.Remove(resolved[i]);

            if (rolledBack > 0)
            {
                Util.Log.Info(
                    "[RailOutside] CTM-owned OutsideConnection components " +
                    "rolled back=" + rolledBack + "."
                );
            }

            if (!complete || _ownedOutsideConnections.Count != 0)
            {
                Util.Log.Error(
                    "[RailOutside] " + stage +
                    " rollback incomplete; pending=" +
                    _ownedOutsideConnections.Count + "."
                );
            }

            return complete && _ownedOutsideConnections.Count == 0;
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
            if (!CanMutateCurrentWorld())
                return;

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

            if (!CanMutateCurrentWorld())
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
