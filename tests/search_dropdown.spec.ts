import { test, expect } from '@playwright/test';

/**
 * Search dropdown determinism test.
 *
 * Each test run creates its own unique Job and Candidate+Application so
 * assertions never depend on pre-existing seeded data.  Uniqueness is
 * guaranteed by a millisecond timestamp + short random suffix.
 */

const uid = () => `${Date.now().toString(36)}_${Math.random().toString(36).slice(2, 6)}`;

test.describe('Search Dropdown – Candidate Name + Job Title', () => {

  test('candidate result shows candidate name and exact job title', async ({ page }) => {
    const id          = uid();
    const jobTitle    = `SearchJob_${id}`;
    // SplitName splits on first space → firstName = candidateFirst, lastName = candidateLast
    const candidateFirst = `SrchFirst${id}`;   // no spaces — becomes the whole firstName
    const candidateLast  = 'SrchLast';
    const candidateEmail = `srch_${id}@test.invalid`;

    // ── 1. Create a unique job via the admin form ───────────────────────
    await page.goto('/Admin/Create');
    await page.locator('input[name="Title"]').fill(jobTitle);
    await page.locator('textarea[name="Description"]').fill('Playwright search dropdown test');
    await page.locator('button.btn-success.btn-lg').click();
    // Wait for redirect to the job index (URL may be /Admin or /Admin/Index)
    await page.waitForLoadState('networkidle');
    await expect(page).not.toHaveURL(/\/Admin\/Create/, { timeout: 10_000 });

    // Locate the JobDetail link for our new job and extract its ID
    const jobLink = page
      .locator('a[href*="JobDetail"]')
      .filter({ hasText: jobTitle })
      .first();
    await expect(jobLink).toBeVisible({ timeout: 10_000 });
    const jobHref = await jobLink.getAttribute('href');
    const jobId   = jobHref!.match(/(\d+)/)![1];
    expect(jobId, 'Could not extract job ID from href').toBeTruthy();

    // ── 2. Submit an application to auto-create the Candidate ───────────
    await page.goto(`/Applications/Create?jobId=${jobId}`);
    await page.locator('input[name="Name"]').fill(`${candidateFirst} ${candidateLast}`);
    await page.locator('input[name="Email"]').fill(candidateEmail);
    await page.locator('input[name="City"]').fill('TestCity');
    // Provide a minimal valid .txt file to satisfy FileUploadHelper.ValidateResume
    await page.setInputFiles('input[name="resume"]', {
      name:     'resume.txt',
      mimeType: 'text/plain',
      buffer:   Buffer.from('search test resume'),
    });
    await page.locator('button.btn-success.btn-lg').click();
    // Wait for redirect to the applications list (URL may be /Applications or /Applications/Index)
    await page.waitForLoadState('networkidle');
    await expect(page).not.toHaveURL(/\/Applications\/Create/, { timeout: 10_000 });

    // ── 3. Search via the navbar dropdown ──────────────────────────────
    await page.goto('/Admin/SearchCandidates');
    await page.waitForLoadState('networkidle');

    await page.locator('#navSearchInput').fill(candidateFirst);

    const dropdown = page.locator('#navSearchResults');
    await expect(dropdown).toBeVisible({ timeout: 10_000 });

    // Locate the specific result row for our candidate (scoped to dropdown)
    const candidateResult = dropdown
      .locator('a')
      .filter({ hasText: `${candidateFirst} ${candidateLast}` })
      .first();
    await expect(candidateResult).toBeVisible({ timeout: 10_000 });

    // ── 4. Assert exact job title in the subtitle ───────────────────────
    const jobSubtitle = candidateResult.locator('.search-result-job');
    await expect(jobSubtitle).toBeVisible();
    await expect(jobSubtitle).toHaveText(jobTitle);
  });

});
