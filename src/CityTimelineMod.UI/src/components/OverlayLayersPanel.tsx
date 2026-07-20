import { bindValue, trigger, useValue } from "cs2/api";

import { Foldout, GroupTitle, Readout, Row, SliderField, ToggleField } from "./Controls";
import { formatCount, getMetric } from "./bundleStats";
import type { BundleStatsSnapshot, MetricRecord } from "./bundleStats";

const BINDING_GROUP = "CityTimelineMod";

const overlayLayersAvailable$ = bindValue<boolean>(BINDING_GROUP, "overlayLayersAvailable", false);
const overlayZoningVisible$ = bindValue<boolean>(BINDING_GROUP, "overlayZoningVisible", true);
const overlayRoadsVisible$ = bindValue<boolean>(BINDING_GROUP, "overlayRoadsVisible", true);
const overlayPathsVisible$ = bindValue<boolean>(BINDING_GROUP, "overlayPathsVisible", true);
const overlayWaterVisible$ = bindValue<boolean>(BINDING_GROUP, "overlayWaterVisible", true);
const overlayBoundsVisible$ = bindValue<boolean>(BINDING_GROUP, "overlayBoundsVisible", true);
const overlayZoningOpacity$ = bindValue<number>(BINDING_GROUP, "overlayZoningOpacity", 1);
const overlayRoadsOpacity$ = bindValue<number>(BINDING_GROUP, "overlayRoadsOpacity", 1);
const overlayPathsOpacity$ = bindValue<number>(BINDING_GROUP, "overlayPathsOpacity", 1);
const overlayWaterOpacity$ = bindValue<number>(BINDING_GROUP, "overlayWaterOpacity", 1);
const overlayBoundsOpacity$ = bindValue<number>(BINDING_GROUP, "overlayBoundsOpacity", 1);
const overlaySublayersJson$ = bindValue<string>(BINDING_GROUP, "overlaySublayersJson", "{}");

interface SublayerDefinition {
  key: string;
  label: string;
  metrics: "zoning" | "roads" | "water";
  aliases: string[];
}

