using System;

namespace CityTimelineMod.Roads
{
    internal static partial class RuntimeRoadSpawner
    {
        private sealed partial class RuntimeRoadImportSession
        {
            private int ResolveImportStage(RoadImportMetadata meta)
            {
                if (meta == null)
                    return 4;

                var highway = NormalizeImportPriorityHighway(meta.Highway);

                if (meta.Roundabout)
                    return 0;

                switch (highway)
                {
                    case "motorway":
                    case "trunk":
                    case "link":
                    case "primary":
                    case "secondary":
                    case "tertiary":
                        return 0;

                    case "residential":
                    case "unclassified":
                        return 1;

                    case "living_street":
                        return 2;

                    case "service":
                        return 3;

                    case "track":
                        return 4;

                    default:
                        return 1;
                }
            }

            private static string ResolveImportStageLabel(int stage)
            {
                switch (stage)
                {
                    case 0:
                        return "backbone";
                    case 1:
                        return "urban";
                    case 2:
                        return "local";
                    case 3:
                        return "service";
                    case 4:
                        return "optional";
                    default:
                        return "unknown";
                }
            }

            private int CalculateRoadDistanceBucket(float distanceSq)
            {
                if (float.IsNaN(distanceSq) || float.IsInfinity(distanceSq) || distanceSq == float.MaxValue)
                    return int.MaxValue;

                var distance = Math.Sqrt(distanceSq);

                // Anneaux de 500 m : on reste centré, mais dans chaque anneau on privilégie les routes importantes.
                var bucketMeters = Math.Max(50.0, _config.RuntimeRoadImportDistanceBucketMeters);
                return (int)Math.Floor(distance / bucketMeters);
            }

            private int CalculateRoadImportPriority(RoadImportMetadata meta)
            {
                if (meta == null)
                    return 0;

                var highway = NormalizeImportPriorityHighway(meta.Highway);
                var priority = 0;

                switch (highway)
                {
                    case "motorway":
                    case "trunk":
                        priority = 1000;
                        break;

                    case "link":
                        priority = 880;
                        break;

                    case "primary":
                        priority = 820;
                        break;

                    case "secondary":
                        priority = 720;
                        break;

                    case "tertiary":
                        priority = 620;
                        break;

                    case "residential":
                        priority = 430;
                        break;

                    case "unclassified":
                        priority = 400;
                        break;

                    case "living_street":
                        priority = 300;
                        break;

                    case "service":
                        priority = 230;
                        break;

                    case "track":
                        priority = 80;
                        break;

                    default:
                        priority = 350;
                        break;
                }

                if (meta.TargetLaneCount >= 6)
                    priority += 180;
                else if (meta.TargetLaneCount >= 4)
                    priority += 130;
                else if (meta.TargetLaneCount == 3)
                    priority += 70;
                else if (meta.TargetLaneCount == 2)
                    priority += 25;

                if (meta.Roundabout)
                    priority += 180;

                if (meta.Oneway && (highway == "motorway" || highway == "trunk" || highway == "link" || highway == "primary"))
                    priority += 60;

                if (IsClearlyUnpavedSurface(meta.Surface))
                    priority -= 250;

                if (highway == "service" && string.Equals(meta.Subcategory, "parking_aisle", StringComparison.OrdinalIgnoreCase))
                    priority -= 120;

                if (priority < 0)
                    priority = 0;

                var weight = Math.Max(0f, _config.RuntimeRoadImportPriorityWeight);
                priority = (int)Math.Round(priority * weight);

                if (priority < 0)
                    priority = 0;

                return priority;
            }

            private string NormalizeImportPriorityHighway(string highway)
            {
                if (string.IsNullOrWhiteSpace(highway))
                    return "residential";

                var h = highway.Trim().ToLowerInvariant();

                if (h.EndsWith("_link", StringComparison.OrdinalIgnoreCase))
                    return "link";

                if (h == "motorway" ||
                    h == "trunk" ||
                    h == "primary" ||
                    h == "secondary" ||
                    h == "tertiary" ||
                    h == "residential" ||
                    h == "unclassified" ||
                    h == "living_street" ||
                    h == "service" ||
                    h == "track")
                {
                    return h;
                }

                return "residential";
            }

            private bool IsClearlyUnpavedSurface(string surface)
            {
                if (string.IsNullOrWhiteSpace(surface))
                    return false;

                var s = surface.Trim().ToLowerInvariant();

                return
                    s == "gravel" ||
                    s == "fine_gravel" ||
                    s == "dirt" ||
                    s == "earth" ||
                    s == "ground" ||
                    s == "grass" ||
                    s == "sand" ||
                    s == "unpaved";
            }

            private bool IsParkingAisle(RoadImportMetadata meta)
            {
                if (meta == null)
                    return false;

                if (string.Equals(meta.Subcategory, "parking_aisle", StringComparison.OrdinalIgnoreCase))
                    return true;

                if (!string.Equals(meta.Highway, "service", StringComparison.OrdinalIgnoreCase))
                    return false;

                var name = meta.Name ?? "";

                return name.IndexOf("parking", StringComparison.OrdinalIgnoreCase) >= 0;
            }

            private bool IsClearlyUnpavedRoad(RoadImportMetadata meta)
            {
                if (meta == null)
                    return false;

                if (string.Equals(meta.Highway, "track", StringComparison.OrdinalIgnoreCase))
                    return true;

                return IsClearlyUnpavedSurface(meta.Surface);
            }
        }
    }
}
