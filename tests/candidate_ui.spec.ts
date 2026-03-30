import { test, expect } from '@playwright/test';

/**
 * Regression suite: Candidate Summary card correctness.
 *
 * Alice Johnson is always the first seeded candidate (Source = LinkedIn, has a
 * ResumePath). We navigate to her page dynamically via the search route so the
 * test is not brittle against auto-increment ID changes after a wipe+reseed.
 */

/** Navigate to Alice Johnson's CandidateDetails page. */
async function goToAlicePage(page: Parameters<Parameters<typeof test>[1]>[0]) {
  await page.goto('/Admin/SearchCandidates?searchQuery=alice');
  await page.waitForLoadState('networkidle');

  const viewLink = page.locator('a[href*="CandidateDetails"]').first();
  await expect(viewLink).toBeVisible({ timeout: 15_000 });

  // Navigate directly via the href attribute — avoids any possible click redirect ambiguity
  const href = await viewLink.getAttribute('href');
  if (!href) throw new Error('No CandidateDetails href found on search page');

  await page.goto(href);
  await page.waitForLoadState('networkidle');
}

test.describe('Candidate Summary & Stage Logic', () => {

  test('summary card integrity – Source badge and resume link placement', async ({ page }) => {
    await goToAlicePage(page);

    // The summary sidebar card must be visible
    const summaryCard = page.locator('.candidate-summary-card');
    await expect(summaryCard).toBeVisible({ timeout: 15_000 });

    // "View Resume" must exist inside .card-body (not the header)
    await expect(summaryCard.locator('.card-body a:has-text("View Resume")')).toBeVisible({ timeout: 10_000 });
    await expect(summaryCard.locator('.card-header a:has-text("View Resume")')).toHaveCount(0);

    // Alice Johnson was seeded with Source = LinkedIn — badge must reflect that
    await expect(summaryCard.locator('.badge:has-text("LinkedIn")')).toBeVisible({ timeout: 10_000 });
  });

  test('"Unassigned" stage is never displayed – valid pipeline state shown', async ({ page }) => {
    await goToAlicePage(page);

    const stageBadge = page.locator('.candidate-summary-card .stage-badge');
    await expect(stageBadge).toBeVisible({ timeout: 15_000 });

    const stageText = (await stageBadge.textContent())?.trim() ?? '';

    // The "Unassigned" fallback must have been eradicated
    expect(stageText).not.toBe('Unassigned');

    // Must be one of the recognised pipeline states
    expect(stageText).toMatch(/Applied|Technical Interview|Offer|Rejected/);
  });

});

