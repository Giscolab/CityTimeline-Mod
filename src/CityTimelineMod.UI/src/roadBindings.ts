import { bindValue, trigger } from "cs2/api";
import { BINDING_GROUP } from "./bindings";

export const roadMotorwayVisible$ = bindValue<boolean>(
  BINDING_GROUP,
  "roadMotorwayVisible",
  true,
);

export const roadTrunkVisible$ = bindValue<boolean>(
  BINDING_GROUP,
  "roadTrunkVisible",
  true,
);

export const roadPrimaryVisible$ = bindValue<boolean>(
  BINDING_GROUP,
  "roadPrimaryVisible",
  true,
);

export const roadSecondaryVisible$ = bindValue<boolean>(
  BINDING_GROUP,
  "roadSecondaryVisible",
  true,
);

export const roadTertiaryVisible$ = bindValue<boolean>(
  BINDING_GROUP,
  "roadTertiaryVisible",
  true,
);

export const roadMotorwayLinkVisible$ = bindValue<boolean>(
  BINDING_GROUP,
  "roadMotorwayLinkVisible",
  true,
);

export const roadTrunkLinkVisible$ = bindValue<boolean>(
  BINDING_GROUP,
  "roadTrunkLinkVisible",
  true,
);

export const roadPrimaryLinkVisible$ = bindValue<boolean>(
  BINDING_GROUP,
  "roadPrimaryLinkVisible",
  true,
);

export const roadSecondaryLinkVisible$ = bindValue<boolean>(
  BINDING_GROUP,
  "roadSecondaryLinkVisible",
  true,
);

export const roadTertiaryLinkVisible$ = bindValue<boolean>(
  BINDING_GROUP,
  "roadTertiaryLinkVisible",
  true,
);

export const roadHighwayFilter$ = bindValue<string>(
  BINDING_GROUP,
  "roadHighwayFilter",
  "all",
);

export function setRoadMotorwayVisible(visible: boolean) {
  trigger(BINDING_GROUP, "setRoadMotorwayVisible", visible);
}

export function setRoadTrunkVisible(visible: boolean) {
  trigger(BINDING_GROUP, "setRoadTrunkVisible", visible);
}

export function setRoadPrimaryVisible(visible: boolean) {
  trigger(BINDING_GROUP, "setRoadPrimaryVisible", visible);
}

export function setRoadSecondaryVisible(visible: boolean) {
  trigger(BINDING_GROUP, "setRoadSecondaryVisible", visible);
}

export function setRoadTertiaryVisible(visible: boolean) {
  trigger(BINDING_GROUP, "setRoadTertiaryVisible", visible);
}

export function setRoadMotorwayLinkVisible(visible: boolean) {
  trigger(BINDING_GROUP, "setRoadMotorwayLinkVisible", visible);
}

export function setRoadTrunkLinkVisible(visible: boolean) {
  trigger(BINDING_GROUP, "setRoadTrunkLinkVisible", visible);
}

export function setRoadPrimaryLinkVisible(visible: boolean) {
  trigger(BINDING_GROUP, "setRoadPrimaryLinkVisible", visible);
}

export function setRoadSecondaryLinkVisible(visible: boolean) {
  trigger(BINDING_GROUP, "setRoadSecondaryLinkVisible", visible);
}

export function setRoadTertiaryLinkVisible(visible: boolean) {
  trigger(BINDING_GROUP, "setRoadTertiaryLinkVisible", visible);
}

export function setAllRoadHighwaysVisible() {
  trigger(BINDING_GROUP, "setAllRoadHighwaysVisible");
}

export function clearAllRoadHighways() {
  trigger(BINDING_GROUP, "clearAllRoadHighways");
}