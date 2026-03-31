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

  test('no duplicate resume upload card – resume accessible only via summary link', async ({ page }) => {
    await goToAlicePage(page);

    // The summary sidebar must contain exactly one "View Resume" link
    const summaryCard = page.locator('.candidate-summary-card');
    await expect(summaryCard.locator('a:has-text("View Resume")')).toHaveCount(1);

    // There must be NO standalone resume card outside the summary sidebar
    // (identified by the card header text "Resume" with a file-alt icon)
    const resumeCardHeaders = page.locator('.card-header h5:has-text("Resume")');
    await expect(resumeCardHeaders).toHaveCount(0);

    // The general document upload box must still be present
    await expect(page.locator('#documentFile')).toBeVisible();
    await expect(page.locator('#documentType')).toBeVisible();
  });

  test('stage 1 submit uses correct endpoint and succeeds', async ({ page }) => {
    // Navigate to Alice's candidate page to discover her application ID
    await page.goto('/Admin/SearchCandidates?searchQuery=alice');
    await page.waitForLoadState('networkidle');

    const viewLink = page.locator('a[href*="CandidateDetails"]').first();
    await expect(viewLink).toBeVisible({ timeout: 15_000 });
    const href = await viewLink.getAttribute('href');
    if (!href) throw new Error('No CandidateDetails href found');
    const appId = href.match(/\d+$/)?.[0];
    if (!appId) throw new Error('Could not parse application ID from href');

    // Track any stray calls to the old wrong endpoint
    const wrongEndpointCalls: string[] = [];
    page.on('request', (req) => {
      if (req.method() === 'POST' && req.url().includes('/Admin/SubmitRecJson')) {
        wrongEndpointCalls.push(req.url());
      }
    });

    // Mock GetStage1RecJson to return Draft status so the Submit button is visible
    await page.route('**/Admin/GetStage1RecJson**', (route) =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          status: 'Draft',
          notes: 'Automated test note',
          strengths: 'Testing',
          concerns: '',
          hireRecommendation: true,
        }),
      }),
    );

    // Capture the submission request and mock a successful redirect response
    let submitPostData = '';
    await page.route('**/Recommendation/SubmitStage1', async (route) => {
      submitPostData = route.request().postData() ?? '';
      await route.fulfill({ status: 200, body: 'OK', contentType: 'text/html' });
    });

    await page.goto(`/Admin/WriteRecommendation?applicationId=${appId}`);
    await page.waitForLoadState('networkidle');

    // Wait for the React component to render the Submit button (only shows when status=Draft)
    const submitBtn = page.locator('button:has-text("Submit for Approval")');
    await expect(submitBtn).toBeVisible({ timeout: 10_000 });

    await submitBtn.click();

    // Assert correct endpoint was called
    await expect(async () => {
      expect(submitPostData).toContain(`applicationId=${appId}`);
    }).toPass({ timeout: 5_000 });

    // Assert antiforgery token was included in the request body
    expect(submitPostData).toContain('__RequestVerificationToken');

    // Assert the old wrong endpoint was never called
    expect(wrongEndpointCalls).toHaveLength(0);
  });

});

