# WiseRecruiter

WiseRecruiter is an ASP.NET Core MVC application for managing the hiring lifecycle — including jobs, candidates, interviews, scorecards, and recommendations.

The system is admin-driven and designed to support structured evaluation workflows while remaining flexible for custom hiring processes.

---

## 🧾 Overview

Core capabilities:

- Job and stage management (including fixed + custom stages)
- Candidate tracking across stages
- Interview scheduling and evaluation
- Scorecard creation and feedback capture
- Candidate recommendations (multi-stage)
- Resume review workflows
- Search and filtering across candidates/jobs

---

## 🏗️ Architecture

The application follows a **Controller → Service → DbContext** pattern:

Controller → Service → AppDbContext

### Responsibilities

- **Controllers**
  - Orchestrate requests
  - Handle routing and HTTP concerns
  - Must remain thin (no business logic)

- **Services**
  - Own all business logic
  - Handle validation, workflows, and side effects
  - Are unit-testable

- **AppDbContext (EF Core)**
  - Data access layer
  - No business logic

- **ViewModels**
  - Used for UI binding
  - Not treated as domain DTOs (avoiding DTO explosion)

---

## 📦 Domain Areas

The system is organized around these domains:

- **Jobs & Stages**
  - Fixed stages (Application, Screen, Offer, Hired)
  - Custom per-job stages between fixed stages

- **Candidates**
  - Applications tied to jobs
  - Stage progression and lifecycle tracking

- **Interviews**
  - Scheduling and interviewer assignment
  - Linked to candidates and stages

- **Scorecards**
  - Structured evaluation forms
  - Linked to interviews and candidates

- **Recommendations**
  - Stage-based candidate recommendations
  - Draft + submit workflows

- **Resume Review**
  - Resume viewing and advancement flow

- **Search**
  - Candidate and job filtering APIs

- **Email Templates & Analytics**
  - Supporting features for admin workflows

---

## 🧪 Testing Strategy

The project uses a mix of **integration and unit tests**.

### Integration Tests
- Exercise controllers end-to-end
- Validate routing, responses, and DB behavior
- Located under: `WiseRecruiter.Tests/Integration`

### Unit Tests
- Target service-layer logic
- Validate business rules in isolation
- Located under: `WiseRecruiter.Tests/Unit`

### Test Factory Pattern
- `AdminControllerFactory` centralizes controller construction
- Prevents duplication across tests
- Makes refactoring safer

---

## ⚠️ Important Testing Rule

Always use:

dotnet build  
dotnet test

Do **NOT** use `dotnet run` during automated workflows or refactoring — it can cause file locking issues in this environment.

---

## 🚧 Refactor Status (Active Work)

The codebase is currently undergoing a **controlled refactor** to decompose a large `AdminController`.

### Completed
- Recommendation workflows extracted to services
- Stage movement logic extracted
- Interview cancellation extracted (with full test coverage)
- Recommendation API split into dedicated controller

### In Progress
- Scorecard creation extraction
- Interview creation extraction

### Planned
- Split AdminController into domain controllers:
  - CandidateController
  - InterviewController
  - ScorecardController
  - JobController
  - SearchController

---

## 📐 Engineering Conventions

These rules are actively enforced:

- Controllers must remain **thin**
- No direct `_context` usage in controllers (target state)
- Services own all business logic
- Preserve behavior when refactoring (no silent changes)
- Avoid unnecessary abstractions
- Avoid DTO proliferation unless justified
- Prefer explicit logic over “clever” patterns

---

## 🛠️ Getting Started

Build and test the project:

dotnet build  
dotnet test

If running locally (optional):

dotnet run

---

## 🧭 Future Direction

- Complete AdminController decomposition
- Standardize command/query service patterns
- Reduce duplication in search/filter logic
- Continue increasing unit test coverage for services

---

## 📌 Notes

This project is intentionally evolving toward a **service-oriented MVC architecture** while maintaining full backward compatibility via tests.

Refactors are done incrementally with strict validation to avoid regressions.
