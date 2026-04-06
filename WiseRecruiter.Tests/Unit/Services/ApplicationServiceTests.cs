using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using JobPortal.Data;
using JobPortal.Models;
using JobPortal.Services.Implementations;
using JobPortal.Services.Interfaces;

namespace WiseRecruiter.Tests.Unit.Services
{
    /// <summary>
    /// Unit tests for ApplicationService core business logic.
    /// Uses EF Core InMemory for isolation while testing real EF queries.
    /// </summary>
    public class ApplicationServiceTests
    {
        private AppDbContext CreateInMemoryContext(string dbName = "test_db")
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: dbName + Guid.NewGuid())
                .Options;
            return new AppDbContext(options);
        }

        [Fact]
        public async Task CreateApplicationAsync_WithNoStageSpecified_AutoAssignsToFirstStage()
        {
            // Arrange
            using var context = CreateInMemoryContext();
            var job = new Job { Title = "Software Engineer" };
            var stage1 = new JobStage { JobId = 1, Name = "Applied", Order = 1 };
            var stage2 = new JobStage { JobId = 1, Name = "Interview", Order = 2 };

            context.Jobs.Add(job);
            context.SaveChanges();

            context.JobStages.AddRange(stage1, stage2);
            context.SaveChanges();

            var application = new Application
            {
                Name = "John Doe",
                Email = "john@example.com",
                City = "Sydney",
                JobId = 1,
                CurrentJobStageId = null
            };

            var service = new ApplicationService(context);

            // Act
            var result = await service.CreateApplicationAsync(application);

            // Assert
            result.CurrentJobStageId.Should().BeNull("new applications start with no custom stage");
            result.CandidateId.Should().BeGreaterThan(0);
            context.Candidates.Any(c => c.Id == result.CandidateId).Should().BeTrue();
            context.Applications.Should().HaveCount(1);
            context.Applications.First().CurrentJobStageId.Should().BeNull();
        }

        [Fact]
        public async Task CreateApplicationAsync_WithStageSpecified_UsesProvidedStage()
        {
            // Arrange
            using var context = CreateInMemoryContext();
            var job = new Job { Title = "Software Engineer" };
            var stage1 = new JobStage { JobId = 1, Name = "Applied", Order = 1 };
            var stage2 = new JobStage { JobId = 1, Name = "Interview", Order = 2 };

            context.Jobs.Add(job);
            context.SaveChanges();

            context.JobStages.AddRange(stage1, stage2);
            context.SaveChanges();

            var application = new Application
            {
                Name = "Jane Smith",
                Email = "jane@example.com",
                City = "Melbourne",
                JobId = 1,
                CurrentJobStageId = stage2.Id
            };

            var service = new ApplicationService(context);

            // Act
            var result = await service.CreateApplicationAsync(application);

            // Assert
            result.CurrentJobStageId.Should().BeNull("CreateApplicationAsync always starts with null stage");
            result.CandidateId.Should().BeGreaterThan(0);
            context.Candidates.Any(c => c.Id == result.CandidateId).Should().BeTrue();
        }

        [Fact]
        public async Task CreateApplicationAsync_WithNullApplication_ThrowsArgumentNullException()
        {
            // Arrange
            using var context = CreateInMemoryContext();
            var service = new ApplicationService(context);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => service.CreateApplicationAsync(null!));
        }

        [Fact]
        public async Task TransitionToStageAsync_WithValidStage_UpdatesApplicationStage()
        {
            // Arrange
            using var context = CreateInMemoryContext();
            var job = new Job { Title = "Software Engineer" };
            var stage1 = new JobStage { JobId = 1, Name = "Applied", Order = 1 };
            var stage2 = new JobStage { JobId = 1, Name = "Interview", Order = 2 };

            context.Jobs.Add(job);
            context.SaveChanges();

            context.JobStages.AddRange(stage1, stage2);
            context.SaveChanges();

            var application = new Application
            {
                Name = "John Doe",
                Email = "john@example.com",
                City = "Sydney",
                JobId = 1,
                CurrentJobStageId = stage1.Id
            };

            context.Applications.Add(application);
            context.SaveChanges();

            var service = new ApplicationService(context);

            // Act
            var result = await service.TransitionToStageAsync(application.Id, stage2.Id);

            // Assert
            result.Should().BeTrue();
            var updatedApp = context.Applications.First();
            updatedApp.CurrentJobStageId.Should().Be(stage2.Id);
        }

        [Fact]
        public async Task TransitionToStageAsync_WithStageBelongingToDifferentJob_ReturnsFalse()
        {
            // Arrange
            using var context = CreateInMemoryContext();
            var job1 = new Job { Title = "Software Engineer" };
            var job2 = new Job { Title = "Product Manager" };
            var stage1 = new JobStage { JobId = 1, Name = "Applied", Order = 1 };
            var stage2 = new JobStage { JobId = 2, Name = "Applied", Order = 1 };

            context.Jobs.AddRange(job1, job2);
            context.SaveChanges();

            context.JobStages.AddRange(stage1, stage2);
            context.SaveChanges();

            var application = new Application
            {
                Name = "John Doe",
                Email = "john@example.com",
                City = "Sydney",
                JobId = 1,
                CurrentJobStageId = stage1.Id
            };

            context.Applications.Add(application);
            context.SaveChanges();

            var service = new ApplicationService(context);

            // Act - try to move to a stage that belongs to job 2
            var result = await service.TransitionToStageAsync(application.Id, stage2.Id);

            // Assert
            result.Should().BeFalse();
            var app = context.Applications.First();
            app.CurrentJobStageId.Should().Be(stage1.Id); // Should not change
        }

        [Fact]
        public async Task TransitionToStageAsync_WithNonExistentApplication_ReturnsFalse()
        {
            // Arrange
            using var context = CreateInMemoryContext();
            var job = new Job { Title = "Software Engineer" };
            var stage = new JobStage { JobId = 1, Name = "Applied", Order = 1 };

            context.Jobs.Add(job);
            context.SaveChanges();

            context.JobStages.Add(stage);
            context.SaveChanges();

            var service = new ApplicationService(context);

            // Act
            var result = await service.TransitionToStageAsync(999, stage.Id);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task GetApplicationsSortedAsync_ByName_ReturnsSortedByNameAscending()
        {
            // Arrange
            using var context = CreateInMemoryContext();
            var job = new Job { Title = "Software Engineer" };
            var stage = new JobStage { JobId = 1, Name = "Applied", Order = 1 };

            context.Jobs.Add(job);
            context.SaveChanges();

            context.JobStages.Add(stage);
            context.SaveChanges();

            var app1 = new Application { Name = "Zoe Adams", Email = "zoe@example.com", City = "Sydney", JobId = 1, CurrentJobStageId = stage.Id };
            var app2 = new Application { Name = "Alice Brown", Email = "alice@example.com", City = "Melbourne", JobId = 1, CurrentJobStageId = stage.Id };
            var app3 = new Application { Name = "Bob Charlie", Email = "bob@example.com", City = "Brisbane", JobId = 1, CurrentJobStageId = stage.Id };

            context.Applications.AddRange(app1, app2, app3);
            context.SaveChanges();

            var service = new ApplicationService(context);

            // Act
            var result = await service.GetApplicationsSortedAsync(1, ApplicationSortBy.Name);

            // Assert
            result.Should().HaveCount(3);
            result[0].Name.Should().Be("Alice Brown");
            result[1].Name.Should().Be("Bob Charlie");
            result[2].Name.Should().Be("Zoe Adams");
        }

        [Fact]
        public async Task GetApplicationsSortedAsync_ByAppliedDate_ReturnsSortedByDateDescending()
        {
            // Arrange
            using var context = CreateInMemoryContext();
            var job = new Job { Title = "Software Engineer" };
            var stage = new JobStage { JobId = 1, Name = "Applied", Order = 1 };

            context.Jobs.Add(job);
            context.SaveChanges();

            context.JobStages.Add(stage);
            context.SaveChanges();

            var today = DateTime.UtcNow;
            var app1 = new Application { Name = "John", Email = "john@example.com", City = "Sydney", JobId = 1, CurrentJobStageId = stage.Id, AppliedDate = today.AddDays(-2) };
            var app2 = new Application { Name = "Jane", Email = "jane@example.com", City = "Melbourne", JobId = 1, CurrentJobStageId = stage.Id, AppliedDate = today };
            var app3 = new Application { Name = "Bob", Email = "bob@example.com", City = "Brisbane", JobId = 1, CurrentJobStageId = stage.Id, AppliedDate = today.AddDays(-1) };

            context.Applications.AddRange(app1, app2, app3);
            context.SaveChanges();

            var service = new ApplicationService(context);

            // Act
            var result = await service.GetApplicationsSortedAsync(1, ApplicationSortBy.AppliedDate);

            // Assert
            result.Should().HaveCount(3);
            result[0].Name.Should().Be("Jane"); // Most recent
            result[1].Name.Should().Be("Bob");
            result[2].Name.Should().Be("John"); // Oldest
        }

        [Fact]
        public async Task GetApplicationsSortedAsync_ByStage_ReturnsAppsSortedByStageOrder()
        {
            // Arrange
            using var context = CreateInMemoryContext();
            var job = new Job { Title = "Software Engineer" };

            context.Jobs.Add(job);
            context.SaveChanges();

            var stage1 = new JobStage { JobId = 1, Name = "Applied", Order = 1 };
            var stage2 = new JobStage { JobId = 1, Name = "Interview", Order = 2 };
            var stage3 = new JobStage { JobId = 1, Name = "Offer", Order = 3 };

            context.JobStages.AddRange(stage1, stage2, stage3);
            context.SaveChanges();

            // Create apps assigned to different stages (mixed order to test sorting)
            var app1 = new Application { Name = "Zoe", Email = "zoe@example.com", City = "Sydney", JobId = 1, CurrentJobStageId = stage3.Id };
            var app2 = new Application { Name = "Alice", Email = "alice@example.com", City = "Melbourne", JobId = 1, CurrentJobStageId = stage1.Id };
            var app3 = new Application { Name = "Bob", Email = "bob@example.com", City = "Brisbane", JobId = 1, CurrentJobStageId = stage2.Id };

            context.Applications.AddRange(app1, app2, app3);
            context.SaveChanges();

            var service = new ApplicationService(context);

            // Act
            var result = await service.GetApplicationsSortedAsync(1, ApplicationSortBy.Stage);

            // Assert
            result.Should().HaveCount(3);
            // Verify that results come back with stage ordering (Stage 1, then Stage 2, then Stage 3)
            // Even though we added them in reverse order (Stage 3, 1, 2)
            result[0].CurrentJobStageId.Should().Be(stage1.Id);
            result[1].CurrentJobStageId.Should().Be(stage2.Id);
            result[2].CurrentJobStageId.Should().Be(stage3.Id);
        }

        [Fact]
        public async Task GetApplicationsSortedAsync_WithEmptyJob_ReturnsEmptyList()
        {
            // Arrange
            using var context = CreateInMemoryContext();
            var job = new Job { Title = "Software Engineer" };
            context.Jobs.Add(job);
            context.SaveChanges();

            var service = new ApplicationService(context);

            // Act
            var result = await service.GetApplicationsSortedAsync(1, ApplicationSortBy.Name);

            // Assert
            result.Should().BeEmpty();
        }
    }
}
