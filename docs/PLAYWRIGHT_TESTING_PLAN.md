# Playwright E2E Testing Plan — WiseRecruiter

**Authors:** Michael Ambrose, Alex Finch
**Date:** 10 April 2026
**Status:** Draft — for Alex's review

---

## Purpose

As WiseRecruiter grows, we can't keep manually testing every feature by clicking through the app and checking the Network tab. Playwright automates a real browser — it clicks buttons, fills forms, and checks results exactly like we do, but in seconds. This plan sets out what we have, what's missing, and what to build next.

## What we already have

We're in better shape than expected. There are already **22 test cases across 8 spec files**, plus a working auth setup and helper utilities.

### Existing test files

| File | What it tests | Tests |
|------|--------------|-------|
| `auth.setup.ts` | Logs in as admin, saves cookies for all other tests | Setup |
| `e2e_suite.spec.ts` | Full workflow: create job, submit application, recommendation approval, interview scheduling | 5 |
| `candidate_ui.spec.ts` | Candidate details page, summary card, stage badges, CSRF token handling | 4 |
| `search_dropdown.spec.ts` | Search bar creates job + candidate, verifies dropdown results | 1 |
| `email_functionality.spec.ts` | Email template preview modal and form validation | 3 |
| `manage_team_bugs.spec.ts` | ManageTeam regression bugs (role badges, error bars, user deletion) | 3 |
| `team_visibility.spec.ts` | New team members appear in the ManageTeam table | 2 |
| `branding.spec.ts` | Navbar colours and branding elements | 2 |
| `security.spec.ts` | Auth redirect for guests, X-Frame-Options header | 2 |

### What's already working well

- Auth setup stores cookies so tests don't need to log in every time
- Unique IDs on test data (timestamps) so tests don't interfere with each other
- Helper functions for common actions (create job, submit application, open candidate details)
- CSRF token extraction and validation
- HTML report generation (`npm run test:e2e:report`)

### How to run them

```
npm run test:e2e
```

This automatically starts the app (`dotnet run`), runs all tests, and generates an HTML report. To view the report:

```
npm run test:e2e:report
```

## What's missing

### Critical gaps (should build first)

1. **Hiring Request workflow** — We just built the HiringRequest feature (PRs #33, #34) but there are zero E2E tests covering it. This is our newest and most complex workflow with a state machine (Draft → Submitted → Approved/Rejected/MoreInfoRequested).

2. **CSRF protection on all POST endpoints** — PR #35 added CSRF tokens to every React POST request. We should have tests that verify these endpoints reject requests without valid tokens. `candidate_ui.spec.ts` already tests this pattern for Stage 1 submission — we need to extend it to cover the other endpoints (move stage, toggle alerts, save/submit recommendations).

3. **Job alert toggle** — No test for the toggle-job alert flow, which we just manually tested today.

4. **Stage 2 recommendations** — The e2e_suite covers Stage 1 approval but not Stage 2.

### Nice to have (build later)

5. **Role-based access** — Test that Recruiters can't access Admin-only pages, HiringManagers can only see their own requests, etc.

6. **Candidate filters sidebar** — Verify the React filter component renders and filtering works.

7. **Analytics page** — Basic smoke test that the page loads and charts render.

8. **Cleanup endpoint** — The tests are designed to call `/test/cleanup` to remove test data, but this endpoint doesn't exist yet. Not urgent (SQLite resets easily) but good for keeping the test database clean.

## Proposed new test files

### Priority 1 — Next week

**`hiring_request.spec.ts`**
- Admin creates a new hiring request (draft)
- Admin submits the request
- Approving executive approves/rejects/requests more info
- Activity timeline shows correct entries
- Ownership enforcement: user A can't edit user B's draft

**`csrf_protection.spec.ts`**
- Move stage POST without CSRF token → rejected (400/403)
- Toggle job alert POST without CSRF token → rejected
- Save recommendation draft POST without CSRF token → rejected
- Submit recommendation POST without CSRF token → rejected
- Same requests WITH valid CSRF token → succeed (200)

### Priority 2 — Following week

**`job_alerts.spec.ts`**
- Toggle alert on → verify state
- Toggle alert off → verify state
- Alert triggered when candidate moves stage

**`stage2_recommendation.spec.ts`**
- Write and save Stage 2 draft
- Submit Stage 2 recommendation
- Verify approval/rejection flow

### Priority 3 — When ready

**`role_access.spec.ts`**
- Recruiter can access candidate pages
- Recruiter cannot access admin settings
- HiringManager sees only their own hiring requests

**`candidate_filters.spec.ts`**
- Filter sidebar renders with correct facets
- Filtering by location narrows results
- Filtering by job narrows results
- Clear filters restores full list

## How we'll write the tests

We'll follow the same patterns already established in the codebase:

- **Unique IDs** using the `e2eName()` helper from `e2e_helpers.ts` to isolate test data
- **Helper functions** for repeated workflows (login, create job, navigate to pages)
- **No hardcoded IDs** — always navigate by searching or extracting IDs from URLs
- **CSRF tokens** extracted from page HTML when needed for POST requests

Each test file should be self-contained: create its own test data, run its checks, and not depend on other test files.

## Discussion points for Michael and Alex

1. **Running tests before merging PRs** — Should we make it a rule that `npm run test:e2e` passes before approving any PR? This would catch regressions automatically.

2. **CI pipeline** — The config already supports CI mode (1 worker, 2 retries). Do we want to set up GitHub Actions to run tests automatically on every PR?

3. **AI project context** — Michael has a `CLAUDE.md` file in the repo that his Claude sessions read for project context and workflow preferences. It's specific to Michael's setup (Cowork Desktop) and clearly marked as such — it won't interfere with Alex's workflow (ChatGPT + Claude Code in VS). Alex: if you want a similar context file for your tools, we can create one for you too.

4. **Test data cleanup** — Should we build the `/test/cleanup` endpoint now, or wait until the test database gets cluttered?

5. **Who writes what** — Suggestion: Michael takes `hiring_request.spec.ts` (since he built the feature), Alex takes `csrf_protection.spec.ts` (since he did the React refactor). Both with our respective AI tools helping.
