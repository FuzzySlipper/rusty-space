/**
 * Pure build-time authoring helpers for the Rust-owned scene wire contract.
 * This package never runs in the product and has no evaluator or live state.
 */
export interface AuthoredCube {
  readonly label: string;
  readonly color: readonly [number, number, number, number];
  readonly scale: number;
}

export interface AuthoredScene {
  readonly schemaVersion: 1;
  readonly cube: AuthoredCube;
}

export function cube(label: string, color: AuthoredCube['color'], scale: number): AuthoredCube {
  return { label, color, scale };
}

export function scene(cubeDefinition: AuthoredCube): AuthoredScene {
  return { schemaVersion: 1, cube: cubeDefinition };
}

export const sampleScene = scene(cube('Rust-owned procedural cube', [0.16, 0.78, 1, 1], 1.5));

