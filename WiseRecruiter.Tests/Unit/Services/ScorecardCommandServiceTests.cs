using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using JobPortal.Data;
using JobPortal.Models;
using JobPortal.Models.ViewModels;
using JobPortal.Services.Implementations;
using JobPortal.Services.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace WiseRecruiter.Tests.Unit.Services
{
    public class ScorecardCommandServiceTests
    {
        private AppDbContext CreateInMemoryContext() =>
            new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase("scorecard_cmd_" + Guid.NewGuid())
                .Options);

        private static ScorecardCommandService CreateService(AppDbContext ctx)
        {
            var templateSvc  = new ScorecardTemplateService(ctx);
            var scorecardSvc = new ScorecardService(ctx, templateSvc);
            return new ScorecardCommandService(ctx, scorecardSvc);
        }

        /// <summary>Seeds candidate + job + application and returns ids.</summary>
        private static async Task<(int candidateId, int applicationId)> SeedAsync(AppDbContext ctx)
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

            var stage = new JobStage { JobId = job.Id, Name = "Applied", Order = 0 };
            ctx.JobStages.Add(stage);
            await ctx.SaveChangesAsync();

            var application = new Application
            {
                CandidateId       = candidate.Id,
                JobId             = job.Id,
                Name              = "Jane Doe",
                Email             = candidate.Email,
                City              = "Sydney",
                CurrentJobStageId = stage.Id
            };
            ctx.Applications.Add(application);
            await ctx.SaveChangesAsync();

            return (candidate.Id, application.Id);
        }

        private static CreateScorecardViewModel BuildModel(int candidateId, int applicationId,
            int? interviewId = null, string? overallRecommendation = null)
        {
            return new CreateScorecardViewModel
            {
                ApplicationId          = applicationId,
                CandidateId            = candidateId,
                CandidateName          = "Jane Doe",
                JobTitle               = "Engineer",
                InterviewId            = interviewId,
                OverallRecommendation  = overallRecommendation,
                Responses = new List<ScorecardResponseInputViewModel>
                {
                    new() { FacetId = 1, FacetName = "Communication", Score = 4.0m, Notes = "Good" },
                    new() { FacetId = 2, FacetName = "Technical",     Score = 3.5m, Notes = "Solid" }
                }
            };
        }

        // ── Happy path ───────────────────────────────────────────────────────

        [Fact]
        public async Task CreateScorecardAsync_Should_CreateScorecard_WithResponses()
        {
            using var ctx = CreateInMemoryContext();
            var (candidateId, applicationId) = await SeedAsync(ctx);
            var svc = CreateService(ctx);
            var model = BuildModel(candidateId, applicationId);

            var result = await svc.CreateScorecardAsync(model, "admin");

            result.Result.Should().Be(CreateScorecardResult.Success);
            result.ScorecardId.Should().NotBeNull();

            var scorecard = await ctx.Scorecards.FindAsync(result.ScorecardId!.Value);
            scorecard.Should().NotBeNull();
            scorecard!.CandidateId.Should().Be(candidateId);
            scorecard.SubmittedBy.Should().Be("admin");

            var responses = await ctx.ScorecardResponses
                .Where(r => r.ScorecardId == scorecard.Id)
                .ToListAsync();
            responses.Should().HaveCount(2);
            responses.Should().Contain(r => r.FacetName == "Communication" && r.Score == 4.0m);
            responses.Should().Contain(r => r.FacetName == "Technical" && r.Score == 3.5m);
        }

        [Fact]
        public async Task CreateScorecardAsync_Should_LinkInterview_WhenInterviewIdProvided()
        {
            using var ctx = CreateInMemoryContext();
            var (candidateId, applicationId) = await SeedAsync(ctx);

            var interview = new Interview
            {
                CandidateId   = candidateId,
                ApplicationId = applicationId,
                ScheduledAt   = DateTime.UtcNow.AddDays(1)
            };
            ctx.Interviews.Add(interview);
            await ctx.SaveChangesAsync();

            var svc = CreateService(ctx);
            var model = BuildModel(candidateId, applicationId, interviewId: interview.Id);

            var result = await svc.CreateScorecardAsync(model, "admin");

            result.Result.Should().Be(CreateScorecardResult.Success);
            var scorecard = await ctx.Scorecards.FindAsync(result.ScorecardId!.Value);
            scorecard!.InterviewId.Should().Be(interview.Id);
        }

        [Fact]
        public async Task CreateScorecardAsync_Should_MarkInterviewCompleted_WhenNotAlreadyCompleted()
        {
            using var ctx = CreateInMemoryContext();
            var (candidateId, applicationId) = await SeedAsync(ctx);

            var interview = new Interview
            {
                CandidateId   = candidateId,
                ApplicationId = applicationId,
                ScheduledAt   = DateTime.UtcNow.AddDays(1),
                CompletedAt   = null
            };
            ctx.Interviews.Add(interview);
            await ctx.SaveChangesAsync();

            var svc = CreateService(ctx);
            var model = BuildModel(candidateId, applicationId, interviewId: interview.Id);

            await svc.CreateScorecardAsync(model, "admin");

            var updated = await ctx.Interviews.FindAsync(interview.Id);
            updated!.CompletedAt.Should().NotBeNull();
        }

        [Fact]
        public async Task CreateScorecardAsync_Should_NotOverwriteCompletedAt_WhenAlreadySet()
        {
            using var ctx = CreateInMemoryContext();
            var (candidateId, applicationId) = await SeedAsync(ctx);

            var existingCompletedAt = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);
            var interview = new Interview
            {
                CandidateId   = candidateId,
                ApplicationId = applicationId,
                ScheduledAt   = DateTime.UtcNow.AddDays(-1),
                CompletedAt   = existingCompletedAt
            };
            ctx.Interviews.Add(interview);
            await ctx.SaveChangesAsync();

            var svc = CreateService(ctx);
            var model = BuildModel(candidateId, applicationId, interviewId: interview.Id);

            await svc.CreateScorecardAsync(model, "admin");

            var updated = await ctx.Interviews.FindAsync(interview.Id);
            updated!.CompletedAt.Should().Be(existingCompletedAt);
        }

        [Fact]
        public async Task CreateScorecardAsync_Should_SetOverallRecommendation_WhenProvided()
        {
            using var ctx = CreateInMemoryContext();
            var (candidateId, applicationId) = await SeedAsync(ctx);
            var svc = CreateService(ctx);
            var model = BuildModel(candidateId, applicationId, overallRecommendation: "Strong Hire");

            var result = await svc.CreateScorecardAsync(model, "admin");

            var scorecard = await ctx.Scorecards.FindAsync(result.ScorecardId!.Value);
            scorecard!.OverallRecommendation.Should().Be("Strong Hire");
        }

        // ── Failure paths ────────────────────────────────────────────────────

        [Fact]
        public async Task CreateScorecardAsync_Should_ReturnInterviewNotFound_WhenInterviewMissing()
        {
            using var ctx = CreateInMemoryContext();
            var (candidateId, applicationId) = await SeedAsync(ctx);
            var svc = CreateService(ctx);
            var model = BuildModel(candidateId, applicationId, interviewId: 9999);

            var result = await svc.CreateScorecardAsync(model, "admin");

            result.Result.Should().Be(CreateScorecardResult.InterviewNotFound);
            result.ScorecardId.Should().BeNull();
        }

        [Fact]
        public async Task CreateScorecardAsync_Should_ReturnInvalidCandidate_WhenInterviewCandidateMismatch()
        {
            using var ctx = CreateInMemoryContext();
            var (candidateId, applicationId) = await SeedAsync(ctx);

            // Seed a second candidate whose interview we'll try to link
            var otherCandidate = new Candidate
            {
                FirstName = "Other",
                LastName  = "Person",
                Email     = $"other_{Guid.NewGuid()}@example.com",
                CreatedAt = DateTime.UtcNow
            };
            ctx.Candidates.Add(otherCandidate);
            await ctx.SaveChangesAsync();

            var interview = new Interview
            {
                CandidateId   = otherCandidate.Id,
                ApplicationId = applicationId,
                ScheduledAt   = DateTime.UtcNow.AddDays(1)
            };
            ctx.Interviews.Add(interview);
            await ctx.SaveChangesAsync();

            var svc = CreateService(ctx);
            var model = BuildModel(candidateId, applicationId, interviewId: interview.Id);

            var result = await svc.CreateScorecardAsync(model, "admin");

            result.Result.Should().Be(CreateScorecardResult.InvalidCandidateForInterview);
            result.ScorecardId.Should().BeNull();
        }
    }
}
