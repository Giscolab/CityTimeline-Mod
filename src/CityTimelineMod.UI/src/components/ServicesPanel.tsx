import { trigger } from "cs2/api";

import { BINDING_GROUP } from "../bindings";
import { Foldout, SliderField, ToggleField } from "./Controls";
import { formatCount } from "./bundleStats";
import type { ServiceFamilyStats } from "./bundleStats";

interface ServicesPanelProps {
  services: ServiceFamilyStats[];
}

function emitServiceSetting(
  triggerName: "setServiceVisible" | "setServiceOpacity",
  key: string,
  value: boolean | number,
) {
  trigger(
    BINDING_GROUP,
    triggerName,
    JSON.stringify({ key, value }),
  );
}

function familyClassName(key: string): string {
  return key.toLowerCase().replace(/[^a-z0-9_-]/g, "-");
}

export function ServicesPanel({ services }: ServicesPanelProps) {
  const isFamilyAvailable = (family: ServiceFamilyStats) =>
    family.available && family.total !== null;
  const availableServices = services.filter(isFamilyAvailable);
  const availableFamilies = availableServices.length;
  const hasKnownTotal = availableServices.length > 0;
  const totalPoints = hasKnownTotal
    ? availableServices.reduce((total, family) => total + (family.total ?? 0), 0)
    : null;

  return (
    <Foldout
      num="09"
      title="Services"
      state={`${availableFamilies}/${services.length} familles · ${formatCount(totalPoints)} points`}
      note="Points OSM projetés sur le terrain, sans placement ni modification du réseau du jeu."
      defaultOpen
    >
      {availableFamilies === 0 ? (
        <div className="ctm-services-message" role="status" aria-live="polite">
          Aucune donnée de services disponible dans ce bundle.
        </div>
      ) : (
        <div className="ctm-service-family-grid">
          {services.map((family) => {
            const familyAvailable = isFamilyAvailable(family);
            const disabled = !familyAvailable;
            const opacityPercent = Math.round(family.opacity * 100);

            return (
              <section
                className={`ctm-service-family service-${familyClassName(family.key)}`}
                key={family.key}
              >
                <header className="ctm-service-family-heading">
                  <span className="ctm-service-swatch" aria-hidden="true" />
                  <span className="ctm-service-family-title">{family.label}</span>
                  <span className={`ctm-service-family-state ${familyAvailable ? "is-connected" : ""}`}>
                    {familyAvailable ? "connecté" : "indisponible"}
                  </span>
                  <strong>{formatCount(family.total)}</strong>
                </header>

                <ToggleField
                  id={`ctm-service-${familyClassName(family.key)}-visible`}
                  checked={family.visible}
                  disabled={disabled}
                  onChange={(value) =>
                    emitServiceSetting("setServiceVisible", family.key, value)
                  }
                >
                  Afficher
                </ToggleField>

                <SliderField
                  label="Opacité"
                  min={0}
                  max={100}
                  step={5}
                  value={opacityPercent}
                  suffix="%"
                  disabled={disabled || !family.visible}
                  onChange={(value) =>
                    emitServiceSetting("setServiceOpacity", family.key, value / 100)
                  }
                />

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
            );
          })}
        </div>
      )}
    </Foldout>
  );
}
