using System;
using System.Globalization;
using Game.Net;
using Game.Prefabs;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace CityTimelineMod.Roads
{
    internal static partial class RuntimeRoadSpawner
    {
        internal static int DumpNearbyVanillaRoadDiagnostics(int maxEdges = 30, float maxDistance = 10000f)
        {
            return DumpNearbyVanillaRoadDiagnosticsDetailed(maxEdges, maxDistance);
        }

        private static int DumpNearbyVanillaRoadDiagnosticsDetailed(int maxEdges = 30, float maxDistance = 10000f)
        {
            var world = World.DefaultGameObjectInjectionWorld;

            if (world == null)
            {
                Util.Log.Info("[RoadVanillaPlaced] diagnostic blocked: World null.");
                return 0;
            }

            EntityQuery query = default(EntityQuery);
            var queryCreated = false;

            try
            {
                var entityManager = world.EntityManager;
                var resolver = EnsureResolverForDiagnostics();

                query = entityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<Edge>(),
                    ComponentType.ReadOnly<Road>()
                );
                queryCreated = true;

                var center = new float2(GeoRoadImportPlacement.CenterX, GeoRoadImportPlacement.CenterZ);
                var logged = 0;
                var scanned = 0;
                var skippedImported = 0;
                var skippedTemp = 0;
                var skippedDistance = 0;
                var skippedInvalidNodes = 0;

                using (var edges = query.ToEntityArray(Allocator.Temp))
                {
                    for (var i = 0; i < edges.Length && logged < maxEdges; i++)
                    {
                        var edgeEntity = edges[i];
                        scanned++;

                        if (!entityManager.Exists(edgeEntity))
                            continue;

                        if (entityManager.HasComponent<ImportedRoadTag>(edgeEntity))
                        {
                            skippedImported++;
                            continue;
                        }

                        if (entityManager.HasComponent<Temp>(edgeEntity))
                        {
                            skippedTemp++;
                            continue;
                        }

                        if (!entityManager.HasComponent<Edge>(edgeEntity))
                            continue;

                        var edge = entityManager.GetComponentData<Edge>(edgeEntity);

                        if (edge.m_Start == Entity.Null ||
                            edge.m_End == Entity.Null ||
                            !entityManager.Exists(edge.m_Start) ||
                            !entityManager.Exists(edge.m_End) ||
                            !entityManager.HasComponent<Node>(edge.m_Start) ||
                            !entityManager.HasComponent<Node>(edge.m_End))
                        {
                            skippedInvalidNodes++;
                            continue;
                        }

                        var start = entityManager.GetComponentData<Node>(edge.m_Start).m_Position;
                        var end = entityManager.GetComponentData<Node>(edge.m_End).m_Position;
                        var mid = new float2((start.x + end.x) * 0.5f, (start.z + end.z) * 0.5f);
                        var distance = math.distance(mid, center);

                        if (distance > maxDistance)
                        {
                            skippedDistance++;
                            continue;
                        }

                        logged++;

                        var hasPrefabRef = entityManager.HasComponent<PrefabRef>(edgeEntity);
                        var prefabEntity = Entity.Null;

                        if (hasPrefabRef)
                            prefabEntity = entityManager.GetComponentData<PrefabRef>(edgeEntity).m_Prefab;

                        var prefabName = "<unresolved>";
                        var prefabType = "<null>";

                        if (resolver != null && prefabEntity != Entity.Null)
                        {
                            var prefabBase = resolver.GetPrefabBase(prefabEntity);
                            prefabName = resolver.GetPrefabName(prefabEntity);
                            prefabType = prefabBase != null ? prefabBase.GetType().FullName : "<null>";
                        }

                        var curveLength = -1f;

                        if (entityManager.HasComponent<Curve>(edgeEntity))
                            curveLength = entityManager.GetComponentData<Curve>(edgeEntity).m_Length;

                        Util.Log.Info(
                            "[RoadVanillaPlaced] edge#" + logged +
                            " entity=" + FormatEntity(edgeEntity) +
                            " distanceToCenter=" + distance.ToString("0.###", CultureInfo.InvariantCulture) +
                            " prefabEntity=" + FormatEntity(prefabEntity) +
                            " prefabName=" + Safe(prefabName) +
                            " prefabType=" + Safe(prefabType) +
                            " curveLength=" + curveLength.ToString("0.###", CultureInfo.InvariantCulture) +
                            " start=" + FormatFloat3(start) +
                            " end=" + FormatFloat3(end)
                        );

                        Util.Log.Info(
                            "[RoadVanillaPlaced] edge#" + logged +
                            " edgeComponents=" + DumpComponentTypes(entityManager, edgeEntity)
                        );

                        Util.Log.Info(
                            "[RoadVanillaPlaced] edge#" + logged +
                            " edge.PrefabRef=" + DumpComponentByFullName(entityManager, edgeEntity, "Game.Prefabs.PrefabRef", 12)
                        );

                        Util.Log.Info(
                            "[RoadVanillaPlaced] edge#" + logged +
                            " edge.Composition=" + DumpComponentByFullName(entityManager, edgeEntity, "Game.Net.Composition", 24)
                        );

                        Util.Log.Info(
                            "[RoadVanillaPlaced] edge#" + logged +
                            " edge.Curve=" + DumpComponentByFullName(entityManager, edgeEntity, "Game.Net.Curve", 24)
                        );

                        if (prefabEntity != Entity.Null && entityManager.Exists(prefabEntity))
                        {
                            Util.Log.Info(
                                "[RoadVanillaPlaced] edge#" + logged +
                                " prefabComponents=" + DumpComponentTypes(entityManager, prefabEntity)
                            );

                            Util.Log.Info(
                                "[RoadVanillaPlaced] edge#" + logged +
                                " prefab.PrefabData=" + DumpComponentByFullName(entityManager, prefabEntity, "Game.Prefabs.PrefabData", 24)
                            );

                            Util.Log.Info(
                                "[RoadVanillaPlaced] edge#" + logged +
                                " prefab.LoadedIndex=" + DumpComponentByFullName(entityManager, prefabEntity, "Game.Prefabs.LoadedIndex", 24)
                            );

                            Util.Log.Info(
                                "[RoadVanillaPlaced] edge#" + logged +
                                " prefab.RoadData=" + DumpComponentByFullName(entityManager, prefabEntity, "Game.Prefabs.RoadData", 40)
                            );

                            Util.Log.Info(
                                "[RoadVanillaPlaced] edge#" + logged +
                                " prefab.NetData=" + DumpComponentByFullName(entityManager, prefabEntity, "Game.Prefabs.NetData", 40)
                            );

                            Util.Log.Info(
                                "[RoadVanillaPlaced] edge#" + logged +
                                " prefab.ObjectData=" + DumpComponentByFullName(entityManager, prefabEntity, "Game.Prefabs.ObjectData", 32)
                            );

                            Util.Log.Info(
                                "[RoadVanillaPlaced] edge#" + logged +
                                " prefab.UIObjectData=" + DumpComponentByFullName(entityManager, prefabEntity, "Game.Prefabs.UIObjectData", 32)
                            );
                        }
                    }
                }

                Util.Log.Info(
                    "[RoadVanillaPlaced] summary scanned=" + scanned +
                    ", logged=" + logged +
                    ", skippedImported=" + skippedImported +
                    ", skippedTemp=" + skippedTemp +
                    ", skippedDistance=" + skippedDistance +
                    ", skippedInvalidNodes=" + skippedInvalidNodes +
                    ", maxDistance=" + maxDistance.ToString("0.###", CultureInfo.InvariantCulture)
                );

                return logged;
            }
            catch (Exception ex)
            {
                Util.Log.Error("[RoadVanillaPlaced] diagnostic failed: " + ex);
                return 0;
            }
            finally
            {
                if (queryCreated)
                    query.Dispose();
            }
        }
    }
}
