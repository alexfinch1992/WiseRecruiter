using System;
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
    /// Guardrail tests for invalid states and failure paths that must remain safe.
    /// </summary>
    public class DataIntegrityTests
    {
        private AppDbContext CreateInMemoryContext(string dbName = "integrity_db")
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: dbName + Guid.NewGuid())
                .Options;
            return new AppDbContext(options);
        }

        [Fact]
        public async Task CreateApplicationAsync_WithoutAvailableStage_ThrowsControlledException()
        {
            using var context = CreateInMemoryContext();
            context.Jobs.Add(new Job { Title = "Backend Engineer" });
            await context.SaveChangesAsync();

            var service = new ApplicationService(context);
            var application = new Application
            {
                Name = "Casey",
                Email = "casey@example.com",
                City = "Sydney",
                JobId = 1,
                CurrentJobStageId = null
            };

            Func<Task> act = async () => await service.CreateApplicationAsync(application);

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*valid stage*");
        }

        [Fact]
        public async Task CreateApplicationAsync_WithInvalidJobId_FailsValidation()
        {
            using var context = CreateInMemoryContext();
            var service = new ApplicationService(context);

            var application = new Application
            {
                Name = "Taylor",
                Email = "taylor@example.com",
                City = "Melbourne",
                JobId = 0
            };

            Func<Task> act = async () => await service.CreateApplicationAsync(application);

            await act.Should().ThrowAsync<ArgumentException>()
                .WithMessage("*JobId*");
        }

        [Fact]
        public async Task CreateApplicationAsync_ForNonExistentJob_FailsGracefully()
        {
            using var context = CreateInMemoryContext();
            var service = new ApplicationService(context);

            var application = new Application
            {
                Name = "Jordan",
                Email = "jordan@example.com",
                City = "Brisbane",
                JobId = 999
            };

            Func<Task> act = async () => await service.CreateApplicationAsync(application);

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*does not exist*");
        }

        [Fact]
        public async Task TransitionToStageAsync_WhenTargetStageDoesNotExist_ReturnsFalse()
        {
            using var context = CreateInMemoryContext();
            context.Jobs.Add(new Job { Title = "QA Engineer" });
            await context.SaveChangesAsync();

            var appliedStage = new JobStage { JobId = 1, Name = "Applied", Order = 1 };
            context.JobStages.Add(appliedStage);
            await context.SaveChangesAsync();

            var app = new Application
            {
                Name = "Morgan",
                Email = "morgan@example.com",
                City = "Perth",
                JobId = 1,
                CurrentJobStageId = appliedStage.Id
            };
            context.Applications.Add(app);
            await context.SaveChangesAsync();

            var service = new ApplicationService(context);

            var result = await service.TransitionToStageAsync(app.Id, 9999);

            result.Should().BeFalse();
            app.CurrentJobStageId.Should().Be(appliedStage.Id);
        }

        [Fact]
        public async Task TransitionToStageAsync_WhenSkippingRequiredStage_ReturnsFalse()
        {
            using var context = CreateInMemoryContext();
            context.Jobs.Add(new Job { Title = "Site Reliability Engineer" });
            await context.SaveChangesAsync();

            var stage1 = new JobStage { JobId = 1, Name = "Applied", Order = 1 };
            var stage2 = new JobStage { JobId = 1, Name = "Screen", Order = 2 };
            var stage3 = new JobStage { JobId = 1, Name = "Interview", Order = 3 };
            context.JobStages.AddRange(stage1, stage2, stage3);
            await context.SaveChangesAsync();

            var app = new Application
            {
                Name = "Alex",
                Email = "alex@example.com",
                City = "Adelaide",
                JobId = 1,
                CurrentJobStageId = stage1.Id
            };
            context.Applications.Add(app);
            await context.SaveChangesAsync();

            var service = new ApplicationService(context);

            var result = await service.TransitionToStageAsync(app.Id, stage3.Id);

            result.Should().BeFalse();
            app.CurrentJobStageId.Should().Be(stage1.Id);
        }

        [Fact]
        public async Task CoreServices_OnEmptyData_DoNotThrowUnexpectedExceptions()
        {
            using var context = CreateInMemoryContext();
            var applicationService = new ApplicationService(context);
            var analyticsService = new AnalyticsService(context);

            Func<Task> analyticsCall = async () => await analyticsService.GetAnalyticsReportAsync();
            Func<Task> retrievalCall = async () => await applicationService.GetApplicationsSortedAsync(999, ApplicationSortBy.Name);

            await analyticsCall.Should().NotThrowAsync();
            await retrievalCall.Should().NotThrowAsync();
        }
    }
}
