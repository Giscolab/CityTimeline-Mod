export type RoadFamilyId =
  | "Small roads"
  | "Medium roads"
  | "Large roads"
  | "Highway"
  | "Alley"
  | "Pedestrian"
  | "Public transport lane"
  | "Quay / Harbor"
  | "Bridge"
  | "Other street"
  | "Special / Dam";

export type RoadPrefab = {
  index: number;
  family: RoadFamilyId;
  name: string;
  speed: string;
};

export type RoadFamily = {
  id: RoadFamilyId;
  label: string;
  summary: string;
  prefabs: RoadPrefab[];
};

const names = (family: RoadFamilyId, speed: string, values: string[]): RoadPrefab[] =>
  values.map((name) => ({
    index: -1,
    family,
    name,
    speed,
  }));

const withIndex = (prefabs: RoadPrefab[]): RoadPrefab[] =>
  prefabs.map((prefab, index) => ({ ...prefab, index }));

const familiesWithoutIndex: RoadFamily[] = [
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

export const roadPrefabFamilies: RoadFamily[] = familiesWithoutIndex.map((family) => ({
  ...family,
  prefabs: indexedPrefabs.filter((prefab) => prefab.family === family.id),
}));

export const roadPrefabs: RoadPrefab[] = indexedPrefabs;

export const roadPrefabTotal = roadPrefabs.length;

export function getRoadDirection(prefabName: string): string {
  if (prefabName.includes("Oneway")) return "one-way";
  if (prefabName.includes("Twoway")) return "two-way";
  return "two-way/unknown";
}

export function getRoadLaneLabel(prefabName: string): string {
  const laneMatch = prefabName.match(/(\d+)[ +\\-]?(?:Lanes|lanes|lane)/);
  if (laneMatch) return `${laneMatch[1]} voie(s)`;
  const asymmetricMatch = prefabName.match(/(\d\+\d)/);
  if (asymmetricMatch) return asymmetricMatch[1];
  return "—";
}

export function getRoadTraits(prefab: RoadPrefab): string[] {
  const traits: string[] = [];
  if (prefab.family !== "Highway" && prefab.family !== "Bridge") traits.push("zoning");
  if (prefab.name.includes("Parking")) traits.push("parking");
  if (prefab.name.includes("Angled")) traits.push("stationnement angle");
  if (prefab.name.includes("Bridge") || prefab.family === "Bridge") traits.push("bridge");
  if (prefab.name.includes("Quay") || prefab.name.includes("Harbor")) traits.push("waterfront");
  if (prefab.name.includes("Public Transport")) traits.push("transport public");
  if (prefab.name.includes("Pedestrian")) traits.push("pieton");
  if (prefab.name.includes("Highway")) traits.push("highway");
  return traits.length > 0 ? traits : ["standard"];
}

export function getRoadFlags(prefab: RoadPrefab): string {
  const flags: string[] = [];
  if (prefab.family !== "Highway" && prefab.family !== "Bridge") flags.push("EnableZoning");
  if (prefab.name.includes("Asymmetric")) flags.push("PreferTrafficLights");
  if (prefab.name.includes("Oneway")) flags.push("OneWay");
  if (prefab.family === "Bridge") flags.push("Bridge");
  return flags.length > 0 ? flags.join(", ") : "—";
}
