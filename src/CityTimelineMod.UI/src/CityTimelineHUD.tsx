import {
  Component,
  ErrorInfo,
  ReactNode,
  useState,
} from "react";

import { bindValue, trigger, useValue } from "cs2/api";
import { Button } from "cs2/ui";

import {
  Foldout,
  GroupTitle,
  HudButton,
  Readout,
  Row,
  Section,
  SliderField,
  ToggleField,
} from "./components/Controls";

import { RailwayPanel } from "./components/RailwayPanel";
import { ServicesPanel } from "./components/ServicesPanel";
import { StatisticsPanel } from "./components/StatisticsPanel";
import { ZoningPanel } from "./components/ZoningPanel";
import {
  formatCount,
  getMetric,
  useBundleStats,
} from "./components/bundleStats";
import type { BundleStatsSnapshot } from "./components/bundleStats";

type TabId = "p1" | "p3" | "stats" | "p4" | "p5";

const tabs: Array<{
  id: TabId;
  label: string;
  count: string;
}> = [
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

interface ErrorBoundaryProps {
  children: ReactNode;
}

interface ErrorBoundaryState {
  error: string | null;
}

class CityTimelineHudErrorBoundary extends Component<
  ErrorBoundaryProps,
  ErrorBoundaryState
> {
  state: ErrorBoundaryState = {
    error: null,
  };

  componentDidCatch(error: Error, info: ErrorInfo) {
    const message =
      error && error.stack
        ? error.stack
        : String(error);

    console.error(
      "[CityTimelineMod UI] React render error",
      message,
      info.componentStack,
    );

    this.setState({
      error: message,
    });
  }

  render() {
    if (this.state.error) {
      return (
        <div className="ctm-hud-host">
          <div className="ctm-hud-error">
            <strong>
              CityTimelineMod UI · erreur React
            </strong>

            <pre>
              {this.state.error}
            </pre>
          </div>
        </div>
      );
    }

    return this.props.children;
  }
}

export const cohtmlHudVisible$ = bindValue<boolean>(
  "CityTimelineMod",
  "cohtmlHudVisible",
  false,
);

export function CityTimelineHUDButton() {
  const visible = useValue(cohtmlHudVisible$);

  return (
    <Button
      variant="floating"
      className={`ctm-launcher ${visible ? "is-active" : ""}`}
      aria-label={visible ? "Fermer CityTimelineMod" : "Ouvrir CityTimelineMod"}
      aria-pressed={visible}
      onSelect={() => trigger("CityTimelineMod", "toggleCohtmlHud")}
    >
      CTM
    </Button>
  );
}

export function CityTimelineHUDHost() {
  const visible = useValue(cohtmlHudVisible$);

  console.info(
    "[CityTimelineMod UI] CityTimelineHUDHost rendering; visible=",
    visible,
  );

  if (!visible) {
    return null;
  }

  // Find It renders its main container directly from moduleRegistry.append("Game", ...).
  // No Portal: the gameplay root owns the component lifecycle and layout.
  return (
    <CityTimelineHudErrorBoundary>
      <CityTimelineHUD
        onClose={() => trigger("CityTimelineMod", "closeCohtmlHud")}
      />
    </CityTimelineHudErrorBoundary>
  );
}

function CityTimelineHUD({
  onClose,
}: {
  onClose: () => void;
}) {
  console.info(
    "[CityTimelineMod UI] CityTimelineHUD rendering",
  );

  const [activeTab, setActiveTab] =
    useState<TabId>("p1");
  const stats = useBundleStats();

  return (
    <div className="ctm-hud-host">
      <div className="ctm-hud ctm-game-ui hookui-loaded">
        <div className="window">
          <div className="window-title header_H_U header_Bpo child-opacity-transition_nkS">
            <div className="title-bar_PF4 ctm-title-bar">
              <div
                className="icon-space_h_f ctm-title-icon"
                aria-hidden="true"
              />

              <div className="title_SVH title_zQN ctm-title-text">
                CityTimelineMod — Panneau HUD
              </div>

              <span className="hint">
                DEV
              </span>

              <button
                type="button"
                className="ctm-close button_bvQ close-button_wKK"
                aria-label="Fermer le HUD"
                onClick={onClose}
              >
                <span
                  className="ctm-close-icon"
                  aria-hidden="true"
                >
                  ×
                </span>
              </button>
            </div>
          </div>

          <div className="window-content content_XD5 content_AD7 child-opacity-transition_nkS">
            <div
              className="tabs"
              role="tablist"
              aria-label="Sections du HUD"
            >
              {tabs.map((tab) => (
                <button
                  key={tab.id}
                  className="tab button_WWa"
                  role="tab"
                  aria-selected={
                    activeTab === tab.id
                  }
                  type="button"
                  onClick={() =>
                    setActiveTab(tab.id)
                  }
                >
                  {tab.label}

                  <span className="count">
                    {tab.count}
                  </span>
                </button>
              ))}
            </div>

            <div className="scroll">
              {activeTab === "p1" ? (
                <PrincipalPane stats={stats} />
              ) : null}

              {activeTab === "p3" ? (
                <BundleOverlayPane stats={stats} />
              ) : null}

              {activeTab === "stats" ? (
                <StatisticsPanel stats={stats} />
              ) : null}

              {activeTab === "p4" ? (
                <DisplayRenderPane stats={stats} />
              ) : null}

              {activeTab === "p5" ? (
                <CalibrationPane />
              ) : null}
            </div>

            <Footer
              onClose={onClose}
              stats={stats}
            />
          </div>
        </div>
      </div>
    </div>
  );
}

function PrincipalPane({ stats }: { stats: BundleStatsSnapshot }) {
  const bundleLabel = stats.bundleName || stats.bundleId || "Bundle actif";
  const roads = getMetric(stats.roads, "total", "roads", "roadTotal", "road_total");
  const paths = getMetric(stats.roads, "paths", "pathways", "pathway");
  const servicePoints = stats.services.some((family) => family.total !== null)
    ? stats.services.reduce((total, family) => total + (family.total ?? 0), 0)
    : null;
  const railwayTotal = getMetric(stats.railway, "total");

  return (
    <div className="ctm-pane">
      <Section
        num="01"
        title="Presets affichage"
        state="always"
        note="Actions directes du mockup. Désactivées à cette étape."
      >
        <Row columns={3}>
          <HudButton
            variant="primary"
            disabled
          >
            Debug
          </HudButton>

          <HudButton
            variant="primary"
            disabled
          >
            Lisible
          </HudButton>

          <HudButton
            variant="primary"
            disabled
          >
            Routes
          </HudButton>

          <HudButton
            variant="primary"
            disabled
          >
            Eau
          </HudButton>

          <HudButton
            variant="primary"
            disabled
          >
            Zoning
          </HudButton>

          <HudButton
            variant="primary"
            disabled
          >
            Bounds
          </HudButton>
        </Row>
      </Section>

      <Section
        num="02"
        title="Résumé"
        state={stats.available ? "connecté" : "indisponible"}
        note={stats.status}
      >
        <Readout>
          {bundleLabel}{stats.bundleId && stats.bundleName ? ` · ${stats.bundleId}` : ""}
        </Readout>

        <Readout>
          objets OSM {formatCount(stats.objects)} · routes {formatCount(roads)} · chemins {formatCount(paths)}
        </Readout>

        <Readout>
          services {formatCount(servicePoints)} · réseau ferroviaire {formatCount(railwayTotal)} voies
        </Readout>
      </Section>
    </div>
  );
}

function BundleOverlayPane({ stats }: { stats: BundleStatsSnapshot }) {
  const bundleLabel = stats.bundleName || stats.bundleId || "Bundle actif";
  const roads = getMetric(stats.roads, "total", "roads", "roadTotal", "road_total");
  const paths = getMetric(stats.roads, "paths", "pathways", "pathway");
  const waterLines = getMetric(stats.water, "lines", "lineCount", "line_count");
  const waterAreas = getMetric(stats.water, "areas", "areaCount", "area_count");

  return (
    <div className="ctm-pane">
      <Foldout
        num="04"
        title="Détails bundle / eau"
        state={stats.available ? "connecté" : "indisponible"}
        note={stats.status}
        defaultOpen
      >
        <Readout>
          {bundleLabel}{stats.bundleId && stats.bundleName ? ` · ${stats.bundleId}` : ""}
        </Readout>

        <Row columns={2}>
          <HudButton disabled>
            Précédent
          </HudButton>

          <HudButton disabled>
            Suivant
          </HudButton>

          <HudButton disabled>
            Charger bundle sélectionné
          </HudButton>

          <HudButton disabled>
            Recharger catalogue
          </HudButton>
        </Row>

        <Readout>
          {formatCount(roads)} routes · {formatCount(paths)} chemins
        </Readout>

        <Readout>
          {formatCount(waterLines)} lignes d’eau · {formatCount(waterAreas)} surfaces d’eau
        </Readout>

        <Readout>
          {formatCount(stats.objects)} objets OSM exploitables
        </Readout>
      </Foldout>

      <Foldout
        num="05"
        title="État overlay / rebuild"
        state="non exposé"
        note="Ce contrat de statistiques ne fournit pas encore la télémétrie du rebuild."
      >
        <Readout>
          Aucune métrique de rebuild reçue. Aucun zéro de démonstration n’est affiché.
        </Readout>

        <HudButton disabled>
          Annuler rebuild
        </HudButton>
      </Foldout>

      <Foldout
        num="06"
        title="Statistiques routes GeoJSON"
        state={stats.available ? "connecté" : "indisponible"}
        note="Utile pour valider le bundle."
      >
        <Readout>
          {formatCount(roads)} routes chargées
        </Readout>

        <Readout>
          {formatCount(paths)} chemins chargés
        </Readout>

        <Readout>
          autoroutes {formatCount(getMetric(stats.roads, "highway", "motorway"))} · axes principaux {formatCount(getMetric(stats.roads, "main", "mainRoad", "largeRoad", "large_road", "primary"))}
        </Readout>

        <Readout>
          secondaires {formatCount(getMetric(stats.roads, "secondary", "secondaryRoad", "mediumRoad", "medium_road"))} · tertiaires/résidentielles {formatCount(getMetric(stats.roads, "tertiary", "tertiaryResidential", "tertiary_residential", "smallRoad", "small_road"))}
        </Readout>

        <Readout>
          bretelles {formatCount(getMetric(stats.roads, "ramp", "link", "rampLink", "ramp_link"))} · non classées {formatCount(getMetric(stats.roads, "unclassified", "unclassifiedRoad", "gravelRoad", "gravel_road", "other"))}
        </Readout>
      </Foldout>
    </div>
  );
}

function DisplayRenderPane({ stats }: { stats: BundleStatsSnapshot }) {
  return (
    <div className="ctm-pane">
      <Foldout
        num="07"
        title="Couches visibles / transparence"
        state="foldout open by default"
        note="Bug d'origine corrigé : chaque foldout est indépendant."
        noteTone="bug"
        defaultOpen
      >
        <GroupTitle>
          Couches
        </GroupTitle>

        <Row columns={2}>
          <ToggleField
            id="ctm-i11"
            defaultChecked
          >
            Zonage
          </ToggleField>

          <ToggleField
            id="ctm-i12"
            defaultChecked
          >
            Routes
          </ToggleField>

          <ToggleField
            id="ctm-i13"
            defaultChecked
          >
            Chemins
          </ToggleField>

          <ToggleField
            id="ctm-i14"
            defaultChecked
          >
            Eau
          </ToggleField>

          <ToggleField id="ctm-i15">
            Bounds
          </ToggleField>

          <ToggleField id="ctm-i16">
            Debug labels
          </ToggleField>
        </Row>

        <GroupTitle>
          Transparence
        </GroupTitle>

        <SliderField
          label="Alpha zoning"
          min={0}
          max={100}
          defaultValue={65}
          suffix="%"
        />

        <SliderField
          label="Alpha routes"
          min={0}
          max={100}
          defaultValue={88}
          suffix="%"
        />

        <SliderField
          label="Alpha eau"
          min={0}
          max={100}
          defaultValue={70}
          suffix="%"
        />

        <GroupTitle>
          Zoning
        </GroupTitle>

        <ZoningPanel />
      </Foldout>

      <RailwayPanel />

      <ServicesPanel services={stats.services} />

      <Foldout
        num="10"
        title="Performance overlay"
        state="foldout"
        note="À ouvrir seulement pour vérifier le coût du rendu."
      >
        <Row columns={2}>
          <ToggleField id="ctm-i17">
            Limiter chunks par frame
          </ToggleField>

          <ToggleField id="ctm-i18">
            Dessiner bounds chunks
          </ToggleField>

          <ToggleField id="ctm-i19">
            Mode basse charge
          </ToggleField>

          <ToggleField id="ctm-i20">
            Mesure temps overlay
          </ToggleField>
        </Row>

        <SliderField
          label="Max chunks visibles"
          min={0}
          max={256}
          defaultValue={64}
        />

        <SliderField
          label="Budget ms overlay"
          min={1}
          max={32}
          defaultValue={8}
        />

        <Readout>
          Télémétrie de performance non exposée par bundleStatsJson.
        </Readout>
      </Foldout>

      <Foldout
        num="11"
        title="Rendu avancé routes / filtres"
        state="foldout"
        note="Filtres visuels locaux pour préparer la cartographie des bindings."
      >
        <GroupTitle>
          Styles routes
        </GroupTitle>

        <Row columns={2}>
          <ToggleField
            id="ctm-i21"
            defaultChecked
          >
            Afficher routes nommées
          </ToggleField>

          <ToggleField
            id="ctm-i22"
            defaultChecked
          >
            Afficher routes sans nom
          </ToggleField>

          <ToggleField id="ctm-i23">
            Routes oneway uniquement
          </ToggleField>

          <ToggleField id="ctm-i24">
            Ponts uniquement
          </ToggleField>

          <ToggleField id="ctm-i25">
            Tunnels uniquement
          </ToggleField>

          <ToggleField id="ctm-i26">
            Highlight maxspeed
          </ToggleField>
        </Row>

        <SliderField
          label="Largeur lignes routes"
          min={1}
          max={20}
          defaultValue={6}
        />

        <SliderField
          label="Seuil maxspeed"
          min={0}
          max={240}
          defaultValue={90}
        />
      </Foldout>
    </div>
  );
}

function CalibrationPane() {
  const [unlocked, setUnlocked] =
    useState(false);

  return (
    <div className="ctm-pane">
      <Foldout
        num="12"
        title="Calage avancé"
        state="foldout · verrouillé"
        note="Doit rester verrouillé par défaut."
      >
        <ToggleField
          id="ctm-i27"
          defaultChecked={unlocked}
          onChange={setUnlocked}
        >
          Déverrouiller le calage
        </ToggleField>

        <div
          className={
            `calage-body ${
              unlocked
                ? "is-unlocked"
                : "is-locked"
            }`
          }
        >
          <SliderField
            label="Offset X"
            min={-1000}
            max={1000}
            defaultValue={0}
            disabled={!unlocked}
          />

          <SliderField
            label="Offset Z"
            min={-1000}
            max={1000}
            defaultValue={0}
            disabled={!unlocked}
          />

          <SliderField
            label="Rotation degrés"
            min={-180}
            max={180}
            defaultValue={0}
            disabled={!unlocked}
          />

          <SliderField
            label="Scale X"
            min={1}
            max={300}
            defaultValue={100}
            disabled={!unlocked}
          />

          <SliderField
            label="Scale Z"
            min={1}
            max={300}
            defaultValue={100}
            disabled={!unlocked}
          />

          <Readout>
            worldScale lecture seule
          </Readout>
        </div>
      </Foldout>
    </div>
  );
}

function Footer({
  onClose,
  stats,
}: {
  onClose: () => void;
  stats: BundleStatsSnapshot;
}) {
  const bundleLabel = stats.bundleName || stats.bundleId || "Bundle actif";

  return (
    <div className="footer">
      <div className="footer-head">
        <span className="num">
          13
        </span>

        <h2>
          Actions finales
        </h2>

        <span className="state always">
          always bottom · épinglé
        </span>
      </div>

      <div className="meta">
        <Readout>
          {stats.available ? `${bundleLabel} · données connectées` : stats.status}
        </Readout>

        <Readout>
          HUD live · statistiques synchronisées
        </Readout>
      </div>

      <div className="actions">
        <HudButton
          variant="primary"
          disabled
          centered
        >
          Appliquer / reconstruire
        </HudButton>

        <HudButton
          disabled
          centered
        >
          Sauvegarder visuels
        </HudButton>

        <HudButton
          disabled
          centered
        >
          Recharger visuels
        </HudButton>

        <HudButton
          centered
          onClick={onClose}
        >
          Fermer
        </HudButton>
      </div>
    </div>
  );
}
