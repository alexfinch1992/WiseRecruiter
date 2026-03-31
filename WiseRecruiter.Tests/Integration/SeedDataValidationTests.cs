using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using JobPortal.Data;
using JobPortal.Helpers;
using JobPortal.Models;
using JobPortal.Services.Implementations;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace WiseRecruiter.Tests.Integration
{
    /// <summary>
    /// Validates the demo seed dataset produced by DbInitializer.WipeAndReseedCandidates.
    /// These tests run the seeder directly against an in-memory database and assert
    /// the three invariants required by the demo brief.
    /// </summary>
    public class SeedDataValidationTests
    {
        private static AppDbContext CreateContext() =>
            new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase("seed_validation_" + Guid.NewGuid())
                .Options);

        /// <summary>
        /// Seeds the 4 demo jobs + their pipeline stages so WipeAndReseedCandidates has data to work with.
        /// </summary>
        private static void SeedJobs(AppDbContext context)
        {
            var jobDefs = new[]
            {
                ("Senior Product Manager", new[] { "Applied", "Screen", "Interview", "Offer", "Hired", "Product Case Study", "Leadership Panel" }),
                ("Backend Engineer",       new[] { "Applied", "Screen", "Interview", "Offer", "Hired", "Coding Challenge", "System Design Round" }),
                ("UX Designer",            new[] { "Applied", "Screen", "Interview", "Offer", "Hired", "Portfolio Review", "Design Exercise" }),
                ("Data Analyst",           new[] { "Applied", "Screen", "Interview", "Offer", "Hired" }),
            };

            foreach (var (title, stages) in jobDefs)
            {
                var job = new Job { Title = title, Description = title + " demo description." };
                context.Jobs.Add(job);
                context.SaveChanges();
                for (int i = 0; i < stages.Length; i++)
                    context.JobStages.Add(new JobStage { JobId = job.Id, Name = stages[i], Order = i });
                context.SaveChanges();
            }
        }

        // ── Test 1 ─────────────────────────────────────────────────────────────────

        [Fact]
        public void At_Least_One_Candidate_Has_Two_Or_More_Applications()
        {
            using var context = CreateContext();
            SeedJobs(context);
            DbInitializer.WipeAndReseedCandidates(context);

            var multiJobCandidateId = context.Applications
                .GroupBy(a => a.CandidateId)
                .Where(g => g.Count() >= 2)
                .Select(g => g.Key)
                .FirstOrDefault();

            multiJobCandidateId.Should().NotBe(0,
                "at least one candidate (alex.shared@example.com) should have 2+ applications");

            var applicationCount = context.Applications
                .Count(a => a.CandidateId == multiJobCandidateId);

            applicationCount.Should().BeGreaterThanOrEqualTo(2,
                "the multi-job candidate must have applications to multiple jobs");
        }

        // ── Test 2 ─────────────────────────────────────────────────────────────────

        [Fact]
        public void All_Applications_Have_ResumePath_Populated()
        {
            using var context = CreateContext();
            SeedJobs(context);
            DbInitializer.WipeAndReseedCandidates(context);

            var totalApplications = context.Applications.Count();
            totalApplications.Should().BeGreaterThan(0, "seed must produce at least one application");

            var withoutResume = context.Applications
                .Where(a => string.IsNullOrEmpty(a.ResumePath))
                .Select(a => new { a.Id, a.Name, a.Email })
                .ToList();

            withoutResume.Should().BeEmpty(
                $"every application must have a ResumePath set; missing: {string.Join(", ", withoutResume.Select(x => x.Email))}");
        }

        // ── Test 3 ─────────────────────────────────────────────────────────────────

        [Fact]
        public void At_Least_One_Interview_Stage_Application_Was_Moved_Without_Stage1_Approval()
        {
            using var context = CreateContext();
            SeedJobs(context);
            DbInitializer.WipeAndReseedCandidates(context);

            var flaggedApplication = context.Applications
                .FirstOrDefault(a =>
                    a.Stage == ApplicationStage.Interview &&
                    a.MovedWithoutStage1Approval);

            flaggedApplication.Should().NotBeNull(
                "at least one Interview-stage application must have MovedWithoutStage1Approval = true " +
                "(demonstrates the soft-gate warning feature)");
        }

        // ── Bonus: sanity counts ────────────────────────────────────────────────────

        [Fact]
        public void Seed_Produces_Expected_Candidate_And_Job_Counts()
        {
            using var context = CreateContext();
            SeedJobs(context);
            DbInitializer.WipeAndReseedCandidates(context);

            var candidateCount = context.Candidates.Count();
            var applicationCount = context.Applications.Count();
            var jobCount = context.Jobs.Count();

            // 25 unique Candidate records (Alex counts as 1, has 2 Applications)
            candidateCount.Should().BeInRange(20, 30,
                $"seed should produce 20–30 candidates; got {candidateCount}");

            // At least as many applications as candidates (multi-job adds one extra)
            applicationCount.Should().BeGreaterThanOrEqualTo(candidateCount,
                "total applications must be >= candidates due to multi-job candidate");

            jobCount.Should().Be(4, "exactly 4 demo jobs must be seeded");
        }

        // ── Bonus: resume distribution ─────────────────────────────────────────────

        [Fact]
        public void Both_Resume_Files_Are_Used_Across_Applications()
        {
            using var context = CreateContext();
            SeedJobs(context);
            DbInitializer.WipeAndReseedCandidates(context);

            var usedPaths = context.Applications
                .Where(a => a.ResumePath != null)
                .Select(a => a.ResumePath!)
                .Distinct()
                .ToList();

            usedPaths.Should().Contain("/documents/john_doe_product_manager_resume.pdf",
                "John's resume must be distributed to at least one candidate");

            usedPaths.Should().Contain("/documents/jane_doe_product_manager_resume.pdf",
                "Jane's resume must be distributed to at least one candidate");
        }
    }
}
