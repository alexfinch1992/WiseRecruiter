using JobPortal.Data;
using JobPortal.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace JobPortal.Helpers
{
    public static class DbInitializer
    {
        public static void Initialize(AppDbContext context)
        {
            // SeedDefaultFacets uses Facet table (ScorecardFacets removed)
            SeedDefaultFacets(context);
            BackfillDefaultFacetCategories(context); // assign CategoryId to seeded facets that were created without one
            SeedDefaultScorecardTemplate(context);
            NullifyOrphanedScorecardTemplateIds(context);

            // Seed admin users
            if (!context.AdminUsers.Any())
            {
                var adminUsers = new[]
                {
                    new AdminUser { Username = "admin",  PasswordHash = PasswordHasher.Hash("admin123") },
                    new AdminUser { Username = "alex",   PasswordHash = PasswordHasher.Hash("admin123") },
                    new AdminUser { Username = "taylor", PasswordHash = PasswordHasher.Hash("admin123") },
                    new AdminUser { Username = "jordan", PasswordHash = PasswordHasher.Hash("admin123") },
                    new AdminUser { Username = "casey",  PasswordHash = PasswordHasher.Hash("admin123") },
                };
                context.AdminUsers.AddRange(adminUsers);
                context.SaveChanges();
            }

            // Seed sample jobs
            if (!context.Jobs.Any())
            {
                var jobDefs = new[]
                {
                    new
                    {
                        Title = "Senior Product Manager",
                        Description = "We are looking for a strategic Senior Product Manager to own the roadmap for our core platform. You will work closely with engineering, design, and go-to-market teams to define and ship products that delight customers. Ideal candidates have 5+ years of product management experience, strong data-driven decision-making, and a track record of leading cross-functional initiatives.",
                        CustomStages = new[] { "Product Case Study", "Leadership Panel" }
                    },
                    new
                    {
                        Title = "Backend Engineer",
                        Description = "Join our platform engineering team as a Backend Engineer building the services and APIs that power WiseRecruiter. You'll work with .NET, EF Core, and cloud infrastructure (AWS/Azure) to design scalable, testable, and secure systems. We value engineers who care about craft, code reviews, and mentoring.",
                        CustomStages = new[] { "Coding Challenge", "System Design Round" }
                    },
                    new
                    {
                        Title = "UX Designer",
                        Description = "We're hiring a UX Designer passionate about simplifying complex workflows for talent acquisition professionals. You will own end-to-end design — from user research and wireframes to polished Figma prototypes and developer handoff. 3+ years of product design experience required, with a portfolio demonstrating impact.",
                        CustomStages = new[] { "Portfolio Review", "Design Exercise" }
                    },
                    new
                    {
                        Title = "Data Analyst",
                        Description = "As a Data Analyst you will turn hiring pipeline data into actionable insights for our customers and internal teams. You'll build dashboards, run ad-hoc analyses, and partner with product to instrument new features. Strong SQL, Python or R skills, and experience with BI tools (Looker, Tableau) required.",
                        CustomStages = Array.Empty<string>()
                    },
                };

                foreach (var def in jobDefs)
                {
                    var job = new Job { Title = def.Title, Description = def.Description };
                    context.Jobs.Add(job);
                    context.SaveChanges();

                    // Standard pipeline stages
                    var standardStages = new[] { "Applied", "Screen", "Interview", "Offer", "Hired" };
                    for (int i = 0; i < standardStages.Length; i++)
                        context.JobStages.Add(new JobStage { JobId = job.Id, Name = standardStages[i], Order = i });

                    // Custom interview-round stages (appended after standard ones)
                    for (int i = 0; i < def.CustomStages.Length; i++)
                        context.JobStages.Add(new JobStage { JobId = job.Id, Name = def.CustomStages[i], Order = standardStages.Length + i });

                    context.SaveChanges();
                }
            }

// Wipe and reseed candidate data (always runs to keep demo data consistent)
        WipeAndReseedCandidates(context);

            BackfillCandidateRelationships(context);
        }

        private static void SeedDefaultFacets(AppDbContext context)
        {
            if (context.Facets.Any()) return;

            // Categories come from HasData in AppDbContext and are always present after migrations.
            var technical = context.Categories.FirstOrDefault(c => c.Name == "Technical");
            var softSkills = context.Categories.FirstOrDefault(c => c.Name == "Soft Skills");

            context.Facets.AddRange(
                new Facet { Name = "Technical Skills", CategoryId = technical?.Id },
                new Facet { Name = "Communication", CategoryId = softSkills?.Id },
                new Facet { Name = "Problem Solving", CategoryId = technical?.Id }
            );
            context.SaveChanges();
        }

        /// <summary>
        /// Assigns categories to the default seeded facets on databases where they were
        /// originally inserted without a CategoryId (e.g. existing Render deployments).
        /// Idempotent: only updates rows where CategoryId is null.
        /// </summary>
        private static void BackfillDefaultFacetCategories(AppDbContext context)
        {
            var categoryLookup = context.Categories
                .ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);

            var assignments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Technical Skills", "Technical" },
                { "Communication", "Soft Skills" },
                { "Problem Solving", "Technical" }
            };

            var changed = false;
            foreach (var (facetName, categoryName) in assignments)
            {
                if (!categoryLookup.TryGetValue(categoryName, out var category)) continue;

                var facet = context.Facets
                    .FirstOrDefault(f => f.Name == facetName && f.CategoryId == null);
                if (facet == null) continue;

                facet.CategoryId = category.Id;
                changed = true;
            }

            if (changed)
                context.SaveChanges();
        }

        private static void SeedDefaultScorecardTemplate(AppDbContext context)
        {
            var template = context.ScorecardTemplates.FirstOrDefault(t => t.Name == "Default Scorecard");
            if (template == null)
            {
                template = new ScorecardTemplate
                {
                    Name = "Default Scorecard"
                };
                context.ScorecardTemplates.Add(template);
                context.SaveChanges();
            }

            var activeFacets = context.Facets
                .Where(f => f.IsActive)
                .OrderBy(f => f.Name)
                .ToList();

            foreach (var facet in activeFacets)
            {
                // Use FacetId (not ScorecardFacetId — legacy column, always 0 on new inserts)
                var existingLink = context.ScorecardTemplateFacets.Any(tf =>
                    tf.ScorecardTemplateId == template.Id &&
                    tf.FacetId == facet.Id);

                if (existingLink)
                    continue;

                context.ScorecardTemplateFacets.Add(new ScorecardTemplateFacet
                {
                    ScorecardTemplateId = template.Id,
                    FacetId = facet.Id
                });
            }

            context.SaveChanges();
        }

        private static void BackfillCandidateRelationships(AppDbContext context)
        {
            var applications = context.Applications.ToList();

            // Collect all applications needing a Candidate, batch-create missing ones, save once
            var newCandidatesByEmail = new Dictionary<string, Candidate>(StringComparer.OrdinalIgnoreCase);
            foreach (var application in applications)
            {
                if (application.CandidateId > 0)
                    continue;

                var email = application.Email ?? string.Empty;
                if (newCandidatesByEmail.ContainsKey(email))
                    continue;

                var existingCandidate = context.Candidates.FirstOrDefault(c => c.Email == email);
                if (existingCandidate != null)
                {
                    newCandidatesByEmail[email] = existingCandidate;
                }
                else
                {
                    var (firstName, lastName) = SplitName(application.Name);
                    var candidate = new Candidate
                    {
                        FirstName = firstName,
                        LastName = lastName,
                        Email = email,
                        CreatedAt = DateTime.UtcNow
                    };
                    context.Candidates.Add(candidate);
                    newCandidatesByEmail[email] = candidate;
                }
            }
            context.SaveChanges(); // persist all new candidates at once

            foreach (var application in applications)
            {
                if (application.CandidateId > 0)
                    continue;

                var email = application.Email ?? string.Empty;
                if (newCandidatesByEmail.TryGetValue(email, out var candidate))
                    application.CandidateId = candidate.Id;
            }
            context.SaveChanges();

            var scorecards = context.Scorecards.ToList();
            var validCandidateIds = context.Candidates.Select(c => c.Id).ToHashSet();

            // First pass: remap legacy scorecards (CandidateId used to store Application.Id)
            foreach (var scorecard in scorecards)
            {
                if (validCandidateIds.Contains(scorecard.CandidateId))
                    continue;

                var legacyApplication = context.Applications.FirstOrDefault(a => a.Id == scorecard.CandidateId);
                if (legacyApplication != null && legacyApplication.CandidateId > 0)
                    scorecard.CandidateId = legacyApplication.CandidateId;
            }

            // Second pass: create placeholder Candidates for any still-orphaned scorecards (batched)
            var orphanedScorecards = scorecards
                .Where(s => !validCandidateIds.Contains(s.CandidateId))
                .ToList();

            if (orphanedScorecards.Any())
            {
                var placeholderPairs = orphanedScorecards.Select(s => new
                {
                    Scorecard = s,
                    Placeholder = new Candidate
                    {
                        FirstName = "Legacy",
                        LastName = "Candidate",
                        Email = $"legacy-candidate-{s.Id}@local.invalid",
                        CreatedAt = DateTime.UtcNow
                    }
                }).ToList();

                foreach (var pair in placeholderPairs)
                    context.Candidates.Add(pair.Placeholder);

                context.SaveChanges(); // persist all placeholders at once to get their Ids

                foreach (var pair in placeholderPairs)
                    pair.Scorecard.CandidateId = pair.Placeholder.Id;
            }

            context.SaveChanges();
        }

        private static (string firstName, string lastName) SplitName(string? fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
                return ("Unknown", "Candidate");

            var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1)
                return (parts[0], "Candidate");

            return (parts[0], string.Join(' ', parts.Skip(1)));
        }

        /// <summary>
        /// Wipes all candidate-related data and reseeds a demo-ready dataset.
        /// Always runs on startup to keep demo data consistent (idempotent via wipe-then-seed).
        /// </summary>
        public static void WipeAndReseedCandidates(AppDbContext context)
        {
            // Wipe in FK-safe order
            context.Interviews.RemoveRange(context.Interviews.ToList());
            context.CandidateRecommendations.RemoveRange(context.CandidateRecommendations.ToList());
            context.Applications.RemoveRange(context.Applications.ToList());
            context.Candidates.RemoveRange(context.Candidates.ToList());
            context.SaveChanges();

            var jobs = context.Jobs.Include(j => j.Stages).ToList();
            if (!jobs.Any()) return;

            // ── Resume paths (distributed alternately) ──────────────────────────────
            var resumePaths = new[]
            {
                "/documents/john_doe_product_manager_resume.pdf",
                "/documents/jane_doe_product_manager_resume.pdf",
            };
            int resumeCounter = 0;
            string NextResume() => resumePaths[resumeCounter++ % resumePaths.Length];

            var cities = new[] { "New York", "San Francisco", "Los Angeles", "Chicago", "Boston",
                                  "Seattle", "Austin", "Denver", "Portland", "Miami" };
            var baseDate = DateTime.UtcNow;
            int cityIdx = 0;
            string NextCity() => cities[cityIdx++ % cities.Length];

            // Helper: persist a candidate and return it
            Candidate AddCandidate(string first, string last, string email, CandidateSource source, int daysAgo)
            {
                var c = new Candidate
                {
                    FirstName = first, LastName = last, Email = email,
                    CreatedAt = baseDate.AddDays(-daysAgo), Source = source,
                };
                context.Candidates.Add(c);
                context.SaveChanges();
                return c;
            }

            // Helper: persist an application and return it
            Application AddApplication(Candidate candidate, Job job, ApplicationStage stage,
                int? jobStageId, int daysAgo, bool movedWithoutApproval = false)
            {
                var app = new Application
                {
                    Name              = $"{candidate.FirstName} {candidate.LastName}",
                    Email             = candidate.Email,
                    City              = NextCity(),
                    JobId             = job.Id,
                    CandidateId       = candidate.Id,
                    Stage             = stage,
                    CurrentJobStageId = jobStageId,
                    AppliedDate       = baseDate.AddDays(-daysAgo),
                    ResumePath        = NextResume(),
                    MovedWithoutStage1Approval = movedWithoutApproval,
                };
                context.Applications.Add(app);
                context.SaveChanges();
                return app;
            }

            // Helper: add Stage1 recommendation
            void AddRec(int applicationId, RecommendationStatus status, bool approved,
                string? summary = null)
            {
                context.CandidateRecommendations.Add(new CandidateRecommendation
                {
                    ApplicationId      = applicationId,
                    Stage              = RecommendationStage.Stage1,
                    Status             = status,
                    Summary            = summary,
                    HireRecommendation = approved ? true : null,
                    ApprovedByUserId   = approved ? 1 : null,
                    ApprovedUtc        = approved ? DateTime.UtcNow.AddDays(-2) : null,
                    LastUpdatedUtc     = DateTime.UtcNow.AddDays(-3),
                });
                context.SaveChanges();
            }

            // Helper: add Interview record
            void AddInterview(int candidateId, int applicationId, int jobStageId, int daysFromNow)
            {
                context.Interviews.Add(new Interview
                {
                    CandidateId   = candidateId,
                    ApplicationId = applicationId,
                    JobStageId    = jobStageId,
                    ScheduledAt   = baseDate.AddDays(daysFromNow),
                    IsCancelled   = false,
                });
                context.SaveChanges();
            }

            // Look up jobs by title
            Job? FindJob(string title) => jobs.FirstOrDefault(j => j.Title == title);
            JobStage? FindStage(Job? job, string name) =>
                job?.Stages?.FirstOrDefault(s => s.Name == name);

            var jobPM   = FindJob("Senior Product Manager");
            var jobBE   = FindJob("Backend Engineer");
            var jobUX   = FindJob("UX Designer");
            var jobDA   = FindJob("Data Analyst");

            // Fall back gracefully to first available stages if custom titles differ
            JobStage? pmInterview  = FindStage(jobPM, "Interview") ?? jobPM?.Stages?.OrderBy(s => s.Order).FirstOrDefault();
            JobStage? beInterview  = FindStage(jobBE, "Interview") ?? jobBE?.Stages?.OrderBy(s => s.Order).FirstOrDefault();
            JobStage? uxInterview  = FindStage(jobUX, "Interview") ?? jobUX?.Stages?.OrderBy(s => s.Order).FirstOrDefault();
            JobStage? daInterview  = FindStage(jobDA, "Interview") ?? jobDA?.Stages?.OrderBy(s => s.Order).FirstOrDefault();

            // ════════════════════════════════════════════════════════════════════════
            // SENIOR PRODUCT MANAGER — 7 candidates
            // ════════════════════════════════════════════════════════════════════════
            if (jobPM != null)
            {
                // Hired
                var alice = AddCandidate("Alice", "Johnson", "alice.johnson@example.com", CandidateSource.LinkedIn, 30);
                var appAlice = AddApplication(alice, jobPM, ApplicationStage.Hired, pmInterview?.Id, 29);
                AddRec(appAlice.Id, RecommendationStatus.Approved, approved: true,
                    "Exceptional product sense and cross-functional leadership. Strong hire.");
                AddInterview(alice.Id, appAlice.Id, pmInterview!.Id, -7);

                // Offer
                var bob = AddCandidate("Bob", "Smith", "bob.smith@example.com", CandidateSource.Referral, 27);
                var appBob = AddApplication(bob, jobPM, ApplicationStage.Offer, null, 26);
                AddRec(appBob.Id, RecommendationStatus.Approved, approved: true,
                    "Strong strategic thinker with proven delivery at scale.");

                // Interview — Stage1 Approved
                var carol = AddCandidate("Carol", "Williams", "carol.williams@example.com", CandidateSource.LinkedIn, 24);
                var appCarol = AddApplication(carol, jobPM, ApplicationStage.Interview, pmInterview?.Id, 23);
                AddRec(appCarol.Id, RecommendationStatus.Approved, approved: true,
                    "Excellent communication and data-driven approach.");
                AddInterview(carol.Id, appCarol.Id, pmInterview!.Id, 3);

                // Interview — NO Stage1 approval (triggers MovedWithoutStage1Approval flag)
                var david = AddCandidate("David", "Brown", "david.brown@example.com", CandidateSource.Applicant, 22);
                var appDavid = AddApplication(david, jobPM, ApplicationStage.Interview, pmInterview?.Id, 21,
                    movedWithoutApproval: true);
                AddInterview(david.Id, appDavid.Id, pmInterview!.Id, 5);

                // Screen
                var emma = AddCandidate("Emma", "Davis", "emma.davis@example.com", CandidateSource.LinkedIn, 19);
                AddApplication(emma, jobPM, ApplicationStage.Screen, null, 18);

                var frank = AddCandidate("Frank", "Wilson", "frank.wilson@example.com", CandidateSource.Applicant, 16);
                AddApplication(frank, jobPM, ApplicationStage.Screen, null, 15);

                // Applied
                var grace = AddCandidate("Grace", "Lee", "grace.lee@example.com", CandidateSource.Internal, 13);
                AddApplication(grace, jobPM, ApplicationStage.Applied, null, 12);
            }

            // ════════════════════════════════════════════════════════════════════════
            // BACKEND ENGINEER — 7 candidates (including shared multi-job candidate)
            // ════════════════════════════════════════════════════════════════════════
            if (jobBE != null)
            {
                // Hired
                var henry = AddCandidate("Henry", "Martinez", "henry.martinez@example.com", CandidateSource.LinkedIn, 29);
                var appHenry = AddApplication(henry, jobBE, ApplicationStage.Hired, beInterview?.Id, 28);
                AddRec(appHenry.Id, RecommendationStatus.Approved, approved: true,
                    "Outstanding systems design skills and excellent culture fit.");
                AddInterview(henry.Id, appHenry.Id, beInterview!.Id, -10);

                // Offer
                var iris = AddCandidate("Iris", "Chen", "iris.chen@example.com", CandidateSource.Referral, 26);
                var appIris = AddApplication(iris, jobBE, ApplicationStage.Offer, null, 25);
                AddRec(appIris.Id, RecommendationStatus.Approved, approved: true,
                    "Deep distributed systems expertise; clear communicator.");

                // Interview — Stage1 Approved
                var jack = AddCandidate("Jack", "Anderson", "jack.anderson@example.com", CandidateSource.LinkedIn, 23);
                var appJack = AddApplication(jack, jobBE, ApplicationStage.Interview, beInterview?.Id, 22);
                AddRec(appJack.Id, RecommendationStatus.Approved, approved: true,
                    "Solid coding skills and pragmatic architectural judgment.");
                AddInterview(jack.Id, appJack.Id, beInterview!.Id, 2);

                // Interview — Stage1 Draft only (no approval → MovedWithoutStage1Approval)
                var karen = AddCandidate("Karen", "Taylor", "karen.taylor@example.com", CandidateSource.Applicant, 20);
                var appKaren = AddApplication(karen, jobBE, ApplicationStage.Interview, beInterview?.Id, 19,
                    movedWithoutApproval: true);
                AddRec(appKaren.Id, RecommendationStatus.Draft, approved: false);
                AddInterview(karen.Id, appKaren.Id, beInterview!.Id, 6);

                // Screen
                var leo = AddCandidate("Leo", "Thomas", "leo.thomas@example.com", CandidateSource.LinkedIn, 17);
                AddApplication(leo, jobBE, ApplicationStage.Screen, null, 16);

                // Applied
                var monica = AddCandidate("Monica", "Garcia", "monica.garcia@example.com", CandidateSource.Applicant, 14);
                AddApplication(monica, jobBE, ApplicationStage.Applied, null, 13);

                // ── MULTI-JOB CANDIDATE (first application — Backend Engineer) ──────
                var alex = AddCandidate("Alex", "Martinez", "alex.shared@example.com", CandidateSource.LinkedIn, 25);
                var appAlexBE = AddApplication(alex, jobBE, ApplicationStage.Interview, beInterview?.Id, 24);
                AddRec(appAlexBE.Id, RecommendationStatus.Submitted, approved: false,
                    "Strong full-stack background; recommendation under review.");
                AddInterview(alex.Id, appAlexBE.Id, beInterview!.Id, 4);

                // ── MULTI-JOB CANDIDATE (second application — UX Designer, added below) ─
                // Alex record is reused — see UX Designer block
            }

            // ════════════════════════════════════════════════════════════════════════
            // UX DESIGNER — 6 candidates (reuses Alex from above for multi-job demo)
            // ════════════════════════════════════════════════════════════════════════
            if (jobUX != null)
            {
                // Offer
                var nathan = AddCandidate("Nathan", "Robinson", "nathan.robinson@example.com", CandidateSource.Referral, 28);
                var appNathan = AddApplication(nathan, jobUX, ApplicationStage.Offer, null, 27);
                AddRec(appNathan.Id, RecommendationStatus.Approved, approved: true,
                    "Beautiful portfolio; highly user-empathetic designer.");

                // Interview — Stage1 Approved
                var olivia = AddCandidate("Olivia", "Clark", "olivia.clark@example.com", CandidateSource.LinkedIn, 21);
                var appOlivia = AddApplication(olivia, jobUX, ApplicationStage.Interview, uxInterview?.Id, 20);
                AddRec(appOlivia.Id, RecommendationStatus.Approved, approved: true,
                    "Systems thinker with strong visual design execution.");
                AddInterview(olivia.Id, appOlivia.Id, uxInterview!.Id, 3);

                // Screen
                var patrick = AddCandidate("Patrick", "Lewis", "patrick.lewis@example.com", CandidateSource.Applicant, 18);
                AddApplication(patrick, jobUX, ApplicationStage.Screen, null, 17);

                // Applied
                var quinn = AddCandidate("Quinn", "Walker", "quinn.walker@example.com", CandidateSource.Internal, 15);
                AddApplication(quinn, jobUX, ApplicationStage.Applied, null, 14);

                var rachel = AddCandidate("Rachel", "Hall", "rachel.hall@example.com", CandidateSource.Applicant, 12);
                AddApplication(rachel, jobUX, ApplicationStage.Applied, null, 11);

                // ── MULTI-JOB CANDIDATE second application (reuse existing Alex record) ──
                var alexRecord = context.Candidates.FirstOrDefault(c => c.Email == "alex.shared@example.com");
                if (alexRecord != null && uxInterview != null)
                {
                    var appAlexUX = AddApplication(alexRecord, jobUX, ApplicationStage.Screen, null, 20);
                    _ = appAlexUX; // second application recorded
                }
            }

            // ════════════════════════════════════════════════════════════════════════
            // DATA ANALYST — 7 candidates
            // ════════════════════════════════════════════════════════════════════════
            if (jobDA != null)
            {
                // Interview — Stage1 Approved
                var sam = AddCandidate("Sam", "Allen", "sam.allen@example.com", CandidateSource.LinkedIn, 27);
                var appSam = AddApplication(sam, jobDA, ApplicationStage.Interview, daInterview?.Id, 26);
                AddRec(appSam.Id, RecommendationStatus.Approved, approved: true,
                    "Exceptional analytical depth; clear communicator with stakeholders.");
                AddInterview(sam.Id, appSam.Id, daInterview!.Id, 1);

                // Screen
                var tina = AddCandidate("Tina", "Young", "tina.young@example.com", CandidateSource.Applicant, 25);
                AddApplication(tina, jobDA, ApplicationStage.Screen, null, 24);

                var victor = AddCandidate("Victor", "Scott", "victor.scott@example.com", CandidateSource.LinkedIn, 22);
                AddApplication(victor, jobDA, ApplicationStage.Screen, null, 21);

                // Applied
                var wendy = AddCandidate("Wendy", "King", "wendy.king@example.com", CandidateSource.Referral, 18);
                AddApplication(wendy, jobDA, ApplicationStage.Applied, null, 17);

                var xavier = AddCandidate("Xavier", "Wright", "xavier.wright@example.com", CandidateSource.Applicant, 15);
                AddApplication(xavier, jobDA, ApplicationStage.Applied, null, 14);

                // Rejected
                var yara = AddCandidate("Yara", "Lopez", "yara.lopez@example.com", CandidateSource.LinkedIn, 29);
                var appYara = AddApplication(yara, jobDA, ApplicationStage.Rejected, null, 28);
                _ = appYara;

                var zoe = AddCandidate("Zoe", "Hill", "zoe.hill@example.com", CandidateSource.Applicant, 10);
                AddApplication(zoe, jobDA, ApplicationStage.Applied, null, 9);
            }
        }

        private static void NullifyOrphanedScorecardTemplateIds(AppDbContext context)
        {
            var validTemplateIds = context.ScorecardTemplates.Select(t => t.Id).ToHashSet();
            var orphanedJobs = context.Jobs
                .Where(j => j.ScorecardTemplateId != null)
                .ToList()
                .Where(j => !validTemplateIds.Contains(j.ScorecardTemplateId!.Value))
                .ToList();

            if (!orphanedJobs.Any())
                return;

            foreach (var job in orphanedJobs)
                job.ScorecardTemplateId = null;

            context.SaveChanges();
        }

        /// <summary>
        /// Seeds the master Admin identity user. Called from Program.cs after role seeding.
        /// Idempotent — skips if the user already exists.
        /// </summary>
        public static async Task SeedAdminUserAsync(UserManager<ApplicationUser> userManager)
        {
            const string email    = "admin@wiserecruiter.com";
            const string password = "Password123!";

            if (await userManager.FindByEmailAsync(email) != null)
                return;

            var admin = new ApplicationUser
            {
                UserName  = email,
                Email     = email,
                FullName  = "System Administrator",
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(admin, password);
            if (result.Succeeded)
                await userManager.AddToRoleAsync(admin, "Admin");
        }

        /// <summary>
        /// Seeds a test approving executive user. Idempotent — skips if the user already exists.
        /// </summary>
        public static async Task SeedExecutiveUserAsync(UserManager<ApplicationUser> userManager)
        {
            const string email    = "executive@wiserecruiter.com";
            const string password = "Password123!";

            if (await userManager.FindByEmailAsync(email) != null)
                return;

            var executive = new ApplicationUser
            {
                UserName             = email,
                Email                = email,
                FullName             = "Approving Executive",
                EmailConfirmed       = true,
                IsApprovingExecutive = true
            };

            var result = await userManager.CreateAsync(executive, password);
            if (result.Succeeded)
                await userManager.AddToRoleAsync(executive, "HiringManager");
        }
    }
}
