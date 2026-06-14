import React, {
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
  SelectField,
  SliderField,
  ToggleField,
} from "./components/Controls";

import { ImportRoutesPane } from "./components/RoadCatalog";
import { ZoningPanel } from "./components/ZoningPanel";

type TabId = "p1" | "p2" | "p3" | "p4" | "p5";

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
    id: "p2",
    label: "Import routes CS2",
    count: "03",
  },
  {
    id: "p3",
    label: "Bundle & overlay",
    count: "04–06",
  },
  {
    id: "p4",
    label: "Affichage & rendu",
    count: "07–09",
  },
  {
    id: "p5",
    label: "Calage avancé",
    count: "10",
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

  const [activeBundle, setActiveBundle] =
    useState("Bundle exemple");

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
                <PrincipalPane />
              ) : null}

              {activeTab === "p2" ? (
                <ImportRoutesPane />
              ) : null}

              {activeTab === "p3" ? (
                <BundleOverlayPane
                  activeBundle={activeBundle}
                  setActiveBundle={
                    setActiveBundle
                  }
                />
              ) : null}

              {activeTab === "p4" ? (
                <DisplayRenderPane />
              ) : null}

              {activeTab === "p5" ? (
                <CalibrationPane />
              ) : null}
            </div>

            <Footer
              onClose={onClose}
            />
          </div>
        </div>
      </div>
    </div>
  );
}

function PrincipalPane() {
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
        state="always"
        note="Résumé utile. À garder en haut."
      >
        <Readout>
          Bundle exemple — Paris 7e
        </Readout>

        <Readout>
          routes 12 480 · chemins 3 102 ·
          total 15 582
        </Readout>

        <Readout>
          enabled · fast-flush · max 60 000 ·
          mode Toutes
        </Readout>

        <Readout>
          import 73% · 11 240 / 15 582
          segments
        </Readout>

        <Readout>
          phase idle · 0 chunks · idle
        </Readout>
      </Section>
    </div>
  );
}

interface BundleOverlayPaneProps {
  activeBundle: string;
  setActiveBundle: (
    value: string,
  ) => void;
}

function BundleOverlayPane({
  activeBundle,
  setActiveBundle,
}: BundleOverlayPaneProps) {
  return (
    <div className="ctm-pane">
      <Foldout
        num="04"
        title="Détails bundle / eau"
        state="foldout"
        note="Fonctionnel, mais doit rester secondaire."
        defaultOpen
      >
        <SelectField
          label="Ville / bundle actif"
          value={activeBundle}
          options={[
            "Bundle exemple",
            "Paris 7e",
            "Lyon centre",
            "Marseille",
          ]}
          onChange={setActiveBundle}
        />

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
          12 480 routes · 3 102 chemins ·
          41 ponts
        </Readout>

        <Readout>
          rivers 8 · lakes 3 · contracts OK
        </Readout>
      </Foldout>

      <Foldout
        num="05"
        title="État overlay / rebuild"
        state="foldout"
        note="Diagnostic utile, pas nécessaire dans le flux principal."
      >
        <Readout>
          Lisible · live
        </Readout>

        <Readout>
          idle · restart 0 · cancel 0
        </Readout>

        <HudButton disabled>
          Annuler rebuild
        </HudButton>
      </Foldout>

      <Foldout
        num="06"
        title="Statistiques routes GeoJSON"
        state="foldout"
        note="Utile pour valider le bundle."
      >
        <Readout>
          12 480 routes chargées
        </Readout>

        <Readout>
          3 102 chemins chargés
        </Readout>

        <Readout>
          15 582 total
        </Readout>

        <Readout>
          named 9 421 · oneway 882 ·
          lanes 7 233 · maxspeed 6 110
        </Readout>

        <Readout>
          ponts 41 · tunnels 12 ·
          ronds-points 87
        </Readout>
      </Foldout>
    </div>
  );
}

function DisplayRenderPane() {
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

      <Foldout
        num="08"
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
          draw 0.8 ms · upload 0.2 ms ·
          chunks 0
        </Readout>
      </Foldout>

      <Foldout
        num="09"
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
        num="10"
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
}: {
  onClose: () => void;
}) {
  return (
    <div className="footer">
      <div className="footer-head">
        <span className="num">
          11
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
          3 changements en attente
        </Readout>

        <Readout>
          live HUD · auto-save off
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