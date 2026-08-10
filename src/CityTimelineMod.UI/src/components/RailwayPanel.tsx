import { bindValue, trigger, useValue } from "cs2/api";

import { BINDING_GROUP } from "../bindings";
import {
  Foldout,
  GroupTitle,
  SliderField,
  ToggleField,
} from "./Controls";

export const railwayAvailable$ = bindValue<boolean>(
  BINDING_GROUP,
  "railwayAvailable",
  false,
);
export const railwayStatus$ = bindValue<string>(
  BINDING_GROUP,
  "railwayStatus",
  "Aucune donnée ferroviaire disponible dans ce bundle.",
);
export const railwayVisible$ = bindValue<boolean>(
  BINDING_GROUP,
  "railwayVisible",
  true,
);
export const railwayOpacity$ = bindValue<number>(
  BINDING_GROUP,
  "railwayOpacity",
  0.9,
);
export const railwayThickness$ = bindValue<number>(
  BINDING_GROUP,
  "railwayThickness",
  3,
);

export const railwayTrainVisible$ = bindValue<boolean>(
  BINDING_GROUP,
  "railwayTrainVisible",
  true,
);
export const railwayTramVisible$ = bindValue<boolean>(
  BINDING_GROUP,
  "railwayTramVisible",
  true,
);
export const railwayLightRailVisible$ = bindValue<boolean>(
  BINDING_GROUP,
  "railwayLightRailVisible",
  true,
);
export const railwaySubwayVisible$ = bindValue<boolean>(
  BINDING_GROUP,
  "railwaySubwayVisible",
  true,
);
export const railwayServiceVisible$ = bindValue<boolean>(
  BINDING_GROUP,
  "railwayServiceVisible",
  true,
);
export const railwayTunnelsVisible$ = bindValue<boolean>(
  BINDING_GROUP,
  "railwayTunnelsVisible",
  true,
);

export const railwayTrainCount$ = bindValue<number>(
  BINDING_GROUP,
  "railwayTrainCount",
  0,
);
export const railwayTramCount$ = bindValue<number>(
  BINDING_GROUP,
  "railwayTramCount",
  0,
);
export const railwayLightRailCount$ = bindValue<number>(
  BINDING_GROUP,
  "railwayLightRailCount",
  0,
);
export const railwaySubwayCount$ = bindValue<number>(
  BINDING_GROUP,
  "railwaySubwayCount",
  0,
);
export const railwayServiceCount$ = bindValue<number>(
  BINDING_GROUP,
  "railwayServiceCount",
  0,
);
export const railwayTunnelCount$ = bindValue<number>(
  BINDING_GROUP,
  "railwayTunnelCount",
  0,
);
export const railwayTotalCount$ = bindValue<number>(
  BINDING_GROUP,
  "railwayTotalCount",
  0,
);

function formatCount(value: number): string {
  const safeValue = Number.isFinite(value)
    ? Math.max(0, Math.trunc(value))
    : 0;

  return String(safeValue).replace(/\B(?=(\d{3})+(?!\d))/g, " ");
}

function clamp(value: number, min: number, max: number): number {
  return Math.min(max, Math.max(min, value));
}

function RailwayFilter(props: {
  id: string;
  label: string;
  swatch: string;
  checked: boolean;
  count: number;
  disabled: boolean;
  triggerName: string;
}) {
  return (
    <ToggleField
      id={props.id}
      checked={props.checked}
      disabled={props.disabled}
      onChange={(checked) =>
        trigger(BINDING_GROUP, props.triggerName, checked)
      }
    >
      <span className="railway-filter-label">
        <span
          className={`railway-filter-swatch ${props.swatch}`}
          aria-hidden="true"
        />
        <span className="railway-filter-name">{props.label}</span>
        <span className="railway-filter-count">
          {formatCount(props.count)}
        </span>
      </span>
    </ToggleField>
  );
}

