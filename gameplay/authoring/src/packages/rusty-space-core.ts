/**
 * The core Rusty Space gameplay package: ship handling authored as a typed
 * catalog and composed into the deterministic envelope Rust admits.
 */

import { composePackage } from '../authoring/mod.js';
import { stockShipHandling } from '../catalogs/ship.js';

export const gameplayPackage = composePackage({
  packageId: 'core',
  version: 1,
  source: { id: 'ship-handling', path: 'gameplay/authoring/src/catalogs/ship.ts' },
  subject: 'rusty-space-ship',
  payload: {
    schemaVersion: 1,
    ...stockShipHandling,
  },
});
