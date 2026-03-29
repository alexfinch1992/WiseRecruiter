using System;
using System.Linq;
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
    public class RecommendationTests
    {
        private AppDbContext CreateInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: "recommendation_db_" + Guid.NewGuid())
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

        private static async Task<(Candidate candidate, Application application, JobStage stage)> SeedAsync(AppDbContext context)
        {
            var candidate = new Candidate
            {
                FirstName = "Jane",
                LastName = "Doe",
                Email = $"jane_{Guid.NewGuid()}@example.com",
                CreatedAt = DateTime.UtcNow
            };
            context.Candidates.Add(candidate);

            var job = new Job { Title = "Software Engineer", Description = "Test role" };
            context.Jobs.Add(job);
            await context.SaveChangesAsync();

            var stage = new JobStage { JobId = job.Id, Name = "Technical Interview", Order = 2 };
            context.JobStages.Add(stage);
            await context.SaveChangesAsync();

            var application = new Application
            {
                Name = "Jane Doe",
                Email = candidate.Email,
                City = "Sydney",
                JobId = job.Id,
                CandidateId = candidate.Id,
                CurrentJobStageId = stage.Id
            };
            context.Applications.Add(application);
            await context.SaveChangesAsync();

            return (candidate, application, stage);
        }

        [Fact]
        public async Task CandidateDetails_WhenNoRecommendation_SetsWarningFlag()
        {
            using var context = CreateInMemoryContext();
            var (_, application, _) = await SeedAsync(context);
            var controller = CreateAdminController(context);

            var result = await controller.CandidateDetails(application.Id);

            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var model = viewResult.Model.Should().BeAssignableTo<CandidateAdminViewModel>().Subject;
            model.RequiresStage1ApprovalWarning.Should().BeTrue();
            model.Recommendations.Should().NotContain(r => r.Stage == RecommendationStage.Stage1);
        }

        [Fact]
        public async Task CandidateDetails_WhenApproved_NoWarningFlag()
        {
            using var context = CreateInMemoryContext();
            var (_, application, _) = await SeedAsync(context);

            context.CandidateRecommendations.Add(new CandidateRecommendation
            {
                ApplicationId = application.Id,
                Stage = RecommendationStage.Stage1,
                Status = RecommendationStatus.Approved,
                LastUpdatedUtc = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var controller = CreateAdminController(context);
            var result = await controller.CandidateDetails(application.Id);

            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var model = viewResult.Model.Should().BeAssignableTo<CandidateAdminViewModel>().Subject;
            model.RequiresStage1ApprovalWarning.Should().BeFalse();
            var stage1 = model.Recommendations.First(r => r.Stage == RecommendationStage.Stage1);
            stage1.Status.Should().Be(RecommendationStatus.Approved);
        }

        [Fact]
        public async Task CandidateDetails_WhenSubmitted_StillShowsWarning()
        {
            using var context = CreateInMemoryContext();
            var (_, application, _) = await SeedAsync(context);

            context.CandidateRecommendations.Add(new CandidateRecommendation
            {
                ApplicationId = application.Id,
                Stage = RecommendationStage.Stage1,
                Status = RecommendationStatus.Submitted,
                LastUpdatedUtc = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var controller = CreateAdminController(context);
            var result = await controller.CandidateDetails(application.Id);

            var model = result.Should().BeOfType<ViewResult>().Subject
                .Model.Should().BeAssignableTo<CandidateAdminViewModel>().Subject;
            model.RequiresStage1ApprovalWarning.Should().BeTrue();
        }

        [Fact]
        public async Task CreateInterview_WithBypass_SchedulesSuccessfully()
        {
            using var context = CreateInMemoryContext();
            var (candidate, application, stage) = await SeedAsync(context);
            var controller = CreateAdminController(context);

            var result = await controller.CreateInterview(
                candidate.Id, application.Id, $"stage:{stage.Id}",
                DateTime.UtcNow.AddDays(3),
                proceedWithoutApproval: true,
                bypassReason: "Urgent hire");

            result.Should().BeOfType<RedirectToActionResult>();
            var interviews = await context.Interviews.ToListAsync();
            interviews.Should().HaveCount(1);
        }

        [Fact]
        public async Task CreateInterview_WithBypass_RecordsBypass()
        {
            using var context = CreateInMemoryContext();
            var (candidate, application, stage) = await SeedAsync(context);
            var controller = CreateAdminController(context);

            await controller.CreateInterview(
                candidate.Id, application.Id, $"stage:{stage.Id}",
                DateTime.UtcNow.AddDays(3),
                proceedWithoutApproval: true,
                bypassReason: "Test bypass reason");

            var rec = await context.CandidateRecommendations
                .FirstOrDefaultAsync(r => r.ApplicationId == application.Id);

            rec.Should().NotBeNull();
            rec!.BypassedApproval.Should().BeTrue();
            rec.BypassReason.Should().Be("Test bypass reason");
            rec.BypassedUtc.Should().NotBeNull();
        }

        [Fact]
        public async Task CreateInterview_WithoutBypass_DoesNotRecordBypass()
        {
            using var context = CreateInMemoryContext();
            var (candidate, application, stage) = await SeedAsync(context);
            var controller = CreateAdminController(context);

            await controller.CreateInterview(
                candidate.Id, application.Id, $"stage:{stage.Id}",
                DateTime.UtcNow.AddDays(3));

            var rec = await context.CandidateRecommendations
                .FirstOrDefaultAsync(r => r.ApplicationId == application.Id);

            // No bypass recorded when proceedWithoutApproval was not set
            rec.Should().BeNull();
        }

        [Fact]
        public async Task CreateInterview_WhenAlreadyApproved_DoesNotRecordBypass()
        {
            using var context = CreateInMemoryContext();
            var (candidate, application, stage) = await SeedAsync(context);

            context.CandidateRecommendations.Add(new CandidateRecommendation
            {
                ApplicationId = application.Id,
                Stage = RecommendationStage.Stage1,
                Status = RecommendationStatus.Approved,
                LastUpdatedUtc = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var controller = CreateAdminController(context);

            await controller.CreateInterview(
                candidate.Id, application.Id, $"stage:{stage.Id}",
                DateTime.UtcNow.AddDays(3),
                proceedWithoutApproval: true,  // set but should be ignored since already approved
                bypassReason: "Should not be recorded");

            var rec = await context.CandidateRecommendations
                .FirstAsync(r => r.ApplicationId == application.Id);

            rec.BypassedApproval.Should().BeFalse();
            rec.BypassReason.Should().BeNull();
        }

        [Fact]
        public async Task Stage1Recommendation_CreatesRecord_WhenNoneExists()
        {
            using var context = CreateInMemoryContext();
            var (_, application, _) = await SeedAsync(context);
            var controller = new RecommendationController(new RecommendationService(context, new StageOrderService()));

            var model = new Stage1RecommendationViewModel
            {
                Notes = "Strong candidate",
                Strengths = "Good fit",
                Concerns = "None",
                HireRecommendation = true
            };

            var result = await controller.Stage1(application.Id, model);

            result.Should().BeOfType<RedirectToActionResult>();

            var rec = await context.CandidateRecommendations
                .FirstOrDefaultAsync(r => r.ApplicationId == application.Id && r.Stage == RecommendationStage.Stage1);

            rec.Should().NotBeNull();
            rec!.Status.Should().Be(RecommendationStatus.Draft);
            rec.Summary.Should().Be("Strong candidate");
            rec.ExperienceFit.Should().Be("Good fit");
            rec.Concerns.Should().Be("None");
            rec.HireRecommendation.Should().BeTrue();
        }

        [Fact]
        public async Task Stage1Recommendation_UpdatesExistingRecord()
        {
            using var context = CreateInMemoryContext();
            var (_, application, _) = await SeedAsync(context);

            // Seed existing recommendation
            context.CandidateRecommendations.Add(new CandidateRecommendation
            {
                ApplicationId = application.Id,
                Stage = RecommendationStage.Stage1,
                Status = RecommendationStatus.Draft,
                Summary = "Old summary",
                LastUpdatedUtc = DateTime.UtcNow.AddDays(-1)
            });
            await context.SaveChangesAsync();

            var controller = new RecommendationController(new RecommendationService(context, new StageOrderService()));

            var model = new Stage1RecommendationViewModel
            {
                Notes = "Updated summary",
                Strengths = "New trajectory",
                Concerns = null,
                HireRecommendation = null
            };

            await controller.Stage1(application.Id, model);

            var recs = await context.CandidateRecommendations
                .Where(r => r.ApplicationId == application.Id && r.Stage == RecommendationStage.Stage1)
                .ToListAsync();

            // No duplicate created
            recs.Should().HaveCount(1);
            recs[0].Summary.Should().Be("Updated summary");
            recs[0].ExperienceFit.Should().Be("New trajectory");
            recs[0].Status.Should().Be(RecommendationStatus.Draft); // status unchanged
        }

        [Fact]
        public async Task CandidateDetails_WhenStage1Approved_ContainsStage2Recommendation()
        {
            using var context = CreateInMemoryContext();
            var (_, application, _) = await SeedAsync(context);
            application.Stage = ApplicationStage.Screen;
            await context.SaveChangesAsync();

            // Seed an approved Stage 1 rec AND a Stage 2 rec (auto-created by system)
            context.CandidateRecommendations.Add(new CandidateRecommendation
            {
                ApplicationId = application.Id,
                Stage = RecommendationStage.Stage1,
                Status = RecommendationStatus.Approved,
                LastUpdatedUtc = DateTime.UtcNow
            });
            context.CandidateRecommendations.Add(new CandidateRecommendation
            {
                ApplicationId = application.Id,
                Stage = RecommendationStage.Stage2,
                Status = RecommendationStatus.Draft,
                LastUpdatedUtc = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var controller = CreateAdminController(context);
            var result = await controller.CandidateDetails(application.Id);

            var model = result.Should().BeOfType<ViewResult>().Subject
                .Model.Should().BeAssignableTo<CandidateAdminViewModel>().Subject;

            model.Recommendations.Should().Contain(r => r.Stage == RecommendationStage.Stage2,
                "Stage 2 recommendation should appear in the ViewModel after Stage 1 is approved");
            model.Recommendations.First(r => r.Stage == RecommendationStage.Stage2)
                .Status.Should().Be(RecommendationStatus.Draft);
        }

        [Fact]
        public async Task Stage2Recommendation_CreatesRecord_WhenNoneExists()
        {
            using var context = CreateInMemoryContext();
            var (_, application, _) = await SeedAsync(context);
            var controller = new RecommendationController(new RecommendationService(context, new StageOrderService()));

            var model = new Stage2RecommendationViewModel
            {
                Notes = "Stage 2 notes",
                Strengths = "Leadership",
                Concerns = "Communication",
                HireRecommendation = true
            };

            var result = await controller.Stage2(application.Id, model);

            result.Should().BeOfType<RedirectToActionResult>();

            var rec = await context.CandidateRecommendations
                .FirstOrDefaultAsync(r => r.ApplicationId == application.Id && r.Stage == RecommendationStage.Stage2);

            rec.Should().NotBeNull();
            rec!.Status.Should().Be(RecommendationStatus.Draft);
            rec.Summary.Should().Be("Stage 2 notes");
            rec.ExperienceFit.Should().Be("Leadership");
        }

        [Fact]
        public async Task ApproveStage1_ViaService_AutoCreatesStage2InCandidateDetails()
        {
            using var context = CreateInMemoryContext();
            var (_, application, _) = await SeedAsync(context);
            application.Stage = ApplicationStage.Screen;
            await context.SaveChangesAsync();

            context.CandidateRecommendations.Add(new CandidateRecommendation
            {
                ApplicationId = application.Id,
                Stage = RecommendationStage.Stage1,
                Status = RecommendationStatus.Submitted,
                SubmittedByUserId = 1,
                SubmittedUtc = DateTime.UtcNow,
                LastUpdatedUtc = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var service = new RecommendationService(context, new StageOrderService());
            await service.ApproveStage1RecommendationAsync(application.Id, userId: 99);

            var controller = CreateAdminController(context);
            var result = await controller.CandidateDetails(application.Id);

            var model = result.Should().BeOfType<ViewResult>().Subject
                .Model.Should().BeAssignableTo<CandidateAdminViewModel>().Subject;

            model.Recommendations.Should().Contain(r => r.Stage == RecommendationStage.Stage2,
                "CandidateDetails should show Stage 2 after it was auto-created by Stage 1 approval");
        }
    }
}
