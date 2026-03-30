using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using FluentAssertions;
using JobPortal.Data;
using JobPortal.Models;
using JobPortal.Models.ViewModels;
using JobPortal.Services.Implementations;
using JobPortal.Services.Interfaces;
using JobPortal.Services.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace WiseRecruiter.Tests.Integration
{
    public class RecommendationAdminControllerTests
    {
        private AppDbContext CreateInMemoryContext() =>
            new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase("rec_admin_ctrl_" + Guid.NewGuid())
                .Options);

        private static RecommendationAdminController CreateController(AppDbContext context, int adminId = 1)
        {
            var controller = new RecommendationAdminController(new RecommendationService(context, new StageOrderService()));
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim("AdminId", adminId.ToString()) },
                        "Identity.Application"))
                }
            };
            return controller;
        }

        private static async Task<(Application application, CandidateRecommendation rec)> SeedAsync(
            AppDbContext context, RecommendationStatus status = RecommendationStatus.Draft)
        {
            var candidate = new Candidate
            {
                FirstName = "Jane",
                LastName = "Smith",
                Email = $"jane_{Guid.NewGuid()}@example.com",
                CreatedAt = DateTime.UtcNow
            };
            context.Candidates.Add(candidate);
            await context.SaveChangesAsync();

            var job = new Job { Title = "Engineer" };
            context.Jobs.Add(job);
            await context.SaveChangesAsync();

            var stage = new JobStage { JobId = job.Id, Name = "Applied", Order = 1 };
            context.JobStages.Add(stage);
            await context.SaveChangesAsync();

            var application = new Application
            {
                CandidateId = candidate.Id,
                JobId = job.Id,
                Name = "Jane Smith",
                Email = candidate.Email,
                City = "Sydney",
                CurrentJobStageId = stage.Id
            };
            context.Applications.Add(application);
            await context.SaveChangesAsync();

            var rec = new CandidateRecommendation
            {
                ApplicationId = application.Id,
                Stage = RecommendationStage.Stage1,
                Status = status,
                Summary = "Solid candidate",
                LastUpdatedUtc = DateTime.UtcNow
            };
            context.CandidateRecommendations.Add(rec);
            await context.SaveChangesAsync();

            return (application, rec);
        }

        // GET Pending: returns view with pending recommendation list (Submitted only)
        [Fact]
        public async Task Pending_ReturnsViewWithPendingList()
        {
            using var context = CreateInMemoryContext();
            await SeedAsync(context, RecommendationStatus.Submitted);
            var controller = CreateController(context);

            var result = await controller.Pending();

            var view = result.Should().BeOfType<ViewResult>().Subject;
            var model = view.Model.Should().BeAssignableTo<List<PendingRecommendationDto>>().Subject;
            model.Should().HaveCount(1);
            model[0].CandidateName.Should().Be("Jane Smith");
        }

        // GET Pending: draft and approved recs excluded
        [Fact]
        public async Task Pending_ExcludesApprovedRecommendations()
        {
            using var context = CreateInMemoryContext();
            await SeedAsync(context, RecommendationStatus.Approved);
            var controller = CreateController(context);

            var result = await controller.Pending();

            var view = result.Should().BeOfType<ViewResult>().Subject;
            var model = view.Model.Should().BeAssignableTo<List<PendingRecommendationDto>>().Subject;
            model.Should().BeEmpty();
        }

        // GET Review: returns view with full recommendation context (Stage 1)
        [Fact]
        public async Task Review_WithStage1_ReturnsStage1ViewAndModel()
        {
            using var context = CreateInMemoryContext();
            var (application, _) = await SeedAsync(context);
            var controller = CreateController(context);

            var result = await controller.Review(application.Id, RecommendationStage.Stage1);

            var view = result.Should().BeOfType<ViewResult>().Subject;
            view.ViewName.Should().Be("Stage1Review");
            var model = view.Model.Should().BeOfType<Stage1ReviewViewModel>().Subject;
            model.ApplicationId.Should().Be(application.Id);
            model.CandidateName.Should().Be("Jane Smith");
            model.Recommendation.Should().NotBeNull();
        }

        // GET Review: non-existent application returns NotFound
        [Fact]
        public async Task Review_WithInvalidId_ReturnsNotFound()
        {
            using var context = CreateInMemoryContext();
            var controller = CreateController(context);

            var result = await controller.Review(applicationId: 9999, RecommendationStage.Stage1);

            result.Should().BeOfType<NotFoundResult>();
        }

        // GET Review: Stage 2 returns Stage 2 view and model
        [Fact]
        public async Task Review_WithStage2_ReturnsStage2ViewAndModel()
        {
            using var context = CreateInMemoryContext();
            var (application, _) = await SeedStage2Async(context, RecommendationStatus.Submitted);
            var controller = CreateController(context);

            var result = await controller.Review(application.Id, RecommendationStage.Stage2);

            var view = result.Should().BeOfType<ViewResult>().Subject;
            view.ViewName.Should().Be("Stage2Review");
            var model = view.Model.Should().BeOfType<Stage2ReviewViewModel>().Subject;
            model.ApplicationId.Should().Be(application.Id);
            model.CandidateName.Should().Be("Stage2 Candidate");
            model.Recommendation.Should().NotBeNull();
        }

        // GET Review: Stage 2 with non-existent application returns NotFound
        [Fact]
        public async Task Review_WithStage2_InvalidId_ReturnsNotFound()
        {
            using var context = CreateInMemoryContext();
            var controller = CreateController(context);

            var result = await controller.Review(applicationId: 9999, RecommendationStage.Stage2);

            result.Should().BeOfType<NotFoundResult>();
        }

        // GET Review: Stage 1 does NOT return Stage 2 view
        [Fact]
        public async Task Review_WithStage1_DoesNotReturnStage2View()
        {
            using var context = CreateInMemoryContext();
            var (application, _) = await SeedAsync(context);
            var controller = CreateController(context);

            var result = await controller.Review(application.Id, RecommendationStage.Stage1);

            var view = result.Should().BeOfType<ViewResult>().Subject;
            view.ViewName.Should().NotBe("Stage2Review");
        }

        // GET Review: Stage 2 does NOT return Stage 1 view
        [Fact]
        public async Task Review_WithStage2_DoesNotReturnStage1View()
        {
            using var context = CreateInMemoryContext();
            var (application, _) = await SeedStage2Async(context, RecommendationStatus.Submitted);
            var controller = CreateController(context);

            var result = await controller.Review(application.Id, RecommendationStage.Stage2);

            var view = result.Should().BeOfType<ViewResult>().Subject;
            view.ViewName.Should().NotBe("Stage1Review");
        }

        // REGRESSION: missing stage parameter returns BadRequest (no silent fallback to Stage 1)
        [Fact]
        public async Task Review_WithNullStage_ReturnsBadRequest()
        {
            using var context = CreateInMemoryContext();
            var (application, _) = await SeedAsync(context);
            var controller = CreateController(context);

            var result = await controller.Review(application.Id, stage: null);

            result.Should().BeOfType<BadRequestObjectResult>();
        }

        // REGRESSION: Stage 2 review must open Stage2Review view, never Stage1Review
        [Fact]
        public async Task Review_WithStage2_NeverOpensStage1ReviewView()
        {
            using var context = CreateInMemoryContext();
            var (application, _) = await SeedStage2Async(context, RecommendationStatus.Submitted);
            var controller = CreateController(context);

            var result = await controller.Review(application.Id, RecommendationStage.Stage2);

            var view = result.Should().BeOfType<ViewResult>().Subject;
            view.ViewName.Should().Be("Stage2Review");
            view.Model.Should().BeOfType<Stage2ReviewViewModel>();
        }

        // REGRESSION: Stage 1 review must open Stage1Review view, never Stage2Review
        [Fact]
        public async Task Review_WithStage1_NeverOpensStage2ReviewView()
        {
            using var context = CreateInMemoryContext();
            var (application, _) = await SeedAsync(context, RecommendationStatus.Submitted);
            var controller = CreateController(context);

            var result = await controller.Review(application.Id, RecommendationStage.Stage1);

            var view = result.Should().BeOfType<ViewResult>().Subject;
            view.ViewName.Should().Be("Stage1Review");
            view.Model.Should().BeOfType<Stage1ReviewViewModel>();
        }

        // POST Approve: approves Submitted rec and redirects to CandidateDetails admin page
        [Fact]
        public async Task Approve_WithValidId_ApprovesAndRedirectsToCandidateDetails()
        {
            using var context = CreateInMemoryContext();
            var (application, _) = await SeedAsync(context, RecommendationStatus.Submitted);
            var controller = CreateController(context, adminId: 42);

            var result = await controller.Approve(application.Id);

            var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirect.ActionName.Should().Be("CandidateDetails");
            redirect.ControllerName.Should().Be("Admin");

            var rec = await context.CandidateRecommendations
                .FirstAsync(r => r.ApplicationId == application.Id);
            rec.Status.Should().Be(RecommendationStatus.Approved);
            rec.ReviewedByUserId.Should().Be(42);
        }

        // POST Approve: non-existent recommendation returns NotFound
        [Fact]
        public async Task Approve_WhenNoRecExists_ReturnsNotFound()
        {
            using var context = CreateInMemoryContext();
            // Seed application without a recommendation
            var candidate = new Candidate
            {
                FirstName = "No",
                LastName = "Rec",
                Email = $"norec_{Guid.NewGuid()}@example.com",
                CreatedAt = DateTime.UtcNow
            };
            context.Candidates.Add(candidate);
            var job = new Job { Title = "Dev" };
            context.Jobs.Add(job);
            await context.SaveChangesAsync();
            var application = new Application
            {
                CandidateId = candidate.Id,
                JobId = job.Id,
                Name = "No Rec",
                Email = candidate.Email,
                City = "Sydney"
            };
            context.Applications.Add(application);
            await context.SaveChangesAsync();

            var controller = CreateController(context, adminId: 1);
            var result = await controller.Approve(application.Id);

            result.Should().BeOfType<NotFoundResult>();
        }

        // POST Approve: already-approved recommendation returns BadRequest
        [Fact]
        public async Task Approve_WhenAlreadyApproved_ReturnsBadRequest()
        {
            using var context = CreateInMemoryContext();
            var (application, _) = await SeedAsync(context, RecommendationStatus.Approved);
            var controller = CreateController(context, adminId: 1);

            var result = await controller.Approve(application.Id);

            result.Should().BeOfType<BadRequestResult>();
        }

        // LIFECYCLE: bypass → save rec → submit → approve → bypass cleared
        [Fact]
        public async Task Stage1_BypassThenApprove_ClearsBypassFlag()
        {
            using var context = CreateInMemoryContext();
            var (_, application, _) = await SeedApplicationAsync(context);

            // Step 2: move to Interview with bypass (no prior recommendation)
            var recommendationService = new RecommendationService(context, new StageOrderService());
            var stageService = new ApplicationStageService(context, recommendationService);
            await stageService.UpdateStageAsync(
                application.Id, ApplicationStage.Interview, proceedWithoutApproval: true, userId: "1");
            await context.SaveChangesAsync();

            // Step 3: assert bypass was recorded
            var recAfterBypass = await context.CandidateRecommendations
                .FirstAsync(r => r.ApplicationId == application.Id);
            recAfterBypass.BypassedApproval.Should().BeTrue();

            // Step 4: save recommendation notes (still Draft)
            await recommendationService.SaveStage1DraftAsync(
                application.Id, "Strong candidate", "Great skills", null, true);

            // Step 5: manually advance to Submitted (submit feature not yet implemented)
            var recDraft = await context.CandidateRecommendations
                .FirstAsync(r => r.ApplicationId == application.Id);
            recDraft.Status = RecommendationStatus.Submitted;
            await context.SaveChangesAsync();

            // C: Assert bypass is still recorded after transition to Submitted
            recDraft.BypassedApproval.Should().BeTrue();

            // Step 6: approve via controller
            var adminController = CreateController(context, adminId: 99);
            var approveResult = await adminController.Approve(application.Id);
            approveResult.Should().BeOfType<RedirectToActionResult>();

            // Step 7: assert bypass cleared
            var recAfterApproval = await context.CandidateRecommendations
                .FirstAsync(r => r.ApplicationId == application.Id);
            recAfterApproval.Status.Should().Be(RecommendationStatus.Approved);
            recAfterApproval.BypassedApproval.Should().BeFalse();
            recAfterApproval.BypassReason.Should().BeNull();
        }

        // Helper for lifecycle test
        private static async Task<(Candidate candidate, Application application, JobStage stage)> SeedApplicationAsync(AppDbContext context)
        {
            var candidate = new Candidate
            {
                FirstName = "Test",
                LastName = "Candidate",
                Email = $"test_{Guid.NewGuid()}@example.com",
                CreatedAt = DateTime.UtcNow
            };
            context.Candidates.Add(candidate);
            await context.SaveChangesAsync();

            var job = new Job { Title = "Developer" };
            context.Jobs.Add(job);
            await context.SaveChangesAsync();

            var stage = new JobStage { JobId = job.Id, Name = "Applied", Order = 1 };
            context.JobStages.Add(stage);
            await context.SaveChangesAsync();

            var application = new Application
            {
                CandidateId = candidate.Id,
                JobId = job.Id,
                Name = "Test Candidate",
                Email = candidate.Email,
                City = "Sydney",
                CurrentJobStageId = stage.Id
            };
            context.Applications.Add(application);
            await context.SaveChangesAsync();

            return (candidate, application, stage);
        }

        // PART 4 — Auth guard: unauthorized approver gets 403
        [Fact]
        public async Task Approve_WhenUnauthorized_Returns403()
        {
            using var context = CreateInMemoryContext();
            var (application, _) = await SeedAsync(context, RecommendationStatus.Submitted);

            var controller = new RecommendationAdminController(
                new RecommendationService(context, new StageOrderService(), authService: new DenyAllAuthService()));
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim("AdminId", "1") }, "Identity.Application"))
                }
            };

            var result = await controller.Approve(application.Id);

            result.Should().BeOfType<ForbidResult>();

            // Record must be unchanged
            var rec = await context.CandidateRecommendations.FirstAsync(r => r.ApplicationId == application.Id);
            rec.Status.Should().Be(RecommendationStatus.Submitted);
        }

        // PART 6 — Pending list shows ONLY Submitted recommendations
        [Fact]
        public async Task Pending_OnlyShowsSubmittedRecommendations()
        {
            using var context = CreateInMemoryContext();

            // Shared application infrastructure
            var candidate = new Candidate
            {
                FirstName = "Filter",
                LastName = "Test",
                Email = $"filter_{Guid.NewGuid()}@example.com",
                CreatedAt = DateTime.UtcNow
            };
            context.Candidates.Add(candidate);
            var job = new Job { Title = "Any" };
            context.Jobs.Add(job);
            await context.SaveChangesAsync();
            var jobStage = new JobStage { JobId = job.Id, Name = "Applied", Order = 1 };
            context.JobStages.Add(jobStage);
            await context.SaveChangesAsync();

            Application MakeApplication(string name) => new Application
            {
                CandidateId = candidate.Id,
                JobId = job.Id,
                Name = name,
                Email = candidate.Email,
                City = "Sydney",
                CurrentJobStageId = jobStage.Id
            };

            var appDraft     = MakeApplication("Draft Candidate");
            var appSubmitted = MakeApplication("Submitted Candidate");
            var appApproved  = MakeApplication("Approved Candidate");
            context.Applications.AddRange(appDraft, appSubmitted, appApproved);
            await context.SaveChangesAsync();

            context.CandidateRecommendations.Add(new CandidateRecommendation
            {
                ApplicationId = appDraft.Id,
                Stage = RecommendationStage.Stage1,
                Status = RecommendationStatus.Draft,
                LastUpdatedUtc = DateTime.UtcNow
            });
            context.CandidateRecommendations.Add(new CandidateRecommendation
            {
                ApplicationId = appSubmitted.Id,
                Stage = RecommendationStage.Stage1,
                Status = RecommendationStatus.Submitted,
                LastUpdatedUtc = DateTime.UtcNow
            });
            context.CandidateRecommendations.Add(new CandidateRecommendation
            {
                ApplicationId = appApproved.Id,
                Stage = RecommendationStage.Stage1,
                Status = RecommendationStatus.Approved,
                LastUpdatedUtc = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var controller = CreateController(context);
            var result = await controller.Pending();

            var view = result.Should().BeOfType<ViewResult>().Subject;
            var model = view.Model.Should().BeAssignableTo<List<PendingRecommendationDto>>().Subject;

            model.Should().HaveCount(1);
            model[0].CandidateName.Should().Be("Submitted Candidate");
        }

        // Pending includes Stage 2 submitted recommendations
        [Fact]
        public async Task Pending_IncludesSubmittedStage2Recommendations()
        {
            using var context = CreateInMemoryContext();
            var (_, _) = await SeedStage2Async(context, RecommendationStatus.Submitted);
            var controller = CreateController(context);

            var result = await controller.Pending();

            var view = result.Should().BeOfType<ViewResult>().Subject;
            var model = view.Model.Should().BeAssignableTo<List<PendingRecommendationDto>>().Subject;

            model.Should().HaveCount(1);
            model[0].Stage.Should().Be(RecommendationStage.Stage2);
            model[0].CandidateName.Should().Be("Stage2 Candidate");
        }

        // Pending includes both Stage 1 and Stage 2 submitted at the same time
        [Fact]
        public async Task Pending_IncludesBothStage1AndStage2_WhenBothSubmitted()
        {
            using var context = CreateInMemoryContext();
            await SeedAsync(context, RecommendationStatus.Submitted);
            await SeedStage2Async(context, RecommendationStatus.Submitted);
            var controller = CreateController(context);

            var result = await controller.Pending();

            var view = result.Should().BeOfType<ViewResult>().Subject;
            var model = view.Model.Should().BeAssignableTo<List<PendingRecommendationDto>>().Subject;

            model.Should().HaveCount(2);
            model.Should().Contain(m => m.Stage == RecommendationStage.Stage1);
            model.Should().Contain(m => m.Stage == RecommendationStage.Stage2);
        }

        // Stage 2 Draft is excluded from pending
        [Fact]
        public async Task Pending_ExcludesStage2DraftRecommendations()
        {
            using var context = CreateInMemoryContext();
            await SeedStage2Async(context, RecommendationStatus.Draft);
            var controller = CreateController(context);

            var result = await controller.Pending();

            var view = result.Should().BeOfType<ViewResult>().Subject;
            var model = view.Model.Should().BeAssignableTo<List<PendingRecommendationDto>>().Subject;
            model.Should().BeEmpty();
        }

        private sealed class DenyAllAuthService : IStageAuthorizationService
        {
            public bool CanApproveStage1(int userId) => false;
            public bool CanApproveStage2(int userId) => false;
        }

        private static async Task<(Application application, CandidateRecommendation rec)> SeedStage2Async(
            AppDbContext context, RecommendationStatus status = RecommendationStatus.Submitted)
        {
            var candidate = new Candidate
            {
                FirstName = "Stage2",
                LastName = "Candidate",
                Email = $"s2_{Guid.NewGuid()}@example.com",
                CreatedAt = DateTime.UtcNow
            };
            context.Candidates.Add(candidate);
            await context.SaveChangesAsync();

            var job = new Job { Title = "Senior Engineer" };
            context.Jobs.Add(job);
            await context.SaveChangesAsync();

            var stage = new JobStage { JobId = job.Id, Name = "Applied", Order = 1 };
            context.JobStages.Add(stage);
            await context.SaveChangesAsync();

            var application = new Application
            {
                CandidateId = candidate.Id,
                JobId = job.Id,
                Name = "Stage2 Candidate",
                Email = candidate.Email,
                City = "Melbourne",
                CurrentJobStageId = stage.Id
            };
            context.Applications.Add(application);
            await context.SaveChangesAsync();

            var rec = new CandidateRecommendation
            {
                ApplicationId = application.Id,
                Stage = RecommendationStage.Stage2,
                Status = status,
                Summary = "Strong Stage 2 candidate",
                LastUpdatedUtc = DateTime.UtcNow
            };
            context.CandidateRecommendations.Add(rec);
            await context.SaveChangesAsync();

            return (application, rec);
        }

        // Stage 2 — POST ApproveStage2: authorized manager approves and redirects to CandidateDetails
        [Fact]
        public async Task ApproveStage2_WhenAuthorized_ApprovesAndRedirectsToCandidateDetails()
        {
            using var context = CreateInMemoryContext();
            var (application, _) = await SeedStage2Async(context, RecommendationStatus.Submitted);
            var controller = CreateController(context, adminId: 55);

            var result = await controller.ApproveStage2(application.Id);

            var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirect.ActionName.Should().Be("CandidateDetails");
            redirect.ControllerName.Should().Be("Admin");

            var rec = await context.CandidateRecommendations
                .FirstAsync(r => r.ApplicationId == application.Id && r.Stage == RecommendationStage.Stage2);
            rec.Status.Should().Be(RecommendationStatus.Approved);
            rec.ReviewedByUserId.Should().Be(55);
        }

        // Stage 2 — POST ApproveStage2: unauthorized (DenyAll) returns 403
        [Fact]
        public async Task ApproveStage2_WhenForbidden_Returns403()
        {
            using var context = CreateInMemoryContext();
            var (application, _) = await SeedStage2Async(context, RecommendationStatus.Submitted);

            var controller = new RecommendationAdminController(
                new RecommendationService(context, new StageOrderService(), authService: new DenyAllAuthService()));
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim("AdminId", "1") }, "Identity.Application"))
                }
            };

            var result = await controller.ApproveStage2(application.Id);

            result.Should().BeOfType<ForbidResult>();

            var rec = await context.CandidateRecommendations
                .FirstAsync(r => r.ApplicationId == application.Id && r.Stage == RecommendationStage.Stage2);
            rec.Status.Should().Be(RecommendationStatus.Submitted);
        }
    }
}