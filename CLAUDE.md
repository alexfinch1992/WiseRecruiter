# WiseRecruiter — Michael's Project Brief for Claude

> **Note:** This file is for Michael's AI workflow (Cowork Desktop / Claude). Alex uses a separate setup (ChatGPT + Claude Code in VS). If you're Alex's AI tool, this file is not intended for you — refer to the shared project docs in `/docs/` instead.

## About Michael (the developer)

- **Name:** Michael Ambrose
- **Email:** michael.ambrose@wisetechglobal.com
- **Skill level:** Beginner developer, learning on the fly
- **Working style:** Michael uses Cowork Desktop as his primary interface. Claude should:
  - Always explain things step by step in plain language
  - Never assume knowledge of git, .NET, React, or terminal commands
  - Provide exact commands to copy-paste, with explanations of what they do
  - When something goes wrong, walk through the fix patiently
  - Act as both a teacher (explaining concepts) and a subject matter expert (making technical decisions)
  - Flag potential risks before they become problems
  - When reviewing code, explain *why* something matters, not just *what* to change

## Team

- **Alex (alexfinch1992)** — Collaborator on WiseRecruiter. Also not a developer. Uses ChatGPT as his AI mentor, then works in Claude Code via VS Code. Communicates via GitHub and Teams.

## Project: WiseRecruiter

- **What it is:** A recruiting/applicant tracking system (ATS) for WiseTech Global
- **Tech stack:** ASP.NET Core 9 (C#), Razor Views + React 18 (TypeScript), SQLite, Entity Framework Core, xUnit tests
- **Repo:** https://github.com/alexfinch1992/WiseRecruiter
- **Run locally:** `cd JobPortal && dotnet run` → opens at http://localhost:5236
- **Editor:** VS Code (not full Visual Studio)
- **Test accounts:** Username `admin`, password `admin123` (legacy auth). Identity auth: `admin@wiserecruiter.com` / `Password123!`

### Architecture overview

- **Controllers/** — 15+ MVC controllers (AdminController is the largest at ~1000 lines, candidate for decomposition)
- **Services/** — Service layer with interfaces and implementations, dependency injection configured in Program.cs
- **Domain/** — State machines for recommendations and hiring requests
- **ClientApp/** — React 18 + TypeScript components, built with Vite, output to wwwroot/js/dist/
- **Data/** — AppDbContext with 23 entities, 24+ EF migrations
- **WiseRecruiter.Tests/** — 56 xUnit test files (unit + integration)
- **playwright/** — E2E framework installed but 0 spec files written yet

### Key patterns

- Role-based auth: Admin, Recruiter, HiringManager, TalentLead, ApprovingExecutive
- Dual auth system: legacy AdminUser table + ASP.NET Core Identity (being migrated)
- CSRF tokens passed to React components via `data-*` attributes on root elements
- State machine pattern for workflow transitions (recommendations, hiring requests)
- File encoding note: Some .cs files are UTF-16LE (shows as binary in git diff — use `iconv` to convert for reading)

## Project: WiseReferral

- **What it is:** Employee referral tracking system
- **Deployed on:** Render
- **Status:** Deployed and running (less active development currently)

## Current state (update this section regularly)

### Last updated: 2026-04-10

**Recent completed work:**
- PR #33 — HiringRequest data model and migration (merged)
- PR #34 — HiringRequest navigation and list page (review feedback addressed, awaiting re-review from Alex)
- PR #35 — React refactor: TS type alignment, window globals elimination, CSRF enforcement (approved by Michael, pending Alex's minor fixes before merge)
- Issue #39 — Tracking issue for service-layer authorization standardization (open)

**Known technical debt:**
- AdminController.cs is ~1000 lines and needs decomposition
- E2E tests (Playwright): framework installed, zero specs written
- React component tests: minimal coverage (testing libraries configured)
- Service-layer authorization is inconsistent between controllers and services
- Some Console.WriteLine calls may still exist (should use ILogger)

## Workflow preferences

- **Daily Digest:** Michael receives a daily dev digest summarizing PR activity, review requests, and action items
- **PR review process:** Claude reviews the diff, identifies issues, then walks Michael through testing locally before approving
- **Git workflow:** Always explain git commands before running them. Michael works on feature branches, PRs go through Alex for review and vice versa
- **Testing:** Currently manual (run app, check Network tab in browser DevTools). Working toward automated testing with xUnit and Playwright
- **Communication with Alex:** Via GitHub PR comments and Microsoft Teams

## How Claude should start each session

1. Read this file first
2. Check for any Daily Digest files (DailyDigest-*.md) for recent activity
3. Ask Michael what he'd like to work on
4. If continuing previous work, check git status and recent commits to understand current state
