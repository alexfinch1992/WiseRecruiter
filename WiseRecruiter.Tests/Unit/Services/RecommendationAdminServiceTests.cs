using System;
using System.Threading.Tasks;
using FluentAssertions;
using JobPortal.Data;
using JobPortal.Models;
using JobPortal.Services.Implementations;
using JobPortal.Services.Interfaces;
using JobPortal.Services.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace WiseRecruiter.Tests.Unit.Services
{
    public class RecommendationAdminServiceTests
    {
        private AppDbContext CreateInMemoryContext() =>
            new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase("rec_admin_service_" + Guid.NewGuid())
                .Options);

        private static async Task<Application> SeedApplicationAsync(AppDbContext context, string candidateName = "Test User")
        {
            var candidate = new Candidate
            {
                FirstName = "Test",
                LastName = "User",
                Email = $"test_{Guid.NewGuid()}@example.com",
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
                Name = candidateName,
                Email = candidate.Email,
                City = "Sydney",
                CurrentJobStageId = stage.Id
            };
            context.Applications.Add(application);
            await context.SaveChangesAsync();

            return application;
        }

        // --- GetPendingRecommendationsAsync ---

        // 1. Pending list includes ONLY Submitted recommendations (Stage 1)
        [Fact]
        public async Task GetPendingRecommendations_ReturnsSubmittedRecsOnly()
        {
            using var context = CreateInMemoryContext();
            var app1 = await SeedApplicationAsync(context, "Alice");
            var app2 = await SeedApplicationAsync(context, "Bob");

            context.CandidateRecommendations.Add(new CandidateRecommendation
            {
                ApplicationId = app1.Id,
                Stage = RecommendationStage.Stage1,
                Status = RecommendationStatus.Submitted,
                Summary = "Alice submitted",
                LastUpdatedUtc = DateTime.UtcNow
            });
            context.CandidateRecommendations.Add(new CandidateRecommendation
            {
                ApplicationId = app2.Id,
                Stage = RecommendationStage.Stage1,
                Status = RecommendationStatus.Submitted,
                Summary = "Bob submitted",
                LastUpdatedUtc = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var service = new RecommendationService(context, new StageOrderService());
            var pending = await service.GetPendingRecommendationsAsync();

            pending.Should().HaveCount(2);
            pending.Should().Contain(p => p.CandidateName == "Alice");
            pending.Should().Contain(p => p.CandidateName == "Bob");
        }

        // 2. Approved and Draft recommendations do not appear in pending list
        [Fact]
        public async Task GetPendingRecommendations_ExcludesApprovedAndDraftRecs()
        {
            using var context = CreateInMemoryContext();
            var app1 = await SeedApplicationAsync(context, "Approved Candidate");
            var app2 = await SeedApplicationAsync(context, "Draft Candidate");

            context.CandidateRecommendations.Add(new CandidateRecommendation
            {
                ApplicationId = app1.Id,
                Stage = RecommendationStage.Stage1,
                Status = RecommendationStatus.Approved,
                Summary = "Approved rec",
                LastUpdatedUtc = DateTime.UtcNow
            });
            context.CandidateRecommendations.Add(new CandidateRecommendation
            {
                ApplicationId = app2.Id,
                Stage = RecommendationStage.Stage1,
                Status = RecommendationStatus.Draft,
                Summary = "Draft rec",
                LastUpdatedUtc = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var service = new RecommendationService(context, new StageOrderService());
            var pending = await service.GetPendingRecommendationsAsync();

            pending.Should().BeEmpty();
        }

        // --- ApproveStage1RecommendationAsync ---

        // 3. Approve action sets Approved status and stores approver userId
        [Fact]
        public async Task ApproveStage1Recommendation_SetsApprovedStatusAndReviewerId()
        {
            using var context = CreateInMemoryContext();
            var application = await SeedApplicationAsync(context);

            context.CandidateRecommendations.Add(new CandidateRecommendation
            {
                ApplicationId = application.Id,
                Stage = RecommendationStage.Stage1,
                Status = RecommendationStatus.Submitted,
                Summary = "Strong candidate",
                LastUpdatedUtc = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var service = new RecommendationService(context, new StageOrderService());
            var result = await service.ApproveStage1RecommendationAsync(application.Id, userId: 99);

            result.Should().Be(ApprovalResult.Approved);

            var rec = await context.CandidateRecommendations
                .FirstAsync(r => r.ApplicationId == application.Id);

            rec.Status.Should().Be(RecommendationStatus.Approved);
            rec.ReviewedByUserId.Should().Be(99);
            rec.ReviewedUtc.Should().NotBeNull();
        }

        // 4. Cannot approve when no recommendation record exists
        [Fact]
        public async Task ApproveStage1Recommendation_WhenNoRecExists_ReturnsNotFound()
        {
            using var context = CreateInMemoryContext();
            var application = await SeedApplicationAsync(context);
            // No recommendation seeded

            var service = new RecommendationService(context, new StageOrderService());
            var result = await service.ApproveStage1RecommendationAsync(application.Id, userId: 1);

            result.Should().Be(ApprovalResult.NotFound);

            var count = await context.CandidateRecommendations.CountAsync();
            count.Should().Be(0);
        }

        // 5. Cannot approve an already-approved recommendation
        [Fact]
        public async Task ApproveStage1Recommendation_WhenAlreadyApproved_ReturnsFalse()
        {
            using var context = CreateInMemoryContext();
            var application = await SeedApplicationAsync(context);

            context.CandidateRecommendations.Add(new CandidateRecommendation
            {
                ApplicationId = application.Id,
                Stage = RecommendationStage.Stage1,
                Status = RecommendationStatus.Approved,
                ReviewedByUserId = 5,
                ReviewedUtc = DateTime.UtcNow.AddDays(-1),
                LastUpdatedUtc = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var service = new RecommendationService(context, new StageOrderService());
            var result = await service.ApproveStage1RecommendationAsync(application.Id, userId: 10);

            result.Should().Be(ApprovalResult.AlreadyApproved);

            // Reviewer should not have changed
            var rec = await context.CandidateRecommendations.FirstAsync(r => r.ApplicationId == application.Id);
            rec.ReviewedByUserId.Should().Be(5);
        }

        // ---- A: Only Submitted in pending (Draft and Approved both excluded) ----

        [Fact]
        public async Task GetPendingRecommendations_OnlyIncludesSubmitted_ExcludesDraftAndApproved()
        {
            using var context = CreateInMemoryContext();
            var appDraft    = await SeedApplicationAsync(context, "Draft Candidate");
            var appSubmitted = await SeedApplicationAsync(context, "Submitted Candidate");
            var appApproved  = await SeedApplicationAsync(context, "Approved Candidate");

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

            var service = new RecommendationService(context, new StageOrderService());
            var pending = await service.GetPendingRecommendationsAsync();

            pending.Should().HaveCount(1);
            pending.Should().Contain(p => p.CandidateName == "Submitted Candidate");
            pending.Should().NotContain(p => p.CandidateName == "Draft Candidate");
            pending.Should().NotContain(p => p.CandidateName == "Approved Candidate");
        }

        // ---- Stage 2 in pending ----

        [Fact]
        public async Task GetPendingRecommendations_IncludesSubmittedStage2()
        {
            using var context = CreateInMemoryContext();
            var app = await SeedApplicationAsync(context, "Stage2 Candidate");

            context.CandidateRecommendations.Add(new CandidateRecommendation
            {
                ApplicationId = app.Id,
                Stage = RecommendationStage.Stage2,
                Status = RecommendationStatus.Submitted,
                Summary = "S2 summary",
                LastUpdatedUtc = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var service = new RecommendationService(context, new StageOrderService());
            var pending = await service.GetPendingRecommendationsAsync();

            pending.Should().HaveCount(1);
            pending[0].CandidateName.Should().Be("Stage2 Candidate");
            pending[0].Stage.Should().Be(RecommendationStage.Stage2);
        }

        [Fact]
        public async Task GetPendingRecommendations_IncludesBothStage1AndStage2_WhenBothSubmitted()
        {
            using var context = CreateInMemoryContext();
            var app1 = await SeedApplicationAsync(context, "S1 Candidate");
            var app2 = await SeedApplicationAsync(context, "S2 Candidate");

            context.CandidateRecommendations.Add(new CandidateRecommendation
            {
                ApplicationId = app1.Id,
                Stage = RecommendationStage.Stage1,
                Status = RecommendationStatus.Submitted,
                LastUpdatedUtc = DateTime.UtcNow
            });
            context.CandidateRecommendations.Add(new CandidateRecommendation
            {
                ApplicationId = app2.Id,
                Stage = RecommendationStage.Stage2,
                Status = RecommendationStatus.Submitted,
                LastUpdatedUtc = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var service = new RecommendationService(context, new StageOrderService());
            var pending = await service.GetPendingRecommendationsAsync();

            pending.Should().HaveCount(2);
            pending.Should().Contain(p => p.Stage == RecommendationStage.Stage1 && p.CandidateName == "S1 Candidate");
            pending.Should().Contain(p => p.Stage == RecommendationStage.Stage2 && p.CandidateName == "S2 Candidate");
        }

        [Fact]
        public async Task GetPendingRecommendations_Stage2Draft_IsExcluded()
        {
            using var context = CreateInMemoryContext();
            var app = await SeedApplicationAsync(context, "Draft S2 Candidate");

            context.CandidateRecommendations.Add(new CandidateRecommendation
            {
                ApplicationId = app.Id,
                Stage = RecommendationStage.Stage2,
                Status = RecommendationStatus.Draft,
                LastUpdatedUtc = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var service = new RecommendationService(context, new StageOrderService());
            var pending = await service.GetPendingRecommendationsAsync();

            pending.Should().BeEmpty();
        }

        // ---- C: ApprovalResult return type ----

        [Fact]
        public async Task ApproveStage1Recommendation_WhenDraft_ReturnsInvalidState()
        {
            using var context = CreateInMemoryContext();
            var application = await SeedApplicationAsync(context);

            context.CandidateRecommendations.Add(new CandidateRecommendation
            {
                ApplicationId = application.Id,
                Stage = RecommendationStage.Stage1,
                Status = RecommendationStatus.Draft,
                LastUpdatedUtc = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var service = new RecommendationService(context, new StageOrderService());
            var result = await service.ApproveStage1RecommendationAsync(application.Id, userId: 1);

            result.Should().Be(ApprovalResult.InvalidState);
        }

        [Fact]
        public async Task ApproveStage1Recommendation_WhenSubmitted_ReturnsApprovedResult()
        {
            using var context = CreateInMemoryContext();
            var application = await SeedApplicationAsync(context);

            context.CandidateRecommendations.Add(new CandidateRecommendation
            {
                ApplicationId = application.Id,
                Stage = RecommendationStage.Stage1,
                Status = RecommendationStatus.Submitted,
                LastUpdatedUtc = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var service = new RecommendationService(context, new StageOrderService());
            var result = await service.ApproveStage1RecommendationAsync(application.Id, userId: 7);

            result.Should().Be(ApprovalResult.Approved);

            var rec = await context.CandidateRecommendations
                .FirstAsync(r => r.ApplicationId == application.Id);
            rec.Status.Should().Be(RecommendationStatus.Approved);
            rec.ReviewedByUserId.Should().Be(7);
        }

        // ---- State machine: valid transitions ----

        // A.1: Draft → Submitted succeeds
        [Fact]
        public async Task SubmitStage1Recommendation_WhenDraft_ReturnsSuccess()
        {
            using var context = CreateInMemoryContext();
            var application = await SeedApplicationAsync(context);

            context.CandidateRecommendations.Add(new CandidateRecommendation
            {
                ApplicationId = application.Id,
                Stage = RecommendationStage.Stage1,
                Status = RecommendationStatus.Draft,
                LastUpdatedUtc = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var service = new RecommendationService(context, new StageOrderService());
            var result = await service.SubmitStage1RecommendationAsync(application.Id, userId: 1);

            result.Should().Be(TransitionResult.Success);

            var rec = await context.CandidateRecommendations
                .FirstAsync(r => r.ApplicationId == application.Id);
            rec.Status.Should().Be(RecommendationStatus.Submitted);
            rec.SubmittedByUserId.Should().Be(1);
            rec.SubmittedUtc.Should().NotBeNull();
        }

        // A.3: Submitted → Draft succeeds (editing flow via SaveStage1DraftAsync)
        [Fact]
        public async Task SaveStage1Draft_WhenSubmitted_TransitionsToDraftAndReturnsSuccess()
        {
            using var context = CreateInMemoryContext();
            var application = await SeedApplicationAsync(context);

            context.CandidateRecommendations.Add(new CandidateRecommendation
            {
                ApplicationId = application.Id,
                Stage = RecommendationStage.Stage1,
                Status = RecommendationStatus.Submitted,
                Summary = "Old summary",
                LastUpdatedUtc = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var service = new RecommendationService(context, new StageOrderService());
            var result = await service.SaveStage1DraftAsync(application.Id, "Updated summary", null, null, null);

            result.Should().Be(TransitionResult.Success);

            var rec = await context.CandidateRecommendations
                .FirstAsync(r => r.ApplicationId == application.Id);
            rec.Status.Should().Be(RecommendationStatus.Draft);
            rec.Summary.Should().Be("Updated summary");
        }

        // ---- State machine: invalid transitions ----

        // B.1: Approved → Submitted → InvalidState
        [Fact]
        public async Task SubmitStage1Recommendation_WhenApproved_ReturnsInvalidState()
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

            var service = new RecommendationService(context, new StageOrderService());
            var result = await service.SubmitStage1RecommendationAsync(application.Id, userId: 1);

            result.Should().Be(TransitionResult.InvalidState);

            // Status must not have changed
            var rec = await context.CandidateRecommendations
                .FirstAsync(r => r.ApplicationId == application.Id);
            rec.Status.Should().Be(RecommendationStatus.Approved);
        }

        // B.2: Saving over an Approved rec → content updated, status preserved
        [Fact]
        public async Task SaveStage1Draft_WhenApproved_AllowsContentUpdate_WithoutChangingStatus()
        {
            using var context = CreateInMemoryContext();
            var application = await SeedApplicationAsync(context);

            context.CandidateRecommendations.Add(new CandidateRecommendation
            {
                ApplicationId = application.Id,
                Stage = RecommendationStage.Stage1,
                Status = RecommendationStatus.Approved,
                Summary = "Original approved content",
                LastUpdatedUtc = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var service = new RecommendationService(context, new StageOrderService());
            var result = await service.SaveStage1DraftAsync(application.Id, "Updated post-approval notes", "New strengths", null, true);

            result.Should().Be(TransitionResult.Success);

            var rec = await context.CandidateRecommendations
                .FirstAsync(r => r.ApplicationId == application.Id);
            rec.Status.Should().Be(RecommendationStatus.Approved);
            rec.Summary.Should().Be("Updated post-approval notes");
            rec.ExperienceFit.Should().Be("New strengths");
            rec.HireRecommendation.Should().BeTrue();
        }

        // Submit when no recommendation exists → NotFound
        [Fact]
        public async Task SubmitStage1Recommendation_WhenNoRec_ReturnsNotFound()
        {
            using var context = CreateInMemoryContext();
            var application = await SeedApplicationAsync(context);

            var service = new RecommendationService(context, new StageOrderService());
            var result = await service.SubmitStage1RecommendationAsync(application.Id, userId: 1);

            result.Should().Be(TransitionResult.NotFound);
        }

        // PART 4 — Auth guard: unauthorized user cannot approve
        [Fact]
        public async Task ApproveStage1_WhenUnauthorized_ReturnsForbidden()
        {
            using var context = CreateInMemoryContext();
            var application = await SeedApplicationAsync(context);

            context.CandidateRecommendations.Add(new CandidateRecommendation
            {
                ApplicationId = application.Id,
                Stage = RecommendationStage.Stage1,
                Status = RecommendationStatus.Submitted,
                LastUpdatedUtc = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var service = new RecommendationService(context, new StageOrderService(), authService: new DenyAllAuthService());
            var result = await service.ApproveStage1RecommendationAsync(application.Id, userId: 1);

            result.Should().Be(ApprovalResult.Forbidden);

            // Status must not have changed
            var rec = await context.CandidateRecommendations.FirstAsync(r => r.ApplicationId == application.Id);
            rec.Status.Should().Be(RecommendationStatus.Submitted);
        }

        private sealed class DenyAllAuthService : IStageAuthorizationService
        {
            public bool CanApproveStage1(int userId) => false;
            public bool CanApproveStage2(int userId) => false;
        }
    }
}
