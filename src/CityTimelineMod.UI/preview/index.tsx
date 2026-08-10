import { StrictMode } from "react";
import { createRoot } from "react-dom/client";

import {
  CityTimelineHUDButton,
  CityTimelineHUDHost,
} from "../src/CityTimelineHUD";

import "../src/citytimeline-hud.css";
import "./preview.css";

function resolvePreviewRoot(): HTMLElement {
  const root = document.getElementById("ctm-preview-root");

  if (!(root instanceof HTMLElement)) {
    throw new Error(
      "CityTimelineMod preview root #ctm-preview-root was not found.",
    );
  }

  return root;
}

function CityTimelinePreview() {
  return (
    <div className="ctm-preview-environment">
      <CityTimelineHUDButton />
      <CityTimelineHUDHost />
    </div>
  );
}

createRoot(resolvePreviewRoot()).render(
  <StrictMode>
    <CityTimelinePreview />
  </StrictMode>,
);