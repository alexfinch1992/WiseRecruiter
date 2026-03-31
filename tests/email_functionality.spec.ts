import { test, expect } from '@playwright/test';

// ── Step 2: Functional preview test ─────────────────────────────────────────

test.describe('Email Template Functionality', () => {

  test('Preview modal opens and contains parsed "John Doe" text', async ({ page }) => {
    await page.goto('/Admin/EmailTemplates');
    await page.waitForLoadState('networkidle');

    // Find the card for "Screening Invite" and click its Preview button
    const card = page.locator('.card', { hasText: 'Screening Invite' });
    await expect(card).toBeVisible();

    await card.locator('button', { hasText: 'Preview' }).click();

    // Wait for Bootstrap to add the 'show' class (animation complete)
    await page.waitForSelector('#previewModal.show', { timeout: 8_000 });

    const modal = page.locator('#previewModal');

    // Assert the parsed body contains "John Doe" ({{FirstName}} replaced server-side)
    const previewBody = modal.locator('#previewBody');
    await expect(previewBody).toContainText('John Doe');
  });

  test('Preview modal title shows template name', async ({ page }) => {
    await page.goto('/Admin/EmailTemplates');
    await page.waitForLoadState('networkidle');

    const card = page.locator('.card', { hasText: 'Screening Invite' });
    await card.locator('button', { hasText: 'Preview' }).click();

    await page.waitForSelector('#previewModal.show', { timeout: 8_000 });

    const modal = page.locator('#previewModal');
    await expect(modal.locator('#previewTemplateName')).toHaveText('Screening Invite');
  });

});

// ── Step 3: Data integrity — empty Name validation ───────────────────────────

test.describe('Email Template Data Integrity', () => {

  test('new template form with empty Name is blocked by browser validation', async ({ page }) => {
    await page.goto('/Admin/EmailTemplates');
    await page.waitForLoadState('networkidle');

    // Open the New Template modal
    await page.locator('button', { hasText: 'New Template' }).click();
    await page.waitForSelector('#newTemplateModal.show', { timeout: 8_000 });

    const modal = page.locator('#newTemplateModal');

    // Fill Subject and Body but leave Name blank
    await modal.locator('#editSubject').fill('Test Subject');
    await modal.locator('#editBody').fill('Test body content');

    // Attempt to submit — HTML5 `required` on #editName blocks the form
    await modal.locator('button[type="submit"]').click();

    // The browser's native validation prevents submission: still on the same page
    await expect(page).toHaveURL(/\/Admin\/EmailTemplates/i);

    // Modal should still be visible (form was not submitted)
    await expect(page.locator('#newTemplateModal.show')).toBeAttached();
  });

});
