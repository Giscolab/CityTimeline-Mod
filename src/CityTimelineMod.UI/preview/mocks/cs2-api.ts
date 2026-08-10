import { useSyncExternalStore } from "react";

export interface ValueBinding<T> {
  readonly group: string;
  readonly name: string;
  readonly value: T;
}

interface MutableBinding<T> {
  readonly group: string;
  readonly name: string;
  value: T;
}

type Listener = () => void;

const bindings = new Map<
  string,
  MutableBinding<unknown>
>();

const listeners = new Set<Listener>();

const previewInitialValues = new Map<
  string,
  unknown
>([
  ["runtimeAvailable", true],
  ["cohtmlHudVisible", true],
]);

function bindingKey(
  group: string,
  name: string,
): string {
  return `${group}:${name}`;
}

function emitChange(): void {
  for (const listener of listeners) {
    listener();
  }
}

function subscribe(
  listener: Listener,
): () => void {
  listeners.add(listener);

  return () => {
    listeners.delete(listener);
  };
}

function getBindingByName(
  name: string,
): MutableBinding<unknown> | undefined {
  for (const binding of bindings.values()) {
    if (binding.name === name) {
      return binding;
    }
  }

  return undefined;
}

function setBindingByName(
  name: string,
  value: unknown,
): boolean {
  const binding = getBindingByName(name);

  if (!binding) {
    return false;
  }

  binding.value = value;
  emitChange();

  return true;
}

function lowerFirst(
  value: string,
): string {
  if (value.length === 0) {
    return value;
  }

  return value[0].toLowerCase() + value.slice(1);
}

export function bindValue<T>(
  group: string,
  name: string,
  defaultValue: T,
): ValueBinding<T> {
  const key = bindingKey(group, name);

  const existing = bindings.get(key);

  if (existing) {
    return existing as MutableBinding<T>;
  }

  const initialValue =
    previewInitialValues.has(name)
      ? previewInitialValues.get(name) as T
      : defaultValue;

  const binding: MutableBinding<T> = {
    group,
    name,
    value: initialValue,
  };

  bindings.set(key, binding);

  return binding;
}

export function useValue<T>(
  binding: ValueBinding<T>,
): T {
  return useSyncExternalStore(
    subscribe,
    () => binding.value,
    () => binding.value,
  );
}

export function trigger(
  group: string,
  name: string,
  ...args: unknown[]
): void {
  console.info(
    `[CTM preview] ${group}.${name}`,
    ...args,
  );

  switch (name) {
    case "toggleCohtmlHud": {
      const binding =
        getBindingByName("cohtmlHudVisible");

      if (binding) {
        binding.value = !Boolean(binding.value);
        emitChange();
      }

      return;
    }

    case "openCohtmlHud":
      setBindingByName(
        "cohtmlHudVisible",
        true,
      );
      return;

    case "closeCohtmlHud":
      setBindingByName(
        "cohtmlHudVisible",
        false,
      );
      return;
  }

  /*
   * Generic preview adapter:
   *
   * setFooBar(value)
   *       ↓
   * fooBar binding
   *
   * This intentionally models the common CTM
   * ValueBinding/Trigger pair without embedding
   * game-domain behavior in the browser preview.
   */
  if (
    name.startsWith("set") &&
    name.length > 3 &&
    args.length > 0
  ) {
    const bindingName = lowerFirst(
      name.slice(3),
    );

    setBindingByName(
      bindingName,
      args[0],
    );
  }
}