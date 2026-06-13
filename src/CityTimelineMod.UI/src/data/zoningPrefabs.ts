export type ZoningDensity = "basse" | "moyenne" | "haute";

export type ZoningFamily = {
  id: "residentiel" | "commercial" | "industriel" | "bureaux";
  label: string;
  densities: {
    density: ZoningDensity;
    label: string;
    prefabs: string[];
  }[];
};

export const zoningFamilies: ZoningFamily[] = [
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
