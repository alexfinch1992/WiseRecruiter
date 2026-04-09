using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using FluentAssertions;
using JobPortal.Data;
using JobPortal.Models;
using JobPortal.Models.ViewModels;
using JobPortal.Services.Implementations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace WiseRecruiter.Tests.Integration
{
    public class RecommendationControllerTests
    {
        private AppDbContext CreateInMemoryContext() =>
            new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase("rec_ctrl_" + Guid.NewGuid())
                .Options);

        private static async Task<(Candidate candidate, Application application, JobStage stage)> SeedAsync(AppDbContext context)
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

            return (candidate, application, stage);
        }

        // GET: loads empty model when no recommendation exists
        [Fact]
        public async Task Stage1_Get_WhenNoRecommendation_RedirectsToWriteRecommendation()
        {
            using var context = CreateInMemoryContext();
            var (_, application, _) = await SeedAsync(context);
            var controller = new RecommendationController(new RecommendationService(context, new StageOrderService()), context);

            var result = controller.Stage1(application.Id);

            var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirect.ActionName.Should().Be("WriteRecommendation");
            redirect.ControllerName.Should().Be("Candidate");
        }

        // GET: pre-populates fields from existing recommendation
        [Fact]
        public async Task Stage1_Get_WithExistingRecommendation_RedirectsToWriteRecommendation()
        {
            using var context = CreateInMemoryContext();
            var (_, application, _) = await SeedAsync(context);

            context.CandidateRecommendations.Add(new CandidateRecommendation
            {
                ApplicationId = application.Id,
                Stage = RecommendationStage.Stage1,
                Status = RecommendationStatus.Draft,
                Summary = "Existing notes",
                ExperienceFit = "Strong fit",
                Concerns = "Minor concern",
                HireRecommendation = true,
                LastUpdatedUtc = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var controller = new RecommendationController(new RecommendationService(context, new StageOrderService()), context);

            var result = controller.Stage1(application.Id);

            var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirect.ActionName.Should().Be("WriteRecommendation");
            redirect.ControllerName.Should().Be("Candidate");
        }

        // POST: creates new recommendation when none exists
        [Fact]
        public async Task Stage1_Post_WhenNoExistingRec_CreatesNewRecord()
        {
            using var context = CreateInMemoryContext();
            var (_, application, _) = await SeedAsync(context);
            var controller = new RecommendationController(new RecommendationService(context, new StageOrderService()), context);

            var model = new Stage1RecommendationViewModel
            {
                Notes = "Great candidate",
                Strengths = "Excellent C#",
                Concerns = "None",
                HireRecommendation = true
            };

            var result = await controller.Stage1(application.Id, model);

            result.Should().BeOfType<RedirectToActionResult>()
                .Which.ActionName.Should().Be("CandidateDetails");

            var rec = await context.CandidateRecommendations
                .FirstOrDefaultAsync(r => r.ApplicationId == application.Id && r.Stage == RecommendationStage.Stage1);

            rec.Should().NotBeNull();
            rec!.Summary.Should().Be("Great candidate");
            rec.ExperienceFit.Should().Be("Excellent C#");
            rec.Concerns.Should().Be("None");
            rec.HireRecommendation.Should().BeTrue();
            rec.Status.Should().Be(RecommendationStatus.Draft);
        }

        // POST: updates existing recommendation without creating duplicate
        [Fact]
        public async Task Stage1_Post_WithExistingRec_UpdatesInPlace()
        {
            using var context = CreateInMemoryContext();
            var (_, application, _) = await SeedAsync(context);

            context.CandidateRecommendations.Add(new CandidateRecommendation
            {
                ApplicationId = application.Id,
                Stage = RecommendationStage.Stage1,
                Status = RecommendationStatus.Draft,
                Summary = "Old notes",
                LastUpdatedUtc = DateTime.UtcNow.AddDays(-1)
            });
            await context.SaveChangesAsync();

            var controller = new RecommendationController(new RecommendationService(context, new StageOrderService()), context);

            var model = new Stage1RecommendationViewModel
            {
                Notes = "Updated notes",
                Strengths = "Better fit",
                Concerns = null,
                HireRecommendation = false
            };

            await controller.Stage1(application.Id, model);

            var count = await context.CandidateRecommendations
                .CountAsync(r => r.ApplicationId == application.Id && r.Stage == RecommendationStage.Stage1);

            count.Should().Be(1); // no duplicate

            var rec = await context.CandidateRecommendations
                .FirstAsync(r => r.ApplicationId == application.Id);

            rec.Summary.Should().Be("Updated notes");
            rec.ExperienceFit.Should().Be("Better fit");
            rec.HireRecommendation.Should().BeFalse();
        }

        // GET: returns NotFound for unknown applicationId
        [Fact]
        public async Task Stage1_Get_WithUnknownApplicationId_RedirectsToWriteRecommendation()
        {
            using var context = CreateInMemoryContext();
            var controller = new RecommendationController(new RecommendationService(context, new StageOrderService()), context);

            var result = controller.Stage1(applicationId: 9999);

            var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirect.ActionName.Should().Be("WriteRecommendation");
            redirect.ControllerName.Should().Be("Candidate");
        }

        // POST: saving a recommendation never elevates its status to Approved
        [Fact]
        public async Task Stage1_Post_NeverSetsApprovedStatus()
        {
            using var context = CreateInMemoryContext();
            var (_, application, _) = await SeedAsync(context);
            var controller = new RecommendationController(new RecommendationService(context, new StageOrderService()), context);

            var model = new Stage1RecommendationViewModel
            {
                Notes = "Looks great",
                HireRecommendation = true
            };

            await controller.Stage1(application.Id, model);

            var rec = await context.CandidateRecommendations
                .FirstAsync(r => r.ApplicationId == application.Id && r.Stage == RecommendationStage.Stage1);

            rec.Status.Should().Be(RecommendationStatus.Draft);
        }

        // POST: redirects to Admin/CandidateDetails
        [Fact]
        public async Task Stage1_Post_RedirectsToAdminCandidateDetails()
        {
            using var context = CreateInMemoryContext();
            var (_, application, _) = await SeedAsync(context);
            var controller = new RecommendationController(new RecommendationService(context, new StageOrderService()), context);

            var model = new Stage1RecommendationViewModel { Notes = "Test" };

            var result = await controller.Stage1(application.Id, model);

            var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirect.ActionName.Should().Be("CandidateDetails");
            redirect.ControllerName.Should().Be("Admin");
            redirect.RouteValues!["id"].Should().Be(application.Id);
        }

        // ─── SubmitStage1 ─────────────────────────────────────────────────

        private static RecommendationController CreateControllerWithUser(AppDbContext context, int adminId = 1)
        {
            var controller = new RecommendationController(new RecommendationService(context, new StageOrderService()), context);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim("AdminId", adminId.ToString()) }, "Identity.Application"))
                }
            };
            return controller;
        }

        // POST SubmitStage1: Draft → Submitted, redirects to CandidateDetails
        [Fact]
        public async Task Stage1_Submit_WithValidDraft_TransitionsToSubmitted()
        {
            using var context = CreateInMemoryContext();
            var (_, application, _) = await SeedAsync(context);

            context.CandidateRecommendations.Add(new CandidateRecommendation
            {
                ApplicationId = application.Id,
                Stage = RecommendationStage.Stage1,
                Status = RecommendationStatus.Draft,
                Summary = "Good candidate",
                LastUpdatedUtc = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var controller = CreateControllerWithUser(context, adminId: 7);

            var result = await controller.SubmitStage1(application.Id);

            var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirect.ActionName.Should().Be("CandidateDetails");
            redirect.ControllerName.Should().Be("Admin");
            redirect.RouteValues!["id"].Should().Be(application.Id);

            var rec = await context.CandidateRecommendations.FirstAsync(r => r.ApplicationId == application.Id);
            rec.Status.Should().Be(RecommendationStatus.Submitted);
            rec.SubmittedByUserId.Should().Be(7);
            rec.SubmittedUtc.Should().NotBeNull();
        }

        // POST SubmitStage1: no recommendation → 404
        [Fact]
        public async Task Stage1_Submit_WhenNotFound_Returns404()
        {
            using var context = CreateInMemoryContext();
            var (_, application, _) = await SeedAsync(context);
            var controller = CreateControllerWithUser(context);

            var result = await controller.SubmitStage1(application.Id);

            result.Should().BeOfType<NotFoundResult>();
        }

        // POST SubmitStage1: already Submitted → 400
        [Fact]
        public async Task Stage1_Submit_WhenInvalidState_Returns400()
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

            var controller = CreateControllerWithUser(context);

            var result = await controller.SubmitStage1(application.Id);

            result.Should().BeOfType<BadRequestResult>();
        }

        // POST Stage1 Save on Approved rec → succeeds and stays Approved
        [Fact]
        public async Task Stage1_Post_WhenApproved_UpdatesContentAndRedirects()
        {
            using var context = CreateInMemoryContext();
            var (_, application, _) = await SeedAsync(context);

            context.CandidateRecommendations.Add(new CandidateRecommendation
            {
                ApplicationId = application.Id,
                Stage = RecommendationStage.Stage1,
                Status = RecommendationStatus.Approved,
                Summary = "Original content",
                LastUpdatedUtc = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var controller = new RecommendationController(new RecommendationService(context, new StageOrderService()), context);
            var model = new Stage1RecommendationViewModel
            {
                Notes = "Post-approval update",
                Strengths = "Strong fit",
                HireRecommendation = true
            };

            var result = await controller.Stage1(application.Id, model);

            var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirect.ActionName.Should().Be("CandidateDetails");

            var rec = await context.CandidateRecommendations.FirstAsync(r => r.ApplicationId == application.Id);
            rec.Status.Should().Be(RecommendationStatus.Approved);
            rec.Summary.Should().Be("Post-approval update");
            rec.ExperienceFit.Should().Be("Strong fit");
        }
    }
}
