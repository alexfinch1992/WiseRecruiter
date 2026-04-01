using System;
using System.Threading.Tasks;
using FluentAssertions;
using JobPortal.Data;
using JobPortal.Models;
using JobPortal.Services.Implementations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace WiseRecruiter.Tests.Integration
{
    public class JobControllerDeleteTests
    {
        private AppDbContext CreateInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: "job_controller_delete_db_" + Guid.NewGuid())
                .Options;

            return new AppDbContext(options);
        }

        [Fact]
        public async Task Delete_Get_WithReturnUrl_PopulatesViewBagAndReturnsView()
        {
            // Arrange
            using var context = CreateInMemoryContext();
            var job = new Job { Title = "Software Engineer", Description = "Build product features" };
            context.Jobs.Add(job);
            await context.SaveChangesAsync();

            var controller = new JobController(new JobQueryService(context), new JobCommandService(context));

            // Act
            var result = await controller.Delete(job.Id, "/Admin/Index");

            // Assert
            result.Should().BeOfType<ViewResult>();
            var viewResult = (ViewResult)result;
            viewResult.Model.Should().Be(job);
            ((string?)controller.ViewBag.ReturnUrl).Should().Be("/Admin/Index");
        }

        [Fact]
        public async Task DeleteConfirmed_Post_WithReturnUrl_DeletesJobAndRedirectsToReturnUrl()
        {
            // Arrange
            using var context = CreateInMemoryContext();
            var job = new Job { Title = "QA Engineer", Description = "Test platform quality" };
            context.Jobs.Add(job);
            await context.SaveChangesAsync();

            var controller = new JobController(new JobQueryService(context), new JobCommandService(context));

            // Act
            var result = await controller.DeleteConfirmed(job.Id, "/Admin/Index");

            // Assert
            result.Should().BeOfType<RedirectResult>();
            var redirectResult = (RedirectResult)result;
            redirectResult.Url.Should().Be("/Admin/Index");
            (await context.Jobs.FindAsync(job.Id)).Should().BeNull();
        }

        [Fact]
        public async Task Details_Get_WithTemplate_ReturnsJobWithTemplateName()
        {
            // Arrange
            using var context = CreateInMemoryContext();
            var template = new ScorecardTemplate { Name = "Engineering Template" };
            context.ScorecardTemplates.Add(template);
            await context.SaveChangesAsync();

            var job = new Job { Title = "Backend Engineer", Description = "Build APIs", ScorecardTemplateId = template.Id };
            context.Jobs.Add(job);
            await context.SaveChangesAsync();

            var controller = new JobController(new JobQueryService(context), new JobCommandService(context));

            // Act
            var result = await controller.Details(job.Id);

            // Assert
            result.Should().BeOfType<ViewResult>();
            var viewResult = (ViewResult)result;
            var model = viewResult.Model.Should().BeOfType<Job>().Subject;
            model.ScorecardTemplate.Should().NotBeNull();
            model.ScorecardTemplate!.Name.Should().Be("Engineering Template");
        }

        [Fact]
        public async Task Details_Get_WithoutTemplate_ReturnsJobWithNullTemplate()
        {
            // Arrange
            using var context = CreateInMemoryContext();
            var job = new Job { Title = "Frontend Engineer", Description = "Build UI", ScorecardTemplateId = null };
            context.Jobs.Add(job);
            await context.SaveChangesAsync();

            var controller = new JobController(new JobQueryService(context), new JobCommandService(context));

            // Act
            var result = await controller.Details(job.Id);

            // Assert
            result.Should().BeOfType<ViewResult>();
            var viewResult = (ViewResult)result;
            var model = viewResult.Model.Should().BeOfType<Job>().Subject;
            model.ScorecardTemplate.Should().BeNull();
        }

        [Fact]
        public async Task DeleteConfirmed_Post_WithoutReturnUrl_DeletesJobAndRedirectsToIndex()
        {
            // Arrange
            using var context = CreateInMemoryContext();
            var job = new Job { Title = "Product Manager", Description = "Own roadmap and delivery" };
            context.Jobs.Add(job);
            await context.SaveChangesAsync();

            var controller = new JobController(new JobQueryService(context), new JobCommandService(context));

            // Act
            var result = await controller.DeleteConfirmed(job.Id, null);

            // Assert
            result.Should().BeOfType<RedirectToActionResult>();
            var redirectResult = (RedirectToActionResult)result;
            redirectResult.ActionName.Should().Be(nameof(JobController.Index));
            (await context.Jobs.FindAsync(job.Id)).Should().BeNull();
        }
    }
}
