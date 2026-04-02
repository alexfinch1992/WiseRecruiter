import { test as setup, expect } from '@playwright/test';
import * as fs from 'fs';
import path from 'path';

const authFile = path.join(__dirname, '../playwright/.auth/user.json');

setup('authenticate as admin', async ({ page }) => {
  // Remove any stale auth file so we always generate a fresh cookie
  if (fs.existsSync(authFile)) {
    fs.unlinkSync(authFile);
  }

  await page.goto('/Account/Login');

  await page.fill('#username', 'admin@wiserecruiter.com');
  await page.fill('#password', 'Password123!');
  await page.click('button[type="submit"]');

  // Wait for post-login redirect. Use waitForURL with a glob that matches only
  // the actual Admin dashboard path, not returnUrl query params.
  // NOTE: /\/Admin/i is unanchored and matches returnUrl=%2FAdmin in failed
  // redirects — use URL.pathname check via page.goto instead.
  await page.waitForURL(url => url.pathname.startsWith('/Admin'), { timeout: 10_000 });

  // Explicitly verify the Admin role is in the session by navigating directly
  // to the Admin-only ManageTeam page. If the role claim is missing,
  // AccessDeniedPath="/" would redirect to "/" — the assertion catches that.
  await page.goto('/AdminSettings/ManageTeam');
  await expect(page).toHaveURL(/ManageTeam/, { timeout: 10_000 });

  // Persist cookies + localStorage now that Admin role access is confirmed
  await page.context().storageState({ path: authFile });
});
