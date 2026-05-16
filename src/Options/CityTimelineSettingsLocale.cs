using System.Collections.Generic;
using Colossal;
using Colossal.Localization;
using Game.Settings;

namespace CityTimelineMod.Options
{
    public sealed class CityTimelineSettingsLocale : IDictionarySource
    {
        private readonly CityTimelineSettings _settings;

        public CityTimelineSettingsLocale(CityTimelineSettings settings)
        {
            _settings = settings;
        }

        public IEnumerable<KeyValuePair<string, string>> ReadEntries(
            IList<IDictionaryEntryError> errors,
            Dictionary<string, int> indexCounts)
        {
            return new Dictionary<string, string>
            {
                { _settings.GetSettingsLocaleID(), "CityTimelineMod" },

                { _settings.GetOptionTabLocaleID(CityTimelineSettings.SectionGeneral), "Général" },
                { _settings.GetOptionGroupLocaleID(CityTimelineSettings.GroupMain), "Activation et couches" },

                { _settings.GetOptionLabelLocaleID(nameof(CityTimelineSettings.ModEnabled)), "Activer CityTimelineMod" },
                { _settings.GetOptionDescLocaleID(nameof(CityTimelineSettings.ModEnabled)), "Active ou désactive le comportement principal du mod au prochain chargement." },

                { _settings.GetOptionLabelLocaleID(nameof(CityTimelineSettings.ShowOverlayHud)), "Afficher le HUD debug" },
                { _settings.GetOptionDescLocaleID(nameof(CityTimelineSettings.ShowOverlayHud)), "Affiche ou masque le HUD local de debug au prochain chargement." },

                { _settings.GetOptionLabelLocaleID(nameof(CityTimelineSettings.RenderRoads)), "Afficher les routes" },
                { _settings.GetOptionDescLocaleID(nameof(CityTimelineSettings.RenderRoads)), "Active le rendu des routes issues du bundle GeoJSON." },

                { _settings.GetOptionLabelLocaleID(nameof(CityTimelineSettings.RenderPaths)), "Afficher les chemins" },
                { _settings.GetOptionDescLocaleID(nameof(CityTimelineSettings.RenderPaths)), "Active le rendu des chemins et paths issus du bundle GeoJSON." },

                { _settings.GetOptionLabelLocaleID(nameof(CityTimelineSettings.RenderWaterLines)), "Afficher les lignes d'eau" },
                { _settings.GetOptionDescLocaleID(nameof(CityTimelineSettings.RenderWaterLines)), "Active le rendu des rivières, cours d'eau et lignes d'eau." },

                { _settings.GetOptionLabelLocaleID(nameof(CityTimelineSettings.RenderWaterAreas)), "Afficher les surfaces d'eau" },
                { _settings.GetOptionDescLocaleID(nameof(CityTimelineSettings.RenderWaterAreas)), "Active le rendu des polygones d'eau." },

                { _settings.GetOptionLabelLocaleID(nameof(CityTimelineSettings.RenderZoning)), "Afficher le zoning" },
                { _settings.GetOptionDescLocaleID(nameof(CityTimelineSettings.RenderZoning)), "Active le rendu des polygones de zoning." }
            };
        }

        public void Unload()
        {
        }
    }
}