export function RailwayPanel() {
  const available = useValue(railwayAvailable$);
  const status = useValue(railwayStatus$);
  const visible = useValue(railwayVisible$);
  const opacity = useValue(railwayOpacity$);
  const thickness = useValue(railwayThickness$);

  const trainVisible = useValue(railwayTrainVisible$);
  const tramVisible = useValue(railwayTramVisible$);
  const lightRailVisible = useValue(railwayLightRailVisible$);
  const subwayVisible = useValue(railwaySubwayVisible$);
  const serviceVisible = useValue(railwayServiceVisible$);
  const tunnelsVisible = useValue(railwayTunnelsVisible$);

  const trainCount = useValue(railwayTrainCount$);
  const tramCount = useValue(railwayTramCount$);
  const lightRailCount = useValue(railwayLightRailCount$);
  const subwayCount = useValue(railwaySubwayCount$);
  const serviceCount = useValue(railwayServiceCount$);
  const tunnelCount = useValue(railwayTunnelCount$);
  const totalCount = useValue(railwayTotalCount$);

  const opacityPercent = Math.round(clamp(opacity, 0, 1) * 100);
  const safeThickness = clamp(thickness, 0.5, 8);
  const disabled = !available;

  return (
    <Foldout
      num="08"
      title="Réseau ferroviaire"
      state={available ? `${formatCount(totalCount)} voies` : "aucune donnée"}
      note="Calque de construction indépendant des routes et des points Transports."
      defaultOpen
    >
      {!available ? (
        <div className="railway-message" role="status" aria-live="polite">
          {status && status.trim()
            ? status
            : "Aucune donnée ferroviaire disponible dans ce bundle."}
        </div>
      ) : null}

      <div
        className={`railway-controls ${disabled ? "is-unavailable" : ""}`}
        aria-disabled={disabled}
      >
        <ToggleField
          id="ctm-railway-visible"
          checked={visible}
          disabled={disabled}
          onChange={(checked) =>
            trigger(BINDING_GROUP, "setRailwayVisible", checked)
          }
        >
          Afficher le réseau
        </ToggleField>

        <SliderField
          label="Opacité"
          min={0}
          max={100}
          step={5}
          value={opacityPercent}
          suffix="%"
          disabled={disabled}
          onChange={(value) =>
            trigger(BINDING_GROUP, "setRailwayOpacity", value / 100)
          }
        />

        <SliderField
          label="Épaisseur"
          min={0.5}
          max={8}
          step={0.5}
          value={safeThickness}
          suffix=" m"
          disabled={disabled}
          onChange={(value) =>
            trigger(BINDING_GROUP, "setRailwayThickness", value)
          }
        />

        <GroupTitle>Filtres et statistiques</GroupTitle>

        <div
          className="railway-filter-grid"
          role="group"
          aria-label="Filtres du réseau ferroviaire"
        >
          <RailwayFilter
            id="ctm-railway-train"
            label="Train"
            swatch="train"
            checked={trainVisible}
            count={trainCount}
            disabled={disabled}
            triggerName="setRailwayTrainVisible"
          />
          <RailwayFilter
            id="ctm-railway-tram"
            label="Tramway"
            swatch="tram"
            checked={tramVisible}
            count={tramCount}
            disabled={disabled}
            triggerName="setRailwayTramVisible"
          />
          <RailwayFilter
            id="ctm-railway-light-rail"
            label="Métro léger"
            swatch="light-rail"
            checked={lightRailVisible}
            count={lightRailCount}
            disabled={disabled}
            triggerName="setRailwayLightRailVisible"
          />
          <RailwayFilter
            id="ctm-railway-subway"
            label="Métro"
            swatch="subway"
            checked={subwayVisible}
            count={subwayCount}
            disabled={disabled}
            triggerName="setRailwaySubwayVisible"
          />
          <RailwayFilter
            id="ctm-railway-service"
            label="Voies de service"
            swatch="service"
            checked={serviceVisible}
            count={serviceCount}
            disabled={disabled}
            triggerName="setRailwayServiceVisible"
          />
          <RailwayFilter
            id="ctm-railway-tunnels"
            label="Tunnels"
            swatch="tunnel"
            checked={tunnelsVisible}
            count={tunnelCount}
            disabled={disabled}
            triggerName="setRailwayTunnelsVisible"
          />
        </div>

        <div className="railway-total" aria-label="Total des voies ferroviaires">
          <span>Total</span>
          <strong>{formatCount(totalCount)}</strong>
        </div>
      </div>

      <div className="railway-notice">
        Plan visuel uniquement — aucun placement automatique.
      </div>
    </Foldout>
  );
}
