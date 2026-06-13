import React, { useMemo, useState } from "react";
import { zoningFamilies } from "../data/zoningPrefabs";
import { Readout, SelectField } from "./Controls";

export function ZoningPanel() {
  const [familyId, setFamilyId] = useState(zoningFamilies[0].id);
  const family = useMemo(() => zoningFamilies.find((item) => item.id === familyId) || zoningFamilies[0], [familyId]);

  return (
    <div className="zoning-panel">
      <SelectField
        label="Famille zoning"
        value={familyId}
        options={zoningFamilies.map((item) => ({ value: item.id, label: item.label }))}
        onChange={(value) => setFamilyId(value as typeof familyId)}
      />
      <div className="zoning-density-list">
        {family.densities.map((density) => (
          <div className="zoning-density" key={density.label}>
            <div className="group-title">{family.label} · {density.label}</div>
            {density.prefabs.map((prefab) => (
              <Readout key={prefab}>{prefab}</Readout>
            ))}
          </div>
        ))}
      </div>
    </div>
  );
}
