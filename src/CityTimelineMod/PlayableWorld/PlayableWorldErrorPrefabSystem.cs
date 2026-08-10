using System;
using System.Collections.Generic;
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
        private readonly Dictionary<Entity, ToolErrorData> _removedErrorPrefabs =
            new Dictionary<Entity, ToolErrorData>();

        private bool _runtimeActive;
        private bool _completed;
        private bool _restorePending;

        protected override void OnCreate()
        {
            base.OnCreate();

            _errorPrefabQuery = GetEntityQuery(
                ComponentType.ReadOnly<ToolErrorData>()
            );

            // Registration and mutation are separate lifecycle steps. A system
            // reused after a hot reload must remain inert until Mod has verified
            // the PlayableWorld Harmony owner and the active World.
            _runtimeActive = false;
            _completed = false;
            _restorePending = false;
            Enabled = false;

            Util.Log.Info(
                "[PlayableWorld] error-prefab system created."
            );
        }

        protected override void OnDestroy()
        {
            try
            {
                if (!DeactivateAndRestore())
                {
                    Util.Log.Error(
                        "[PlayableWorld] error-prefab destroy cleanup incomplete."
                    );
                }
            }
            catch (Exception ex)
            {
                Util.Log.Error(
                    "[PlayableWorld] error-prefab destroy cleanup failed: " + ex
                );
            }

            base.OnDestroy();
        }

        internal bool ActivateForRuntime()
        {
            if (_runtimeActive && CanActivateForCurrentWorld())
            {
                if (!_completed)
                    Enabled = true;

                return true;
            }

            _runtimeActive = false;
            Enabled = false;

            if (!RestoreRemovedErrorPrefabs("activation preflight"))
            {
                _restorePending = true;
                Enabled = true;
                Util.Log.Error(
                    "[PlayableWorld] error-prefab activation blocked: " +
                    "a previous mutation could not be restored."
                );
                return false;
            }

            _restorePending = false;
            if (!CanActivateForCurrentWorld())
            {
                Util.Log.Error(
                    "[PlayableWorld] error-prefab activation blocked: " +
                    "runtime, module, or World authorization is unavailable."
                );
                return false;
            }

            _completed = false;
            _runtimeActive = true;
            Enabled = true;

            Util.Log.Info(
                "[PlayableWorld] error-prefab system activated for World sequence=" +
                World.SequenceNumber + "."
            );

            return true;
        }

        internal bool DeactivateAndRestore()
        {
            _runtimeActive = false;
            _completed = false;

            var complete = RestoreRemovedErrorPrefabs("runtime teardown");
            _restorePending = !complete;
            Enabled = !complete;
            return complete;
        }

        protected override void OnUpdate()
        {
            if (_restorePending)
            {
                if (RestoreRemovedErrorPrefabs("deferred runtime teardown"))
                {
                    _restorePending = false;
                    Enabled = false;
                }

                return;
            }

            if (_runtimeActive && !CanMutateCurrentWorld())
            {
                DeactivateAndRestore();
                return;
            }

            if (!CanMutateCurrentWorld())
                return;

            if (_completed)
                return;

            if (_errorPrefabQuery.IsEmptyIgnoreFilter)
                return;

            try
            {
                Dependency.Complete();
            }
            catch (Exception ex)
            {
                Util.Log.Error(
                    "[PlayableWorld] error-prefab dependency completion failed; " +
                    "update skipped. " + ex
                );
                return;
            }

            if (!CanMutateCurrentWorld())
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
                    if (!CanMutateCurrentWorld())
                        return;

                    if (
                        errorData[i].m_Error !=
                        ErrorType.ExceedsCityLimits
                    )
                    {
                        continue;
                    }

                    if (!_removedErrorPrefabs.ContainsKey(entities[i]))
                        _removedErrorPrefabs.Add(entities[i], errorData[i]);

                    if (!EntityManager.RemoveComponent<ToolErrorData>(entities[i]))
                    {
                        _removedErrorPrefabs.Remove(entities[i]);
                        continue;
                    }

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

        private bool CanActivateForCurrentWorld()
        {
            return
                Mod.RuntimeEnabled &&
                Mod.IsActiveRuntimeWorld(World) &&
                PlayableWorldState.Installed &&
                PlayableWorldState.HasActiveWorld &&
                World != null &&
                World.IsCreated &&
                World.SequenceNumber == PlayableWorldState.ActiveWorldSequence;
        }

        private bool CanMutateCurrentWorld()
        {
            return
                _runtimeActive &&
                Mod.IsActiveRuntimeWorld(World) &&
                PlayableWorldState.Enabled &&
                World != null &&
                World.IsCreated &&
                World.SequenceNumber == PlayableWorldState.ActiveWorldSequence;
        }

        private bool RestoreRemovedErrorPrefabs(string stage)
        {
            if (_removedErrorPrefabs.Count == 0)
                return true;

            if (World == null || !World.IsCreated)
            {
                // The entities cannot survive destruction of their World. Drop
                // stale Entity handles instead of carrying them into a new one.
                _removedErrorPrefabs.Clear();
                return true;
            }

            try
            {
                Dependency.Complete();
            }
            catch (Exception ex)
            {
                Util.Log.Error(
                    "[PlayableWorld] error-prefab " + stage +
                    " dependency completion failed: " + ex
                );
                return false;
            }

            bool complete = true;
            int restored = 0;
            var resolved = new List<Entity>();

            foreach (var pair in _removedErrorPrefabs)
            {
                try
                {
                    if (!EntityManager.Exists(pair.Key))
                    {
                        resolved.Add(pair.Key);
                        continue;
                    }

                    // Never overwrite a value recreated or changed by the game
                    // or another mod after CTM removed its original component.
                    if (EntityManager.HasComponent<ToolErrorData>(pair.Key))
                    {
                        resolved.Add(pair.Key);
                        continue;
                    }

                    if (!EntityManager.AddComponentData(pair.Key, pair.Value))
                    {
                        complete = false;
                        continue;
                    }

                    resolved.Add(pair.Key);
                    restored++;
                }
                catch (Exception ex)
                {
                    complete = false;
                    Util.Log.Error(
                        "[PlayableWorld] error-prefab " + stage +
                        " restore failed for entity=" + pair.Key.Index + ":" +
                        pair.Key.Version + ". " + ex
                    );
                }
            }

            for (int i = 0; i < resolved.Count; i++)
                _removedErrorPrefabs.Remove(resolved[i]);

            if (restored > 0)
            {
                Util.Log.Info(
                    "[PlayableWorld] error-prefab components restored=" +
                    restored + "."
                );
            }

            return complete && _removedErrorPrefabs.Count == 0;
        }
    }
}
