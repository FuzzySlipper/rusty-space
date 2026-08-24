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

test('a browser session can turn and thrust the ship', async ({ page }) => {
  await page.setViewportSize({ width: 1000, height: 760 });
  await page.goto('/');

  const label = page.getByTestId('space-label');
  await expect(page.locator('canvas')).toHaveCount(1);
  await expect(label).toContainText('pos', { timeout: 15_000 });

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
