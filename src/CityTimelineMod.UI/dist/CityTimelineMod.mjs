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
const RailwayPanel_1 = require("./components/RailwayPanel");
const ServicesPanel_1 = require("./components/ServicesPanel");
const StatisticsPanel_1 = require("./components/StatisticsPanel");
const OverlayLayersPanel_1 = require("./components/OverlayLayersPanel");
const bundleStats_1 = require("./components/bundleStats");
const tabs = [
    {
        id: "p1",
        label: "Principal",
        count: "01·02",
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
    if (!visible) {
        return null;
    }
    // Find It renders its main container directly from moduleRegistry.append("Game", ...).
    // No Portal: the gameplay root owns the component lifecycle and layout.
    return (React.createElement(CityTimelineHudErrorBoundary, null,
        React.createElement(CityTimelineHUD, { onClose: () => (0, api_1.trigger)("CityTimelineMod", "closeCohtmlHud") })));
}
function CityTimelineHUD({ onClose, }) {
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
                "entit\u00E9s visuelles ",
                (0, bundleStats_1.formatCount)(stats.visualEntities),
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
                (0, bundleStats_1.formatCount)(stats.visualEntities),
                " entit\u00E9s visuelles du bundle")),
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
        React.createElement(OverlayLayersPanel_1.OverlayLayersPanel, { stats: stats }),
        React.createElement(RailwayPanel_1.RailwayPanel, null),
        React.createElement(ServicesPanel_1.ServicesPanel, { services: stats.services }),
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
    visualEntities: null,
    uniqueOsmElements: null,
    zoning: {},
    roads: {},
    water: {},
    services: [],
    railway: {},
    coverage: {
        complete: false,
        presentFiles: 0,
        expectedFiles: 0,
        missingFiles: [],
    },
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
function asCoverage(value) {
    if (!isRecord(value))
        return EMPTY_STATS.coverage;
    const presentFiles = asCount(value.presentFiles) ?? 0;
    const expectedFiles = asCount(value.expectedFiles) ?? 0;
    const missingFiles = Array.isArray(value.missingFiles)
        ? value.missingFiles.filter((item) => typeof item === "string")
        : [];
    return {
        complete: value.complete === true,
        presentFiles,
        expectedFiles,
        missingFiles,
    };
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
            visualEntities: asCount(value.visualEntities),
            uniqueOsmElements: asCount(value.uniqueOsmElements),
            zoning: asMetrics(value.zoning),
            roads: asMetrics(value.roads),
            water: asMetrics(value.water),
            services: asServices(value.services),
            railway: asMetrics(value.railway),
            coverage: asCoverage(value.coverage),
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
    const [open, setOpen] = (0, react_1.useState)(Boolean(props.defaultOpen));
    return (React.createElement("details", { className: "card foldout section_sop", open: open, onToggle: (event) => setOpen(event.currentTarget.open) },
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
"components/OverlayLayersPanel.tsx": function(module, exports, require) {
"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.OverlayLayersPanel = OverlayLayersPanel;
const api_1 = require("cs2/api");
const Controls_1 = require("./Controls");
const bundleStats_1 = require("./bundleStats");
const BINDING_GROUP = "CityTimelineMod";
const overlayLayersAvailable$ = (0, api_1.bindValue)(BINDING_GROUP, "overlayLayersAvailable", false);
const overlayZoningVisible$ = (0, api_1.bindValue)(BINDING_GROUP, "overlayZoningVisible", true);
const overlayRoadsVisible$ = (0, api_1.bindValue)(BINDING_GROUP, "overlayRoadsVisible", true);
const overlayPathsVisible$ = (0, api_1.bindValue)(BINDING_GROUP, "overlayPathsVisible", true);
const overlayWaterVisible$ = (0, api_1.bindValue)(BINDING_GROUP, "overlayWaterVisible", true);
const overlayBoundsVisible$ = (0, api_1.bindValue)(BINDING_GROUP, "overlayBoundsVisible", true);
const overlayZoningOpacity$ = (0, api_1.bindValue)(BINDING_GROUP, "overlayZoningOpacity", 1);
const overlayRoadsOpacity$ = (0, api_1.bindValue)(BINDING_GROUP, "overlayRoadsOpacity", 1);
const overlayPathsOpacity$ = (0, api_1.bindValue)(BINDING_GROUP, "overlayPathsOpacity", 1);
const overlayWaterOpacity$ = (0, api_1.bindValue)(BINDING_GROUP, "overlayWaterOpacity", 1);
const overlayBoundsOpacity$ = (0, api_1.bindValue)(BINDING_GROUP, "overlayBoundsOpacity", 1);
const overlaySublayersJson$ = (0, api_1.bindValue)(BINDING_GROUP, "overlaySublayersJson", "{}");
const ZONING_SUBLAYERS = [
    { key: "zoning.residential_high", label: "Résidentiel haute densité", metrics: "zoning", aliases: ["residentialHigh", "residential_high"] },
    { key: "zoning.residential_medium", label: "Résidentiel moyenne densité", metrics: "zoning", aliases: ["residentialMedium", "residential_medium"] },
    { key: "zoning.residential_low", label: "Résidentiel basse densité", metrics: "zoning", aliases: ["residentialLow", "residential_low"] },
    { key: "zoning.commercial_high", label: "Commercial haute densité", metrics: "zoning", aliases: ["commercialHigh", "commercial_high"] },
    { key: "zoning.commercial_low", label: "Commercial basse densité", metrics: "zoning", aliases: ["commercialLow", "commercial_low"] },
    { key: "zoning.retail", label: "Commerce de détail", metrics: "zoning", aliases: ["retail"] },
    { key: "zoning.industrial", label: "Industrie", metrics: "zoning", aliases: ["industrial", "industry"] },
    { key: "zoning.parking_structure", label: "Parking en ouvrage", metrics: "zoning", aliases: ["parkingStructure", "parking_structure"] },
    { key: "zoning.parking_surface", label: "Parking de surface", metrics: "zoning", aliases: ["parkingSurface", "parking_surface"] },
    { key: "zoning.office", label: "Bureaux", metrics: "zoning", aliases: ["office", "offices"] },
    { key: "zoning.mixed", label: "Usage mixte", metrics: "zoning", aliases: ["mixed", "mixedUse", "mixed_use"] },
];
const ROAD_SUBLAYERS = [
    { key: "roads.highway", label: "Autoroute", metrics: "roads", aliases: ["highway", "motorway"] },
    { key: "roads.large_road", label: "Axe principal", metrics: "roads", aliases: ["main", "largeRoad", "large_road"] },
    { key: "roads.medium_road", label: "Route secondaire", metrics: "roads", aliases: ["secondary", "mediumRoad", "medium_road"] },
    { key: "roads.small_road", label: "Route tertiaire / résidentielle", metrics: "roads", aliases: ["tertiary", "smallRoad", "small_road"] },
    { key: "roads.ramp", label: "Bretelle / liaison", metrics: "roads", aliases: ["ramp", "link"] },
    { key: "roads.gravel_road", label: "Route non classée", metrics: "roads", aliases: ["unclassified", "gravelRoad", "gravel_road"] },
    { key: "roads.pathway", label: "Chemin / piéton", metrics: "roads", aliases: ["paths", "pathways", "pathway"] },
];
const WATER_SUBLAYERS = [
    { key: "water.lines", label: "Eau — lignes", metrics: "water", aliases: ["lines", "lineCount", "line_count"] },
    { key: "water.areas", label: "Eau — surfaces", metrics: "water", aliases: ["areas", "areaCount", "area_count"] },
];
const ALL_SUBLAYERS = [...ZONING_SUBLAYERS, ...ROAD_SUBLAYERS, ...WATER_SUBLAYERS];
function clamp01(value) {
    return Math.min(1, Math.max(0, Number.isFinite(value) ? value : 1));
}
function parseSublayerVisibility(json) {
    try {
        const parsed = JSON.parse(json || "{}");
        if (typeof parsed !== "object" || parsed === null || Array.isArray(parsed))
            return {};
        const layers = parsed.layers;
        if (typeof layers !== "object" || layers === null || Array.isArray(layers))
            return {};
        const result = {};
        Object.entries(layers).forEach(([key, value]) => {
            if (typeof value === "boolean")
                result[key] = value;
        });
        return result;
    }
    catch {
        return {};
    }
}
function layerMetrics(stats, kind) {
    return stats[kind];
}
function SublayerGroup(props) {
    return (React.createElement("section", { className: "ctm-overlay-sublayer-group" },
        React.createElement(Controls_1.GroupTitle, null, props.title),
        React.createElement("div", { className: "ctm-overlay-sublayer-grid" }, props.definitions.map((definition) => {
            const count = (0, bundleStats_1.getMetric)(layerMetrics(props.stats, definition.metrics), ...definition.aliases);
            const checked = props.visibility[definition.key] !== false;
            return (React.createElement(Controls_1.ToggleField, { id: `ctm-${definition.key.replace(/[^a-z0-9]+/gi, "-")}`, key: definition.key, checked: checked, disabled: count === null, onChange: (value) => (0, api_1.trigger)(BINDING_GROUP, "setOverlaySublayerVisible", JSON.stringify({ key: definition.key, value })) },
                React.createElement("span", { className: "ctm-overlay-sublayer-label" },
                    React.createElement("span", null, definition.label),
                    React.createElement("strong", null, (0, bundleStats_1.formatCount)(count)))));
        }))));
}
function OverlayLayersPanel({ stats }) {
    const available = (0, api_1.useValue)(overlayLayersAvailable$);
    const zoningVisible = (0, api_1.useValue)(overlayZoningVisible$);
    const roadsVisible = (0, api_1.useValue)(overlayRoadsVisible$);
    const pathsVisible = (0, api_1.useValue)(overlayPathsVisible$);
    const waterVisible = (0, api_1.useValue)(overlayWaterVisible$);
    const boundsVisible = (0, api_1.useValue)(overlayBoundsVisible$);
    const zoningOpacity = (0, api_1.useValue)(overlayZoningOpacity$);
    const roadsOpacity = (0, api_1.useValue)(overlayRoadsOpacity$);
    const pathsOpacity = (0, api_1.useValue)(overlayPathsOpacity$);
    const waterOpacity = (0, api_1.useValue)(overlayWaterOpacity$);
    const boundsOpacity = (0, api_1.useValue)(overlayBoundsOpacity$);
    const visibility = parseSublayerVisibility((0, api_1.useValue)(overlaySublayersJson$));
    const activeSublayers = ALL_SUBLAYERS.filter((layer) => visibility[layer.key] !== false).length;
    const setVisible = (triggerName, value) => (0, api_1.trigger)(BINDING_GROUP, triggerName, value);
    const setOpacity = (triggerName, value) => (0, api_1.trigger)(BINDING_GROUP, triggerName, value / 100);
    return (React.createElement(Controls_1.Foldout, { num: "07", title: "Couches visibles / transparence", state: `${activeSublayers}/${ALL_SUBLAYERS.length} sous-couches actives`, note: "Contr\u00F4les live reli\u00E9s au rendu : 11 zoning, 7 routes/chemins et 2 hydrographie.", defaultOpen: true },
        React.createElement(Controls_1.GroupTitle, null, "Groupes ma\u00EEtres"),
        React.createElement(Controls_1.Row, { columns: 2 },
            React.createElement(Controls_1.ToggleField, { id: "ctm-overlay-zoning", checked: zoningVisible, disabled: !available, onChange: (value) => setVisible("setOverlayZoningVisible", value) }, "Zonage"),
            React.createElement(Controls_1.ToggleField, { id: "ctm-overlay-roads", checked: roadsVisible, disabled: !available, onChange: (value) => setVisible("setOverlayRoadsVisible", value) }, "Routes"),
            React.createElement(Controls_1.ToggleField, { id: "ctm-overlay-paths", checked: pathsVisible, disabled: !available, onChange: (value) => setVisible("setOverlayPathsVisible", value) }, "Chemins"),
            React.createElement(Controls_1.ToggleField, { id: "ctm-overlay-water", checked: waterVisible, disabled: !available, onChange: (value) => setVisible("setOverlayWaterVisible", value) }, "Eau"),
            React.createElement(Controls_1.ToggleField, { id: "ctm-overlay-bounds", checked: boundsVisible, disabled: !available, onChange: (value) => setVisible("setOverlayBoundsVisible", value) }, "Limites de carte")),
        React.createElement(Controls_1.GroupTitle, null, "Transparence par groupe"),
        React.createElement(Controls_1.SliderField, { label: "Alpha zoning", min: 0, max: 100, step: 5, value: Math.round(clamp01(zoningOpacity) * 100), suffix: "%", disabled: !available || !zoningVisible, onChange: (value) => setOpacity("setOverlayZoningOpacity", value) }),
        React.createElement(Controls_1.SliderField, { label: "Alpha routes", min: 0, max: 100, step: 5, value: Math.round(clamp01(roadsOpacity) * 100), suffix: "%", disabled: !available || !roadsVisible, onChange: (value) => setOpacity("setOverlayRoadsOpacity", value) }),
        React.createElement(Controls_1.SliderField, { label: "Alpha chemins", min: 0, max: 100, step: 5, value: Math.round(clamp01(pathsOpacity) * 100), suffix: "%", disabled: !available || !pathsVisible, onChange: (value) => setOpacity("setOverlayPathsOpacity", value) }),
        React.createElement(Controls_1.SliderField, { label: "Alpha eau", min: 0, max: 100, step: 5, value: Math.round(clamp01(waterOpacity) * 100), suffix: "%", disabled: !available || !waterVisible, onChange: (value) => setOpacity("setOverlayWaterOpacity", value) }),
        React.createElement(Controls_1.SliderField, { label: "Alpha limites", min: 0, max: 100, step: 5, value: Math.round(clamp01(boundsOpacity) * 100), suffix: "%", disabled: !available || !boundsVisible, onChange: (value) => setOpacity("setOverlayBoundsOpacity", value) }),
        React.createElement(SublayerGroup, { title: "Zonage et stationnement \u00B7 11", definitions: ZONING_SUBLAYERS, stats: stats, visibility: visibility }),
        React.createElement(SublayerGroup, { title: "Routes et chemins \u00B7 7", definitions: ROAD_SUBLAYERS, stats: stats, visibility: visibility }),
        React.createElement(SublayerGroup, { title: "Hydrographie \u00B7 2", definitions: WATER_SUBLAYERS, stats: stats, visibility: visibility }),
        React.createElement(Controls_1.Readout, null, "Chaque ligne est aliment\u00E9e par le bundle actif et pilote uniquement un mat\u00E9riau ou un filtre visuel.")));
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
function ServicesPanel({ services }) {
    const isFamilyAvailable = (family) => family.available && family.total !== null;
    const availableServices = services.filter(isFamilyAvailable);
    const availableFamilies = availableServices.length;
    const hasKnownTotal = availableServices.length > 0;
    const totalPoints = hasKnownTotal
        ? availableServices.reduce((total, family) => total + (family.total ?? 0), 0)
        : null;
    return (React.createElement(Controls_1.Foldout, { num: "09", title: "Services", state: `${availableFamilies}/${services.length} familles · ${(0, bundleStats_1.formatCount)(totalPoints)} points`, note: "Points OSM projet\u00E9s sur le terrain, sans placement ni modification du r\u00E9seau du jeu.", defaultOpen: true }, availableFamilies === 0 ? (React.createElement("div", { className: "ctm-services-message", role: "status", "aria-live": "polite" }, "Aucune donn\u00E9e de services disponible dans ce bundle.")) : (React.createElement("div", { className: "ctm-service-family-grid" }, services.map((family) => {
        const familyAvailable = isFamilyAvailable(family);
        const disabled = !familyAvailable;
        const opacityPercent = Math.round(family.opacity * 100);
        return (React.createElement("section", { className: `ctm-service-family service-${familyClassName(family.key)}`, key: family.key },
            React.createElement("header", { className: "ctm-service-family-heading" },
                React.createElement("span", { className: "ctm-service-swatch", "aria-hidden": "true" }),
                React.createElement("span", { className: "ctm-service-family-title" }, family.label),
                React.createElement("span", { className: `ctm-service-family-state ${familyAvailable ? "is-connected" : ""}` }, familyAvailable ? "connecté" : "indisponible"),
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
    const availableServiceFamilies = stats.services.filter((family) => family.available && family.total !== null);
    return (React.createElement("div", { className: "ctm-pane ctm-statistics-pane" },
        React.createElement(Controls_1.Section, { num: "S0", title: "Indicateurs du bundle", state: state, note: stats.status },
            React.createElement("div", { className: "ctm-statistics-summary" },
                React.createElement(Controls_1.Readout, null, bundleLabel),
                React.createElement(Controls_1.Readout, null,
                    "Entit\u00E9s visuelles du bundle \u00B7 ",
                    React.createElement("strong", null, (0, bundleStats_1.formatCount)(stats.visualEntities))),
                React.createElement(Controls_1.Readout, null,
                    "Objets overlay historiques \u00B7 ",
                    (0, bundleStats_1.formatCount)(stats.objects),
                    " \u00B7 \u00E9l\u00E9ments OSM uniques \u00B7 ",
                    (0, bundleStats_1.formatCount)(stats.uniqueOsmElements)),
                React.createElement(Controls_1.Readout, null, "0 = source charg\u00E9e sans objet correspondant \u00B7 \u2014 = donn\u00E9e absente du bundle"),
                React.createElement(Controls_1.Readout, null,
                    "Contrat bundle complet \u00B7 ",
                    React.createElement("strong", null,
                        (0, bundleStats_1.formatCount)(stats.coverage.presentFiles),
                        " / ",
                        (0, bundleStats_1.formatCount)(stats.coverage.expectedFiles),
                        " fichiers"),
                    stats.coverage.complete ? " · complet" : " · incomplet"),
                !stats.coverage.complete && stats.coverage.missingFiles.length > 0 ? (React.createElement(Controls_1.Readout, null,
                    "Manquants : ",
                    stats.coverage.missingFiles.join(", "))) : null)),
        React.createElement(Controls_1.Foldout, { num: "S1", title: "Zoning et stationnement", state: state, defaultOpen: true },
            React.createElement("table", { className: "ctm-stats-table" },
                React.createElement("tbody", null, renderMetricRows(stats.zoning, ZONING_METRICS)))),
        React.createElement(Controls_1.Foldout, { num: "S2", title: "Routes, chemins et hydrographie", state: state, defaultOpen: true },
            React.createElement("table", { className: "ctm-stats-table" },
                React.createElement("tbody", null,
                    renderMetricRows(stats.roads, ROAD_METRICS),
                    renderMetricRows(stats.water, WATER_METRICS)))),
        React.createElement(Controls_1.Foldout, { num: "S3", title: "Services", state: `${availableServiceFamilies.length}/${stats.services.length} familles connectées`, defaultOpen: true }, availableServiceFamilies.length === 0 ? (React.createElement(Controls_1.Readout, null, "Aucune donn\u00E9e de services disponible dans ce bundle.")) : (React.createElement("div", { className: "ctm-service-stats-list" }, availableServiceFamilies.map((family) => (React.createElement("section", { className: "ctm-service-stat-family", key: family.key },
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
