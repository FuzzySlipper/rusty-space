import { expect, test } from '@playwright/test';
import { PNG } from 'pngjs';

for (const viewport of [
  { name: 'square', width: 760, height: 760 },
  { name: 'wide', width: 1280, height: 720 },
]) {
  test(`renders one bounded Engine viewport without document overflow (${viewport.name})`, async ({ page }) => {
    await page.setViewportSize({ width: viewport.width, height: viewport.height });
    await page.goto('/');

    const canvas = page.locator('canvas');
    const label = page.getByTestId('template-label');
    await expect(canvas).toHaveCount(1);
    await expect(label).toBeVisible();

    const layout = await page.evaluate(() => {
      const canvas = document.querySelector('canvas');
      const label = document.querySelector<HTMLElement>('[data-testid="template-label"]');
      if (canvas === null || label === null) throw new Error('template viewport is missing');
      const c = canvas.getBoundingClientRect();
      const l = label.getBoundingClientRect();
      return {
        scrollWidth: document.documentElement.scrollWidth,
        scrollHeight: document.documentElement.scrollHeight,
        viewportWidth: window.innerWidth,
        viewportHeight: window.innerHeight,
        canvas: { left: c.left, top: c.top, right: c.right, bottom: c.bottom, width: c.width, height: c.height },
        label: { left: l.left, top: l.top, right: l.right, bottom: l.bottom },
      };
    });
    expect(layout.scrollWidth).toBe(layout.viewportWidth);
    expect(layout.scrollHeight).toBe(layout.viewportHeight);
    expect(layout.canvas.width).toBeGreaterThan(0);
    expect(layout.canvas.height).toBeGreaterThan(0);
    expect(layout.canvas.left).toBeGreaterThanOrEqual(0);
    expect(layout.canvas.top).toBeGreaterThanOrEqual(0);
    expect(layout.canvas.right).toBeLessThanOrEqual(layout.viewportWidth);
    expect(layout.canvas.bottom).toBeLessThanOrEqual(layout.viewportHeight);
    expect(layout.label.left).toBeGreaterThanOrEqual(layout.canvas.left);
    expect(layout.label.top).toBeGreaterThanOrEqual(layout.canvas.top);
    expect(layout.label.right).toBeLessThanOrEqual(layout.canvas.right);
    expect(layout.label.bottom).toBeLessThanOrEqual(layout.canvas.bottom);

    await label.evaluate((element) => {
      element.style.visibility = 'hidden';
    });
    const pixels = PNG.sync.read(await canvas.screenshot());
    let differsFromClear = false;
    for (let index = 0; index < pixels.data.length; index += 4) {
      const [red, green, blue] = [pixels.data[index]!, pixels.data[index + 1]!, pixels.data[index + 2]!];
      if (red !== 7 || green !== 18 || blue !== 23) {
        differsFromClear = true;
        break;
      }
    }
    expect(differsFromClear).toBe(true);
  });
}
