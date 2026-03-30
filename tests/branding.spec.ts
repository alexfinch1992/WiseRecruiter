import { test, expect } from '@playwright/test';

test.describe('Branding Audit', () => {

  test('navbar background-color is WiseTech Navy rgb(20, 30, 48)', async ({ page }) => {
    await page.goto('/');

    const navbar = page.locator('nav.navbar');
    await expect(navbar).toBeVisible();

    const bgColor = await navbar.evaluate(
      (el) => window.getComputedStyle(el).backgroundColor
    );

    expect(bgColor).toBe('rgb(20, 30, 48)');
  });

  test('logo / brand text is visible in the navbar', async ({ page }) => {
    await page.goto('/');

    const brand = page.locator('a.navbar-brand');
    await expect(brand).toBeVisible();

    // Either the image loaded successfully OR the fallback text is displayed.
    const logoImg  = brand.locator('img');
    const logoText = brand.locator('span');

    const imgVisible  = await logoImg.isVisible().catch(() => false);
    const textVisible = await logoText.isVisible().catch(() => false);

    expect(imgVisible || textVisible).toBe(true);
  });

});
