declare module "cs2/api" {
  export interface ValueBinding<T> {
    readonly value?: T;
  }

  export function bindValue<T>(
    group: string,
    name: string,
    defaultValue: T,
  ): ValueBinding<T>;

  export function useValue<T>(binding: ValueBinding<T>): T;

  export function trigger(
    group: string,
    name: string,
    ...args: unknown[]
  ): void;
}
