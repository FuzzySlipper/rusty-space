/**
 * The stock ship's handling values. First-cut Asteroids-style tuning; adjust
 * here (not in Rust) when tuning the feel. See
 * docs/plans/ship-controls-prototype.md for the starting-value rationale.
 */

import { shipHandling } from '../authoring/mod.js';

export const stockShipHandling = shipHandling({
  maxSpeed: 12,
  maxThrust: 18,
  maxTurnRate: 3,
  throttleResponseTime: 0.08,
  steeringResponseTime: 0.12,
});
