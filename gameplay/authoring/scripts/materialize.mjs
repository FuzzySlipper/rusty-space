import { mkdir, readFile, readdir, writeFile } from 'node:fs/promises';
import { dirname, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

// Materializes every package entry in gameplay/authoring/src/packages/
// (compiled to dist/packages/) into content/gameplay/<domain>-<package>.package.json.
// Output is deterministic: same sources, same bytes, drift-checked by `--check`.
// Build plumbing only — semantic validation is Rust's.

const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const packagesDirectory = resolve(scriptDirectory, '../dist/packages');
const outputDirectory = resolve(scriptDirectory, '../../../content/gameplay');
const check = process.argv.includes('--check');

const entries = (await readdir(packagesDirectory))
  .filter((entry) => entry.endsWith('.js'))
  .sort();

await mkdir(outputDirectory, { recursive: true });
for (const entry of entries) {
  const module = await import(pathToFileURL(resolve(packagesDirectory, entry)).href);
  const artifact = module.gameplayPackage;
  if (artifact?.canonicalJson === undefined) {
    throw new Error(`${entry} does not export a canonical gameplayPackage artifact`);
  }
  const name = `${artifact.package.domain}-${artifact.package.package}.package.json`;
  const output = resolve(outputDirectory, name);
  // canonicalJson is the exact byte string the Engine fingerprints.
  const expected = `${artifact.canonicalJson}\n`;
  if (check) {
    const actual = await readFile(output, 'utf8');
    if (actual !== expected) {
      throw new Error(`${name} drifts from gameplay/authoring; run pnpm authoring:materialize`);
    }
  } else {
    await writeFile(output, expected, 'utf8');
    console.log(`materialized ${name} (${artifact.fingerprint})`);
  }
}
