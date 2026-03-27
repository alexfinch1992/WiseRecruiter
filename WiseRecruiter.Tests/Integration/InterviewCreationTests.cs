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
    public class InterviewCreationTests
    {
        private AppDbContext CreateInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: "interview_creation_db_" + Guid.NewGuid())
                .Options;
            return new AppDbContext(options);
        }

        private static async Task<(Candidate candidate, Application application, JobStage stage)> SeedPrerequisitesAsync(AppDbContext context)
        {
            var candidate = new Candidate
            {
                FirstName = "Jane",
                LastName = "Doe",
                Email = "jane.doe@example.com",
                CreatedAt = DateTime.UtcNow
            };
            context.Candidates.Add(candidate);

            var job = new Job { Title = "Engineer", Description = "Test job" };
            context.Jobs.Add(job);
            await context.SaveChangesAsync();

            var stage = new JobStage { JobId = job.Id, Name = "Interview", Order = 2 };
            context.JobStages.Add(stage);
            await context.SaveChangesAsync();

            var application = new Application
            {
                Name = "Jane Doe",
                Email = "jane.doe@example.com",
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
        public async Task CreateInterviewAsync_PersistsInterviewToDatabase()
        {
            using var context = CreateInMemoryContext();
            var (candidate, application, stage) = await SeedPrerequisitesAsync(context);
            var service = new InterviewService(context);
            var scheduledAt = new DateTime(2026, 4, 15, 10, 0, 0, DateTimeKind.Utc);

            var result = await service.CreateInterviewAsync(candidate.Id, application.Id, stage.Id, scheduledAt);

            result.Id.Should().BeGreaterThan(0);
            result.CandidateId.Should().Be(candidate.Id);
            result.ApplicationId.Should().Be(application.Id);
            result.JobStageId.Should().Be(stage.Id);
            result.ScheduledAt.Should().Be(scheduledAt);
            result.IsCancelled.Should().BeFalse();
            result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

            var inDb = await context.Interviews.FindAsync(result.Id);
            inDb.Should().NotBeNull();
            inDb!.CandidateId.Should().Be(candidate.Id);
            inDb.ApplicationId.Should().Be(application.Id);
            inDb.JobStageId.Should().Be(stage.Id);
        }

        [Fact]
        public async Task CreateInterviewAsync_FKRelationshipsAreValid()
        {
            using var context = CreateInMemoryContext();
            var (candidate, application, stage) = await SeedPrerequisitesAsync(context);
            var service = new InterviewService(context);
            var scheduledAt = DateTime.UtcNow.AddDays(7);

            var result = await service.CreateInterviewAsync(candidate.Id, application.Id, stage.Id, scheduledAt);

            var withNavProps = await context.Interviews
                .Include(i => i.Candidate)
                .Include(i => i.Application)
                .Include(i => i.JobStage)
                .FirstAsync(i => i.Id == result.Id);

            withNavProps.Candidate.Should().NotBeNull();
            withNavProps.Candidate!.Id.Should().Be(candidate.Id);
            withNavProps.Candidate.Email.Should().Be("jane.doe@example.com");

            withNavProps.Application.Should().NotBeNull();
            withNavProps.Application!.Id.Should().Be(application.Id);

            withNavProps.JobStage.Should().NotBeNull();
            withNavProps.JobStage!.Name.Should().Be("Interview");
        }

        [Fact]
        public async Task CreateInterviewAsync_ThrowsWhenCandidateNotFound()
        {
            using var context = CreateInMemoryContext();
            var (_, application, stage) = await SeedPrerequisitesAsync(context);
            var service = new InterviewService(context);

            var act = async () => await service.CreateInterviewAsync(9999, application.Id, stage.Id, DateTime.UtcNow);

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*9999*");
        }

        [Fact]
        public async Task CreateInterviewAsync_ThrowsWhenApplicationNotFound()
        {
            using var context = CreateInMemoryContext();
            var (candidate, _, stage) = await SeedPrerequisitesAsync(context);
            var service = new InterviewService(context);

            var act = async () => await service.CreateInterviewAsync(candidate.Id, 9999, stage.Id, DateTime.UtcNow);

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*9999*");
        }

        [Fact]
        public async Task CreateInterviewAsync_ThrowsWhenJobStageNotFound()
        {
            using var context = CreateInMemoryContext();
            var (candidate, application, _) = await SeedPrerequisitesAsync(context);
            var service = new InterviewService(context);

            var act = async () => await service.CreateInterviewAsync(candidate.Id, application.Id, 9999, DateTime.UtcNow);

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*9999*");
        }

        [Fact]
        public async Task CreateInterviewAsync_DoesNotCreateInterviewers()
        {
            using var context = CreateInMemoryContext();
            var (candidate, application, stage) = await SeedPrerequisitesAsync(context);
            var service = new InterviewService(context);

            await service.CreateInterviewAsync(candidate.Id, application.Id, stage.Id, DateTime.UtcNow.AddDays(3));

            var interviewers = await context.InterviewInterviewers.ToListAsync();
            interviewers.Should().BeEmpty();
        }
    }
}
