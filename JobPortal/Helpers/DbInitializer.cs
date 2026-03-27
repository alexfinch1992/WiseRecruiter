using JobPortal.Data;
using JobPortal.Models;
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

            // Seed dummy candidates
            if (!context.Applications.Any())
            {
                var jobs = context.Jobs.Include(j => j.Stages).ToList();
                if (jobs.Any())
                {
                    var applications = GenerateDummyCandidates(jobs);

                    // Create Candidates BEFORE Applications to satisfy the FK constraint
                    // (Application.CandidateId is a required FK; inserting with CandidateId=0 fails with FK checks on)
                    var candidatesByEmail = new Dictionary<string, Candidate>(StringComparer.OrdinalIgnoreCase);
                    foreach (var app in applications)
                    {
                        var email = app.Email ?? string.Empty;
                        if (candidatesByEmail.ContainsKey(email))
                            continue;

                        var (firstName, lastName) = SplitName(app.Name);
                        var candidate = new Candidate
                        {
                            FirstName = firstName,
                            LastName = lastName,
                            Email = email,
                            CreatedAt = DateTime.UtcNow
                        };
                        context.Candidates.Add(candidate);
                        candidatesByEmail[email] = candidate;
                    }
                    context.SaveChanges(); // persist all candidates at once to get their Ids

                    foreach (var app in applications)
                    {
                        var email = app.Email ?? string.Empty;
                        if (candidatesByEmail.TryGetValue(email, out var candidate))
                            app.CandidateId = candidate.Id;
                    }

                    context.Applications.AddRange(applications);
                    context.SaveChanges();
                }
            }

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

        private static List<Application> GenerateDummyCandidates(List<Job> jobs)
        {
            var applications = new List<Application>();
            var cities = new[] { "New York", "San Francisco", "Los Angeles", "Chicago", "Boston", "Seattle", "Austin", "Denver", "Portland", "Miami" };

            var candidateNames = new[]
            {
                ("Alice Johnson", "alice.johnson@email.com"),
                ("Bob Smith", "bob.smith@email.com"),
                ("Carol Williams", "carol.williams@email.com"),
                ("David Brown", "david.brown@email.com"),
                ("Emma Davis", "emma.davis@email.com"),
                ("Frank Wilson", "frank.wilson@email.com"),
                ("Grace Lee", "grace.lee@email.com"),
                ("Henry Martinez", "henry.martinez@email.com"),
                ("Iris Chen", "iris.chen@email.com"),
                ("Jack Anderson", "jack.anderson@email.com"),
                ("Karen Taylor", "karen.taylor@email.com"),
                ("Leo Thomas", "leo.thomas@email.com"),
                ("Monica Garcia", "monica.garcia@email.com"),
                ("Nathan Robinson", "nathan.robinson@email.com"),
                ("Olivia Clark", "olivia.clark@email.com")
            };

            var random = new Random(42); // Fixed seed for consistency

            foreach (var job in jobs)
            {
                var jobStages = job.Stages?.OrderBy(s => s.Order).ToList() ?? new List<JobStage>();
                
                if (!jobStages.Any())
                    continue; // Skip if no stages for this job

                // Add 7-8 candidates per job
                int candidatesPerJob = random.Next(7, 9);
                
                for (int i = 0; i < candidatesPerJob; i++)
                {
                    var (name, email) = candidateNames[applications.Count % candidateNames.Length];
                    var cityIndex = (applications.Count + i) % cities.Length;
                    var stageIndex = (applications.Count + i) % jobStages.Count;
                    var daysAgo = random.Next(1, 30);

                    applications.Add(new Application
                    {
                        Name = name,
                        Email = $"{name.ToLower().Replace(" ", ".")}.{applications.Count}@email.com",
                        City = cities[cityIndex],
                        JobId = job.Id,
                        CurrentJobStageId = jobStages[stageIndex].Id,
                        AppliedDate = DateTime.UtcNow.AddDays(-daysAgo),
                        ResumePath = null // In real scenario, would be actual resume files
                    });
                }
            }

            return applications;
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
    }
}
