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
            var service = new RecommendationService(context, new StageOrderService());

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

            var service = new RecommendationService(context, new StageOrderService());

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

            var service = new RecommendationService(context, new StageOrderService());

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

            var service = new RecommendationService(context, new StageOrderService());

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
            var service = new RecommendationService(context, new StageOrderService());

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

            var service = new RecommendationService(context, new StageOrderService());
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

            var service = new RecommendationService(context, new StageOrderService());
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

            var service = new RecommendationService(context, new StageOrderService());
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

            var service = new RecommendationService(context, new StageOrderService());
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

        // ---- Auto-advance on approval ----

        // 1. Candidate at Screen → approval advances to Interview AND sets all metadata
        [Fact]
        public async Task Approve_WhenAtScreen_AdvancesToInterviewAndSetsMetadata()
        {
            using var context = CreateInMemoryContext();
            var application = await SeedApplicationAsync(context);
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
            var result = await service.ApproveStage1RecommendationAsync(application.Id, userId: 99, approvalFeedback: "Great candidate");

            result.Should().Be(ApprovalResult.Approved);

            var updatedApp = await context.Applications.FindAsync(application.Id);
            updatedApp!.Stage.Should().Be(ApplicationStage.Interview);

            var rec = await context.CandidateRecommendations.FirstAsync(r => r.ApplicationId == application.Id);
            rec.Status.Should().Be(RecommendationStatus.Approved);
            rec.ApprovedByUserId.Should().Be(99);
            rec.ApprovedUtc.Should().NotBeNull();
            rec.ApprovalFeedback.Should().Be("Great candidate");
        }

        // 2. Candidate already past Screen (Interview) → approval succeeds but stage does not change
        [Fact]
        public async Task Approve_WhenPastScreen_DoesNotChangeStage()
        {
            using var context = CreateInMemoryContext();
            var application = await SeedApplicationAsync(context);
            application.Stage = ApplicationStage.Interview;
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
            var result = await service.ApproveStage1RecommendationAsync(application.Id, userId: 99);

            result.Should().Be(ApprovalResult.Approved);

            var updatedApp = await context.Applications.FindAsync(application.Id);
            updatedApp!.Stage.Should().Be(ApplicationStage.Interview); // unchanged
        }

        // 3. Candidate at Hired (no meaningful next stage) → approval succeeds, stage unchanged
        [Fact]
        public async Task Approve_WhenAtHired_DoesNotChangeStage()
        {
            using var context = CreateInMemoryContext();
            var application = await SeedApplicationAsync(context);
            application.Stage = ApplicationStage.Hired;
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
            var result = await service.ApproveStage1RecommendationAsync(application.Id, userId: 99);

            result.Should().Be(ApprovalResult.Approved);

            var updatedApp = await context.Applications.FindAsync(application.Id);
            updatedApp!.Stage.Should().Be(ApplicationStage.Hired); // unchanged
        }

        // 4. Approval called twice → second returns AlreadyApproved, no further stage change
        [Fact]
        public async Task Approve_CalledTwice_SecondCallReturnsAlreadyApproved_NoStageChange()
        {
            using var context = CreateInMemoryContext();
            var application = await SeedApplicationAsync(context);
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
            var first  = await service.ApproveStage1RecommendationAsync(application.Id, userId: 99);
            var second = await service.ApproveStage1RecommendationAsync(application.Id, userId: 99);

            first.Should().Be(ApprovalResult.Approved);
            second.Should().Be(ApprovalResult.AlreadyApproved);

            // Stage advanced once on first call, not again on second
            var updatedApp = await context.Applications.FindAsync(application.Id);
            updatedApp!.Stage.Should().Be(ApplicationStage.Interview);
        }

        // ---- Stage 2 approval tests ----

        [Fact]
        public async Task ApproveStage2_WhenManager_Approves_Succeeds()
        {
            using var context = CreateInMemoryContext();
            var application = await SeedApplicationAsync(context);

            context.CandidateRecommendations.Add(new CandidateRecommendation
            {
                ApplicationId = application.Id,
                Stage = RecommendationStage.Stage2,
                Status = RecommendationStatus.Submitted,
                SubmittedByUserId = 1,
                SubmittedUtc = DateTime.UtcNow,
                LastUpdatedUtc = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            // CanApproveStage2 = true (default StageAuthorizationService)
            var service = new RecommendationService(context, new StageOrderService());
            var result = await service.ApproveStage2RecommendationAsync(application.Id, userId: 7, approvalFeedback: "Final approval");

            result.Should().Be(ApprovalResult.Approved);

            var rec = await context.CandidateRecommendations
                .FirstAsync(r => r.ApplicationId == application.Id && r.Stage == RecommendationStage.Stage2);
            rec.Status.Should().Be(RecommendationStatus.Approved);
            rec.ApprovedByUserId.Should().Be(7);
            rec.ApprovedUtc.Should().NotBeNull();
            rec.ApprovalFeedback.Should().Be("Final approval");
        }

        [Fact]
        public async Task ApproveStage2_WhenNotManager_ReturnsForbidden()
        {
            using var context = CreateInMemoryContext();
            var application = await SeedApplicationAsync(context);

            context.CandidateRecommendations.Add(new CandidateRecommendation
            {
                ApplicationId = application.Id,
                Stage = RecommendationStage.Stage2,
                Status = RecommendationStatus.Submitted,
                SubmittedByUserId = 1,
                SubmittedUtc = DateTime.UtcNow,
                LastUpdatedUtc = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var denyAuth = new DenyStage2AuthService();
            var service = new RecommendationService(context, new StageOrderService(), authService: denyAuth);
            var result = await service.ApproveStage2RecommendationAsync(application.Id, userId: 1);

            result.Should().Be(ApprovalResult.Forbidden);

            var rec = await context.CandidateRecommendations
                .FirstAsync(r => r.ApplicationId == application.Id && r.Stage == RecommendationStage.Stage2);
            rec.Status.Should().Be(RecommendationStatus.Submitted); // unchanged
        }

        [Fact]
        public async Task ApproveStage2_WhenDraft_ReturnsInvalidState()
        {
            using var context = CreateInMemoryContext();
            var application = await SeedApplicationAsync(context);

            context.CandidateRecommendations.Add(new CandidateRecommendation
            {
                ApplicationId = application.Id,
                Stage = RecommendationStage.Stage2,
                Status = RecommendationStatus.Draft,
                LastUpdatedUtc = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var service = new RecommendationService(context, new StageOrderService());
            var result = await service.ApproveStage2RecommendationAsync(application.Id, userId: 1);

            result.Should().Be(ApprovalResult.InvalidState);

            var rec = await context.CandidateRecommendations
                .FirstAsync(r => r.ApplicationId == application.Id && r.Stage == RecommendationStage.Stage2);
            rec.Status.Should().Be(RecommendationStatus.Draft); // unchanged
        }

        // ---- Stage 2 auto-creation on Stage 1 approval ----

        [Fact]
        public async Task ApproveStage1_CreatesStage2DraftRecommendation()
        {
            using var context = CreateInMemoryContext();
            var application = await SeedApplicationAsync(context);
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
            var result = await service.ApproveStage1RecommendationAsync(application.Id, userId: 99);

            result.Should().Be(ApprovalResult.Approved);

            var stage2Rec = await context.CandidateRecommendations
                .FirstOrDefaultAsync(r => r.ApplicationId == application.Id && r.Stage == RecommendationStage.Stage2);

            stage2Rec.Should().NotBeNull("Stage 2 recommendation should be auto-created after Stage 1 approval");
            stage2Rec!.Status.Should().Be(RecommendationStatus.Draft);
        }

        [Fact]
        public async Task ApproveStage1_WhenStage2AlreadyExists_DoesNotDuplicate()
        {
            using var context = CreateInMemoryContext();
            var application = await SeedApplicationAsync(context);
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
            context.CandidateRecommendations.Add(new CandidateRecommendation
            {
                ApplicationId = application.Id,
                Stage = RecommendationStage.Stage2,
                Status = RecommendationStatus.Draft,
                LastUpdatedUtc = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var service = new RecommendationService(context, new StageOrderService());
            await service.ApproveStage1RecommendationAsync(application.Id, userId: 99);

            var stage2Count = await context.CandidateRecommendations
                .CountAsync(r => r.ApplicationId == application.Id && r.Stage == RecommendationStage.Stage2);

            stage2Count.Should().Be(1, "Stage 2 should not be duplicated if one already exists");
        }

        // ---- Stage 2 save/submit ----

        [Fact]
        public async Task SaveStage2Draft_WhenNoRecExists_CreatesNewDraft()
        {
            using var context = CreateInMemoryContext();
            var application = await SeedApplicationAsync(context);
            var service = new RecommendationService(context, new StageOrderService());

            var result = await service.SaveStage2DraftAsync(application.Id, "S2 notes", "S2 strengths", "S2 concerns", true);

            result.Should().Be(TransitionResult.Success);

            var rec = await context.CandidateRecommendations
                .FirstOrDefaultAsync(r => r.ApplicationId == application.Id && r.Stage == RecommendationStage.Stage2);

            rec.Should().NotBeNull();
            rec!.Status.Should().Be(RecommendationStatus.Draft);
            rec.Summary.Should().Be("S2 notes");
            rec.ExperienceFit.Should().Be("S2 strengths");
        }

        [Fact]
        public async Task SaveStage2Draft_WhenDraftExists_UpdatesContent()
        {
            using var context = CreateInMemoryContext();
            var application = await SeedApplicationAsync(context);

            context.CandidateRecommendations.Add(new CandidateRecommendation
            {
                ApplicationId = application.Id,
                Stage = RecommendationStage.Stage2,
                Status = RecommendationStatus.Draft,
                Summary = "Old notes",
                LastUpdatedUtc = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var service = new RecommendationService(context, new StageOrderService());
            var result = await service.SaveStage2DraftAsync(application.Id, "New notes", null, null, null);

            result.Should().Be(TransitionResult.Success);

            var count = await context.CandidateRecommendations
                .CountAsync(r => r.ApplicationId == application.Id && r.Stage == RecommendationStage.Stage2);
            count.Should().Be(1, "no duplicate should be created");

            var rec = await context.CandidateRecommendations
                .FirstAsync(r => r.ApplicationId == application.Id && r.Stage == RecommendationStage.Stage2);
            rec.Summary.Should().Be("New notes");
        }

        [Fact]
        public async Task SubmitStage2_WhenDraftExists_TransitionsToSubmitted()
        {
            using var context = CreateInMemoryContext();
            var application = await SeedApplicationAsync(context);

            context.CandidateRecommendations.Add(new CandidateRecommendation
            {
                ApplicationId = application.Id,
                Stage = RecommendationStage.Stage2,
                Status = RecommendationStatus.Draft,
                LastUpdatedUtc = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var service = new RecommendationService(context, new StageOrderService());
            var result = await service.SubmitStage2RecommendationAsync(application.Id, userId: 5);

            result.Should().Be(TransitionResult.Success);

            var rec = await context.CandidateRecommendations
                .FirstAsync(r => r.ApplicationId == application.Id && r.Stage == RecommendationStage.Stage2);
            rec.Status.Should().Be(RecommendationStatus.Submitted);
            rec.SubmittedByUserId.Should().Be(5);
        }

        [Fact]
        public async Task SubmitStage2_WhenNoRec_ReturnsNotFound()
        {
            using var context = CreateInMemoryContext();
            var application = await SeedApplicationAsync(context);
            var service = new RecommendationService(context, new StageOrderService());

            var result = await service.SubmitStage2RecommendationAsync(application.Id, userId: 1);

            result.Should().Be(TransitionResult.NotFound);
        }

        private sealed class DenyStage2AuthService : IStageAuthorizationService
        {
            public bool CanApproveStage1(int userId) => true;
            public bool CanApproveStage2(int userId) => false;
        }
    }
}
