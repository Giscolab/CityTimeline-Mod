import React, { useMemo, useState } from "react";
import {
  getRoadDirection,
  getRoadFlags,
  getRoadLaneLabel,
  getRoadTraits,
  RoadFamilyId,
  RoadPrefab,
  roadPrefabFamilies,
  roadPrefabTotal,
  roadPrefabs,
} from "../data/roadPrefabs";
import { Foldout, GroupTitle, HudButton, NumberField, Readout, Row, Section, SelectField, TextField, ToggleField } from "./Controls";

type FamilyFilter = RoadFamilyId | "all" | null;

function sortRoads(roads: RoadPrefab[], sort: string): RoadPrefab[] {
  const next = roads.slice();
  if (sort === "name") return next.sort((a, b) => a.name.localeCompare(b.name));
  if (sort === "speed") return next.sort((a, b) => parseInt(b.speed, 10) - parseInt(a.speed, 10));
  return next.sort((a, b) => a.index - b.index);
}

export function ImportRoutesPane() {
  const [source, setSource] = useState("roads");
  const [highway, setHighway] = useState("primary,secondary,tertiary,residential");
  const [onewayOnly, setOnewayOnly] = useState(false);
  const [bridgeOnly, setBridgeOnly] = useState(false);
  const [tunnelOnly, setTunnelOnly] = useState(false);
  const [lanesMin, setLanesMin] = useState(0);
  const [maxSegments, setMaxSegments] = useState(0);
  const [mode, setMode] = useState("single");
  const [family, setFamily] = useState<FamilyFilter>(null);
  const [search, setSearch] = useState("");
  const [sort, setSort] = useState("default");
  const [selectedRoad, setSelectedRoad] = useState<RoadPrefab | null>(null);

  const visibleRoads = useMemo(() => {
    const query = search.trim().toLowerCase();
    if (!family && !query) return [];

    const filtered = roadPrefabs.filter((prefab) => {
      const familyMatches = family === "all" || family === null || prefab.family === family;
      const queryMatches = !query || prefab.name.toLowerCase().includes(query);
      return familyMatches && queryMatches;
    });

    return sortRoads(filtered, sort);
  }, [family, search, sort]);

  const effectiveRule = [
    `source=${source}`,
    `highway=${highway}`,
    `onewayOnly=${onewayOnly}`,
    `bridgeOnly=${bridgeOnly}`,
    `tunnelOnly=${tunnelOnly}`,
    `lanesMin=${lanesMin}`,
    `maxSegments=${maxSegments}`,
    `mode=${mode}`,
    `targetPrefabName=${selectedRoad ? selectedRoad.name : "(aucun)"}`,
  ].join("\n");

  return (
    <>
      <div className="import-layout">
        <aside className="import-column">
          <Section num="1" title="Source GeoJSON / OSM" note="Ces filtres décident quels segments du bundle sont éligibles.">
            <SelectField label="source" value={source} options={["roads", "paths", "all"]} onChange={setSource} />
            <TextField label="highway" value={highway} onChange={setHighway} />
            <ToggleField id="ctm-i1" defaultChecked={onewayOnly} onChange={setOnewayOnly}>
              oneway=true uniquement
            </ToggleField>
            <ToggleField id="ctm-i2" defaultChecked={bridgeOnly} onChange={setBridgeOnly}>
              bridge=true uniquement
            </ToggleField>
            <ToggleField id="ctm-i3" defaultChecked={tunnelOnly} onChange={setTunnelOnly}>
              tunnel=true uniquement
            </ToggleField>
            <NumberField label="lanes min" value={lanesMin} min={0} onChange={setLanesMin} />
            <NumberField label="max segments" value={maxSegments} min={0} onChange={setMaxSegments} />
          </Section>

          <Section num="2" title="Familles RoadPrefab" note="Filtre visuel. Ne remplace pas le PrefabName exact.">
            <div className="cat-grid">
              <button type="button" className={`cat-btn button_WWa ${family === "all" ? "active" : ""}`} onClick={() => setFamily("all")}>
                Tous <span>{roadPrefabTotal}</span>
              </button>
              {roadPrefabFamilies.map((item) => (
                <button
                  type="button"
                  key={item.id}
                  className={`cat-btn button_WWa ${family === item.id ? "active" : ""}`}
                  onClick={() => setFamily(item.id)}
                >
                  {item.label} <span>{item.prefabs.length}</span>
                </button>
              ))}
            </div>
          </Section>

          <Section num="3" title="Mode import">
            <label className={`toggle field_amr field_cjf toggle-item_uwk ${mode === "single" ? "checked" : "unchecked"}`}>
              <input className="ctm-native-input" type="radio" name="mode" checked={mode === "single"} onChange={() => setMode("single")} />
              <span className="toggle-label label_VSW label_T__">Importer vers un seul RoadPrefab cible</span>
              <span className={`toggle_cca toggle_ATa item-mouse-states_Fmi ${mode === "single" ? "checked" : "unchecked"}`} aria-hidden="true">
                <span className={`checkmark_NXV ${mode === "single" ? "checked" : "unchecked"}`} />
              </span>
            </label>
            <label className={`toggle field_amr field_cjf toggle-item_uwk ${mode === "auto" ? "checked" : "unchecked"}`}>
              <input className="ctm-native-input" type="radio" name="mode" checked={mode === "auto"} onChange={() => setMode("auto")} />
              <span className="toggle-label label_VSW label_T__">Mapping automatique OSM → RoadPrefab</span>
              <span className={`toggle_cca toggle_ATa item-mouse-states_Fmi ${mode === "auto" ? "checked" : "unchecked"}`} aria-hidden="true">
                <span className={`checkmark_NXV ${mode === "auto" ? "checked" : "unchecked"}`} />
              </span>
            </label>
            <label className={`toggle field_amr field_cjf toggle-item_uwk ${mode === "selected" ? "checked" : "unchecked"}`}>
              <input className="ctm-native-input" type="radio" name="mode" checked={mode === "selected"} onChange={() => setMode("selected")} />
              <span className="toggle-label label_VSW label_T__">Mapping manuel par groupe sélectionné</span>
              <span className={`toggle_cca toggle_ATa item-mouse-states_Fmi ${mode === "selected" ? "checked" : "unchecked"}`} aria-hidden="true">
                <span className={`checkmark_NXV ${mode === "selected" ? "checked" : "unchecked"}`} />
              </span>
            </label>
          </Section>
        </aside>

        <section className="road-catalog">
          <div className="road-toolbar">
            <TextField label="Rechercher" value={search} placeholder="Small, Highway, Quay, Bridge..." onChange={setSearch} />
            <SelectField
              label="Tri"
              value={sort}
              options={[
                { value: "default", label: "Catégorie / vitesse / UI" },
                { value: "name", label: "Nom" },
                { value: "speed", label: "Vitesse" },
              ]}
              onChange={setSort}
            />
          </div>

          {!family && search.trim() === "" ? (
            <Readout>Sélectionner une famille RoadPrefab pour afficher ses routes. Les 92 prefabs ne sont pas listés par défaut.</Readout>
          ) : null}

          <div className="road-list">
            {visibleRoads.map((prefab) => (
              <RoadCard
                key={`${prefab.index}-${prefab.name}`}
                prefab={prefab}
                selected={selectedRoad?.name === prefab.name}
                onSelect={() => setSelectedRoad(prefab)}
              />
            ))}
            {(family || search.trim() !== "") && visibleRoads.length === 0 ? <Readout>Aucun RoadPrefab ne correspond à ces filtres.</Readout> : null}
          </div>
        </section>

        <aside className="preview section_sop">
          <header className="ctm-section-header header_l0j first_l25">
            <div className="ctm-section-header-inner header_hMN">
              <span className="num">4</span>
              <div className="ctm-section-title title_mu8">Règle effective</div>
            </div>
          </header>
          <div className="preview-body">
            <Readout>Le bouton Importer lit exactement ces champs. Rien d’autre.</Readout>
            <TextField label="RoadPrefab cible" value={selectedRoad ? selectedRoad.name : "(aucun)"} readOnly />
            <pre className="rule">{effectiveRule}</pre>
            <HudButton variant="danger" disabled>Importer uniquement cette sélection</HudButton>
            <HudButton variant="diag" disabled>Prévisualiser segments éligibles</HudButton>
            <HudButton variant="diag" disabled>Diagnostic mapping sélection</HudButton>
          </div>
        </aside>
      </div>

      <Foldout num="03b" title="Options runtime & diagnostics" state="avancé" note="Réglages globaux du pipeline d'import et outils de diag.">
        <GroupTitle>État</GroupTitle>
        <Readout>cache OK · 15 582 segments</Readout>
        <Readout>runtime ON · runOnce false · pipeline fast-flush</Readout>
        <GroupTitle>Options</GroupTitle>
        <Row columns={2}>
          <ToggleField id="ctm-i7">Autoriser import runtime</ToggleField>
          <ToggleField id="ctm-i8">Refuser un second import identique</ToggleField>
          <ToggleField id="ctm-i9">Afficher progression runtime HUD</ToggleField>
          <ToggleField id="ctm-i10">Logs sélection import verbose</ToggleField>
        </Row>
        <SliderFieldLocal label="Max segments import" defaultValue={60000} max={200000} />
        <SliderFieldLocal label="Batch import runtime" defaultValue={1000} max={5000} />
        <GroupTitle>Pipeline</GroupTitle>
        <Row columns={2}>
          <HudButton disabled>Pipeline fast-flush</HudButton>
          <HudButton disabled>Pipeline batch-safe</HudButton>
        </Row>
        <GroupTitle>Exécution globale</GroupTitle>
        <Row columns={2}>
          <HudButton variant="danger" disabled>Importer routes runtime (tout)</HudButton>
          <HudButton disabled>Undo import routes runtime</HudButton>
        </Row>
        <GroupTitle>Diagnostics</GroupTitle>
        <Row columns={2}>
          <HudButton variant="diag" disabled>Diagnostic contenu GeoJSON routes</HudButton>
          <HudButton variant="diag" disabled>Diagnostic prefabs vanilla routes</HudButton>
          <HudButton variant="diag" disabled>Diagnostic noms exacts prefabs routes</HudButton>
          <HudButton variant="diag" disabled>Diagnostic mapping import routes</HudButton>
          <HudButton variant="diag" disabled>Diagnostic routes importées</HudButton>
          <HudButton variant="diag" disabled>Diagnostic routes vanilla placées</HudButton>
        </Row>
      </Foldout>

      <Foldout num="03c" title="Résumé 92 prefabs" state="stats">
        <Section num="∑" title="Résumé classé">
          <table>
            <tbody>
              {roadPrefabFamilies.map((item) => (
                <tr key={item.id}>
                  <td>{item.label}</td>
                  <td className="count-cell">{item.prefabs.length}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </Section>
      </Foldout>
    </>
  );
}

function SliderFieldLocal(props: { label: string; defaultValue: number; max: number }) {
  const [value, setValue] = useState(props.defaultValue);
  return (
    <div className="field field_amr field_cjf slider">
      <div className="field-label label_VSW label_T__">
        {props.label} <span className="field-value">· {value}</span>
      </div>
      <div className="field-control">
        <input type="range" min={0} max={props.max} value={value} onChange={(event) => setValue(Number(event.currentTarget.value))} />
      </div>
    </div>
  );
}

function RoadCard(props: { prefab: RoadPrefab; selected: boolean; onSelect: () => void }) {
  const traits = getRoadTraits(props.prefab);
  return (
    <article className={`road-card ${props.selected ? "selected" : ""}`}>
      <label className="select-line field_cjf">
        <input className="ctm-native-input" type="radio" name="roadPrefab" checked={props.selected} onChange={props.onSelect} />
        <span className="editor-family">{props.prefab.family}</span>
        <span className={`road-choice toggle_cca toggle_ATa item-mouse-states_Fmi ${props.selected ? "checked" : "unchecked"}`} aria-hidden="true">
          <span className={`checkmark_NXV ${props.selected ? "checked" : "unchecked"}`} />
        </span>
      </label>
      <h3>{props.prefab.name}</h3>
      <div className="meta-grid">
        <div>
          <b>Direction</b>
          <span>{getRoadDirection(props.prefab.name)}</span>
        </div>
        <div>
          <b>Voies nom</b>
          <span>{getRoadLaneLabel(props.prefab.name)}</span>
        </div>
        <div>
          <b>Vitesse</b>
          <span>{props.prefab.speed}</span>
        </div>
        <div>
          <b>Priorité UI</b>
          <span>{props.prefab.index}</span>
        </div>
      </div>
      <div className="flags">{getRoadFlags(props.prefab)}</div>
      <div className="tags">
        {traits.map((trait) => (
          <span className="tag" key={trait}>
            {trait}
          </span>
        ))}
      </div>
      <details>
        <summary>Informations complémentaires</summary>
        <div className="sections">Nom interne exact : {props.prefab.name} · famille : {props.prefab.family} · vitesse : {props.prefab.speed}</div>
      </details>
    </article>
  );
}
