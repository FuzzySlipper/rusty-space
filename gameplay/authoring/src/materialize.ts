import { readFile, writeFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';

import { sampleScene } from './scene.js';

const output = fileURLToPath(new URL('../../../content/gameplay/sample-scene.json', import.meta.url));
const expected = `${JSON.stringify(sampleScene, null, 2)}\n`;

if (process.argv.includes('--check')) {
  const actual = await readFile(output, 'utf8');
  if (actual !== expected) {
    throw new Error('content/gameplay/sample-scene.json drifts from gameplay/authoring; run pnpm authoring:materialize');
  }
} else {
  await writeFile(output, expected, 'utf8');
}

