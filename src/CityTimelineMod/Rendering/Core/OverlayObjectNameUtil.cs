namespace CityTimelineMod.Rendering.Core
{
    internal static class OverlayObjectNameUtil
    {
        internal static string SanitizeObjectName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "unknown";

            return value
                .ToLowerInvariant()
                .Replace(" ", "_")
                .Replace("-", "_")
                .Replace("/", "_")
                .Replace("\\", "_");
        }
    }
}
