import React from "react";
import { ModRegistrar } from "cs2/modding";
import { CityTimelineHUDMount } from "./CityTimelineHUD";
import "./citytimeline-hud.css";

const register: ModRegistrar = (moduleRegistry) => {
  console.info("[CityTimelineMod UI] registering HUD on GameTopLeft");
  moduleRegistry.append("GameTopLeft", CityTimelineHUDMount);
};

export default register;
