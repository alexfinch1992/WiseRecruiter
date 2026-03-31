import { test, expect } from '@playwright/test';

/**
 * Regression suite for three ManageTeam page bugs:
 *
 * 1. Role badge overwritten by job count – `querySelector('.badge')` grabs the
 *    FIRST `.badge` in the row which is the role badge, not the count badge.
 *    Fix: add `.role-badge` / `.job-count-badge` classes and target precisely.
 *
 * 2. Pink error bar on every page load – bare `asp-validation-summary="All"`
 *    with Bootstrap `alert-danger` classes renders a coloured box even when
 *    ModelState has no errors.
 *    Fix: remove the tag (this view has no server-side form POST).
 *
 * 3. No delete capability – users can be created but not removed.
 *    Fix: add `DeleteUser` action + Delete button per row.
 *
 * Each test creates its own unique user to avoid cross-test interference.
 */

const uid = () => `${Date.now().toString(36)}_${Math.random().toString(36).slice(2, 6)}`;

test.describe('ManageTeam – Bug Fixes', () => {

  // ── 1. Role Badge ─────────────────────────────────────────────────────
  test('role badge text is not overwritten when saving job access changes', async ({ page }) => {
    const id       = uid();
    const fullName = `RoleBug_${id}`;
    const email    = `rolebug_${id}@test.invalid`;
    const role     = 'HiringManager';

    // Create user via the "Create User" modal
    await page.goto('/AdminSettings/ManageTeam');
    await page.waitForLoadState('networkidle');
    await page.locator('[data-bs-target="#createUserModal"]').click();
    await page.locator('#cu-fullname').fill(fullName);
    await page.locator('#cu-email').fill(email);
    await page.locator('#cu-role').selectOption(role);
    await page.locator('#cu-submit-btn').click();
    // window.location.reload() is called on success; wait for the navigation
    await page.waitForLoadState('networkidle', { timeout: 15_000 });

    // Find user's row in the team table
    const row = page.locator('tr').filter({ hasText: fullName });
    await expect(row).toBeVisible({ timeout: 10_000 });

    // Open Manage Access modal and save immediately (0 or more jobs assigned)
    await row.locator('.manage-access-btn').click();
    await expect(page.locator('#manageAccessModal')).toBeVisible({ timeout: 5_000 });
    await page.locator('#ma-save-btn').click();

    // Wait for modal to close (JS hides it after the AJAX save succeeds)
    await expect(page.locator('#manageAccessModal')).not.toBeVisible({ timeout: 8_000 });

    // ── Key assertion: role badge must still show the role string, not a number ──
    // Requires the `.role-badge` class to be present and the JS to target `.job-count-badge`
    const roleBadge = row.locator('td').nth(1).locator('.role-badge');
    await expect(roleBadge).toHaveText(role);
  });

  // ── 2. Pink Error Bar ─────────────────────────────────────────────────
  test('no pink/red alert bar is visible on page load', async ({ page }) => {
    await page.goto('/AdminSettings/ManageTeam');
    await page.waitForLoadState('networkidle');

    // The bare `asp-validation-summary="All"` + `alert-danger` renders a coloured
    // box even with an empty ModelState — this assertion detects that.
    // After the fix (tag removed), the selector must find 0 elements.
    const errorBar = page.locator('.alert.alert-danger');
    await expect(errorBar).toHaveCount(0);
  });

  // ── 3. Delete User ────────────────────────────────────────────────────
  test('deleted user no longer appears in the team table', async ({ page }) => {
    const id       = uid();
    const fullName = `DelTest_${id}`;
    const email    = `deltest_${id}@test.invalid`;
    const role     = 'Recruiter';

    // Create user
    await page.goto('/AdminSettings/ManageTeam');
    await page.waitForLoadState('networkidle');
    await page.locator('[data-bs-target="#createUserModal"]').click();
    await page.locator('#cu-fullname').fill(fullName);
    await page.locator('#cu-email').fill(email);
    await page.locator('#cu-role').selectOption(role);
    await page.locator('#cu-submit-btn').click();
    await page.waitForLoadState('networkidle', { timeout: 15_000 });

    // Verify the newly created user is visible
    const row = page.locator('tr').filter({ hasText: fullName });
    await expect(row).toBeVisible({ timeout: 10_000 });

    // Register dialog handler BEFORE triggering the confirm dialog
    page.once('dialog', dialog => dialog.accept());
    await row.locator('.delete-user-btn').click();

    // After deletion, window.location.reload() is called — wait for it
    await page.waitForLoadState('networkidle', { timeout: 15_000 });

    // User must be gone
    await expect(page.locator('tr').filter({ hasText: fullName })).toHaveCount(0);
  });

});
