/**
 * Ship handling definitions: the authored tuning values that constitute the
 * ship's flight feel. The meaning and validation of every field is owned by
 * the Rust compiler in `crates/product-gameplay`; this file only composes them.
 */

export interface ShipHandlingDefinition {
  readonly maxSpeed: number;
  readonly maxThrust: number;
  readonly maxTurnRate: number;
  readonly throttleResponseTime: number;
  readonly steeringResponseTime: number;
}

export function shipHandling(
  values: ShipHandlingDefinition,
): ShipHandlingDefinition {
  for (const [field, value] of Object.entries(values)) {
    if (typeof value !== 'number' || !Number.isFinite(value) || value <= 0) {
      throw new Error(
        `shipHandling ${field} must be a finite positive number, got ${String(value)}`,
      );
    }
  }
  return { ...values };
}
