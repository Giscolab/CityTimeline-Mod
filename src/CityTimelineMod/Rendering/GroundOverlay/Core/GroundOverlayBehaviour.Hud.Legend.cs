using System.Collections.Generic;
using UnityEngine;

namespace CityTimelineMod.Rendering
{
    // Légende visuelle du HUD : pastilles de couleur + signification métier.
    // Les couleurs affichées sont résolues depuis la config au moment du rendu,
    // donc toujours synchronisées avec le rendu réel de la carte
    // (convention partagée : docs/color-conventions.md).
    internal sealed partial class GroundOverlayBehaviour
    {
        private bool _modernHudShowLegend;

        private static GUIStyle _hudSwatchLabelStyle;
        private static GUIStyle _hudLegendHeaderStyle;

        private const float HudSwatchSize = 12f;
        private const int HudSwatchesPerRow = 2;

        private struct HudSwatch
        {
            internal Color Color;
            internal string Label;

            internal HudSwatch(Color color, string label)
            {
                Color = color;
                Label = label;
            }
        }

        private static void EnsureHudLegendStyles()
        {
            if (_hudSwatchLabelStyle != null)
                return;

            _hudSwatchLabelStyle = new GUIStyle(GUI.skin.label);
            _hudSwatchLabelStyle.fontSize = 13;
            _hudSwatchLabelStyle.margin = new RectOffset(2, 8, 2, 2);
            _hudSwatchLabelStyle.normal.textColor = HudTextMuted;

            _hudLegendHeaderStyle = new GUIStyle(GUI.skin.label);
            _hudLegendHeaderStyle.fontSize = 14;
            _hudLegendHeaderStyle.normal.textColor = HudAccent;
        }

        private Color ResolveHudSwatchColor(string colorName)
        {
            if (_config == null)
                return Color.white;

            var color = _config.ResolveColorName(colorName, 1f);
            color.a = 1f;
            return color;
        }

        private static void DrawHudSwatch(Color color)
        {
            var rect = GUILayoutUtility.GetRect(
                HudSwatchSize,
                HudSwatchSize,
                GUILayout.Width(HudSwatchSize),
                GUILayout.Height(HudSwatchSize)
            );

            rect.y += 3f;

            var previousColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previousColor;
        }

