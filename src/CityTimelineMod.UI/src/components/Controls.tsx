import { ReactNode, useState } from "react";

type ButtonVariant = "default" | "primary" | "danger" | "diag";

function cx(...parts: Array<string | false | null | undefined>): string {
  return parts.filter(Boolean).join(" ");
}

export function Section(props: {
  num: string;
  title: string;
  state?: string;
  note?: string;
  noteTone?: "default" | "bug";
  children: ReactNode;
}) {
  return (
    <section className="card section_sop">
      <header className="ctm-section-header header_l0j first_l25">
        <div className="ctm-section-header-inner header_hMN">
          <span className="num">{props.num}</span>
          <div className="ctm-section-title title_mu8">{props.title}</div>
          {props.state ? <span className="state always">{props.state}</span> : null}
        </div>
      </header>
      {props.note ? <div className={cx("note", props.noteTone === "bug" && "bug")}>{props.note}</div> : null}
      <div className="items">{props.children}</div>
    </section>
  );
}

export function Foldout(props: {
  num: string;
  title: string;
  state?: string;
  note?: string;
  noteTone?: "default" | "bug";
  defaultOpen?: boolean;
  children: ReactNode;
}) {
  const [open, setOpen] = useState(Boolean(props.defaultOpen));

  return (
    <details
      className={cx("card foldout section_sop", open && "is-open")}
      open={open}
      onToggle={(event) => setOpen(event.currentTarget.open)}
    >
      <summary className="ctm-section-header header_l0j first_l25">
        <div className="ctm-section-header-inner header_hMN">
          <span className="ctm-foldout-arrow" aria-hidden="true" />
          <span className="num">{props.num}</span>
          <div className="ctm-section-title title_mu8">{props.title}</div>
          {props.state ? <span className="state">{props.state}</span> : null}
        </div>
      </summary>
      {props.note ? <div className={cx("note", props.noteTone === "bug" && "bug")}>{props.note}</div> : null}
      <div className="items">{props.children}</div>
    </details>
  );
}

export function GroupTitle({ children }: { children: ReactNode }) {
  return <div className="group-title">{children}</div>;
}

export function Readout({ children }: { children: ReactNode }) {
  return (
    <div className="readout field_amr field_cjf">
      <div className="label_VSW label_T__">{children}</div>
    </div>
  );
}

export function Row(props: { columns?: 2 | 3; children: ReactNode }) {
  return <div className={cx("row", props.columns === 2 && "cols-2", props.columns === 3 && "cols-3")}>{props.children}</div>;
}

export function HudButton(props: {
  children: ReactNode;
  variant?: ButtonVariant;
  disabled?: boolean;
  centered?: boolean;
  onClick?: () => void;
}) {
  const disabled = Boolean(props.disabled);
  return (
    <button
      type="button"
      className={cx("btn button_WWa", props.variant, props.centered && "is-centered", disabled && "is-disabled")}
      disabled={disabled}
      aria-disabled={disabled}
      onClick={disabled ? undefined : props.onClick}
    >
      {props.children}
    </button>
  );
}

export function ToggleField(props: {
  id: string;
  children: ReactNode;
  checked?: boolean;
  defaultChecked?: boolean;
  disabled?: boolean;
  type?: "checkbox" | "radio";
  name?: string;
  onChange?: (checked: boolean) => void;
}) {
  const [uncontrolledChecked, setUncontrolledChecked] = useState(
    Boolean(props.defaultChecked),
  );
  const isControlled = props.checked !== undefined;
  const checked = isControlled ? Boolean(props.checked) : uncontrolledChecked;
  const disabled = Boolean(props.disabled);
  return (
    <label
      htmlFor={props.id}
      className={cx("toggle field_amr field_cjf toggle-item_uwk", checked ? "checked" : "unchecked", disabled && "is-disabled")}
    >
      <input
        id={props.id}
        className="ctm-native-input"
        type={props.type || "checkbox"}
        name={props.name}
        checked={checked}
        disabled={disabled}
        onChange={(event) => {
          const nextChecked = event.currentTarget.checked;

          if (!isControlled) {
            setUncontrolledChecked(nextChecked);
          }

          props.onChange?.(nextChecked);
        }}
      />
      <span className="toggle-label label_VSW label_T__">{props.children}</span>
      <span className={cx("toggle_cca toggle_ATa item-mouse-states_Fmi", checked ? "checked" : "unchecked")} aria-hidden="true">
        <span className={cx("checkmark_NXV", checked ? "checked" : "unchecked")} />
      </span>
    </label>
  );
}

