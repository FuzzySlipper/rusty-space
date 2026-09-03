// Visual verification: exercise zoom, inertial coast, turning, and redirected thrust.
// Run: node scripts/verify-scene.mjs <url> <outDir>
import { chromium } from '/home/dev/rusty-engine/render/node_modules/@playwright/test/index.mjs';
import { mkdirSync } from 'node:fs';

const url = process.argv[2] ?? 'http://127.0.0.1:3081/';
const outDir = process.argv[3] ?? '/tmp/rusty-verify';
mkdirSync(outDir, { recursive: true });

const browser = await chromium.launch();
const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
const errors = [];
const consoleMessages = [];
const requestFailures = [];
const inputRequests = [];
page.on('pageerror', (error) => errors.push(String(error)));
page.on('console', (message) => consoleMessages.push(`${message.type()}: ${message.text()}`));
page.on('requestfailed', (request) => requestFailures.push(
  `${request.method()} ${request.url()}: ${request.failure()?.errorText ?? 'unknown failure'}`,
));
page.on('request', (request) => {
  if (request.url().includes('/__rusty/product/runtime/input')) {
    inputRequests.push(request.postData());
  }
});

// The realtime product host intentionally keeps its transport open, so
// networkidle is not a reachable readiness state. Wait for the document and
// Engine-owned canvas instead.
await page.goto(url, { waitUntil: 'domcontentloaded', timeout: 30000 });
const canvas = page.locator('canvas').first();
await canvas.waitFor({ state: 'visible', timeout: 15000 });

const hud = page.locator('aside p').last();
const readHud = async () => await hud.textContent();

// Idle frame and two deliberately distinct camera distances.
await page.screenshot({ path: `${outDir}/01-loaded.png` });

await page.waitForTimeout(500);
const canvasCount = await page.locator('canvas').count();
if (canvasCount === 0) {
  throw new Error(JSON.stringify({ errors, consoleMessages, requestFailures }, null, 2));
}

const box = await canvas.boundingBox();
if (box === null) {
  throw new Error('Engine canvas disappeared before visual input could begin');
}
const cx = box.x + box.width / 2;
const cy = box.y + box.height / 2;
await page.mouse.click(cx, cy);

await page.mouse.wheel(0, 640);
await page.waitForTimeout(500);
await page.screenshot({ path: `${outDir}/02-zoomed-out.png` });

await page.mouse.wheel(0, -640);
await page.mouse.wheel(0, -640);
await page.waitForTimeout(500);
await page.screenshot({ path: `${outDir}/03-zoomed-in.png` });

await page.keyboard.down('w');
await page.waitForTimeout(2500);
await page.keyboard.up('w');
await page.waitForTimeout(400);
const coastStart = await readHud();
await page.screenshot({ path: `${outDir}/04-coast-start.png` });

await page.waitForTimeout(3000);
const coastEnd = await readHud();
await page.screenshot({ path: `${outDir}/05-coast-end.png` });

await page.keyboard.down('a');
await page.waitForTimeout(1200);
const turnWhileCoasting = await readHud();
await page.screenshot({ path: `${outDir}/06-turn-while-coasting.png` });
await page.keyboard.up('a');
await page.waitForTimeout(700);

await page.keyboard.down('w');
await page.waitForTimeout(1200);
await page.keyboard.up('w');
await page.waitForTimeout(400);
const redirectedThrust = await readHud();
await page.screenshot({ path: `${outDir}/07-redirected-thrust.png` });

console.log(JSON.stringify({
  canvas: box,
  pageErrors: errors,
  hud: { coastStart, coastEnd, turnWhileCoasting, redirectedThrust },
  inputRequests,
}, null, 2));
await browser.close();
