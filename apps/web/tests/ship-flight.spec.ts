import { expect, test, type Page } from '@playwright/test';

type Label = ReturnType<Page['getByTestId']>;

interface Position {
  x: number;
  z: number;
}

async function readLabelText(label: Label): Promise<string> {
  return (await label.textContent()) ?? '';
}

async function readPosition(label: Label): Promise<Position> {
  const text = await readLabelText(label);
  const match = /pos (-?[\d.]+), (-?[\d.]+)/.exec(text);
  if (match === null) throw new Error(`ship position not found in HUD label: ${text}`);
  return { x: Number(match[1]), z: Number(match[2]) };
}

async function readSpeed(label: Label): Promise<number> {
  const text = await readLabelText(label);
  const match = /speed (-?[\d.]+)/.exec(text);
  if (match === null) throw new Error(`ship speed not found in HUD label: ${text}`);
  return Number(match[1]);
}

async function readHeadingDegrees(label: Label): Promise<number> {
  const text = await readLabelText(label);
  const match = /heading (-?[\d.]+)°/.exec(text);
  if (match === null) throw new Error(`ship heading not found in HUD label: ${text}`);
  return Number(match[1]);
}

async function readAcceptedCommandSequence(page: Page): Promise<number> {
  const text = (await page.getByTestId('session-status').textContent()) ?? '';
  const match = /command (\d+) accepted/.exec(text);
  if (match === null) throw new Error(`accepted command receipt not found: ${text}`);
  return Number(match[1]);
}

test('a browser session can turn and thrust the ship', async ({ page }) => {
  await page.setViewportSize({ width: 1000, height: 760 });
  let admittedBaseline: { ops?: Array<{ op?: string; handle?: number; node?: { metadata?: { label?: string; tags?: string[] } } }> } | undefined;
  page.on('websocket', (webSocket) => {
    webSocket.on('framereceived', (payload) => {
      try {
        const encoded = typeof payload === 'string'
          ? payload
          : (payload as { payload?: string }).payload ?? payload.toString();
        const message = JSON.parse(encoded) as {
          type?: string;
          update?: { frame?: typeof admittedBaseline };
        };
        if (message.type === 'baseline' && message.update?.frame !== undefined) {
          admittedBaseline = message.update.frame;
        }
      } catch {
        // Other WebSocket frames are not part of this browser evidence.
      }
    });
  });
  await page.goto('/');

  const label = page.getByTestId('space-label');
  await expect(page.locator('canvas')).toHaveCount(1);
  await expect(label).toContainText('pos', { timeout: 15_000 });
  await expect.poll(() => admittedBaseline).toBeDefined();
  const admittedNodes = (admittedBaseline?.ops ?? [])
    .filter((operation) => operation.op === 'create')
    .map((operation) => ({ handle: operation.handle, metadata: operation.node?.metadata }))
    .filter((operation): operation is { handle: number | undefined; metadata: { label?: string; tags?: string[] } } => operation.metadata !== undefined);
  expect(admittedNodes.map((operation) => operation.handle)).toEqual([1, 2, 3, 4]);
  expect(admittedNodes.map((operation) => operation.metadata.label)).toEqual([
    'ship',
    'heading',
    'velocity',
    'projected-path',
  ]);
  expect(admittedNodes[0]?.metadata?.tags).toContain('rusty-space-ship');

  // Thrust forward: the ship accelerates and its position advances.
  const start = await readPosition(label);
  await page.keyboard.down('ArrowUp');
  await page.waitForTimeout(700);
  await page.keyboard.up('ArrowUp');
  await page.waitForTimeout(120);
  const afterThrust = await readPosition(label);
  expect(Math.hypot(afterThrust.x - start.x, afterThrust.z - start.z)).toBeGreaterThan(0.5);

  // Releasing thrust never brakes: the ship keeps drifting.
  const coastStart = await readPosition(label);
  await page.waitForTimeout(500);
  const coastEnd = await readPosition(label);
  expect(Math.hypot(coastEnd.x - coastStart.x, coastEnd.z - coastStart.z)).toBeGreaterThan(0.2);

  // Turning while drifting changes heading without changing speed: heading and
  // velocity are decoupled.
  const headingBefore = await readHeadingDegrees(label);
  const speedBefore = await readSpeed(label);
  await page.keyboard.down('ArrowRight');
  await page.waitForTimeout(400);
  await page.keyboard.up('ArrowRight');
  await page.waitForTimeout(120);
  const headingAfter = await readHeadingDegrees(label);
  const speedAfter = await readSpeed(label);
  expect(Math.abs(headingAfter - headingBefore)).toBeGreaterThan(15);
  expect(Math.abs(speedAfter - speedBefore)).toBeLessThan(0.5);
});

