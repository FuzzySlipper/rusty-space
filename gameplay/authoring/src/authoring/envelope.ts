/**
 * Package envelope composition through the Engine's canonical binary64
 * authoring API (schema 2). Provenance points at the authored catalog source so
 * the committed artifact stays traceable. The canonicalJson is the exact byte
 * string the Engine fingerprints; materialization writes it verbatim.
 */

import { authorBinary64RulePackage } from '@rusty-engine/gameplay-rules-authoring';
import type { JsonValue } from '@rusty-engine/gameplay-rules-contracts';

import type { ShipHandlingDefinition } from './definitions.js';

export interface ShipHandlingPayload extends ShipHandlingDefinition {
  readonly schemaVersion: 2;
}

export interface PackageInput {
  readonly packageId: string;
  readonly version: number;
  readonly source: Readonly<{ id: string; path: string }>;
  readonly subject: string;
  readonly payload: ShipHandlingPayload;
}

export const composePackage = (input: PackageInput) =>
  authorBinary64RulePackage({
    domain: 'rusty-space',
    package: input.packageId,
    version: input.version,
    dependencies: [],
    sources: [{ id: input.source.id, path: input.source.path }],
    provenance: [{ subject: input.subject, source: input.source.id }],
    payload: input.payload as unknown as JsonValue,
  });
