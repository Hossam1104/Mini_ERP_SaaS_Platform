import { defineConfig, devices } from '@playwright/test';

const baseURL = process.env['MESP_E2E_BASE_URL'] ?? 'http://127.0.0.1:4300';
const port = new URL(baseURL).port || '80';

export default defineConfig({
  testDir: './e2e',
  fullyParallel: true,
  forbidOnly: true,
  retries: process.env['CI'] ? 2 : 0,
  reporter: 'line',
  use: {
    baseURL,
    trace: 'on-first-retry',
  },
  webServer: {
    command: `npm run start -- --host 127.0.0.1 --port ${port}`,
    url: baseURL,
    reuseExistingServer: !process.env['CI'],
    timeout: 120000,
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
});
