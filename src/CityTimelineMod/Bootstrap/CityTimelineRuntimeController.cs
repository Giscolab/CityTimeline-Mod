using System;
using System.Collections.Generic;
using UnityEngine;
using CityTimelineMod.Util;

namespace CityTimelineMod
{
    internal sealed class CityTimelineRuntimeController : MonoBehaviour
    {
        private const string RootName = "CityTimelineMod_RuntimeController";
        private const string ControllerComponentFullName =
            "CityTimelineMod.CityTimelineRuntimeController";

        internal static void Install()
        {
            // This IMGUI controller is legacy. Keeping Install as a cleanup-only
            // compatibility entry point prevents older bootstrap code from
            // recreating it during a hot reload.
            if (Uninstall())
            {
                Log.Info("Legacy CityTimelineMod runtime controller remains disabled.");
            }
            else
            {
                Log.Error(
                    "Legacy CityTimelineMod runtime controller cleanup remains pending."
                );
            }
        }

        internal static bool Uninstall()
        {
            var roots = new Dictionary<int, GameObject>();
            var complete = true;

            try
            {
                var controllers = Resources.FindObjectsOfTypeAll<CityTimelineRuntimeController>();
                foreach (var controller in controllers)
                {
                    try
                    {
                        if (controller != null &&
                            controller.gameObject != null &&
                            controller.gameObject.scene.IsValid())
                        {
                            roots[controller.gameObject.GetInstanceID()] = controller.gameObject;
                        }
                    }
                    catch (Exception ex)
                    {
                        complete = false;
                        Log.Error(
                            "Legacy CityTimelineMod runtime controller instance " +
                            "inspection failed: " + ex
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                complete = false;
                Log.Error("Legacy CityTimelineMod runtime controller component discovery failed: " + ex);
            }

            try
            {
                var gameObjects = Resources.FindObjectsOfTypeAll<GameObject>();
                foreach (var gameObject in gameObjects)
                {
                    try
                    {
                        if (gameObject == null || !gameObject.scene.IsValid())
                            continue;

                        var isCurrentRoot = string.Equals(
                            gameObject.name,
                            RootName,
                            StringComparison.Ordinal
                        );
                        var isDisposedRoot = gameObject.name != null &&
                            gameObject.name.StartsWith(
                                RootName + "_Disposed_",
                                StringComparison.Ordinal
                            );

                        if ((isCurrentRoot || isDisposedRoot) &&
                            HasCtmControllerComponent(gameObject))
                        {
                            roots[gameObject.GetInstanceID()] = gameObject;
                        }
                    }
                    catch (Exception ex)
                    {
                        complete = false;
                        Log.Error(
                            "Legacy CityTimelineMod runtime controller root " +
                            "inspection failed: " + ex
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                complete = false;
                Log.Error("Legacy CityTimelineMod runtime controller root discovery failed: " + ex);
            }

            foreach (var pair in roots)
            {
                var root = pair.Value;
                if (root == null)
                    continue;

                try
                {
                    // Destroy is deferred by Unity. Disable and rename first so
                    // no OnGUI callback or name-based lookup can revive it.
                    root.SetActive(false);
                    root.name = RootName + "_Disposed_" + pair.Key;
                    UnityEngine.Object.Destroy(root);
                }
                catch (Exception ex)
                {
                    complete = false;
                    Log.Error("Legacy CityTimelineMod runtime controller teardown failed: " + ex);
                }
            }

            return complete;
        }

        private static bool HasCtmControllerComponent(GameObject root)
        {
            var components = root.GetComponents<Component>();
            for (var i = 0; i < components.Length; i++)
            {
                var component = components[i];
                if (component == null)
                    continue;

                var type = component.GetType();
                if (string.Equals(
                        type.FullName,
                        ControllerComponentFullName,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        type.Assembly.GetName().Name,
                        typeof(Mod).Assembly.GetName().Name,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private void Awake()
        {
            // A stale serialized component can survive until the startup
            // preflight finds its root. It must be inert even during that gap.
            enabled = false;
        }

        private void OnGUI()
        {
            if (!Mod.RuntimeEnabled || !enabled || !gameObject.activeInHierarchy)
                return;
        }
    }
}
