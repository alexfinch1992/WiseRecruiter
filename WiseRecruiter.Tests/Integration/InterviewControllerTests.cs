using System;
using System.Threading.Tasks;
using FluentAssertions;
using JobPortal.Data;
using JobPortal.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WiseRecruiter.Tests.Helpers;
using Xunit;

namespace WiseRecruiter.Tests.Integration
{
    public class InterviewControllerTests
    {
        private AppDbContext CreateInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: "interview_controller_db_" + Guid.NewGuid())
                .Options;
            return new AppDbContext(options);
        }

        private static InterviewController CreateController(AppDbContext context)
            => InterviewControllerFactory.Create(context);

        private static async Task<(Candidate candidate, Application application, JobStage stage)> SeedAsync(AppDbContext context)
        {
            var candidate = new Candidate
            {
                FirstName = "Test",
                LastName = "Candidate",
                Email = $"test_{Guid.NewGuid()}@example.com",
                CreatedAt = DateTime.UtcNow
            };
            context.Candidates.Add(candidate);

            var job = new Job { Title = "Engineer", Description = "Test" };
            context.Jobs.Add(job);
            await context.SaveChangesAsync();

            var stage = new JobStage { JobId = job.Id, Name = "Interview Round 1", Order = 2 };
            context.JobStages.Add(stage);
            await context.SaveChangesAsync();

            var application = new Application
            {
                Name = "Test Candidate",
                Email = candidate.Email,
                City = "Sydney",
                JobId = job.Id,
                CandidateId = candidate.Id,
                CurrentJobStageId = stage.Id
            };
            context.Applications.Add(application);
            await context.SaveChangesAsync();

            return (candidate, application, stage);
        }

        [Fact]
        public async Task CreateInterview_FromController_Works()
        {
            // Arrange
            using var context = CreateInMemoryContext();
            var (candidate, application, stage) = await SeedAsync(context);
            var controller = CreateController(context);
            var scheduledAt = new DateTime(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc);

            // Act
            var result = await controller.CreateInterview(candidate.Id, application.Id, $"stage:{stage.Id}", scheduledAt);

            // Assert - redirects back to CandidateDetails
            var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirect.ActionName.Should().Be("CandidateDetails");

            // Assert - interview exists in DB
            var interview = await context.Interviews.SingleOrDefaultAsync();
            interview.Should().NotBeNull();
            interview!.CandidateId.Should().Be(candidate.Id);
            interview.ApplicationId.Should().Be(application.Id);
            interview.JobStageId.Should().Be(stage.Id);
            interview.ScheduledAt.Should().Be(scheduledAt);
            interview.IsCancelled.Should().BeFalse();
        }

        [Fact]
        public async Task CreateInterview_WhenApplicationBelongsToOtherCandidate_ReturnsBadRequest()
        {
            // Arrange
            using var context = CreateInMemoryContext();
            var (_, application, stage) = await SeedAsync(context);

            // A different candidate
            var otherCandidate = new Candidate
            {
                FirstName = "Other",
                LastName = "Person",
                Email = "other@example.com",
                CreatedAt = DateTime.UtcNow
            };
            context.Candidates.Add(otherCandidate);
            await context.SaveChangesAsync();

            var controller = CreateController(context);

            // Act: use otherCandidate.Id but application belongs to first candidate
            var result = await controller.CreateInterview(otherCandidate.Id, application.Id, $"stage:{stage.Id}", DateTime.UtcNow.AddDays(1));

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
            (await context.Interviews.AnyAsync()).Should().BeFalse();
        }
    }
}