        private static void DrawHudSwatchRow(List<HudSwatch> swatches, int startIndex, int count)
        {
            GUILayout.BeginHorizontal();

            for (var i = startIndex; i < startIndex + count && i < swatches.Count; i++)
            {
                DrawHudSwatch(swatches[i].Color);
                GUILayout.Label(swatches[i].Label, _hudSwatchLabelStyle);
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private static void DrawHudSwatches(List<HudSwatch> swatches)
        {
            for (var i = 0; i < swatches.Count; i += HudSwatchesPerRow)
                DrawHudSwatchRow(swatches, i, HudSwatchesPerRow);
        }

        // Correspondance couche HUD -> pastilles (couleur config + signification métier).
        private List<HudSwatch> GetModernLayerSwatches(string layerId)
        {
            if (_config == null)
                return null;

            switch (layerId)
            {
                case "zoning.residential":
                    return new List<HudSwatch>
                    {
                        new HudSwatch(ResolveHudSwatchColor(_config.ZoningResidentialLowColor), "Faible densité"),
                        new HudSwatch(ResolveHudSwatchColor(_config.ZoningResidentialMediumColor), "Moyenne"),
                        new HudSwatch(ResolveHudSwatchColor(_config.ZoningResidentialHighColor), "Haute")
                    };

                case "zoning.commercial":
                    return new List<HudSwatch>
                    {
                        new HudSwatch(ResolveHudSwatchColor(_config.ZoningCommercialLowColor), "Basse densité"),
                        new HudSwatch(ResolveHudSwatchColor(_config.ZoningCommercialHighColor), "Haute densité"),
                        new HudSwatch(ResolveHudSwatchColor(_config.ZoningRetailColor), "Commerce de détail"),
                        new HudSwatch(ResolveHudSwatchColor(_config.ZoningMixedColor), "Usage mixte")
                    };

                case "zoning.industrial":
                    return new List<HudSwatch>
                    {
                        new HudSwatch(ResolveHudSwatchColor(_config.ZoningIndustrialColor), "Industrie")
                    };

                case "zoning.office":
                    return new List<HudSwatch>
                    {
                        new HudSwatch(ResolveHudSwatchColor(_config.ZoningOfficeColor), "Bureaux")
                    };

                case "parking":
                    return new List<HudSwatch>
                    {
                        new HudSwatch(ResolveHudSwatchColor(_config.ZoningSurfaceColor), "Surface"),
                        new HudSwatch(ResolveHudSwatchColor(_config.ZoningRampColor), "Rampe d'accès")
                    };

                case "roads.paths":
                    return new List<HudSwatch>
                    {
                        new HudSwatch(ResolveHudSwatchColor(_config.RoadColorMotorway), "Autoroute"),
                        new HudSwatch(ResolveHudSwatchColor(_config.RoadColorPrimary), "Axe principal"),
                        new HudSwatch(ResolveHudSwatchColor(_config.RoadColorSecondary), "Secondaire"),
                        new HudSwatch(ResolveHudSwatchColor(_config.RoadColorTertiary), "Tertiaire"),
                        new HudSwatch(ResolveHudSwatchColor(_config.RoadColorLink), "Bretelle"),
                        new HudSwatch(ResolveHudSwatchColor(_config.PathColor), "Chemin piéton"),
                        new HudSwatch(ResolveHudSwatchColor(_config.RoadColorDefault), "Autre voie")
                    };

                case "water":
                    return new List<HudSwatch>
                    {
                        new HudSwatch(ResolveHudSwatchColor(_config.WaterLineColor), "Ligne d'eau"),
                        new HudSwatch(ResolveHudSwatchColor(_config.WaterAreaFillColor), "Surface"),
                        new HudSwatch(ResolveHudSwatchColor(_config.WaterAreaOutlineColor), "Contour")
                    };

                default:
                    return null;
            }
        }

        private void DrawModernLayerSwatches(string layerId)
        {
            var swatches = GetModernLayerSwatches(layerId);

            if (swatches == null || swatches.Count == 0)
                return;

            EnsureHudLegendStyles();
            DrawHudSwatches(swatches);
        }

        // Section repliable en haut de l'onglet Couches : lecture immédiate
        // des familles de couleurs visibles sur la carte.
        private void DrawModernLegendSection()
        {
            if (_config == null)
                return;

            EnsureHudLegendStyles();

            GUILayout.BeginVertical(_hudSubblockStyle);

            var toggleLabel = _modernHudShowLegend ? "▼ Légende visuelle" : "► Légende visuelle";

            if (GUILayout.Button(toggleLabel, _hudLegendHeaderStyle))
                _modernHudShowLegend = !_modernHudShowLegend;

            if (_modernHudShowLegend)
            {
                GUILayout.Space(4f);

                var families = new List<HudSwatch>
                {
                    new HudSwatch(ResolveHudSwatchColor(_config.ZoningResidentialMediumColor), "Verts = Résidentiel"),
                    new HudSwatch(ResolveHudSwatchColor(_config.ZoningCommercialHighColor), "Bleus = Commercial / Retail"),
                    new HudSwatch(ResolveHudSwatchColor(_config.ZoningIndustrialColor), "Ambre = Industrie"),
                    new HudSwatch(ResolveHudSwatchColor(_config.ZoningOfficeColor), "Violet = Bureaux"),
                    new HudSwatch(ResolveHudSwatchColor(_config.ZoningMixedColor), "Turquoise = Usage mixte"),
                    new HudSwatch(ResolveHudSwatchColor(_config.ZoningRampColor), "Verts vifs = Parkings"),
                    new HudSwatch(ResolveHudSwatchColor(_config.WaterAreaFillColor), "Bleus eau = Hydrographie"),
                    new HudSwatch(ResolveHudSwatchColor(_config.RoadColorMotorway), "Rouge→blanc = Routes par importance")
                };

                for (var i = 0; i < families.Count; i += 2)
                    DrawHudSwatchRow(families, i, 2);
            }

            GUILayout.EndVertical();
            GUILayout.Space(8f);
        }
    }
}
