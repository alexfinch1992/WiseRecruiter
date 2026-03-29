using System;
using System.Security.Claims;
using System.Threading.Tasks;
using FluentAssertions;
using JobPortal.Data;
using JobPortal.Models;
using JobPortal.Services.Implementations;
using JobPortal.Services.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace WiseRecruiter.Tests.Integration
{
    /// <summary>
    /// Integration tests for stage movement (AdminController.UpdateApplicationStage)
    /// and approval redirect (RecommendationAdminController.Approve / ApproveStage2).
    ///
    /// Tests 1–3 expose that the redirect destination is always "CandidateDetails".
    /// Tests 4–5 are FAILING before the fix: approval currently redirects to "Pending"
    ///            instead of back to the candidate admin page.
    /// </summary>
    public class AdminControllerTests
    {
        // ── helpers ─────────────────────────────────────────────────────────────

        private static AppDbContext CreateInMemoryContext() =>
            new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase("admin_ctrl_" + Guid.NewGuid())
                .Options);

        private static AdminController CreateAdminController(AppDbContext context)
        {
            IScorecardTemplateService templateService    = new ScorecardTemplateService(context);
            IApplicationService       applicationService = new ApplicationService(context);
            IAnalyticsService         analyticsService   = new AnalyticsService(context);
            IScorecardService         scorecardService   = new ScorecardService(context, templateService);
            IJobService               jobService         = new JobService(context);
            IScorecardAnalyticsService scorecardAnalyticsService = new ScorecardAnalyticsService(context);
            IInterviewService         interviewService   = new InterviewService(context);

            var controller = new AdminController(
                context,
                new Mock<IWebHostEnvironment>().Object,
                applicationService, analyticsService, scorecardService,
                templateService, jobService, scorecardAnalyticsService, interviewService,
                new RecommendationService(context, new StageOrderService()),
                new ApplicationStageService(context, new RecommendationService(context, new StageOrderService())),
                new HiringPipelineService(),
                new GlobalSearchService(context))
            {
                TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())
            };

            return controller;
        }

        private static RecommendationAdminController CreateApprovalController(AppDbContext context, int adminId = 1)
        {
            var controller = new RecommendationAdminController(new RecommendationService(context, new StageOrderService()));
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim("AdminId", adminId.ToString()) },
                        "AdminAuth"))
                }
            };
            return controller;
        }

        private static async Task<Application> SeedApplicationAsync(AppDbContext context)
        {
            var candidate = new Candidate
            {
                FirstName = "Test",
                LastName  = "Admin",
                Email     = $"admin_{Guid.NewGuid()}@example.com",
                CreatedAt = DateTime.UtcNow
            };
            context.Candidates.Add(candidate);

            var job = new Job { Title = "Developer", Description = "Test" };
            context.Jobs.Add(job);
            await context.SaveChangesAsync();

            var application = new Application
            {
                Name        = "Test Admin",
                Email       = candidate.Email,
                City        = "Sydney",
                JobId       = job.Id,
                CandidateId = candidate.Id,
                Stage       = ApplicationStage.Applied
            };
            context.Applications.Add(application);
            await context.SaveChangesAsync();

            return application;
        }

        private static async Task<Application> SeedSubmittedRecommendationAsync(
            AppDbContext context, RecommendationStage stage = RecommendationStage.Stage1)
        {
            var application = await SeedApplicationAsync(context);

            context.CandidateRecommendations.Add(new CandidateRecommendation
            {
                ApplicationId   = application.Id,
                Stage           = stage,
                Status          = RecommendationStatus.Submitted,
                Summary         = "Ready for review",
                LastUpdatedUtc  = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            return application;
        }

        // ── 1. Valid manual stage move → updates stage AND redirects to CandidateDetails ─

        [Fact]
        public async Task UpdateApplicationStage_ValidMove_UpdatesStageAndRedirectsToCandidateDetails()
        {
            using var context    = CreateInMemoryContext();
            var application      = await SeedApplicationAsync(context);
            var controller       = CreateAdminController(context);

            var result = await controller.UpdateApplicationStage(application.Id, "enum:Screen");

            var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirect.ActionName.Should().Be("CandidateDetails");

            var updated = await context.Applications.FindAsync(application.Id);
            updated!.Stage.Should().Be(ApplicationStage.Screen);
        }

        // ── 2. Move to Interview without approval → warning flag set, stage unchanged ──

        [Fact]
        public async Task UpdateApplicationStage_ToInterview_WithoutApproval_SetsWarningAndRedirectsToCandidateDetails()
        {
            using var context    = CreateInMemoryContext();
            var application      = await SeedApplicationAsync(context);
            var controller       = CreateAdminController(context);

            var result = await controller.UpdateApplicationStage(
                application.Id, "enum:Interview", proceedWithoutApproval: false);

            var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirect.ActionName.Should().Be("CandidateDetails");

            controller.TempData["StageApprovalWarning"].Should().Be(application.Id);

            var unchanged = await context.Applications.FindAsync(application.Id);
            unchanged!.Stage.Should().Be(ApplicationStage.Applied);
        }

        // ── 3. Move with bypass → stage updated AND redirects to CandidateDetails ────

        [Fact]
        public async Task UpdateApplicationStage_WithBypass_UpdatesStageAndRedirectsToCandidateDetails()
        {
            using var context    = CreateInMemoryContext();
            var application      = await SeedApplicationAsync(context);
            var controller       = CreateAdminController(context);

            var result = await controller.UpdateApplicationStage(
                application.Id, "enum:Interview", proceedWithoutApproval: true);

            var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirect.ActionName.Should().Be("CandidateDetails");

            var updated = await context.Applications.FindAsync(application.Id);
            updated!.Stage.Should().Be(ApplicationStage.Interview);
        }

        // ── 4. Stage 1 approval → must redirect to CandidateDetails (admin route) ──
        // FAILS before fix: currently redirects to "Pending"

        [Fact]
        public async Task ApproveStage1_OnSuccess_RedirectsToCandidateDetailsAdminPage()
        {
            using var context    = CreateInMemoryContext();
            var application      = await SeedSubmittedRecommendationAsync(context, RecommendationStage.Stage1);
            var controller       = CreateApprovalController(context, adminId: 1);

            var result = await controller.Approve(application.Id);

            var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirect.ActionName.Should().Be("CandidateDetails");
            redirect.ControllerName.Should().Be("Admin");
            redirect.RouteValues!["id"].Should().Be(application.Id);
        }

        // ── 5. Stage 2 approval → must redirect to CandidateDetails (admin route) ──
        // FAILS before fix: currently redirects to "Pending"

        [Fact]
        public async Task ApproveStage2_OnSuccess_RedirectsToCandidateDetailsAdminPage()
        {
            using var context    = CreateInMemoryContext();
            var application      = await SeedSubmittedRecommendationAsync(context, RecommendationStage.Stage2);
            var controller       = CreateApprovalController(context, adminId: 1);

            var result = await controller.ApproveStage2(application.Id);

            var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirect.ActionName.Should().Be("CandidateDetails");
            redirect.ControllerName.Should().Be("Admin");
            redirect.RouteValues!["id"].Should().Be(application.Id);
        }
    }
}
