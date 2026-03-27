using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using JobPortal.Data;
using JobPortal.Models;
using JobPortal.Services.Implementations;
using JobPortal.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace WiseRecruiter.Tests.Unit.Services
{
    /// <summary>
    /// Unit tests for JobService auto-stage creation logic.
    /// Verifies that creating a job automatically creates default stages.
    /// </summary>
    public class JobServiceTests
    {
        private AppDbContext CreateInMemoryContext(string dbName = "job_service_db")
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: dbName + Guid.NewGuid())
                .Options;
            return new AppDbContext(options);
        }

        [Fact]
        public async Task CreateJobAsync_CreatesJobWithDefaultStages()
        {
            // Arrange
            using var context = CreateInMemoryContext();
            var service = new JobService(context);
            var job = new Job { Title = "Software Engineer", Description = "Build great software" };

            // Act
            var result = await service.CreateJobAsync(job);

            // Assert
            result.Id.Should().BeGreaterThan(0);
            result.Title.Should().Be("Software Engineer");

            // Verify the job was saved
            var savedJob = await context.Jobs.FindAsync(result.Id);
            savedJob.Should().NotBeNull();
            savedJob!.Title.Should().Be("Software Engineer");

            // Verify default stages were created
            var stages = await context.JobStages.Where(s => s.JobId == result.Id).OrderBy(s => s.Order).ToListAsync();
            stages.Should().HaveCount(3);
            stages[0].Name.Should().Be("Applied");
            stages[0].Order.Should().Be(1);
            stages[1].Name.Should().Be("Interview");
            stages[1].Order.Should().Be(2);
            stages[2].Name.Should().Be("Offer");
            stages[2].Order.Should().Be(3);
        }

        [Fact]
        public async Task CreateJobAsync_StagesAreLinkedToCorrectJob()
        {
            // Arrange
            using var context = CreateInMemoryContext();
            var service = new JobService(context);
            var job1 = new Job { Title = "Job 1" };
            var job2 = new Job { Title = "Job 2" };

            // Act
            var createdJob1 = await service.CreateJobAsync(job1);
            var createdJob2 = await service.CreateJobAsync(job2);

            // Assert
            var job1Stages = await context.JobStages.Where(s => s.JobId == createdJob1.Id).ToListAsync();
            var job2Stages = await context.JobStages.Where(s => s.JobId == createdJob2.Id).ToListAsync();

            job1Stages.Should().HaveCount(3);
            job2Stages.Should().HaveCount(3);

            // Verify they are different jobs' stages
            job1Stages.All(s => s.JobId == createdJob1.Id).Should().BeTrue();
            job2Stages.All(s => s.JobId == createdJob2.Id).Should().BeTrue();
            job1Stages.First().Id.Should().NotBe(job2Stages.First().Id);
        }

        [Fact]
        public async Task CreateJobAsync_WithNullJob_ThrowsArgumentNullException()
        {
            // Arrange
            using var context = CreateInMemoryContext();
            var service = new JobService(context);

            // Act & Assert
            Func<Task> act = async () => await service.CreateJobAsync(null!);
            await act.Should().ThrowAsync<ArgumentNullException>();
        }

        [Fact]
        public async Task CreateJobAsync_WithEmptyTitle_ThrowsArgumentException()
        {
            // Arrange
            using var context = CreateInMemoryContext();
            var service = new JobService(context);
            var job = new Job { Title = "", Description = "Description" };

            // Act & Assert
            Func<Task> act = async () => await service.CreateJobAsync(job);
            await act.Should().ThrowAsync<ArgumentException>().WithMessage("*title*");
        }

        [Fact]
        public async Task CreateJobAsync_WithWhitespaceTitle_ThrowsArgumentException()
        {
            // Arrange
            using var context = CreateInMemoryContext();
            var service = new JobService(context);
            var job = new Job { Title = "   ", Description = "Description" };

            // Act & Assert
            Func<Task> act = async () => await service.CreateJobAsync(job);
            await act.Should().ThrowAsync<ArgumentException>().WithMessage("*title*");
        }

        [Fact]
        public async Task CreateJobAsync_StagesCanBeRetrievedForApplication()
        {
            // Arrange
            using var context = CreateInMemoryContext();
            var service = new JobService(context);
            var job = new Job { Title = "Backend Developer" };

            // Act
            var createdJob = await service.CreateJobAsync(job);

            // Query to mimic Application creation flow
            var firstStage = await context.JobStages
                .Where(s => s.JobId == createdJob.Id)
                .OrderBy(s => s.Order)
                .FirstOrDefaultAsync();

            // Assert
            firstStage.Should().NotBeNull();
            firstStage!.Name.Should().Be("Applied");
            firstStage.JobId.Should().Be(createdJob.Id);
        }

        [Fact]
        public async Task CreateJobAsync_MultipleCallsCreateUniqueStageSets()
        {
            // Arrange
            using var context = CreateInMemoryContext();
            var service = new JobService(context);

            // Act
            var job1 = await service.CreateJobAsync(new Job { Title = "Job A" });
            var job2 = await service.CreateJobAsync(new Job { Title = "Job B" });
            var job3 = await service.CreateJobAsync(new Job { Title = "Job C" });

            // Assert
            var totalStages = await context.JobStages.CountAsync();
            totalStages.Should().Be(9); // 3 jobs × 3 stages each

            var job1StageIds = await context.JobStages
                .Where(s => s.JobId == job1.Id)
                .Select(s => s.Id)
                .ToListAsync();
            var job2StageIds = await context.JobStages
                .Where(s => s.JobId == job2.Id)
                .Select(s => s.Id)
                .ToListAsync();
            var job3StageIds = await context.JobStages
                .Where(s => s.JobId == job3.Id)
                .Select(s => s.Id)
                .ToListAsync();

            // Verify no overlap
            job1StageIds.Intersect(job2StageIds).Should().BeEmpty();
            job2StageIds.Intersect(job3StageIds).Should().BeEmpty();
            job1StageIds.Intersect(job3StageIds).Should().BeEmpty();
        }
    }
}
