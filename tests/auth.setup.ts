import { test as setup } from '@playwright/test';
import path from 'path';

const authFile = path.join(__dirname, '../playwright/.auth/user.json');

setup('authenticate as admin', async ({ page }) => {
  await page.goto('/Account/Login');

  await page.fill('#username', 'admin@wiserecruiter.com');
  await page.fill('#password', 'Password123!');
  await page.click('button[type="submit"]');

  // Wait until we land on the Admin dashboard (redirect after successful login)
  await page.waitForURL(/\/Admin/i, { timeout: 10_000 });

  // Persist cookies + localStorage so other tests can reuse the session
  await page.context().storageState({ path: authFile });
});
