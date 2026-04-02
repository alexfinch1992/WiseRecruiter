import { test, expect, type Page } from '@playwright/test';
import { RUN_ID, e2eName } from './e2e_helpers';

// ─── shared setup helpers ─────────────────────────────────────────────────────

/**
 * Create a job via the admin form and return its numeric database ID.
 * Relies on the post-create redirect showing a JobDetail link with the ID.
 */
async function createJob(page: Page, title: string): Promise<string> {
    await page.goto('/Admin/Create');
    await page.locator('input[name="Title"]').fill(title);
    await page.locator('textarea[name="Description"]').fill('E2E automated test job');
    await page.locator('button.btn-success.btn-lg').click();

    const jobLink = page.locator('a[href*="JobDetail"]').filter({ hasText: title }).first();
    await expect(jobLink).toBeVisible({ timeout: 10_000 });

    const href = await jobLink.getAttribute('href');
    const match = href?.match(/(\d+)/);
    if (!match) throw new Error(`Could not extract job ID from href: ${href}`);
    return match[1];
}

/**
 * Submit a job application via the public form and wait for the success redirect.
 */
async function submitApplication(
    page: Page,
    jobId: string,
    name: string,
    email: string,
): Promise<void> {
    await page.goto(`/Applications/Create?jobId=${jobId}`);
    await page.locator('input[name="Name"]').fill(name);
    await page.locator('input[name="Email"]').fill(email);
    await page.locator('input[name="City"]').fill('TestCity');
    // Minimal plain-text file satisfies FileUploadHelper.ValidateResume
    await page.setInputFiles('input[name="resume"]', {
        name: 'resume.txt',
        mimeType: 'text/plain',
        buffer: Buffer.from('e2e test resume placeholder'),
    });
    await page.locator('button.btn-success.btn-lg').click();
    await expect(page).not.toHaveURL(/\/Applications\/Create/, { timeout: 10_000 });
}

/**
 * Navigate to Job Detail, find the candidate row by their display name, and
 * click through to CandidateDetails.  After this helper returns, `page` is on
 * the CandidateDetails page.
 */
async function openCandidateDetails(
    page: Page,
    jobId: string,
    candidateName: string,
): Promise<void> {
    await page.goto(`/Admin/JobDetail/${jobId}`);

    const row = page.locator('tr').filter({ hasText: candidateName }).first();
    await expect(row).toBeVisible({ timeout: 10_000 });
    await row.locator('a[href*="CandidateDetails"]').first().click();
    await page.waitForURL(/CandidateDetails/, { timeout: 10_000 });
}

/**
 * Extract the application/candidate numeric ID from the current CandidateDetails URL.
 * Handles both /CandidateDetails/123 (route param) and /CandidateDetails?id=123 (query).
 */
function extractIdFromUrl(url: string): string {
    const parsed = new URL(url);
    const fromRoute = parsed.pathname.match(/(\d+)$/)?.[1];
    const fromQuery = parsed.searchParams.get('id') ?? undefined;
    const id = fromRoute ?? fromQuery;
    if (!id) throw new Error(`Could not extract ID from URL: ${url}`);
    return id;
}

// ─── suite ───────────────────────────────────────────────────────────────────