export function SliderField(props: {
  label: string;
  min: number;
  max: number;
  value?: number;
  defaultValue?: number;
  step?: number;
  disabled?: boolean;
  suffix?: string;
  onChange?: (value: number) => void;
}) {
  const [uncontrolledValue, setUncontrolledValue] = useState(
    props.defaultValue ?? props.min,
  );
  const isControlled = props.value !== undefined;
  const value = isControlled ? Number(props.value) : uncontrolledValue;

  return (
    <div className={cx("field field_amr field_cjf slider", props.disabled && "is-disabled")}>
      <div className="field-label label_VSW label_T__">
        {props.label} <span className="field-value">· {value}{props.suffix || ""}</span>
      </div>
      <div className="field-control">
        <input
          type="range"
          min={props.min}
          max={props.max}
          step={props.step}
          value={value}
          disabled={props.disabled}
          onChange={(event) => {
            const nextValue = Number(event.currentTarget.value);

            if (!isControlled) {
              setUncontrolledValue(nextValue);
            }

            props.onChange?.(nextValue);
          }}
        />
      </div>
    </div>
  );
}

export function SelectField(props: {
  label: string;
  value: string;
  options: Array<string | { value: string; label: string }>;
  onChange: (value: string) => void;
}) {
  // CoHTML does not expose the native HTMLSelectElement.options collection
  // expected by ReactDOM. A native <select>/<option> therefore crashes the
  // game UI renderer. Keep this control entirely button-based.
  const options = props.options.map((option) =>
    typeof option === "string"
      ? { value: option, label: option }
      : option,
  );

  const currentIndex = Math.max(
    0,
    options.findIndex((option) => option.value === props.value),
  );
  const current = options[currentIndex];
  const disabled = options.length === 0;

  const selectOffset = (offset: number) => {
    if (disabled) return;

    const nextIndex =
      (currentIndex + offset + options.length) % options.length;

    props.onChange(options[nextIndex].value);
  };

  return (
    <div className="field field_amr field_cjf selector">
      <div className="field-label label_VSW label_T__">{props.label}</div>
      <div className="field-control ctm-choice-control">
        <button
          type="button"
          className={cx(
            "ctm-choice-step previous button_WWa",
            disabled && "is-disabled",
          )}
          aria-label={`${props.label} : choix précédent`}
          disabled={disabled}
          onClick={() => selectOffset(-1)}
        >
          ‹
        </button>

        <button
          type="button"
          className={cx(
            "ctm-choice-value button_WWa",
            disabled && "is-disabled",
          )}
          aria-label={`${props.label} : choix suivant`}
          disabled={disabled}
          onClick={() => selectOffset(1)}
        >
          {current ? current.label : props.value || "—"}
        </button>

        <button
          type="button"
          className={cx(
            "ctm-choice-step next button_WWa",
            disabled && "is-disabled",
          )}
          aria-label={`${props.label} : choix suivant`}
          disabled={disabled}
          onClick={() => selectOffset(1)}
        >
          ›
        </button>
      </div>
    </div>
  );
}

export function TextField(props: {
  label: string;
  value: string;
  placeholder?: string;
  readOnly?: boolean;
  onChange?: (value: string) => void;
}) {
  return (
    <div className="field field_amr field_cjf">
      <div className="field-label label_VSW label_T__">{props.label}</div>
      <div className="field-control">
        <input
          type="text"
          value={props.value}
          placeholder={props.placeholder}
          readOnly={props.readOnly}
          onChange={(event) => props.onChange?.(event.currentTarget.value)}
        />
      </div>
    </div>
  );
}

export function NumberField(props: {
  label: string;
  value: number;
  min?: number;
  onChange: (value: number) => void;
}) {
  return (
    <div className="field field_amr field_cjf">
      <div className="field-label label_VSW label_T__">{props.label}</div>
      <div className="field-control">
        <input
          type="number"
          min={props.min}
          value={props.value}
          onChange={(event) => props.onChange(Number(event.currentTarget.value))}
        />
      </div>
    </div>
  );
}