const ZONING_SUBLAYERS: SublayerDefinition[] = [
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

const ROAD_SUBLAYERS: SublayerDefinition[] = [
  { key: "roads.highway", label: "Autoroute", metrics: "roads", aliases: ["highway", "motorway"] },
  { key: "roads.large_road", label: "Axe principal", metrics: "roads", aliases: ["main", "largeRoad", "large_road"] },
  { key: "roads.medium_road", label: "Route secondaire", metrics: "roads", aliases: ["secondary", "mediumRoad", "medium_road"] },
  { key: "roads.small_road", label: "Route tertiaire / résidentielle", metrics: "roads", aliases: ["tertiary", "smallRoad", "small_road"] },
  { key: "roads.ramp", label: "Bretelle / liaison", metrics: "roads", aliases: ["ramp", "link"] },
  { key: "roads.gravel_road", label: "Route non classée", metrics: "roads", aliases: ["unclassified", "gravelRoad", "gravel_road"] },
  { key: "roads.pathway", label: "Chemin / piéton", metrics: "roads", aliases: ["paths", "pathways", "pathway"] },
];

const WATER_SUBLAYERS: SublayerDefinition[] = [
  { key: "water.lines", label: "Eau — lignes", metrics: "water", aliases: ["lines", "lineCount", "line_count"] },
  { key: "water.areas", label: "Eau — surfaces", metrics: "water", aliases: ["areas", "areaCount", "area_count"] },
];

const ALL_SUBLAYERS = [...ZONING_SUBLAYERS, ...ROAD_SUBLAYERS, ...WATER_SUBLAYERS];

function clamp01(value: number): number {
  return Math.min(1, Math.max(0, Number.isFinite(value) ? value : 1));
}

function parseSublayerVisibility(json: string): Record<string, boolean> {
  try {
    const parsed: unknown = JSON.parse(json || "{}");
    if (typeof parsed !== "object" || parsed === null || Array.isArray(parsed)) return {};
    const layers = (parsed as { layers?: unknown }).layers;
    if (typeof layers !== "object" || layers === null || Array.isArray(layers)) return {};

    const result: Record<string, boolean> = {};
    Object.entries(layers).forEach(([key, value]) => {
      if (typeof value === "boolean") result[key] = value;
    });
    return result;
  } catch {
    return {};
  }
}

function layerMetrics(stats: BundleStatsSnapshot, kind: SublayerDefinition["metrics"]): MetricRecord {
  return stats[kind];
}

function SublayerGroup(props: {
  title: string;
  definitions: SublayerDefinition[];
  stats: BundleStatsSnapshot;
  visibility: Record<string, boolean>;
}) {
  return (
    <section className="ctm-overlay-sublayer-group">
      <GroupTitle>{props.title}</GroupTitle>
      <div className="ctm-overlay-sublayer-grid">
        {props.definitions.map((definition) => {
          const count = getMetric(layerMetrics(props.stats, definition.metrics), ...definition.aliases);
          const checked = props.visibility[definition.key] !== false;
          return (
            <ToggleField
              id={`ctm-${definition.key.replace(/[^a-z0-9]+/gi, "-")}`}
              key={definition.key}
              checked={checked}
              disabled={count === null}
              onChange={(value) => trigger(
                BINDING_GROUP,
                "setOverlaySublayerVisible",
                JSON.stringify({ key: definition.key, value }),
              )}
            >
              <span className="ctm-overlay-sublayer-label">
                <span>{definition.label}</span>
                <strong>{formatCount(count)}</strong>
              </span>
            </ToggleField>
          );
        })}
      </div>
    </section>
  );
}

export function OverlayLayersPanel({ stats }: { stats: BundleStatsSnapshot }) {
  const available = useValue(overlayLayersAvailable$);
  const zoningVisible = useValue(overlayZoningVisible$);
  const roadsVisible = useValue(overlayRoadsVisible$);
  const pathsVisible = useValue(overlayPathsVisible$);
  const waterVisible = useValue(overlayWaterVisible$);
  const boundsVisible = useValue(overlayBoundsVisible$);
  const zoningOpacity = useValue(overlayZoningOpacity$);
  const roadsOpacity = useValue(overlayRoadsOpacity$);
  const pathsOpacity = useValue(overlayPathsOpacity$);
  const waterOpacity = useValue(overlayWaterOpacity$);
  const boundsOpacity = useValue(overlayBoundsOpacity$);
  const visibility = parseSublayerVisibility(useValue(overlaySublayersJson$));
  const activeSublayers = ALL_SUBLAYERS.filter((layer) => visibility[layer.key] !== false).length;

  const setVisible = (triggerName: string, value: boolean) =>
    trigger(BINDING_GROUP, triggerName, value);
  const setOpacity = (triggerName: string, value: number) =>
    trigger(BINDING_GROUP, triggerName, value / 100);

  return (
    <Foldout
      num="07"
      title="Couches visibles / transparence"
      state={`${activeSublayers}/${ALL_SUBLAYERS.length} sous-couches actives`}
      note="Contrôles live reliés au rendu : 11 zoning, 7 routes/chemins et 2 hydrographie."
      defaultOpen
    >
      <GroupTitle>Groupes maîtres</GroupTitle>
      <Row columns={2}>
        <ToggleField id="ctm-overlay-zoning" checked={zoningVisible} disabled={!available} onChange={(value) => setVisible("setOverlayZoningVisible", value)}>Zonage</ToggleField>
        <ToggleField id="ctm-overlay-roads" checked={roadsVisible} disabled={!available} onChange={(value) => setVisible("setOverlayRoadsVisible", value)}>Routes</ToggleField>
        <ToggleField id="ctm-overlay-paths" checked={pathsVisible} disabled={!available} onChange={(value) => setVisible("setOverlayPathsVisible", value)}>Chemins</ToggleField>
        <ToggleField id="ctm-overlay-water" checked={waterVisible} disabled={!available} onChange={(value) => setVisible("setOverlayWaterVisible", value)}>Eau</ToggleField>
        <ToggleField id="ctm-overlay-bounds" checked={boundsVisible} disabled={!available} onChange={(value) => setVisible("setOverlayBoundsVisible", value)}>Limites de carte</ToggleField>
      </Row>

      <GroupTitle>Transparence par groupe</GroupTitle>
      <SliderField label="Alpha zoning" min={0} max={100} step={5} value={Math.round(clamp01(zoningOpacity) * 100)} suffix="%" disabled={!available || !zoningVisible} onChange={(value) => setOpacity("setOverlayZoningOpacity", value)} />
      <SliderField label="Alpha routes" min={0} max={100} step={5} value={Math.round(clamp01(roadsOpacity) * 100)} suffix="%" disabled={!available || !roadsVisible} onChange={(value) => setOpacity("setOverlayRoadsOpacity", value)} />
      <SliderField label="Alpha chemins" min={0} max={100} step={5} value={Math.round(clamp01(pathsOpacity) * 100)} suffix="%" disabled={!available || !pathsVisible} onChange={(value) => setOpacity("setOverlayPathsOpacity", value)} />
      <SliderField label="Alpha eau" min={0} max={100} step={5} value={Math.round(clamp01(waterOpacity) * 100)} suffix="%" disabled={!available || !waterVisible} onChange={(value) => setOpacity("setOverlayWaterOpacity", value)} />
      <SliderField label="Alpha limites" min={0} max={100} step={5} value={Math.round(clamp01(boundsOpacity) * 100)} suffix="%" disabled={!available || !boundsVisible} onChange={(value) => setOpacity("setOverlayBoundsOpacity", value)} />

      <SublayerGroup title="Zonage et stationnement · 11" definitions={ZONING_SUBLAYERS} stats={stats} visibility={visibility} />
      <SublayerGroup title="Routes et chemins · 7" definitions={ROAD_SUBLAYERS} stats={stats} visibility={visibility} />
      <SublayerGroup title="Hydrographie · 2" definitions={WATER_SUBLAYERS} stats={stats} visibility={visibility} />

      <Readout>
        Chaque ligne est alimentée par le bundle actif et pilote uniquement un matériau ou un filtre visuel.
      </Readout>
    </Foldout>
  );
}
