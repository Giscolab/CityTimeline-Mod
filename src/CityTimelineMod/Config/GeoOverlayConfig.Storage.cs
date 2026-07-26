using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CityTimelineMod.Config
{
    internal sealed partial class GeoOverlayConfig
    {
        private static readonly object ConfigFileLock = new object();

        internal static bool TryReadRuntimeConfigRoot(
            string path,
            out JObject root,
            out bool exists,
            out string error
        )
        {
            lock (ConfigFileLock)
            {
                return TryReadRuntimeConfigRootUnsafe(path, out root, out exists, out error);
            }
        }

        internal static bool TryUpdateRuntimeConfigFile(
            string path,
            Action<JObject> update,
            out JObject updatedRoot,
            out string error
        )
        {
            updatedRoot = null;
            error = null;

            if (string.IsNullOrWhiteSpace(path))
            {
                error = "ConfigPath is empty.";
                return false;
            }

            if (update == null)
            {
                error = "The config update is empty.";
                return false;
            }

            lock (ConfigFileLock)
            {
                try
                {
                    JObject root;
                    bool exists;
                    if (!TryReadRuntimeConfigRootUnsafe(path, out root, out exists, out error))
                        return false;

                    update(root);
                    EnsureExperimentalLifecycleDefaults(root);

                    var directory = Path.GetDirectoryName(path);
                    if (!string.IsNullOrWhiteSpace(directory))
                        Directory.CreateDirectory(directory);

                    File.WriteAllText(
                        path,
                        root.ToString(Formatting.Indented),
                        new UTF8Encoding(false)
                    );

                    updatedRoot = (JObject)root.DeepClone();
                    return true;
                }
                catch (Exception ex)
                {
                    error = ex.ToString();
                    return false;
                }
            }
        }

        private static bool TryReadRuntimeConfigRootUnsafe(
            string path,
            out JObject root,
            out bool exists,
            out string error
        )
        {
            root = null;
            exists = false;
            error = null;

            if (string.IsNullOrWhiteSpace(path))
            {
                error = "ConfigPath is empty.";
                return false;
            }

            try
            {
                // Read directly instead of probing with File.Exists: access failures
                // must not be mistaken for an absent, therefore valid, config.
                var json = File.ReadAllText(path, Encoding.UTF8);
                root = JObject.Parse(json);
                exists = true;
                return true;
            }
            catch (FileNotFoundException)
            {
                root = new JObject();
                return true;
            }
            catch (DirectoryNotFoundException)
            {
                root = new JObject();
                return true;
            }
            catch (Exception ex)
            {
                error = ex.ToString();
                return false;
            }
        }

        private static void EnsureExperimentalLifecycleDefaults(JObject root)
        {
            if (root == null)
                return;

            JToken ignored;
            if (!root.TryGetValue(
                "largeMapEnabled",
                StringComparison.OrdinalIgnoreCase,
                out ignored
            ))
            {
                root["largeMapEnabled"] = false;
            }

            if (!root.TryGetValue(
                "playableWorldEnabled",
                StringComparison.OrdinalIgnoreCase,
                out ignored
            ))
            {
                root["playableWorldEnabled"] = false;
            }
        }
    }
}
