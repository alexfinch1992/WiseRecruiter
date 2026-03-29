using System.Threading.Tasks;
using FluentAssertions;
using JobPortal.Data;
using JobPortal.Models;
using JobPortal.Services.Implementations;
using JobPortal.Services.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace WiseRecruiter.Tests.Integration
{
    /// <summary>
    /// Tests for AdminController job create/edit with ScorecardTemplateId FK validation.
    /// Verifies the defensive validation guard prevents saving jobs with invalid template IDs.
    /// </summary>
    public class AdminControllerJobTemplateTests
    {
        private AppDbContext CreateInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: "admin_job_template_db_" + System.Guid.NewGuid())
                .Options;
            return new AppDbContext(options);
        }

        private AdminController CreateAdminController(AppDbContext context, IScorecardTemplateService templateService)
        {
            var webHostEnvironment = new Mock<IWebHostEnvironment>().Object;
            IApplicationService applicationService = new ApplicationService(context);
            IAnalyticsService analyticsService = new AnalyticsService(context);
            IScorecardService scorecardService = new ScorecardService(context, templateService);
            IJobService jobService = new JobService(context);
            return new AdminController(context, webHostEnvironment, applicationService, analyticsService, scorecardService, templateService, jobService, new ScorecardAnalyticsService(context), new InterviewService(context), new RecommendationService(context, new StageOrderService()), new ApplicationStageService(context, new RecommendationService(context, new StageOrderService())), new HiringPipelineService(), new GlobalSearchService(context));
        }

        [Fact]
        public async Task CreateJob_Post_WithNonExistentScorecardTemplateId_AddsModelStateErrorAndReturnsView()
        {
            // Arrange
            using var context = CreateInMemoryContext();
            IScorecardTemplateService templateService = new ScorecardTemplateService(context);
            var controller = CreateAdminController(context, templateService);

            var job = new Job { Title = "Engineer", ScorecardTemplateId = 999 };

            // Act
            var result = await controller.Create(job);

            // Assert
            result.Should().BeOfType<ViewResult>();
            controller.ModelState.ContainsKey("ScorecardTemplateId").Should().BeTrue();
            controller.ModelState["ScorecardTemplateId"]!.Errors.Should().NotBeEmpty();
            context.Jobs.Should().BeEmpty();
        }

        [Fact]
        public async Task CreateJob_Post_WithValidScorecardTemplateId_SavesJobSuccessfully()
        {
            // Arrange
            using var context = CreateInMemoryContext();
            IScorecardTemplateService templateService = new ScorecardTemplateService(context);
            var template = await templateService.CreateTemplate("Engineering Scorecard");
            var controller = CreateAdminController(context, templateService);

            var job = new Job { Title = "Engineer", ScorecardTemplateId = template.Id };

            // Act
            var result = await controller.Create(job);

            // Assert
            result.Should().BeOfType<RedirectToActionResult>();
            context.Jobs.Should().ContainSingle(j => j.Title == "Engineer" && j.ScorecardTemplateId == template.Id);
        }

        [Fact]
        public async Task CreateJob_Post_WithNullScorecardTemplateId_SavesJobSuccessfully()
        {
            // Arrange
            using var context = CreateInMemoryContext();
            IScorecardTemplateService templateService = new ScorecardTemplateService(context);
            var controller = CreateAdminController(context, templateService);

            var job = new Job { Title = "Engineer", ScorecardTemplateId = null };

            // Act
            var result = await controller.Create(job);

            // Assert
            result.Should().BeOfType<RedirectToActionResult>();
            context.Jobs.Should().ContainSingle(j => j.Title == "Engineer" && j.ScorecardTemplateId == null);
        }

        [Fact]
        public async Task EditJob_Post_WithNonExistentScorecardTemplateId_AddsModelStateErrorAndReturnsView()
        {
            // Arrange
            using var context = CreateInMemoryContext();
            var existingJob = new Job { Title = "DevOps Engineer", ScorecardTemplateId = null };
            context.Jobs.Add(existingJob);
            await context.SaveChangesAsync();

            IScorecardTemplateService templateService = new ScorecardTemplateService(context);
            var controller = CreateAdminController(context, templateService);

            var updatedJob = new Job { Id = existingJob.Id, Title = "DevOps Engineer", ScorecardTemplateId = 999 };

            // Act
            var result = await controller.Edit(existingJob.Id, updatedJob);

            // Assert
            result.Should().BeOfType<ViewResult>();
            controller.ModelState.ContainsKey("ScorecardTemplateId").Should().BeTrue();
            controller.ModelState["ScorecardTemplateId"]!.Errors.Should().NotBeEmpty();
        }
    }
}
