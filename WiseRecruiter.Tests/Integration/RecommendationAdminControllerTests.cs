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
            var controller = new RecommendationAdminController(new RecommendationService(context));
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

        // GET Review: returns view with full recommendation context
        [Fact]
        public async Task Review_WithValidId_ReturnsViewWithModel()
        {
            using var context = CreateInMemoryContext();
            var (application, _) = await SeedAsync(context);
            var controller = CreateController(context);

            var result = await controller.Review(application.Id);

            var view = result.Should().BeOfType<ViewResult>().Subject;
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

            var result = await controller.Review(applicationId: 9999);

            result.Should().BeOfType<NotFoundResult>();
        }

        // POST Approve: approves Submitted rec and redirects to Pending
        [Fact]
        public async Task Approve_WithValidId_ApprovesAndRedirectsToPending()
        {
            using var context = CreateInMemoryContext();
            var (application, _) = await SeedAsync(context, RecommendationStatus.Submitted);
            var controller = CreateController(context, adminId: 42);

            var result = await controller.Approve(application.Id);

            var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirect.ActionName.Should().Be("Pending");

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
            var recommendationService = new RecommendationService(context);
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
                new RecommendationService(context, authService: new DenyAllAuthService()));
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim("AdminId", "1") }, "AdminAuth"))
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

        private sealed class DenyAllAuthService : IStageAuthorizationService
        {
            public bool CanApproveStage1(int userId) => false;
            public bool CanApproveStage2(int userId) => false;
        }
    }
}