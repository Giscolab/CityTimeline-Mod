using System;
using System.Collections.Generic;
using Colossal.UI.Binding;
using CityTimelineMod.Rendering;

namespace CityTimelineMod.UI
{
    internal sealed partial class CityTimelineUISystem
    {
        private static readonly string[] RoadHighwayKeys =
        {
            "motorway",
            "trunk",
            "primary",
            "secondary",
            "tertiary",
            "motorway_link",
            "trunk_link",
            "primary_link",
            "secondary_link",
            "tertiary_link"
        };

        private ValueBinding<bool> _roadMotorwayVisibleBinding;
        private ValueBinding<bool> _roadTrunkVisibleBinding;
        private ValueBinding<bool> _roadPrimaryVisibleBinding;
        private ValueBinding<bool> _roadSecondaryVisibleBinding;
        private ValueBinding<bool> _roadTertiaryVisibleBinding;

        private ValueBinding<bool> _roadMotorwayLinkVisibleBinding;
        private ValueBinding<bool> _roadTrunkLinkVisibleBinding;
        private ValueBinding<bool> _roadPrimaryLinkVisibleBinding;
        private ValueBinding<bool> _roadSecondaryLinkVisibleBinding;
        private ValueBinding<bool> _roadTertiaryLinkVisibleBinding;

        private ValueBinding<string> _roadHighwayFilterBinding;

        private TriggerBinding<bool> _setRoadMotorwayVisibleBinding;
        private TriggerBinding<bool> _setRoadTrunkVisibleBinding;
        private TriggerBinding<bool> _setRoadPrimaryVisibleBinding;
        private TriggerBinding<bool> _setRoadSecondaryVisibleBinding;
        private TriggerBinding<bool> _setRoadTertiaryVisibleBinding;

        private TriggerBinding<bool> _setRoadMotorwayLinkVisibleBinding;
        private TriggerBinding<bool> _setRoadTrunkLinkVisibleBinding;
        private TriggerBinding<bool> _setRoadPrimaryLinkVisibleBinding;
        private TriggerBinding<bool> _setRoadSecondaryLinkVisibleBinding;
        private TriggerBinding<bool> _setRoadTertiaryLinkVisibleBinding;

        private TriggerBinding _setAllRoadHighwaysVisibleBinding;
        private TriggerBinding _clearAllRoadHighwaysBinding;

