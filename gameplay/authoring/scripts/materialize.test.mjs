import assert from 'node:assert/strict';
import { execFile } from 'node:child_process';
import { mkdtemp, mkdir, readFile, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { dirname, resolve } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';
import { promisify } from 'node:util';

const execFileAsync = promisify(execFile);
const scriptsDirectory = dirname(fileURLToPath(import.meta.url));
const script = resolve(scriptsDirectory, 'materialize.mjs');

const canonicalJson = '{"fixture":true}\n';

async function writeCurrentPackageFixture({ packages, source, output }) {
  await writeFile(resolve(source, 'current.ts'), 'export {};\n', 'utf8');
  await writeFile(
    resolve(packages, 'current.js'),
    `export const gameplayPackage = { package: { domain: 'rusty-space', package: 'core' }, canonicalJson: ${JSON.stringify(canonicalJson)}, fingerprint: 'fixture' };\n`,
    'utf8',
  );
  await writeFile(resolve(output, 'rusty-space-core.package.json'), canonicalJson, 'utf8');
}

function fixtureEnvironment({ packages, source, output }) {
  return {
    ...process.env,
    RUSTY_SPACE_AUTHORING_PACKAGES_DIR: packages,
    RUSTY_SPACE_AUTHORING_SOURCE_PACKAGES_DIR: source,
    RUSTY_SPACE_GAMEPLAY_OUTPUT_DIR: output,
  };
}

test('check reports a stale compiled package module after its source disappears', async () => {
  const fixtureRoot = await mkdtemp(resolve(tmpdir(), 'rusty-space-materializer-'));
  const packages = resolve(fixtureRoot, 'packages');
  const source = resolve(fixtureRoot, 'source');
  const output = resolve(fixtureRoot, 'content');
  await mkdir(packages);
  await mkdir(source);
  await mkdir(output);
  await writeCurrentPackageFixture({ packages, source, output });
  await writeFile(
    resolve(packages, 'removed.js'),
    `export const gameplayPackage = { package: { domain: 'rusty-space', package: 'removed' }, canonicalJson: ${JSON.stringify(canonicalJson)}, fingerprint: 'fixture' };\n`,
    'utf8',
  );

  await assert.rejects(
    execFileAsync(process.execPath, [script, '--check'], {
      env: fixtureEnvironment({ packages, source, output }),
    }),
    (error) => {
      assert.match(error.stderr, /compiled authoring module closure drifts from source/);
      assert.match(error.stderr, /stale: removed\.js/);
      return true;
    },
  );
});

test('check reports a stale committed package artifact and does not rewrite it', async () => {
  const fixtureRoot = await mkdtemp(resolve(tmpdir(), 'rusty-space-materializer-'));
  const packages = resolve(fixtureRoot, 'packages');
  const source = resolve(fixtureRoot, 'source');
  const output = resolve(fixtureRoot, 'content');
  await mkdir(packages);
  await mkdir(source);
  await mkdir(output);
  await writeCurrentPackageFixture({ packages, source, output });
  await writeFile(resolve(output, 'stale.package.json'), '{"stale":true}\n', 'utf8');

  await assert.rejects(
    execFileAsync(process.execPath, [script, '--check'], {
      env: fixtureEnvironment({ packages, source, output }),
    }),
    (error) => {
      assert.match(error.stderr, /stale: stale\.package\.json/);
      return true;
    },
  );
  assert.equal(
    await readFile(resolve(output, 'stale.package.json'), 'utf8'),
    '{"stale":true}\n',
    '--check must not rewrite the committed artifact directory',
  );
});

test('check reports a missing artifact directory deterministically', async () => {
  const fixtureRoot = await mkdtemp(resolve(tmpdir(), 'rusty-space-materializer-'));
  const packages = resolve(fixtureRoot, 'packages');
  const source = resolve(fixtureRoot, 'source');
  const output = resolve(fixtureRoot, 'missing-content');
  await mkdir(packages);
  await mkdir(source);
  await writeFile(resolve(source, 'current.ts'), 'export {};\n', 'utf8');
  await writeFile(
    resolve(packages, 'current.js'),
    `export const gameplayPackage = { package: { domain: 'rusty-space', package: 'core' }, canonicalJson: ${JSON.stringify(canonicalJson)}, fingerprint: 'fixture' };\n`,
    'utf8',
  );

  await assert.rejects(
    execFileAsync(process.execPath, [script, '--check'], {
      env: fixtureEnvironment({ packages, source, output }),
    }),
    (error) => {
      assert.match(error.stderr, /gameplay artifact closure drifts from gameplay\/authoring/);
      assert.match(error.stderr, /missing: rusty-space-core\.package\.json/);
      assert.match(error.stderr, /output directory is missing/);
      return true;
    },
  );
});

test('check rejects an unsafe generated filename identity', async () => {
  const fixtureRoot = await mkdtemp(resolve(tmpdir(), 'rusty-space-materializer-'));
  const packages = resolve(fixtureRoot, 'packages');
  const source = resolve(fixtureRoot, 'source');
  const output = resolve(fixtureRoot, 'content');
  await mkdir(packages);
  await mkdir(source);
  await mkdir(output);
  await writeFile(resolve(source, 'unsafe.ts'), 'export {};\n', 'utf8');
  await writeFile(
    resolve(packages, 'unsafe.js'),
    `export const gameplayPackage = { package: { domain: 'rusty-space', package: '../escape' }, canonicalJson: ${JSON.stringify(canonicalJson)}, fingerprint: 'fixture' };\n`,
    'utf8',
  );

  await assert.rejects(
    execFileAsync(process.execPath, [script, '--check'], {
      env: fixtureEnvironment({ packages, source, output }),
    }),
    (error) => {
      assert.match(error.stderr, /safe filename components/);
      return true;
    },
  );
});
