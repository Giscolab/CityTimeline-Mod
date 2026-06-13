using System;
using CityTimelineMod.Config;
using CityTimelineMod.Importers;
using CityTimelineMod.Rendering;
using CityTimelineMod.Rendering.Core;
using UnityEngine;

namespace CityTimelineMod.Rendering.Bounds
{
    internal static class MapBoundsRenderer
    {
        internal static void Render(
            GeoOverlayConfig config,
            Transform parent,
            Material worldMapMaterial,
            Material heightMapMaterial,
            Material centerMaterial,
            double originLon,
            double originLat,
            Func<Vector3, float> resolveY,
            Action<string> logVerbose
        )
        {
            if (config == null || !config.RenderMapBounds)
                return;

            double worldMinLon = config.WorldMapMinLon;
            double worldMinLat = config.WorldMapMinLat;
            double worldMaxLon = config.WorldMapMaxLon;
            double worldMaxLat = config.WorldMapMaxLat;

            double heightMinLon = config.HeightMapMinLon;
            double heightMinLat = config.HeightMapMinLat;
            double heightMaxLon = config.HeightMapMaxLon;
            double heightMaxLat = config.HeightMapMaxLat;

            var mapBoundsCenterLon = originLon;
            var mapBoundsCenterLat = originLat;

            if (config.MapBoundsUseCenterAndSize)
            {
                ComputeGeoSquareBounds(
                    mapBoundsCenterLon,
                    mapBoundsCenterLat,
                    config.WorldMapSizeKm,
                    out worldMinLon,
                    out worldMinLat,
                    out worldMaxLon,
                    out worldMaxLat
                );

                ComputeGeoSquareBounds(
                    mapBoundsCenterLon,
                    mapBoundsCenterLat,
                    config.HeightMapSizeKm,
                    out heightMinLon,
                    out heightMinLat,
                    out heightMaxLon,
                    out heightMaxLat
                );
            }

            if (config.RenderWorldMapBounds)
            {
                RenderGeoBoundsRectangle(
                    "worldmap_bounds",
                    worldMinLon,
                    worldMinLat,
                    worldMaxLon,
                    worldMaxLat,
                    worldMapMaterial,
                    config,
                    parent,
                    originLon,
                    originLat,
                    resolveY,
                    9f
                );
            }

            if (config.RenderHeightMapBounds)
            {
                RenderGeoBoundsRectangle(
                    "heightmap_bounds",
                    heightMinLon,
                    heightMinLat,
                    heightMaxLon,
                    heightMaxLat,
                    heightMapMaterial,
                    config,
                    parent,
                    originLon,
                    originLat,
                    resolveY,
                    7f
                );
            }

            var centerLon = config.MapBoundsUseCenterAndSize ? mapBoundsCenterLon : (heightMinLon + heightMaxLon) * 0.5;
            var centerLat = config.MapBoundsUseCenterAndSize ? mapBoundsCenterLat : (heightMinLat + heightMaxLat) * 0.5;
            var center = GeoToBoundsWorld(centerLon, centerLat, config, originLon, originLat, resolveY, 0.2f);

            if (config.RenderMapCenter)
                CreateBoundsMarker("map_bounds_center_marker", center, centerMaterial, parent);

            logVerbose(
                "GroundOverlay bounds: mode=" + (config.MapBoundsUseCenterAndSize ? "center+size:origin" : "bbox") +
                ", originLon=" + originLon +
                ", originLat=" + originLat +
                ", boundsCenterLon=" + mapBoundsCenterLon +
                ", boundsCenterLat=" + mapBoundsCenterLat +
                ", worldMapSizeKm=" + config.WorldMapSizeKm +
                ", heightMapSizeKm=" + config.HeightMapSizeKm +
                ", worldMap=[" + worldMinLat + "," + worldMinLon + "," + worldMaxLat + "," + worldMaxLon + "]" +
                ", heightMap=[" + heightMinLat + "," + heightMinLon + "," + heightMaxLat + "," + heightMaxLon + "]"
            );
        }

        private static void ComputeGeoSquareBounds(double centerLon, double centerLat, float sizeKm, out double minLon, out double minLat, out double maxLon, out double maxLat)
        {
            var safeSizeKm = Math.Max(0.001, sizeKm);
            var halfKm = safeSizeKm * 0.5;

            const double kmPerDegreeLat = 111.32;
            var latRadians = centerLat * Math.PI / 180.0;
            var cosLat = Math.Cos(latRadians);

            if (Math.Abs(cosLat) < 0.000001)
                cosLat = 0.000001;

            var kmPerDegreeLon = kmPerDegreeLat * cosLat;

            var halfLatDegrees = halfKm / kmPerDegreeLat;
            var halfLonDegrees = halfKm / kmPerDegreeLon;

            minLon = centerLon - halfLonDegrees;
            maxLon = centerLon + halfLonDegrees;
            minLat = centerLat - halfLatDegrees;
            maxLat = centerLat + halfLatDegrees;
        }

        private static void RenderGeoBoundsRectangle(string name, double minLon, double minLat, double maxLon, double maxLat, Material material, GeoOverlayConfig config, Transform parent, double originLon, double originLat, Func<Vector3, float> resolveY, float width)
        {
            var southWest = GeoToBoundsWorld(minLon, minLat, config, originLon, originLat, resolveY, 0f);
            var southEast = GeoToBoundsWorld(maxLon, minLat, config, originLon, originLat, resolveY, 0f);
            var northEast = GeoToBoundsWorld(maxLon, maxLat, config, originLon, originLat, resolveY, 0f);
            var northWest = GeoToBoundsWorld(minLon, maxLat, config, originLon, originLat, resolveY, 0f);

            CreateBoundsSegment(name + "_south", southWest, southEast, material, parent, width);
            CreateBoundsSegment(name + "_east", southEast, northEast, material, parent, width);
            CreateBoundsSegment(name + "_north", northEast, northWest, material, parent, width);
            CreateBoundsSegment(name + "_west", northWest, southWest, material, parent, width);
        }

        private static Vector3 GeoToBoundsWorld(double lon, double lat, GeoOverlayConfig config, double originLon, double originLat, Func<Vector3, float> resolveY, float extraYOffset)
        {
            var point = new GeoPoint(lon, lat);
            var world = GeoTransform.ToWorld(point, config, originLon, originLat);
            world.y = resolveY(world) + config.GroundMargin + config.MapBoundsYOffset + extraYOffset;
            return world;
        }

        private static void CreateBoundsSegment(string name, Vector3 a, Vector3 b, Material material, Transform parent, float width)
        {
            if (material == null)
                return;

            var mid = (a + b) * 0.5f;
            var delta = b - a;

            var sizeX = Mathf.Max(Mathf.Abs(delta.x), width);
            var sizeZ = Mathf.Max(Mathf.Abs(delta.z), width);

            if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.z))
                sizeZ = width;
            else
                sizeX = width;

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, true);
            cube.transform.position = mid;
            cube.transform.localScale = new Vector3(sizeX, 4f, sizeZ);

            OverlayPrimitiveUtil.RemoveCollider(cube);
            OverlayPrimitiveUtil.ApplyMaterial(cube, material);
        }

        private static void CreateBoundsMarker(string name, Vector3 center, Material material, Transform parent)
        {
            if (material == null)
                return;

            var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = name;
            marker.transform.SetParent(parent, true);
            marker.transform.position = center + Vector3.up * 40f;
            marker.transform.localScale = new Vector3(40f, 80f, 40f);

            OverlayPrimitiveUtil.RemoveCollider(marker);
            OverlayPrimitiveUtil.ApplyMaterial(marker, material);
        }
    }
}
