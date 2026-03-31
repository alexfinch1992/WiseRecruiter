using System;
using System.Threading.Tasks;
using FluentAssertions;
using JobPortal.Data;
using JobPortal.Models;
using JobPortal.Services.Implementations;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace WiseRecruiter.Tests.Unit.Services
{
    public class ApplicationStageServiceTests
    {
        private AppDbContext CreateInMemoryContext() =>
            new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase("stage_svc_" + Guid.NewGuid())
                .Options);

        private static async Task<Application> SeedApplicationAsync(AppDbContext context,
            ApplicationStage initial = ApplicationStage.Applied)
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
                Name = "Test User",
                Email = candidate.Email,
                City = "Sydney",
                Stage = initial,
                CurrentJobStageId = stage.Id
            };
            context.Applications.Add(application);
            await context.SaveChangesAsync();

            return application;
        }

        private static ApplicationStageService CreateService(AppDbContext context) =>
            new ApplicationStageService(context, new RecommendationService(context, new StageOrderService()));

        // Case 1: Approved → stage updates, no warning
        [Fact]
        public async Task UpdateStageAsync_ToInterview_WhenApproved_UpdatesStageAndNoWarning()
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

            var service = CreateService(context);

            var result = await service.UpdateStageAsync(
                application.Id, ApplicationStage.Interview,
                proceedWithoutApproval: false, userId: "1");

            result.RequiresApprovalWarning.Should().BeFalse();

            var updated = await context.Applications.FindAsync(application.Id);
            updated!.Stage.Should().Be(ApplicationStage.Interview);
        }

        // Case 2: Not approved + no bypass → stage IS updated (non-blocking), no warning
        [Fact]
        public async Task UpdateStageAsync_ToInterview_WhenNotApprovedAndNoBypass_UpdatesStageWithoutWarning()
        {
            using var context = CreateInMemoryContext();
            var application = await SeedApplicationAsync(context);
            // No recommendation at all

            var service = CreateService(context);

            var result = await service.UpdateStageAsync(
                application.Id, ApplicationStage.Interview,
                proceedWithoutApproval: false, userId: "");

            result.RequiresApprovalWarning.Should().BeFalse();

            var updated = await context.Applications.FindAsync(application.Id);
            updated!.Stage.Should().Be(ApplicationStage.Interview); // stage moves regardless
        }

        // New: movement succeeds without Stage 1 approval
        [Fact]
        public async Task UpdateStageAsync_MoveSucceeds_WhenStage1ApprovalMissing()
        {
            using var context = CreateInMemoryContext();
            var application = await SeedApplicationAsync(context);
            // No recommendation — Stage 1 approval is absent

            var service = CreateService(context);

            var result = await service.UpdateStageAsync(
                application.Id, ApplicationStage.Interview,
                proceedWithoutApproval: false, userId: "user1");

            // Stage must be updated — no hard gate
            var updated = await context.Applications.FindAsync(application.Id);
            updated!.Stage.Should().Be(ApplicationStage.Interview);
            result.RequiresApprovalWarning.Should().BeFalse();
        }

        // New: flag is set when approval missing and moving past Screen
        [Fact]
        public async Task UpdateStageAsync_SetsMovedWithoutStage1ApprovalFlag_WhenApprovalMissing()
        {
            using var context = CreateInMemoryContext();
            var application = await SeedApplicationAsync(context);
            // No Stage 1 recommendation

            var service = CreateService(context);

            await service.UpdateStageAsync(
                application.Id, ApplicationStage.Interview,
                proceedWithoutApproval: false, userId: "user1");

            var updated = await context.Applications.FindAsync(application.Id);
            updated!.MovedWithoutStage1Approval.Should().BeTrue();
        }

        // Case 3: Not approved + proceedWithoutApproval → stage updates
        [Fact]
        public async Task UpdateStageAsync_ToInterview_WithBypass_UpdatesStageAndNoWarning()
        {
            using var context = CreateInMemoryContext();
            var application = await SeedApplicationAsync(context);

            var service = CreateService(context);

            var result = await service.UpdateStageAsync(
                application.Id, ApplicationStage.Interview,
                proceedWithoutApproval: true, userId: "42");

            result.RequiresApprovalWarning.Should().BeFalse();

            var updated = await context.Applications.FindAsync(application.Id);
            updated!.Stage.Should().Be(ApplicationStage.Interview);
        }

        // Case 4: Already bypassed → existing bypass is not overwritten
        [Fact]
        public async Task UpdateStageAsync_WithBypass_WhenAlreadyBypassed_DoesNotOverwriteBypassFields()
        {
            using var context = CreateInMemoryContext();
            var application = await SeedApplicationAsync(context);
            var originalBypassTime = DateTime.UtcNow.AddHours(-3);

            context.CandidateRecommendations.Add(new CandidateRecommendation
            {
                ApplicationId = application.Id,
                Stage = RecommendationStage.Stage1,
                Status = RecommendationStatus.Draft,
                BypassedApproval = true,
                BypassedUtc = originalBypassTime,
                BypassedByUserId = 7,
                LastUpdatedUtc = DateTime.UtcNow.AddDays(-1)
            });
            await context.SaveChangesAsync();

            var service = CreateService(context);

            var result = await service.UpdateStageAsync(
                application.Id, ApplicationStage.Interview,
                proceedWithoutApproval: true, userId: "99");

            result.RequiresApprovalWarning.Should().BeFalse();

            var rec = await context.CandidateRecommendations
                .FirstAsync(r => r.ApplicationId == application.Id);

            // Original bypass fields preserved
            rec.BypassedByUserId.Should().Be(7);
            rec.BypassedUtc.Should().BeCloseTo(originalBypassTime, TimeSpan.FromSeconds(1));
        }

        // Non-Interview stages skip the approval check entirely
        [Fact]
        public async Task UpdateStageAsync_ToNonInterviewStage_SkipsApprovalCheckAndUpdatesStage()
        {
            using var context = CreateInMemoryContext();
            var application = await SeedApplicationAsync(context);
            // No recommendation — if check ran it would warn, but it should be skipped

            var service = CreateService(context);

            var result = await service.UpdateStageAsync(
                application.Id, ApplicationStage.Rejected,
                proceedWithoutApproval: false, userId: "");

            result.RequiresApprovalWarning.Should().BeFalse();

            var updated = await context.Applications.FindAsync(application.Id);
            updated!.Stage.Should().Be(ApplicationStage.Rejected);
        }
    }
}
