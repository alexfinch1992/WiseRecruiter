# WiseRecruiter

A full-featured recruiting and applicant tracking system built with ASP.NET Core MVC, React, and SQLite.

## Overview

WiseRecruiter helps hiring teams manage the entire recruitment pipeline — from job posting and candidate intake through interviews, evaluations, and hiring decisions. It includes structured scorecard-based evaluations, a two-stage recommendation approval workflow, interview scheduling, in-app alerts, and team-based job ownership.

## Tech Stack

| Layer | Technology |
|-------|-----------|
| **Backend** | ASP.NET Core 9.0 MVC, C# |
| **Database** | SQLite via EF Core |
| **Auth** | ASP.NET Core Identity (roles: Admin, Recruiter, HiringManager) |
| **Frontend** | Razor Views + React 18 (TypeScript, Vite) |
| **Testing** | xUnit, Moq, FluentAssertions, Playwright |
| **Container** | Docker (multi-stage build) |

## Getting Started

### Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Node.js 18+](https://nodejs.org/) (for React client build and Playwright tests)

### Run Locally

```bash
cd JobPortal
dotnet run
```

The app starts at `http://localhost:5236`. On first run, the database is auto-created with migrations and seed data.

### Default Admin Account

| Field | Value |
|-------|-------|
| Username | `admin@wiserecruiter.com` |
| Password | `Password123!` |

### Build

```bash
dotnet build
```

The client-side React app (`ClientApp/`) is built automatically during Debug builds via `npm install && npm run build`.

### Run Tests

**Unit & Integration tests:**

```bash
dotnet test
```

**E2E tests (Playwright):**

```bash
npm install
npx playwright install
npx playwright test
```

### Docker

```bash
cd JobPortal
docker build -t wiserecruiter .
docker run -p 8080:8080 wiserecruiter
```

## Project Structure

```
Application Site.sln
├── JobPortal/                      # Main web application
│   ├── Controllers/                # MVC + API controllers
│   ├── Models/                     # EF Core entities and ViewModels
│   ├── Views/                      # Razor views (Admin, Job, Candidate, etc.)
│   ├── Services/
│   │   ├── Interfaces/             # Service contracts
│   │   ├── Implementations/        # Service implementations
│   │   ├── Alerts/                 # Alert service, recipient/reviewer resolvers
│   │   └── Auth/                   # Custom claims factory
│   ├── Domain/
│   │   └── Recommendations/        # State machine for approval workflow
│   ├── Data/                       # AppDbContext, migrations
│   ├── Helpers/                    # DbInitializer, PasswordHasher, FileUpload
│   ├── ClientApp/                  # React 18 + TypeScript + Vite
│   │   └── src/components/         # AlertBell, CandidateDashboard, etc.
│   └── wwwroot/                    # Static assets, compiled JS bundles
├── WiseRecruiter.Tests/            # xUnit unit & integration tests
└── tests/                          # Playwright E2E specs
```

## Core Features

### Jobs & Pipeline

- Create jobs with title, description, and optional scorecard template
- Assign a **Primary Recruiter** (owner) and **Reviewers** per job
- Each job gets default pipeline stages (Screen, Interview, Offer) on creation
- Candidates move through stages with audit logging

### Candidates & Applications

- Candidates apply to jobs; multiple applications from the same email are unified
- Application status tracking: Active → Rejected / Withdrawn
- Stage progression: Applied → Screen → Interview → Offer → Hired
- Resume upload and inline PDF viewing
- Global search across candidates, jobs, and applications

### Interviews

- Schedule interviews linked to specific pipeline stages
- Assign multiple interviewers per interview
- Cancel or complete interviews with status tracking

### Scorecards & Evaluation

- **Scorecard Templates** define reusable evaluation criteria (facets)
- Facets are grouped into categories with display ordering
- Interviewers fill out scorecards with per-facet ratings and notes
- Overall recommendation captured per scorecard
- Analytics: average scores, facet breakdowns

### Recommendations (Two-Stage Approval)

- **Stage 1**: Initial recommendation with strengths, concerns, and hire recommendation
- **Stage 2**: Follow-up evaluation
- Workflow states: Draft → Submitted → Approved / Rejected / NeedsRevision
- Lead reviewer sets outcome: Proceed / MoreInfo / NotSuitable
- State machine enforces valid transitions

### Alerts & Notifications

- In-app notification system with bell icon dropdown
- Alerts fired on recommendation submissions to assigned reviewers
- Per-job alert subscriptions (toggle on/off)
- Alerts link directly to the relevant candidate detail page

### Team & Access Control

- **Roles**: Admin, Recruiter, HiringManager
- Admins manage team membership and reviewer assignments per job
- Job-level access control via `JobUser` assignments
- Stage transition authorization checks

### Email

- Template-based email system with placeholder substitution (`{{FirstName}}`)
- Admin-managed email templates

### Audit Trail

- Entity-level audit logging (entity name, action, changes JSON, user, timestamp)
- Compliance-ready change tracking

## Architecture Notes

- **Service Layer**: Controllers delegate to injected services for business logic; some controllers also access `AppDbContext` directly for simpler operations.
- **State Machine**: The recommendation approval workflow uses a generic `IStageStateMachine<TContext>` pattern with separate Stage1/Stage2 implementations.
- **React Embedding**: React components are built via Vite into `wwwroot/js/dist/` and mounted into Razor views using `createRoot`.
- **Legacy Compatibility**: The `AdminUser` table predates Identity. A custom `AdminClaimsPrincipalFactory` bridges the two by injecting `AdminId` claims at sign-in.

## License

Proprietary. All rights reserved.
