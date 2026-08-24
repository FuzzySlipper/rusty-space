import { defineConfig } from '@playwright/test';

const port = Number(process.env['RUSTY_SPACE_PORT'] ?? '4191');

export default defineConfig({
  testDir: './tests',
  timeout: 30_000,
  workers: 1,
  use: {
    baseURL: `http://127.0.0.1:${String(port)}`,
    browserName: 'chromium',
    headless: true,
    launchOptions: { args: ['--enable-webgl', '--ignore-gpu-blocklist', '--use-angle=swiftshader'] },
  },
  webServer: {
    command: `../../target/debug/browser-host --addr 127.0.0.1:${String(port)}`,
    port,
    reuseExistingServer: false,
    timeout: 30_000,
  },
});
