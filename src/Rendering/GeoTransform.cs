using System;
using CityTimelineMod.Config;
using CityTimelineMod.Importers;
using UnityEngine;

namespace CityTimelineMod.Rendering
{
    internal static class GeoTransform
    {
        internal static Vector3 ToWorld(GeoPoint point, GeoOverlayConfig config, double originLon, double originLat)
        {
            const double metersPerDegreeLat = 111320.0;

            var centerLatRad = originLat * Math.PI / 180.0;
            var metersPerDegreeLon = metersPerDegreeLat * Math.Cos(centerLatRad);

            // 1. Coordonnées réelles locales en mètres
            var xMeters = (point.Lon - originLon) * metersPerDegreeLon;
            var zMeters = (point.Lat - originLat) * metersPerDegreeLat;

            // 2. Échelle globale + correction non uniforme si besoin
            var x = (float)(xMeters * config.WorldScale * config.OverlayScaleX);
            var z = (float)(zMeters * config.WorldScale * config.OverlayScaleZ);

            // 3. Miroirs éventuels
            if (config.FlipX)
                x = -x;

            if (config.FlipZ)
                z = -z;

            // 4. Rotation autour de l'origine
            var angleRad = config.OverlayRotationDegrees * Mathf.Deg2Rad;
            var cos = Mathf.Cos(angleRad);
            var sin = Mathf.Sin(angleRad);

            var rotatedX = x * cos - z * sin;
            var rotatedZ = x * sin + z * cos;

            // 5. Décalage monde final
            return new Vector3(
                config.WorldOriginX + rotatedX,
                0f,
                config.WorldOriginZ + rotatedZ
            );
        }
    }
}
