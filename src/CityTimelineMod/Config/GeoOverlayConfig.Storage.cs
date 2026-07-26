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

                    if (!TryWriteUtf8TextAtomically(
                        path,
                        root.ToString(Formatting.Indented),
                        out error
                    ))
                    {
                        return false;
                    }

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

        internal static bool TryWriteUtf8TextAtomically(
            string path,
            string contents,
            out string error
        )
        {
            return TryWriteBytesAtomically(
                path,
                new UTF8Encoding(false).GetBytes(contents ?? string.Empty),
                out error
            );
        }

        internal static bool TryWriteBytesAtomically(
            string path,
            byte[] contents,
            out string error
        )
        {
            error = null;

            if (string.IsNullOrWhiteSpace(path))
            {
                error = "The target path is empty.";
                return false;
            }

            if (contents == null)
            {
                error = "The file contents are null.";
                return false;
            }

            string temporaryPath = null;

            try
            {
                var fullPath = Path.GetFullPath(path);
                var directory = Path.GetDirectoryName(fullPath);
                if (string.IsNullOrWhiteSpace(directory))
                {
                    error = "The target directory is empty for " + fullPath;
                    return false;
                }

                Directory.CreateDirectory(directory);
                temporaryPath = Path.Combine(
                    directory,
                    "." + Path.GetFileName(fullPath) + "." + Guid.NewGuid().ToString("N") + ".tmp"
                );

                using (var output = new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    4096,
                    FileOptions.WriteThrough
                ))
                {
                    output.Write(contents, 0, contents.Length);
                    output.Flush();
                }

                if (File.Exists(fullPath))
                    File.Replace(temporaryPath, fullPath, null);
                else
                    File.Move(temporaryPath, fullPath);

                temporaryPath = null;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.ToString();
                return false;
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(temporaryPath))
                {
                    try
                    {
                        if (File.Exists(temporaryPath))
                            File.Delete(temporaryPath);
                    }
                    catch
                    {
                        // Best-effort cleanup only. The destination was never
                        // replaced if the temporary file is still present.
                    }
                }
            }
        }

        internal static bool ByteArraysEqual(byte[] expected, byte[] actual)
        {
            if (ReferenceEquals(expected, actual))
                return true;
            if (expected == null || actual == null || expected.Length != actual.Length)
                return false;

            for (var index = 0; index < expected.Length; index++)
            {
                if (expected[index] != actual[index])
                    return false;
            }

            return true;
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
