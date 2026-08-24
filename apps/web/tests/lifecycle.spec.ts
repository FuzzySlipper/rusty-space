import { expect, test, type WebSocket as PlaywrightWebSocket } from '@playwright/test';

test('keeps loading bounded until Rust admits a baseline and renders a bounded startup failure', async ({ page }) => {
  let route: { send(message: string): void } | undefined;
  await page.routeWebSocket(/\/api\/session$/, (webSocket) => {
    route = webSocket;
  });

  void page.goto('/', { waitUntil: 'commit' }).catch(() => undefined);
  const loading = page.locator('[data-rusty-application-loading]');
  await expect(loading).toHaveText('Loading Rusty Space…');
  await expect(page.locator('[data-rusty-application-presentation-frame]')).toHaveCount(1);
  await expect.poll(() => route).toBeDefined();

  const loadingLayout = await page.evaluate(() => {
    const frame = document.querySelector<HTMLElement>('[data-rusty-application-presentation-frame]');
    const loading = document.querySelector<HTMLElement>('[data-rusty-application-loading]');
    if (frame === null || loading === null) throw new Error('bounded loading presentation is missing');
    const frameRect = frame.getBoundingClientRect();
    const loadingRect = loading.getBoundingClientRect();
    return {
      frame: { left: frameRect.left, top: frameRect.top, right: frameRect.right, bottom: frameRect.bottom },
      loading: { left: loadingRect.left, top: loadingRect.top, right: loadingRect.right, bottom: loadingRect.bottom },
    };
  });
  expect(loadingLayout.loading).toEqual(loadingLayout.frame);

  route?.send('{not-json');
  const failure = page.locator('[data-rusty-application-failure]');
  await expect(failure).toContainText('Rusty Space failed to start');
  await expect(failure).toContainText('baseline failed: malformed server message');
  await expect(page.locator('canvas')).toHaveCount(0);
  await expect(page.locator('.space-surface')).toHaveCount(0);
  await expect(page.locator('[data-rusty-application-presentation-frame]')).toHaveCount(1);

  const failureLayout = await page.evaluate(() => ({
    scrollWidth: document.documentElement.scrollWidth,
    scrollHeight: document.documentElement.scrollHeight,
    width: window.innerWidth,
    height: window.innerHeight,
  }));
  expect(failureLayout).toEqual({
    scrollWidth: failureLayout.width,
    scrollHeight: failureLayout.height,
    width: failureLayout.width,
    height: failureLayout.height,
  });
});

test('blocks modal input and disposes and remounts one complete product owner', async ({ page }) => {
  await page.addInitScript(() => {
    window.__rustySpaceBrowserTestProbe = {};
  });

  const sockets: PlaywrightWebSocket[] = [];
  const sentFrames: string[] = [];
  page.on('websocket', (socket) => {
    sockets.push(socket);
    socket.on('framesent', (frame) => {
      const encoded = typeof frame === 'string'
        ? frame
        : (frame as { payload?: string }).payload ?? frame.toString();
      sentFrames.push(encoded);
    });
  });

  await page.goto('/');
  await expect(page.getByTestId('space-label')).toContainText('pos', { timeout: 15_000 });
  await page.keyboard.down('ArrowUp');
  await expect(page.getByTestId('session-status')).toContainText('command');

  await page.evaluate(() => window.__rustySpaceBrowserTestProbe?.setInteractionMode?.('modal'));
  await expect.poll(() => sentFrames.filter((frame) => frame.includes('"throttle":0')).length).toBeGreaterThan(0);
  const activeCommandsAfterNeutralization = sentFrames.filter((frame) => frame.includes('"throttle":1')).length;
  await page.keyboard.up('ArrowUp');
  await page.keyboard.press('ArrowUp');
  await page.waitForTimeout(150);
  expect(sentFrames.filter((frame) => frame.includes('"throttle":1')))
    .toHaveLength(activeCommandsAfterNeutralization);

  await page.evaluate(() => window.__rustySpaceBrowserTestProbe?.setInteractionMode?.('gameplay'));
  const lifecycle = await page.evaluate(async () => {
    const probe = window.__rustySpaceBrowserTestProbe;
    if (probe?.application === undefined || probe.disposeAndRemount === undefined) {
      throw new Error('browser test lifecycle probe is missing');
    }
    const oldApplication = probe.application;
    await probe.disposeAndRemount();
    let staleRendererRejected = false;
    try {
      oldApplication.renderer.renderOnce();
    } catch {
      staleRendererRejected = true;
    }
    return {
      oldState: oldApplication.readout().state,
      newState: probe.application?.readout().state,
      staleRendererRejected,
      canvases: document.querySelectorAll('canvas[data-rusty-application-renderer="engine-owned"]').length,
      hosts: document.querySelectorAll('[data-rusty-application-host]').length,
      surfaces: document.querySelectorAll('.space-surface').length,
    };
  });
  expect(lifecycle).toEqual({
    oldState: 'disposed',
    newState: 'ready',
    staleRendererRejected: true,
    canvases: 1,
    hosts: 1,
    surfaces: 1,
  });
  await expect(page.getByTestId('space-label')).toContainText('pos', { timeout: 15_000 });
  await expect.poll(() => sockets.length).toBe(2);
  expect(sockets[0]?.isClosed()).toBe(true);

  const commandsBefore = sentFrames.filter((frame) => frame.includes('setFlightIntent')).length;
  await page.keyboard.down('ArrowRight');
  await page.keyboard.up('ArrowRight');
  await expect.poll(() => sentFrames.filter((frame) => frame.includes('setFlightIntent')).length)
    .toBe(commandsBefore + 2);
});
