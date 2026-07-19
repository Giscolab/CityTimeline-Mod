import type { ReactNode } from "react";

import { Foldout, Readout, Section } from "./Controls";
import {
  formatCount,
  getMetric,
} from "./bundleStats";
import type {
  BundleStatsSnapshot,
  MetricRecord,
} from "./bundleStats";

interface StatisticsPanelProps {
  stats: BundleStatsSnapshot;
}

interface MetricDefinition {
  label: string;
  aliases: string[];
}

const ZONING_METRICS: MetricDefinition[] = [
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

const ROAD_METRICS: MetricDefinition[] = [
  { label: "Autoroute", aliases: ["highway", "motorway"] },
  { label: "Axe principal", aliases: ["main", "mainRoad", "largeRoad", "large_road", "primary"] },
  { label: "Route secondaire", aliases: ["secondary", "secondaryRoad", "mediumRoad", "medium_road"] },
  { label: "Route tertiaire / résidentielle", aliases: ["tertiary", "tertiaryResidential", "tertiary_residential", "smallRoad", "small_road"] },
  { label: "Bretelle / liaison", aliases: ["ramp", "link", "rampLink", "ramp_link"] },
  { label: "Route non classée", aliases: ["unclassified", "unclassifiedRoad", "gravelRoad", "gravel_road", "other"] },
  { label: "Chemin / piéton", aliases: ["paths", "pathways", "pathway"] },
  { label: "Total routes", aliases: ["total", "roads", "roadTotal", "road_total"] },
];

const WATER_METRICS: MetricDefinition[] = [
  { label: "Eau — lignes", aliases: ["lines", "lineCount", "line_count"] },
  { label: "Eau — surfaces", aliases: ["areas", "areaCount", "area_count"] },
];

const RAILWAY_METRICS: MetricDefinition[] = [
  { label: "Train", aliases: ["train"] },
  { label: "Tramway", aliases: ["tram", "tramway"] },
  { label: "Métro léger", aliases: ["lightRail", "light_rail"] },
  { label: "Métro", aliases: ["subway", "metro"] },
  { label: "Voies de service", aliases: ["service", "serviceTracks", "service_tracks"] },
  { label: "Tunnels", aliases: ["tunnels", "tunnel"] },
  { label: "Total", aliases: ["total"] },
];

function renderMetricRows(
  metrics: MetricRecord,
  definitions: MetricDefinition[],
): ReactNode {
  return definitions.map((definition) => (
    <tr key={definition.label}>
      <th scope="row">{definition.label}</th>
      <td className="count-cell">
        {formatCount(getMetric(metrics, ...definition.aliases))}
      </td>
    </tr>
  ));
}

export function StatisticsPanel({ stats }: StatisticsPanelProps) {
  const bundleLabel = stats.bundleName || stats.bundleId || "Bundle actif";
  const state = stats.available ? "connecté" : "indisponible";

  return (
    <div className="ctm-pane ctm-statistics-pane">
      <Section
        num="S0"
        title="Indicateurs du bundle"
        state={state}
        note={stats.status}
      >
        <div className="ctm-statistics-summary">
          <Readout>{bundleLabel}</Readout>
          <Readout>
            Objets OSM exploitables · <strong>{formatCount(stats.objects)}</strong>
          </Readout>
        </div>
      </Section>

      <Foldout
        num="S1"
        title="Zoning et stationnement"
        state={state}
        defaultOpen
      >
        <table className="ctm-stats-table">
          <tbody>{renderMetricRows(stats.zoning, ZONING_METRICS)}</tbody>
        </table>
      </Foldout>

      <Foldout
        num="S2"
        title="Routes, chemins et hydrographie"
        state={state}
        defaultOpen
      >
        <table className="ctm-stats-table">
          <tbody>
            {renderMetricRows(stats.roads, ROAD_METRICS)}
            {renderMetricRows(stats.water, WATER_METRICS)}
          </tbody>
        </table>
      </Foldout>

      <Foldout
        num="S3"
        title="Services"
        state={`${stats.services.length} familles`}
        defaultOpen
      >
        {stats.services.length === 0 ? (
          <Readout>Aucune donnée de services disponible dans ce bundle.</Readout>
        ) : (
          <div className="ctm-service-stats-list">
            {stats.services.map((family) => (
              <section className="ctm-service-stat-family" key={family.key}>
                <header className="ctm-service-family-heading">
                  <span>{family.label}</span>
                  <span className="ctm-service-family-state">
                    {family.available ? "connecté" : "indisponible"}
                  </span>
                  <strong>{formatCount(family.total)}</strong>
                </header>

                <table className="ctm-stats-table ctm-stats-table-compact">
                  <tbody>
                    {family.subcategories.map((subcategory) => (
                      <tr key={`${family.key}.${subcategory.key}`}>
                        <th scope="row">{subcategory.label}</th>
                        <td className="count-cell">{formatCount(subcategory.count)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </section>
            ))}
          </div>
        )}
      </Foldout>

      <Foldout
        num="S4"
        title="Réseau ferroviaire"
        state={formatCount(getMetric(stats.railway, "total"))}
      >
        <table className="ctm-stats-table">
          <tbody>{renderMetricRows(stats.railway, RAILWAY_METRICS)}</tbody>
        </table>
      </Foldout>
    </div>
  );
}
