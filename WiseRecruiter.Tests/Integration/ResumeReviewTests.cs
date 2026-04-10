using System;
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
using WiseRecruiter.Tests.Helpers;
using Xunit;

namespace WiseRecruiter.Tests.Integration
{
    public class ResumeReviewTests
    {
        private AppDbContext CreateInMemoryContext() =>
            new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase("resume_review_" + Guid.NewGuid())
                .Options);

        private static AdminController CreateController(AppDbContext context)
            => AdminControllerFactory.Create(context);

        private async Task<(Job job, Application application)> SeedApplicationAsync(
            AppDbContext ctx,
            ApplicationStage stage = ApplicationStage.Applied,
            string? resumePath = "/uploads/resumes/test.pdf")
        {
            var job = new Job { Title = "Test Job" };
            ctx.Jobs.Add(job);
            await ctx.SaveChangesAsync();

            var application = new Application
            {
                Name = "Alice Smith",
                Email = "alice@test.com",
                City = "Sydney",
                JobId = job.Id,
                Stage = stage,
                ResumePath = resumePath
            };
            ctx.Applications.Add(application);
            await ctx.SaveChangesAsync();
            return (job, application);
        }

        // ── GET ResumeReview ──────────────────────────────────────────────────

        [Fact]
        public async Task ResumeReview_ReturnsOnlyAppliedApplications()
        {
            await using var ctx = CreateInMemoryContext();
            var (job, _) = await SeedApplicationAsync(ctx, ApplicationStage.Applied);
            // Add a Screen application that should NOT appear
            ctx.Applications.Add(new Application
            {
                Name = "Bob Screen",
                Email = "bob@test.com",
                City = "Sydney",
                JobId = job.Id,
                Stage = ApplicationStage.Screen,
                ResumePath = "/uploads/resumes/bob.pdf"
            });
            await ctx.SaveChangesAsync();

            var controller = CreateController(ctx);
            var result = await controller.ResumeReview(job.Id, 0);

            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var model = viewResult.Model.Should().BeOfType<ResumeReviewViewModel>().Subject;
            model.TotalCount.Should().Be(1);
        }

        [Fact]
        public async Task ResumeReview_IncludesApplicationsWithNullResumePath()
        {
            await using var ctx = CreateInMemoryContext();
            var (job, _) = await SeedApplicationAsync(ctx, ApplicationStage.Applied, resumePath: null);

            var controller = CreateController(ctx);
            var result = await controller.ResumeReview(job.Id, 0);

            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var model = viewResult.Model.Should().BeOfType<ResumeReviewViewModel>().Subject;
            model.TotalCount.Should().Be(1);
            model.HasResume.Should().BeFalse();
        }

        [Fact]
        public async Task ResumeReview_SetsHasResume_WhenResumePathPresent()
        {
            await using var ctx = CreateInMemoryContext();
            var (job, _) = await SeedApplicationAsync(ctx, ApplicationStage.Applied, resumePath: "/uploads/resumes/cv.pdf");

            var controller = CreateController(ctx);
            var result = await controller.ResumeReview(job.Id, 0);

            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var model = viewResult.Model.Should().BeOfType<ResumeReviewViewModel>().Subject;
            model.HasResume.Should().BeTrue();
            model.ResumeUrl.Should().Be("/uploads/resumes/cv.pdf");
        }

        [Fact]
        public async Task ResumeReview_WithNoAppliedApplications_ReturnsQueueEmptyState()
        {
            await using var ctx = CreateInMemoryContext();
            var job = new Job { Title = "Empty Job" };
            ctx.Jobs.Add(job);
            await ctx.SaveChangesAsync();

            var controller = CreateController(ctx);
            var result = await controller.ResumeReview(job.Id, 0);

            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var model = viewResult.Model.Should().BeOfType<ResumeReviewViewModel>().Subject;
            model.IsQueueEmpty.Should().BeTrue();
        }

        [Fact]
        public async Task ResumeReview_WithIndexBeyondCount_ReturnsQueueCompleteState()
        {
            await using var ctx = CreateInMemoryContext();
            var (job, _) = await SeedApplicationAsync(ctx, ApplicationStage.Applied);

            var controller = CreateController(ctx);
            var result = await controller.ResumeReview(job.Id, index: 5);

            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var model = viewResult.Model.Should().BeOfType<ResumeReviewViewModel>().Subject;
            model.IsQueueComplete.Should().BeTrue();
        }

        [Fact]
        public async Task ResumeReview_SetsCurrentIndex_AndTotalCount()
        {
            await using var ctx = CreateInMemoryContext();
            var job = new Job { Title = "Multi Job" };
            ctx.Jobs.Add(job);
            await ctx.SaveChangesAsync();

            ctx.Applications.AddRange(
                new Application { Name = "A1", Email = "a1@t.com", City = "X", JobId = job.Id, Stage = ApplicationStage.Applied },
                new Application { Name = "A2", Email = "a2@t.com", City = "X", JobId = job.Id, Stage = ApplicationStage.Applied }
            );
            await ctx.SaveChangesAsync();

            var controller = CreateController(ctx);
            var result = await controller.ResumeReview(job.Id, index: 1);

            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var model = viewResult.Model.Should().BeOfType<ResumeReviewViewModel>().Subject;
            model.CurrentIndex.Should().Be(1);
            model.TotalCount.Should().Be(2);
            model.CandidateName.Should().Be("A2");
        }

        // ── POST AdvanceToScreen ──────────────────────────────────────────────

