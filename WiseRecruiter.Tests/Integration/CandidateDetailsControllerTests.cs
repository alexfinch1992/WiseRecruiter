using System;
using System.Security.Claims;
using System.Threading.Tasks;
using FluentAssertions;
using JobPortal.Data;
using JobPortal.Models;
using JobPortal.Models.ViewModels;
using JobPortal.Services.Implementations;
using JobPortal.Services.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;
using WiseRecruiter.Tests.Helpers;
using Xunit;

namespace WiseRecruiter.Tests.Integration
{
    /// <summary>
    /// Safety-net tests for AdminController.CandidateDetails.
    /// Covers: successful load, NotFound (null id), NotFound (missing app), HiringManager forbidden,
    /// and scorecard inclusion — ensuring these behaviours survive the service extraction.
    /// </summary>
    public class CandidateDetailsControllerTests
    {
        // ── helpers ─────────────────────────────────────────────────────────────

        private static AppDbContext CreateInMemoryContext() =>
            new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase("candidate_details_" + Guid.NewGuid())
                .Options);

        private static AdminController CreateAdminController(AppDbContext context, ClaimsPrincipal? user = null)
            => AdminControllerFactory.Create(context, user);

        private static async Task<Application> SeedApplicationAsync(AppDbContext context)
        {
            var candidate = new Candidate
            {
                FirstName = "Jane",
                LastName  = "Doe",
                Email     = $"jane_{Guid.NewGuid()}@example.com",
                CreatedAt = DateTime.UtcNow
            };
            context.Candidates.Add(candidate);

            var job = new Job { Title = "Software Engineer", Description = "Test job" };
            context.Jobs.Add(job);
            await context.SaveChangesAsync();

            context.JobStages.Add(new JobStage { JobId = job.Id, Name = "Technical Interview", Order = 1 });

            var application = new Application
            {
                Name        = "Jane Doe",
                Email       = candidate.Email,
                City        = "Sydney",
                JobId       = job.Id,
                CandidateId = candidate.Id,
                Stage       = ApplicationStage.Applied,
                AppliedDate = DateTime.UtcNow
            };
            context.Applications.Add(application);
            await context.SaveChangesAsync();

            return application;
        }

        // ── 1. Valid application → ViewResult with populated CandidateAdminViewModel ──

        [Fact]
        public async Task CandidateDetails_ValidApplication_ReturnsViewWithViewModel()
        {
            using var context = CreateInMemoryContext();
            var application   = await SeedApplicationAsync(context);
            var controller    = CreateAdminController(context);

            var result = await controller.CandidateDetails(application.Id);

            var view = result.Should().BeOfType<ViewResult>().Subject;
            var vm   = view.Model.Should().BeOfType<CandidateAdminViewModel>().Subject;
            vm.Id.Should().Be(application.Id);
            vm.Name.Should().Be("Jane Doe");
            vm.JobTitle.Should().Be("Software Engineer");
        }

        // ── 2. Null id → NotFound ────────────────────────────────────────────────────

        [Fact]
        public async Task CandidateDetails_NullId_ReturnsNotFound()
        {
            using var context = CreateInMemoryContext();
            var controller    = CreateAdminController(context);

            var result = await controller.CandidateDetails(null);

            result.Should().BeOfType<NotFoundResult>();
        }

        // ── 3. Non-existent application id → NotFound ───────────────────────────────

        [Fact]
        public async Task CandidateDetails_NonexistentApplication_ReturnsNotFound()
        {
            using var context = CreateInMemoryContext();
            var controller    = CreateAdminController(context);

            var result = await controller.CandidateDetails(99999);

            result.Should().BeOfType<NotFoundResult>();
        }

        // ── 4. HiringManager with no job assignment → Forbid ────────────────────────

        [Fact]
        public async Task CandidateDetails_HiringManagerWithoutAccess_ReturnsForbid()
        {
            using var context = CreateInMemoryContext();
            var application   = await SeedApplicationAsync(context);

            var hiringManagerUser = new ClaimsPrincipal(
                new ClaimsIdentity(
                    new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, "hm-user-id"),
                        new Claim(ClaimTypes.Role, "HiringManager"),
                    },
                    "Identity.Application"));

            // No JobAssignment seeded → CanAccessJobAsync returns false
            var controller = CreateAdminController(context, hiringManagerUser);

            var result = await controller.CandidateDetails(application.Id);

            result.Should().BeOfType<ForbidResult>();
        }

        // ── 5. Scorecard seeded → ViewModel includes it ─────────────────────────────

        [Fact]
        public async Task CandidateDetails_WithScorecard_IncludesScorecardInViewModel()
        {
            using var context = CreateInMemoryContext();
            var application   = await SeedApplicationAsync(context);

            context.Scorecards.Add(new Scorecard
            {
                CandidateId = application.CandidateId,
                SubmittedBy = "admin",
                SubmittedAt = DateTime.UtcNow,
                IsArchived  = false
            });
            await context.SaveChangesAsync();

            var controller = CreateAdminController(context);

            var result = await controller.CandidateDetails(application.Id);

            var view = result.Should().BeOfType<ViewResult>().Subject;
            var vm   = view.Model.Should().BeOfType<CandidateAdminViewModel>().Subject;
            vm.Scorecards.Should().HaveCount(1);
        }

        // ── 6. HiringManager WITH access → returns View (not Forbid) ────────────────

        [Fact]
        public async Task CandidateDetails_HiringManagerWithAccess_ReturnsView()
        {
            using var context = CreateInMemoryContext();
            var application   = await SeedApplicationAsync(context);
            const string hmUserId = "hm-user-with-access";

            // Seed a JobAssignment so the HiringManager is allowed
            context.JobAssignments.Add(new JobAssignment { UserId = hmUserId, JobId = application.JobId });
            await context.SaveChangesAsync();

            var hiringManagerUser = new ClaimsPrincipal(
                new ClaimsIdentity(
                    new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, hmUserId),
                        new Claim(ClaimTypes.Role, "HiringManager"),
                    },
                    "Identity.Application"));

            var controller = CreateAdminController(context, hiringManagerUser);

            var result = await controller.CandidateDetails(application.Id);

            result.Should().BeOfType<ViewResult>();
        }
    }
}
