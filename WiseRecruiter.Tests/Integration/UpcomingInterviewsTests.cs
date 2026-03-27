using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using JobPortal.Data;
using JobPortal.Models;
using JobPortal.Models.ViewModels;
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
    public class UpcomingInterviewsTests
    {
        private AppDbContext CreateInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: "upcoming_interviews_db_" + Guid.NewGuid())
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
                templateService, jobService, scorecardAnalyticsService, interviewService)
            {
                TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())
            };

            return controller;
        }

        private static async Task<(Candidate candidate, Application application, JobStage stage)> SeedAsync(AppDbContext context)
        {
            var candidate = new Candidate
            {
                FirstName = "Jane",
                LastName = "Smith",
                Email = $"jane_{Guid.NewGuid()}@example.com",
                CreatedAt = DateTime.UtcNow
            };
            context.Candidates.Add(candidate);

            var job = new Job { Title = "Software Engineer", Description = "Test job" };
            context.Jobs.Add(job);
            await context.SaveChangesAsync();

            var stage = new JobStage { JobId = job.Id, Name = "Technical Interview", Order = 2 };
            context.JobStages.Add(stage);
            await context.SaveChangesAsync();

            var application = new Application
            {
                Name = "Jane Smith",
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
        public async Task UpcomingInterviews_OnlyReturnsUpcomingInterviews()
        {
            // Arrange
            using var context = CreateInMemoryContext();
            var (candidate, application, stage) = await SeedAsync(context);

            var upcomingInterview = new Interview
            {
                CandidateId = candidate.Id,
                ApplicationId = application.Id,
                JobStageId = stage.Id,
                ScheduledAt = DateTime.UtcNow.AddDays(3),
                CreatedAt = DateTime.UtcNow
            };

            var completedInterview = new Interview
            {
                CandidateId = candidate.Id,
                ApplicationId = application.Id,
                JobStageId = stage.Id,
                ScheduledAt = DateTime.UtcNow.AddDays(5),
                CompletedAt = DateTime.UtcNow.AddDays(-1),
                CreatedAt = DateTime.UtcNow
            };

            var cancelledInterview = new Interview
            {
                CandidateId = candidate.Id,
                ApplicationId = application.Id,
                JobStageId = stage.Id,
                ScheduledAt = DateTime.UtcNow.AddDays(7),
                IsCancelled = true,
                CreatedAt = DateTime.UtcNow
            };

            context.Interviews.AddRange(upcomingInterview, completedInterview, cancelledInterview);
            await context.SaveChangesAsync();

            var controller = CreateAdminController(context);

            // Act
            var result = await controller.Analytics();

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var model = viewResult.Model.Should().BeAssignableTo<AnalyticsViewModel>().Subject;

            model.UpcomingInterviews.Should().HaveCount(1);
            model.UpcomingInterviews[0].InterviewId.Should().Be(upcomingInterview.Id);
            model.UpcomingInterviews[0].CandidateName.Should().Be("Jane Smith");
            model.UpcomingInterviews[0].JobTitle.Should().Be("Software Engineer");
        }
    }
}