        [Fact]
        public async Task AdvanceToScreen_WithAppliedApplication_AdvancesToScreen()
        {
            await using var ctx = CreateInMemoryContext();
            var (job, application) = await SeedApplicationAsync(ctx, ApplicationStage.Applied);

            var controller = CreateController(ctx);
            await controller.AdvanceToScreen(application.Id, job.Id, 0);

            var updated = await ctx.Applications.FindAsync(application.Id);
            updated!.Stage.Should().Be(ApplicationStage.Screen);
        }

        [Fact]
        public async Task AdvanceToScreen_WithAppliedApplication_RedirectsToResumeReview()
        {
            await using var ctx = CreateInMemoryContext();
            var (job, application) = await SeedApplicationAsync(ctx, ApplicationStage.Applied);

            var controller = CreateController(ctx);
            var result = await controller.AdvanceToScreen(application.Id, job.Id, 0);

            var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirect.ActionName.Should().Be("ResumeReview");
            redirect.RouteValues!["jobId"].Should().Be(job.Id);
            redirect.RouteValues["index"].Should().Be(0);
        }

        [Fact]
        public async Task AdvanceToScreen_WithNonAppliedApplication_ReturnsBadRequest()
        {
            await using var ctx = CreateInMemoryContext();
            var (job, application) = await SeedApplicationAsync(ctx, ApplicationStage.Screen);

            var controller = CreateController(ctx);
            var result = await controller.AdvanceToScreen(application.Id, job.Id, 0);

            result.Should().BeOfType<BadRequestObjectResult>();
        }

        // ── New edge-case tests (added TDD-first) ────────────────────────────

        [Fact]
        public async Task AdvanceToScreen_WithWrongJobId_ReturnsBadRequest()
        {
            // Advance an application that belongs to job1 but pass job2's id
            await using var ctx = CreateInMemoryContext();

            var job1 = new Job { Title = "Job One" };
            var job2 = new Job { Title = "Job Two" };
            ctx.Jobs.AddRange(job1, job2);
            await ctx.SaveChangesAsync();

            var application = new Application
            {
                Name = "Wrong Job",
                Email = "w@test.com",
                City = "X",
                JobId = job1.Id,
                Stage = ApplicationStage.Applied
            };
            ctx.Applications.Add(application);
            await ctx.SaveChangesAsync();

            var controller = CreateController(ctx);
            var result = await controller.AdvanceToScreen(application.Id, job2.Id, 0);

            result.Should().BeOfType<BadRequestResult>();
        }

        [Fact]
        public async Task ResumeReview_NegativeIndex_ClampsToZero()
        {
            await using var ctx = CreateInMemoryContext();
            var (job, _) = await SeedApplicationAsync(ctx, ApplicationStage.Applied);

            var controller = CreateController(ctx);
            var result = await controller.ResumeReview(job.Id, index: -5);

            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var model = viewResult.Model.Should().BeOfType<ResumeReviewViewModel>().Subject;
            model.CurrentIndex.Should().Be(0);
            model.ApplicationId.Should().NotBeNull();
        }

        [Fact]
        public async Task ResumeReview_NullResumePath_ResumeUrlIsNull()
        {
            await using var ctx = CreateInMemoryContext();
            var (job, _) = await SeedApplicationAsync(ctx, ApplicationStage.Applied, resumePath: null);

            var controller = CreateController(ctx);
            var result = await controller.ResumeReview(job.Id, 0);

            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var model = viewResult.Model.Should().BeOfType<ResumeReviewViewModel>().Subject;
            model.ResumeUrl.Should().BeNull();
            model.HasResume.Should().BeFalse();
        }

        [Fact]
        public async Task ResumeReview_NonNullResumePath_ResumeUrlMatchesPath()
        {
            await using var ctx = CreateInMemoryContext();
            var (job, _) = await SeedApplicationAsync(ctx, ApplicationStage.Applied, resumePath: "/uploads/resumes/test.pdf");

            var controller = CreateController(ctx);
            var result = await controller.ResumeReview(job.Id, 0);

            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var model = viewResult.Model.Should().BeOfType<ResumeReviewViewModel>().Subject;
            model.ResumeUrl.Should().Be("/uploads/resumes/test.pdf");
            model.HasResume.Should().BeTrue();
        }

        [Fact]
        public async Task AdvanceToScreen_DoesNotIncrementIndex_SoQueueShrinksIntoSameSlot()
        {
            // After advancing index=0 candidate, the next candidate slides into index=0
            await using var ctx = CreateInMemoryContext();
            var job = new Job { Title = "Two Candidates" };
            ctx.Jobs.Add(job);
            await ctx.SaveChangesAsync();

            var app1 = new Application { Name = "First", Email = "f@t.com", City = "X", JobId = job.Id, Stage = ApplicationStage.Applied };
            var app2 = new Application { Name = "Second", Email = "s@t.com", City = "X", JobId = job.Id, Stage = ApplicationStage.Applied };
            ctx.Applications.AddRange(app1, app2);
            await ctx.SaveChangesAsync();

            var controller = CreateController(ctx);
            // Advance app1 (at index 0); redirect stays at index 0
            var result = await controller.AdvanceToScreen(app1.Id, job.Id, 0);

            var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirect.RouteValues!["index"].Should().Be(0);

            // Now query what's at index 0: should be app2 (app1 is now Screen)
            var nextResult = await controller.ResumeReview(job.Id, 0);
            var model = ((ViewResult)nextResult).Model.Should().BeOfType<ResumeReviewViewModel>().Subject;
            model.CandidateName.Should().Be("Second");
            model.TotalCount.Should().Be(1);
        }
    }
}
