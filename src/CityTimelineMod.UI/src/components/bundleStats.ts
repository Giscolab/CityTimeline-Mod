import { bindValue, useValue } from "cs2/api";

import { BINDING_GROUP } from "../bindings";

export type MetricRecord = Record<string, number | null>;

export interface ServiceSubcategoryStats {
  key: string;
  label: string;
  count: number | null;
}

export interface ServiceFamilyStats {
  key: string;
  label: string;
  available: boolean;
  total: number | null;
  visible: boolean;
  opacity: number;
  subcategories: ServiceSubcategoryStats[];
}

export interface BundleStatsSnapshot {
  available: boolean;
  status: string;
  bundleId: string;
  bundleName: string;
  objects: number | null;
  visualEntities: number | null;
  uniqueOsmElements: number | null;
  zoning: MetricRecord;
  roads: MetricRecord;
  water: MetricRecord;
  services: ServiceFamilyStats[];
  railway: MetricRecord;
  coverage: {
    complete: boolean;
    presentFiles: number;
    expectedFiles: number;
    missingFiles: string[];
  };
}

export const bundleStatsJson$ = bindValue<string>(
  BINDING_GROUP,
  "bundleStatsJson",
  "{}",
);

const EMPTY_STATS: BundleStatsSnapshot = {
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

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function asString(value: unknown): string {
  return typeof value === "string" ? value.trim() : "";
}

function asCount(value: unknown): number | null {
  if (typeof value !== "number" || !Number.isFinite(value)) {
    return null;
  }

  return Math.max(0, Math.trunc(value));
}

function asOpacity(value: unknown): number {
  if (typeof value !== "number" || !Number.isFinite(value)) {
    return 1;
  }

  return Math.min(1, Math.max(0, value));
}

function asMetrics(value: unknown): MetricRecord {
  if (!isRecord(value)) {
    return {};
  }

  const result: MetricRecord = {};

  Object.keys(value).forEach((key) => {
    const count = asCount(value[key]);

    if (count !== null) {
      result[key] = count;
    }
  });

  return result;
}

function asSubcategories(value: unknown): ServiceSubcategoryStats[] {
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

function asServices(value: unknown): ServiceFamilyStats[] {
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

function asCoverage(value: unknown): BundleStatsSnapshot["coverage"] {
  if (!isRecord(value)) return EMPTY_STATS.coverage;
  const presentFiles = asCount(value.presentFiles) ?? 0;
  const expectedFiles = asCount(value.expectedFiles) ?? 0;
  const missingFiles = Array.isArray(value.missingFiles)
    ? value.missingFiles.filter((item): item is string => typeof item === "string")
    : [];
  return {
    complete: value.complete === true,
    presentFiles,
    expectedFiles,
    missingFiles,
  };
}

export function parseBundleStatsJson(json: string): BundleStatsSnapshot {
  if (!json || !json.trim()) {
    return EMPTY_STATS;
  }

  try {
    const value: unknown = JSON.parse(json);

    if (!isRecord(value)) {
      return EMPTY_STATS;
    }

    const available = value.available === true;

    return {
      available,
      status: asString(value.status) || (
        available ? "Données du bundle chargées." : EMPTY_STATS.status
      ),
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
  } catch (error) {
    console.error("[CityTimelineMod UI] Invalid bundleStatsJson binding", error);
    return {
      ...EMPTY_STATS,
      status: "Statistiques du bundle illisibles.",
    };
  }
}

export function useBundleStats(): BundleStatsSnapshot {
  return parseBundleStatsJson(useValue(bundleStatsJson$));
}

export function formatCount(value: number | null | undefined): string {
  if (value === null || value === undefined || !Number.isFinite(value)) {
    return "—";
  }

  const safeValue = Math.max(0, Math.trunc(value));
  return String(safeValue).replace(/\B(?=(\d{3})+(?!\d))/g, " ");
}

function normalizeMetricKey(value: string): string {
  return value.toLocaleLowerCase().replace(/[^a-z0-9]/g, "");
}

export function getMetric(
  metrics: MetricRecord,
  ...aliases: string[]
): number | null {
  const normalizedAliases = aliases.map(normalizeMetricKey);

  for (const key of Object.keys(metrics)) {
    const normalizedKey = normalizeMetricKey(key);
    const matches = normalizedAliases.some((alias) =>
      normalizedKey === alias ||
      normalizedKey === `${alias}count` ||
      `${normalizedKey}count` === alias
    );

    if (matches) {
      return metrics[key] ?? null;
    }
  }

  return null;
}
