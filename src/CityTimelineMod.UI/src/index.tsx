import { ModRegistrar } from "cs2/modding";
import {
  CityTimelineHUDButton,
  CityTimelineHUDHost,
} from "./CityTimelineHUD";
import "./citytimeline-hud.css";

const register: ModRegistrar = (moduleRegistry) => {
  console.info("[CityTimelineMod UI] registering CoHTML UI");

  // Trigger only.
  moduleRegistry.append("GameTopLeft", CityTimelineHUDButton);

  // Find It architecture: render the panel directly in the gameplay root.
  moduleRegistry.append("Game", CityTimelineHUDHost);
};

export default register;
