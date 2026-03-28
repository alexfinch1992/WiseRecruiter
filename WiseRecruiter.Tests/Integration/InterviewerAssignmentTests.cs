using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using JobPortal.Data;
using JobPortal.Models;
using JobPortal.Services.Implementations;
using JobPortal.Services.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace WiseRecruiter.Tests.Integration
{
    public class InterviewerAssignmentTests
    {
        private AppDbContext CreateInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: "interviewer_assignment_db_" + Guid.NewGuid())
                .Options;
            return new AppDbContext(options);
        }

        private AdminController CreateAdminController(AppDbContext context)
        {
            IScorecardTemplateService templateService = new ScorecardTemplateService(context);
            IApplicationService applicationService = new ApplicationService(context);
            IAnalyticsService analyticsService = new AnalyticsService(context);
            IScorecardService scorecardService = new ScorecardService(context, templateService);
            IJobService jobService = new JobService(context);
            IScorecardAnalyticsService scorecardAnalyticsService = new ScorecardAnalyticsService(context);
            IInterviewService interviewService = new InterviewService(context);

            var controller = new AdminController(
                context,
                new Mock<IWebHostEnvironment>().Object,
                applicationService, analyticsService, scorecardService,
                templateService, jobService, scorecardAnalyticsService, interviewService, new RecommendationService(context), new ApplicationStageService(context, new RecommendationService(context)))
            {
                TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())
            };

            return controller;
        }

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

            var stage = new JobStage { JobId = job.Id, Name = "Technical Interview", Order = 2 };
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
        public async Task CreateInterview_WithTwoInterviewers_CreatesInterviewInterviewerRows()
        {
            // Arrange
            using var context = CreateInMemoryContext();
            var (candidate, application, stage) = await SeedAsync(context);

            var admin1 = new AdminUser { Username = "alice", PasswordHash = "hash1" };
            var admin2 = new AdminUser { Username = "bob", PasswordHash = "hash2" };
            context.AdminUsers.AddRange(admin1, admin2);
            await context.SaveChangesAsync();

            var controller = CreateAdminController(context);
            var scheduledAt = new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc);
            var interviewerIds = new List<int> { admin1.Id, admin2.Id };

            // Act
            var result = await controller.CreateInterview(candidate.Id, application.Id, stage.Id, scheduledAt, interviewerIds);

            // Assert - redirects
            result.Should().BeOfType<RedirectToActionResult>();

            // Assert - exactly 2 InterviewInterviewer rows
            var links = await context.InterviewInterviewers.ToListAsync();
            links.Should().HaveCount(2);

            // Assert - correct AdminUserIds
            var linkedAdminIds = links.Select(l => l.AdminUserId).OrderBy(x => x).ToList();
            linkedAdminIds.Should().BeEquivalentTo(new[] { admin1.Id, admin2.Id });
        }
    }
}