test.describe('E2E Suite', () => {
    test.afterAll(async ({ request }) => {
        try {
            // When a /test/cleanup endpoint is added server-side, it will delete all
            // records whose names start with RUN_ID, keeping the DB tidy after test runs.
            await request.post('/test/cleanup', { data: { prefix: RUN_ID } });
        } catch {
            // Endpoint not yet implemented — unique RUN_ID names provide isolation.
        }
    });

    // ── Test 1: Admin Flow ────────────────────────────────────────────────────
    test('admin creates a job with a stage and sees both in job detail', async ({ page }) => {
        const jobTitle  = e2eName('Job');
        const stageName = e2eName('Stage');

        const jobId = await createJob(page, jobTitle);

        // Add a custom pipeline stage from the JobDetail inline form
        await page.goto(`/Admin/JobDetail/${jobId}`);

        // The "Interview Stages" section is collapsed by default (display:none).
        // Click the toggle button to expand it — this reveals the stageName input.
        await page.getByRole('button', { name: /toggle interview stages/i }).click();

        const stageInput = page.locator('input[name="stageName"]');
        await expect(stageInput).toBeVisible({ timeout: 10_000 });

        await stageInput.fill(stageName);
        await page.locator('button.btn-outline-success').filter({ hasText: 'Add Stage' }).click();

        // Both the job title header and the new stage name must be visible
        await expect(page.getByText(jobTitle)).toBeVisible();
        const stage = page
            .locator('#stagesContainer, #stagesCollapsedView')
            .getByText(stageName)
            .first();
        await expect(stage).toBeVisible({ timeout: 10_000 });
    });

    // ── Test 2: Candidate Flow ────────────────────────────────────────────────
    test('submitted application appears in the job detail applicants list', async ({ page }) => {
        const jobTitle = e2eName('CandJob');
        const appName  = e2eName('Applicant');
        const appEmail = `${RUN_ID}_cand@test.invalid`;

        const jobId = await createJob(page, jobTitle);
        await submitApplication(page, jobId, appName, appEmail);

        // Navigate to JobDetail and confirm the applicant row is listed
        await page.goto(`/Admin/JobDetail/${jobId}`);
        await expect(
            page.locator('tr').filter({ hasText: appName }).first(),
        ).toBeVisible({ timeout: 10_000 });
    });

    // ── Test 3: Recommendation Flow ───────────────────────────────────────────
    test('admin writes, submits, and approves a stage 1 recommendation', async ({ page }) => {
        const jobTitle = e2eName('RecJob');
        const appName  = e2eName('RecApplicant');
        const appEmail = `${RUN_ID}_rec@test.invalid`;

        const jobId = await createJob(page, jobTitle);
        await submitApplication(page, jobId, appName, appEmail);
        await openCandidateDetails(page, jobId, appName);

        const appId = extractIdFromUrl(page.url());

        // Open the Stage 1 WriteRecommendation page (React editor)
        await page.goto(`/Recommendation/Stage1?applicationId=${appId}`);

        // Wait for the React component to hydrate — the textareas are rendered
        // after the initial /Admin/GetStage1RecJson fetch resolves.
        const textareas = page.locator('#recommendation-editor-root textarea');
        await expect(textareas).toHaveCount(3, { timeout: 10_000 });

        // Fill the recommendation fields by position (Notes → Strengths → Concerns)
        await textareas.nth(0).fill('Strong communicator with solid relevant experience.');
        await textareas.nth(1).fill('Technical aptitude and clear problem-solving.');
        await textareas.nth(2).fill('None identified at this stage.');

        // Save Draft — the "Submit for Approval" button only appears after status becomes Draft
        const saveDraftButton = page.getByRole('button', { name: /save/i });
        await expect(saveDraftButton).toBeVisible({ timeout: 10_000 });
        await saveDraftButton.click();
        const submitForApprovalBtn = page.getByRole('button', { name: /submit/i });
        await expect(submitForApprovalBtn).toBeVisible({ timeout: 10_000 });

        // Submit for Approval — assert the outcome rather than a URL change
        await submitForApprovalBtn.click();

        // Stage 1 status must show "Awaiting admin review" (covers both nav and in-place update)
        await expect(
            page.getByText(/Awaiting admin review/i)
        ).toBeVisible({ timeout: 10_000 });

        // Approve the recommendation via the admin endpoint.
        // page.request shares the page's auth cookies. The CSRF token is read from
        // a hidden field rendered by ASP.NET on the current CandidateDetails page.
        const csrfToken = await page
            .locator('input[name="__RequestVerificationToken"]')
            .first()
            .inputValue();
        const approveResp = await page.request.post(
            `/Admin/Recommendations/Approve/${appId}`,
            {
                headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
                data: `__RequestVerificationToken=${encodeURIComponent(csrfToken)}`,
            },
        );
        expect(approveResp.ok()).toBeTruthy();

        // Reload and confirm the "Approved" success badge is visible
        await page.reload();
        await expect(
            page.locator('.badge.bg-success').filter({ hasText: 'Approved' }).first(),
        ).toBeVisible({ timeout: 5_000 });
    });

    // ── Test 4: Interview Flow ────────────────────────────────────────────────
    test('admin schedules an interview and then cancels it', async ({ page }) => {
        const jobTitle = e2eName('IntJob');
        const appName  = e2eName('IntApplicant');
        const appEmail = `${RUN_ID}_int@test.invalid`;

        const jobId = await createJob(page, jobTitle);
        await submitApplication(page, jobId, appName, appEmail);
        await openCandidateDetails(page, jobId, appName);

        // Build a datetime-local string 2 days in the future
        const future    = new Date(Date.now() + 2 * 24 * 60 * 60 * 1000);
        const pad       = (n: number) => String(n).padStart(2, '0');
        const dateLocal = [
            `${future.getFullYear()}-${pad(future.getMonth() + 1)}-${pad(future.getDate())}`,
            `T${pad(future.getHours())}:${pad(future.getMinutes())}`,
        ].join('');

        // Fill the schedule form
        await page.locator('input[name="scheduledAt"]').fill(dateLocal);

        // Select the first available stage option (enum values when no custom stages exist)
        await page.locator('select[name="selectedStage"]').selectOption({ index: 0 });

        // If Stage 1 approval hasn't been granted the Schedule button is disabled.
        // The page's onchange handler re-enables it when #bypassCheckbox is checked.
        const scheduleBtn = page.locator('#scheduleSubmitBtn');
        if (await scheduleBtn.getAttribute('disabled') !== null) {
            await page.locator('#bypassCheckbox').check();
            const bypassReason = page.locator('input[name="bypassReason"]');
            if (await bypassReason.isVisible()) {
                await bypassReason.fill('E2E test bypass');
            }
        }

        await scheduleBtn.click();

        // The new interview row shows an "Upcoming" status badge for a future date
        const upcomingRow = page
            .locator('tbody tr')
            .filter({ has: page.locator('.badge').filter({ hasText: 'Upcoming' }) })
            .first();
        await expect(upcomingRow).toBeVisible({ timeout: 10_000 });

        // Cancel the interview
        await upcomingRow.locator('button', { hasText: 'Cancel' }).click();

        // After cancellation the badge must show Cancelled (or Canceled)
        await expect(
            page.locator('.badge', { hasText: /(Cancelled|Canceled)/i })
        ).toBeVisible({ timeout: 10_000 });
    });

    // ── Test 5: Authorization Guard ───────────────────────────────────────────
    test('unauthenticated user is redirected to login for a protected admin page', async ({ browser }) => {
        // Explicitly pass storageState: undefined to guarantee no cookies or
        // auth state are inherited from the project-level storageState config.
        const context = await browser.newContext({ storageState: undefined });
        await context.clearCookies();
        const guestPage = await context.newPage();
        guestPage.setDefaultNavigationTimeout(10_000);

        try {
            await guestPage.goto('http://localhost:5236/Admin/Create');
            // ASP.NET Identity appends a returnUrl query param — match on pathname only
            await expect(guestPage).toHaveURL(/\/Account\/Login/i);
        } finally {
            await context.close();
        }
    });
});
