import { defineConfig } from '@playwright/test';

const port = Number(process.env['RUSTY_TEMPLATE_PORT'] ?? '4191');

export default defineConfig({
  testDir: './tests',
  timeout: 30_000,
  use: {
    baseURL: `http://127.0.0.1:${String(port)}`,
    browserName: 'chromium',
    headless: true,
    launchOptions: { args: ['--enable-webgl', '--ignore-gpu-blocklist', '--use-angle=swiftshader'] },
  },
  webServer: {
    command: `pnpm exec vite --host 127.0.0.1 --port ${String(port)}`,
    port,
    reuseExistingServer: false,
    timeout: 30_000,
  },
});

