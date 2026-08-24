import { expect, test } from '@playwright/test';

for (const viewport of [
  { name: 'square', width: 760, height: 760 },
  { name: 'wide', width: 1280, height: 720 },
]) {
  test(`renders one bounded Engine viewport without document overflow (${viewport.name})`, async ({ page }) => {
    await page.setViewportSize({ width: viewport.width, height: viewport.height });
    await page.goto('/');

    const canvas = page.locator('canvas');
    const label = page.getByTestId('space-label');
    await expect(canvas).toHaveCount(1);
    await expect(label).toBeVisible();
    // Wait for the WebSocket session to deliver the first projected ship frame.
    await expect(label).toContainText('pos', { timeout: 15_000 });

    const layout = await page.evaluate(() => {
      const canvas = document.querySelector('canvas');
      const label = document.querySelector<HTMLElement>('[data-testid="space-label"]');
      if (canvas === null || label === null) throw new Error('Rusty Space viewport is missing');
      const c = canvas.getBoundingClientRect();
      const l = label.getBoundingClientRect();
      return {
        scrollWidth: document.documentElement.scrollWidth,
        scrollHeight: document.documentElement.scrollHeight,
        viewportWidth: window.innerWidth,
        viewportHeight: window.innerHeight,
        engineCanvasCount: document.querySelectorAll('canvas[data-rusty-application-renderer="engine-owned"]').length,
        canvas: { left: c.left, top: c.top, right: c.right, bottom: c.bottom, width: c.width, height: c.height },
        backing: { width: canvas.width, height: canvas.height },
        label: { left: l.left, top: l.top, right: l.right, bottom: l.bottom },
      };
    });
    expect(layout.scrollWidth).toBe(layout.viewportWidth);
    expect(layout.scrollHeight).toBe(layout.viewportHeight);
    expect(layout.engineCanvasCount).toBe(1);
    expect(layout.canvas.width).toBeGreaterThan(0);
    expect(layout.canvas.height).toBeGreaterThan(0);
    expect(layout.backing.width).toBeGreaterThan(0);
    expect(layout.backing.height).toBeGreaterThan(0);
    expect(layout.canvas.left).toBeGreaterThanOrEqual(0);
    expect(layout.canvas.top).toBeGreaterThanOrEqual(0);
    expect(layout.canvas.right).toBeLessThanOrEqual(layout.viewportWidth);
    expect(layout.canvas.bottom).toBeLessThanOrEqual(layout.viewportHeight);
    expect(layout.label.left).toBeGreaterThanOrEqual(layout.canvas.left);
    expect(layout.label.top).toBeGreaterThanOrEqual(layout.canvas.top);
    expect(layout.label.right).toBeLessThanOrEqual(layout.canvas.right);
    expect(layout.label.bottom).toBeLessThanOrEqual(layout.canvas.bottom);

  });
}

