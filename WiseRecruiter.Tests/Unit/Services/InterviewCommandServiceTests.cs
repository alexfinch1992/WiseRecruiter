using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using JobPortal.Data;
using JobPortal.Models;
using JobPortal.Services.Implementations;
using JobPortal.Services.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace WiseRecruiter.Tests.Unit.Services
{
    public class InterviewCommandServiceTests
    {
        private AppDbContext CreateInMemoryContext() =>
            new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase("interview_cmd_" + Guid.NewGuid())
                .Options);

        /// <summary>Seeds the minimum graph needed: Candidate + Job + JobStage + Application.</summary>
        private static async Task<(int candidateId, int applicationId)> SeedApplicationAsync(AppDbContext ctx)
        {
            var candidate = new Candidate
            {
                FirstName = "Jane",
                LastName  = "Doe",
                Email     = $"jane_{Guid.NewGuid()}@example.com",
                CreatedAt = DateTime.UtcNow
            };
            ctx.Candidates.Add(candidate);
            await ctx.SaveChangesAsync();

            var job = new Job { Title = "Engineer" };
            ctx.Jobs.Add(job);
            await ctx.SaveChangesAsync();

            var stage = new JobStage { JobId = job.Id, Name = "Interview", Order = 1 };
            ctx.JobStages.Add(stage);
            await ctx.SaveChangesAsync();

            var application = new Application
            {
                CandidateId       = candidate.Id,
                JobId             = job.Id,
                Name              = "Jane Doe",
                Email             = candidate.Email,
                City              = "Sydney",
                Stage             = ApplicationStage.Applied,
                CurrentJobStageId = stage.Id
            };
            ctx.Applications.Add(application);
            await ctx.SaveChangesAsync();

            return (candidate.Id, application.Id);
        }

        private static InterviewCommandService CreateService(AppDbContext ctx) =>
            new InterviewCommandService(
                ctx,
                new InterviewService(ctx),
                new RecommendationService(ctx, new StageOrderService()));

        // ── Failure cases ────────────────────────────────────────────────────

        [Fact]
        public async Task CreateAsync_ReturnsInvalidApplication_WhenApplicationNotFound()
        {
            using var ctx = CreateInMemoryContext();
            var svc = CreateService(ctx);

            var result = await svc.CreateAsync(
                candidateId: 999, applicationId: 999,
                selectedStage: "enum:Applied",
                scheduledAt: DateTime.UtcNow.AddDays(1),
                selectedInterviewerIds: null,
                proceedWithoutApproval: false, bypassReason: null,
                userId: "admin1");

            result.Success.Should().BeFalse();
            result.Error.Should().Be(InterviewCreateError.InvalidApplication);
        }

        [Fact]
        public async Task CreateAsync_ReturnsInvalidApplication_WhenCandidateIdMismatch()
        {
            using var ctx = CreateInMemoryContext();
            var (_, applicationId) = await SeedApplicationAsync(ctx);
            var svc = CreateService(ctx);

            var result = await svc.CreateAsync(
                candidateId: 9999, applicationId: applicationId,
                selectedStage: "enum:Applied",
                scheduledAt: DateTime.UtcNow.AddDays(1),
                selectedInterviewerIds: null,
                proceedWithoutApproval: false, bypassReason: null,
                userId: "admin1");

            result.Success.Should().BeFalse();
            result.Error.Should().Be(InterviewCreateError.InvalidApplication);
        }

        [Fact]
        public async Task CreateAsync_ReturnsInvalidStageFormat_WhenStageStringHasNoPrefix()
        {
            using var ctx = CreateInMemoryContext();
            var (candidateId, applicationId) = await SeedApplicationAsync(ctx);
            var svc = CreateService(ctx);

            var result = await svc.CreateAsync(
                candidateId, applicationId,
                selectedStage: "Interview",   // no stage:/enum: prefix
                scheduledAt: DateTime.UtcNow.AddDays(1),
                selectedInterviewerIds: null,
                proceedWithoutApproval: false, bypassReason: null,
                userId: "admin1");

            result.Success.Should().BeFalse();
            result.Error.Should().Be(InterviewCreateError.InvalidStageFormat);
        }

        [Fact]
        public async Task CreateAsync_ReturnsInvalidStageFormat_WhenStageIdNotParseable()
        {
            using var ctx = CreateInMemoryContext();
            var (candidateId, applicationId) = await SeedApplicationAsync(ctx);
            var svc = CreateService(ctx);

            var result = await svc.CreateAsync(
                candidateId, applicationId,
                selectedStage: "stage:abc",   // non-numeric after "stage:"
                scheduledAt: DateTime.UtcNow.AddDays(1),
                selectedInterviewerIds: null,
                proceedWithoutApproval: false, bypassReason: null,
                userId: "admin1");

            result.Success.Should().BeFalse();
            result.Error.Should().Be(InterviewCreateError.InvalidStageFormat);
        }

        [Fact]
        public async Task CreateAsync_ReturnsInvalidInterviewer_WhenInterviewerIdDoesNotExist()
        {
            using var ctx = CreateInMemoryContext();
            var (candidateId, applicationId) = await SeedApplicationAsync(ctx);
            var svc = CreateService(ctx);

            var result = await svc.CreateAsync(
                candidateId, applicationId,
                selectedStage: "enum:Applied",
                scheduledAt: DateTime.UtcNow.AddDays(1),
                selectedInterviewerIds: new List<int> { 9999 },   // non-existent admin user
                proceedWithoutApproval: false, bypassReason: null,
                userId: "admin1");

            result.Success.Should().BeFalse();
            result.Error.Should().Be(InterviewCreateError.InvalidInterviewer);
        }

        // ── Happy path ───────────────────────────────────────────────────────

        [Fact]
        public async Task CreateAsync_ReturnsSuccess_WithEnumStage()
        {
            using var ctx = CreateInMemoryContext();
            var (candidateId, applicationId) = await SeedApplicationAsync(ctx);
            var svc = CreateService(ctx);

            var result = await svc.CreateAsync(
                candidateId, applicationId,
                selectedStage: "enum:Applied",
                scheduledAt: DateTime.UtcNow.AddDays(1),
                selectedInterviewerIds: null,
                proceedWithoutApproval: false, bypassReason: null,
                userId: "admin1");

            result.Success.Should().BeTrue();
            result.Error.Should().Be(InterviewCreateError.None);
            result.ApplicationId.Should().Be(applicationId);
        }

        [Fact]
        public async Task CreateAsync_ReturnsSuccess_WithValidStageId()
        {
            using var ctx = CreateInMemoryContext();
            var (candidateId, applicationId) = await SeedApplicationAsync(ctx);
            // The seeding creates a JobStage with Id = 1 (first in fresh in-memory DB per GUID)
            var stageId = (await ctx.JobStages.FirstAsync()).Id;
            var svc = CreateService(ctx);

            var result = await svc.CreateAsync(
                candidateId, applicationId,
                selectedStage: $"stage:{stageId}",
                scheduledAt: DateTime.UtcNow.AddDays(1),
                selectedInterviewerIds: null,
                proceedWithoutApproval: false, bypassReason: null,
                userId: "admin1");

            result.Success.Should().BeTrue();
            result.ApplicationId.Should().Be(applicationId);
        }

        [Fact]
        public async Task CreateAsync_ReturnsSuccess_WithValidInterviewer()
        {
            using var ctx = CreateInMemoryContext();
            var (candidateId, applicationId) = await SeedApplicationAsync(ctx);

            var admin = new AdminUser { Username = "interviewer", PasswordHash = "x" };
            ctx.AdminUsers.Add(admin);
            await ctx.SaveChangesAsync();

            var svc = CreateService(ctx);

            var result = await svc.CreateAsync(
                candidateId, applicationId,
                selectedStage: "enum:Applied",
                scheduledAt: DateTime.UtcNow.AddDays(1),
                selectedInterviewerIds: new List<int> { admin.Id },
                proceedWithoutApproval: false, bypassReason: null,
                userId: "admin1");

            result.Success.Should().BeTrue();
            var interviewers = await ctx.InterviewInterviewers.ToListAsync();
            interviewers.Should().HaveCount(1).And.Contain(ii => ii.AdminUserId == admin.Id);
        }

        // ── UTC conversion ───────────────────────────────────────────────────

        [Fact]
        public async Task CreateAsync_PreservesUtcDateTime_WhenAlreadyUtc()
        {
            using var ctx = CreateInMemoryContext();
            var (candidateId, applicationId) = await SeedApplicationAsync(ctx);
            var svc = CreateService(ctx);

            var utcTime = new DateTime(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc);

            await svc.CreateAsync(
                candidateId, applicationId,
                selectedStage: "enum:Applied",
                scheduledAt: utcTime,
                selectedInterviewerIds: null,
                proceedWithoutApproval: false, bypassReason: null,
                userId: "admin1");

            var interview = await ctx.Interviews.FirstAsync();
            interview.ScheduledAt.Kind.Should().Be(DateTimeKind.Utc);
            interview.ScheduledAt.Should().Be(utcTime);
        }

        [Fact]
        public async Task CreateAsync_ConvertsUnspecifiedDateTimeToUtc()
        {
            using var ctx = CreateInMemoryContext();
            var (candidateId, applicationId) = await SeedApplicationAsync(ctx);
            var svc = CreateService(ctx);

            var unspecified = new DateTime(2026, 6, 15, 10, 0, 0, DateTimeKind.Unspecified);

            await svc.CreateAsync(
                candidateId, applicationId,
                selectedStage: "enum:Applied",
                scheduledAt: unspecified,
                selectedInterviewerIds: null,
                proceedWithoutApproval: false, bypassReason: null,
                userId: "admin1");

            var interview = await ctx.Interviews.FirstAsync();
            // InterviewService normalises to UTC, so Kind must be Utc
            interview.ScheduledAt.Kind.Should().Be(DateTimeKind.Utc);
        }
    }
}
