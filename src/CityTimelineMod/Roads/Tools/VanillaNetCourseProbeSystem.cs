using System;
using System.Collections.Generic;
using System.Globalization;
using Game;
using Game.Common;
using Game.Net;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace CityTimelineMod.Roads
{
    public partial class VanillaNetCourseProbeSystem : GameSystemBase
    {
        private EntityQuery _anyTempQuery;
        private EntityQuery _anyNetCourseQuery;
        private EntityQuery _creationNetCourseQuery;
        private EntityQuery _tempCreationNetCourseQuery;
        private EntityQuery _edgeRoadQuery;
        private readonly HashSet<string> _loggedCreationNetCourses = new HashSet<string>();
        private int _frames;

        protected override void OnCreate()
        {
            base.OnCreate();

            _anyTempQuery = GetEntityQuery(
                ComponentType.ReadOnly<Temp>()
            );

            _anyNetCourseQuery = GetEntityQuery(
                ComponentType.ReadOnly<NetCourse>()
            );

            _creationNetCourseQuery = GetEntityQuery(
                ComponentType.ReadOnly<CreationDefinition>(),
                ComponentType.ReadOnly<NetCourse>()
            );

            _tempCreationNetCourseQuery = GetEntityQuery(
                ComponentType.ReadOnly<Temp>(),
                ComponentType.ReadOnly<CreationDefinition>(),
                ComponentType.ReadOnly<NetCourse>()
            );

            _edgeRoadQuery = GetEntityQuery(
                ComponentType.ReadOnly<Edge>(),
                ComponentType.ReadOnly<Road>(),
                ComponentType.ReadOnly<Curve>()
            );

            Enabled = true;

            Util.Log.Info("[VanillaProbe] VanillaNetCourseProbeSystem OnCreate.");
        }

        protected override void OnUpdate()
        {
            _frames++;

            if (_frames % 30 == 0)
            {
                Util.Log.Info(
                    "[VanillaProbe][Alive] " +
                    "frame=" + _frames +
                    ", temp=" + _anyTempQuery.CalculateEntityCount() +
                    ", anyNetCourse=" + _anyNetCourseQuery.CalculateEntityCount() +
                    ", creationNetCourse=" + _creationNetCourseQuery.CalculateEntityCount() +
                    ", tempCreationNetCourse=" + _tempCreationNetCourseQuery.CalculateEntityCount() +
                    ", edgeRoadCurve=" + _edgeRoadQuery.CalculateEntityCount()
                );
            }

            if (_creationNetCourseQuery.IsEmpty)
                return;

            try
            {
                using (var entities = _creationNetCourseQuery.ToEntityArray(Allocator.Temp))
                {
                    for (var i = 0; i < entities.Length; i++)
                    {
                        var entity = entities[i];
                        var logKey = entity.Index + ":" + entity.Version;

                        if (_loggedCreationNetCourses.Contains(logKey))
                            continue;

                        _loggedCreationNetCourses.Add(logKey);

                        if (!EntityManager.Exists(entity))
                            continue;

                        var creation = EntityManager.GetComponentData<CreationDefinition>(entity);
                        var course = EntityManager.GetComponentData<NetCourse>(entity);

                        var hasTemp = EntityManager.HasComponent<Temp>(entity);
                        var hasUpdated = EntityManager.HasComponent<Updated>(entity);
                        var hasOwnerDefinition = EntityManager.HasComponent<OwnerDefinition>(entity);

                        Util.Log.Info(
                            "[VanillaProbe][CreationNetCourse] entity=" + FormatEntity(entity) +
                            ", hasTemp=" + hasTemp +
                            ", hasUpdated=" + hasUpdated +
                            ", hasOwnerDefinition=" + hasOwnerDefinition +
                            ", prefab=" + FormatEntity(creation.m_Prefab) +
                            ", subPrefab=" + FormatEntity(creation.m_SubPrefab) +
                            ", owner=" + FormatEntity(creation.m_Owner) +
                            ", original=" + FormatEntity(creation.m_Original) +
                            ", attached=" + FormatEntity(creation.m_Attached) +
                            ", randomSeed=" + creation.m_RandomSeed +
                            ", creationFlags=" + creation.m_Flags +
                            ", length=" + F(course.m_Length) +
                            ", elevation=(" + F(course.m_Elevation.x) +
                            "," + F(course.m_Elevation.y) + ")" +
                            ", fixedIndex=" + course.m_FixedIndex
                        );

                        DumpCoursePos("start", course.m_StartPosition);
                        DumpCoursePos("end", course.m_EndPosition);
                        DumpReferencedEntity("start", course.m_StartPosition.m_Entity);
                        DumpReferencedEntity("end", course.m_EndPosition.m_Entity);

                        Util.Log.Info(
                            "[VanillaProbe][Curve] entity=" + FormatEntity(entity) +
                            ", a=" + F3(course.m_Curve.a) +
                            ", b=" + F3(course.m_Curve.b) +
                            ", c=" + F3(course.m_Curve.c) +
                            ", d=" + F3(course.m_Curve.d)
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                Util.Log.Error("[VanillaProbe] failed to dump CreationDefinition+NetCourse: " + ex);
            }
        }

        private void DumpReferencedEntity(string label, Entity entity)
        {
            if (entity == Entity.Null || !EntityManager.Exists(entity))
                return;

            Util.Log.Info(
                "[VanillaProbe][ReferencedEntity] " + label +
                ", entity=" + FormatEntity(entity) +
                ", hasNode=" + EntityManager.HasComponent<Node>(entity) +
                ", hasEdge=" + EntityManager.HasComponent<Edge>(entity) +
                ", hasRoad=" + EntityManager.HasComponent<Road>(entity) +
                ", hasCurve=" + EntityManager.HasComponent<Curve>(entity) +
                ", hasTemp=" + EntityManager.HasComponent<Temp>(entity) +
                ", hasDeleted=" + EntityManager.HasComponent<Deleted>(entity)
            );
        }

        private static void DumpCoursePos(string label, CoursePos pos)
        {
            Util.Log.Info(
                "[VanillaProbe][CoursePos] " + label +
                ", entity=" + FormatEntity(pos.m_Entity) +
                ", position=" + F3(pos.m_Position) +
                ", elevation=(" + F(pos.m_Elevation.x) +
                "," + F(pos.m_Elevation.y) + ")" +
                ", courseDelta=" + F(pos.m_CourseDelta) +
                ", splitPosition=" + F(pos.m_SplitPosition) +
                ", flags=" + pos.m_Flags +
                ", parentMesh=" + pos.m_ParentMesh
            );
        }

        private static string FormatEntity(Entity entity)
        {
            return entity == Entity.Null ? "Entity.Null" : "#" + entity.Index + ":" + entity.Version;
        }

        private static string F(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string F3(float3 value)
        {
            return "(" +
                F(value.x) + "," +
                F(value.y) + "," +
                F(value.z) +
                ")";
        }
    }
}
