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
    public class RecommendationServiceTests
    {
        private AppDbContext CreateInMemoryContext() =>
            new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase("rec_service_" + Guid.NewGuid())
                .Options);

        private static async Task<Application> SeedApplicationAsync(AppDbContext context)
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
                CurrentJobStageId = stage.Id
            };
            context.Applications.Add(application);
            await context.SaveChangesAsync();

            return application;
        }

        // Case 1: No recommendation exists → creates one when bypassing
        [Fact]
        public async Task GetOrPrepare_NoExistingRec_BypassTrue_CreatesRecommendation()
        {
            using var context = CreateInMemoryContext();
            var application = await SeedApplicationAsync(context);
            var service = new RecommendationService(context);

            var (rec, isApproved) = await service.GetOrPrepareStage1RecommendationAsync(
                application.Id, proceedWithoutApproval: true, bypassReason: "Urgent hire", userId: "42");

            rec.Should().NotBeNull();
            rec!.Stage.Should().Be(RecommendationStage.Stage1);
            rec.Status.Should().Be(RecommendationStatus.Draft);
            rec.BypassedApproval.Should().BeTrue();
            rec.BypassedByUserId.Should().Be(42);
            rec.BypassReason.Should().Be("Urgent hire");
            isApproved.Should().BeFalse();

            var saved = await context.CandidateRecommendations
                .FirstOrDefaultAsync(r => r.ApplicationId == application.Id);
            saved.Should().NotBeNull();
        }

        // Case 2: Existing Draft recommendation → does not create a duplicate
        [Fact]
        public async Task GetOrPrepare_ExistingRec_BypassTrue_DoesNotDuplicate()
        {
            using var context = CreateInMemoryContext();
            var application = await SeedApplicationAsync(context);

            context.CandidateRecommendations.Add(new CandidateRecommendation
            {
                ApplicationId = application.Id,
                Stage = RecommendationStage.Stage1,
                Status = RecommendationStatus.Draft,
                LastUpdatedUtc = DateTime.UtcNow.AddDays(-1)
            });
            await context.SaveChangesAsync();

            var service = new RecommendationService(context);

            await service.GetOrPrepareStage1RecommendationAsync(
                application.Id, proceedWithoutApproval: true, bypassReason: null, userId: "");

            var count = await context.CandidateRecommendations
                .CountAsync(r => r.ApplicationId == application.Id);
            count.Should().Be(1);
        }

        // Case 3: Already bypassed → does not overwrite existing bypass fields
        [Fact]
        public async Task GetOrPrepare_AlreadyBypassed_DoesNotOverwriteBypassFields()
        {
            using var context = CreateInMemoryContext();
            var application = await SeedApplicationAsync(context);
            var originalBypassTime = DateTime.UtcNow.AddHours(-2);

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

            var service = new RecommendationService(context);

            await service.GetOrPrepareStage1RecommendationAsync(
                application.Id, proceedWithoutApproval: true, bypassReason: "New reason", userId: "99");

            var rec = await context.CandidateRecommendations
                .FirstAsync(r => r.ApplicationId == application.Id);

            // Existing bypass fields are not overwritten
            rec.BypassedByUserId.Should().Be(7);
            rec.BypassedUtc.Should().BeCloseTo(originalBypassTime, TimeSpan.FromSeconds(1));
        }

        // Case 4: Approved recommendation → bypass not applied even if proceedWithoutApproval=true
        [Fact]
        public async Task GetOrPrepare_ApprovedRec_BypassTrue_ReturnsApprovedAndDoesNotMutate()
        {
            using var context = CreateInMemoryContext();
            var application = await SeedApplicationAsync(context);

            context.CandidateRecommendations.Add(new CandidateRecommendation
            {
                ApplicationId = application.Id,
                Stage = RecommendationStage.Stage1,
                Status = RecommendationStatus.Approved,
                LastUpdatedUtc = DateTime.UtcNow.AddDays(-1)
            });
            await context.SaveChangesAsync();

            var service = new RecommendationService(context);

            var (rec, isApproved) = await service.GetOrPrepareStage1RecommendationAsync(
                application.Id, proceedWithoutApproval: true, bypassReason: "Ignored", userId: "1");

            isApproved.Should().BeTrue();
            rec!.BypassedApproval.Should().BeFalse(); // not modified
            rec.Status.Should().Be(RecommendationStatus.Approved);
        }

        // No bypass requested and no recommendation → returns (null, false) without persisting
        [Fact]
        public async Task GetOrPrepare_NoBypas_NoRec_ReturnsNullAndFalse()
        {
            using var context = CreateInMemoryContext();
            var application = await SeedApplicationAsync(context);
            var service = new RecommendationService(context);

            var (rec, isApproved) = await service.GetOrPrepareStage1RecommendationAsync(
                application.Id, proceedWithoutApproval: false, bypassReason: null, userId: "");

            rec.Should().BeNull();
            isApproved.Should().BeFalse();

            var count = await context.CandidateRecommendations.CountAsync();
            count.Should().Be(0); // nothing persisted
        }

        // ---- Part 1: bypass flag behaviour in SaveStage1DraftAsync ----

        // 1. Saving over an Approved rec → bypass fields preserved, content updated, status stays Approved
        [Fact]
        public async Task SaveStage1Draft_WhenApprovedRecExists_UpdatesContentAndPreservesStatus()
        {
            using var context = CreateInMemoryContext();
            var application = await SeedApplicationAsync(context);

            context.CandidateRecommendations.Add(new CandidateRecommendation
            {
                ApplicationId = application.Id,
                Stage = RecommendationStage.Stage1,
                Status = RecommendationStatus.Approved,
                BypassedApproval = true,
                BypassReason = "Urgent hire",
                BypassedByUserId = 42,
                Summary = "Original",
                LastUpdatedUtc = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var service = new RecommendationService(context);
            var result = await service.SaveStage1DraftAsync(application.Id, "Updated notes", null, null, null);

            result.Should().Be(JobPortal.Services.Models.TransitionResult.Success);

            var rec = await context.CandidateRecommendations
                .FirstAsync(r => r.ApplicationId == application.Id);
            rec.Status.Should().Be(RecommendationStatus.Approved);
            rec.Summary.Should().Be("Updated notes");
            // Bypass metadata untouched
            rec.BypassedApproval.Should().BeTrue();
            rec.BypassReason.Should().Be("Urgent hire");
        }

        // 2. Saving over a Draft rec → bypass fields are preserved
        [Fact]
        public async Task SaveStage1Draft_WhenDraftRecExists_PreservesBypassFields()
        {
            using var context = CreateInMemoryContext();
            var application = await SeedApplicationAsync(context);

            context.CandidateRecommendations.Add(new CandidateRecommendation
            {
                ApplicationId = application.Id,
                Stage = RecommendationStage.Stage1,
                Status = RecommendationStatus.Draft,
                BypassedApproval = true,
                BypassReason = "Initial bypass",
                BypassedByUserId = 7,
                LastUpdatedUtc = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var service = new RecommendationService(context);
            await service.SaveStage1DraftAsync(application.Id, "Updated notes", null, null, null);

            var rec = await context.CandidateRecommendations
                .FirstAsync(r => r.ApplicationId == application.Id);

            rec.BypassedApproval.Should().BeTrue();
            rec.BypassReason.Should().Be("Initial bypass");
            rec.BypassedByUserId.Should().Be(7);
        }

        // 3. Approved rec with no bypass already set → content updated, bypass stays false
        [Fact]
        public async Task SaveStage1Draft_WhenApprovedRecWithNoBypass_UpdatesContentAndBypassRemainsCleared()
        {
            using var context = CreateInMemoryContext();
            var application = await SeedApplicationAsync(context);

            context.CandidateRecommendations.Add(new CandidateRecommendation
            {
                ApplicationId = application.Id,
                Stage = RecommendationStage.Stage1,
                Status = RecommendationStatus.Approved,
                BypassedApproval = false,
                LastUpdatedUtc = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var service = new RecommendationService(context);
            var result = await service.SaveStage1DraftAsync(application.Id, "Notes", "Strengths", null, true);

            result.Should().Be(JobPortal.Services.Models.TransitionResult.Success);

            var rec = await context.CandidateRecommendations
                .FirstAsync(r => r.ApplicationId == application.Id);

            rec.Status.Should().Be(RecommendationStatus.Approved);
            rec.Summary.Should().Be("Notes");
            rec.BypassedApproval.Should().BeFalse();
            rec.BypassReason.Should().BeNull();
        }

        // 4. Saving twice on Approved rec → both succeed, last write wins
        [Fact]
        public async Task SaveStage1Draft_CalledTwiceOnApprovedRec_BothSucceed()
        {
            using var context = CreateInMemoryContext();
            var application = await SeedApplicationAsync(context);

            context.CandidateRecommendations.Add(new CandidateRecommendation
            {
                ApplicationId = application.Id,
                Stage = RecommendationStage.Stage1,
                Status = RecommendationStatus.Approved,
                BypassedApproval = true,
                BypassReason = "Original bypass",
                LastUpdatedUtc = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var service = new RecommendationService(context);
            var r1 = await service.SaveStage1DraftAsync(application.Id, "First save", null, null, null);
            var r2 = await service.SaveStage1DraftAsync(application.Id, "Second save", null, null, null);

            r1.Should().Be(JobPortal.Services.Models.TransitionResult.Success);
            r2.Should().Be(JobPortal.Services.Models.TransitionResult.Success);

            var rec = await context.CandidateRecommendations
                .FirstAsync(r => r.ApplicationId == application.Id);
            rec.Status.Should().Be(RecommendationStatus.Approved);
            rec.Summary.Should().Be("Second save");
            // Bypass metadata preserved
            rec.BypassedApproval.Should().BeTrue();
            rec.BypassReason.Should().Be("Original bypass");
        }
    }
}
