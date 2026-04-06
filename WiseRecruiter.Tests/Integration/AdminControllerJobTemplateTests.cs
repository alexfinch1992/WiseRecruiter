using System.Threading.Tasks;
using FluentAssertions;
using JobPortal.Data;
using JobPortal.Models;
using JobPortal.Models.ViewModels;
using JobPortal.Services.Implementations;
using JobPortal.Services.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using WiseRecruiter.Tests.Helpers;
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

        private static AdminController CreateAdminController(AppDbContext context, IScorecardTemplateService templateService)
            => AdminControllerFactory.Create(context, templateService: templateService);

        [Fact]
        public async Task CreateJob_Post_WithNonExistentScorecardTemplateId_AddsModelStateErrorAndReturnsView()
        {
            // Arrange
            using var context = CreateInMemoryContext();
            IScorecardTemplateService templateService = new ScorecardTemplateService(context);
            var controller = CreateAdminController(context, templateService);

            var vm = new CreateJobViewModel { Title = "Engineer", ScorecardTemplateId = 999 };

            // Act
            var result = await controller.Create(vm);

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

            var vm = new CreateJobViewModel { Title = "Engineer", ScorecardTemplateId = template.Id };

            // Act
            var result = await controller.Create(vm);

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

            var vm = new CreateJobViewModel { Title = "Engineer", ScorecardTemplateId = null };

            // Act
            var result = await controller.Create(vm);

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