        private void CreateRoadBindings()
        {
            _roadMotorwayVisibleBinding =
                new ValueBinding<bool>(BindingGroup, "roadMotorwayVisible", true);

            _roadTrunkVisibleBinding =
                new ValueBinding<bool>(BindingGroup, "roadTrunkVisible", true);

            _roadPrimaryVisibleBinding =
                new ValueBinding<bool>(BindingGroup, "roadPrimaryVisible", true);

            _roadSecondaryVisibleBinding =
                new ValueBinding<bool>(BindingGroup, "roadSecondaryVisible", true);

            _roadTertiaryVisibleBinding =
                new ValueBinding<bool>(BindingGroup, "roadTertiaryVisible", true);

            _roadMotorwayLinkVisibleBinding =
                new ValueBinding<bool>(BindingGroup, "roadMotorwayLinkVisible", true);

            _roadTrunkLinkVisibleBinding =
                new ValueBinding<bool>(BindingGroup, "roadTrunkLinkVisible", true);

            _roadPrimaryLinkVisibleBinding =
                new ValueBinding<bool>(BindingGroup, "roadPrimaryLinkVisible", true);

            _roadSecondaryLinkVisibleBinding =
                new ValueBinding<bool>(BindingGroup, "roadSecondaryLinkVisible", true);

            _roadTertiaryLinkVisibleBinding =
                new ValueBinding<bool>(BindingGroup, "roadTertiaryLinkVisible", true);

            _roadHighwayFilterBinding =
                new ValueBinding<string>(BindingGroup, "roadHighwayFilter", "all");

            AddRuntimeBinding(_roadMotorwayVisibleBinding);
            AddRuntimeBinding(_roadTrunkVisibleBinding);
            AddRuntimeBinding(_roadPrimaryVisibleBinding);
            AddRuntimeBinding(_roadSecondaryVisibleBinding);
            AddRuntimeBinding(_roadTertiaryVisibleBinding);

            AddRuntimeBinding(_roadMotorwayLinkVisibleBinding);
            AddRuntimeBinding(_roadTrunkLinkVisibleBinding);
            AddRuntimeBinding(_roadPrimaryLinkVisibleBinding);
            AddRuntimeBinding(_roadSecondaryLinkVisibleBinding);
            AddRuntimeBinding(_roadTertiaryLinkVisibleBinding);

            AddRuntimeBinding(_roadHighwayFilterBinding);

            _setRoadMotorwayVisibleBinding =
                CreateRoadHighwayTrigger("setRoadMotorwayVisible", "motorway");

            _setRoadTrunkVisibleBinding =
                CreateRoadHighwayTrigger("setRoadTrunkVisible", "trunk");

            _setRoadPrimaryVisibleBinding =
                CreateRoadHighwayTrigger("setRoadPrimaryVisible", "primary");

            _setRoadSecondaryVisibleBinding =
                CreateRoadHighwayTrigger("setRoadSecondaryVisible", "secondary");

            _setRoadTertiaryVisibleBinding =
                CreateRoadHighwayTrigger("setRoadTertiaryVisible", "tertiary");

            _setRoadMotorwayLinkVisibleBinding =
                CreateRoadHighwayTrigger(
                    "setRoadMotorwayLinkVisible",
                    "motorway_link"
                );

            _setRoadTrunkLinkVisibleBinding =
                CreateRoadHighwayTrigger(
                    "setRoadTrunkLinkVisible",
                    "trunk_link"
                );

            _setRoadPrimaryLinkVisibleBinding =
                CreateRoadHighwayTrigger(
                    "setRoadPrimaryLinkVisible",
                    "primary_link"
                );

            _setRoadSecondaryLinkVisibleBinding =
                CreateRoadHighwayTrigger(
                    "setRoadSecondaryLinkVisible",
                    "secondary_link"
                );

            _setRoadTertiaryLinkVisibleBinding =
                CreateRoadHighwayTrigger(
                    "setRoadTertiaryLinkVisible",
                    "tertiary_link"
                );

            _setAllRoadHighwaysVisibleBinding =
                new TriggerBinding(
                    BindingGroup,
                    "setAllRoadHighwaysVisible",
                    () => SetRoadHighwayFilter("all")
                );

            _clearAllRoadHighwaysBinding =
                new TriggerBinding(
                    BindingGroup,
                    "clearAllRoadHighways",
                    () => SetRoadHighwayFilter("(none)")
                );

            AddRuntimeBinding(_setRoadMotorwayVisibleBinding);
            AddRuntimeBinding(_setRoadTrunkVisibleBinding);
            AddRuntimeBinding(_setRoadPrimaryVisibleBinding);
            AddRuntimeBinding(_setRoadSecondaryVisibleBinding);
            AddRuntimeBinding(_setRoadTertiaryVisibleBinding);

            AddRuntimeBinding(_setRoadMotorwayLinkVisibleBinding);
            AddRuntimeBinding(_setRoadTrunkLinkVisibleBinding);
            AddRuntimeBinding(_setRoadPrimaryLinkVisibleBinding);
            AddRuntimeBinding(_setRoadSecondaryLinkVisibleBinding);
            AddRuntimeBinding(_setRoadTertiaryLinkVisibleBinding);

            AddRuntimeBinding(_setAllRoadHighwaysVisibleBinding);
            AddRuntimeBinding(_clearAllRoadHighwaysBinding);

            SyncRoadBindings();
        }

        private TriggerBinding<bool> CreateRoadHighwayTrigger(
            string triggerName,
            string highway)
        {
            return new TriggerBinding<bool>(
                BindingGroup,
                triggerName,
                visible => SetRoadHighwayVisible(highway, visible)
            );
        }

