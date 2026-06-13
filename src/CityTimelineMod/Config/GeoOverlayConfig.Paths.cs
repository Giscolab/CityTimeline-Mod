using System;
using System.IO;

namespace CityTimelineMod.Config
{
    internal sealed partial class GeoOverlayConfig
    {
        private static string ResolvePath(string path, string baseDir)
        {
            if (string.IsNullOrWhiteSpace(path))
                return path;

            var expanded = Environment.ExpandEnvironmentVariables(path);

            if (Path.IsPathRooted(expanded))
                return Path.GetFullPath(expanded);

            if (!string.IsNullOrWhiteSpace(baseDir))
                return Path.GetFullPath(Path.Combine(baseDir, expanded));

            return Path.GetFullPath(expanded);
        }
    }
}
