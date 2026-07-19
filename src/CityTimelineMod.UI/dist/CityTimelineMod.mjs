/*!
 * Cities: Skylines II UI Module
 * Id: CityTimelineMod
 * Generated with the repository TypeScript compiler (Node-free fallback).
 */
var React = window.React;
var __ctmModules = {"CityTimelineHUD.tsx": function(module, exports, require) {
"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.cohtmlHudVisible$ = void 0;
exports.CityTimelineHUDButton = CityTimelineHUDButton;
exports.CityTimelineHUDHost = CityTimelineHUDHost;
const react_1 = require("react");
const api_1 = require("cs2/api");
const ui_1 = require("cs2/ui");
const Controls_1 = require("./components/Controls");
const RoadCatalog_1 = require("./components/RoadCatalog");
const RailwayPanel_1 = require("./components/RailwayPanel");
const ServicesPanel_1 = require("./components/ServicesPanel");
const StatisticsPanel_1 = require("./components/StatisticsPanel");
const ZoningPanel_1 = require("./components/ZoningPanel");
const bundleStats_1 = require("./components/bundleStats");
const tabs = [
    {
        id: "p1",
        label: "Principal",
        count: "01·02",
    },
    {
        id: "p2",
        label: "Import routes CS2",
        count: "03",
    },
    {
        id: "p3",
        label: "Bundle & overlay",
        count: "04–06",
    },
    {
        id: "stats",
        label: "Statistiques",
        count: "DATA",
    },
    {
        id: "p4",
        label: "Couches & rendu",
        count: "07–11",
    },
    {
        id: "p5",
        label: "Calage avancé",
        count: "12",
    },
];
class CityTimelineHudErrorBoundary extends react_1.Component {
    constructor() {
        super(...arguments);
        this.state = {
            error: null,
        };
    }
    componentDidCatch(error, info) {
        const message = error && error.stack
            ? error.stack
            : String(error);
        console.error("[CityTimelineMod UI] React render error", message, info.componentStack);
        this.setState({
            error: message,
        });
    }
    render() {
        if (this.state.error) {
            return (React.createElement("div", { className: "ctm-hud-host" },
                React.createElement("div", { className: "ctm-hud-error" },
                    React.createElement("strong", null, "CityTimelineMod UI \u00B7 erreur React"),
                    React.createElement("pre", null, this.state.error))));
        }
        return this.props.children;
    }
}
exports.cohtmlHudVisible$ = (0, api_1.bindValue)("CityTimelineMod", "cohtmlHudVisible", false);
function CityTimelineHUDButton() {
    const visible = (0, api_1.useValue)(exports.cohtmlHudVisible$);
    return (React.createElement(ui_1.Button, { variant: "floating", className: `ctm-launcher ${visible ? "is-active" : ""}`, "aria-label": visible ? "Fermer CityTimelineMod" : "Ouvrir CityTimelineMod", "aria-pressed": visible, onSelect: () => (0, api_1.trigger)("CityTimelineMod", "toggleCohtmlHud") }, "CTM"));
}
function CityTimelineHUDHost() {
    const visible = (0, api_1.useValue)(exports.cohtmlHudVisible$);
    console.info("[CityTimelineMod UI] CityTimelineHUDHost rendering; visible=", visible);
    if (!visible) {
        return null;
    }
    // Find It renders its main container directly from moduleRegistry.append("Game", ...).
    // No Portal: the gameplay root owns the component lifecycle and layout.
    return (React.createElement(CityTimelineHudErrorBoundary, null,
        React.createElement(CityTimelineHUD, { onClose: () => (0, api_1.trigger)("CityTimelineMod", "closeCohtmlHud") })));
}
function CityTimelineHUD({ onClose, }) {
    console.info("[CityTimelineMod UI] CityTimelineHUD rendering");
    const [activeTab, setActiveTab] = (0, react_1.useState)("p1");
    const stats = (0, bundleStats_1.useBundleStats)();
    return (React.createElement("div", { className: "ctm-hud-host" },
        React.createElement("div", { className: "ctm-hud ctm-game-ui hookui-loaded" },
            React.createElement("div", { className: "window" },
                React.createElement("div", { className: "window-title header_H_U header_Bpo child-opacity-transition_nkS" },
                    React.createElement("div", { className: "title-bar_PF4 ctm-title-bar" },
                        React.createElement("div", { className: "icon-space_h_f ctm-title-icon", "aria-hidden": "true" }),
                        React.createElement("div", { className: "title_SVH title_zQN ctm-title-text" }, "CityTimelineMod \u2014 Panneau HUD"),
                        React.createElement("span", { className: "hint" }, "DEV"),
                        React.createElement("button", { type: "button", className: "ctm-close button_bvQ close-button_wKK", "aria-label": "Fermer le HUD", onClick: onClose },
                            React.createElement("span", { className: "ctm-close-icon", "aria-hidden": "true" }, "\u00D7")))),
                React.createElement("div", { className: "window-content content_XD5 content_AD7 child-opacity-transition_nkS" },
                    React.createElement("div", { className: "tabs", role: "tablist", "aria-label": "Sections du HUD" }, tabs.map((tab) => (React.createElement("button", { key: tab.id, className: "tab button_WWa", role: "tab", "aria-selected": activeTab === tab.id, type: "button", onClick: () => setActiveTab(tab.id) },
                        tab.label,
                        React.createElement("span", { className: "count" }, tab.count))))),
                    React.createElement("div", { className: "scroll" },
                        activeTab === "p1" ? (React.createElement(PrincipalPane, { stats: stats })) : null,
                        activeTab === "p2" ? (React.createElement(RoadCatalog_1.ImportRoutesPane, null)) : null,
                        activeTab === "p3" ? (React.createElement(BundleOverlayPane, { stats: stats })) : null,
                        activeTab === "stats" ? (React.createElement(StatisticsPanel_1.StatisticsPanel, { stats: stats })) : null,
                        activeTab === "p4" ? (React.createElement(DisplayRenderPane, { stats: stats })) : null,
                        activeTab === "p5" ? (React.createElement(CalibrationPane, null)) : null),
                    React.createElement(Footer, { onClose: onClose, stats: stats }))))));
}
function PrincipalPane({ stats }) {
    const bundleLabel = stats.bundleName || stats.bundleId || "Bundle actif";
    const roads = (0, bundleStats_1.getMetric)(stats.roads, "total", "roads", "roadTotal", "road_total");
    const paths = (0, bundleStats_1.getMetric)(stats.roads, "paths", "pathways", "pathway");
    const servicePoints = stats.services.some((family) => family.total !== null)
        ? stats.services.reduce((total, family) => total + (family.total ?? 0), 0)
        : null;
    const railwayTotal = (0, bundleStats_1.getMetric)(stats.railway, "total");
    return (React.createElement("div", { className: "ctm-pane" },
        React.createElement(Controls_1.Section, { num: "01", title: "Presets affichage", state: "always", note: "Actions directes du mockup. D\u00E9sactiv\u00E9es \u00E0 cette \u00E9tape." },
            React.createElement(Controls_1.Row, { columns: 3 },
                React.createElement(Controls_1.HudButton, { variant: "primary", disabled: true }, "Debug"),
                React.createElement(Controls_1.HudButton, { variant: "primary", disabled: true }, "Lisible"),
                React.createElement(Controls_1.HudButton, { variant: "primary", disabled: true }, "Routes"),
                React.createElement(Controls_1.HudButton, { variant: "primary", disabled: true }, "Eau"),
                React.createElement(Controls_1.HudButton, { variant: "primary", disabled: true }, "Zoning"),
                React.createElement(Controls_1.HudButton, { variant: "primary", disabled: true }, "Bounds"))),
        React.createElement(Controls_1.Section, { num: "02", title: "R\u00E9sum\u00E9", state: stats.available ? "connecté" : "indisponible", note: stats.status },
            React.createElement(Controls_1.Readout, null,
                bundleLabel,
                stats.bundleId && stats.bundleName ? ` · ${stats.bundleId}` : ""),
            React.createElement(Controls_1.Readout, null,
                "objets OSM ",
                (0, bundleStats_1.formatCount)(stats.objects),
                " \u00B7 routes ",
                (0, bundleStats_1.formatCount)(roads),
                " \u00B7 chemins ",
                (0, bundleStats_1.formatCount)(paths)),
            React.createElement(Controls_1.Readout, null,
                "services ",
                (0, bundleStats_1.formatCount)(servicePoints),
                " \u00B7 r\u00E9seau ferroviaire ",
                (0, bundleStats_1.formatCount)(railwayTotal),
                " voies"))));
}
function BundleOverlayPane({ stats }) {
    const bundleLabel = stats.bundleName || stats.bundleId || "Bundle actif";
    const roads = (0, bundleStats_1.getMetric)(stats.roads, "total", "roads", "roadTotal", "road_total");
    const paths = (0, bundleStats_1.getMetric)(stats.roads, "paths", "pathways", "pathway");
    const waterLines = (0, bundleStats_1.getMetric)(stats.water, "lines", "lineCount", "line_count");
    const waterAreas = (0, bundleStats_1.getMetric)(stats.water, "areas", "areaCount", "area_count");
    return (React.createElement("div", { className: "ctm-pane" },
        React.createElement(Controls_1.Foldout, { num: "04", title: "D\u00E9tails bundle / eau", state: stats.available ? "connecté" : "indisponible", note: stats.status, defaultOpen: true },
            React.createElement(Controls_1.Readout, null,
                bundleLabel,
                stats.bundleId && stats.bundleName ? ` · ${stats.bundleId}` : ""),
            React.createElement(Controls_1.Row, { columns: 2 },
                React.createElement(Controls_1.HudButton, { disabled: true }, "Pr\u00E9c\u00E9dent"),
                React.createElement(Controls_1.HudButton, { disabled: true }, "Suivant"),
                React.createElement(Controls_1.HudButton, { disabled: true }, "Charger bundle s\u00E9lectionn\u00E9"),
                React.createElement(Controls_1.HudButton, { disabled: true }, "Recharger catalogue")),
            React.createElement(Controls_1.Readout, null,
                (0, bundleStats_1.formatCount)(roads),
                " routes \u00B7 ",
                (0, bundleStats_1.formatCount)(paths),
                " chemins"),
            React.createElement(Controls_1.Readout, null,
                (0, bundleStats_1.formatCount)(waterLines),
                " lignes d\u2019eau \u00B7 ",
                (0, bundleStats_1.formatCount)(waterAreas),
                " surfaces d\u2019eau"),
            React.createElement(Controls_1.Readout, null,
                (0, bundleStats_1.formatCount)(stats.objects),
                " objets OSM exploitables")),
        React.createElement(Controls_1.Foldout, { num: "05", title: "\u00C9tat overlay / rebuild", state: "non expos\u00E9", note: "Ce contrat de statistiques ne fournit pas encore la t\u00E9l\u00E9m\u00E9trie du rebuild." },
            React.createElement(Controls_1.Readout, null, "Aucune m\u00E9trique de rebuild re\u00E7ue. Aucun z\u00E9ro de d\u00E9monstration n\u2019est affich\u00E9."),
            React.createElement(Controls_1.HudButton, { disabled: true }, "Annuler rebuild")),
        React.createElement(Controls_1.Foldout, { num: "06", title: "Statistiques routes GeoJSON", state: stats.available ? "connecté" : "indisponible", note: "Utile pour valider le bundle." },
            React.createElement(Controls_1.Readout, null,
                (0, bundleStats_1.formatCount)(roads),
                " routes charg\u00E9es"),
            React.createElement(Controls_1.Readout, null,
                (0, bundleStats_1.formatCount)(paths),
                " chemins charg\u00E9s"),
            React.createElement(Controls_1.Readout, null,
                "autoroutes ",
                (0, bundleStats_1.formatCount)((0, bundleStats_1.getMetric)(stats.roads, "highway", "motorway")),
                " \u00B7 axes principaux ",
                (0, bundleStats_1.formatCount)((0, bundleStats_1.getMetric)(stats.roads, "main", "mainRoad", "largeRoad", "large_road", "primary"))),
            React.createElement(Controls_1.Readout, null,
                "secondaires ",
                (0, bundleStats_1.formatCount)((0, bundleStats_1.getMetric)(stats.roads, "secondary", "secondaryRoad", "mediumRoad", "medium_road")),
                " \u00B7 tertiaires/r\u00E9sidentielles ",
                (0, bundleStats_1.formatCount)((0, bundleStats_1.getMetric)(stats.roads, "tertiary", "tertiaryResidential", "tertiary_residential", "smallRoad", "small_road"))),
            React.createElement(Controls_1.Readout, null,
                "bretelles ",
                (0, bundleStats_1.formatCount)((0, bundleStats_1.getMetric)(stats.roads, "ramp", "link", "rampLink", "ramp_link")),
                " \u00B7 non class\u00E9es ",
                (0, bundleStats_1.formatCount)((0, bundleStats_1.getMetric)(stats.roads, "unclassified", "unclassifiedRoad", "gravelRoad", "gravel_road", "other"))))));
}
function DisplayRenderPane({ stats }) {
    return (React.createElement("div", { className: "ctm-pane" },
        React.createElement(Controls_1.Foldout, { num: "07", title: "Couches visibles / transparence", state: "foldout open by default", note: "Bug d'origine corrig\u00E9 : chaque foldout est ind\u00E9pendant.", noteTone: "bug", defaultOpen: true },
            React.createElement(Controls_1.GroupTitle, null, "Couches"),
            React.createElement(Controls_1.Row, { columns: 2 },
                React.createElement(Controls_1.ToggleField, { id: "ctm-i11", defaultChecked: true }, "Zonage"),
                React.createElement(Controls_1.ToggleField, { id: "ctm-i12", defaultChecked: true }, "Routes"),
                React.createElement(Controls_1.ToggleField, { id: "ctm-i13", defaultChecked: true }, "Chemins"),
                React.createElement(Controls_1.ToggleField, { id: "ctm-i14", defaultChecked: true }, "Eau"),
                React.createElement(Controls_1.ToggleField, { id: "ctm-i15" }, "Bounds"),
                React.createElement(Controls_1.ToggleField, { id: "ctm-i16" }, "Debug labels")),
            React.createElement(Controls_1.GroupTitle, null, "Transparence"),
            React.createElement(Controls_1.SliderField, { label: "Alpha zoning", min: 0, max: 100, defaultValue: 65, suffix: "%" }),
            React.createElement(Controls_1.SliderField, { label: "Alpha routes", min: 0, max: 100, defaultValue: 88, suffix: "%" }),
            React.createElement(Controls_1.SliderField, { label: "Alpha eau", min: 0, max: 100, defaultValue: 70, suffix: "%" }),
            React.createElement(Controls_1.GroupTitle, null, "Zoning"),
            React.createElement(ZoningPanel_1.ZoningPanel, null)),
        React.createElement(RailwayPanel_1.RailwayPanel, null),
        React.createElement(ServicesPanel_1.ServicesPanel, { services: stats.services, status: stats.status }),
        React.createElement(Controls_1.Foldout, { num: "10", title: "Performance overlay", state: "foldout", note: "\u00C0 ouvrir seulement pour v\u00E9rifier le co\u00FBt du rendu." },
            React.createElement(Controls_1.Row, { columns: 2 },
                React.createElement(Controls_1.ToggleField, { id: "ctm-i17" }, "Limiter chunks par frame"),
                React.createElement(Controls_1.ToggleField, { id: "ctm-i18" }, "Dessiner bounds chunks"),
                React.createElement(Controls_1.ToggleField, { id: "ctm-i19" }, "Mode basse charge"),
                React.createElement(Controls_1.ToggleField, { id: "ctm-i20" }, "Mesure temps overlay")),
            React.createElement(Controls_1.SliderField, { label: "Max chunks visibles", min: 0, max: 256, defaultValue: 64 }),
            React.createElement(Controls_1.SliderField, { label: "Budget ms overlay", min: 1, max: 32, defaultValue: 8 }),
            React.createElement(Controls_1.Readout, null, "T\u00E9l\u00E9m\u00E9trie de performance non expos\u00E9e par bundleStatsJson.")),
        React.createElement(Controls_1.Foldout, { num: "11", title: "Rendu avanc\u00E9 routes / filtres", state: "foldout", note: "Filtres visuels locaux pour pr\u00E9parer la cartographie des bindings." },
            React.createElement(Controls_1.GroupTitle, null, "Styles routes"),
            React.createElement(Controls_1.Row, { columns: 2 },
                React.createElement(Controls_1.ToggleField, { id: "ctm-i21", defaultChecked: true }, "Afficher routes nomm\u00E9es"),
                React.createElement(Controls_1.ToggleField, { id: "ctm-i22", defaultChecked: true }, "Afficher routes sans nom"),
                React.createElement(Controls_1.ToggleField, { id: "ctm-i23" }, "Routes oneway uniquement"),
                React.createElement(Controls_1.ToggleField, { id: "ctm-i24" }, "Ponts uniquement"),
                React.createElement(Controls_1.ToggleField, { id: "ctm-i25" }, "Tunnels uniquement"),
                React.createElement(Controls_1.ToggleField, { id: "ctm-i26" }, "Highlight maxspeed")),
            React.createElement(Controls_1.SliderField, { label: "Largeur lignes routes", min: 1, max: 20, defaultValue: 6 }),
            React.createElement(Controls_1.SliderField, { label: "Seuil maxspeed", min: 0, max: 240, defaultValue: 90 }))));
}
function CalibrationPane() {
    const [unlocked, setUnlocked] = (0, react_1.useState)(false);
    return (React.createElement("div", { className: "ctm-pane" },
        React.createElement(Controls_1.Foldout, { num: "12", title: "Calage avanc\u00E9", state: "foldout \u00B7 verrouill\u00E9", note: "Doit rester verrouill\u00E9 par d\u00E9faut." },
            React.createElement(Controls_1.ToggleField, { id: "ctm-i27", defaultChecked: unlocked, onChange: setUnlocked }, "D\u00E9verrouiller le calage"),
            React.createElement("div", { className: `calage-body ${unlocked
                    ? "is-unlocked"
                    : "is-locked"}` },
                React.createElement(Controls_1.SliderField, { label: "Offset X", min: -1000, max: 1000, defaultValue: 0, disabled: !unlocked }),
                React.createElement(Controls_1.SliderField, { label: "Offset Z", min: -1000, max: 1000, defaultValue: 0, disabled: !unlocked }),
                React.createElement(Controls_1.SliderField, { label: "Rotation degr\u00E9s", min: -180, max: 180, defaultValue: 0, disabled: !unlocked }),
                React.createElement(Controls_1.SliderField, { label: "Scale X", min: 1, max: 300, defaultValue: 100, disabled: !unlocked }),
                React.createElement(Controls_1.SliderField, { label: "Scale Z", min: 1, max: 300, defaultValue: 100, disabled: !unlocked }),
                React.createElement(Controls_1.Readout, null, "worldScale lecture seule")))));
}
function Footer({ onClose, stats, }) {
    const bundleLabel = stats.bundleName || stats.bundleId || "Bundle actif";
    return (React.createElement("div", { className: "footer" },
        React.createElement("div", { className: "footer-head" },
            React.createElement("span", { className: "num" }, "13"),
            React.createElement("h2", null, "Actions finales"),
            React.createElement("span", { className: "state always" }, "always bottom \u00B7 \u00E9pingl\u00E9")),
        React.createElement("div", { className: "meta" },
            React.createElement(Controls_1.Readout, null, stats.available ? `${bundleLabel} · données connectées` : stats.status),
            React.createElement(Controls_1.Readout, null, "HUD live \u00B7 statistiques synchronis\u00E9es")),
        React.createElement("div", { className: "actions" },
            React.createElement(Controls_1.HudButton, { variant: "primary", disabled: true, centered: true }, "Appliquer / reconstruire"),
            React.createElement(Controls_1.HudButton, { disabled: true, centered: true }, "Sauvegarder visuels"),
            React.createElement(Controls_1.HudButton, { disabled: true, centered: true }, "Recharger visuels"),
            React.createElement(Controls_1.HudButton, { centered: true, onClick: onClose }, "Fermer"))));
}

},
"components/bundleStats.ts": function(module, exports, require) {
"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.bundleStatsJson$ = void 0;
exports.parseBundleStatsJson = parseBundleStatsJson;
exports.useBundleStats = useBundleStats;
exports.formatCount = formatCount;
exports.getMetric = getMetric;
const api_1 = require("cs2/api");
const BINDING_GROUP = "CityTimelineMod";
exports.bundleStatsJson$ = (0, api_1.bindValue)(BINDING_GROUP, "bundleStatsJson", "{}");
const EMPTY_STATS = {
    available: false,
    status: "Statistiques du bundle indisponibles.",
    bundleId: "",
    bundleName: "",
    objects: null,
    zoning: {},
    roads: {},
    water: {},
    services: [],
    railway: {},
};
function isRecord(value) {
    return typeof value === "object" && value !== null && !Array.isArray(value);
}
function asString(value) {
    return typeof value === "string" ? value.trim() : "";
}
function asCount(value) {
    if (typeof value !== "number" || !Number.isFinite(value)) {
        return null;
    }
    return Math.max(0, Math.trunc(value));
}
function asOpacity(value) {
    if (typeof value !== "number" || !Number.isFinite(value)) {
        return 1;
    }
    return Math.min(1, Math.max(0, value));
}
function asMetrics(value) {
    if (!isRecord(value)) {
        return {};
    }
    const result = {};
    Object.keys(value).forEach((key) => {
        const count = asCount(value[key]);
        if (count !== null) {
            result[key] = count;
        }
    });
    return result;
}
function asSubcategories(value) {
    if (!Array.isArray(value)) {
        return [];
    }
    return value.flatMap((item) => {
        if (!isRecord(item)) {
            return [];
        }
        const key = asString(item.key);
        const label = asString(item.label) || key;
        if (!key) {
            return [];
        }
        return [{
                key,
                label,
                count: asCount(item.count),
            }];
    });
}
function asServices(value) {
    if (!Array.isArray(value)) {
        return [];
    }
    return value.flatMap((item) => {
        if (!isRecord(item)) {
            return [];
        }
        const key = asString(item.key);
        const label = asString(item.label) || key;
        if (!key) {
            return [];
        }
        return [{
                key,
                label,
                available: item.available === true,
                total: asCount(item.total),
                visible: item.visible !== false,
                opacity: asOpacity(item.opacity),
                subcategories: asSubcategories(item.subcategories),
            }];
    });
}
function parseBundleStatsJson(json) {
    if (!json || !json.trim()) {
        return EMPTY_STATS;
    }
    try {
        const value = JSON.parse(json);
        if (!isRecord(value)) {
            return EMPTY_STATS;
        }
        const available = value.available === true;
        return {
            available,
            status: asString(value.status) || (available ? "Données du bundle chargées." : EMPTY_STATS.status),
            bundleId: asString(value.bundleId),
            bundleName: asString(value.bundleName),
            objects: asCount(value.objects),
            zoning: asMetrics(value.zoning),
            roads: asMetrics(value.roads),
            water: asMetrics(value.water),
            services: asServices(value.services),
            railway: asMetrics(value.railway),
        };
    }
    catch (error) {
        console.error("[CityTimelineMod UI] Invalid bundleStatsJson binding", error);
        return {
            ...EMPTY_STATS,
            status: "Statistiques du bundle illisibles.",
        };
    }
}
function useBundleStats() {
    return parseBundleStatsJson((0, api_1.useValue)(exports.bundleStatsJson$));
}
function formatCount(value) {
    if (value === null || value === undefined || !Number.isFinite(value)) {
        return "—";
    }
    const safeValue = Math.max(0, Math.trunc(value));
    return String(safeValue).replace(/\B(?=(\d{3})+(?!\d))/g, " ");
}
function normalizeMetricKey(value) {
    return value.toLocaleLowerCase().replace(/[^a-z0-9]/g, "");
}
function getMetric(metrics, ...aliases) {
    const normalizedAliases = aliases.map(normalizeMetricKey);
    for (const key of Object.keys(metrics)) {
        const normalizedKey = normalizeMetricKey(key);
        const matches = normalizedAliases.some((alias) => normalizedKey === alias ||
            normalizedKey === `${alias}count` ||
            `${normalizedKey}count` === alias);
        if (matches) {
            return metrics[key] ?? null;
        }
    }
    return null;
}

},
"components/Controls.tsx": function(module, exports, require) {
"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.Section = Section;
exports.Foldout = Foldout;
exports.GroupTitle = GroupTitle;
exports.Readout = Readout;
exports.Row = Row;
exports.HudButton = HudButton;
exports.ToggleField = ToggleField;
exports.SliderField = SliderField;
exports.SelectField = SelectField;
exports.TextField = TextField;
exports.NumberField = NumberField;
const react_1 = require("react");
function cx(...parts) {
    return parts.filter(Boolean).join(" ");
}
function Section(props) {
    return (React.createElement("section", { className: "card section_sop" },
        React.createElement("header", { className: "ctm-section-header header_l0j first_l25" },
            React.createElement("div", { className: "ctm-section-header-inner header_hMN" },
                React.createElement("span", { className: "num" }, props.num),
                React.createElement("div", { className: "ctm-section-title title_mu8" }, props.title),
                props.state ? React.createElement("span", { className: "state always" }, props.state) : null)),
        props.note ? React.createElement("div", { className: cx("note", props.noteTone === "bug" && "bug") }, props.note) : null,
        React.createElement("div", { className: "items" }, props.children)));
}
function Foldout(props) {
    return (React.createElement("details", { className: "card foldout section_sop", open: props.defaultOpen },
        React.createElement("summary", { className: "ctm-section-header header_l0j first_l25" },
            React.createElement("div", { className: "ctm-section-header-inner header_hMN" },
                React.createElement("span", { className: "ctm-foldout-arrow", "aria-hidden": "true" }),
                React.createElement("span", { className: "num" }, props.num),
                React.createElement("div", { className: "ctm-section-title title_mu8" }, props.title),
                props.state ? React.createElement("span", { className: "state" }, props.state) : null)),
        props.note ? React.createElement("div", { className: cx("note", props.noteTone === "bug" && "bug") }, props.note) : null,
        React.createElement("div", { className: "items" }, props.children)));
}
function GroupTitle({ children }) {
    return React.createElement("div", { className: "group-title" }, children);
}
function Readout({ children }) {
    return (React.createElement("div", { className: "readout field_amr field_cjf" },
        React.createElement("div", { className: "label_VSW label_T__" }, children)));
}
function Row(props) {
    return React.createElement("div", { className: cx("row", props.columns === 2 && "cols-2", props.columns === 3 && "cols-3") }, props.children);
}
function HudButton(props) {
    const disabled = Boolean(props.disabled);
    return (React.createElement("button", { type: "button", className: cx("btn button_WWa", props.variant, props.centered && "is-centered", disabled && "is-disabled"), disabled: disabled, "aria-disabled": disabled, onClick: disabled ? undefined : props.onClick }, props.children));
}
function ToggleField(props) {
    const [uncontrolledChecked, setUncontrolledChecked] = (0, react_1.useState)(Boolean(props.defaultChecked));
    const isControlled = props.checked !== undefined;
    const checked = isControlled ? Boolean(props.checked) : uncontrolledChecked;
    const disabled = Boolean(props.disabled);
    return (React.createElement("label", { htmlFor: props.id, className: cx("toggle field_amr field_cjf toggle-item_uwk", checked ? "checked" : "unchecked", disabled && "is-disabled") },
        React.createElement("input", { id: props.id, className: "ctm-native-input", type: props.type || "checkbox", name: props.name, checked: checked, disabled: disabled, onChange: (event) => {
                const nextChecked = event.currentTarget.checked;
                if (!isControlled) {
                    setUncontrolledChecked(nextChecked);
                }
                props.onChange?.(nextChecked);
            } }),
        React.createElement("span", { className: "toggle-label label_VSW label_T__" }, props.children),
        React.createElement("span", { className: cx("toggle_cca toggle_ATa item-mouse-states_Fmi", checked ? "checked" : "unchecked"), "aria-hidden": "true" },
            React.createElement("span", { className: cx("checkmark_NXV", checked ? "checked" : "unchecked") }))));
}
function SliderField(props) {
    const [uncontrolledValue, setUncontrolledValue] = (0, react_1.useState)(props.defaultValue ?? props.min);
    const isControlled = props.value !== undefined;
    const value = isControlled ? Number(props.value) : uncontrolledValue;
    return (React.createElement("div", { className: cx("field field_amr field_cjf slider", props.disabled && "is-disabled") },
        React.createElement("div", { className: "field-label label_VSW label_T__" },
            props.label,
            " ",
            React.createElement("span", { className: "field-value" },
                "\u00B7 ",
                value,
                props.suffix || "")),
        React.createElement("div", { className: "field-control" },
            React.createElement("input", { type: "range", min: props.min, max: props.max, step: props.step, value: value, disabled: props.disabled, onChange: (event) => {
                    const nextValue = Number(event.currentTarget.value);
                    if (!isControlled) {
                        setUncontrolledValue(nextValue);
                    }
                    props.onChange?.(nextValue);
                } }))));
}
function SelectField(props) {
    // CoHTML does not expose the native HTMLSelectElement.options collection
    // expected by ReactDOM. A native <select>/<option> therefore crashes the
    // game UI renderer. Keep this control entirely button-based.
    const options = props.options.map((option) => typeof option === "string"
        ? { value: option, label: option }
        : option);
    const currentIndex = Math.max(0, options.findIndex((option) => option.value === props.value));
    const current = options[currentIndex];
    const disabled = options.length === 0;
    const selectOffset = (offset) => {
        if (disabled)
            return;
        const nextIndex = (currentIndex + offset + options.length) % options.length;
        props.onChange(options[nextIndex].value);
    };
    return (React.createElement("div", { className: "field field_amr field_cjf selector" },
        React.createElement("div", { className: "field-label label_VSW label_T__" }, props.label),
        React.createElement("div", { className: "field-control ctm-choice-control" },
            React.createElement("button", { type: "button", className: "ctm-choice-step button_WWa", "aria-label": `${props.label} : choix précédent`, disabled: disabled, onClick: () => selectOffset(-1) }, "\u2039"),
            React.createElement("button", { type: "button", className: "ctm-choice-value button_WWa", "aria-label": `${props.label} : choix suivant`, disabled: disabled, onClick: () => selectOffset(1) }, current ? current.label : props.value || "—"),
            React.createElement("button", { type: "button", className: "ctm-choice-step button_WWa", "aria-label": `${props.label} : choix suivant`, disabled: disabled, onClick: () => selectOffset(1) }, "\u203A"))));
}
function TextField(props) {
    return (React.createElement("div", { className: "field field_amr field_cjf" },
        React.createElement("div", { className: "field-label label_VSW label_T__" }, props.label),
        React.createElement("div", { className: "field-control" },
            React.createElement("input", { type: "text", value: props.value, placeholder: props.placeholder, readOnly: props.readOnly, onChange: (event) => props.onChange?.(event.currentTarget.value) }))));
}
function NumberField(props) {
    return (React.createElement("div", { className: "field field_amr field_cjf" },
        React.createElement("div", { className: "field-label label_VSW label_T__" }, props.label),
        React.createElement("div", { className: "field-control" },
            React.createElement("input", { type: "number", min: props.min, value: props.value, onChange: (event) => props.onChange(Number(event.currentTarget.value)) }))));
}

},
"components/RailwayPanel.tsx": function(module, exports, require) {
"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.railwayTotalCount$ = exports.railwayTunnelCount$ = exports.railwayServiceCount$ = exports.railwaySubwayCount$ = exports.railwayLightRailCount$ = exports.railwayTramCount$ = exports.railwayTrainCount$ = exports.railwayTunnelsVisible$ = exports.railwayServiceVisible$ = exports.railwaySubwayVisible$ = exports.railwayLightRailVisible$ = exports.railwayTramVisible$ = exports.railwayTrainVisible$ = exports.railwayThickness$ = exports.railwayOpacity$ = exports.railwayVisible$ = exports.railwayStatus$ = exports.railwayAvailable$ = void 0;
exports.RailwayPanel = RailwayPanel;
const api_1 = require("cs2/api");
const Controls_1 = require("./Controls");
const BINDING_GROUP = "CityTimelineMod";
exports.railwayAvailable$ = (0, api_1.bindValue)(BINDING_GROUP, "railwayAvailable", false);
exports.railwayStatus$ = (0, api_1.bindValue)(BINDING_GROUP, "railwayStatus", "Aucune donnée ferroviaire disponible dans ce bundle.");
exports.railwayVisible$ = (0, api_1.bindValue)(BINDING_GROUP, "railwayVisible", true);
exports.railwayOpacity$ = (0, api_1.bindValue)(BINDING_GROUP, "railwayOpacity", 0.9);
exports.railwayThickness$ = (0, api_1.bindValue)(BINDING_GROUP, "railwayThickness", 3);
exports.railwayTrainVisible$ = (0, api_1.bindValue)(BINDING_GROUP, "railwayTrainVisible", true);
exports.railwayTramVisible$ = (0, api_1.bindValue)(BINDING_GROUP, "railwayTramVisible", true);
exports.railwayLightRailVisible$ = (0, api_1.bindValue)(BINDING_GROUP, "railwayLightRailVisible", true);
exports.railwaySubwayVisible$ = (0, api_1.bindValue)(BINDING_GROUP, "railwaySubwayVisible", true);
exports.railwayServiceVisible$ = (0, api_1.bindValue)(BINDING_GROUP, "railwayServiceVisible", true);
exports.railwayTunnelsVisible$ = (0, api_1.bindValue)(BINDING_GROUP, "railwayTunnelsVisible", true);
exports.railwayTrainCount$ = (0, api_1.bindValue)(BINDING_GROUP, "railwayTrainCount", 0);
exports.railwayTramCount$ = (0, api_1.bindValue)(BINDING_GROUP, "railwayTramCount", 0);
exports.railwayLightRailCount$ = (0, api_1.bindValue)(BINDING_GROUP, "railwayLightRailCount", 0);
exports.railwaySubwayCount$ = (0, api_1.bindValue)(BINDING_GROUP, "railwaySubwayCount", 0);
exports.railwayServiceCount$ = (0, api_1.bindValue)(BINDING_GROUP, "railwayServiceCount", 0);
exports.railwayTunnelCount$ = (0, api_1.bindValue)(BINDING_GROUP, "railwayTunnelCount", 0);
exports.railwayTotalCount$ = (0, api_1.bindValue)(BINDING_GROUP, "railwayTotalCount", 0);
function formatCount(value) {
    const safeValue = Number.isFinite(value)
        ? Math.max(0, Math.trunc(value))
        : 0;
    return String(safeValue).replace(/\B(?=(\d{3})+(?!\d))/g, " ");
}
function clamp(value, min, max) {
    return Math.min(max, Math.max(min, value));
}
function RailwayFilter(props) {
    return (React.createElement(Controls_1.ToggleField, { id: props.id, checked: props.checked, disabled: props.disabled, onChange: (checked) => (0, api_1.trigger)(BINDING_GROUP, props.triggerName, checked) },
        React.createElement("span", { className: "railway-filter-label" },
            React.createElement("span", { className: `railway-filter-swatch ${props.swatch}`, "aria-hidden": "true" }),
            React.createElement("span", { className: "railway-filter-name" }, props.label),
            React.createElement("span", { className: "railway-filter-count" }, formatCount(props.count)))));
}
function RailwayPanel() {
    const available = (0, api_1.useValue)(exports.railwayAvailable$);
    const status = (0, api_1.useValue)(exports.railwayStatus$);
    const visible = (0, api_1.useValue)(exports.railwayVisible$);
    const opacity = (0, api_1.useValue)(exports.railwayOpacity$);
    const thickness = (0, api_1.useValue)(exports.railwayThickness$);
    const trainVisible = (0, api_1.useValue)(exports.railwayTrainVisible$);
    const tramVisible = (0, api_1.useValue)(exports.railwayTramVisible$);
    const lightRailVisible = (0, api_1.useValue)(exports.railwayLightRailVisible$);
    const subwayVisible = (0, api_1.useValue)(exports.railwaySubwayVisible$);
    const serviceVisible = (0, api_1.useValue)(exports.railwayServiceVisible$);
    const tunnelsVisible = (0, api_1.useValue)(exports.railwayTunnelsVisible$);
    const trainCount = (0, api_1.useValue)(exports.railwayTrainCount$);
    const tramCount = (0, api_1.useValue)(exports.railwayTramCount$);
    const lightRailCount = (0, api_1.useValue)(exports.railwayLightRailCount$);
    const subwayCount = (0, api_1.useValue)(exports.railwaySubwayCount$);
    const serviceCount = (0, api_1.useValue)(exports.railwayServiceCount$);
    const tunnelCount = (0, api_1.useValue)(exports.railwayTunnelCount$);
    const totalCount = (0, api_1.useValue)(exports.railwayTotalCount$);
    const opacityPercent = Math.round(clamp(opacity, 0, 1) * 100);
    const safeThickness = clamp(thickness, 0.5, 8);
    const disabled = !available;
    return (React.createElement(Controls_1.Foldout, { num: "08", title: "R\u00E9seau ferroviaire", state: available ? `${formatCount(totalCount)} voies` : "aucune donnée", note: "Calque de construction ind\u00E9pendant des routes et des points Transports.", defaultOpen: true },
        !available ? (React.createElement("div", { className: "railway-message", role: "status", "aria-live": "polite" }, status && status.trim()
            ? status
            : "Aucune donnée ferroviaire disponible dans ce bundle.")) : null,
        React.createElement("div", { className: `railway-controls ${disabled ? "is-unavailable" : ""}`, "aria-disabled": disabled },
            React.createElement(Controls_1.ToggleField, { id: "ctm-railway-visible", checked: visible, disabled: disabled, onChange: (checked) => (0, api_1.trigger)(BINDING_GROUP, "setRailwayVisible", checked) }, "Afficher le r\u00E9seau"),
            React.createElement(Controls_1.SliderField, { label: "Opacit\u00E9", min: 0, max: 100, step: 5, value: opacityPercent, suffix: "%", disabled: disabled, onChange: (value) => (0, api_1.trigger)(BINDING_GROUP, "setRailwayOpacity", value / 100) }),
            React.createElement(Controls_1.SliderField, { label: "\u00C9paisseur", min: 0.5, max: 8, step: 0.5, value: safeThickness, suffix: " m", disabled: disabled, onChange: (value) => (0, api_1.trigger)(BINDING_GROUP, "setRailwayThickness", value) }),
            React.createElement(Controls_1.GroupTitle, null, "Filtres et statistiques"),
            React.createElement("div", { className: "railway-filter-grid", role: "group", "aria-label": "Filtres du r\u00E9seau ferroviaire" },
                React.createElement(RailwayFilter, { id: "ctm-railway-train", label: "Train", swatch: "train", checked: trainVisible, count: trainCount, disabled: disabled, triggerName: "setRailwayTrainVisible" }),
                React.createElement(RailwayFilter, { id: "ctm-railway-tram", label: "Tramway", swatch: "tram", checked: tramVisible, count: tramCount, disabled: disabled, triggerName: "setRailwayTramVisible" }),
                React.createElement(RailwayFilter, { id: "ctm-railway-light-rail", label: "M\u00E9tro l\u00E9ger", swatch: "light-rail", checked: lightRailVisible, count: lightRailCount, disabled: disabled, triggerName: "setRailwayLightRailVisible" }),
                React.createElement(RailwayFilter, { id: "ctm-railway-subway", label: "M\u00E9tro", swatch: "subway", checked: subwayVisible, count: subwayCount, disabled: disabled, triggerName: "setRailwaySubwayVisible" }),
                React.createElement(RailwayFilter, { id: "ctm-railway-service", label: "Voies de service", swatch: "service", checked: serviceVisible, count: serviceCount, disabled: disabled, triggerName: "setRailwayServiceVisible" }),
                React.createElement(RailwayFilter, { id: "ctm-railway-tunnels", label: "Tunnels", swatch: "tunnel", checked: tunnelsVisible, count: tunnelCount, disabled: disabled, triggerName: "setRailwayTunnelsVisible" })),
            React.createElement("div", { className: "railway-total", "aria-label": "Total des voies ferroviaires" },
                React.createElement("span", null, "Total"),
                React.createElement("strong", null, formatCount(totalCount)))),
        React.createElement("div", { className: "railway-notice" }, "Plan visuel uniquement \u2014 aucun placement automatique.")));
}

},
"components/RoadCatalog.tsx": function(module, exports, require) {
"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.ImportRoutesPane = ImportRoutesPane;
const react_1 = require("react");
const roadPrefabs_1 = require("../data/roadPrefabs");
const Controls_1 = require("./Controls");
function sortRoads(roads, sort) {
    const next = roads.slice();
    if (sort === "name")
        return next.sort((a, b) => a.name.localeCompare(b.name));
    if (sort === "speed")
        return next.sort((a, b) => parseInt(b.speed, 10) - parseInt(a.speed, 10));
    return next.sort((a, b) => a.index - b.index);
}
function ImportRoutesPane() {
    const [source, setSource] = (0, react_1.useState)("roads");
    const [highway, setHighway] = (0, react_1.useState)("primary,secondary,tertiary,residential");
    const [onewayOnly, setOnewayOnly] = (0, react_1.useState)(false);
    const [bridgeOnly, setBridgeOnly] = (0, react_1.useState)(false);
    const [tunnelOnly, setTunnelOnly] = (0, react_1.useState)(false);
    const [lanesMin, setLanesMin] = (0, react_1.useState)(0);
    const [maxSegments, setMaxSegments] = (0, react_1.useState)(0);
    const [mode, setMode] = (0, react_1.useState)("single");
    const [family, setFamily] = (0, react_1.useState)(null);
    const [search, setSearch] = (0, react_1.useState)("");
    const [sort, setSort] = (0, react_1.useState)("default");
    const [selectedRoad, setSelectedRoad] = (0, react_1.useState)(null);
    const visibleRoads = (0, react_1.useMemo)(() => {
        const query = search.trim().toLowerCase();
        if (!family && !query)
            return [];
        const filtered = roadPrefabs_1.roadPrefabs.filter((prefab) => {
            const familyMatches = family === "all" || family === null || prefab.family === family;
            const queryMatches = !query || prefab.name.toLowerCase().includes(query);
            return familyMatches && queryMatches;
        });
        return sortRoads(filtered, sort);
    }, [family, search, sort]);
    const effectiveRule = [
        `source=${source}`,
        `highway=${highway}`,
        `onewayOnly=${onewayOnly}`,
        `bridgeOnly=${bridgeOnly}`,
        `tunnelOnly=${tunnelOnly}`,
        `lanesMin=${lanesMin}`,
        `maxSegments=${maxSegments}`,
        `mode=${mode}`,
        `targetPrefabName=${selectedRoad ? selectedRoad.name : "(aucun)"}`,
    ].join("\n");
    return (React.createElement(React.Fragment, null,
        React.createElement("div", { className: "import-layout" },
            React.createElement("aside", { className: "import-column" },
                React.createElement(Controls_1.Section, { num: "1", title: "Source GeoJSON / OSM", note: "Ces filtres d\u00E9cident quels segments du bundle sont \u00E9ligibles." },
                    React.createElement(Controls_1.SelectField, { label: "source", value: source, options: ["roads", "paths", "all"], onChange: setSource }),
                    React.createElement(Controls_1.TextField, { label: "highway", value: highway, onChange: setHighway }),
                    React.createElement(Controls_1.ToggleField, { id: "ctm-i1", defaultChecked: onewayOnly, onChange: setOnewayOnly }, "oneway=true uniquement"),
                    React.createElement(Controls_1.ToggleField, { id: "ctm-i2", defaultChecked: bridgeOnly, onChange: setBridgeOnly }, "bridge=true uniquement"),
                    React.createElement(Controls_1.ToggleField, { id: "ctm-i3", defaultChecked: tunnelOnly, onChange: setTunnelOnly }, "tunnel=true uniquement"),
                    React.createElement(Controls_1.NumberField, { label: "lanes min", value: lanesMin, min: 0, onChange: setLanesMin }),
                    React.createElement(Controls_1.NumberField, { label: "max segments", value: maxSegments, min: 0, onChange: setMaxSegments })),
                React.createElement(Controls_1.Section, { num: "2", title: "Familles RoadPrefab", note: "Filtre visuel. Ne remplace pas le PrefabName exact." },
                    React.createElement("div", { className: "cat-grid" },
                        React.createElement("button", { type: "button", className: `cat-btn button_WWa ${family === "all" ? "active" : ""}`, onClick: () => setFamily("all") },
                            "Tous ",
                            React.createElement("span", null, roadPrefabs_1.roadPrefabTotal)),
                        roadPrefabs_1.roadPrefabFamilies.map((item) => (React.createElement("button", { type: "button", key: item.id, className: `cat-btn button_WWa ${family === item.id ? "active" : ""}`, onClick: () => setFamily(item.id) },
                            item.label,
                            " ",
                            React.createElement("span", null, item.prefabs.length)))))),
                React.createElement(Controls_1.Section, { num: "3", title: "Mode import" },
                    React.createElement("label", { className: `toggle field_amr field_cjf toggle-item_uwk ${mode === "single" ? "checked" : "unchecked"}` },
                        React.createElement("input", { className: "ctm-native-input", type: "radio", name: "mode", checked: mode === "single", onChange: () => setMode("single") }),
                        React.createElement("span", { className: "toggle-label label_VSW label_T__" }, "Importer vers un seul RoadPrefab cible"),
                        React.createElement("span", { className: `toggle_cca toggle_ATa item-mouse-states_Fmi ${mode === "single" ? "checked" : "unchecked"}`, "aria-hidden": "true" },
                            React.createElement("span", { className: `checkmark_NXV ${mode === "single" ? "checked" : "unchecked"}` }))),
                    React.createElement("label", { className: `toggle field_amr field_cjf toggle-item_uwk ${mode === "auto" ? "checked" : "unchecked"}` },
                        React.createElement("input", { className: "ctm-native-input", type: "radio", name: "mode", checked: mode === "auto", onChange: () => setMode("auto") }),
                        React.createElement("span", { className: "toggle-label label_VSW label_T__" }, "Mapping automatique OSM \u2192 RoadPrefab"),
                        React.createElement("span", { className: `toggle_cca toggle_ATa item-mouse-states_Fmi ${mode === "auto" ? "checked" : "unchecked"}`, "aria-hidden": "true" },
                            React.createElement("span", { className: `checkmark_NXV ${mode === "auto" ? "checked" : "unchecked"}` }))),
                    React.createElement("label", { className: `toggle field_amr field_cjf toggle-item_uwk ${mode === "selected" ? "checked" : "unchecked"}` },
                        React.createElement("input", { className: "ctm-native-input", type: "radio", name: "mode", checked: mode === "selected", onChange: () => setMode("selected") }),
                        React.createElement("span", { className: "toggle-label label_VSW label_T__" }, "Mapping manuel par groupe s\u00E9lectionn\u00E9"),
                        React.createElement("span", { className: `toggle_cca toggle_ATa item-mouse-states_Fmi ${mode === "selected" ? "checked" : "unchecked"}`, "aria-hidden": "true" },
                            React.createElement("span", { className: `checkmark_NXV ${mode === "selected" ? "checked" : "unchecked"}` }))))),
            React.createElement("section", { className: "road-catalog" },
                React.createElement("div", { className: "road-toolbar" },
                    React.createElement(Controls_1.TextField, { label: "Rechercher", value: search, placeholder: "Small, Highway, Quay, Bridge...", onChange: setSearch }),
                    React.createElement(Controls_1.SelectField, { label: "Tri", value: sort, options: [
                            { value: "default", label: "Catégorie / vitesse / UI" },
                            { value: "name", label: "Nom" },
                            { value: "speed", label: "Vitesse" },
                        ], onChange: setSort })),
                !family && search.trim() === "" ? (React.createElement(Controls_1.Readout, null, "S\u00E9lectionner une famille RoadPrefab pour afficher ses routes. Les 92 prefabs ne sont pas list\u00E9s par d\u00E9faut.")) : null,
                React.createElement("div", { className: "road-list" },
                    visibleRoads.map((prefab) => (React.createElement(RoadCard, { key: `${prefab.index}-${prefab.name}`, prefab: prefab, selected: selectedRoad?.name === prefab.name, onSelect: () => setSelectedRoad(prefab) }))),
                    (family || search.trim() !== "") && visibleRoads.length === 0 ? React.createElement(Controls_1.Readout, null, "Aucun RoadPrefab ne correspond \u00E0 ces filtres.") : null)),
            React.createElement("aside", { className: "preview section_sop" },
                React.createElement("header", { className: "ctm-section-header header_l0j first_l25" },
                    React.createElement("div", { className: "ctm-section-header-inner header_hMN" },
                        React.createElement("span", { className: "num" }, "4"),
                        React.createElement("div", { className: "ctm-section-title title_mu8" }, "R\u00E8gle effective"))),
                React.createElement("div", { className: "preview-body" },
                    React.createElement(Controls_1.Readout, null, "Le bouton Importer lit exactement ces champs. Rien d\u2019autre."),
                    React.createElement(Controls_1.TextField, { label: "RoadPrefab cible", value: selectedRoad ? selectedRoad.name : "(aucun)", readOnly: true }),
                    React.createElement("pre", { className: "rule" }, effectiveRule),
                    React.createElement(Controls_1.HudButton, { variant: "danger", disabled: true }, "Importer uniquement cette s\u00E9lection"),
                    React.createElement(Controls_1.HudButton, { variant: "diag", disabled: true }, "Pr\u00E9visualiser segments \u00E9ligibles"),
                    React.createElement(Controls_1.HudButton, { variant: "diag", disabled: true }, "Diagnostic mapping s\u00E9lection")))),
        React.createElement(Controls_1.Foldout, { num: "03b", title: "Options runtime & diagnostics", state: "avanc\u00E9", note: "R\u00E9glages globaux du pipeline d'import et outils de diag." },
            React.createElement(Controls_1.GroupTitle, null, "\u00C9tat"),
            React.createElement(Controls_1.Readout, null, "cache OK \u00B7 15 582 segments"),
            React.createElement(Controls_1.Readout, null, "runtime ON \u00B7 runOnce false \u00B7 pipeline fast-flush"),
            React.createElement(Controls_1.GroupTitle, null, "Options"),
            React.createElement(Controls_1.Row, { columns: 2 },
                React.createElement(Controls_1.ToggleField, { id: "ctm-i7" }, "Autoriser import runtime"),
                React.createElement(Controls_1.ToggleField, { id: "ctm-i8" }, "Refuser un second import identique"),
                React.createElement(Controls_1.ToggleField, { id: "ctm-i9" }, "Afficher progression runtime HUD"),
                React.createElement(Controls_1.ToggleField, { id: "ctm-i10" }, "Logs s\u00E9lection import verbose")),
            React.createElement(SliderFieldLocal, { label: "Max segments import", defaultValue: 60000, max: 200000 }),
            React.createElement(SliderFieldLocal, { label: "Batch import runtime", defaultValue: 1000, max: 5000 }),
            React.createElement(Controls_1.GroupTitle, null, "Pipeline"),
            React.createElement(Controls_1.Row, { columns: 2 },
                React.createElement(Controls_1.HudButton, { disabled: true }, "Pipeline fast-flush"),
                React.createElement(Controls_1.HudButton, { disabled: true }, "Pipeline batch-safe")),
            React.createElement(Controls_1.GroupTitle, null, "Ex\u00E9cution globale"),
            React.createElement(Controls_1.Row, { columns: 2 },
                React.createElement(Controls_1.HudButton, { variant: "danger", disabled: true }, "Importer routes runtime (tout)"),
                React.createElement(Controls_1.HudButton, { disabled: true }, "Undo import routes runtime")),
            React.createElement(Controls_1.GroupTitle, null, "Diagnostics"),
            React.createElement(Controls_1.Row, { columns: 2 },
                React.createElement(Controls_1.HudButton, { variant: "diag", disabled: true }, "Diagnostic contenu GeoJSON routes"),
                React.createElement(Controls_1.HudButton, { variant: "diag", disabled: true }, "Diagnostic prefabs vanilla routes"),
                React.createElement(Controls_1.HudButton, { variant: "diag", disabled: true }, "Diagnostic noms exacts prefabs routes"),
                React.createElement(Controls_1.HudButton, { variant: "diag", disabled: true }, "Diagnostic mapping import routes"),
                React.createElement(Controls_1.HudButton, { variant: "diag", disabled: true }, "Diagnostic routes import\u00E9es"),
                React.createElement(Controls_1.HudButton, { variant: "diag", disabled: true }, "Diagnostic routes vanilla plac\u00E9es"))),
        React.createElement(Controls_1.Foldout, { num: "03c", title: "R\u00E9sum\u00E9 92 prefabs", state: "stats" },
            React.createElement(Controls_1.Section, { num: "\u2211", title: "R\u00E9sum\u00E9 class\u00E9" },
                React.createElement("table", null,
                    React.createElement("tbody", null, roadPrefabs_1.roadPrefabFamilies.map((item) => (React.createElement("tr", { key: item.id },
                        React.createElement("td", null, item.label),
                        React.createElement("td", { className: "count-cell" }, item.prefabs.length))))))))));
}
function SliderFieldLocal(props) {
    const [value, setValue] = (0, react_1.useState)(props.defaultValue);
    return (React.createElement("div", { className: "field field_amr field_cjf slider" },
        React.createElement("div", { className: "field-label label_VSW label_T__" },
            props.label,
            " ",
            React.createElement("span", { className: "field-value" },
                "\u00B7 ",
                value)),
        React.createElement("div", { className: "field-control" },
            React.createElement("input", { type: "range", min: 0, max: props.max, value: value, onChange: (event) => setValue(Number(event.currentTarget.value)) }))));
}
function RoadCard(props) {
    const traits = (0, roadPrefabs_1.getRoadTraits)(props.prefab);
    return (React.createElement("article", { className: `road-card ${props.selected ? "selected" : ""}` },
        React.createElement("label", { className: "select-line field_cjf" },
            React.createElement("input", { className: "ctm-native-input", type: "radio", name: "roadPrefab", checked: props.selected, onChange: props.onSelect }),
            React.createElement("span", { className: "editor-family" }, props.prefab.family),
            React.createElement("span", { className: `road-choice toggle_cca toggle_ATa item-mouse-states_Fmi ${props.selected ? "checked" : "unchecked"}`, "aria-hidden": "true" },
                React.createElement("span", { className: `checkmark_NXV ${props.selected ? "checked" : "unchecked"}` }))),
        React.createElement("h3", null, props.prefab.name),
        React.createElement("div", { className: "meta-grid" },
            React.createElement("div", null,
                React.createElement("b", null, "Direction"),
                React.createElement("span", null, (0, roadPrefabs_1.getRoadDirection)(props.prefab.name))),
            React.createElement("div", null,
                React.createElement("b", null, "Voies nom"),
                React.createElement("span", null, (0, roadPrefabs_1.getRoadLaneLabel)(props.prefab.name))),
            React.createElement("div", null,
                React.createElement("b", null, "Vitesse"),
                React.createElement("span", null, props.prefab.speed)),
            React.createElement("div", null,
                React.createElement("b", null, "Priorit\u00E9 UI"),
                React.createElement("span", null, props.prefab.index))),
        React.createElement("div", { className: "flags" }, (0, roadPrefabs_1.getRoadFlags)(props.prefab)),
        React.createElement("div", { className: "tags" }, traits.map((trait) => (React.createElement("span", { className: "tag", key: trait }, trait)))),
        React.createElement("details", null,
            React.createElement("summary", null, "Informations compl\u00E9mentaires"),
            React.createElement("div", { className: "sections" },
                "Nom interne exact : ",
                props.prefab.name,
                " \u00B7 famille : ",
                props.prefab.family,
                " \u00B7 vitesse : ",
                props.prefab.speed))));
}

},
"components/ServicesPanel.tsx": function(module, exports, require) {
"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.ServicesPanel = ServicesPanel;
const api_1 = require("cs2/api");
const Controls_1 = require("./Controls");
const bundleStats_1 = require("./bundleStats");
const BINDING_GROUP = "CityTimelineMod";
function emitServiceSetting(triggerName, key, value) {
    (0, api_1.trigger)(BINDING_GROUP, triggerName, JSON.stringify({ key, value }));
}
function familyClassName(key) {
    return key.toLowerCase().replace(/[^a-z0-9_-]/g, "-");
}
function ServicesPanel({ services, status }) {
    const availableFamilies = services.filter((family) => family.available).length;
    const hasKnownTotal = services.some((family) => family.total !== null);
    const totalPoints = hasKnownTotal
        ? services.reduce((total, family) => total + (family.total ?? 0), 0)
        : null;
    return (React.createElement(Controls_1.Foldout, { num: "09", title: "Services", state: `${availableFamilies}/${services.length} familles · ${(0, bundleStats_1.formatCount)(totalPoints)} points`, note: "Points OSM projet\u00E9s sur le terrain, sans placement ni modification du r\u00E9seau du jeu.", defaultOpen: true }, services.length === 0 ? (React.createElement("div", { className: "ctm-services-message", role: "status", "aria-live": "polite" }, status || "Aucune donnée de services disponible dans ce bundle.")) : (React.createElement("div", { className: "ctm-service-family-grid" }, services.map((family) => {
        const disabled = !family.available;
        const opacityPercent = Math.round(family.opacity * 100);
        return (React.createElement("section", { className: `ctm-service-family service-${familyClassName(family.key)}`, key: family.key },
            React.createElement("header", { className: "ctm-service-family-heading" },
                React.createElement("span", { className: "ctm-service-swatch", "aria-hidden": "true" }),
                React.createElement("span", { className: "ctm-service-family-title" }, family.label),
                React.createElement("span", { className: `ctm-service-family-state ${family.available ? "is-connected" : ""}` }, family.available ? "connecté" : "indisponible"),
                React.createElement("strong", null, (0, bundleStats_1.formatCount)(family.total))),
            React.createElement(Controls_1.ToggleField, { id: `ctm-service-${familyClassName(family.key)}-visible`, checked: family.visible, disabled: disabled, onChange: (value) => emitServiceSetting("setServiceVisible", family.key, value) }, "Afficher"),
            React.createElement(Controls_1.SliderField, { label: "Opacit\u00E9", min: 0, max: 100, step: 5, value: opacityPercent, suffix: "%", disabled: disabled || !family.visible, onChange: (value) => emitServiceSetting("setServiceOpacity", family.key, value / 100) }),
            React.createElement("table", { className: "ctm-stats-table ctm-stats-table-compact" },
                React.createElement("tbody", null, family.subcategories.map((subcategory) => (React.createElement("tr", { key: `${family.key}.${subcategory.key}` },
                    React.createElement("th", { scope: "row" }, subcategory.label),
                    React.createElement("td", { className: "count-cell" }, (0, bundleStats_1.formatCount)(subcategory.count)))))))));
    })))));
}

},
"components/StatisticsPanel.tsx": function(module, exports, require) {
"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.StatisticsPanel = StatisticsPanel;
const Controls_1 = require("./Controls");
const bundleStats_1 = require("./bundleStats");
const ZONING_METRICS = [
    { label: "Résidentiel haute densité", aliases: ["residentialHigh", "residential_high"] },
    { label: "Résidentiel moyenne densité", aliases: ["residentialMedium", "residential_medium"] },
    { label: "Résidentiel basse densité", aliases: ["residentialLow", "residential_low"] },
    { label: "Commercial haute densité", aliases: ["commercialHigh", "commercial_high"] },
    { label: "Commercial basse densité", aliases: ["commercialLow", "commercial_low"] },
    { label: "Commerce de détail", aliases: ["retail"] },
    { label: "Industrie", aliases: ["industrial", "industry"] },
    { label: "Parking en ouvrage", aliases: ["parkingStructure", "parking_structure", "structuredParking", "parkingRamp"] },
    { label: "Parking de surface", aliases: ["parkingSurface", "parking_surface", "surfaceParking"] },
    { label: "Bureaux", aliases: ["office", "offices"] },
    { label: "Usage mixte", aliases: ["mixed", "mixedUse", "mixed_use"] },
];
const ROAD_METRICS = [
    { label: "Autoroute", aliases: ["highway", "motorway"] },
    { label: "Axe principal", aliases: ["main", "mainRoad", "largeRoad", "large_road", "primary"] },
    { label: "Route secondaire", aliases: ["secondary", "secondaryRoad", "mediumRoad", "medium_road"] },
    { label: "Route tertiaire / résidentielle", aliases: ["tertiary", "tertiaryResidential", "tertiary_residential", "smallRoad", "small_road"] },
    { label: "Bretelle / liaison", aliases: ["ramp", "link", "rampLink", "ramp_link"] },
    { label: "Route non classée", aliases: ["unclassified", "unclassifiedRoad", "gravelRoad", "gravel_road", "other"] },
    { label: "Chemin / piéton", aliases: ["paths", "pathways", "pathway"] },
    { label: "Total routes", aliases: ["total", "roads", "roadTotal", "road_total"] },
];
const WATER_METRICS = [
    { label: "Eau — lignes", aliases: ["lines", "lineCount", "line_count"] },
    { label: "Eau — surfaces", aliases: ["areas", "areaCount", "area_count"] },
];
const RAILWAY_METRICS = [
    { label: "Train", aliases: ["train"] },
    { label: "Tramway", aliases: ["tram", "tramway"] },
    { label: "Métro léger", aliases: ["lightRail", "light_rail"] },
    { label: "Métro", aliases: ["subway", "metro"] },
    { label: "Voies de service", aliases: ["service", "serviceTracks", "service_tracks"] },
    { label: "Tunnels", aliases: ["tunnels", "tunnel"] },
    { label: "Total", aliases: ["total"] },
];
function renderMetricRows(metrics, definitions) {
    return definitions.map((definition) => (React.createElement("tr", { key: definition.label },
        React.createElement("th", { scope: "row" }, definition.label),
        React.createElement("td", { className: "count-cell" }, (0, bundleStats_1.formatCount)((0, bundleStats_1.getMetric)(metrics, ...definition.aliases))))));
}
function StatisticsPanel({ stats }) {
    const bundleLabel = stats.bundleName || stats.bundleId || "Bundle actif";
    const state = stats.available ? "connecté" : "indisponible";
    return (React.createElement("div", { className: "ctm-pane ctm-statistics-pane" },
        React.createElement(Controls_1.Section, { num: "S0", title: "Indicateurs du bundle", state: state, note: stats.status },
            React.createElement("div", { className: "ctm-statistics-summary" },
                React.createElement(Controls_1.Readout, null, bundleLabel),
                React.createElement(Controls_1.Readout, null,
                    "Objets OSM exploitables \u00B7 ",
                    React.createElement("strong", null, (0, bundleStats_1.formatCount)(stats.objects))))),
        React.createElement(Controls_1.Foldout, { num: "S1", title: "Zoning et stationnement", state: state, defaultOpen: true },
            React.createElement("table", { className: "ctm-stats-table" },
                React.createElement("tbody", null, renderMetricRows(stats.zoning, ZONING_METRICS)))),
        React.createElement(Controls_1.Foldout, { num: "S2", title: "Routes, chemins et hydrographie", state: state, defaultOpen: true },
            React.createElement("table", { className: "ctm-stats-table" },
                React.createElement("tbody", null,
                    renderMetricRows(stats.roads, ROAD_METRICS),
                    renderMetricRows(stats.water, WATER_METRICS)))),
        React.createElement(Controls_1.Foldout, { num: "S3", title: "Services", state: `${stats.services.length} familles`, defaultOpen: true }, stats.services.length === 0 ? (React.createElement(Controls_1.Readout, null, "Aucune donn\u00E9e de services disponible dans ce bundle.")) : (React.createElement("div", { className: "ctm-service-stats-list" }, stats.services.map((family) => (React.createElement("section", { className: "ctm-service-stat-family", key: family.key },
            React.createElement("header", { className: "ctm-service-family-heading" },
                React.createElement("span", null, family.label),
                React.createElement("span", { className: "ctm-service-family-state" }, family.available ? "connecté" : "indisponible"),
                React.createElement("strong", null, (0, bundleStats_1.formatCount)(family.total))),
            React.createElement("table", { className: "ctm-stats-table ctm-stats-table-compact" },
                React.createElement("tbody", null, family.subcategories.map((subcategory) => (React.createElement("tr", { key: `${family.key}.${subcategory.key}` },
                    React.createElement("th", { scope: "row" }, subcategory.label),
                    React.createElement("td", { className: "count-cell" }, (0, bundleStats_1.formatCount)(subcategory.count))))))))))))),
        React.createElement(Controls_1.Foldout, { num: "S4", title: "R\u00E9seau ferroviaire", state: (0, bundleStats_1.formatCount)((0, bundleStats_1.getMetric)(stats.railway, "total")) },
            React.createElement("table", { className: "ctm-stats-table" },
                React.createElement("tbody", null, renderMetricRows(stats.railway, RAILWAY_METRICS))))));
}

},
"components/ZoningPanel.tsx": function(module, exports, require) {
"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.ZoningPanel = ZoningPanel;
const react_1 = require("react");
const zoningPrefabs_1 = require("../data/zoningPrefabs");
const Controls_1 = require("./Controls");
function ZoningPanel() {
    const [familyId, setFamilyId] = (0, react_1.useState)(zoningPrefabs_1.zoningFamilies[0].id);
    const family = (0, react_1.useMemo)(() => zoningPrefabs_1.zoningFamilies.find((item) => item.id === familyId) || zoningPrefabs_1.zoningFamilies[0], [familyId]);
    return (React.createElement("div", { className: "zoning-panel" },
        React.createElement(Controls_1.SelectField, { label: "Famille zoning", value: familyId, options: zoningPrefabs_1.zoningFamilies.map((item) => ({ value: item.id, label: item.label })), onChange: (value) => setFamilyId(value) }),
        React.createElement("div", { className: "zoning-density-list" }, family.densities.map((density) => (React.createElement("div", { className: "zoning-density", key: density.label },
            React.createElement("div", { className: "group-title" },
                family.label,
                " \u00B7 ",
                density.label),
            density.prefabs.map((prefab) => (React.createElement(Controls_1.Readout, { key: prefab }, prefab)))))))));
}

},
"data/roadPrefabs.ts": function(module, exports, require) {
"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.roadPrefabTotal = exports.roadPrefabs = exports.roadPrefabFamilies = void 0;
exports.getRoadDirection = getRoadDirection;
exports.getRoadLaneLabel = getRoadLaneLabel;
exports.getRoadTraits = getRoadTraits;
exports.getRoadFlags = getRoadFlags;
const names = (family, speed, values) => values.map((name) => ({
    index: -1,
    family,
    name,
    speed,
}));
const withIndex = (prefabs) => prefabs.map((prefab, index) => ({ ...prefab, index }));
const familiesWithoutIndex = [
    {
        id: "Small roads",
        label: "Small roads",
        summary: "Rues locales, stationnement et variantes asymetriques.",
        prefabs: names("Small roads", "80 km/h", [
            "Small Road",
            "Small Road Asymmetric",
            "Small Road Oneway - 1 lane",
            "Small Road Oneway",
            "Small Road Oneway - 3 lanes",
            "Small Road - Double Sided Parking",
            "Small Road - Double Sided Parking Angled",
            "Small Road Divided - Double Sided Parking",
            "Small Road Divided - Double Sided Parking Angled",
            "Small Road Oneway - Single Sided Parking",
            "Small Road Oneway - Single Sided Parking Angled",
        ]),
    },
    {
        id: "Medium roads",
        label: "Medium roads",
        summary: "Axes urbains moyens, sens uniques et stationnement.",
        prefabs: names("Medium roads", "100 km/h", [
            "Medium Road",
            "Medium Road Divided",
            "Medium Road Asymmetric - 1+3 lanes",
            "Medium Road Asymmetric",
            "Medium Road Oneway",
            "Medium Road Oneway - 5 lanes",
            "Medium Road - Double Sided Parking",
            "Medium Road - Double Sided Parking Angled",
        ]),
    },
    {
        id: "Large roads",
        label: "Large roads",
        summary: "Grands axes, variantes divisees et XL.",
        prefabs: [
            ...names("Large roads", "100 km/h", [
                "Large Road",
                "Large Road Asymmetric - 2+4 lanes",
                "Large Road Asymmetric - 2+5 lanes",
                "Large Road Asymmetric",
                "Large Road Oneway",
                "Large Road Oneway - 7 lanes",
            ]),
            ...names("Large roads", "120 km/h", ["Large Road Divided", "XL Road Divided"]),
        ],
    },
    {
        id: "Highway",
        label: "Highways",
        summary: "Autoroutes une voie a cinq voies et variantes bidirectionnelles.",
        prefabs: [
            ...names("Highway", "160 km/h", ["Highway Oneway - 1 lane"]),
            ...names("Highway", "200 km/h", [
                "Highway Twoway - 2 lanes",
                "Highway Twoway - 3 lanes",
                "Highway Twoway - 4 lanes",
                "Highway Twoway Asymmetric - 1+3 lanes",
                "Highway Twoway Asymmetric - 2+3 lanes",
            ]),
            ...names("Highway", "240 km/h", [
                "Highway Oneway - 2 lanes",
                "Highway Oneway - 3 lanes",
                "Highway Oneway - 4 lanes",
                "Highway Oneway - 5 lanes",
            ]),
        ],
    },
    {
        id: "Alley",
        label: "Alleys",
        summary: "Ruelles et variantes avec stationnement.",
        prefabs: names("Alley", "60 km/h", [
            "Alley",
            "Alley Oneway",
            "Alley - Double Sided Parking",
            "Alley - Double Sided Parking Angled",
            "Alley Oneway - Double Sided Parking",
            "Alley Oneway - Double Sided Parking Angled",
        ]),
    },
    {
        id: "Pedestrian",
        label: "Pedestrian streets",
        summary: "Rues pietonnes principales et petites.",
        prefabs: names("Pedestrian", "40 km/h", ["Pedestrian Street", "Pedestrian Street Small"]),
    },
    {
        id: "Public transport lane",
        label: "Public transport lanes",
        summary: "Voies reservees aux transports publics.",
        prefabs: names("Public transport lane", "100 km/h", [
            "Double Public Transport Lane",
            "Oneway Public Transport Lane",
        ]),
    },
    {
        id: "Quay / Harbor",
        label: "Quays / Harbor roads",
        summary: "Quais, routes portuaires et acces de portes.",
        prefabs: [
            ...names("Quay / Harbor", "80 km/h", [
                "QuayMedium01",
                "QuayMedium02",
                "QuayMedium03",
                "HarborRoad01",
                "HarborRoad02",
                "HarborGateRoadOneway01",
                "HarborGateRoad01",
                "HarborGateRoadOneway02",
                "HarborGateRoad02",
                "HarborGateRoadOneway03",
                "HarborQuay01",
            ]),
            ...names("Quay / Harbor", "100 km/h", ["QuayLarge01", "QuayLarge02", "QuayLarge03"]),
        ],
    },
    {
        id: "Bridge",
        label: "Bridges",
        summary: "Ponts decoratifs, routiers, autoroutiers et franchissements.",
        prefabs: [
            ...names("Bridge", "60 km/h", ["Wooden Covered Bridge - 2 lanes"]),
            ...names("Bridge", "80 km/h", [
                "ExtradosedBridge02 Above Road",
                "Tied Arch Bridge - 4 lanes",
                "SuspensionBridge01",
                "Truss Arch Bridge - Small Road - 2 Lanes",
                "SuspensionBridge03",
                "SuspensionBridge04",
                "DrawBridge01",
            ]),
            ...names("Bridge", "100 km/h", [
                "TrussArchBridge02",
                "ExtradosedBridge03",
                "ExtradosedBridge04",
                "ExtradosedBridge02",
                "DrawBridge02",
                "DrawBridge03",
            ]),
            ...names("Bridge", "120 km/h", [
                "Extradosed Bridge - Large Road Divided - 6 Lanes",
                "Cable-stayed Bridge - XL Road Divided - 8 Lanes",
            ]),
            ...names("Bridge", "160 km/h", [
                "LiftBridge03",
                "Grand Bridge",
                "Golden Gate Bridge",
                "Golden Gate Road",
            ]),
            ...names("Bridge", "200 km/h", [
                "SuspensionBridge02 Above Road",
                "Truss Arch Bridge - Highway Twoway - 2 Lanes",
                "Suspension Bridge - Highway Oneway - 2 Lanes",
                "Suspension Bridge - Highway Oneway - 3 lanes",
                "Suspension Bridge - Highway Oneway - 4 Lanes",
                "Suspension Bridge - Highway Oneway - 5 Lanes",
                "SuspensionBridge02",
                "ExtradosedBridge01",
            ]),
        ],
    },
    {
        id: "Other street",
        label: "Other roads",
        summary: "Routes gravier.",
        prefabs: names("Other street", "60 km/h", ["Gravel Road", "Gravel Road Oneway"]),
    },
    {
        id: "Special / Dam",
        label: "Special",
        summary: "Route technique du barrage hydroelectrique.",
        prefabs: names("Special / Dam", "80 km/h", ["Hydroelectric_Power_Plant_01 Dam"]),
    },
];
const indexedPrefabs = withIndex(familiesWithoutIndex.flatMap((family) => family.prefabs));
exports.roadPrefabFamilies = familiesWithoutIndex.map((family) => ({
    ...family,
    prefabs: indexedPrefabs.filter((prefab) => prefab.family === family.id),
}));
exports.roadPrefabs = indexedPrefabs;
exports.roadPrefabTotal = exports.roadPrefabs.length;
function getRoadDirection(prefabName) {
    if (prefabName.includes("Oneway"))
        return "one-way";
    if (prefabName.includes("Twoway"))
        return "two-way";
    return "two-way/unknown";
}
function getRoadLaneLabel(prefabName) {
    const laneMatch = prefabName.match(/(\d+)[ +\\-]?(?:Lanes|lanes|lane)/);
    if (laneMatch)
        return `${laneMatch[1]} voie(s)`;
    const asymmetricMatch = prefabName.match(/(\d\+\d)/);
    if (asymmetricMatch)
        return asymmetricMatch[1];
    return "—";
}
function getRoadTraits(prefab) {
    const traits = [];
    if (prefab.family !== "Highway" && prefab.family !== "Bridge")
        traits.push("zoning");
    if (prefab.name.includes("Parking"))
        traits.push("parking");
    if (prefab.name.includes("Angled"))
        traits.push("stationnement angle");
    if (prefab.name.includes("Bridge") || prefab.family === "Bridge")
        traits.push("bridge");
    if (prefab.name.includes("Quay") || prefab.name.includes("Harbor"))
        traits.push("waterfront");
    if (prefab.name.includes("Public Transport"))
        traits.push("transport public");
    if (prefab.name.includes("Pedestrian"))
        traits.push("pieton");
    if (prefab.name.includes("Highway"))
        traits.push("highway");
    return traits.length > 0 ? traits : ["standard"];
}
function getRoadFlags(prefab) {
    const flags = [];
    if (prefab.family !== "Highway" && prefab.family !== "Bridge")
        flags.push("EnableZoning");
    if (prefab.name.includes("Asymmetric"))
        flags.push("PreferTrafficLights");
    if (prefab.name.includes("Oneway"))
        flags.push("OneWay");
    if (prefab.family === "Bridge")
        flags.push("Bridge");
    return flags.length > 0 ? flags.join(", ") : "—";
}

},
"data/zoningPrefabs.ts": function(module, exports, require) {
"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.zoningFamilies = void 0;
exports.zoningFamilies = [
    {
        id: "residentiel",
        label: "Résidentiel",
        densities: [
            {
                density: "basse",
                label: "Basse densité",
                prefabs: ["Residential Low", "Residential Low Waterfront", "Row Housing Low"],
            },
            {
                density: "moyenne",
                label: "Moyenne densité",
                prefabs: ["Residential Medium", "Residential Mixed Medium", "Row Housing Medium"],
            },
            {
                density: "haute",
                label: "Haute densité",
                prefabs: ["Residential High", "Residential High Waterfront", "Residential High Mixed"],
            },
        ],
    },
    {
        id: "commercial",
        label: "Commercial",
        densities: [
            {
                density: "basse",
                label: "Basse densité",
                prefabs: ["Commercial Low", "Commercial Low Waterfront"],
            },
            {
                density: "moyenne",
                label: "Moyenne densité",
                prefabs: ["Commercial Medium", "Commercial Mixed Medium"],
            },
            {
                density: "haute",
                label: "Haute densité",
                prefabs: ["Commercial High", "Commercial High Mixed"],
            },
        ],
    },
    {
        id: "industriel",
        label: "Industriel",
        densities: [
            {
                density: "basse",
                label: "Production",
                prefabs: ["Industrial Manufacturing", "Industrial Warehouse", "Industrial Specialized"],
            },
            {
                density: "moyenne",
                label: "Logistique",
                prefabs: ["Industrial Logistics", "Industrial Processing"],
            },
        ],
    },
    {
        id: "bureaux",
        label: "Bureaux",
        densities: [
            {
                density: "moyenne",
                label: "Moyenne densité",
                prefabs: ["Office Medium", "Office Mixed Medium"],
            },
            {
                density: "haute",
                label: "Haute densité",
                prefabs: ["Office High", "Office Tower", "Office High Mixed"],
            },
        ],
    },
];

},
"index.tsx": function(module, exports, require) {
"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
const CityTimelineHUD_1 = require("./CityTimelineHUD");
require("./citytimeline-hud.css");
const register = (moduleRegistry) => {
    console.info("[CityTimelineMod UI] registering CoHTML UI");
    // Trigger only.
    moduleRegistry.append("GameTopLeft", CityTimelineHUD_1.CityTimelineHUDButton);
    // Find It architecture: render the panel directly in the gameplay root.
    moduleRegistry.append("Game", CityTimelineHUD_1.CityTimelineHUDHost);
};
exports.default = register;

}};
var __ctmCache = Object.create(null);
var __ctmExternals = {
  "react": window.React,
  "cs2/modding": window["cs2/modding"],
  "cs2/api": window["cs2/api"],
  "cs2/ui": window["cs2/ui"]
};
function __ctmNormalize(path) {
  var output = [];
  path.split("/").forEach(function(part) {
    if (!part || part === ".") return;
    if (part === "..") output.pop(); else output.push(part);
  });
  return output.join("/");
}
function __ctmResolve(request, from) {
  if (request === "./citytimeline-hud.css" || request.endsWith(".css")) return "@css";
  if (request.charAt(0) !== ".") return "@external:" + request;
  var slash = from.lastIndexOf("/");
  var base = (slash >= 0 ? from.slice(0, slash + 1) : "") + request;
  base = __ctmNormalize(base);
  var candidates = [base, base + ".tsx", base + ".ts", base + "/index.tsx", base + "/index.ts"];
  for (var i = 0; i < candidates.length; i++) if (__ctmModules[candidates[i]]) return candidates[i];
  throw new Error("CityTimelineMod UI module introuvable: " + request + " depuis " + from);
}
function __ctmLoad(id, from) {
  var resolved = from ? __ctmResolve(id, from) : id;
  if (resolved === "@css") return {};
  if (resolved.indexOf("@external:") === 0) {
    var externalName = resolved.slice(10);
    if (__ctmExternals[externalName]) return __ctmExternals[externalName];
    throw new Error("CityTimelineMod UI externe introuvable: " + externalName);
  }
  if (__ctmCache[resolved]) return __ctmCache[resolved].exports;
  var factory = __ctmModules[resolved];
  if (!factory) throw new Error("CityTimelineMod UI module non compilé: " + resolved);
  var module = { exports: {} };
  __ctmCache[resolved] = module;
  factory(module, module.exports, function(request) { return __ctmLoad(request, resolved); });
  return module.exports;
}
var __ctmEntry = __ctmLoad("index.tsx");
var __ctmRegister = __ctmEntry.default;
export { __ctmRegister as default };
export const hasCSS = true;