        private void SetRoadHighwayVisible(string highway, bool visible)
        {
            if (!RuntimeCallbacksAllowed)
                return;

            var config = Mod.RuntimeConfig;
            if (config == null)
                return;

            var enabled = ParseRoadHighwayFilter(config.RoadHighwayFilter);

            if (IsAllRoadHighwaysEnabled(config.RoadHighwayFilter))
            {
                enabled.Clear();

                for (var i = 0; i < RoadHighwayKeys.Length; i++)
                    enabled.Add(RoadHighwayKeys[i]);
            }

            if (visible)
                enabled.Add(highway);
            else
                enabled.Remove(highway);

            var filter = BuildRoadHighwayFilter(enabled);
            SetRoadHighwayFilter(filter);
        }

        private void SetRoadHighwayFilter(string filter)
        {
            if (!RuntimeCallbacksAllowed)
                return;

            if (!GeoDebugOverlay.SetRoadHighwayFilter(filter))
                return;

            SyncRoadBindings();
        }

        private void SyncRoadBindings()
        {
            if (!RuntimeCallbacksAllowed)
                return;

            var config = Mod.RuntimeConfig;
            if (config == null)
                return;

            var filter = string.IsNullOrWhiteSpace(config.RoadHighwayFilter)
                ? "all"
                : config.RoadHighwayFilter.Trim().ToLowerInvariant();

            var all = IsAllRoadHighwaysEnabled(filter);
            var enabled = ParseRoadHighwayFilter(filter);

            UpdateBinding(
                _roadMotorwayVisibleBinding,
                all || enabled.Contains("motorway")
            );

            UpdateBinding(
                _roadTrunkVisibleBinding,
                all || enabled.Contains("trunk")
            );

            UpdateBinding(
                _roadPrimaryVisibleBinding,
                all || enabled.Contains("primary")
            );

            UpdateBinding(
                _roadSecondaryVisibleBinding,
                all || enabled.Contains("secondary")
            );

            UpdateBinding(
                _roadTertiaryVisibleBinding,
                all || enabled.Contains("tertiary")
            );

            UpdateBinding(
                _roadMotorwayLinkVisibleBinding,
                all || enabled.Contains("motorway_link")
            );

            UpdateBinding(
                _roadTrunkLinkVisibleBinding,
                all || enabled.Contains("trunk_link")
            );

            UpdateBinding(
                _roadPrimaryLinkVisibleBinding,
                all || enabled.Contains("primary_link")
            );

            UpdateBinding(
                _roadSecondaryLinkVisibleBinding,
                all || enabled.Contains("secondary_link")
            );

            UpdateBinding(
                _roadTertiaryLinkVisibleBinding,
                all || enabled.Contains("tertiary_link")
            );

            UpdateBinding(_roadHighwayFilterBinding, filter);
        }

        private static bool IsAllRoadHighwaysEnabled(string filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
                return true;

            var normalized = filter.Trim().ToLowerInvariant();

            return normalized == "all" || normalized == "*";
        }

        private static HashSet<string> ParseRoadHighwayFilter(string filter)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(filter))
                return result;

            var parts = filter.Split(',');

            for (var i = 0; i < parts.Length; i++)
            {
                var value = parts[i].Trim().ToLowerInvariant();

                if (string.IsNullOrWhiteSpace(value) ||
                    value == "all" ||
                    value == "*")
                {
                    continue;
                }

                result.Add(value);
            }

            return result;
        }

        private static string BuildRoadHighwayFilter(
            HashSet<string> enabled)
        {
            if (enabled == null || enabled.Count == 0)
                return "(none)";

            if (enabled.Count == RoadHighwayKeys.Length)
            {
                var complete = true;

                for (var i = 0; i < RoadHighwayKeys.Length; i++)
                {
                    if (!enabled.Contains(RoadHighwayKeys[i]))
                    {
                        complete = false;
                        break;
                    }
                }

                if (complete)
                    return "all";
            }

            var values = new List<string>();

            for (var i = 0; i < RoadHighwayKeys.Length; i++)
            {
                var key = RoadHighwayKeys[i];

                if (enabled.Contains(key))
                    values.Add(key);
            }

            return string.Join(",", values.ToArray());
        }
    }
}
