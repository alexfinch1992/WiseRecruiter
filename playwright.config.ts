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

  projects: [
    // 1. Run the auth setup first; stores cookies to playwright/.auth/user.json
    {
      name: 'setup',
      testMatch: /auth\.setup\.ts/,
    },

    // 2. Branding & security tests — no auth needed
    {
      name: 'chromium-public',
      use: { ...devices['Desktop Chrome'] },
      testMatch: /branding\.spec\.ts|security\.spec\.ts/,
    },

    // 3. Authenticated tests — depend on setup
    {
      name: 'chromium',
      use: {
        ...devices['Desktop Chrome'],
        storageState: authFile,
      },
      testMatch: /email_functionality\.spec\.ts|candidate_ui\.spec\.ts|team_visibility\.spec\.ts|search_dropdown\.spec\.ts|manage_team_bugs\.spec\.ts/,
      dependencies: ['setup'],
    },
  ],
});
