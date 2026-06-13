using System;
using System.Collections.Generic;
using Game.Net;
using Game.Prefabs;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace CityTimelineMod.Roads
{
    internal static partial class RuntimeRoadSpawner
    {
        internal static int UndoImportedRoads()
        {
            Util.Log.Info("[RoadImport] undo disabled: DEV/V1 safety. Direct EntityManager.DestroyEntity on CS2 net entities is not used.");
            _lastImportKey = null;
            return 0;
        }

        internal static void ForceQueueOneTestRoad(World world)
        {
            try
            {
                if (world == null)
                {
                    Util.Log.Info("[RoadImport] World null, impossible de créer la route test.");
                    return;
                }

                var entityManager = world.EntityManager;

                using (var prefabs = entityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<Game.Prefabs.RoadData>()
                ).ToEntityArray(Unity.Collections.Allocator.Temp))
                {
                    if (prefabs.Length == 0)
                    {
                        Util.Log.Info("[RoadImport] Aucun prefab RoadData trouvé.");
                        return;
                    }

                    var prefabEntity = prefabs[0];
                    var tool = world.GetOrCreateSystemManaged<RuntimeRoadToolSystem>();

                    tool.QueueTestRoad(
                        prefabEntity,
                        new float3(0f, 50f, 0f),
                        new float3(200f, 50f, 0f)
                    );

                    Util.Log.Info("[RoadImport] Route test runtime envoyée vers RuntimeRoadToolSystem. prefabEntity=" + prefabEntity.Index);
                }
            }
            catch (Exception ex)
            {
                Util.Log.Error("[RoadImport] Erreur pendant ForceQueueOneTestRoad: " + ex);
            }
        }

        private static RoadPrefabResolver EnsureResolver(EntityManager entityManager, PrefabSystem prefabSystem)
        {
            if (_resolver == null)
                _resolver = new RoadPrefabResolver(entityManager, prefabSystem);

            return _resolver;
        }

        private static HashSet<int> CaptureRoadEdgeEntityIndices(EntityManager entityManager)
        {
            var result = new HashSet<int>();
            EntityQuery query = default(EntityQuery);
            var queryCreated = false;

            try
            {
                query = entityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<Edge>(),
                    ComponentType.ReadOnly<Road>()
                );
                queryCreated = true;

                using (var entities = query.ToEntityArray(Allocator.Temp))
                {
                    for (var i = 0; i < entities.Length; i++)
                        result.Add(entities[i].Index);
                }
            }
            catch (Exception ex)
            {
                Util.Log.Error("[RoadImport] Could not capture road Edge/Road baseline: " + ex.Message);
            }
            finally
            {
                if (queryCreated)
                    query.Dispose();
            }

            return result;
        }

        private static int DeleteTaggedImportedEdges(EntityManager entityManager)
        {
            EntityQuery query = default(EntityQuery);
            var queryCreated = false;

            try
            {
                query = entityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<ImportedRoadTag>(),
                    ComponentType.ReadOnly<Edge>()
                );
                queryCreated = true;

                using (var entities = query.ToEntityArray(Allocator.Temp))
                {
                    var count = entities.Length;

                    if (count > 0)
                        entityManager.DestroyEntity(entities);

                    return count;
                }
            }
            catch (Exception ex)
            {
                Util.Log.Error("[RoadImport] Failed to delete tagged imported edges: " + ex.Message);
                return 0;
            }
            finally
            {
                if (queryCreated)
                    query.Dispose();
            }
        }

        private static int DeleteTaggedOrphanNodes(EntityManager entityManager)
        {
            EntityQuery taggedNodeQuery = default(EntityQuery);
            EntityQuery edgeQuery = default(EntityQuery);
            var taggedNodeQueryCreated = false;
            var edgeQueryCreated = false;

            try
            {
                var referencedNodeIndices = new HashSet<int>();

                edgeQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<Edge>());
                edgeQueryCreated = true;

                using (var edges = edgeQuery.ToEntityArray(Allocator.Temp))
                {
                    for (var i = 0; i < edges.Length; i++)
                    {
                        var edgeEntity = edges[i];

                        if (!entityManager.Exists(edgeEntity) || !entityManager.HasComponent<Edge>(edgeEntity))
                            continue;

                        var edge = entityManager.GetComponentData<Edge>(edgeEntity);

                        if (edge.m_Start != Entity.Null)
                            referencedNodeIndices.Add(edge.m_Start.Index);

                        if (edge.m_End != Entity.Null)
                            referencedNodeIndices.Add(edge.m_End.Index);
                    }
                }

                taggedNodeQuery = entityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<ImportedRoadTag>(),
                    ComponentType.ReadOnly<Node>()
                );
                taggedNodeQueryCreated = true;

                var toDelete = new NativeList<Entity>(Allocator.Temp);

                try
                {
                    using (var nodes = taggedNodeQuery.ToEntityArray(Allocator.Temp))
                    {
                        for (var i = 0; i < nodes.Length; i++)
                        {
                            var nodeEntity = nodes[i];

                            if (!entityManager.Exists(nodeEntity) || !entityManager.HasComponent<Node>(nodeEntity))
                                continue;

                            if (!referencedNodeIndices.Contains(nodeEntity.Index))
                                toDelete.Add(nodeEntity);
                        }
                    }

                    var count = toDelete.Length;

                    if (count > 0)
                        entityManager.DestroyEntity(toDelete.AsArray());

                    return count;
                }
                finally
                {
                    if (toDelete.IsCreated)
                        toDelete.Dispose();
                }
            }
            catch (Exception ex)
            {
                Util.Log.Error("[RoadImport] Failed to delete tagged orphan nodes: " + ex.Message);
                return 0;
            }
            finally
            {
                if (edgeQueryCreated)
                    edgeQuery.Dispose();

                if (taggedNodeQueryCreated)
                    taggedNodeQuery.Dispose();
            }
        }
    }
}
