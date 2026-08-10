import type {
  ButtonHTMLAttributes,
  ReactNode,
} from "react";

interface PreviewButtonProps
  extends Omit<
    ButtonHTMLAttributes<HTMLButtonElement>,
    "onSelect"
  > {
  variant?: string;
  onSelect?: () => void;
  children?: ReactNode;
}

export function Button({
  variant,
  onSelect,
  onClick,
  children,
  ...buttonProps
}: PreviewButtonProps) {
  return (
    <button
      {...buttonProps}
      data-cs2-variant={variant}
      onClick={(event) => {
        onClick?.(event);
        onSelect?.();
      }}
    >
      {children}
    </button>
  );
}

export function Portal({
  children,
}: {
  children?: ReactNode;
}) {
  return <>{children}</>;
}