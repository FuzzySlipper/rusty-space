import { copyFile, mkdir, readFile, rm, writeFile } from 'node:fs/promises';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const [output] = process.argv.slice(2);
if (output === undefined) {
  throw new Error('usage: generate-browser-bundle.mjs <output-directory>');
}

const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const repositoryRoot = resolve(scriptDirectory, '..');
const engineRoot = resolve(process.env.RUSTY_ENGINE_ROOT ?? join(repositoryRoot, '..', 'rusty-engine'));
const engineHostRoot = join(engineRoot, 'render', 'artifacts', 'product-browser-host');
const uiSource = join(repositoryRoot, 'src', 'ui', 'main.js');
const runtimeAdapterSource = 'export const PRODUCT_RUNTIME_HTTP_BASE_PATH = "/__rusty/product/runtime/";\n';

const { productBrowserBundleAssets } = await import(pathToFileURL(join(engineHostRoot, 'index.js')).href);
const engineHostModule = await readFile(join(engineHostRoot, 'product-browser-host.js'), 'utf8');
const assets = productBrowserBundleAssets({
  engineHostModule,
  uiModule: './ui/main.js',
  runtimeAdapterModule: './runtime-adapter.js',
  lifecycleMode: 'realtime',
  realtimeAdvanceOwner: 'browser',
  uiProjection: {
    expectedStream: 'rusty-space',
    expectedContract: 'rusty.space.hud',
  },
});

await mkdir(join(output, 'ui'), { recursive: true });
// The C# runtime injects the sole renderer preload descriptor from the product's
// admitted resources. Space currently selects no resource files.
await rm(join(output, 'renderer-preload.json'), { force: true });
for (const asset of assets) {
  const path = join(output, asset.name);
  await mkdir(dirname(path), { recursive: true });
  await writeFile(path, asset.content);
}
await copyFile(uiSource, join(output, 'ui', 'main.js'));
await writeFile(join(output, 'runtime-adapter.js'), runtimeAdapterSource);
