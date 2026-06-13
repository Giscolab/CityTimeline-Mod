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
                case "rouge":
                case "red":
                    return new Color(1f, 0f, 0f, safeAlpha);

                case "vert":
                case "green":
                    return new Color(0f, 1f, 0f, safeAlpha);

                case "vertclair":
                case "lime":
                case "residentialgreen":
                    return new Color(0.45f, 1f, 0.35f, safeAlpha);

                case "vertMoyen":
                    return new Color(0.20f, 0.75f, 0.25f, safeAlpha);

                case "vertfonce":
                    return new Color(0.05f, 0.40f, 0.12f, safeAlpha);

                case "bleu":
                case "blue":
                case "waterblue":
                    return new Color(0.05f, 0.45f, 1f, safeAlpha);

                case "bleuclair":
                    return new Color(0.35f, 0.75f, 1f, safeAlpha);

                case "bleufonce":
                case "darkblue":
                case "deepwater":
                    return new Color(0.02f, 0.12f, 0.42f, safeAlpha);

                case "cyan":
                case "aqua":
                    return new Color(0f, 1f, 1f, safeAlpha);

                case "jaune":
                case "yellow":
                case "warmyellow":
                    return new Color(1f, 1f, 0f, safeAlpha);

                case "magenta":
                case "heightmagenta":
                    return new Color(1f, 0f, 1f, safeAlpha);

                case "violet":
                case "purple":
                case "retailpurple":
                case "violetblue":
                    return new Color(0.75f, 0.2f, 1f, safeAlpha);

                case "blanc":
                case "white":
                    return new Color(1f, 1f, 1f, safeAlpha);

                case "noir":
                case "black":
                    return new Color(0f, 0f, 0f, safeAlpha);

                case "grisclair":
                case "lightgray":
                    return new Color(0.78f, 0.78f, 0.78f, safeAlpha);

                case "gris":
                case "gray":
                    return new Color(0.45f, 0.45f, 0.45f, safeAlpha);

                case "grisfonce":
                case "darkgray":
                    return new Color(0.22f, 0.22f, 0.22f, safeAlpha);

                case "orange":
                case "industrialorange":
                    return new Color(1f, 0.45f, 0f, safeAlpha);

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
                    return new Color(1f, 0.82f, 0.15f, safeAlpha);

                case "orangefonce":
                case "deeporange":
                    return new Color(1f, 0.28f, 0f, safeAlpha);

                case "bleucommercial":
                case "commercialblue":
                    return new Color(0.35f, 0.25f, 1f, safeAlpha);

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
                    return new Color(1f, 0.45f, 0f, safeAlpha);
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
