declare module "cs2/modding" {
  import type { ComponentType } from "react";

  export type AppendHookTargets =
    | "Menu"
    | "Editor"
    | "Game"
    | "GameTopLeft"
    | "GameTopRight"
    | "GameBottomRight"
    | "UniversalModMenu";

  export type ModuleRegistryAppend = ComponentType<{}> | (() => JSX.Element);

  export type ModuleRegistry = {
    append(target: AppendHookTargets, appendedComponent: ModuleRegistryAppend, index?: number): void;
  };

  export type ModRegistrar = (moduleRegistry: ModuleRegistry) => void;
}
