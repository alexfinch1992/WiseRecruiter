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
                var jobs = new[]
                {
                    new Job
                    {
                        Title = "Senior Software Engineer",
                        Description = "We are looking for an experienced Senior Software Engineer to join our growing team. You will work on challenging projects using modern technologies including .NET, React, and cloud services. Ideal candidates will have 5+ years of experience and strong problem-solving skills."
                    },
                    new Job
                    {
                        Title = "Full Stack Developer",
                        Description = "Join us as a Full Stack Developer and help build amazing web applications. We're looking for someone with expertise in both frontend (React/Vue) and backend (Node.js/Python) technologies. You'll work in an agile environment with a talented team of developers."
                    }
                };
                context.Jobs.AddRange(jobs);
                context.SaveChanges();
                
                // Seed default stages for each job
                var allJobs = context.Jobs.ToList();
                var defaultStages = new[] { "Application", "Screen", "Interview", "Offer" };
                
                foreach (var job in allJobs)
                {
                    for (int i = 0; i < defaultStages.Length; i++)
                    {
                        context.JobStages.Add(new JobStage
                        {
                            JobId = job.Id,
                            Name = defaultStages[i],
                            Order = i
                        });
                    }
                }
                context.SaveChanges();
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

        private static void WipeAndReseedCandidates(AppDbContext context)
        {
            // Wipe in FK-safe order
            context.Interviews.RemoveRange(context.Interviews.ToList());
            context.CandidateRecommendations.RemoveRange(context.CandidateRecommendations.ToList());
            context.Applications.RemoveRange(context.Applications.ToList());
            context.Candidates.RemoveRange(context.Candidates.ToList());
            context.SaveChanges();

            var jobs = context.Jobs.Include(j => j.Stages).ToList();
            if (!jobs.Any()) return;

            var names = new (string First, string Last, string EmailPrefix)[]
            {
                ("Alice",   "Johnson",  "alice.johnson"),
                ("Bob",     "Smith",    "bob.smith"),
                ("Carol",   "Williams", "carol.williams"),
                ("David",   "Brown",    "david.brown"),
                ("Emma",    "Davis",    "emma.davis"),
                ("Frank",   "Wilson",   "frank.wilson"),
                ("Grace",   "Lee",      "grace.lee"),
                ("Henry",   "Martinez", "henry.martinez"),
                ("Iris",    "Chen",     "iris.chen"),
                ("Jack",    "Anderson", "jack.anderson"),
                ("Karen",   "Taylor",   "karen.taylor"),
                ("Leo",     "Thomas",   "leo.thomas"),
                ("Monica",  "Garcia",   "monica.garcia"),
                ("Nathan",  "Robinson", "nathan.robinson"),
                ("Olivia",  "Clark",    "olivia.clark"),
                ("Patrick", "Lewis",    "patrick.lewis"),
                ("Quinn",   "Walker",   "quinn.walker"),
                ("Rachel",  "Hall",     "rachel.hall"),
                ("Sam",     "Allen",    "sam.allen"),
                ("Tina",    "Young",    "tina.young"),
            };

            var cities = new[] { "New York", "San Francisco", "Los Angeles", "Chicago", "Boston",
                                  "Seattle", "Austin", "Denver", "Portland", "Miami" };

            // 10 candidates per job: 3 Applied, 2 Screen, 3 Interview, 1 Offer, 1 Rejected
            var stageDistribution = new[]
            {
                (ApplicationStage.Applied,   3),
                (ApplicationStage.Screen,    2),
                (ApplicationStage.Interview, 3),
                (ApplicationStage.Offer,     1),
                (ApplicationStage.Rejected,  1),
            };

            int globalNameIndex = 0;

            foreach (var job in jobs)
            {
                var jobStages = job.Stages?.OrderBy(s => s.Order).ToList() ?? new List<JobStage>();

                // Prefer the stage named "Interview", otherwise fall back to the first stage
                var interviewJobStage = jobStages.FirstOrDefault(s => s.Name == "Interview")
                                     ?? jobStages.FirstOrDefault();

                int candidateIndex = 0;

                foreach (var (stage, count) in stageDistribution)
                {
                    for (int i = 0; i < count; i++)
                    {
                        var n = names[globalNameIndex % names.Length];
                        var jobSuffix = jobs.IndexOf(job) + 1;
                        var email = $"{n.EmailPrefix}.job{jobSuffix}@example.com";
                        var city = cities[candidateIndex % cities.Length];

                        // --- Candidate ---
                        var candidate = new Candidate
                        {
                            FirstName = n.First,
                            LastName  = n.Last,
                            Email     = email,
                            CreatedAt = DateTime.UtcNow.AddDays(-(29 - candidateIndex)),
                        };
                        context.Candidates.Add(candidate);
                        context.SaveChanges(); // need Id for Application FK

                        // --- Application ---
                        int? currentJobStageId = stage == ApplicationStage.Interview
                            ? interviewJobStage?.Id
                            : null;

                        var application = new Application
                        {
                            Name             = $"{n.First} {n.Last}",
                            Email            = email,
                            City             = city,
                            JobId            = job.Id,
                            CandidateId      = candidate.Id,
                            Stage            = stage,
                            CurrentJobStageId = currentJobStageId,
                            AppliedDate      = DateTime.UtcNow.AddDays(-(28 - candidateIndex)),
                        };
                        context.Applications.Add(application);
                        context.SaveChanges(); // need Id for Recommendation + Interview FKs

                        // --- Recommendation + Interview (Interview-stage candidates only) ---
                        if (stage == ApplicationStage.Interview && interviewJobStage != null)
                        {
                            var recStatus = i switch
                            {
                                0 => RecommendationStatus.Approved,
                                1 => RecommendationStatus.Submitted,
                                _ => RecommendationStatus.Draft,
                            };

                            context.CandidateRecommendations.Add(new CandidateRecommendation
                            {
                                ApplicationId      = application.Id,
                                Stage              = RecommendationStage.Stage1,
                                Status             = recStatus,
                                Summary            = i == 0 ? "Strong candidate with excellent technical skills and culture fit." : null,
                                HireRecommendation = i == 0 ? true : null,
                            });

                            context.Interviews.Add(new Interview
                            {
                                CandidateId   = candidate.Id,
                                ApplicationId = application.Id,
                                JobStageId    = interviewJobStage.Id,
                                ScheduledAt   = DateTime.UtcNow.AddDays(7 + candidateIndex),
                                IsCancelled   = false,
                            });

                            context.SaveChanges();
                        }

                        candidateIndex++;
                        globalNameIndex++;
                    }
                }
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
    }
}
