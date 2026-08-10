using System;
using System.Globalization;
using Game.Common;
using Game.Net;
using Game.Prefabs;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;

namespace CityTimelineMod.Roads
{
    internal static partial class RuntimeRoadSpawner
    {
        internal static int DumpImportedRoadDiagnostics(int maxEdges = 20)
        {
            var world = World.DefaultGameObjectInjectionWorld;

            if (world == null)
            {
                Util.Log.Info("[RoadImportDiag] blocked: World null.");
                return 0;
            }

            EntityQuery query = default(EntityQuery);
            var queryCreated = false;

            try
            {
                var entityManager = world.EntityManager;

                query = entityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<ImportedRoadTag>(),
                    ComponentType.ReadOnly<Edge>()
                );
                queryCreated = true;

                using (var edges = query.ToEntityArray(Allocator.Temp))
                {
                    var total = edges.Length;
                    var limit = Math.Max(0, Math.Min(maxEdges, total));

                    var hasRoadCount = 0;
                    var hasCurveCount = 0;
                    var hasPrefabRefCount = 0;
                    var hasCompositionCount = 0;
                    var hasEdgeGeometryCount = 0;
                    var hasStartNodeGeometryCount = 0;
                    var hasEndNodeGeometryCount = 0;
                    var hasTempCount = 0;
                    var hasUpdatedCount = 0;

                    Util.Log.Info(
                        "[RoadImportDiag] tagged imported road edges=" + total +
                        ", logging=" + limit
                    );

                    for (var i = 0; i < total; i++)
                    {
                        var edgeEntity = edges[i];

                        if (!entityManager.Exists(edgeEntity))
                        {
                            if (i < limit)
                                Util.Log.Info("[RoadImportDiag] edge#" + i + " entity=" + edgeEntity.Index + " exists=False");

                            continue;
                        }

                        var hasEdge = entityManager.HasComponent<Edge>(edgeEntity);
                        var hasRoad = entityManager.HasComponent<Road>(edgeEntity);
                        var hasCurve = entityManager.HasComponent<Curve>(edgeEntity);
                        var hasPrefabRef = entityManager.HasComponent<PrefabRef>(edgeEntity);
                        var hasComposition = entityManager.HasComponent<Composition>(edgeEntity);
                        var hasEdgeGeometry = entityManager.HasComponent<EdgeGeometry>(edgeEntity);
                        var hasStartNodeGeometry = entityManager.HasComponent<StartNodeGeometry>(edgeEntity);
                        var hasEndNodeGeometry = entityManager.HasComponent<EndNodeGeometry>(edgeEntity);
                        var hasTemp = entityManager.HasComponent<Temp>(edgeEntity);
                        var hasUpdated = entityManager.HasComponent<Updated>(edgeEntity);

                        if (hasRoad)
                            hasRoadCount++;

                        if (hasCurve)
                            hasCurveCount++;

                        if (hasPrefabRef)
                            hasPrefabRefCount++;

                        if (hasComposition)
                            hasCompositionCount++;

                        if (hasEdgeGeometry)
                            hasEdgeGeometryCount++;

                        if (hasStartNodeGeometry)
                            hasStartNodeGeometryCount++;

                        if (hasEndNodeGeometry)
                            hasEndNodeGeometryCount++;

                        if (hasTemp)
                            hasTempCount++;

                        if (hasUpdated)
                            hasUpdatedCount++;

                        if (i >= limit)
                            continue;

                        var prefabEntity = Entity.Null;

                        if (hasPrefabRef)
                            prefabEntity = entityManager.GetComponentData<PrefabRef>(edgeEntity).m_Prefab;

                        var curveLength = -1f;

                        if (hasCurve)
                            curveLength = entityManager.GetComponentData<Curve>(edgeEntity).m_Length;

                        var startNode = Entity.Null;
                        var endNode = Entity.Null;

                        if (hasEdge)
                        {
                            var edge = entityManager.GetComponentData<Edge>(edgeEntity);
                            startNode = edge.m_Start;
                            endNode = edge.m_End;
                        }

                        Util.Log.Info(
                            "[RoadImportDiag] edge#" + i +
                            " entity=" + edgeEntity.Index +
                            " Edge=" + hasEdge +
                            " Road=" + hasRoad +
                            " Curve=" + hasCurve +
                            " PrefabRef=" + hasPrefabRef +
                            " PrefabEntity=" + FormatEntity(prefabEntity) +
                            " Composition=" + hasComposition +
                            " EdgeGeometry=" + hasEdgeGeometry +
                            " StartNodeGeometry=" + hasStartNodeGeometry +
                            " EndNodeGeometry=" + hasEndNodeGeometry +
                            " Temp=" + hasTemp +
                            " Updated=" + hasUpdated +
                            " CurveLength=" + curveLength.ToString("0.###", CultureInfo.InvariantCulture)
                        );

                        Util.Log.Info(
                            "[RoadImportDiag] edge#" + i +
                            " start=" + DescribeImportedNode(entityManager, startNode) +
                            " end=" + DescribeImportedNode(entityManager, endNode)
                        );

                        Util.Log.Info(
                            "[RoadImportDiag] edge#" + i +
                            " components=" + DumpComponentTypes(entityManager, edgeEntity)
                        );

                        if (startNode != Entity.Null && entityManager.Exists(startNode))
                        {
                            Util.Log.Info(
                                "[RoadImportDiag] edge#" + i +
                                " startNodeComponents=" + DumpComponentTypes(entityManager, startNode)
                            );
                        }

                        if (endNode != Entity.Null && entityManager.Exists(endNode))
                        {
                            Util.Log.Info(
                                "[RoadImportDiag] edge#" + i +
                                " endNodeComponents=" + DumpComponentTypes(entityManager, endNode)
                            );
                        }
                    }

                    Util.Log.Info(
                        "[RoadImportDiag] summary taggedEdges=" + total +
                        ", Road=" + hasRoadCount +
                        ", Curve=" + hasCurveCount +
                        ", PrefabRef=" + hasPrefabRefCount +
                        ", Composition=" + hasCompositionCount +
                        ", EdgeGeometry=" + hasEdgeGeometryCount +
                        ", StartNodeGeometry=" + hasStartNodeGeometryCount +
                        ", EndNodeGeometry=" + hasEndNodeGeometryCount +
                        ", Temp=" + hasTempCount +
                        ", Updated=" + hasUpdatedCount
                    );

                    if (total > 0 && hasEdgeGeometryCount == 0)
                    {
                        Util.Log.Info("[RoadImportDiag] verdict: Edge/Road entities exist, but no EdgeGeometry was generated. Problem is prefab/finalization, not GeoJSON.");
                    }
                    else if (total > 0)
                    {
                        Util.Log.Info("[RoadImportDiag] verdict: EdgeGeometry exists. If roads are invisible, check position/camera/height.");
                    }

                    return total;
                }
            }
            catch (Exception ex)
            {
                Util.Log.Error("[RoadImportDiag] failed: " + ex);
                return 0;
            }
            finally
            {
                if (queryCreated)
                    query.Dispose();
            }
        }

        private static string DescribeImportedNode(EntityManager entityManager, Entity nodeEntity)
        {
            if (nodeEntity == Entity.Null)
                return "Entity.Null";

            if (!entityManager.Exists(nodeEntity))
                return "#" + nodeEntity.Index + " exists=False";

            var hasNode = entityManager.HasComponent<Node>(nodeEntity);
            var hasTag = entityManager.HasComponent<ImportedRoadTag>(nodeEntity);
            var hasTemp = entityManager.HasComponent<Temp>(nodeEntity);
            var hasUpdated = entityManager.HasComponent<Updated>(nodeEntity);
            var position = "n/a";

            if (hasNode)
            {
                var node = entityManager.GetComponentData<Node>(nodeEntity);
                position = FormatFloat3(node.m_Position);
            }

            return
                "#" + nodeEntity.Index +
                " exists=True" +
                " Node=" + hasNode +
                " ImportedRoadTag=" + hasTag +
                " Temp=" + hasTemp +
                " Updated=" + hasUpdated +
                " pos=" + position;
        }
    }
}
