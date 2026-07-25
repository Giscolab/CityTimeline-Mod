using Colossal.Mathematics;
using Game;
using Game.Prefabs;
using Game.Simulation;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.Scripting;

namespace CityTimelineMod.PlayableWorld
{
    /*
     * Corrige le profil vertical des nouveaux réseaux terrestres
     * (routes et voies ferrées) qui utilisent le backdrop
     * hors des ±7168 m.
     *
     * La courbe XZ reste strictement inchangée.
     * Aucun champ du TerrainSystem n'est modifié.
     */
    [Preserve]
    [UpdateBefore(typeof(GenerateNodesSystem))]
    internal sealed partial class PlayableWorldRoadProfileSystem
        : GameSystemBase
    {
        private TerrainSystem _terrainSystem;
        private EntityQuery _courseQuery;

        protected override void OnCreate()
        {
            base.OnCreate();

            _terrainSystem =
                World.GetOrCreateSystemManaged<TerrainSystem>();

            _courseQuery = GetEntityQuery(
                ComponentType.ReadWrite<NetCourse>(),
                ComponentType.ReadOnly<CreationDefinition>()
            );

            Util.Log.Info(
                "[PlayableWorld] network-profile system created."
            );
        }

        protected override void OnUpdate()
        {
            if (!PlayableWorldState.Enabled)
                return;

            if (!PlayableWorldState.Initialized)
                return;

            if (_courseQuery.IsEmptyIgnoreFilter)
                return;

            TerrainHeightData terrainData =
                _terrainSystem.GetHeightData(false);

            if (!terrainData.isCreated)
                return;

            if (
                !terrainData.hasBackdrop ||
                !terrainData.downscaledHeights.IsCreated ||
                terrainData.downscaledHeights.Length == 0
            )
            {
                return;
            }

            Bounds3 detailedBounds =
                TerrainUtils.GetBounds(ref terrainData);

            int changed = 0;
            int changedRoads = 0;
            int changedTracks = 0;

            using (
                NativeArray<Entity> entities =
                    _courseQuery.ToEntityArray(
                        Allocator.Temp
                    )
            )
            using (
                NativeArray<NetCourse> courses =
                    _courseQuery
                        .ToComponentDataArray<NetCourse>(
                            Allocator.Temp
                        )
            )
            using (
                NativeArray<CreationDefinition> definitions =
                    _courseQuery
                        .ToComponentDataArray<CreationDefinition>(
                            Allocator.Temp
                        )
            )
            {
                for (int i = 0; i < entities.Length; i++)
                {
                    CreationDefinition definition =
                        definitions[i];

                    if (definition.m_Prefab == Entity.Null)
                        continue;

                    bool isRoad =
                        EntityManager.HasComponent<RoadData>(
                            definition.m_Prefab
                        );

                    bool isTrack =
                        EntityManager.HasComponent<TrackData>(
                            definition.m_Prefab
                        );

                    if (!isRoad && !isTrack)
                    {
                        continue;
                    }

                    if (
                        (
                            definition.m_Flags &
                            CreationFlags.Permanent
                        ) == 0
                    )
                    {
                        continue;
                    }

                    CreationFlags rejectedFlags =
                        CreationFlags.Delete |
                        CreationFlags.Select |
                        CreationFlags.Upgrade |
                        CreationFlags.Relocate |
                        CreationFlags.Lowered |
                        CreationFlags.SubElevation;

                    if (
                        (
                            definition.m_Flags &
                            rejectedFlags
                        ) != 0
                    )
                    {
                        continue;
                    }

                    NetCourse course = courses[i];

                    /*
                     * Ne pas modifier ponts, tunnels,
                     * routes surélevées ou réseaux attachés.
                     */
                    if (
                        math.any(
                            math.abs(course.m_Elevation) >
                            0.001f
                        ) ||
                        math.any(
                            math.abs(
                                course
                                    .m_StartPosition
                                    .m_Elevation
                            ) > 0.001f
                        ) ||
                        math.any(
                            math.abs(
                                course
                                    .m_EndPosition
                                    .m_Elevation
                            ) > 0.001f
                        ) ||
                        course.m_StartPosition.m_ParentMesh >= 0 ||
                        course.m_EndPosition.m_ParentMesh >= 0
                    )
                    {
                        continue;
                    }

                    if (
                        !TouchesBackdrop(
                            course,
                            detailedBounds
                        )
                    )
                    {
                        continue;
                    }

                    ApplyTerrainProfile(
                        ref course,
                        ref terrainData
                    );

                    EntityManager.SetComponentData(
                        entities[i],
                        course
                    );

                    changed++;

                    if (isTrack)
                        changedTracks++;
                    else
                        changedRoads++;
                }
            }

            if (changed > 0)
            {
                Util.Log.Info(
                    "[PlayableWorld] backdrop network profiles " +
                    "applied. courses=" +
                    changed +
                    ", roads=" +
                    changedRoads +
                    ", tracks=" +
                    changedTracks
                );
            }
        }

        private static bool TouchesBackdrop(
            NetCourse course,
            Bounds3 detailedBounds
        )
        {
            float3 p0 = course.m_Curve.a;
            float3 p1 = MathUtils.Position(
                course.m_Curve,
                1f / 3f
            );
            float3 p2 = MathUtils.Position(
                course.m_Curve,
                2f / 3f
            );
            float3 p3 = course.m_Curve.d;

            return
                IsOutsideDetailed(
                    p0,
                    detailedBounds
                ) ||
                IsOutsideDetailed(
                    p1,
                    detailedBounds
                ) ||
                IsOutsideDetailed(
                    p2,
                    detailedBounds
                ) ||
                IsOutsideDetailed(
                    p3,
                    detailedBounds
                );
        }

        private static bool IsOutsideDetailed(
            float3 position,
            Bounds3 bounds
        )
        {
            return
                position.x < bounds.min.x ||
                position.x > bounds.max.x ||
                position.z < bounds.min.z ||
                position.z > bounds.max.z;
        }

        private static void ApplyTerrainProfile(
            ref NetCourse course,
            ref TerrainHeightData terrainData
        )
        {
            Bezier4x3 curve = course.m_Curve;

            /*
             * Les points XZ restent intacts.
             * Seules leurs hauteurs Y sont recalculées.
             */
            if (
                course.m_StartPosition.m_Entity ==
                Entity.Null
            )
            {
                curve.a.y = TerrainUtils.SampleHeight(
                    ref terrainData,
                    curve.a
                );
            }
            else
            {
                curve.a.y =
                    course.m_StartPosition.m_Position.y;
            }

            if (
                course.m_EndPosition.m_Entity ==
                Entity.Null
            )
            {
                curve.d.y = TerrainUtils.SampleHeight(
                    ref terrainData,
                    curve.d
                );
            }
            else
            {
                curve.d.y =
                    course.m_EndPosition.m_Position.y;
            }

            float3 position1 = MathUtils.Position(
                curve,
                1f / 3f
            );

            float3 position2 = MathUtils.Position(
                curve,
                2f / 3f
            );

            float height1 = TerrainUtils.SampleHeight(
                ref terrainData,
                position1
            );

            float height2 = TerrainUtils.SampleHeight(
                ref terrainData,
                position2
            );

            /*
             * Résolution exacte des hauteurs B et C
             * pour que la Bézier passe par :
             *
             * t = 0
             * t = 1/3
             * t = 2/3
             * t = 1
             */
            float remainder1 =
                height1 -
                (
                    8f * curve.a.y +
                    curve.d.y
                ) / 27f;

            float remainder2 =
                height2 -
                (
                    curve.a.y +
                    8f * curve.d.y
                ) / 27f;

            curve.b.y =
                3f * remainder1 -
                1.5f * remainder2;

            curve.c.y =
                3f * remainder2 -
                1.5f * remainder1;

            course.m_Curve = curve;

            course.m_StartPosition.m_Position.y =
                curve.a.y;

            course.m_EndPosition.m_Position.y =
                curve.d.y;

            course.m_Length =
                MathUtils.Length(curve);
        }
    }
}
