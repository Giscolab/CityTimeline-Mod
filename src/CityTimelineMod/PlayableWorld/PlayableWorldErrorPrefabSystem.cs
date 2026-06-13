using Game;
using Game.Prefabs;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using UnityEngine.Scripting;

namespace CityTimelineMod.PlayableWorld
{
    [Preserve]
    [UpdateBefore(typeof(ValidationSystem))]
    internal sealed partial class PlayableWorldErrorPrefabSystem
        : GameSystemBase
    {
        private EntityQuery _errorPrefabQuery;
        private bool _completed;

        protected override void OnCreate()
        {
            base.OnCreate();

            _errorPrefabQuery = GetEntityQuery(
                ComponentType.ReadOnly<ToolErrorData>()
            );

            Util.Log.Info(
                "[PlayableWorld] error-prefab system created."
            );
        }

        protected override void OnUpdate()
        {
            if (_completed)
                return;

            if (_errorPrefabQuery.IsEmptyIgnoreFilter)
                return;

            int removed = 0;

            using (
                NativeArray<Entity> entities =
                    _errorPrefabQuery.ToEntityArray(
                        Allocator.Temp
                    )
            )
            using (
                NativeArray<ToolErrorData> errorData =
                    _errorPrefabQuery.ToComponentDataArray<ToolErrorData>(
                        Allocator.Temp
                    )
            )
            {
                for (int i = 0; i < entities.Length; i++)
                {
                    if (
                        errorData[i].m_Error !=
                        ErrorType.ExceedsCityLimits
                    )
                    {
                        continue;
                    }

                    EntityManager.RemoveComponent<ToolErrorData>(
                        entities[i]
                    );

                    removed++;
                }
            }

            if (removed == 0)
                return;

            _completed = true;
            Enabled = false;

            Util.Log.Info(
                "[PlayableWorld] ExceedsCityLimits error prefab " +
                "disabled. removed=" +
                removed
            );
        }
    }
}
