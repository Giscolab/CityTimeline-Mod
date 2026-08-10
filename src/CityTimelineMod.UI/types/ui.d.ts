declare module "cs2/ui" {
  import type {
    ButtonHTMLAttributes,
    ComponentType,
    ReactNode
  } from "react";

  export interface ButtonProps
    extends Omit<ButtonHTMLAttributes<HTMLButtonElement>, "onSelect"> {
    variant?: string;
    src?: string;
    onSelect?: () => void;
    children?: ReactNode;
  }

  export const Button: ComponentType<ButtonProps>;

  export const Portal: ComponentType<{
    children?: ReactNode;
  }>;
}
