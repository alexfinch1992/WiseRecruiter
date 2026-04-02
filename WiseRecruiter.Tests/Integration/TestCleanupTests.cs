using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using FluentAssertions;
using JobPortal.Controllers;
using JobPortal.Data;
using JobPortal.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace WiseRecruiter.Tests.Integration
{
    /// <summary>
    /// Verifies the TestCleanupController:
    ///   1. Returns 404 outside Development.
    ///   2. Deletes entities whose Job.Title or Application.Email starts with the prefix.
    ///   3. Never deletes entities whose names do NOT start with the prefix.
    ///   4. Handles an empty prefix gracefully (returns 400).
    ///   5. Is idempotent: a second call with the same prefix succeeds with zero deletes.
    /// </summary>
    public class TestCleanupTests
    {
        // ── Context factory ───────────────────────────────────────────────────

        private static AppDbContext CreateContext() =>
            new AppDbContext(
                new DbContextOptionsBuilder<AppDbContext>()
                    .UseInMemoryDatabase("cleanup_" + Guid.NewGuid())
                    .Options);

        // ── Controller factory ────────────────────────────────────────────────

        private static TestCleanupController CreateController(
            AppDbContext context,
            bool isDevelopment = true)
        {
            var envMock = new Mock<IWebHostEnvironment>();
            envMock.Setup(e => e.EnvironmentName)
                   .Returns(isDevelopment ? "Development" : "Production");

            var controller = new TestCleanupController(context, envMock.Object);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(
                        new ClaimsIdentity(
                            new[] { new Claim(ClaimTypes.Role, "Admin") },
                            "test"))
                }
            };
            return controller;
        }

        // ── Seed helpers ──────────────────────────────────────────────────────

        private static async Task<(Job matchingJob, Job realJob,
                                   Application matchingApp, Application realApp)>
            SeedDataAsync(AppDbContext context, string prefix)
        {
            // Candidate for the matching application
            var matchingCandidate = new Candidate
            {
                FirstName = "E2E",
                LastName  = "Test",
                Email     = $"{prefix}_candidate@test.invalid",
                CreatedAt = DateTime.UtcNow,
            };
            // Candidate for the real (non-matching) application
            var realCandidate = new Candidate
            {
                FirstName = "Real",
                LastName  = "Person",
                Email     = "real_person@company.com",
                CreatedAt = DateTime.UtcNow,
            };
            context.Candidates.AddRange(matchingCandidate, realCandidate);

            var matchingJob = new Job { Title = $"{prefix}_JOB", Description = "E2E job" };
            var realJob     = new Job { Title = "REAL_JOB_DO_NOT_DELETE", Description = "Permanent" };
            context.Jobs.AddRange(matchingJob, realJob);

            await context.SaveChangesAsync();

            var matchingApp = new Application
            {
                Name        = "E2E Applicant",
                Email       = $"{prefix}_applicant@test.invalid",
                City        = "TestCity",
                JobId       = matchingJob.Id,
                CandidateId = matchingCandidate.Id,
                Stage       = ApplicationStage.Applied,
            };
            var realApp = new Application
            {
                Name        = "Real Applicant",
                Email       = "real@company.com",
                City        = "RealCity",
                JobId       = realJob.Id,
                CandidateId = realCandidate.Id,
                Stage       = ApplicationStage.Applied,
            };
            context.Applications.AddRange(matchingApp, realApp);
            await context.SaveChangesAsync();

            return (matchingJob, realJob, matchingApp, realApp);
        }

        // ── Test 1: returns 404 outside Development ───────────────────────────

        [Fact]
        public async Task Cleanup_ReturnsNotFound_OutsideDevelopment()
        {
            using var context = CreateContext();
            var controller = CreateController(context, isDevelopment: false);

            var result = await controller.Cleanup(new CleanupRequest { Prefix = "E2E_" });

            result.Should().BeOfType<NotFoundResult>();
        }

        // ── Test 2: returns 400 for empty prefix ──────────────────────────────

        [Fact]
        public async Task Cleanup_ReturnsBadRequest_WhenPrefixIsEmpty()
        {
            using var context = CreateContext();
            var controller = CreateController(context);

            var result = await controller.Cleanup(new CleanupRequest { Prefix = "" });

            result.Should().BeOfType<BadRequestObjectResult>();
        }

        // ── Test 3: deletes ONLY matching entities ────────────────────────────

        [Fact]
        public async Task Cleanup_DeletesMatchingEntities_RetainsUnrelated()
        {
            const string prefix = "E2E_VERIFY";
            using var context = CreateContext();
            var (matchingJob, realJob, matchingApp, realApp) =
                await SeedDataAsync(context, prefix);

            var controller = CreateController(context);

            // Act
            var result = await controller.Cleanup(new CleanupRequest { Prefix = prefix });

            // Assert: HTTP 200
            result.Should().BeOfType<OkObjectResult>();

            // Matching job and application must be gone
            (await context.Jobs.FindAsync(matchingJob.Id)).Should().BeNull(
                "matching job should have been deleted");
            (await context.Applications.FindAsync(matchingApp.Id)).Should().BeNull(
                "matching application should have been deleted");

            // Real job and application must still exist
            (await context.Jobs.FindAsync(realJob.Id)).Should().NotBeNull(
                "real job must NOT be deleted");
            (await context.Applications.FindAsync(realApp.Id)).Should().NotBeNull(
                "real application must NOT be deleted");
        }

        // ── Test 4: response body contains accurate counts ────────────────────

        [Fact]
        public async Task Cleanup_Response_ContainsCorrectDeleteCounts()
        {
            const string prefix = "E2E_COUNTS";
            using var context = CreateContext();
            await SeedDataAsync(context, prefix);

            var controller = CreateController(context);
            var result = (OkObjectResult)await controller.Cleanup(new CleanupRequest { Prefix = prefix });

            // Deserialize via the anonymous type's properties using reflection
            var value = result.Value!;
            var type  = value.GetType();

            ((int)type.GetProperty("deletedJobs")!.GetValue(value)!).Should().Be(1);
            ((int)type.GetProperty("deletedApplications")!.GetValue(value)!).Should().Be(1);
            ((int)type.GetProperty("deletedCandidates")!.GetValue(value)!).Should().Be(1);
        }

        // ── Test 5: idempotent — second call with same prefix is safe ─────────

        [Fact]
        public async Task Cleanup_IsIdempotent_SecondCallSucceeds()
        {
            const string prefix = "E2E_IDEMPOTENT";
            using var context = CreateContext();
            await SeedDataAsync(context, prefix);

            var controller = CreateController(context);
            var req = new CleanupRequest { Prefix = prefix };

            // First call
            var first = await controller.Cleanup(req);
            first.Should().BeOfType<OkObjectResult>();

            // Second call — nothing left to delete, must not throw
            var second = await controller.Cleanup(req);
            second.Should().BeOfType<OkObjectResult>();

            var value = ((OkObjectResult)second).Value!;
            var type  = value.GetType();
            ((int)type.GetProperty("deletedJobs")!.GetValue(value)!).Should().Be(0);
        }

        // ── Test 6: cascade — child records removed before parent ─────────────

        [Fact]
        public async Task Cleanup_DeletesChildRecords_BeforeParents()
        {
            const string prefix = "E2E_CASCADE";
            using var context = CreateContext();
            var (_, _, matchingApp, _) = await SeedDataAsync(context, prefix);

            // Add a recommendation and a document to the matching application
            context.CandidateRecommendations.Add(new CandidateRecommendation
            {
                ApplicationId  = matchingApp.Id,
                Stage          = RecommendationStage.Stage1,
                Status         = RecommendationStatus.Draft,
                LastUpdatedUtc = DateTime.UtcNow,
            });
            context.Documents.Add(new Document
            {
                ApplicationId = matchingApp.Id,
                FileName      = "resume.pdf",
                FilePath      = "/uploads/resume.pdf",
                Type          = DocumentType.Resume,
            });
            await context.SaveChangesAsync();

            var controller = CreateController(context);
            var result = await controller.Cleanup(new CleanupRequest { Prefix = prefix });

            result.Should().BeOfType<OkObjectResult>("cleanup must complete without FK violation");

            // All child records must be gone
            (await context.CandidateRecommendations
                .AnyAsync(r => r.ApplicationId == matchingApp.Id)).Should().BeFalse();
            (await context.Documents
                .AnyAsync(d => d.ApplicationId == matchingApp.Id)).Should().BeFalse();
        }

        // ── Test 7: candidate shared with non-matching app is retained ─────────

        [Fact]
        public async Task Cleanup_RetainsCandidate_WhenTheyHaveOtherApplications()
        {
            const string prefix = "E2E_SHARED";
            using var context = CreateContext();

            // One candidate - two applications: one matching, one not
            var sharedCandidate = new Candidate
            {
                FirstName = "Shared",
                LastName  = "Person",
                Email     = "shared@company.com",
                CreatedAt = DateTime.UtcNow,
            };
            context.Candidates.Add(sharedCandidate);

            var matchingJob = new Job { Title = $"{prefix}_JOB2", Description = "E2E" };
            var realJob     = new Job { Title = "REAL_JOB_2", Description = "Permanent" };
            context.Jobs.AddRange(matchingJob, realJob);
            await context.SaveChangesAsync();

            var matchingApp = new Application
            {
                Name        = "Shared",
                Email       = $"{prefix}_shared@test.invalid",
                City        = "TestCity",
                JobId       = matchingJob.Id,
                CandidateId = sharedCandidate.Id,
                Stage       = ApplicationStage.Applied,
            };
            var realApp = new Application
            {
                Name        = "Shared",
                Email       = "shared_real@company.com",
                City        = "RealCity",
                JobId       = realJob.Id,
                CandidateId = sharedCandidate.Id,  // same candidate!
                Stage       = ApplicationStage.Applied,
            };
            context.Applications.AddRange(matchingApp, realApp);
            await context.SaveChangesAsync();

            var controller = CreateController(context);
            await controller.Cleanup(new CleanupRequest { Prefix = prefix });

            // Matching application gone, real application and shared candidate intact
            (await context.Applications.FindAsync(matchingApp.Id)).Should().BeNull();
            (await context.Applications.FindAsync(realApp.Id)).Should().NotBeNull();
            (await context.Candidates.FindAsync(sharedCandidate.Id)).Should().NotBeNull(
                "candidate with remaining applications must NOT be deleted");
        }
    }
}
