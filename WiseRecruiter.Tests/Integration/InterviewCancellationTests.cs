using System;
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
using WiseRecruiter.Tests.Helpers;
using Xunit;

namespace WiseRecruiter.Tests.Integration
{
    public class InterviewCancellationTests
    {
        private AppDbContext CreateInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: "interview_cancel_db_" + Guid.NewGuid())
                .Options;
            return new AppDbContext(options);
        }

        private static AdminController CreateAdminController(AppDbContext context)
            => AdminControllerFactory.Create(context);

        private static InterviewController CreateInterviewController(AppDbContext context)
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

        private static async Task<Interview> SeedInterviewAsync(AppDbContext context, Candidate candidate, Application application, JobStage stage)
        {
            var interview = new Interview
            {
                CandidateId = candidate.Id,
                ApplicationId = application.Id,
                JobStageId = stage.Id,
                ScheduledAt = DateTime.UtcNow.AddDays(7),
                CreatedAt = DateTime.UtcNow
            };
            context.Interviews.Add(interview);
            await context.SaveChangesAsync();
            return interview;
        }

        private static async Task SeedDefaultTemplateAsync(AppDbContext context)
        {
            var template = new ScorecardTemplate { Name = "Default Scorecard" };
            context.ScorecardTemplates.Add(template);
            await context.SaveChangesAsync();
        }

        [Fact]
        public async Task CancelInterview_SetsIsCancelledTrue()
        {
            using var context = CreateInMemoryContext();
            var (candidate, application, stage) = await SeedAsync(context);
            var interview = await SeedInterviewAsync(context, candidate, application, stage);
            var controller = CreateInterviewController(context);

            var result = await controller.CancelInterview(interview.Id, candidate.Id);

            result.Should().BeOfType<OkResult>();
            var updated = await context.Interviews.FindAsync(interview.Id);
            updated!.IsCancelled.Should().BeTrue();
        }

        [Fact]
        public async Task CancelInterview_ReturnsNotFound_WhenInterviewMissing()
        {
            using var context = CreateInMemoryContext();
            var (candidate, _, _) = await SeedAsync(context);
            var controller = CreateInterviewController(context);

            var result = await controller.CancelInterview(interviewId: 9999, candidateId: candidate.Id);

            result.Should().BeOfType<NotFoundResult>();
        }

        [Fact]
        public async Task CancelInterview_ReturnsBadRequest_WhenCandidateIdMismatch()
        {
            using var context = CreateInMemoryContext();
            var (candidate, application, stage) = await SeedAsync(context);
            var interview = await SeedInterviewAsync(context, candidate, application, stage);
            var controller = CreateInterviewController(context);

            var result = await controller.CancelInterview(interview.Id, candidateId: candidate.Id + 999);

            result.Should().BeOfType<BadRequestObjectResult>()
                .Which.Value.Should().Be("Invalid interview for this candidate.");
            var updated = await context.Interviews.FindAsync(interview.Id);
            updated!.IsCancelled.Should().BeFalse();
        }

        [Fact]
        public async Task CancelledInterview_NotShownInScorecardDropdown()
        {
            using var context = CreateInMemoryContext();
            await SeedDefaultTemplateAsync(context);
            var (candidate, application, stage) = await SeedAsync(context);
            var interview = await SeedInterviewAsync(context, candidate, application, stage);
            var controller = CreateInterviewController(context);

            // Cancel the interview
            await controller.CancelInterview(interview.Id, candidate.Id);

            // Trigger the CreateScorecard GET which populates AvailableInterviews
            var adminController = CreateAdminController(context);
            var result = await adminController.CreateScorecard(application.Id);

            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var model = viewResult.Model.Should().BeAssignableTo<JobPortal.Models.ViewModels.CreateScorecardViewModel>().Subject;
            model.AvailableInterviews.Should().NotContain(i => i.Id == interview.Id);
        }

        [Fact]
        public async Task CompletedInterview_NotShownInScorecardDropdown()
        {
            using var context = CreateInMemoryContext();
            await SeedDefaultTemplateAsync(context);
            var (candidate, application, stage) = await SeedAsync(context);
            var interview = await SeedInterviewAsync(context, candidate, application, stage);

            // Mark as completed directly
            interview.CompletedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();

            var controller = CreateAdminController(context);

            var result = await controller.CreateScorecard(application.Id);

            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var model = viewResult.Model.Should().BeAssignableTo<JobPortal.Models.ViewModels.CreateScorecardViewModel>().Subject;
            model.AvailableInterviews.Should().NotContain(i => i.Id == interview.Id);
        }
    }
}