test('centers the public presentation frame across the aspect interval and survives transient zero sizing', async ({ page }) => {
  await page.setViewportSize({ width: 900, height: 900 });
  await page.goto('/');
  await expect(page.getByTestId('space-label')).toContainText('pos', { timeout: 15_000 });

  const cases = [
    { name: 'below-minimum', width: 900, height: 900, frameWidth: 900, frameHeight: 675 },
    { name: 'minimum-edge', width: 960, height: 720, frameWidth: 960, frameHeight: 720 },
    { name: 'inside-interval', width: 1000, height: 700, frameWidth: 1000, frameHeight: 700 },
    { name: 'maximum-edge', width: 1280, height: 720, frameWidth: 1280, frameHeight: 720 },
    { name: 'beyond-maximum', width: 1600, height: 700, frameWidth: 700 * (16 / 9), frameHeight: 700 },
  ] as const;

  for (const expected of cases) {
    await page.setViewportSize({ width: expected.width, height: expected.height });
    await expect.poll(async () => {
      const geometry = await page.evaluate(() => {
        const frame = document.querySelector<HTMLElement>('[data-rusty-application-presentation-frame]');
        const canvas = document.querySelector<HTMLCanvasElement>('canvas');
        if (frame === null || canvas === null) throw new Error('bounded Engine frame is missing');
        const rect = frame.getBoundingClientRect();
        const canvasRect = canvas.getBoundingClientRect();
        return {
          frame: { left: rect.left, top: rect.top, width: rect.width, height: rect.height },
          canvas: { left: canvasRect.left, top: canvasRect.top, width: canvasRect.width, height: canvasRect.height },
          scrollWidth: document.documentElement.scrollWidth,
          scrollHeight: document.documentElement.scrollHeight,
          viewportWidth: window.innerWidth,
          viewportHeight: window.innerHeight,
        };
      });
      return Math.abs(geometry.frame.width - expected.frameWidth) < 1
        && Math.abs(geometry.frame.height - expected.frameHeight) < 1
        && Math.abs(geometry.frame.left - ((expected.width - expected.frameWidth) / 2)) < 1
        && Math.abs(geometry.frame.top - ((expected.height - expected.frameHeight) / 2)) < 1
        && Math.abs(geometry.canvas.width - expected.frameWidth) < 1
        && Math.abs(geometry.canvas.height - expected.frameHeight) < 1
        && geometry.scrollWidth === geometry.viewportWidth
        && geometry.scrollHeight === geometry.viewportHeight;
    }, { timeout: 5_000 }).toBe(true);
  }

  // The Engine host deliberately represents a hidden/transient mount as a
  // zero-sized frame. It must recover to a real backing canvas when the mount
  // becomes measurable again, without introducing NaN CSS or document scroll.
  await page.evaluate(() => {
    const root = document.querySelector<HTMLElement>('#application');
    if (root === null) throw new Error('application root is missing');
    root.style.width = '0px';
    root.style.height = '0px';
    window.dispatchEvent(new Event('resize'));
  });
  await expect.poll(() => page.evaluate(() => {
    const frame = document.querySelector<HTMLElement>('[data-rusty-application-presentation-frame]');
    return frame?.getBoundingClientRect().width ?? -1;
  }), { timeout: 5_000 }).toBe(0);

  await page.evaluate(() => {
    const root = document.querySelector<HTMLElement>('#application');
    if (root === null) throw new Error('application root is missing');
    root.style.removeProperty('width');
    root.style.removeProperty('height');
    window.dispatchEvent(new Event('resize'));
  });
  await expect.poll(() => page.evaluate(() => {
    const canvas = document.querySelector<HTMLCanvasElement>('canvas');
    return canvas?.width ?? 0;
  }), { timeout: 5_000 }).toBeGreaterThan(0);
  await expect.poll(() => page.evaluate(() => ({
    scrollWidth: document.documentElement.scrollWidth,
    scrollHeight: document.documentElement.scrollHeight,
    width: window.innerWidth,
    height: window.innerHeight,
  }))).toEqual({ scrollWidth: 1600, scrollHeight: 700, width: 1600, height: 700 });
});

test('keeps oversized product UI inside the shared presentation frame without document overflow', async ({ page }) => {
  await page.setViewportSize({ width: 360, height: 640 });
  await page.goto('/');
  await expect(page.getByTestId('space-label')).toContainText('pos', { timeout: 15_000 });
  await page.evaluate(() => {
    const oversized = 'Rusty Space '.repeat(300);
    const label = document.querySelector<HTMLElement>('[data-testid="space-label"]');
    const status = document.querySelector<HTMLElement>('[data-testid="session-status"]');
    if (label === null || status === null) throw new Error('product UI is missing');
    label.textContent = oversized;
    status.textContent = oversized;
  });

  const layout = await page.evaluate(() => {
    const frame = document.querySelector<HTMLElement>('[data-rusty-application-presentation-frame]');
    const label = document.querySelector<HTMLElement>('[data-testid="space-label"]');
    const status = document.querySelector<HTMLElement>('[data-testid="session-status"]');
    if (frame === null || label === null || status === null) throw new Error('bounded product UI is missing');
    const frameRect = frame.getBoundingClientRect();
    const labelRect = label.getBoundingClientRect();
    const statusRect = status.getBoundingClientRect();
    return {
      frame: frameRect.toJSON(),
      label: labelRect.toJSON(),
      status: statusRect.toJSON(),
      scrollWidth: document.documentElement.scrollWidth,
      scrollHeight: document.documentElement.scrollHeight,
      width: window.innerWidth,
      height: window.innerHeight,
    };
  });
  expect(layout.scrollWidth).toBe(layout.width);
  expect(layout.scrollHeight).toBe(layout.height);
  expect(layout.label.left).toBeGreaterThanOrEqual(layout.frame.left);
  expect(layout.label.right).toBeLessThanOrEqual(layout.frame.right);
  expect(layout.status.left).toBeGreaterThanOrEqual(layout.frame.left);
  expect(layout.status.right).toBeLessThanOrEqual(layout.frame.right);
});
