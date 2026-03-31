import { test, expect } from '@playwright/test';

test.describe('Security Audit', () => {

  test('unauthenticated access to /Admin/EmailTemplates redirects to /Account/Login', async ({ page }) => {
    // Navigate without any auth cookies — Playwright starts with a clean context.
    await page.goto('/Admin/EmailTemplates');

    // ASP.NET Identity redirects with a returnUrl query param; check the pathname only.
    await expect(page).toHaveURL(/\/Account\/Login/i);
  });

  test('home page response includes X-Frame-Options: DENY header', async ({ request }) => {
    const response = await request.get('/');

    expect(response.ok()).toBe(true);

    const xFrameOptions = response.headers()['x-frame-options'];
    expect(xFrameOptions).toBeDefined();
    expect(xFrameOptions.toUpperCase()).toBe('DENY');
  });

});