test('late, reconnecting, and concurrent sessions preserve a complete baseline and one controller', async ({ page }) => {
  const browserContext = page.context();
  await page.setViewportSize({ width: 1000, height: 760 });
  await page.goto('/');
  const firstLabel = page.getByTestId('space-label');
  await expect(firstLabel).toContainText('pos', { timeout: 15_000 });

  // Let the service produce update-only retained-frame diffs before the next
  // browser connects. The next page still has to render from its own Create
  // baseline, not from this page's historical renderer state.
  await page.waitForTimeout(350);
  const second = await browserContext.newPage();
  await second.setViewportSize({ width: 1000, height: 760 });
  await second.goto('/');
  const secondLabel = second.getByTestId('space-label');
  await expect(second.locator('canvas')).toHaveCount(1);
  await expect(secondLabel).toContainText('pos', { timeout: 15_000 });

  // The second connection replaces the first controller lease. A delayed
  // first-page intent receives a typed stale-generation rejection rather than
  // changing the Rust-owned command.
  await page.bringToFront();
  await page.keyboard.down('ArrowUp');
  await expect(page.getByTestId('session-status')).toContainText('Controls rejected: staleGeneration');
  await page.keyboard.up('ArrowUp');

  await second.bringToFront();
  await second.keyboard.down('ArrowUp');
  await expect(second.getByTestId('session-status')).toContainText('command', { timeout: 5_000 });
  const commandBeforeFirstCloses = await readAcceptedCommandSequence(second);
  await page.close(); // stale teardown must not revoke the replacement lease.
  await second.keyboard.down('ArrowRight');
  await expect.poll(() => readAcceptedCommandSequence(second)).toBeGreaterThan(commandBeforeFirstCloses);

  // Blur and pagehide clear held input locally and send a new authoritative
  // neutral intent before transport teardown.
  const commandBeforeBlur = await readAcceptedCommandSequence(second);
  await second.evaluate(() => window.dispatchEvent(new Event('blur')));
  await expect.poll(() => readAcceptedCommandSequence(second)).toBeGreaterThan(commandBeforeBlur);
  const commandBeforePageHide = await readAcceptedCommandSequence(second);
  await second.evaluate(() => window.dispatchEvent(new Event('pagehide')));
  await expect.poll(() => readAcceptedCommandSequence(second)).toBeGreaterThan(commandBeforePageHide);
  await second.close(); // close while thrust is held; host must neutralize it.

  const reconnected = await browserContext.newPage();
  await reconnected.setViewportSize({ width: 1000, height: 760 });
  await reconnected.goto('/');
  const reconnectedLabel = reconnected.getByTestId('space-label');
  await expect(reconnectedLabel).toContainText('pos', { timeout: 15_000 });
  const speedAfterDisconnect = await readSpeed(reconnectedLabel);
  await reconnected.waitForTimeout(450);
  const speedAfterWait = await readSpeed(reconnectedLabel);
  expect(Math.abs(speedAfterWait - speedAfterDisconnect)).toBeLessThan(0.2);

  // A reload is a real browser transport replacement and must render a fresh
  // baseline before its subsequent frame diffs are applied.
  await reconnected.reload();
  await expect(reconnectedLabel).toContainText('pos', { timeout: 15_000 });
  await reconnected.close();
});
