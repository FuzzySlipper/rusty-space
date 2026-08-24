/** Stock ship tuning; change it here so the admitted package stays reproducible. */

import { shipHandling } from '../authoring/mod.js';

export const stockShipHandling = shipHandling({
  maxSpeed: 12,
  maxThrust: 18,
  maxTurnRate: 3,
  throttleResponseTime: 0.08,
  steeringResponseTime: 0.12,
  fieldCoupling: 0.55,
});
