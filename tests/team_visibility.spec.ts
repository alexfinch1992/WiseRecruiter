import { test, expect, Page } from '@playwright/test';

/**
 * ManageTeam visibility tests.
 *
 * Verifies that users created via the Admin UI (any role) appear in the
 * /AdminSettings/ManageTeam table immediately after creation.
 *
 * Auth: reuses stored admin session from playwright/.auth/user.json
 */

function uid(): string {
  return `${Date.now()}.${Math.floor(Math.random() * 9000) + 1000}`;
}

async function createUserViaModal(
  page: Page,
  fullName: string,
  email: string,
  role: string,
): Promise<void> {
  await page.goto('/AdminSettings/ManageTeam');
  await page.waitForLoadState('networkidle');

  // Open the Create User modal
  await page.click('button[data-bs-target="#createUserModal"]');
  await page.waitForSelector('#createUserModal.show', { timeout: 5_000 });

  // Fill in the form
  await page.fill('#cu-fullname', fullName);
  await page.fill('#cu-email', email);
  await page.selectOption('#cu-role', role);

  // Submit — on success the JS calls window.location.reload()
  await Promise.all([
    page.waitForNavigation({ waitUntil: 'networkidle', timeout: 15_000 }),
    page.click('#cu-submit-btn'),
  ]);
}

test.describe('ManageTeam — User Visibility', () => {

  test('newly created Recruiter appears in the team table', async ({ page }) => {
    const id       = uid();
    const fullName = `Test Recruiter ${id}`;
    const email    = `recruiter.${id}@test.wisetech.com`;

    await createUserViaModal(page, fullName, email, 'Recruiter');

    // The user row must be visible
    await expect(page.locator(`text=${fullName}`)).toBeVisible({ timeout: 10_000 });

    // The role badge (2nd column) must display "Recruiter", not the old hardcoded "HiringManager"
    const recruiterRow = page.locator('tr', { hasText: fullName });
    await expect(recruiterRow.locator('td').nth(1).locator('span.badge')).toHaveText('Recruiter', { timeout: 5_000 });
  });

  test('newly created Admin appears in the team table', async ({ page }) => {
    const id       = uid();
    const fullName = `Test Admin ${id}`;
    const email    = `admin.${id}@test.wisetech.com`;

    await createUserViaModal(page, fullName, email, 'Admin');

    // The user row must be visible
    await expect(page.locator(`text=${fullName}`)).toBeVisible({ timeout: 10_000 });

    // The role badge (2nd column) must display "Admin"
    const adminRow = page.locator('tr', { hasText: fullName });
    await expect(adminRow.locator('td').nth(1).locator('span.badge')).toHaveText('Admin', { timeout: 5_000 });
  });
});
