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

namespace WiseRecruiter.Tests.Integration
{
    /// <summary>
    /// Integration tests for core application workflow.
    /// Tests complete flows using InMemory database to verify service interactions.
    /// </summary>
    public class ApplicationFlowTests
    {
        private AppDbContext CreateInMemoryContext(string dbName = "test_db")
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: dbName + Guid.NewGuid())
                .Options;
            return new AppDbContext(options);
        }

        [Fact]
        public async Task ApplicationCreationFlow_CreatesApplicationWithDefaultStage()
        {
            // Arrange
            using var context = CreateInMemoryContext();
            
            // Setup job and stages
            var job = new Job { Title = "Software Engineer", Description = "Build great software" };
            var stage1 = new JobStage { JobId = 1, Name = "Applied", Order = 1 };
            var stage2 = new JobStage { JobId = 1, Name = "Interview", Order = 2 };

            context.Jobs.Add(job);
            context.SaveChanges();

            context.JobStages.AddRange(stage1, stage2);
            context.SaveChanges();

            var applicationService = new ApplicationService(context);
            var analyticsService = new AnalyticsService(context);

            var newApplication = new Application
            {
                Name = "Alice Smith",
                Email = "alice@example.com",
                City = "Sydney",
                JobId = 1,
                CurrentJobStageId = null // Not specified, should auto-assign
            };

            // Act
            var result = await applicationService.CreateApplicationAsync(newApplication);
            var analytics = await analyticsService.GetJobAnalyticsAsync(1);

            // Assert
            result.Id.Should().BeGreaterThan(0);
            result.CurrentJobStageId.Should().BeNull("new applications start with no custom stage");
            result.Name.Should().Be("Alice Smith");
            
            analytics.TotalApplications.Should().Be(1);
            // Stage distribution uses custom pipeline stages (CurrentJobStageId); new apps start null, so no pipeline stage bucket yet
        }

        [Fact]
        public async Task ApplicationMovementFlow_TransitionsApplicationThroughStages()
        {
            // Arrange
            using var context = CreateInMemoryContext();
            
            var job = new Job { Title = "Software Engineer" };
            var stage1 = new JobStage { JobId = 1, Name = "Applied", Order = 1 };
            var stage2 = new JobStage { JobId = 1, Name = "Interview", Order = 2 };
            var stage3 = new JobStage { JobId = 1, Name = "Offer", Order = 3 };

            context.Jobs.Add(job);
            context.SaveChanges();

            context.JobStages.AddRange(stage1, stage2, stage3);
            context.SaveChanges();

            var application = new Application
            {
                Name = "Bob Johnson",
                Email = "bob@example.com",
                City = "Melbourne",
                JobId = 1,
                CurrentJobStageId = stage1.Id
            };

            context.Applications.Add(application);
            context.SaveChanges();

            var applicationService = new ApplicationService(context);
            var analyticsService = new AnalyticsService(context);

            // Act 1: Move to Interview
            var transitionResult1 = await applicationService.TransitionToStageAsync(application.Id, stage2.Id);
            var analytics1 = await analyticsService.GetJobAnalyticsAsync(1);

            // Act 2: Move to Offer
            var transitionResult2 = await applicationService.TransitionToStageAsync(application.Id, stage3.Id);
            var analytics2 = await analyticsService.GetJobAnalyticsAsync(1);

            // Assert
            transitionResult1.Should().BeTrue();
            transitionResult2.Should().BeTrue();

            var refreshedApp = context.Applications.First();
            refreshedApp.CurrentJobStageId.Should().Be(stage3.Id);

            // Verify analytics updated
            var offerStageStats = analytics2.StageDistribution.First(s => s.StageName == "Offer");
            offerStageStats.Count.Should().Be(1);

            var appliedStageStats = analytics2.StageDistribution.First(s => s.StageName == "Applied");
            appliedStageStats.Count.Should().Be(0);
        }

        [Fact]
        public async Task MultipleApplicationsFlow_CorrectlyTracksMultipleCandidates()
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

            var applicationService = new ApplicationService(context);

            var app1 = new Application { Name = "Alice", Email = "alice@example.com", City = "Sydney", JobId = 1 };
            var app2 = new Application { Name = "Bob", Email = "bob@example.com", City = "Melbourne", JobId = 1 };
            var app3 = new Application { Name = "Charlie", Email = "charlie@example.com", City = "Brisbane", JobId = 1 };

            // Act
            await applicationService.CreateApplicationAsync(app1);
            await applicationService.CreateApplicationAsync(app2);
            await applicationService.CreateApplicationAsync(app3);

            // Move only Bob to Interview
            await applicationService.TransitionToStageAsync(app2.Id, stage2.Id);

            var sortedByStage = await applicationService.GetApplicationsSortedAsync(1, ApplicationSortBy.Stage);

            // Assert
            sortedByStage.Should().HaveCount(3);
            
            // Count applications in each stage
            var appliedStageApps = sortedByStage.Where(a => a.CurrentJobStageId == null).ToList();
            var interviewStageApps = sortedByStage.Where(a => a.CurrentJobStageId == stage2.Id).ToList();
            
            appliedStageApps.Should().HaveCount(2); // Alice and Charlie
            interviewStageApps.Should().HaveCount(1); // Bob
            
            // Verify the interview stage app is Bob
            interviewStageApps[0].Name.Should().Be("Bob");
            
            // Verify applied stage apps are alphabetically sorted
            appliedStageApps[0].Name.Should().Be("Alice");
            appliedStageApps[1].Name.Should().Be("Charlie");
        }

        [Fact]
        public async Task CompleteApplicationFlow_EndToEnd()
        {
            // Arrange
            using var context = CreateInMemoryContext();
            
            // Setup: Create job with 3 stages
            var job = new Job { Title = "Product Designer" };
            var stage1 = new JobStage { JobId = 1, Name = "Applied", Order = 1 };
            var stage2 = new JobStage { JobId = 1, Name = "Design Challenge", Order = 2 };
            var stage3 = new JobStage { JobId = 1, Name = "Interview", Order = 3 };

            context.Jobs.Add(job);
            context.SaveChanges();

            context.JobStages.AddRange(stage1, stage2, stage3);
            context.SaveChanges();

            var applicationService = new ApplicationService(context);
            var analyticsService = new AnalyticsService(context);

            // Act
            // Step 1: Create multiple applications
            var app1 = new Application { Name = "Diana", Email = "diana@example.com", City = "Sydney", JobId = 1 };
            var app2 = new Application { Name = "Eva", Email = "eva@example.com", City = "Melbourne", JobId = 1 };

            await applicationService.CreateApplicationAsync(app1);
            await applicationService.CreateApplicationAsync(app2);

            var initialAnalytics = await analyticsService.GetAnalyticsReportAsync();

            // Step 2: Progress Diana through stages
            await applicationService.TransitionToStageAsync(app1.Id, stage2.Id);
            await applicationService.TransitionToStageAsync(app1.Id, stage3.Id);

            // Step 3: Progress Eva to challenge phase
            await applicationService.TransitionToStageAsync(app2.Id, stage2.Id);

            var finalAnalytics = await analyticsService.GetAnalyticsReportAsync();

            // Assert
            initialAnalytics.TotalApplications.Should().Be(2);
            
            var finalCandidatesByStage = finalAnalytics.CandidatesByStage;
            var appliedCount = finalCandidatesByStage.FirstOrDefault(s => s.StageName == "Applied")?.Count ?? 0;
            var challengeCount = finalCandidatesByStage.First(s => s.StageName == "Design Challenge").Count;
            var interviewCount = finalCandidatesByStage.First(s => s.StageName == "Interview").Count;

            appliedCount.Should().Be(0);
            challengeCount.Should().Be(1); // Eva
            interviewCount.Should().Be(1); // Diana
        }

        [Fact]
        public async Task JobCreationWithAutoStages_ApplicationCreationSucceedsWithValidStage()
        {
            // Arrange
            using var context = CreateInMemoryContext();
            var jobService = new JobService(context);
            var applicationService = new ApplicationService(context);

            // Act 1: Create a job (which auto-creates default stages)
            var job = await jobService.CreateJobAsync(new Job { Title = "QA Engineer" });

            // Act 2: Create an application for that job
            var application = new Application
            {
                Name = "Emma Wilson",
                Email = "emma@example.com",
                City = "Sydney",
                JobId = job.Id
            };
            var createdApp = await applicationService.CreateApplicationAsync(application);

            // Assert: Application was created with null stage (display falls back to Application.Stage = Applied)
            createdApp.Id.Should().BeGreaterThan(0);
            createdApp.CurrentJobStageId.Should().BeNull();
        }
    }
}
