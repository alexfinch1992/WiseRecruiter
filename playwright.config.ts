import { defineConfig, devices } from '@playwright/test';
import path from 'path';

const authFile = path.join(__dirname, 'playwright/.auth/user.json');

export default defineConfig({
  testDir: './tests',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI ? 1 : undefined,
  reporter: 'html',

  use: {
    baseURL: 'http://localhost:5236',
    trace: 'on-first-retry',
  },

  webServer: {
    command: 'dotnet run --project JobPortal',
    url: 'http://localhost:5236',
    reuseExistingServer: true,
    timeout: 120_000,
  },

  projects: [
    // 1. Run the auth setup first; stores cookies to playwright/.auth/user.json
    {
      name: 'setup',
      testMatch: /tests[\/\\]auth\.setup\.ts/,
    },

    // 2. E2E suite — authenticated, depends on setup
    {
      name: 'chromium',
      use: {
        ...devices['Desktop Chrome'],
        storageState: authFile,
      },
      testMatch: /tests[\/\\]e2e_suite\.spec\.ts/,
      dependencies: ['setup'],
    },
  ],
});
