using System;
using System.Collections.Generic;
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
    /// Unit tests for AnalyticsService aggregation and reporting logic.
    /// Validates stage breakdown, candidate counts, and edge cases.
    /// </summary>
    public class AnalyticsServiceTests
    {
        private AppDbContext CreateInMemoryContext(string dbName = "test_db")
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: dbName + Guid.NewGuid())
                .Options;
            return new AppDbContext(options);
        }

        [Fact]
        public async Task GetAnalyticsReportAsync_WithMultipleApplications_ReturnsCorrectStageCounts()
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

            var app1 = new Application { Name = "John", Email = "john@example.com", City = "Sydney", JobId = 1, CurrentJobStageId = stage1.Id };
            var app2 = new Application { Name = "Jane", Email = "jane@example.com", City = "Melbourne", JobId = 1, CurrentJobStageId = stage1.Id };
            var app3 = new Application { Name = "Bob", Email = "bob@example.com", City = "Brisbane", JobId = 1, CurrentJobStageId = stage2.Id };

            context.Applications.AddRange(app1, app2, app3);
            context.SaveChanges();

            var service = new AnalyticsService(context);

            // Act
            var report = await service.GetAnalyticsReportAsync();

            // Assert
            report.TotalApplications.Should().Be(3);
            report.TotalJobs.Should().Be(1);
            report.CandidatesByStage.Should().HaveCount(2);
            
            var appliedStage = report.CandidatesByStage.First(s => s.StageName == "Applied");
            appliedStage.Count.Should().Be(2);
            appliedStage.PercentageOfTotal.Should().Be(66.7);

            var interviewStage = report.CandidatesByStage.First(s => s.StageName == "Interview");
            interviewStage.Count.Should().Be(1);
            interviewStage.PercentageOfTotal.Should().Be(33.3);
        }

        [Fact]
        public async Task GetAnalyticsReportAsync_WithNoApplications_ReturnsZeroCounts()
        {
            // Arrange
            using var context = CreateInMemoryContext();
            var job = new Job { Title = "Software Engineer" };
            var stage = new JobStage { JobId = 1, Name = "Applied", Order = 1 };

            context.Jobs.Add(job);
            context.SaveChanges();

            context.JobStages.Add(stage);
            context.SaveChanges();

            var service = new AnalyticsService(context);

            // Act
            var report = await service.GetAnalyticsReportAsync();

            // Assert
            report.TotalApplications.Should().Be(0);
            report.TotalJobs.Should().Be(1);
            report.AverageApplicationsPerJob.Should().Be(0);
            foreach (var stageData in report.CandidatesByStage)
            {
                stageData.Count.Should().Be(0);
                stageData.PercentageOfTotal.Should().Be(0);
            }
        }

        [Fact]
        public async Task GetAnalyticsReportAsync_WithMultipleJobs_ComputesCorrectAverages()
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

            var app1 = new Application { Name = "John", Email = "john@example.com", City = "Sydney", JobId = 1, CurrentJobStageId = stage1.Id };
            var app2 = new Application { Name = "Jane", Email = "jane@example.com", City = "Melbourne", JobId = 1, CurrentJobStageId = stage1.Id };
            var app3 = new Application { Name = "Bob", Email = "bob@example.com", City = "Brisbane", JobId = 2, CurrentJobStageId = stage2.Id };

            context.Applications.AddRange(app1, app2, app3);
            context.SaveChanges();

            var service = new AnalyticsService(context);

            // Act
            var report = await service.GetAnalyticsReportAsync();

            // Assert
            report.TotalApplications.Should().Be(3);
            report.TotalJobs.Should().Be(2);
            report.AverageApplicationsPerJob.Should().Be(1.5);
        }

        [Fact]
        public async Task GetAnalyticsReportAsync_IncludesCorrectJobStats()
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

            var app1 = new Application { Name = "John", Email = "john@example.com", City = "Sydney", JobId = 1, CurrentJobStageId = stage1.Id };
            var app2 = new Application { Name = "Jane", Email = "jane@example.com", City = "Melbourne", JobId = 1, CurrentJobStageId = stage1.Id };
            var app3 = new Application { Name = "Bob", Email = "bob@example.com", City = "Brisbane", JobId = 2, CurrentJobStageId = stage2.Id };

            context.Applications.AddRange(app1, app2, app3);
            context.SaveChanges();

            var service = new AnalyticsService(context);

            // Act
            var report = await service.GetAnalyticsReportAsync();

            // Assert
            report.JobStats.Should().HaveCount(2);
            
            var softwareEngIneerStats = report.JobStats.First(j => j.JobTitle == "Software Engineer");
            softwareEngIneerStats.TotalApplications.Should().Be(2);

            var productManagerStats = report.JobStats.First(j => j.JobTitle == "Product Manager");
            productManagerStats.TotalApplications.Should().Be(1);

            // Should be ordered by application count (descending)
            report.JobStats[0].TotalApplications.Should().BeGreaterThanOrEqualTo(report.JobStats[1].TotalApplications);
        }

        [Fact]
        public async Task GetAnalyticsReportAsync_ComputesApplicationTrendsCorrectly()
        {
            // Arrange
            using var context = CreateInMemoryContext();
            var job = new Job { Title = "Software Engineer" };
            var stage = new JobStage { JobId = 1, Name = "Applied", Order = 1 };

            context.Jobs.Add(job);
            context.SaveChanges();

            context.JobStages.Add(stage);
            context.SaveChanges();

            var today = DateTime.UtcNow.Date;
            var app1 = new Application { Name = "John", Email = "john@example.com", City = "Sydney", JobId = 1, CurrentJobStageId = stage.Id, AppliedDate = today };
            var app2 = new Application { Name = "Jane", Email = "jane@example.com", City = "Melbourne", JobId = 1, CurrentJobStageId = stage.Id, AppliedDate = today };
            var app3 = new Application { Name = "Bob", Email = "bob@example.com", City = "Brisbane", JobId = 1, CurrentJobStageId = stage.Id, AppliedDate = today.AddDays(1) };

            context.Applications.AddRange(app1, app2, app3);
            context.SaveChanges();

            var service = new AnalyticsService(context);

            // Act
            var report = await service.GetAnalyticsReportAsync();

            // Assert
            report.ApplicationTrends.Should().HaveCount(2);
            report.ApplicationTrends[0].ApplicationsReceived.Should().Be(2);
            report.ApplicationTrends[0].CumulativeTotal.Should().Be(2);
            report.ApplicationTrends[1].ApplicationsReceived.Should().Be(1);
            report.ApplicationTrends[1].CumulativeTotal.Should().Be(3);
        }

        [Fact]
        public async Task GetJobAnalyticsAsync_WithValidJobId_ReturnsCorrectJobData()
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

            var app1 = new Application { Name = "John", Email = "john@example.com", City = "Sydney", JobId = 1, CurrentJobStageId = stage1.Id };
            var app2 = new Application { Name = "Jane", Email = "jane@example.com", City = "Melbourne", JobId = 1, CurrentJobStageId = stage2.Id };

            context.Applications.AddRange(app1, app2);
            context.SaveChanges();

            var service = new AnalyticsService(context);

            // Act
            var analytics = await service.GetJobAnalyticsAsync(1);

            // Assert
            analytics.JobId.Should().Be(1);
            analytics.JobTitle.Should().Be("Software Engineer");
            analytics.TotalApplications.Should().Be(2);
            analytics.StageDistribution.Should().HaveCount(2);
        }

        [Fact]
        public async Task GetJobAnalyticsAsync_WithInvalidJobId_ThrowsKeyNotFoundException()
        {
            // Arrange
            using var context = CreateInMemoryContext();
            var service = new AnalyticsService(context);

            // Act & Assert
            await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetJobAnalyticsAsync(999));
        }

        [Fact]
        public async Task GetJobAnalyticsAsync_WithNoApplications_ReturnsZeroCount()
        {
            // Arrange
            using var context = CreateInMemoryContext();
            var job = new Job { Title = "Software Engineer" };
            var stage = new JobStage { JobId = 1, Name = "Applied", Order = 1 };

            context.Jobs.Add(job);
            context.SaveChanges();

            context.JobStages.Add(stage);
            context.SaveChanges();

            var service = new AnalyticsService(context);

            // Act
            var analytics = await service.GetJobAnalyticsAsync(1);

            // Assert
            analytics.TotalApplications.Should().Be(0);
            foreach (var stageData in analytics.StageDistribution)
            {
                stageData.Count.Should().Be(0);
            }
        }
    }
}
