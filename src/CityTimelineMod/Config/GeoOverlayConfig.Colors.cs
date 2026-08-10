using UnityEngine;

namespace CityTimelineMod.Config
{
    internal sealed partial class GeoOverlayConfig
    {
        internal Color ResolveColorName(string colorName, float alpha)
        {
            var safeAlpha = Mathf.Clamp01(alpha);
            var key = NormalizeColorName(colorName);

            switch (key)
            {
                // Palette alignée sur cs2-realmap-generator (src/road_categories.py + visualizer/js/config.js).
                // Les hex en commentaire sont la référence realmap.

                case "rouge":
                case "red":
                    // realmap highway #ff4d4d
                    return new Color(1f, 0.302f, 0.302f, safeAlpha);

                case "vert":
                case "green":
                    return new Color(0f, 1f, 0f, safeAlpha);

                case "vertclair":
                case "lime":
                case "residentialgreen":
                    // realmap res_low #7ab64d
                    return new Color(0.478f, 0.714f, 0.302f, safeAlpha);

                case "vertmoyen":
                    // realmap res_med #2d9d54
                    return new Color(0.176f, 0.616f, 0.329f, safeAlpha);

                case "vertfonce":
                    // realmap res_high #0b6f3a
                    return new Color(0.043f, 0.435f, 0.227f, safeAlpha);

                case "bleu":
                case "blue":
                case "waterblue":
                    // realmap water areas #0ea5e9
                    return new Color(0.055f, 0.647f, 0.914f, safeAlpha);

                case "bleuclair":
                    // realmap water lines #38bdf8
                    return new Color(0.220f, 0.741f, 0.973f, safeAlpha);

                case "bleufonce":
                case "darkblue":
                case "deepwater":
                    // realmap water stroke #0284c7
                    return new Color(0.008f, 0.518f, 0.780f, safeAlpha);

                case "cyan":
                case "aqua":
                    // realmap pathway #2ad4ff
                    return new Color(0.165f, 0.831f, 1f, safeAlpha);

                case "jaune":
                case "yellow":
                case "warmyellow":
                    // realmap medium_road #ffd60a
                    return new Color(1f, 0.839f, 0.039f, safeAlpha);

                case "magenta":
                case "heightmagenta":
                case "magentaheightmap":
                    // realmap ramp #ff3df5
                    return new Color(1f, 0.239f, 0.961f, safeAlpha);

                case "violet":
                case "purple":
                case "retailpurple":
                case "violetblue":
                    // realmap office #a46bd5
                    return new Color(0.643f, 0.420f, 0.835f, safeAlpha);

                case "blanc":
                case "white":
                    return new Color(1f, 1f, 1f, safeAlpha);

                case "noir":
                case "black":
                    return new Color(0f, 0f, 0f, safeAlpha);

                case "grisclair":
                case "lightgray":
                    // realmap gravel_road #c7d0d9
                    return new Color(0.780f, 0.816f, 0.851f, safeAlpha);

                case "gris":
                case "gray":
                    return new Color(0.45f, 0.45f, 0.45f, safeAlpha);

                case "grisfonce":
                case "darkgray":
                    return new Color(0.22f, 0.22f, 0.22f, safeAlpha);

                case "orange":
                case "industrialorange":
                    // realmap large_road #ff9f1c
                    return new Color(1f, 0.624f, 0.110f, safeAlpha);

                case "orangerouge":
                case "orangered":
                    return new Color(1f, 0.18f, 0f, safeAlpha);

                case "orangefort":
                case "strongorange":
                    return new Color(1f, 0.38f, 0f, safeAlpha);

                case "orangeclair":
                case "lightorange":
                    return new Color(1f, 0.62f, 0.05f, safeAlpha);

                case "ambre":
                case "amber":
                    // realmap industrial #d6ad32
                    return new Color(0.839f, 0.678f, 0.196f, safeAlpha);

                case "orangefonce":
                case "deeporange":
                    return new Color(1f, 0.28f, 0f, safeAlpha);

                case "bleucommercial":
                case "commercialblue":
                    // realmap com_high #1f57d6
                    return new Color(0.122f, 0.341f, 0.839f, safeAlpha);

                case "bleucommercialclair":
                case "commercialbluelight":
                    // realmap com_low #4aa3ff
                    return new Color(0.290f, 0.639f, 1f, safeAlpha);

                case "bleudetail":
                case "retailblue":
                    // realmap retail #74c5ff
                    return new Color(0.455f, 0.773f, 1f, safeAlpha);

                case "vertparking":
                case "parkinggreen":
                    // realmap prk_ramp #5fe86e
                    return new Color(0.373f, 0.910f, 0.431f, safeAlpha);

                case "vertparkingclair":
                case "parkinggreenlight":
                    // realmap prk_surface #b9ed70
                    return new Color(0.725f, 0.929f, 0.439f, safeAlpha);

                case "turquoise":
                case "mixedturquoise":
                    // realmap mixed #2ed6e5
                    return new Color(0.180f, 0.839f, 0.898f, safeAlpha);

                case "bleubureaux":
                case "officeblue":
                    return new Color(0.2f, 0.55f, 1f, safeAlpha);

                case "blanclimites":
                case "boundswhite":
                    return new Color(1f, 1f, 1f, safeAlpha);

                case "rougecentre":
                case "centerred":
                    return new Color(1f, 0f, 0f, safeAlpha);

                default:
                    return new Color(1f, 0.624f, 0.110f, safeAlpha);
            }
        }

        private static string NormalizeColorName(string colorName)
        {
            if (string.IsNullOrWhiteSpace(colorName))
                return "";

            var normalized = colorName.Trim().Normalize(System.Text.NormalizationForm.FormD);
            var builder = new System.Text.StringBuilder(normalized.Length);

            for (var i = 0; i < normalized.Length; i++)
            {
                var c = normalized[i];
                var category = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);

                if (category == System.Globalization.UnicodeCategory.NonSpacingMark)
                    continue;

                if (char.IsWhiteSpace(c) || c == '-' || c == '_')
                    continue;

                builder.Append(char.ToLowerInvariant(c));
            }

            return builder.ToString().Normalize(System.Text.NormalizationForm.FormC);
        }
    }
}
