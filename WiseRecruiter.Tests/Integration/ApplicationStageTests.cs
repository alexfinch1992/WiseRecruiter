using System;
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
using Xunit;

namespace WiseRecruiter.Tests.Integration
{
    public class ApplicationStageTests
    {
        private AppDbContext CreateInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: "stage_db_" + Guid.NewGuid())
                .Options;
            return new AppDbContext(options);
        }

        private AdminController CreateAdminController(AppDbContext context)
        {
            IScorecardTemplateService templateService = new ScorecardTemplateService(context);
            IApplicationService applicationService = new ApplicationService(context);
            IAnalyticsService analyticsService = new AnalyticsService(context);
            IScorecardService scorecardService = new ScorecardService(context, templateService);
            IJobService jobService = new JobService(context);
            IScorecardAnalyticsService scorecardAnalyticsService = new ScorecardAnalyticsService(context);
            IInterviewService interviewService = new InterviewService(context);

            var controller = new AdminController(
                context,
                new Mock<IWebHostEnvironment>().Object,
                applicationService, analyticsService, scorecardService,
                templateService, jobService, scorecardAnalyticsService, interviewService, new RecommendationService(context, new StageOrderService()), new ApplicationStageService(context, new RecommendationService(context, new StageOrderService())), new HiringPipelineService(), new GlobalSearchService(context))
            {
                TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())
            };

            return controller;
        }

        private static async Task<Application> SeedApplicationAsync(AppDbContext context)
        {
            var candidate = new Candidate
            {
                FirstName = "Alex",
                LastName = "Test",
                Email = $"alex_{Guid.NewGuid()}@example.com",
                CreatedAt = DateTime.UtcNow
            };
            context.Candidates.Add(candidate);

            var job = new Job { Title = "Developer", Description = "Test" };
            context.Jobs.Add(job);
            await context.SaveChangesAsync();

            var application = new Application
            {
                Name = "Alex Test",
                Email = candidate.Email,
                City = "Sydney",
                JobId = job.Id,
                CandidateId = candidate.Id,
                Stage = ApplicationStage.Applied
            };
            context.Applications.Add(application);
            await context.SaveChangesAsync();

            return application;
        }

        // --- 1. Stage updates correctly ---

        [Fact]
        public async Task UpdateApplicationStage_ToScreen_UpdatesStage()
        {
            using var context = CreateInMemoryContext();
            var application = await SeedApplicationAsync(context);
            var controller = CreateAdminController(context);

            var result = await controller.UpdateApplicationStage(application.Id, ApplicationStage.Screen);

            result.Should().BeOfType<RedirectToActionResult>();

            var updated = await context.Applications.FindAsync(application.Id);
            updated!.Stage.Should().Be(ApplicationStage.Screen);
        }

        [Fact]
        public async Task UpdateApplicationStage_ToOffer_UpdatesStage()
        {
            using var context = CreateInMemoryContext();
            var application = await SeedApplicationAsync(context);
            var controller = CreateAdminController(context);

            var result = await controller.UpdateApplicationStage(application.Id, ApplicationStage.Offer);

            result.Should().BeOfType<RedirectToActionResult>();

            var updated = await context.Applications.FindAsync(application.Id);
            updated!.Stage.Should().Be(ApplicationStage.Offer);
        }

        // --- 2. Moving to Interview without approval triggers warning ---

        [Fact]
        public async Task UpdateApplicationStage_ToInterview_WithoutApproval_RedirectsWithWarningFlag()
        {
            using var context = CreateInMemoryContext();
            var application = await SeedApplicationAsync(context);
            var controller = CreateAdminController(context);

            // No recommendation exists; proceedWithoutApproval = false (default)
            var result = await controller.UpdateApplicationStage(application.Id, ApplicationStage.Interview, proceedWithoutApproval: false);

            // Should redirect back to CandidateDetails (server-driven warning pattern)
            var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirect.ActionName.Should().Be("CandidateDetails");

            // TempData carries the warning signal for the next GET
            controller.TempData["StageApprovalWarning"].Should().Be(application.Id);

            // Stage should NOT have changed
            var unchanged = await context.Applications.FindAsync(application.Id);
            unchanged!.Stage.Should().Be(ApplicationStage.Applied);
        }

        [Fact]
        public async Task UpdateApplicationStage_ToInterview_WhenApproved_UpdatesWithoutWarning()
        {
            using var context = CreateInMemoryContext();
            var application = await SeedApplicationAsync(context);

            context.CandidateRecommendations.Add(new CandidateRecommendation
            {
                ApplicationId = application.Id,
                Stage = RecommendationStage.Stage1,
                Status = RecommendationStatus.Approved,
                LastUpdatedUtc = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var controller = CreateAdminController(context);

            var result = await controller.UpdateApplicationStage(application.Id, ApplicationStage.Interview, proceedWithoutApproval: false);

            result.Should().BeOfType<RedirectToActionResult>();

            var updated = await context.Applications.FindAsync(application.Id);
            updated!.Stage.Should().Be(ApplicationStage.Interview);
        }

        // --- 3. Proceeding with bypass updates stage successfully ---

        [Fact]
        public async Task UpdateApplicationStage_ToInterview_WithBypass_UpdatesStage()
        {
            using var context = CreateInMemoryContext();
            var application = await SeedApplicationAsync(context);
            var controller = CreateAdminController(context);

            var result = await controller.UpdateApplicationStage(application.Id, ApplicationStage.Interview, proceedWithoutApproval: true);

            result.Should().BeOfType<RedirectToActionResult>();

            var updated = await context.Applications.FindAsync(application.Id);
            updated!.Stage.Should().Be(ApplicationStage.Interview);
        }

        // --- 4. Bypass is recorded on recommendation ---

        [Fact]
        public async Task UpdateApplicationStage_WithBypass_RecordsBypassOnRecommendation()
        {
            using var context = CreateInMemoryContext();
            var application = await SeedApplicationAsync(context);
            var controller = CreateAdminController(context);

            await controller.UpdateApplicationStage(application.Id, ApplicationStage.Interview, proceedWithoutApproval: true);

            var rec = await context.CandidateRecommendations
                .FirstOrDefaultAsync(r => r.ApplicationId == application.Id && r.Stage == RecommendationStage.Stage1);

            rec.Should().NotBeNull();
            rec!.BypassedApproval.Should().BeTrue();
            rec.BypassedUtc.Should().NotBeNull();
        }

        [Fact]
        public async Task UpdateApplicationStage_WithBypass_DoesNotCreateDuplicateRecommendation()
        {
            using var context = CreateInMemoryContext();
            var application = await SeedApplicationAsync(context);

            // Seed an existing Draft recommendation
            context.CandidateRecommendations.Add(new CandidateRecommendation
            {
                ApplicationId = application.Id,
                Stage = RecommendationStage.Stage1,
                Status = RecommendationStatus.Draft,
                LastUpdatedUtc = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var controller = CreateAdminController(context);

            await controller.UpdateApplicationStage(application.Id, ApplicationStage.Interview, proceedWithoutApproval: true);

            var count = await context.CandidateRecommendations
                .CountAsync(r => r.ApplicationId == application.Id && r.Stage == RecommendationStage.Stage1);

            count.Should().Be(1); // no duplicate created
        }

        // --- 5. Non-Interview stages skip approval check entirely ---

        [Fact]
        public async Task UpdateApplicationStage_ToRejected_SkipsApprovalCheck()
        {
            using var context = CreateInMemoryContext();
            var application = await SeedApplicationAsync(context);
            var controller = CreateAdminController(context);

            // No recommendation, but moving to Rejected — should just update
            var result = await controller.UpdateApplicationStage(application.Id, ApplicationStage.Rejected, proceedWithoutApproval: false);

            result.Should().BeOfType<RedirectToActionResult>();

            var updated = await context.Applications.FindAsync(application.Id);
            updated!.Stage.Should().Be(ApplicationStage.Rejected);
        }

        // --- ViewModel: ApplicationStage is loaded into view ---

        [Fact]
        public async Task CandidateDetails_LoadsApplicationStageIntoViewModel()
        {
            using var context = CreateInMemoryContext();
            var application = await SeedApplicationAsync(context);

            application.Stage = ApplicationStage.Screen;
            await context.SaveChangesAsync();

            var controller = CreateAdminController(context);

            var result = await controller.CandidateDetails(application.Id);

            var model = result.Should().BeOfType<ViewResult>().Subject
                .Model.Should().BeAssignableTo<CandidateAdminViewModel>().Subject;
            model.ApplicationStage.Should().Be(ApplicationStage.Screen);
        }

        [Fact]
        public async Task CandidateDetails_WhenStageWarningTempDataSet_SetsRequiresStageApprovalWarning()
        {
            using var context = CreateInMemoryContext();
            var application = await SeedApplicationAsync(context);
            var controller = CreateAdminController(context);

            // Simulate the TempData set by UpdateApplicationStage redirect
            controller.TempData["StageApprovalWarning"] = application.Id;

            var result = await controller.CandidateDetails(application.Id);

            var model = result.Should().BeOfType<ViewResult>().Subject
                .Model.Should().BeAssignableTo<CandidateAdminViewModel>().Subject;
            model.RequiresStageApprovalWarning.Should().BeTrue();
            model.PendingApplicationStage.Should().Be(ApplicationStage.Interview);
        }

        [Fact]
        public async Task CandidateDetails_WithNoTempData_DoesNotSetRequiresStageApprovalWarning()
        {
            using var context = CreateInMemoryContext();
            var application = await SeedApplicationAsync(context);

            // No recommendation, no TempData — warning should NOT be shown
            var controller = CreateAdminController(context);

            var result = await controller.CandidateDetails(application.Id);

            var model = result.Should().BeOfType<ViewResult>().Subject
                .Model.Should().BeAssignableTo<CandidateAdminViewModel>().Subject;
            model.RequiresStageApprovalWarning.Should().BeFalse();
            model.PendingApplicationStage.Should().BeNull();
        }

        // --- Pipeline visual: ApplicationStage is the source of truth, not CurrentJobStageId ---

        [Fact]
        public async Task CandidateDetails_PipelineStageReflectsApplicationStage_NotCurrentJobStageId()
        {
            // Regression test: the Hiring Pipeline card was previously driven by CurrentJobStageId
            // (a job-specific interview stage), which is independent from Application.Stage.
            // This test verifies that CandidateDetails returns the correct ApplicationStage in the
            // viewmodel regardless of what CurrentJobStageId is set to.
            using var context = CreateInMemoryContext();
            var application = await SeedApplicationAsync(context);

            // Seed a job stage and assign it to the application (simulating a hiring-stage assignment)
            var jobStage = new JobStage { JobId = application.JobId, Name = "Technical Round", Order = 1 };
            context.JobStages.Add(jobStage);
            await context.SaveChangesAsync();

            application.CurrentJobStageId = jobStage.Id;
            application.Stage = ApplicationStage.Interview; // application-level pipeline stage
            await context.SaveChangesAsync();

            var controller = CreateAdminController(context);
            var result = await controller.CandidateDetails(application.Id);

            var model = result.Should().BeOfType<ViewResult>().Subject
                .Model.Should().BeAssignableTo<CandidateAdminViewModel>().Subject;

            // The pipeline visual must reflect Application.Stage
            model.ApplicationStage.Should().Be(ApplicationStage.Interview);
        }

        // --- 6. Draft recommendation still blocks stage advance ---

        [Fact]
        public async Task UpdateApplicationStage_ToInterview_AfterSavingDraftViaService_StillRequiresApproval()
        {
            using var context = CreateInMemoryContext();
            var application = await SeedApplicationAsync(context);

            // Save as Draft via service (exactly what RecommendationController does)
            var recService = new RecommendationService(context, new StageOrderService());
            await recService.SaveStage1DraftAsync(application.Id, "Good candidate", "Strong skills", null, true);

            var controller = CreateAdminController(context);
            var result = await controller.UpdateApplicationStage(application.Id, ApplicationStage.Interview, proceedWithoutApproval: false);

            // Stage still blocked
            var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirect.ActionName.Should().Be("CandidateDetails");
            controller.TempData["StageApprovalWarning"].Should().Be(application.Id);

            var unchanged = await context.Applications.FindAsync(application.Id);
            unchanged!.Stage.Should().Be(ApplicationStage.Applied);
        }

        [Fact]
        public async Task UpdateApplicationStage_ToInterview_WithBypass_WhenDraftRecommendationExists_AdvancesStage()
        {
            using var context = CreateInMemoryContext();
            var application = await SeedApplicationAsync(context);

            // Seed an existing Draft recommendation
            context.CandidateRecommendations.Add(new CandidateRecommendation
            {
                ApplicationId = application.Id,
                Stage = RecommendationStage.Stage1,
                Status = RecommendationStatus.Draft,
                LastUpdatedUtc = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var controller = CreateAdminController(context);
            await controller.UpdateApplicationStage(application.Id, ApplicationStage.Interview, proceedWithoutApproval: true);

            var updated = await context.Applications.FindAsync(application.Id);
            updated!.Stage.Should().Be(ApplicationStage.Interview);
        }
    }
}
