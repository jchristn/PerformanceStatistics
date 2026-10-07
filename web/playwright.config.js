import { defineConfig } from '@playwright/test';

// End-to-end tests against a running stack (docker compose). Set TASKMGR_WEB_URL, TASKMGR_ADMIN_PASSWORD,
// and TASKMGR_E2E_HOST (a host the stack is monitoring).
export default defineConfig({
  testDir: './e2e',
  timeout: 60000,
  use: {
    baseURL: process.env.TASKMGR_WEB_URL || 'http://localhost:3000',
    screenshot: 'only-on-failure',
    viewport: { width: 1440, height: 900 },
  },
  reporter: [['list']],
});
