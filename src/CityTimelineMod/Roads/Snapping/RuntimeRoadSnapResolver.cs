using System;
using System.Collections.Generic;
using System.Globalization;
using Colossal.Mathematics;
using Game.Common;
using Game.Net;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace CityTimelineMod.Roads
{
    internal enum RuntimeRoadSnapTargetKind
    {
        None = 0,
        Node = 1,
        Edge = 2
    }

    internal struct RuntimeRoadSnapTarget
    {
        internal RuntimeRoadSnapTargetKind Kind;
        internal Entity Entity;
        internal float3 Position;
        internal float SplitPosition;

        internal bool HasTarget
        {
            get { return Kind != RuntimeRoadSnapTargetKind.None && Entity != Entity.Null; }
        }

        internal bool IsNode
        {
            get { return Kind == RuntimeRoadSnapTargetKind.Node && Entity != Entity.Null; }
        }

        internal bool IsEdge
        {
            get { return Kind == RuntimeRoadSnapTargetKind.Edge && Entity != Entity.Null; }
        }

        internal static RuntimeRoadSnapTarget None(float3 position)
        {
            return new RuntimeRoadSnapTarget
            {
                Kind = RuntimeRoadSnapTargetKind.None,
                Entity = Entity.Null,
                Position = position,
                SplitPosition = 0f
            };
        }

        internal static RuntimeRoadSnapTarget Node(Entity entity, float3 position)
        {
            return new RuntimeRoadSnapTarget
            {
                Kind = RuntimeRoadSnapTargetKind.Node,
                Entity = entity,
                Position = position,
                SplitPosition = 0f
            };
        }

        internal static RuntimeRoadSnapTarget Edge(Entity entity, float3 position, float splitPosition)
        {
            return new RuntimeRoadSnapTarget
            {
                Kind = RuntimeRoadSnapTargetKind.Edge,
                Entity = entity,
                Position = position,
                SplitPosition = math.clamp(splitPosition, 0f, 1f)
            };
        }
    }

    internal sealed class RuntimeRoadSnapResolver
    {
        private const int ProjectionSamples = 24;
        private const int ProjectionRefinementIterations = 8;

        private readonly EntityManager _entityManager;
        private readonly EntityQuery _nodeQuery;
        private readonly EntityQuery _edgeQuery;
        private readonly Dictionary<long, List<NodeCandidate>> _nodeGrid = new Dictionary<long, List<NodeCandidate>>();
        private readonly Dictionary<long, List<EdgeCandidate>> _edgeGrid = new Dictionary<long, List<EdgeCandidate>>();
        private readonly HashSet<int> _visitedEdges = new HashSet<int>();

        private float _nodeToleranceMeters = 4f;
        private float _edgeToleranceMeters = 4f;
        private float _nodeCellSize = 4f;
        private float _edgeCellSize = 4f;
        private int _nodeCount;
        private int _edgeCount;

        private int _nodeHits;
        private int _edgeHits;
        private int _misses;
        private int _resolveCalls;
        private float _maxNodeDistance;
        private float _maxEdgeDistance;
        private float _totalNodeDistance;
        private float _totalEdgeDistance;

        internal RuntimeRoadSnapResolver(EntityManager entityManager)
        {
            _entityManager = entityManager;
            _nodeQuery = _entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<Node>(),
                ComponentType.ReadOnly<Game.Objects.Transform>(),
                ComponentType.Exclude<Temp>()
            );
            _edgeQuery = _entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<Edge>(),
                ComponentType.ReadOnly<Road>(),
                ComponentType.ReadOnly<Curve>(),
                ComponentType.Exclude<Temp>()
            );
        }

        internal void SetTolerances(float nodeToleranceMeters, float edgeToleranceMeters)
        {
            _nodeToleranceMeters = math.max(0.25f, nodeToleranceMeters);
            _edgeToleranceMeters = math.max(0.25f, edgeToleranceMeters);
            _nodeCellSize = math.max(1f, _nodeToleranceMeters);
            _edgeCellSize = math.max(1f, _edgeToleranceMeters);
        }

        internal void ResetStats()
        {
            _nodeHits = 0;
            _edgeHits = 0;
            _misses = 0;
            _resolveCalls = 0;
            _maxNodeDistance = 0f;
            _maxEdgeDistance = 0f;
            _totalNodeDistance = 0f;
            _totalEdgeDistance = 0f;
        }

        internal void Refresh()
        {
            _nodeGrid.Clear();
            _edgeGrid.Clear();
            _nodeCount = 0;
            _edgeCount = 0;

            RefreshNodes();
            RefreshEdges();

            Util.Log.Info(
                "[RoadSnapResolver] refreshed. nodes=" + _nodeCount +
                ", edges=" + _edgeCount +
                ", nodeTolerance=" + F(_nodeToleranceMeters) +
                ", edgeTolerance=" + F(_edgeToleranceMeters)
            );
        }

        internal RuntimeRoadSnapTarget Resolve(float3 requestedPosition)
        {
            _resolveCalls++;

            RuntimeRoadSnapTarget target;
            float distance;

            if (TryResolveNode(requestedPosition, out target, out distance))
            {
                _nodeHits++;
                _totalNodeDistance += distance;
                if (distance > _maxNodeDistance)
                    _maxNodeDistance = distance;
                return target;
            }

            if (TryResolveEdge(requestedPosition, out target, out distance))
            {
                _edgeHits++;
                _totalEdgeDistance += distance;
                if (distance > _maxEdgeDistance)
                    _maxEdgeDistance = distance;
                return target;
            }

            _misses++;
            return RuntimeRoadSnapTarget.None(requestedPosition);
        }

        internal void LogStats(int commitIndex, int commitRequests, int commitDeltaEdges, int skippedDegenerate)
        {
            var avgNode = _nodeHits > 0 ? _totalNodeDistance / _nodeHits : 0f;
            var avgEdge = _edgeHits > 0 ? _totalEdgeDistance / _edgeHits : 0f;

            Util.Log.Info(
                "[RoadSnapResolver] summary. commitIndex=" + commitIndex +
                ", requests=" + commitRequests +
                ", resolveCalls=" + _resolveCalls +
                ", nodeHits=" + _nodeHits +
                ", edgeHits=" + _edgeHits +
                ", misses=" + _misses +
                ", skippedDegenerate=" + skippedDegenerate +
                ", commitDeltaEdges=" + commitDeltaEdges +
                ", avgNodeDistance=" + F(avgNode) +
                ", maxNodeDistance=" + F(_maxNodeDistance) +
                ", avgEdgeDistance=" + F(avgEdge) +
                ", maxEdgeDistance=" + F(_maxEdgeDistance)
            );
        }

        private void RefreshNodes()
        {
            if (_nodeQuery.IsEmpty)
                return;

            try
            {
                using (var entities = _nodeQuery.ToEntityArray(Allocator.Temp))
                {
                    for (var i = 0; i < entities.Length; i++)
                    {
                        var entity = entities[i];

                        if (!_entityManager.Exists(entity) ||
                            _entityManager.HasComponent<Deleted>(entity) ||
                            !_entityManager.HasComponent<Game.Objects.Transform>(entity))
                        {
                            continue;
                        }

                        var transform = _entityManager.GetComponentData<Game.Objects.Transform>(entity);
                        var candidate = new NodeCandidate
                        {
                            Entity = entity,
                            Position = transform.m_Position
                        };

                        AddNodeCandidate(candidate);
                        _nodeCount++;
                    }
                }
            }
            catch (Exception ex)
            {
                Util.Log.Error("[RoadSnapResolver] failed to refresh nodes: " + ex);
            }
        }

        private void RefreshEdges()
        {
            if (_edgeQuery.IsEmpty)
                return;

            try
            {
                using (var entities = _edgeQuery.ToEntityArray(Allocator.Temp))
                {
                    for (var i = 0; i < entities.Length; i++)
                    {
                        var entity = entities[i];

                        if (!_entityManager.Exists(entity) ||
                            _entityManager.HasComponent<Deleted>(entity) ||
                            !_entityManager.HasComponent<Curve>(entity))
                        {
                            continue;
                        }

                        var curve = _entityManager.GetComponentData<Curve>(entity);
                        var candidate = new EdgeCandidate
                        {
                            Entity = entity,
                            Bezier = curve.m_Bezier
                        };

                        AddEdgeCandidate(candidate);
                        _edgeCount++;
                    }
                }
            }
            catch (Exception ex)
            {
                Util.Log.Error("[RoadSnapResolver] failed to refresh edges: " + ex);
            }
        }

        private bool TryResolveNode(float3 requestedPosition, out RuntimeRoadSnapTarget target, out float distance)
        {
            target = RuntimeRoadSnapTarget.None(requestedPosition);
            distance = 0f;

            var point = XZ(requestedPosition);
            var cellX = Quantize(point.x, _nodeCellSize);
            var cellZ = Quantize(point.y, _nodeCellSize);
            var bestDistanceSq = _nodeToleranceMeters * _nodeToleranceMeters;
            var best = default(NodeCandidate);
            var found = false;

            for (var dz = -1; dz <= 1; dz++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    List<NodeCandidate> bucket;

                    if (!_nodeGrid.TryGetValue(MakeCellKey(cellX + dx, cellZ + dz), out bucket))
                        continue;

                    for (var i = 0; i < bucket.Count; i++)
                    {
                        var candidate = bucket[i];
                        var candidateDistanceSq = HorizontalDistanceSq(point, XZ(candidate.Position));

                        if (candidateDistanceSq <= bestDistanceSq)
                        {
                            bestDistanceSq = candidateDistanceSq;
                            best = candidate;
                            found = true;
                        }
                    }
                }
            }

            if (!found)
                return false;

            distance = math.sqrt(bestDistanceSq);
            target = RuntimeRoadSnapTarget.Node(best.Entity, best.Position);
            return true;
        }

        private bool TryResolveEdge(float3 requestedPosition, out RuntimeRoadSnapTarget target, out float distance)
        {
            target = RuntimeRoadSnapTarget.None(requestedPosition);
            distance = 0f;

            var point = XZ(requestedPosition);
            var cellX = Quantize(point.x, _edgeCellSize);
            var cellZ = Quantize(point.y, _edgeCellSize);
            var bestDistanceSq = _edgeToleranceMeters * _edgeToleranceMeters;
            var bestEntity = Entity.Null;
            var bestPosition = requestedPosition;
            var bestSplitPosition = 0f;
            var found = false;

            _visitedEdges.Clear();

            for (var dz = -1; dz <= 1; dz++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    List<EdgeCandidate> bucket;

                    if (!_edgeGrid.TryGetValue(MakeCellKey(cellX + dx, cellZ + dz), out bucket))
                        continue;

                    for (var i = 0; i < bucket.Count; i++)
                    {
                        var candidate = bucket[i];

                        if (!_visitedEdges.Add(candidate.Entity.Index))
                            continue;

                        float splitPosition;
                        float3 projected;
                        var candidateDistanceSq = ProjectHorizontal(candidate.Bezier, requestedPosition, out splitPosition, out projected);

                        if (candidateDistanceSq <= bestDistanceSq)
                        {
                            bestDistanceSq = candidateDistanceSq;
                            bestEntity = candidate.Entity;
                            bestPosition = projected;
                            bestSplitPosition = splitPosition;
                            found = true;
                        }
                    }
                }
            }

            if (!found)
                return false;

            distance = math.sqrt(bestDistanceSq);
            target = RuntimeRoadSnapTarget.Edge(bestEntity, bestPosition, bestSplitPosition);
            return true;
        }

        private void AddNodeCandidate(NodeCandidate candidate)
        {
            var cellX = Quantize(candidate.Position.x, _nodeCellSize);
            var cellZ = Quantize(candidate.Position.z, _nodeCellSize);
            var key = MakeCellKey(cellX, cellZ);
            List<NodeCandidate> bucket;

            if (!_nodeGrid.TryGetValue(key, out bucket))
            {
                bucket = new List<NodeCandidate>(4);
                _nodeGrid[key] = bucket;
            }

            bucket.Add(candidate);
        }

        private void AddEdgeCandidate(EdgeCandidate candidate)
        {
            var minX = math.min(math.min(candidate.Bezier.a.x, candidate.Bezier.b.x), math.min(candidate.Bezier.c.x, candidate.Bezier.d.x)) - _edgeToleranceMeters;
            var maxX = math.max(math.max(candidate.Bezier.a.x, candidate.Bezier.b.x), math.max(candidate.Bezier.c.x, candidate.Bezier.d.x)) + _edgeToleranceMeters;
            var minZ = math.min(math.min(candidate.Bezier.a.z, candidate.Bezier.b.z), math.min(candidate.Bezier.c.z, candidate.Bezier.d.z)) - _edgeToleranceMeters;
            var maxZ = math.max(math.max(candidate.Bezier.a.z, candidate.Bezier.b.z), math.max(candidate.Bezier.c.z, candidate.Bezier.d.z)) + _edgeToleranceMeters;

            var minCellX = Quantize(minX, _edgeCellSize);
            var maxCellX = Quantize(maxX, _edgeCellSize);
            var minCellZ = Quantize(minZ, _edgeCellSize);
            var maxCellZ = Quantize(maxZ, _edgeCellSize);

            for (var z = minCellZ; z <= maxCellZ; z++)
            {
                for (var x = minCellX; x <= maxCellX; x++)
                {
                    var key = MakeCellKey(x, z);
                    List<EdgeCandidate> bucket;

                    if (!_edgeGrid.TryGetValue(key, out bucket))
                    {
                        bucket = new List<EdgeCandidate>(4);
                        _edgeGrid[key] = bucket;
                    }

                    bucket.Add(candidate);
                }
            }
        }

        private static float ProjectHorizontal(Bezier4x3 bezier, float3 point, out float splitPosition, out float3 projected)
        {
            var bestT = 0f;
            var bestPosition = bezier.a;
            var bestDistanceSq = HorizontalDistanceSq(XZ(point), XZ(bestPosition));
            var sampleStep = 1f / ProjectionSamples;

            for (var i = 1; i <= ProjectionSamples; i++)
            {
                var t = i * sampleStep;
                var position = Evaluate(bezier, t);
                var distanceSq = HorizontalDistanceSq(XZ(point), XZ(position));

                if (distanceSq < bestDistanceSq)
                {
                    bestDistanceSq = distanceSq;
                    bestT = t;
                    bestPosition = position;
                }
            }

            var left = math.max(0f, bestT - sampleStep);
            var right = math.min(1f, bestT + sampleStep);

            for (var i = 0; i < ProjectionRefinementIterations; i++)
            {
                var t1 = left + (right - left) / 3f;
                var t2 = right - (right - left) / 3f;
                var p1 = Evaluate(bezier, t1);
                var p2 = Evaluate(bezier, t2);
                var d1 = HorizontalDistanceSq(XZ(point), XZ(p1));
                var d2 = HorizontalDistanceSq(XZ(point), XZ(p2));

                if (d1 < d2)
                    right = t2;
                else
                    left = t1;
            }

            bestT = (left + right) * 0.5f;
            bestPosition = Evaluate(bezier, bestT);
            bestDistanceSq = HorizontalDistanceSq(XZ(point), XZ(bestPosition));

            splitPosition = bestT;
            projected = bestPosition;
            return bestDistanceSq;
        }

        private static float3 Evaluate(Bezier4x3 bezier, float t)
        {
            var u = 1f - t;
            var uu = u * u;
            var tt = t * t;

            return
                bezier.a * (uu * u) +
                bezier.b * (3f * uu * t) +
                bezier.c * (3f * u * tt) +
                bezier.d * (tt * t);
        }

        private static float HorizontalDistanceSq(float2 a, float2 b)
        {
            var dx = a.x - b.x;
            var dz = a.y - b.y;
            return dx * dx + dz * dz;
        }

        private static float2 XZ(float3 value)
        {
            return new float2(value.x, value.z);
        }

        private static int Quantize(float value, float cellSize)
        {
            return (int)math.floor(value / cellSize);
        }

        private static long MakeCellKey(int x, int z)
        {
            return ((long)x << 32) ^ (uint)z;
        }

        private static string F(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private struct NodeCandidate
        {
            internal Entity Entity;
            internal float3 Position;
        }

        private struct EdgeCandidate
        {
            internal Entity Entity;
            internal Bezier4x3 Bezier;
        }
    }
}
