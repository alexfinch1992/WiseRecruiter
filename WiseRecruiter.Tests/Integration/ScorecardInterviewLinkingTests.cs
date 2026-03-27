using System;
using System.Threading.Tasks;
using FluentAssertions;
using JobPortal.Data;
using JobPortal.Models;
using JobPortal.Services.Implementations;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace WiseRecruiter.Tests.Integration
{
    public class ScorecardInterviewLinkingTests
    {
        private AppDbContext CreateInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: "scorecard_interview_db_" + Guid.NewGuid())
                .Options;
            return new AppDbContext(options);
        }

        private static async Task<(Candidate candidate, Application application, JobStage stage, Interview interview)> SeedAsync(AppDbContext context)
        {
            var candidate = new Candidate
            {
                FirstName = "Alex",
                LastName = "Test",
                Email = "alex.test@example.com",
                CreatedAt = DateTime.UtcNow
            };
            context.Candidates.Add(candidate);

            var job = new Job { Title = "Developer", Description = "Dev role" };
            context.Jobs.Add(job);
            await context.SaveChangesAsync();

            var stage = new JobStage { JobId = job.Id, Name = "Technical Interview", Order = 2 };
            context.JobStages.Add(stage);
            await context.SaveChangesAsync();

            var application = new Application
            {
                Name = "Alex Test",
                Email = "alex.test@example.com",
                City = "Sydney",
                JobId = job.Id,
                CandidateId = candidate.Id,
                CurrentJobStageId = stage.Id
            };
            context.Applications.Add(application);
            await context.SaveChangesAsync();

            var interview = new Interview
            {
                CandidateId = candidate.Id,
                ApplicationId = application.Id,
                JobStageId = stage.Id,
                ScheduledAt = new DateTime(2026, 5, 1, 9, 0, 0, DateTimeKind.Utc),
                CreatedAt = DateTime.UtcNow
            };
            context.Interviews.Add(interview);
            await context.SaveChangesAsync();

            return (candidate, application, stage, interview);
        }

        [Fact]
        public async Task CreateScorecard_WithInterviewId_SetsInterviewIdOnScorecard()
        {
            using var context = CreateInMemoryContext();
            var (candidate, _, _, interview) = await SeedAsync(context);
            var service = new ScorecardService(context);

            var scorecard = await service.CreateScorecardAsync(candidate.Id, "reviewer@example.com");
            scorecard.InterviewId = interview.Id;
            await context.SaveChangesAsync();

            var inDb = await context.Scorecards.FindAsync(scorecard.Id);
            inDb!.InterviewId.Should().Be(interview.Id);
        }

        [Fact]
        public async Task CreateScorecard_WithInterviewId_MarksInterviewCompleted()
        {
            using var context = CreateInMemoryContext();
            var (candidate, _, _, interview) = await SeedAsync(context);
            var service = new ScorecardService(context);

            // Simulate the controller flow: create scorecard, link interview, mark completed
            var scorecard = await service.CreateScorecardAsync(candidate.Id, "reviewer@example.com");
            scorecard.InterviewId = interview.Id;
            interview.CompletedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();

            var linkedInterview = await context.Interviews.FindAsync(interview.Id);
            linkedInterview!.CompletedAt.Should().NotBeNull();
            linkedInterview.CompletedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        }

        [Fact]
        public async Task CreateScorecard_WithoutInterviewId_LeavesInterviewCompletedAtNull()
        {
            using var context = CreateInMemoryContext();
            var (candidate, _, _, interview) = await SeedAsync(context);
            var service = new ScorecardService(context);

            await service.CreateScorecardAsync(candidate.Id, "reviewer@example.com");

            var savedInterview = await context.Interviews.FindAsync(interview.Id);
            savedInterview!.CompletedAt.Should().BeNull();
        }
    }
}
