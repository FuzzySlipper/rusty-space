import { mkdir, readFile, readdir, writeFile } from 'node:fs/promises';
import { dirname, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

// Materializes every package entry in gameplay/authoring/src/packages/
// (compiled to dist/packages/) into content/gameplay/<domain>-<package>.package.json.
// Output is deterministic: same sources, same bytes, drift-checked by `--check`.
// Build plumbing only — semantic validation is Rust's.

const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const sourcePackagesDirectory = process.env.RUSTY_SPACE_AUTHORING_SOURCE_PACKAGES_DIR
  ? resolve(process.env.RUSTY_SPACE_AUTHORING_SOURCE_PACKAGES_DIR)
  : resolve(scriptDirectory, '../src/packages');
const packagesDirectory = process.env.RUSTY_SPACE_AUTHORING_PACKAGES_DIR
  ? resolve(process.env.RUSTY_SPACE_AUTHORING_PACKAGES_DIR)
  : resolve(scriptDirectory, '../dist/packages');
const outputDirectory = process.env.RUSTY_SPACE_GAMEPLAY_OUTPUT_DIR
  ? resolve(process.env.RUSTY_SPACE_GAMEPLAY_OUTPUT_DIR)
  : resolve(scriptDirectory, '../../../content/gameplay');
const check = process.argv.includes('--check');

const sourceEntries = (await readdir(sourcePackagesDirectory))
  .filter((entry) => entry.endsWith('.ts'))
  .sort();
const entries = (await readdir(packagesDirectory))
  .filter((entry) => entry.endsWith('.js'))
  .sort();

const sourceStems = new Set(sourceEntries.map((entry) => entry.slice(0, -'.ts'.length)));
const compiledStems = new Set(entries.map((entry) => entry.slice(0, -'.js'.length)));
const staleCompiledEntries = entries.filter((entry) => !sourceStems.has(entry.slice(0, -'.js'.length)));
const missingCompiledEntries = sourceEntries.filter(
  (entry) => !compiledStems.has(entry.slice(0, -'.ts'.length)),
);
if (staleCompiledEntries.length > 0 || missingCompiledEntries.length > 0) {
  throw new Error(
    `compiled authoring module closure drifts from source; stale: ${staleCompiledEntries.join(', ') || '(none)'}; missing: ${missingCompiledEntries.join(', ') || '(none)'}; run pnpm --dir gameplay/authoring build after removing stale dist/packages files`,
  );
}

function artifactFileName(artifact) {
  const { domain, package: packageId } = artifact.package;
  const safeComponent = /^[A-Za-z0-9][A-Za-z0-9._-]*$/;
  if (!safeComponent.test(domain) || !safeComponent.test(packageId)) {
    throw new Error(
      `generated gameplay artifact identity must use safe filename components: ${domain}/${packageId}`,
    );
  }
  return `${domain}-${packageId}.package.json`;
}

const expectedArtifacts = new Map();
for (const entry of entries) {
  const module = await import(pathToFileURL(resolve(packagesDirectory, entry)).href);
  const artifact = module.gameplayPackage;
  if (artifact?.canonicalJson === undefined) {
    throw new Error(`${entry} does not export a canonical gameplayPackage artifact`);
  }
  const name = artifactFileName(artifact);
  // canonicalJson is the exact newline-terminated byte string the Engine fingerprints.
  const expected = artifact.canonicalJson;
  if (expectedArtifacts.has(name)) {
    throw new Error(`${entry} duplicates generated gameplay artifact ${name}`);
  }
  expectedArtifacts.set(name, { expected, fingerprint: artifact.fingerprint });
}

if (check) {
  const expectedEntries = [...expectedArtifacts.keys()].sort();
  let actualEntries;
  try {
    actualEntries = (await readdir(outputDirectory))
      .filter((entry) => entry.endsWith('.package.json'))
      .sort();
  } catch (error) {
    if (error?.code === 'ENOENT') {
      throw new Error(
        `gameplay artifact closure drifts from gameplay/authoring; stale: (none); missing: ${expectedEntries.join(', ') || '(none)'}; output directory is missing; run pnpm authoring:materialize`,
      );
    }
    throw error;
  }
  const staleEntries = actualEntries.filter((entry) => !expectedArtifacts.has(entry));
  const missingEntries = expectedEntries.filter((entry) => !actualEntries.includes(entry));
  if (staleEntries.length > 0 || missingEntries.length > 0) {
    throw new Error(
      `gameplay artifact closure drifts from gameplay/authoring; stale: ${staleEntries.join(', ') || '(none)'}; missing: ${missingEntries.join(', ') || '(none)'}; run pnpm authoring:materialize`,
    );
  }
  for (const name of expectedEntries) {
    const actual = await readFile(resolve(outputDirectory, name), 'utf8');
    if (actual !== expectedArtifacts.get(name).expected) {
      throw new Error(`${name} drifts from gameplay/authoring; run pnpm authoring:materialize`);
    }
  }
} else {
  await mkdir(outputDirectory, { recursive: true });
  for (const [name, { expected, fingerprint }] of expectedArtifacts) {
    const output = resolve(outputDirectory, name);
    await writeFile(output, expected, 'utf8');
    console.log(`materialized ${name} (${fingerprint})`);
  }
}
