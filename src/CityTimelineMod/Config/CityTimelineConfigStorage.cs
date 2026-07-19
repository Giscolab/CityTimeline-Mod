using System;
using System.IO;
using CityTimelineMod.Util;
using UnityEngine;

namespace CityTimelineMod.Config
{
    /// <summary>
    /// Separates the immutable mod payload from mutable user state.
    ///
    /// CS2 watches the complete local-mod directory and reloads CoHTML media
    /// whenever a file in it changes.  Writing config.json beside the DLL/UI
    /// therefore refreshed the game UI after every option save.
    /// </summary>
    internal static class CityTimelineConfigStorage
    {
        private const string ModFolderName = "CityTimelineMod";
        private static readonly object MigrationLock = new object();

        internal static string GetWritableConfigPath()
        {
            var persistentRoot = Application.persistentDataPath;

            if (string.IsNullOrWhiteSpace(persistentRoot))
            {
                var localAppData = Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData
                );
                persistentRoot = Path.GetFullPath(Path.Combine(
                    localAppData,
                    "..",
                    "LocalLow",
                    "Colossal Order",
                    "Cities Skylines II"
                ));
            }

            return Path.Combine(
                persistentRoot,
                "ModsSettings",
                ModFolderName,
                "config.json"
            );
        }

        internal static string ResolveWritableConfigPath()
        {
            return ResolveWritableConfigPath(ResolveRuntimeModDirectory());
        }

        internal static string ResolveWritableConfigPath(string modDir)
        {
            var writablePath = GetWritableConfigPath();

            lock (MigrationLock)
            {
                var writableDirectory = Path.GetDirectoryName(writablePath);
                if (!string.IsNullOrWhiteSpace(writableDirectory))
                    Directory.CreateDirectory(writableDirectory);

                if (File.Exists(writablePath))
                    return writablePath;

                var legacyPath = string.IsNullOrWhiteSpace(modDir)
                    ? null
                    : Path.Combine(modDir, "config.json");

                if (!string.IsNullOrWhiteSpace(legacyPath) && File.Exists(legacyPath))
                {
                    File.Copy(legacyPath, writablePath, false);
                    Log.Info(
                        "CityTimelineMod: migrated writable config outside watched mod directory. from=" +
                        legacyPath + ", to=" + writablePath
                    );
                }
            }

            return writablePath;
        }

        internal static string GetBundledDefaultConfigPath(string modDir)
        {
            return string.IsNullOrWhiteSpace(modDir)
                ? ""
                : Path.Combine(modDir, "config.json");
        }

        private static string ResolveRuntimeModDirectory()
        {
            var localModsPath = Environment.GetEnvironmentVariable(
                "CSII_LOCALMODSPATH",
                EnvironmentVariableTarget.User
            );

            if (!string.IsNullOrWhiteSpace(localModsPath))
                return Path.Combine(localModsPath, ModFolderName);

            var persistentRoot = Application.persistentDataPath;
            if (!string.IsNullOrWhiteSpace(persistentRoot))
                return Path.Combine(persistentRoot, "Mods", ModFolderName);

            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(
                userProfile,
                "AppData",
                "LocalLow",
                "Colossal Order",
                "Cities Skylines II",
                "Mods",
                ModFolderName
            );
        }
    }
}
